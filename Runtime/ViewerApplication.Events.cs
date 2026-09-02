using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Deucarian.TemplateViewer
{
    public sealed partial class ViewerApplication
    {
        private string currentRemoteEndpoint = string.Empty;

        /// <summary>
        /// Endpoint committed by the newest successfully completed viewer
        /// initialization. Generic presentation events use the same route as
        /// that ready viewer lifecycle.
        /// </summary>
        public string CurrentRemoteEndpoint =>
            Volatile.Read(ref currentRemoteEndpoint) ?? string.Empty;

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

        private bool TryCommitCurrentRemoteEndpoint(
            int generation,
            string remoteEndpoint,
            CancellationToken cancellationToken)
        {
            lock (initializationStateGate)
            {
                if (generation != initializationGeneration)
                {
                    return false;
                }

                cancellationToken.ThrowIfCancellationRequested();
                Volatile.Write(
                    ref currentRemoteEndpoint,
                    remoteEndpoint ?? string.Empty);
                return true;
            }
        }

        private void ClearCurrentRemoteEndpoint()
        {
            Volatile.Write(ref currentRemoteEndpoint, string.Empty);
        }
    }
}
