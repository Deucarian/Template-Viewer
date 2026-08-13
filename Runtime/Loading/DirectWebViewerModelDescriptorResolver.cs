using System;

namespace Deucarian.TemplateViewerWeb.Loading
{
    public sealed class DirectWebViewerModelDescriptorResolver :
        IWebViewerModelDescriptorResolver
    {
        public bool TryResolve(
            WebViewerInitializeRequest request,
            out WebViewerModelDescriptor descriptor,
            out string error)
        {
            descriptor = default;
            if (request == null)
            {
                error = "The initialization payload is required.";
                return false;
            }

            if (request.Revision < 0)
            {
                error = "revision cannot be negative.";
                return false;
            }

            string sourceUrl = string.IsNullOrWhiteSpace(request.ModelUrl)
                ? string.Empty
                : request.ModelUrl.Trim();
            if (sourceUrl.Length > 0 &&
                (!Uri.TryCreate(sourceUrl, UriKind.RelativeOrAbsolute, out Uri uri) ||
                 (uri.IsAbsoluteUri &&
                  uri.Scheme != Uri.UriSchemeHttp &&
                  uri.Scheme != Uri.UriSchemeHttps)))
            {
                error = "model_url must be an HTTP(S) URL or an API-relative endpoint.";
                return false;
            }

            descriptor = new WebViewerModelDescriptor(
                sourceUrl,
                request.ModelId,
                request.ModelVersion,
                request.CacheVersion,
                request.CacheHash,
                request.AppendPlatformQuery);
            error = string.Empty;
            return true;
        }
    }
}
