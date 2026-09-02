using System;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.Authentication;

namespace Deucarian.TemplateViewer.Commands
{
    /// <summary>
    /// Adapts sanitized Viewer Authentication outcomes to the template's
    /// active platform event publisher.
    /// </summary>
    public sealed class ViewerAuthenticationEventPublisher :
        IAuthenticationEventPublisher
    {
        private readonly IViewerEventPublisher eventPublisher;
        private readonly string remoteEndpoint;
        private readonly Action<ViewerAuthenticationOutcomeEventArgs>
            notifyOutcome;

        public ViewerAuthenticationEventPublisher(
            IViewerEventPublisher publisher,
            string configuredRemoteEndpoint)
            : this(publisher, configuredRemoteEndpoint, null)
        {
        }

        internal ViewerAuthenticationEventPublisher(
            IViewerEventPublisher publisher,
            string configuredRemoteEndpoint,
            Action<ViewerAuthenticationOutcomeEventArgs> outcomeNotification)
        {
            eventPublisher = publisher ??
                throw new ArgumentNullException(nameof(publisher));
            remoteEndpoint = string.IsNullOrWhiteSpace(configuredRemoteEndpoint)
                ? throw new ArgumentException(
                    "A configured platform endpoint is required.",
                    nameof(configuredRemoteEndpoint))
                : configuredRemoteEndpoint.Trim();
            notifyOutcome = outcomeNotification;
        }

        public Task PublishAsync(
            string eventName,
            AuthenticationStatusSnapshot status,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (status == null)
            {
                throw new ArgumentNullException(nameof(status));
            }

            ViewerAuthenticationOutcomeEventArgs outcome =
                ViewerAuthenticationOutcomeEventArgs.Create(
                    eventName,
                    status);
            try
            {
                notifyOutcome?.Invoke(outcome);
            }
            catch (Exception)
            {
                // A local compatibility observer cannot suppress the one
                // authoritative platform publication.
            }

            return eventPublisher.PublishAsync(
                eventName,
                outcome.Payload,
                remoteEndpoint,
                cancellationToken);
        }
    }
}
