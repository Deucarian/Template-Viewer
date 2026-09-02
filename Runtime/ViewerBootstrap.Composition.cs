using System;
using System.Collections.Generic;
using System.Threading;
using Deucarian.API.Core;
using Deucarian.CommandRouting;
using Deucarian.Diagnostics;
using Deucarian.TemplateViewer.Commands;
using Deucarian.TemplateViewer.Diagnostics;
using Deucarian.TemplateViewer.Loading;
using Deucarian.Authentication;
using Deucarian.ViewerRendering;
using Deucarian.ViewerShell;
using Newtonsoft.Json.Linq;

namespace Deucarian.TemplateViewer
{
    public abstract partial class ViewerBootstrap
    {
        private void ComposeCore()
        {
            compositionStage = "discovering viewer features";
            featureBehaviours = ViewerFeatureComposition.ResolveBehaviours(
                this,
                explicitFeatureBehaviours);
            compositionStage = "creating scene presentation dependencies";
            EnsureSceneDependencies();
            compositionStage = "creating viewer rendering";
            ViewerRenderingInstaller rendering = ComposeRendering();
            compositionStage = "creating viewer navigation";
            referenceNavigation = ComposeReferenceNavigation(rendering) ??
                throw new InvalidOperationException(
                    "The viewer requires reference navigation.");
            compositionStage = "creating the viewer shell";
            shellPresenter = ComposeShell(rendering);
            referenceNavigation.BeginReferenceLoad();

            compositionStage = "creating the authenticated API connection";
            ComposeAuthentication(
                out IApiClient apiClient,
                out string apiBaseUrl,
                out IReadOnlyCollection<string> effectiveOrigins);
            compositionStage = "creating the model loader";
            modelLoader = new ObjectLoadingViewerModelLoader(
                this,
                apiClient,
                loadedModelParent,
                apiBaseUrl,
                effectiveOrigins);
            modelLoader.ProgressChanged += OnModelLoadingProgress;

            compositionStage = "resolving product feature ownership";
            IViewerVisibilityFeatureFactory visibilityFactory =
                ResolveVisibilityFeatureFactory(featureBehaviours);
            ICommandHandler<ViewerApplication> initializationHandler =
                ViewerFeatureComposition.ResolveInitializationCommandHandler(
                    featureBehaviours);
            IViewerModelReadinessFeature readinessFeature =
                ViewerFeatureComposition.ResolveModelReadinessFeature(
                    featureBehaviours);
            if (EnableModelRevealReadiness)
            {
                readinessFeature = ViewerModelReadinessComposition.Create(
                    this,
                    readinessFeature,
                    out modelRevealReadiness);
            }
            else
            {
                modelRevealReadiness = null;
            }

            compositionStage = "creating the viewer application";
            application = new ViewerApplication(
                new DirectViewerModelDescriptorResolver(),
                modelLoader,
                referenceNavigation,
                platformAdapter.EventPublisher,
                embeddedReferenceModel,
                authenticationSession,
                visibilityFactory,
                readinessFeature);
            compositionStage = "creating viewer command handlers";
            var authenticationEvents =
                new ViewerAuthenticationEventPublisher(
                    platformAdapter.EventPublisher,
                    platformAdapter.EventEndpoint,
                    OnAuthenticationOutcome);
            var handlers = new List<ICommandHandler<ViewerApplication>>(
                ViewerCommandHandlers.CreateWithPresentation(
                    authenticationEvents,
                    includeGenericVisibilityCommands:
                        visibilityFactory == null,
                    initializationHandler: initializationHandler,
                    navigationController: navigationInstaller?.Controller,
                    renderingController: rendering?.Controller));

            AttachFeatures(handlers);
            compositionStage = "registering viewer command handlers";
            commandRuntime = new CommandRoutingRuntime<ViewerApplication>(
                application,
                handlers,
                new CommandRoutingOptions(
                    historyCapacity: 64,
                    logSuccessfulCommands: false,
                    logFailedCommands: true));
            commandRuntime.Dispatcher.CommandCompleted += OnCommandCompleted;
            SynchronizationContext unityContext =
                SynchronizationContext.Current ??
                throw new InvalidOperationException(
                    "Viewer composition requires Unity's main-thread " +
                    "synchronization context.");
            commandFailureProjector = new ViewerCommandFailureProjector(
                commandRuntime,
                unityContext,
                application.PublishEventAsync,
                OnCommandFailureProjected,
                PrepareCommandFailureProjection);
            compositionStage = "creating the local command port";
            localCommandPort =
                GetComponent<CommandRoutePortBehaviour>() ??
                gameObject.AddComponent<CommandRoutePortBehaviour>();
            localCommandPort.Initialize(commandRuntime);
            compositionStage = "registering viewer diagnostics";
            diagnosticRegistration = DiagnosticProviderRegistry.Register(
                new ViewerApplicationDiagnosticProvider(application));
            compositionStage = "connecting viewer lifecycle status";
            shellStatusAdapter = new ViewerShellStatusAdapter(
                application,
                shellPresenter,
                platformAdapter.LifecycleStatusSink);

            compositionStage = "activating the platform command transport";
            commandTransportActivation =
                platformAdapter.ActivateCommandTransport(commandRuntime);
            if (commandTransportActivation == null)
            {
                throw new InvalidOperationException(
                    "The viewer platform adapter returned no command " +
                    "transport activation lease.");
            }

            if (rendering?.Controller != null)
            {
                compositionStage = "publishing initial display settings";
                displaySettingsEvents =
                    new ViewerDisplaySettingsEventPublisher(
                        application,
                        rendering.Controller,
                        platformAdapter.EventEndpoint);
                _ = displaySettingsEvents.PublishInitialAsync();
            }
        }

