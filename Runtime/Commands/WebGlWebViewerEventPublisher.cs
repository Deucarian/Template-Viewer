using System;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.CommandRouting.WebGLIntegration;
using Newtonsoft.Json.Linq;

namespace Deucarian.TemplateViewerWeb.Commands
{
    public sealed class WebGlWebViewerEventPublisher : IWebViewerEventPublisher
    {
        private readonly WebGlCommandTransport transport;

        public WebGlWebViewerEventPublisher(WebGlCommandTransport commandTransport)
        {
            transport = commandTransport ??
                throw new ArgumentNullException(nameof(commandTransport));
        }

        public Task PublishAsync(
            string eventName,
            JObject payload,
            string remoteEndpoint,
            CancellationToken cancellationToken = default)
        {
            return transport.PublishEventAsync(
                eventName,
                payload,
                remoteEndpoint,
                cancellationToken);
        }
    }
}
