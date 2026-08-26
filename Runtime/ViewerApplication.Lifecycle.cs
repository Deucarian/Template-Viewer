using System;
using System.Threading;
using Deucarian.TemplateViewer.Selection;
using Newtonsoft.Json.Linq;

namespace Deucarian.TemplateViewer
{
    public sealed partial class ViewerApplication
    {
        private static CommandOperationResult SupersededInitialization() =>
            CommandOperationResult.Failure(
                "superseded",
                "The initialization was superseded.");

        private bool TryGetReadySelection(out CommandOperationResult failure)
        {
            if (disposed)
            {
                failure = CommandOperationResult.Failure(
                    "viewer_disposed",
                    "The viewer application is disposed.");
                return false;
            }

            if (Lifecycle != ViewerLifecycleState.Ready || selection == null)
            {
                failure = CommandOperationResult.Failure(
                    "viewer_not_ready",
                    "Initialize the viewer before changing visibility.");
                return false;
            }

            failure = default;
            return true;
        }

        private static CommandOperationResult SelectionFailure(
            ViewerSelectionResult result)
        {
            string code = result.Outcome == ViewerSelectionOutcome.Stale
                ? "stale_revision"
                : "invalid_selection";
            return CommandOperationResult.Failure(code, result.Message);
        }

        private static JObject CreateSelectionEvent(
            long revision,
            int count,
            bool cleared) =>
            new JObject
            {
                ["revision"] = revision,
                ["selected_count"] = count,
                ["cleared"] = cleared
            };

        private void ResetCurrentModel()
        {
            ViewerModelContext model = CurrentModel;
            CurrentModel = null;
            if (model != null)
            {
                NotifyModelUnloading(model);
            }

            visibilityFeature?.Dispose();
            visibilityFeature = null;
            selection = null;
            modelLoader.Unload();
            if (embeddedModel != null)
            {
                embeddedModel.SetActive(false);
            }

            navigation.BeginReferenceLoad();
        }

        private void CancelInitialization()
        {
            if (initializationCancellation == null)
            {
                return;
            }

            initializationCancellation.Cancel();
            initializationCancellation.Dispose();
            initializationCancellation = null;
        }

        private void SetLifecycle(ViewerLifecycleState value)
        {
            if (Lifecycle == value)
            {
                return;
            }

            Lifecycle = value;
            LifecycleChanged?.Invoke(value);
        }

        private void DisposeCore()
        {
            disposed = true;
            Interlocked.Increment(ref initializationGeneration);
            CancelInitialization();
            ViewerModelContext model = CurrentModel;
            CurrentModel = null;
            if (model != null)
            {
                NotifyModelUnloading(model);
            }

            visibilityFeature?.Dispose();
            visibilityFeature = null;
            selection = null;
            modelLoader.Unload();
            if (embeddedModel != null)
            {
                embeddedModel.SetActive(false);
            }

            navigation.BeginReferenceLoad();
            modelLoader.Dispose();
            SetLifecycle(ViewerLifecycleState.Disposed);
        }

        private bool TryCreateVisibilityFeature(
            ViewerModelContext context,
            out IViewerVisibilityFeature feature,
            out string error)
        {
            if (visibilityFeatureFactory != null)
            {
                if (!visibilityFeatureFactory.TryCreate(
                        context,
                        out feature,
                        out error))
                {
                    feature?.Dispose();
                    feature = null;
                    error = string.IsNullOrWhiteSpace(error)
                        ? "The custom visibility feature could not be created."
                        : error.Trim();
                    return false;
                }

                if (feature == null)
                {
                    error = "The custom visibility factory returned no feature.";
                    return false;
                }

                error = string.Empty;
                return true;
            }

            if (!GenericViewerVisibilityFeature.TryCreate(
                    context,
                    out GenericViewerVisibilityFeature genericFeature,
                    out error))
            {
                feature = null;
                return false;
            }

            feature = genericFeature;
            return true;
        }

        private void NotifyModelReady(ViewerModelContext context) =>
            InvokeModelEvent(ModelReady, context);

        private void NotifyModelUnloading(ViewerModelContext context) =>
            InvokeModelEvent(ModelUnloading, context);

        private static void InvokeModelEvent(
            Action<ViewerModelContext> handlers,
            ViewerModelContext context)
        {
            if (handlers == null)
            {
                return;
            }

            foreach (Action<ViewerModelContext> handler in
                     handlers.GetInvocationList())
            {
                try
                {
                    handler(context);
                }
                catch (Exception)
                {
                    // Product observers cannot invalidate the core lifecycle.
                }
            }
        }
    }
}
