using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deucarian.TemplateViewer.Tests
{
    public sealed class ViewerBootstrapLifecycleTests
    {
        [UnityTest]
        public IEnumerator DestroyReleasesTransportBeforeAdapter()
        {
            GameObject root = new GameObject("Viewer lifecycle test");
            var adapter = new FakeViewerPlatformAdapter();
            try
            {
                FakeViewerBootstrap bootstrap =
                    root.AddComponent<FakeViewerBootstrap>();
                bootstrap.enabled = false;
                bootstrap.Adapter = adapter;
                bootstrap.ComposeNow();

                Object.Destroy(root);
                yield return null;

                Assert.That(adapter.ActivationDisposeCount, Is.EqualTo(1));
                Assert.That(adapter.DisposeCount, Is.EqualTo(1));
                Assert.That(
                    adapter.CleanupOrder,
                    Is.EqualTo(new[] { "activation", "adapter" }));
            }
            finally
            {
                if (root != null)
                {
                    Object.DestroyImmediate(root);
                }
            }
        }
    }
}