        private void AttachFeatures(
            ICollection<ICommandHandler<ViewerApplication>> handlers)
        {
            for (int i = 0; i < featureBehaviours.Length; i++)
            {
                ViewerFeatureBehaviour feature = featureBehaviours[i];
                compositionStage = "attaching product feature " +
                    feature.GetType().Name;
                feature.Attach(application);
                compositionStage = "creating command handlers for product " +
                    "feature " + feature.GetType().Name;
                IReadOnlyList<ICommandHandler<ViewerApplication>>
                    featureHandlers = feature.CreateCommandHandlers();
                if (featureHandlers == null)
                {
                    continue;
                }

                for (int j = 0; j < featureHandlers.Count; j++)
                {
                    if (featureHandlers[j] == null)
                    {
                        throw new InvalidOperationException(
                            "A viewer feature returned a null command handler.");
                    }

                    handlers.Add(featureHandlers[j]);
                }
            }
        }

        private void ReleaseComposition()
        {
            ViewerCommandFailureProjector failureProjector =
                commandFailureProjector;
            commandFailureProjector = null;
            TryCleanup(() => failureProjector?.Dispose());

            CommandRoutingRuntime<ViewerApplication> runtime = commandRuntime;
            if (runtime != null)
            {
                runtime.Dispatcher.CommandCompleted -= OnCommandCompleted;
            }

            ViewerDisplaySettingsEventPublisher settingsEvents =
                displaySettingsEvents;
            displaySettingsEvents = null;
            TryCleanup(() => settingsEvents?.Dispose());

            ViewerShellStatusAdapter statusAdapter = shellStatusAdapter;
            shellStatusAdapter = null;
            TryCleanup(() => statusAdapter?.Dispose());

            IDisposable activation = commandTransportActivation;
            commandTransportActivation = null;
            TryCleanup(() => activation?.Dispose());

            CommandRoutePortBehaviour routePort = localCommandPort;
            localCommandPort = null;
            TryCleanup(() => routePort?.Clear(runtime));

            ViewerApplication currentApplication = application;
            application = null;
            ViewerFeatureBehaviour[] features = featureBehaviours;
            featureBehaviours = Array.Empty<ViewerFeatureBehaviour>();
            for (int i = features.Length - 1; i >= 0; i--)
            {
                ViewerFeatureBehaviour feature = features[i];
                TryCleanup(() => feature?.Detach(currentApplication));
            }

            TryCleanup(() => currentApplication?.Dispose());
            ViewerModelRevealReadinessFeature revealReadiness =
                modelRevealReadiness;
            modelRevealReadiness = null;
            TryCleanup(() => revealReadiness?.Dispose());
            commandRuntime = null;
            TryCleanup(() => runtime?.Dispose());

            DiagnosticProviderRegistration diagnostics =
                diagnosticRegistration;
            diagnosticRegistration = null;
            TryCleanup(() => diagnostics?.Dispose());

            ViewerShellPresenter presenter = shellPresenter;
            shellPresenter = null;
            TryCleanup(() => presenter?.Dispose());

            referenceNavigation = null;
            navigationInstaller = null;
            renderingInstaller = null;

            ObjectLoadingViewerModelLoader loader = modelLoader;
            modelLoader = null;
            if (loader != null)
            {
                loader.ProgressChanged -= OnModelLoadingProgress;
                TryCleanup(loader.Dispose);
            }

            ReleaseAuthenticationComposition();

            IViewerPlatformAdapter adapter = platformAdapter;
            platformAdapter = null;
            TryCleanup(() => adapter?.Dispose());
        }

