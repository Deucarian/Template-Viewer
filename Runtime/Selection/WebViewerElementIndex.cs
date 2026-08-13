using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deucarian.TemplateViewerWeb.Selection
{
    public sealed class WebViewerElementIndex
    {
        private readonly SortedDictionary<string, WebViewerElement> elements;

        private WebViewerElementIndex(
            SortedDictionary<string, WebViewerElement> indexedElements)
        {
            elements = indexedElements;
        }

        public int Count => elements.Count;
        public IEnumerable<string> ElementIds => elements.Keys;

        public bool Contains(string elementId) =>
            !string.IsNullOrWhiteSpace(elementId) && elements.ContainsKey(elementId.Trim());

        public bool TryGet(string elementId, out WebViewerElement element)
        {
            element = null;
            return !string.IsNullOrWhiteSpace(elementId) &&
                   elements.TryGetValue(elementId.Trim(), out element);
        }

        public static bool TryCreate(
            GameObject root,
            out WebViewerElementIndex index,
            out string error)
        {
            index = null;
            if (root == null)
            {
                error = "A model root is required.";
                return false;
            }

            var result = new SortedDictionary<string, WebViewerElement>(
                StringComparer.Ordinal);
            WebViewerElement[] candidates =
                root.GetComponentsInChildren<WebViewerElement>(true);
            for (int i = 0; i < candidates.Length; i++)
            {
                WebViewerElement element = candidates[i];
                string id = element.ElementId;
                if (id.Length == 0)
                {
                    error = "Every WebViewerElement requires a stable ID.";
                    return false;
                }

                if (HasManagedAncestor(element.transform, root.transform))
                {
                    error = "Managed visibility elements cannot be nested: " + id + ".";
                    return false;
                }

                if (result.ContainsKey(id))
                {
                    error = "Duplicate WebViewerElement ID: " + id + ".";
                    return false;
                }

                result.Add(id, element);
            }

            if (result.Count == 0)
            {
                error = "The model contains no WebViewerElement identifiers.";
                return false;
            }

            index = new WebViewerElementIndex(result);
            error = string.Empty;
            return true;
        }

        private static bool HasManagedAncestor(Transform current, Transform root)
        {
            Transform parent = current.parent;
            while (parent != null && parent != root.parent)
            {
                if (parent.GetComponent<WebViewerElement>() != null)
                {
                    return true;
                }

                if (parent == root)
                {
                    break;
                }

                parent = parent.parent;
            }

            return false;
        }
    }
}
