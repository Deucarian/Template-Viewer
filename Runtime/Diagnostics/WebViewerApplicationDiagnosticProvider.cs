using Deucarian.Diagnostics;

namespace Deucarian.TemplateViewerWeb.Diagnostics
{
    public sealed class WebViewerApplicationDiagnosticProvider : IDiagnosticProvider
    {
        private readonly WebViewerApplication application;

        public WebViewerApplicationDiagnosticProvider(WebViewerApplication viewerApplication)
        {
            application = viewerApplication;
        }

        public string ProviderId => "template-viewer-web";
        public string DisplayName => "Web Viewer Template";

        public void Collect(DiagnosticReportBuilder builder)
        {
            WebViewerLifecycleState lifecycle = application?.Lifecycle ??
                WebViewerLifecycleState.Disposed;
            DiagnosticSeverity severity = lifecycle == WebViewerLifecycleState.Failed
                ? DiagnosticSeverity.Error
                : lifecycle == WebViewerLifecycleState.Ready
                    ? DiagnosticSeverity.Success
                    : DiagnosticSeverity.Info;
            DiagnosticSection section = builder.AddSection(ProviderId, DisplayName);
            section.AddItem(
                "lifecycle",
                "Lifecycle",
                lifecycle.ToString(),
                severity);
            section.AddItem(
                "latest_revision",
                "Latest revision",
                (application?.LatestRevision ?? -1).ToString());
            section.AddItem(
                "indexed_elements",
                "Indexed elements",
                (application?.IndexedElementCount ?? 0).ToString());
            section.AddItem(
                "selected_elements",
                "Selected elements",
                (application?.SelectedElementCount ?? 0).ToString());
        }
    }
}
