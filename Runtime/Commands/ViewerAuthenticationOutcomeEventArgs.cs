using System;
using System.Globalization;
using Deucarian.Authentication;
using Newtonsoft.Json.Linq;

namespace Deucarian.TemplateViewer
{
    /// <summary>
    /// Immutable view of one sanitized authentication outcome. Product
    /// features may mirror its payload to local legacy observers, while the
    /// viewer composition remains the sole remote publisher.
    /// </summary>
    public sealed class ViewerAuthenticationOutcomeEventArgs : EventArgs
    {
        private readonly JObject payload;

        private ViewerAuthenticationOutcomeEventArgs(
            string eventName,
            AuthenticationStatusSnapshot status,
            JObject canonicalPayload)
        {
            EventName = eventName;
            Status = status ?? throw new ArgumentNullException(nameof(status));
            payload = canonicalPayload ??
                throw new ArgumentNullException(nameof(canonicalPayload));
        }

        public string EventName { get; }

        public AuthenticationStatusSnapshot Status { get; }

        /// <summary>
        /// Returns a defensive copy so local observers cannot mutate the
        /// canonical projection or the payload sent through the platform.
        /// </summary>
        public JObject Payload => (JObject)payload.DeepClone();

        internal static ViewerAuthenticationOutcomeEventArgs Create(
            string eventName,
            AuthenticationStatusSnapshot status)
        {
            if (status == null)
            {
                throw new ArgumentNullException(nameof(status));
            }

            var canonicalPayload = new JObject
            {
                ["status"] = status.Status.ToString(),
                ["has_access_token"] = status.HasAccessToken,
                ["can_refresh"] = status.CanRefresh,
                ["expiry_known"] = status.ExpiresAtUtc.HasValue
            };
            if (status.ExpiresAtUtc.HasValue)
            {
                canonicalPayload["expires_at_utc"] =
                    status.ExpiresAtUtc.Value.ToUniversalTime().ToString(
                        "O",
                        CultureInfo.InvariantCulture);
            }

            return new ViewerAuthenticationOutcomeEventArgs(
                eventName,
                status,
                canonicalPayload);
        }
    }
}
