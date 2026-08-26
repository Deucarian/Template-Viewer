using System;
using Deucarian.CommandRouting;

namespace Deucarian.TemplateViewer
{
    /// <summary>
    /// Receives lifecycle state that must be projected outside the shared
    /// in-viewer shell, such as a browser host, desktop window, or XR shell.
    /// </summary>
    public interface IViewerLifecycleStatusSink
    {
        void ReportLifecycle(
            ViewerLifecycleState lifecycle,
            string message);

        void ReportLoadingProgress(
            string operationId,
            float normalized,
            string message);
    }

    /// <summary>
    /// Owns the host-specific boundary for one viewer composition. The core
    /// never selects a browser, desktop, or XR implementation itself.
    /// </summary>
    public interface IViewerPlatformAdapter : IDisposable
    {
        string PlatformId { get; }
        string EventEndpoint { get; }
        IViewerEventPublisher EventPublisher { get; }
        IViewerLifecycleStatusSink LifecycleStatusSink { get; }

        IDisposable ActivateCommandTransport(
            CommandRoutingRuntime<ViewerApplication> commandRuntime);
    }

    internal static class ViewerPlatformAdapterValidation
    {
        internal static void Validate(IViewerPlatformAdapter adapter)
        {
            if (adapter == null)
            {
                throw new InvalidOperationException(
                    "A viewer platform adapter is required.");
            }

            if (string.IsNullOrWhiteSpace(adapter.PlatformId))
            {
                throw new InvalidOperationException(
                    "The viewer platform adapter requires a stable ID.");
            }

            if (string.IsNullOrWhiteSpace(adapter.EventEndpoint))
            {
                throw new InvalidOperationException(
                    "The viewer platform adapter requires an event endpoint.");
            }

            if (adapter.EventPublisher == null)
            {
                throw new InvalidOperationException(
                    "The viewer platform adapter requires an event publisher.");
            }

            if (adapter.LifecycleStatusSink == null)
            {
                throw new InvalidOperationException(
                    "The viewer platform adapter requires a lifecycle status sink.");
            }
        }
    }
}
