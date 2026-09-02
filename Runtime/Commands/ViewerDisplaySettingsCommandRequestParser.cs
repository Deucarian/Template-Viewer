using System;
using Deucarian.ViewerRendering;
using Newtonsoft.Json.Linq;

namespace Deucarian.TemplateViewer.Commands
{
    internal static class ViewerDisplaySettingsCommandRequestParser
    {
        public static bool TryParse(
            JToken payload,
            out ViewerDisplaySettingsRequest request,
            out string error)
        {
            request = default;
            error = null;
            if (!(payload is JObject value))
            {
                error = "set_display_settings requires an object payload.";
                return false;
            }

            ViewerRenderingMode? renderingMode = null;
            bool? cameraRelativeLight = null;

            if (TryReadAliasedProperty(
                    value,
                    "rendering_mode",
                    "renderingMode",
                    out JToken renderingModeToken,
                    out error))
            {
                if (renderingModeToken == null ||
                    renderingModeToken.Type != JTokenType.String ||
                    !ViewerDisplaySettingsPayload.TryParseRenderingMode(
                        renderingModeToken.Value<string>(),
                        out ViewerRenderingMode parsedMode))
                {
                    error = "rendering_mode must be color_faithful or realistic.";
                    return false;
                }

                renderingMode = parsedMode;
            }
            else if (error != null)
            {
                return false;
            }

            if (TryReadAliasedProperty(
                    value,
                    "camera_relative_light",
                    "cameraRelativeLight",
                    out JToken cameraLightToken,
                    out error))
            {
                if (cameraLightToken == null ||
                    cameraLightToken.Type != JTokenType.Boolean)
                {
                    error = "camera_relative_light must be a boolean.";
                    return false;
                }

                cameraRelativeLight = cameraLightToken.Value<bool>();
            }
            else if (error != null)
            {
                return false;
            }

            if (!renderingMode.HasValue &&
                !cameraRelativeLight.HasValue)
            {
                error = "set_display_settings requires rendering_mode or " +
                        "camera_relative_light.";
                return false;
            }

            request = new ViewerDisplaySettingsRequest(
                renderingMode,
                cameraRelativeLight);
            return true;
        }

        private static bool TryReadAliasedProperty(
            JObject value,
            string canonicalName,
            string aliasName,
            out JToken result,
            out string error)
        {
            error = null;
            bool hasCanonical = value.TryGetValue(
                canonicalName,
                StringComparison.Ordinal,
                out JToken canonical);
            bool hasAlias = value.TryGetValue(
                aliasName,
                StringComparison.Ordinal,
                out JToken alias);
            if (hasCanonical &&
                hasAlias &&
                !JToken.DeepEquals(canonical, alias))
            {
                result = null;
                error = canonicalName + " and " + aliasName +
                        " cannot disagree.";
                return false;
            }

            result = hasCanonical ? canonical : alias;
            return hasCanonical || hasAlias;
        }
    }
}
