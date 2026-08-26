using System;
using Deucarian.ViewerShell;
using UnityEngine;

namespace Deucarian.TemplateViewer
{
    /// <summary>
    /// Projects the generic viewer lifecycle into the reusable viewer shell
    /// and the selected platform's external lifecycle sink.
    /// </summary>
    internal sealed class ViewerShellStatusAdapter : IDisposable
    {
        private readonly ViewerApplication application;
        private readonly ViewerShellPresenter shell;
        private readonly IViewerLifecycleStatusSink statusSink;
        private bool disposed;

        public ViewerShellStatusAdapter(
            ViewerApplication application,
            ViewerShellPresenter shell,
            IViewerLifecycleStatusSink lifecycleStatusSink)
        {
            this.application = application ??
                throw new ArgumentNullException(nameof(application));
            this.shell = shell;
            statusSink = lifecycleStatusSink ??
                throw new ArgumentNullException(nameof(lifecycleStatusSink));
            application.LifecycleChanged += OnLifecycleChanged;
            application.LoadingProgressChanged += OnLoadingProgressChanged;
            OnLifecycleChanged(application.Lifecycle);
        }

        internal ViewerShellStatusSnapshot LastSnapshot { get; private set; }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            application.LifecycleChanged -= OnLifecycleChanged;
            application.LoadingProgressChanged -= OnLoadingProgressChanged;
            disposed = true;
        }

        private void OnLifecycleChanged(ViewerLifecycleState lifecycle)
        {
            string diagnostics = FormatDiagnostics();
            switch (lifecycle)
            {
                case ViewerLifecycleState.Created:
                    statusSink.ReportLifecycle(
                        lifecycle,
                        "Waiting for viewer initialization");
                    Apply(ViewerShellStatusSnapshot.Uninitialized(
                        "Waiting for viewer initialization",
                        diagnostics));
                    break;
                case ViewerLifecycleState.Loading:
                    statusSink.ReportLifecycle(lifecycle, "Loading model");
                    Apply(ViewerShellStatusSnapshot.Loading(
                        "Loading model\u2026",
                        diagnostics));
                    break;
                case ViewerLifecycleState.Ready:
                    statusSink.ReportLifecycle(lifecycle, "Viewer ready");
                    Apply(ViewerShellStatusSnapshot.Ready(
                        "Ready \u2022 " + application.IndexedElementCount +
                        " elements",
                        diagnostics));
                    break;
                case ViewerLifecycleState.Failed:
                    statusSink.ReportLifecycle(
                        lifecycle,
                        "Viewer initialization failed");
                    Apply(ViewerShellStatusSnapshot.Error(
                        "Viewer initialization failed",
                        diagnostics));
                    break;
                case ViewerLifecycleState.Disposed:
                    statusSink.ReportLifecycle(lifecycle, "Viewer disposed");
                    Apply(ViewerShellStatusSnapshot.Uninitialized(
                        "Viewer disposed",
                        diagnostics));
                    break;
            }
        }

        private void OnLoadingProgressChanged(float normalized, string message)
        {
            string label = string.IsNullOrWhiteSpace(message)
                ? "Loading model"
                : message.Trim();
            Apply(ViewerShellStatusSnapshot.Loading(
                label + " \u2022 " +
                Mathf.RoundToInt(Mathf.Clamp01(normalized) * 100f) + "%",
                FormatDiagnostics()));
            statusSink.ReportLoadingProgress(
                "model",
                normalized,
                label);
        }

        private void Apply(ViewerShellStatusSnapshot snapshot)
        {
            LastSnapshot = snapshot;
            shell?.ApplyStatus(snapshot);
        }

        private string FormatDiagnostics()
        {
            string revision = application.LatestRevision >= 0
                ? application.LatestRevision.ToString()
                : "none";
            return "revision=" + revision +
                   "    elements=" + application.IndexedElementCount +
                   "    selected=" + application.SelectedElementCount;
        }
    }
}
