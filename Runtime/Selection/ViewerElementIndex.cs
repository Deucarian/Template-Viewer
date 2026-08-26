using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deucarian.TemplateViewer.Selection
{
    public sealed class ViewerElementIndex
    {
        private readonly SortedDictionary<string, ViewerElement> elements;

        private ViewerElementIndex(
            SortedDictionary<string, ViewerElement> indexedElements)
        {
            elements = indexedElements;
        }

        public int Count => elements.Count;
        public IEnumerable<string> ElementIds => elements.Keys;

        public bool Contains(string elementId) =>
            !string.IsNullOrWhiteSpace(elementId) && elements.ContainsKey(elementId.Trim());

        public bool TryGet(string elementId, out ViewerElement element)
        {
            element = null;
            return !string.IsNullOrWhiteSpace(elementId) &&
                   elements.TryGetValue(elementId.Trim(), out element);
        }

        public static bool TryCreate(
            GameObject root,
            out ViewerElementIndex index,
            out string error)
        {
            index = null;
            if (root == null)
            {
                error = "A model root is required.";
                return false;
            }

            var result = new SortedDictionary<string, ViewerElement>(
                StringComparer.Ordinal);
            ViewerElement[] candidates =
                root.GetComponentsInChildren<ViewerElement>(true);
            for (int i = 0; i < candidates.Length; i++)
            {
                ViewerElement element = candidates[i];
                string id = element.ElementId;
                if (id.Length == 0)
                {
                    error = "Every ViewerElement requires a stable ID.";
                    return false;
                }

                if (HasManagedAncestor(element.transform, root.transform))
                {
                    error = "Managed visibility elements cannot be nested: " + id + ".";
                    return false;
                }

                if (result.ContainsKey(id))
                {
                    error = "Duplicate ViewerElement ID: " + id + ".";
                    return false;
                }

                result.Add(id, element);
            }

            if (result.Count == 0)
            {
                error = "The model contains no ViewerElement identifiers.";
                return false;
            }

            index = new ViewerElementIndex(result);
            error = string.Empty;
            return true;
        }

        private static bool HasManagedAncestor(Transform current, Transform root)
        {
            Transform parent = current.parent;
            while (parent != null && parent != root.parent)
            {
                if (parent.GetComponent<ViewerElement>() != null)
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
