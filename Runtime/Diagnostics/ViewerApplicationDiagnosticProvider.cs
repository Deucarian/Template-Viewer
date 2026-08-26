using Deucarian.Diagnostics;
using Deucarian.ViewerAuthentication;

namespace Deucarian.TemplateViewer.Diagnostics
{
    public sealed class ViewerApplicationDiagnosticProvider : IDiagnosticProvider
    {
        private readonly ViewerApplication application;

        public ViewerApplicationDiagnosticProvider(ViewerApplication viewerApplication)
        {
            application = viewerApplication;
        }

        public string ProviderId => "template-viewer";
        public string DisplayName => "Viewer Template";

        public void Collect(DiagnosticReportBuilder builder)
        {
            ViewerLifecycleState lifecycle = application?.Lifecycle ??
                ViewerLifecycleState.Disposed;
            DiagnosticSeverity severity = lifecycle == ViewerLifecycleState.Failed
                ? DiagnosticSeverity.Error
                : lifecycle == ViewerLifecycleState.Ready
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
            ViewerAuthenticationStatusSnapshot authentication =
                application?.AuthenticationSession?.Status ??
                new ViewerAuthenticationStatusSnapshot(
                    ViewerAuthenticationStatus.Missing,
                    false,
                    false,
                    null);
            section.AddItem(
                "authentication",
                "Authentication",
                authentication.Status.ToString(),
                GetAuthenticationSeverity(authentication.Status));
        }

        private static DiagnosticSeverity GetAuthenticationSeverity(
            ViewerAuthenticationStatus status)
        {
            switch (status)
            {
                case ViewerAuthenticationStatus.Active:
                    return DiagnosticSeverity.Success;
                case ViewerAuthenticationStatus.Expired:
                    return DiagnosticSeverity.Error;
                case ViewerAuthenticationStatus.Expiring:
                case ViewerAuthenticationStatus.ExpiryUnknown:
                case ViewerAuthenticationStatus.Missing:
                default:
                    return DiagnosticSeverity.Warning;
            }
        }
    }
}
