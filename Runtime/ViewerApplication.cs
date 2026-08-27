using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.TemplateViewer.Selection;
using Deucarian.ViewerAuthentication;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Deucarian.TemplateViewer
{
    public sealed partial class ViewerApplication :
        IDisposable,
        IViewerAuthenticationHost
    {
        private readonly IViewerModelDescriptorResolver descriptorResolver;
        private readonly IViewerModelLoader modelLoader;
        private readonly IViewerReferenceNavigation navigation;
        private readonly IViewerEventPublisher eventPublisher;
        private readonly GameObject embeddedModel;
        private readonly IViewerAuthenticationSession authenticationSession;
        private readonly IViewerVisibilityFeatureFactory
            visibilityFeatureFactory;
        private readonly IViewerModelReadinessFeature modelReadinessFeature;
        private CancellationTokenSource initializationCancellation;
        private IViewerVisibilityFeature visibilityFeature;
        private ViewerSelectionStateOwner selection;
        private int initializationGeneration;
        private long latestRevision = -1;
        private bool disposed;

        public ViewerApplication(
            IViewerModelDescriptorResolver resolver,
            IViewerModelLoader loader,
            IViewerReferenceNavigation referenceNavigation,
            IViewerEventPublisher publisher,
            GameObject embeddedReferenceModel = null,
            IViewerAuthenticationSession viewerAuthentication = null,
            IViewerVisibilityFeatureFactory customVisibilityFeatureFactory = null,
            IViewerModelReadinessFeature customModelReadinessFeature = null)
        {
            descriptorResolver = resolver ??
                throw new ArgumentNullException(nameof(resolver));
            modelLoader = loader ?? throw new ArgumentNullException(nameof(loader));
            navigation = referenceNavigation ??
                throw new ArgumentNullException(nameof(referenceNavigation));
            eventPublisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
            embeddedModel = embeddedReferenceModel;
            authenticationSession = viewerAuthentication ??
                new ViewerAuthenticationSession();
            visibilityFeatureFactory = customVisibilityFeatureFactory;
            modelReadinessFeature = customModelReadinessFeature;
            Lifecycle = ViewerLifecycleState.Created;
            if (embeddedModel != null)
            {
                embeddedModel.SetActive(false);
            }
        }

        public event Action<ViewerLifecycleState> LifecycleChanged;
        public event Action<float, string> LoadingProgressChanged;
        public event Action<ViewerModelContext> ModelReady;
        public event Action<ViewerModelContext> ModelUnloading;

        public ViewerLifecycleState Lifecycle { get; private set; }
        public long LatestRevision => Interlocked.Read(ref latestRevision);
        public int IndexedElementCount =>
            visibilityFeature?.IndexedElementCount ?? 0;
        public int SelectedElementCount =>
            visibilityFeature?.SelectedElementCount ?? 0;
        public ViewerModelContext CurrentModel { get; private set; }
        public IViewerAuthenticationSession AuthenticationSession =>
            authenticationSession;

        /// <summary>
        /// Publishes a product-owned event through the active platform
        /// adapter's secured event route.
        /// </summary>
        public Task PublishEventAsync(
            string eventName,
            JObject payload,
            string remoteEndpoint,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(eventName))
            {
                throw new ArgumentException(
                    "An event name is required.",
                    nameof(eventName));
            }

            if (disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }

            return eventPublisher.PublishAsync(
                eventName.Trim(),
                payload ?? new JObject(),
                remoteEndpoint,
                cancellationToken);
        }

        public async Task<CommandOperationResult> InitializeAsync(
            ViewerInitializeRequest request,
            string remoteEndpoint,
            CancellationToken cancellationToken)
        {
            if (disposed)
            {
                return CommandOperationResult.Failure(
                    "viewer_disposed",
                    "The viewer application is disposed.");
            }

            if (!descriptorResolver.TryResolve(
                    request,
                    out ViewerModelDescriptor descriptor,
                    out string validationError))
            {
                return CommandOperationResult.Failure(
                    "invalid_initialization",
                    validationError);
            }

            if (!TryAdvanceRevision(request.Revision))
            {
                return CommandOperationResult.Failure(
                    "stale_revision",
                    "The initialization revision is stale.");
            }

            int generation = Interlocked.Increment(ref initializationGeneration);
            CancelInitialization();
            initializationCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            CancellationToken token = initializationCancellation.Token;
            try
            {
                ResetCurrentModel();
                SetLifecycle(ViewerLifecycleState.Loading);
                await eventPublisher.PublishAsync(
                    "viewer_loading",
                    new JObject { ["revision"] = request.Revision },
                    remoteEndpoint,
                    token);
                if (!IsInitializationCurrent(generation, token))
                {
                    return SupersededInitialization();
                }

                GameObject referenceRoot;
                if (descriptor.UsesEmbeddedModel)
                {
                    referenceRoot = embeddedModel;
                    if (referenceRoot != null)
                    {
                        referenceRoot.SetActive(true);
                    }
                }
                else
                {
                    ViewerModelLoadResult loadResult =
                        await modelLoader.LoadAsync(descriptor, token);
                    if (!IsInitializationCurrent(generation, token))
                    {
                        return SupersededInitialization();
                    }

                    if (loadResult == null || !loadResult.Succeeded)
                    {
                        return await FailInitializationAsync(
                            request.Revision,
                            loadResult?.Message ?? "The model did not load.",
                            remoteEndpoint,
                            generation,
                            token);
                    }

                    referenceRoot = loadResult.ReferenceRoot;
                }

                if (referenceRoot == null)
                {
                    return await FailInitializationAsync(
                        request.Revision,
                        "No embedded model or model_url was supplied.",
                        remoteEndpoint,
                        generation,
                        token);
                }

                if (!ViewerModelPresentation.TryPrepare(
                        referenceRoot.transform,
                        request,
                        out string presentationError))
                {
                    return await FailInitializationAsync(
                        request.Revision,
                        presentationError,
                        remoteEndpoint,
                        generation,
                        token);
                }

                var modelContext = new ViewerModelContext(
                    referenceRoot,
                    descriptor,
                    request.Revision);
                if (!TryCreateVisibilityFeature(
                        modelContext,
                        out IViewerVisibilityFeature createdFeature,
                        out string featureError))
                {
                    return await FailInitializationAsync(
                        request.Revision,
                        featureError,
                        remoteEndpoint,
                        generation,
                        token);
                }

                if (!IsInitializationCurrent(generation, token))
                {
                    return SupersededInitialization();
                }

                visibilityFeature = createdFeature;
                selection = (createdFeature as GenericViewerVisibilityFeature)
                    ?.Selection;
                if (!navigation.RegisterReference(referenceRoot, true, true))
                {
                    return await FailInitializationAsync(
                        request.Revision,
                        "The model contains no renderable reference bounds.",
                        remoteEndpoint,
                        generation,
                        token);
                }

                if (!IsInitializationCurrent(generation, token))
                {
                    return SupersededInitialization();
                }

                if (modelReadinessFeature != null)
                {
                    ViewerModelReadinessResult readiness =
                        await modelReadinessFeature.PrepareAsync(
                            modelContext,
                            remoteEndpoint,
                            token);
                    if (!IsInitializationCurrent(generation, token))
                    {
                        return SupersededInitialization();
                    }

                    if (readiness == null || !readiness.Succeeded)
                    {
                        return await FailInitializationAsync(
                            request.Revision,
                            readiness?.Message ??
                                "Product model preparation returned no result.",
                            remoteEndpoint,
                            generation,
                            token);
                    }
                }

                SetLifecycle(ViewerLifecycleState.Ready);
                CurrentModel = modelContext;
                NotifyModelReady(modelContext);
                await eventPublisher.PublishAsync(
                    "viewer_ready",
                    new JObject
                    {
                        ["revision"] = request.Revision,
                        ["model_id"] = descriptor.ModelId,
                        ["model_version"] = descriptor.ModelVersion,
                        ["element_count"] = IndexedElementCount
                    },
                    remoteEndpoint,
                    token);
                if (!IsInitializationCurrent(generation, token))
                {
                    return SupersededInitialization();
                }

                return CommandOperationResult.Success(new JObject
                {
                    ["revision"] = request.Revision,
                    ["element_count"] = IndexedElementCount
                });
            }
            catch (OperationCanceledException)
                when (IsInitializationSuperseded(generation, cancellationToken))
            {
                return SupersededInitialization();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return await FailInitializationAsync(
                    request.Revision,
                    "Viewer initialization failed unexpectedly.",
                    remoteEndpoint,
                    generation,
                    token);
            }
        }

        public async Task<CommandOperationResult> SelectAsync(
            ViewerSelectionRequest request,
            string remoteEndpoint,
            CancellationToken cancellationToken)
        {
            if (!TryGetReadySelection(out CommandOperationResult failure))
            {
                return failure;
            }

            if (request == null)
            {
                return CommandOperationResult.Failure(
                    "invalid_selection",
                    "The selection payload is required.");
            }

            ViewerSelectionResult result = selection.Select(
                request.Revision,
                request.ElementIds);
            if (!result.Applied)
            {
                return SelectionFailure(result);
            }

            TryAdvanceRevision(result.Revision);
            await eventPublisher.PublishAsync(
                "selection_applied",
                CreateSelectionEvent(result.Revision, selection.SelectedIds.Count, false),
                remoteEndpoint,
                cancellationToken);
            return CommandOperationResult.Success(
                CreateSelectionEvent(result.Revision, selection.SelectedIds.Count, false));
        }

        public async Task<CommandOperationResult> ClearAsync(
            ViewerRevisionRequest request,
            string remoteEndpoint,
            CancellationToken cancellationToken)
        {
            if (!TryGetReadySelection(out CommandOperationResult failure))
            {
                return failure;
            }

            if (request == null)
            {
                return CommandOperationResult.Failure(
                    "invalid_clear",
                    "The clear payload is required.");
            }

            ViewerSelectionResult result = selection.Clear(request.Revision);
            if (!result.Applied)
            {
                return SelectionFailure(result);
            }

            TryAdvanceRevision(result.Revision);
            await eventPublisher.PublishAsync(
                "selection_applied",
                CreateSelectionEvent(result.Revision, 0, true),
                remoteEndpoint,
                cancellationToken);
            return CommandOperationResult.Success(
                CreateSelectionEvent(result.Revision, 0, true));
        }

        public async Task<CommandOperationResult> DisposeViewerAsync(
            ViewerRevisionRequest request,
            string remoteEndpoint,
            CancellationToken cancellationToken)
        {
            if (disposed)
            {
                return CommandOperationResult.Success(
                    new JObject { ["already_disposed"] = true });
            }

            if (request == null || !TryAdvanceRevision(request.Revision))
            {
                return CommandOperationResult.Failure(
                    "stale_revision",
                    "A newer disposal revision is required.");
            }

            DisposeCore();
            await eventPublisher.PublishAsync(
                "viewer_disposed",
                new JObject { ["revision"] = request.Revision },
                remoteEndpoint,
                cancellationToken);
            return CommandOperationResult.Success(
                new JObject { ["revision"] = request.Revision });
        }

        public void ReportLoadingProgress(float normalized, string message)
        {
            LoadingProgressChanged?.Invoke(Mathf.Clamp01(normalized), message ?? string.Empty);
        }

        public void Dispose()
        {
            if (!disposed)
            {
                DisposeCore();
            }
        }

        private async Task<CommandOperationResult> FailInitializationAsync(
            long revision,
            string message,
            string remoteEndpoint,
            int generation,
            CancellationToken cancellationToken)
        {
            ResetCurrentModel();
            SetLifecycle(ViewerLifecycleState.Failed);
            try
            {
                await eventPublisher.PublishAsync(
                    "viewer_failed",
                    new JObject
                    {
                        ["revision"] = revision,
                        ["code"] = "initialization_failed",
                        ["message"] = message
                    },
                    remoteEndpoint,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                // Failure notification is best effort. The local lifecycle must
                // remain Failed even when the platform event route is unavailable.
            }
            if (!IsInitializationCurrent(generation, cancellationToken))
            {
                return SupersededInitialization();
            }

            return CommandOperationResult.Failure(
                "initialization_failed",
                message);
        }

        private bool IsInitializationCurrent(
            int generation,
            CancellationToken cancellationToken)
        {
            if (generation != Volatile.Read(ref initializationGeneration))
            {
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();
            return true;
        }

        private bool IsInitializationSuperseded(
            int generation,
            CancellationToken callerCancellationToken) =>
            generation != Volatile.Read(ref initializationGeneration) &&
            !callerCancellationToken.IsCancellationRequested;

        private bool TryAdvanceRevision(long revision)
        {
            while (true)
            {
                long current = Interlocked.Read(ref latestRevision);
                if (revision <= current)
                {
                    return false;
                }

                if (Interlocked.CompareExchange(
                        ref latestRevision,
                        revision,
                        current) == current)
                {
                    return true;
                }
            }
        }

        public bool TryRecordRevision(long revision) =>
            TryAdvanceRevision(revision);
    }
}
