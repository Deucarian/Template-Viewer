using System;
using System.Collections.Generic;

namespace Deucarian.TemplateViewerWeb.Selection
{
    public sealed class WebViewerVisibilityController
    {
        private readonly WebViewerElementIndex index;
        private readonly SortedDictionary<string, bool> baseline;

        public WebViewerVisibilityController(WebViewerElementIndex elementIndex)
        {
            index = elementIndex ?? throw new ArgumentNullException(nameof(elementIndex));
            baseline = new SortedDictionary<string, bool>(StringComparer.Ordinal);
            foreach (string id in index.ElementIds)
            {
                index.TryGet(id, out WebViewerElement element);
                baseline[id] = element.gameObject.activeSelf;
            }
        }

        public bool TryApplySelection(
            IReadOnlyCollection<string> selectedIds,
            out string error)
        {
            if (selectedIds == null || selectedIds.Count == 0)
            {
                error = "At least one element ID is required.";
                return false;
            }

            var selected = new HashSet<string>(selectedIds, StringComparer.Ordinal);
            foreach (string id in selected)
            {
                if (!index.Contains(id))
                {
                    error = "Unknown element ID: " + id + ".";
                    return false;
                }
            }

            foreach (string id in index.ElementIds)
            {
                index.TryGet(id, out WebViewerElement element);
                bool visible = selected.Contains(id);
                if (element.gameObject.activeSelf != visible)
                {
                    element.gameObject.SetActive(visible);
                }
            }

            error = string.Empty;
            return true;
        }

        public void RestoreBaseline()
        {
            foreach (KeyValuePair<string, bool> pair in baseline)
            {
                if (index.TryGet(pair.Key, out WebViewerElement element) &&
                    element.gameObject.activeSelf != pair.Value)
                {
                    element.gameObject.SetActive(pair.Value);
                }
            }
        }
    }
}
