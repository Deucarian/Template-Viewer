using System.Collections;
using Deucarian.TemplateViewerWeb.Selection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deucarian.TemplateViewerWeb.Tests
{
    public sealed class WebViewerVisibilityCameraInvariantTests
    {
        [UnityTest]
        public IEnumerator SelectionAndClearDoNotChangeCameraState()
        {
            GameObject root = new GameObject("Root");
            GameObject cameraObject = new GameObject("Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            try
            {
                CreateElement(root.transform, "one");
                CreateElement(root.transform, "two");
                Assert.That(
                    WebViewerElementIndex.TryCreate(root, out WebViewerElementIndex index, out string error),
                    Is.True,
                    error);
                var state = new WebViewerSelectionStateOwner(
                    0,
                    new WebViewerVisibilityController(index));

                camera.transform.SetPositionAndRotation(
                    new Vector3(3.5f, 7.2f, -11.4f),
                    Quaternion.Euler(23f, 41f, 2f));
                camera.orthographic = true;
                camera.orthographicSize = 8.25f;
                Vector3 position = camera.transform.position;
                Quaternion rotation = camera.transform.rotation;
                bool orthographic = camera.orthographic;
                float size = camera.orthographicSize;

                state.Select(1, new[] { "two" });
                state.Clear(2);
                yield return null;

                Assert.That(camera.transform.position, Is.EqualTo(position));
                Assert.That(camera.transform.rotation, Is.EqualTo(rotation));
                Assert.That(camera.orthographic, Is.EqualTo(orthographic));
                Assert.That(camera.orthographicSize, Is.EqualTo(size));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(cameraObject);
            }
        }

        private static void CreateElement(Transform parent, string id)
        {
            GameObject element = GameObject.CreatePrimitive(PrimitiveType.Cube);
            element.transform.SetParent(parent, false);
            element.AddComponent<WebViewerElement>().Initialize(id);
        }
    }
}
