using System;
using System.Text.RegularExpressions;

namespace Deucarian.TemplateViewer.Diagnostics
{
    internal static class ViewerCompositionDiagnostic
    {
        private const int MaximumDetailLength = 600;

        private static readonly Regex AbsoluteUrl = new Regex(
            @"https?://[^\s""'<>]+",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex SecretContent = new Regex(
            @"\b((access|refresh|id)[_-]?token|token|authorization|" +
            @"proxy[_-]?authorization|password|passwd|secret|" +
            @"client[_-]?secret|api[_-]?key|cookie|set[_-]?cookie|bearer)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex Whitespace = new Regex(
            @"\s+",
            RegexOptions.CultureInvariant);

        internal static string Format(
            string stage,
            Exception exception)
        {
            string normalizedStage = NormalizeStage(stage);
            string exceptionType = exception?.GetType().Name ??
                "UnknownException";
            string details = Sanitize(exception?.Message);
            return "Viewer composition failed while " + normalizedStage +
                   " (" + exceptionType + "): " + details + " " +
                   "Correct the named component or package setting, save " +
                   "the scene, and enter Play Mode again.";
        }

        internal static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "No additional configuration details were provided.";
            }

            string sanitized = AbsoluteUrl.Replace(value, "<redacted-url>");
            // Exception text is not a structured credential format. Redacting
            // only the next word leaks values in Bearer headers, JSON, quoted
            // or multiword assignments. Omit the entire sensitive detail;
            // the safe composition stage and exception type remain useful.
            if (SecretContent.IsMatch(sanitized))
            {
                return "Sensitive configuration details were omitted.";
            }
            sanitized = Whitespace.Replace(sanitized, " ").Trim();
            if (sanitized.Length <= MaximumDetailLength)
            {
                return sanitized;
            }

            return sanitized.Substring(0, MaximumDetailLength).TrimEnd() +
                   "…";
        }

        private static string NormalizeStage(string stage) =>
            string.IsNullOrWhiteSpace(stage)
                ? "starting the viewer"
                : Whitespace.Replace(stage, " ").Trim();
    }
}
