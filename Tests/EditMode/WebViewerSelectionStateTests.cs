using Deucarian.TemplateViewerWeb.Selection;
using NUnit.Framework;
using UnityEngine;

namespace Deucarian.TemplateViewerWeb.Tests
{
    public sealed class WebViewerSelectionStateTests
    {
        private GameObject root;
        private GameObject alpha;
        private GameObject beta;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Root");
            alpha = CreateElement("alpha", true);
            beta = CreateElement("beta", false);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        [Test]
        public void SelectionIsReplacementAndClearRestoresCapturedBaseline()
        {
            WebViewerSelectionStateOwner state = CreateState(3);

            WebViewerSelectionResult selected = state.Select(4, new[] { "beta" });
            Assert.That(selected.Outcome, Is.EqualTo(WebViewerSelectionOutcome.Applied));
            Assert.That(alpha.activeSelf, Is.False);
            Assert.That(beta.activeSelf, Is.True);

            WebViewerSelectionResult cleared = state.Clear(5);
            Assert.That(cleared.Outcome, Is.EqualTo(WebViewerSelectionOutcome.Applied));
            Assert.That(alpha.activeSelf, Is.True);
            Assert.That(beta.activeSelf, Is.False);
        }

        [Test]
        public void StaleAndInvalidSelectionPreserveLastValidVisibility()
        {
            WebViewerSelectionStateOwner state = CreateState(0);
            Assert.That(state.Select(2, new[] { "alpha" }).Applied, Is.True);

            Assert.That(
                state.Select(1, new[] { "beta" }).Outcome,
                Is.EqualTo(WebViewerSelectionOutcome.Stale));
            Assert.That(
                state.Select(3, new[] { "missing" }).Outcome,
                Is.EqualTo(WebViewerSelectionOutcome.Invalid));

            Assert.That(alpha.activeSelf, Is.True);
            Assert.That(beta.activeSelf, Is.False);
            Assert.That(state.LatestRevision, Is.EqualTo(2));
        }

        [Test]
        public void RepeatedEquivalentPlanIsIdempotentAtNewerRevision()
        {
            WebViewerSelectionStateOwner state = CreateState(0);

            Assert.That(state.Select(1, new[] { "beta" }).Applied, Is.True);
            Assert.That(state.Select(2, new[] { "beta", "beta" }).Applied, Is.True);

            Assert.That(alpha.activeSelf, Is.False);
            Assert.That(beta.activeSelf, Is.True);
            Assert.That(state.LatestRevision, Is.EqualTo(2));
        }

        private WebViewerSelectionStateOwner CreateState(long revision)
        {
            Assert.That(
                WebViewerElementIndex.TryCreate(root, out WebViewerElementIndex index, out string error),
                Is.True,
                error);
            return new WebViewerSelectionStateOwner(
                revision,
                new WebViewerVisibilityController(index));
        }

        private GameObject CreateElement(string id, bool active)
        {
            GameObject element = GameObject.CreatePrimitive(PrimitiveType.Cube);
            element.name = id;
            element.transform.SetParent(root.transform, false);
            element.AddComponent<WebViewerElement>().Initialize(id);
            element.SetActive(active);
            return element;
        }
    }
}
