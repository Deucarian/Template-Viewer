using System;
using System.Collections.Generic;
using System.Linq;

namespace Deucarian.TemplateViewerWeb.Selection
{
    public enum WebViewerSelectionOutcome
    {
        Applied,
        Stale,
        Invalid
    }

    public readonly struct WebViewerSelectionResult
    {
        public WebViewerSelectionResult(
            WebViewerSelectionOutcome outcome,
            long revision,
            string message)
        {
            Outcome = outcome;
            Revision = revision;
            Message = message ?? string.Empty;
        }

        public WebViewerSelectionOutcome Outcome { get; }
        public long Revision { get; }
        public string Message { get; }
        public bool Applied => Outcome == WebViewerSelectionOutcome.Applied;
    }

    public sealed class WebViewerSelectionStateOwner
    {
        private readonly WebViewerVisibilityController visibility;
        private string[] selectedIds = Array.Empty<string>();

        public WebViewerSelectionStateOwner(
            long initialRevision,
            WebViewerVisibilityController visibilityController)
        {
            LatestRevision = initialRevision;
            visibility = visibilityController ??
                throw new ArgumentNullException(nameof(visibilityController));
        }

        public event Action<long, IReadOnlyList<string>> Changed;

        public long LatestRevision { get; private set; }
        public IReadOnlyList<string> SelectedIds => selectedIds;

        public WebViewerSelectionResult Select(
            long revision,
            IEnumerable<string> requestedIds)
        {
            if (revision <= LatestRevision)
            {
                return new WebViewerSelectionResult(
                    WebViewerSelectionOutcome.Stale,
                    revision,
                    "The selection revision is stale.");
            }

            string[] normalized = (requestedIds ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            if (!visibility.TryApplySelection(normalized, out string error))
            {
                return new WebViewerSelectionResult(
                    WebViewerSelectionOutcome.Invalid,
                    revision,
                    error);
            }

            LatestRevision = revision;
            selectedIds = normalized;
            Changed?.Invoke(LatestRevision, selectedIds);
            return new WebViewerSelectionResult(
                WebViewerSelectionOutcome.Applied,
                revision,
                "Selection applied.");
        }

        public WebViewerSelectionResult Clear(long revision)
        {
            if (revision <= LatestRevision)
            {
                return new WebViewerSelectionResult(
                    WebViewerSelectionOutcome.Stale,
                    revision,
                    "The clear revision is stale.");
            }

            visibility.RestoreBaseline();
            LatestRevision = revision;
            selectedIds = Array.Empty<string>();
            Changed?.Invoke(LatestRevision, selectedIds);
            return new WebViewerSelectionResult(
                WebViewerSelectionOutcome.Applied,
                revision,
                "Baseline visibility restored.");
        }
    }
}
