using System;
using System.Collections.Generic;
using Deucarian.API.Core;
using Deucarian.CommandRouting;
using Deucarian.Diagnostics;
using Deucarian.TemplateViewer.Commands;
using Deucarian.TemplateViewer.Diagnostics;
using Deucarian.TemplateViewer.Loading;
using Deucarian.ViewerAuthentication;
using Deucarian.ViewerRendering;
using Deucarian.ViewerShell;

namespace Deucarian.TemplateViewer
{
    public abstract partial class ViewerBootstrap
    {
        private void ComposeCore()
        {
            featureBehaviours = ViewerFeatureComposition.ResolveBehaviours(
                this,
                explicitFeatureBehaviours);
            EnsureSceneDependencies();
            ViewerRenderingInstaller rendering = ComposeRendering();
            referenceNavigation = ComposeReferenceNavigation(rendering) ??
                throw new InvalidOperationException(
                    "The viewer requires reference navigation.");
            shellPresenter = ComposeShell(rendering);
            referenceNavigation.BeginReferenceLoad();

            ComposeAuthentication(
                out IApiClient apiClient,
                out string apiBaseUrl,
                out IReadOnlyCollection<string> effectiveOrigins);
            modelLoader = new ObjectLoadingViewerModelLoader(
                this,
                apiClient,
                loadedModelParent,
                apiBaseUrl,
                effectiveOrigins);
            modelLoader.ProgressChanged += OnModelLoadingProgress;

            IViewerVisibilityFeatureFactory visibilityFactory =
                ResolveVisibilityFeatureFactory(featureBehaviours);
            ICommandHandler<ViewerApplication> initializationHandler =
                ViewerFeatureComposition.ResolveInitializationCommandHandler(
                    featureBehaviours);

            application = new ViewerApplication(
                new DirectViewerModelDescriptorResolver(),
                modelLoader,
                referenceNavigation,
                platformAdapter.EventPublisher,
                embeddedReferenceModel,
                authenticationSession,
                visibilityFactory);
            var authenticationEvents =
                new ViewerAuthenticationEventPublisher(
                    platformAdapter.EventPublisher,
                    platformAdapter.EventEndpoint);
            var handlers = new List<ICommandHandler<ViewerApplication>>(
                ViewerCommandHandlers.Create(
                    authenticationEvents,
                    includeGenericVisibilityCommands:
                        visibilityFactory == null,
                    initializationHandler: initializationHandler));

            AttachFeatures(handlers);
            commandRuntime = new CommandRoutingRuntime<ViewerApplication>(
                application,
                handlers,
                new CommandRoutingOptions(
                    historyCapacity: 64,
                    logSuccessfulCommands: false,
                    logFailedCommands: true));
            localCommandPort =
                GetComponent<CommandRoutePortBehaviour>() ??
                gameObject.AddComponent<CommandRoutePortBehaviour>();
            localCommandPort.Initialize(commandRuntime);
            diagnosticRegistration = DiagnosticProviderRegistry.Register(
                new ViewerApplicationDiagnosticProvider(application));
            shellStatusAdapter = new ViewerShellStatusAdapter(
                application,
                shellPresenter,
                platformAdapter.LifecycleStatusSink);

            commandTransportActivation =
                platformAdapter.ActivateCommandTransport(commandRuntime);
            if (commandTransportActivation == null)
            {
                throw new InvalidOperationException(
                    "The viewer platform adapter returned no command " +
                    "transport activation lease.");
            }
        }

        private void AttachFeatures(
            ICollection<ICommandHandler<ViewerApplication>> handlers)
        {
            for (int i = 0; i < featureBehaviours.Length; i++)
            {
                ViewerFeatureBehaviour feature = featureBehaviours[i];
                feature.Attach(application);
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
            ViewerShellStatusAdapter statusAdapter = shellStatusAdapter;
            shellStatusAdapter = null;
            TryCleanup(() => statusAdapter?.Dispose());

            IDisposable activation = commandTransportActivation;
            commandTransportActivation = null;
            TryCleanup(() => activation?.Dispose());

            CommandRoutePortBehaviour routePort = localCommandPort;
            CommandRoutingRuntime<ViewerApplication> runtime = commandRuntime;
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
