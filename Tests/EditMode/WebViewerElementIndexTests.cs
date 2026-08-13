using Deucarian.TemplateViewerWeb.Selection;
using NUnit.Framework;
using UnityEngine;

namespace Deucarian.TemplateViewerWeb.Tests
{
    public sealed class WebViewerElementIndexTests
    {
        [Test]
        public void RejectsDuplicateStableIdentifiers()
        {
            GameObject root = new GameObject("Root");
            try
            {
                AddElement(root.transform, "same");
                AddElement(root.transform, "same");

                Assert.That(
                    WebViewerElementIndex.TryCreate(root, out _, out string error),
                    Is.False);
                StringAssert.Contains("Duplicate", error);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RejectsNestedManagedVisibilityRoots()
        {
            GameObject root = new GameObject("Root");
            try
            {
                GameObject parent = AddElement(root.transform, "parent");
                AddElement(parent.transform, "child");

                Assert.That(
                    WebViewerElementIndex.TryCreate(root, out _, out string error),
                    Is.False);
                StringAssert.Contains("cannot be nested", error);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject AddElement(Transform parent, string id)
        {
            GameObject element = new GameObject(id);
            element.transform.SetParent(parent, false);
            element.AddComponent<WebViewerElement>().Initialize(id);
            return element;
        }
    }
}