        private static IViewerVisibilityFeatureFactory
            ResolveVisibilityFeatureFactory(
                IReadOnlyList<ViewerFeatureBehaviour> features)
        {
            IViewerVisibilityFeatureFactory result = null;
            for (int i = 0; i < features.Count; i++)
            {
                IViewerVisibilityFeatureFactory candidate =
                    features[i].VisibilityFeatureFactory;
                if (candidate == null)
                {
                    continue;
                }

                if (result != null && !ReferenceEquals(result, candidate))
                {
                    throw new InvalidOperationException(
                        "Only one viewer feature may own model visibility.");
                }

                result = candidate;
            }

            return result;
        }

        private void OnCommandCompleted(
            object sender,
            CommandDispatchEventArgs eventArgs)
        {
            ViewerApplication currentApplication = application;
            ViewerFeatureBehaviour[] features = featureBehaviours;
            for (int index = 0; index < features.Length; index++)
            {
                ViewerFeatureBehaviour feature = features[index];
                TryCleanup(() => feature?.OnCommandCompleted(
                    currentApplication,
                    eventArgs));
            }
        }

        private void OnCommandFailureProjected(
            ViewerCommandFailureProjectionEventArgs eventArgs)
        {
            ViewerApplication currentApplication = application;
            ViewerFeatureBehaviour[] features = featureBehaviours;
            for (int index = 0; index < features.Length; index++)
            {
                ViewerFeatureBehaviour feature = features[index];
                TryCleanup(() => feature?.OnCommandFailureProjected(
                    currentApplication,
                    eventArgs));
            }
        }

        private ViewerCommandFailureProjectionEventArgs
            PrepareCommandFailureProjection(
                ViewerCommandFailureProjectionEventArgs eventArgs)
        {
            JObject payload = eventArgs.Payload;
            ViewerApplication currentApplication = application;
            ViewerFeatureBehaviour[] features = featureBehaviours;
            for (int index = 0; index < features.Length; index++)
            {
                ViewerFeatureBehaviour feature = features[index];
                TryCleanup(() =>
                    feature?.CustomizeCommandFailureProjection(
                        currentApplication,
                        eventArgs.Command,
                        payload));
            }

            return eventArgs.WithProductPayload(payload);
        }

        private void OnAuthenticationOutcome(
            ViewerAuthenticationOutcomeEventArgs eventArgs)
        {
            ViewerApplication currentApplication = application;
            ViewerFeatureBehaviour[] features = featureBehaviours;
            for (int index = 0; index < features.Length; index++)
            {
                ViewerFeatureBehaviour feature = features[index];
                TryCleanup(() => feature?.OnAuthenticationOutcome(
                    currentApplication,
                    eventArgs));
            }
        }

        private static void TryCleanup(Action cleanup)
        {
            try
            {
                cleanup?.Invoke();
            }
            catch (Exception)
            {
                // Cleanup continues so no later resource remains live.
            }
        }
    }
}
