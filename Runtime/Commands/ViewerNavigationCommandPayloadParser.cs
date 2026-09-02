using System;
using Deucarian.CommandRouting;
using Deucarian.ViewerNavigation;
using Newtonsoft.Json.Linq;

namespace Deucarian.TemplateViewer.Commands
{
    internal static class ViewerCommandPayloadAccess
    {
        public static JToken Resolve(CommandEnvelope command)
        {
            if (command?.RawEnvelope != null &&
                command.RawEnvelope.TryGetValue(
                    "payload",
                    StringComparison.Ordinal,
                    out JToken rawPayload))
            {
                return rawPayload;
            }

            return command?.Payload;
        }
    }

    internal static class ViewerNavigationCommandPayloadParser
    {
        public static bool TryCreate(
            CommandEnvelope envelope,
            string normalizedCommandName,
            ViewerNavigationCommandKind kind,
            out ViewerNavigationCommand command,
            out string error)
        {
            if (kind == ViewerNavigationCommandKind.DirectAction)
            {
                command = new ViewerNavigationCommand
                {
                    Action = normalizedCommandName
                };
                error = string.Empty;
                return true;
            }

            JToken explicitNavigation = null;
            envelope?.RawEnvelope?.TryGetValue(
                "navigation",
                StringComparison.Ordinal,
                out explicitNavigation);
            JToken payload = explicitNavigation == null ||
                             explicitNavigation.Type == JTokenType.Null
                ? ViewerCommandPayloadAccess.Resolve(envelope)
                : explicitNavigation;

            if (kind == ViewerNavigationCommandKind.Mode &&
                payload?.Type == JTokenType.String)
            {
                command = new ViewerNavigationCommand
                {
                    Mode = payload.Value<string>()
                };
                error = string.Empty;
                return true;
            }

            if (!TryParse(payload, out command, out error))
            {
                return false;
            }

            if (kind == ViewerNavigationCommandKind.Mode &&
                string.IsNullOrWhiteSpace(command.Mode))
            {
                command.Mode = command.Action;
                command.Action = null;
            }

            return true;
        }

        private static bool TryParse(
            JToken payload,
            out ViewerNavigationCommand command,
            out string error)
        {
            command = new ViewerNavigationCommand();
            error = string.Empty;
            if (payload == null || payload.Type == JTokenType.Null)
            {
                return true;
            }

            if (payload.Type == JTokenType.String)
            {
                command.Action = payload.Value<string>();
                return true;
            }

            if (payload.Type == JTokenType.Float ||
                payload.Type == JTokenType.Integer)
            {
                float scalarSensitivity = payload.Value<float>();
                if (!IsFinite(scalarSensitivity))
                {
                    error =
                        "Navigation sensitivity must be a finite number.";
                    return false;
                }

                command.Sensitivity = scalarSensitivity;
                return true;
            }

            if (!(payload is JObject value))
            {
                error = "Navigation command payload must be an object, " +
                        "string, or number.";
                return false;
            }

            if (!TryReadString(value, "action", out string action, out error) ||
                !TryReadString(value, "mode", out string mode, out error) ||
                !TryReadString(value, "view", out string view, out error) ||
                !TryReadFloat(
                    value,
                    "sensitivity",
                    out float? sensitivity,
                    out error) ||
                !TryReadFloat(
                    value,
                    "global_sensitivity",
                    out float? globalSensitivity,
                    out error))
            {
                return false;
            }

            command.Action = action;
            command.Mode = mode;
            command.View = view;
            command.Sensitivity = sensitivity;
            command.GlobalSensitivity = globalSensitivity;
            return true;
        }

        private static bool TryReadString(
            JObject value,
            string propertyName,
            out string result,
            out string error)
        {
            result = null;
            error = string.Empty;
            if (!value.TryGetValue(
                    propertyName,
                    StringComparison.Ordinal,
                    out JToken token) ||
                token == null || token.Type == JTokenType.Null)
            {
                return true;
            }

            if (token.Type != JTokenType.String)
            {
                error = propertyName + " must be a string.";
                return false;
            }

            result = token.Value<string>();
            return true;
        }

        private static bool TryReadFloat(
            JObject value,
            string propertyName,
            out float? result,
            out string error)
        {
            result = null;
            error = string.Empty;
            if (!value.TryGetValue(
                    propertyName,
                    StringComparison.Ordinal,
                    out JToken token) ||
                token == null || token.Type == JTokenType.Null)
            {
                return true;
            }

            if (token.Type != JTokenType.Integer &&
                token.Type != JTokenType.Float)
            {
                error = propertyName + " must be a number.";
                return false;
            }

            float valueResult = token.Value<float>();
            if (!IsFinite(valueResult))
            {
                error = propertyName + " must be a finite number.";
                return false;
            }

            result = valueResult;
            return true;
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
