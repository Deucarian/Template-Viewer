using System;
using System.Collections.Generic;
using System.Linq;

namespace Deucarian.TemplateViewer.Selection
{
    public enum ViewerSelectionOutcome
    {
        Applied,
        Stale,
        Invalid
    }

    public readonly struct ViewerSelectionResult
    {
        public ViewerSelectionResult(
            ViewerSelectionOutcome outcome,
            long revision,
            string message)
        {
            Outcome = outcome;
            Revision = revision;
            Message = message ?? string.Empty;
        }

        public ViewerSelectionOutcome Outcome { get; }
        public long Revision { get; }
        public string Message { get; }
        public bool Applied => Outcome == ViewerSelectionOutcome.Applied;
    }

    public sealed class ViewerSelectionStateOwner
    {
        private readonly ViewerVisibilityController visibility;
        private string[] selectedIds = Array.Empty<string>();

        public ViewerSelectionStateOwner(
            long initialRevision,
            ViewerVisibilityController visibilityController)
        {
            LatestRevision = initialRevision;
            visibility = visibilityController ??
                throw new ArgumentNullException(nameof(visibilityController));
        }

        public event Action<long, IReadOnlyList<string>> Changed;

        public long LatestRevision { get; private set; }
        public IReadOnlyList<string> SelectedIds => selectedIds;

        public ViewerSelectionResult Select(
            long revision,
            IEnumerable<string> requestedIds)
        {
            if (revision <= LatestRevision)
            {
                return new ViewerSelectionResult(
                    ViewerSelectionOutcome.Stale,
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
                return new ViewerSelectionResult(
                    ViewerSelectionOutcome.Invalid,
                    revision,
                    error);
            }

            LatestRevision = revision;
            selectedIds = normalized;
            Changed?.Invoke(LatestRevision, selectedIds);
            return new ViewerSelectionResult(
                ViewerSelectionOutcome.Applied,
                revision,
                "Selection applied.");
        }

        public ViewerSelectionResult Clear(long revision)
        {
            if (revision <= LatestRevision)
            {
                return new ViewerSelectionResult(
                    ViewerSelectionOutcome.Stale,
                    revision,
                    "The clear revision is stale.");
            }

            visibility.RestoreBaseline();
            LatestRevision = revision;
            selectedIds = Array.Empty<string>();
            Changed?.Invoke(LatestRevision, selectedIds);
            return new ViewerSelectionResult(
                ViewerSelectionOutcome.Applied,
                revision,
                "Baseline visibility restored.");
        }
    }
}
