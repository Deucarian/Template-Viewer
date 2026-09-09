using System;

namespace Deucarian.TemplateViewer
{
    public abstract partial class ViewerBootstrap
    {
        private IViewerLifecycleStatusSink earlyLifecycleStatusSink;

        /// <summary>
        /// Optionally supplies the platform's status sink before transport or
        /// presentation construction. It must not require either to exist.
        /// The same sink receives the application's normal lifecycle later.
        /// </summary>
        protected virtual IViewerLifecycleStatusSink
            CreateEarlyLifecycleStatusSink() => null;

        protected IViewerLifecycleStatusSink EarlyLifecycleStatusSink =>
            EnsureEarlyLifecycleStatusSink();

        /// <summary>Only a stable, credential-free code may be returned.</summary>
        protected virtual string CompositionFailureCode =>
            "viewer_composition_failed";

        private IViewerLifecycleStatusSink EnsureEarlyLifecycleStatusSink()
        {
            if (earlyLifecycleStatusSink == null)
            {
                earlyLifecycleStatusSink = CreateEarlyLifecycleStatusSink();
            }

            return earlyLifecycleStatusSink;
        }

        private void ReportCompositionFailure()
        {
            try
            {
                earlyLifecycleStatusSink?.ReportLifecycle(
                    ViewerLifecycleState.Failed,
                    CompositionFailureCode);
            }
            catch (Exception)
            {
                // A failed optional status sink must not mask the original
                // composition error or prevent deterministic cleanup.
            }
        }
    }
}
