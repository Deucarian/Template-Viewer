using System;
using Deucarian.CommandRouting;
using Newtonsoft.Json.Linq;

namespace Deucarian.TemplateViewer
{
    /// <summary>
    /// Immutable view of the one canonical command_failed projection. Product
    /// features may mirror the payload to local legacy observers, but remote
    /// publication remains owned by the viewer composition root.
    /// </summary>
    public sealed class ViewerCommandFailureProjectionEventArgs : EventArgs
    {
        public const string EventName = "command_failed";

        private readonly JObject payload;

        private ViewerCommandFailureProjectionEventArgs(
            string command,
            string errorCode,
            string message,
            string remoteEndpoint,
            JObject canonicalPayload)
        {
            Command = command;
            ErrorCode = errorCode ?? string.Empty;
            Message = message ?? string.Empty;
            RemoteEndpoint = remoteEndpoint;
            payload = canonicalPayload == null
                ? new JObject()
                : (JObject)canonicalPayload.DeepClone();
        }

        public string Command { get; }

        public string ErrorCode { get; }

        public string Message { get; }

        /// <summary>
        /// The exact effective endpoint supplied by Command Routing for this
        /// route outcome.
        /// </summary>
        public string RemoteEndpoint { get; }

        /// <summary>
        /// Returns a defensive copy so observers cannot mutate the canonical
        /// payload or one another's view.
        /// </summary>
        public JObject Payload => (JObject)payload.DeepClone();

        internal ViewerCommandFailureProjectionEventArgs WithProductPayload(
            JObject productPayload)
        {
            JObject canonicalPayload = productPayload == null
                ? new JObject()
                : (JObject)productPayload.DeepClone();
            canonicalPayload["command"] = string.IsNullOrEmpty(Command)
                ? JValue.CreateNull()
                : new JValue(Command);
            canonicalPayload["error_code"] = ErrorCode;
            canonicalPayload["message"] = Message;
            return new ViewerCommandFailureProjectionEventArgs(
                Command,
                ErrorCode,
                Message,
                RemoteEndpoint,
                canonicalPayload);
        }

        internal static bool TryCreate(
            CommandRouteCompletedEventArgs route,
            out ViewerCommandFailureProjectionEventArgs projection)
        {
            CommandRouteOutcome outcome = route?.Outcome;
            CommandResult result = outcome?.Result;
            if (result == null || result.Succeeded)
            {
                projection = null;
                return false;
            }

            string routedCommand = outcome.Command?.CommandName;
            string command = string.IsNullOrWhiteSpace(routedCommand)
                ? null
                : routedCommand;
            MapFailure(
                result,
                command,
                out string errorCode,
                out string message);
            JObject canonicalPayload = result.Payload == null
                ? new JObject()
                : (JObject)result.Payload.DeepClone();
            canonicalPayload["command"] = string.IsNullOrEmpty(command)
                ? JValue.CreateNull()
                : new JValue(command);
            canonicalPayload["error_code"] = errorCode;
            canonicalPayload["message"] = message;

            projection = new ViewerCommandFailureProjectionEventArgs(
                command,
                errorCode,
                message,
                route.RemoteEndpoint,
                canonicalPayload);
            return true;
        }

        private static void MapFailure(
            CommandResult result,
            string command,
            out string errorCode,
            out string message)
        {
            switch (result.ErrorCode)
            {
                case CommandRoutingErrorCodes.EmptyMessage:
                case CommandRoutingErrorCodes.MalformedEnvelope:
                case CommandRoutingErrorCodes.MessageTooLarge:
                    errorCode = "invalid_json";
                    message = "Viewer command JSON could not be parsed.";
                    return;
                case CommandRoutingErrorCodes.MissingCommand:
                    errorCode = "missing_command";
                    message = "Viewer command requires a command name.";
                    return;
                case CommandRoutingErrorCodes.UnsupportedCommand:
                    errorCode = "unsupported_command";
                    message = "Unsupported viewer command: " +
                              (command ?? string.Empty) + ".";
                    return;
                default:
                    errorCode = result.ErrorCode;
                    message = result.Message;
                    return;
            }
        }
    }
}
