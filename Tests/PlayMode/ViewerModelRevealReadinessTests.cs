using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deucarian.TemplateViewer.Tests
{
    public sealed class ViewerModelRevealReadinessTests
    {
        [UnityTest]
        public IEnumerator ReducedMotionCommitsAuthoredScaleImmediately()
        {
            GameObject hostObject = new GameObject("Reveal host");
            GameObject model = new GameObject("Reveal model");
            Vector3 authoredScale = new Vector3(2f, 3f, 4f);
            model.transform.localScale = authoredScale;
            try
            {
                var host = hostObject.AddComponent<ViewerModelRevealTestHost>();
                var reveal = new ViewerModelRevealReadinessFeature(
                    host,
                    () => false,
                    10f);

                Task<ViewerModelReadinessResult> task = reveal.PrepareAsync(
                    CreateContext(model),
                    "test",
                    CancellationToken.None);

                Assert.That(task.IsCompleted, Is.True);
                Assert.That(task.Result.Succeeded, Is.True);
                Assert.That(model.transform.localScale, Is.EqualTo(authoredScale));
            }
            finally
            {
                Object.DestroyImmediate(hostObject);
                Object.DestroyImmediate(model);
            }

            yield break;
        }

        [UnityTest]
        public IEnumerator ProductReadinessFailureRestoresScaleBeforeReturning()
        {
            GameObject hostObject = new GameObject("Reveal failure host");
            GameObject model = new GameObject("Reveal failure model");
            Vector3 authoredScale = new Vector3(1.5f, 2f, 0.75f);
            model.transform.localScale = authoredScale;
            try
            {
                var host = hostObject.AddComponent<ViewerModelRevealTestHost>();
                var composition = new ConcurrentViewerModelReadinessFeature(
                    new ViewerModelRevealReadinessFeature(
                        host,
                        () => true,
                        10f),
                    new ImmediateFailureReadiness());

                Task<ViewerModelReadinessResult> task = composition.PrepareAsync(
                    CreateContext(model),
                    "test",
                    CancellationToken.None);
                Assert.That(model.transform.localScale, Is.EqualTo(Vector3.zero));

                yield return WaitFor(task);

                Assert.That(task.IsCompletedSuccessfully, Is.True);
                Assert.That(task.Result.Succeeded, Is.False);
                Assert.That(task.Result.Message, Is.EqualTo("reports failed"));
                Assert.That(model.transform.localScale, Is.EqualTo(authoredScale));
            }
            finally
            {
                Object.DestroyImmediate(hostObject);
                Object.DestroyImmediate(model);
            }
        }

        [UnityTest]
        public IEnumerator CallerCancellationRestoresScaleAndCancelsReadiness()
        {
            GameObject hostObject = new GameObject("Reveal cancellation host");
            GameObject model = new GameObject("Reveal cancellation model");
            Vector3 authoredScale = new Vector3(2f, 0.5f, 3f);
            model.transform.localScale = authoredScale;
            var cancellation = new CancellationTokenSource();
            try
            {
                var host = hostObject.AddComponent<ViewerModelRevealTestHost>();
                var composition = new ConcurrentViewerModelReadinessFeature(
                    new ViewerModelRevealReadinessFeature(
                        host,
                        () => true,
                        10f),
                    new CancellationOnlyReadiness());

                Task<ViewerModelReadinessResult> task = composition.PrepareAsync(
                    CreateContext(model),
                    "test",
                    cancellation.Token);
                Assert.That(model.transform.localScale, Is.EqualTo(Vector3.zero));

                cancellation.Cancel();
                yield return WaitFor(task);

                Assert.That(task.IsCanceled, Is.True);
                Assert.That(model.transform.localScale, Is.EqualTo(authoredScale));
            }
            finally
            {
                cancellation.Dispose();
                Object.DestroyImmediate(hostObject);
                Object.DestroyImmediate(model);
            }
        }

        [UnityTest]
        public IEnumerator RevealAndProductReadinessStartConcurrentlyAndBothFinish()
        {
            GameObject hostObject = new GameObject("Concurrent reveal host");
            GameObject model = new GameObject("Concurrent reveal model");
            Vector3 authoredScale = new Vector3(1.25f, 2.25f, 3.25f);
            model.transform.localScale = authoredScale;
            try
            {
                var host = hostObject.AddComponent<ViewerModelRevealTestHost>();
                var product = new ScaleObservingReadiness(model.transform);
                var composition = new ConcurrentViewerModelReadinessFeature(
                    new ViewerModelRevealReadinessFeature(
                        host,
                        () => true,
                        0.02f),
                    product);

                Task<ViewerModelReadinessResult> task = composition.PrepareAsync(
                    CreateContext(model),
                    "test",
                    CancellationToken.None);

                Assert.That(product.Started, Is.True);
                Assert.That(product.ScaleWhenStarted, Is.EqualTo(Vector3.zero));
                product.Complete();
                yield return WaitFor(task);

                Assert.That(task.IsCompletedSuccessfully, Is.True);
                Assert.That(task.Result.Succeeded, Is.True);
                Assert.That(model.transform.localScale, Is.EqualTo(authoredScale));
            }
            finally
            {
                Object.DestroyImmediate(hostObject);
                Object.DestroyImmediate(model);
            }
        }

        [UnityTest]
        public IEnumerator SupersedingRevealRestoresBeforeCapturingTheNextScale()
        {
            GameObject hostObject = new GameObject("Superseded reveal host");
            GameObject model = new GameObject("Superseded reveal model");
            Vector3 authoredScale = new Vector3(1.75f, 2.5f, 0.8f);
            model.transform.localScale = authoredScale;
            ViewerModelRevealReadinessFeature reveal = null;
            try
            {
                var host = hostObject.AddComponent<ViewerModelRevealTestHost>();
                reveal = new ViewerModelRevealReadinessFeature(
                    host,
                    () => true,
                    0.02f);

                Task<ViewerModelReadinessResult> first = reveal.PrepareAsync(
                    CreateContext(model),
                    "first",
                    CancellationToken.None);
                Assert.That(model.transform.localScale, Is.EqualTo(Vector3.zero));

                Task<ViewerModelReadinessResult> second = reveal.PrepareAsync(
                    CreateContext(model),
                    "second",
                    CancellationToken.None);

                Assert.That(first.IsCanceled, Is.True);
                Assert.That(model.transform.localScale, Is.EqualTo(Vector3.zero));
                yield return WaitFor(second);

                Assert.That(second.IsCompletedSuccessfully, Is.True);
                Assert.That(model.transform.localScale, Is.EqualTo(authoredScale));
            }
            finally
            {
                reveal?.Dispose();
                Object.DestroyImmediate(hostObject);
                Object.DestroyImmediate(model);
            }
        }

        [UnityTest]
        public IEnumerator DisabledHostCompletesWithoutLeavingZeroScale()
        {
            GameObject hostObject = new GameObject("Disabled reveal host");
            GameObject model = new GameObject("Disabled reveal model");
            Vector3 authoredScale = new Vector3(2f, 1.25f, 3f);
            model.transform.localScale = authoredScale;
            ViewerModelRevealReadinessFeature reveal = null;
            try
            {
                var host = hostObject.AddComponent<ViewerModelRevealTestHost>();
                reveal = new ViewerModelRevealReadinessFeature(
                    host,
                    () => true,
                    10f);
                Task<ViewerModelReadinessResult> task = reveal.PrepareAsync(
                    CreateContext(model),
                    "test",
                    CancellationToken.None);
                Assert.That(model.transform.localScale, Is.EqualTo(Vector3.zero));

                host.enabled = false;
                yield return WaitFor(task);

                Assert.That(task.IsCanceled, Is.True);
                Assert.That(model.transform.localScale, Is.EqualTo(authoredScale));
            }
            finally
            {
                reveal?.Dispose();
                Object.DestroyImmediate(hostObject);
                Object.DestroyImmediate(model);
            }
        }

        [UnityTest]
        public IEnumerator DisabledHostCancelsConcurrentPendingProduct()
        {
            GameObject hostObject = new GameObject(
                "Disabled concurrent reveal host");
            GameObject model = new GameObject(
                "Disabled concurrent reveal model");
            Vector3 authoredScale = new Vector3(2.5f, 1.5f, 0.75f);
            model.transform.localScale = authoredScale;
            ViewerModelRevealReadinessFeature reveal = null;
            try
            {
                var host = hostObject.AddComponent<ViewerModelRevealTestHost>();
                var product = new CancellationObservingReadiness();
                reveal = new ViewerModelRevealReadinessFeature(
                    host,
                    () => true,
                    10f);
                var composition = new ConcurrentViewerModelReadinessFeature(
                    reveal,
                    product);

                Task<ViewerModelReadinessResult> task =
                    composition.PrepareAsync(
                        CreateContext(model),
                        "test",
                        CancellationToken.None);
                Assert.That(product.Started, Is.True);
                Assert.That(model.transform.localScale, Is.EqualTo(Vector3.zero));

                host.enabled = false;
                yield return WaitFor(task);

                Assert.That(task.IsCanceled, Is.True);
                Assert.That(product.CancellationObserved, Is.True);
                Assert.That(model.transform.localScale, Is.EqualTo(authoredScale));
            }
            finally
            {
                reveal?.Dispose();
                Object.DestroyImmediate(hostObject);
                Object.DestroyImmediate(model);
            }
        }

        [UnityTest]
        public IEnumerator DestroyedHostCancelsWithoutLeavingZeroScale()
        {
            GameObject hostObject = new GameObject("Destroyed reveal host");
            GameObject model = new GameObject("Destroyed reveal model");
            Vector3 authoredScale = new Vector3(0.6f, 1.8f, 2.4f);
            model.transform.localScale = authoredScale;
            ViewerModelRevealReadinessFeature reveal = null;
            try
            {
                var host = hostObject.AddComponent<ViewerModelRevealTestHost>();
                reveal = new ViewerModelRevealReadinessFeature(
                    host,
                    () => true,
                    10f);
                Task<ViewerModelReadinessResult> task = reveal.PrepareAsync(
                    CreateContext(model),
                    "test",
                    CancellationToken.None);
                Assert.That(model.transform.localScale, Is.EqualTo(Vector3.zero));

                Object.Destroy(hostObject);
                yield return null;
                yield return WaitFor(task);

                Assert.That(task.IsCanceled, Is.True);
                Assert.That(model.transform.localScale, Is.EqualTo(authoredScale));
            }
            finally
            {
                reveal?.Dispose();
                if (hostObject != null)
                {
                    Object.DestroyImmediate(hostObject);
                }

                Object.DestroyImmediate(model);
            }
        }

        private static ViewerModelContext CreateContext(GameObject model) =>
            new ViewerModelContext(
                model,
                new ViewerModelDescriptor(
                    string.Empty,
                    "model",
                    "version",
                    null,
                    null,
                    false),
                1);

        private static IEnumerator WaitFor(Task task)
        {
            const int maximumFrames = 120;
            int frames = 0;
            while (!task.IsCompleted && frames++ < maximumFrames)
            {
                yield return null;
            }

            Assert.That(task.IsCompleted, Is.True, "Readiness task timed out.");
        }

        private sealed class ImmediateFailureReadiness :
            IViewerModelReadinessFeature
        {
            public Task<ViewerModelReadinessResult> PrepareAsync(
                ViewerModelContext context,
                string remoteEndpoint,
                CancellationToken cancellationToken) =>
                Task.FromResult(
                    ViewerModelReadinessResult.Failure("reports failed"));
        }

        private sealed class CancellationOnlyReadiness :
            IViewerModelReadinessFeature
        {
            public async Task<ViewerModelReadinessResult> PrepareAsync(
                ViewerModelContext context,
                string remoteEndpoint,
                CancellationToken cancellationToken)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return ViewerModelReadinessResult.Success();
            }
        }

        private sealed class ScaleObservingReadiness :
            IViewerModelReadinessFeature
        {
            private readonly Transform model;
            private readonly TaskCompletionSource<ViewerModelReadinessResult>
                completion = new TaskCompletionSource<
                    ViewerModelReadinessResult>();

            public ScaleObservingReadiness(Transform modelTransform)
            {
                model = modelTransform;
            }

            public bool Started { get; private set; }
            public Vector3 ScaleWhenStarted { get; private set; }

            public Task<ViewerModelReadinessResult> PrepareAsync(
                ViewerModelContext context,
                string remoteEndpoint,
                CancellationToken cancellationToken)
            {
                Started = true;
                ScaleWhenStarted = model.localScale;
                cancellationToken.Register(() =>
                    completion.TrySetCanceled(cancellationToken));
                return completion.Task;
            }

            public void Complete()
            {
                completion.TrySetResult(ViewerModelReadinessResult.Success());
            }
        }

        private sealed class CancellationObservingReadiness :
            IViewerModelReadinessFeature
        {
            private readonly TaskCompletionSource<ViewerModelReadinessResult>
                completion = new TaskCompletionSource<
                    ViewerModelReadinessResult>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

            public bool Started { get; private set; }
            public bool CancellationObserved { get; private set; }

            public Task<ViewerModelReadinessResult> PrepareAsync(
                ViewerModelContext context,
                string remoteEndpoint,
                CancellationToken cancellationToken)
            {
                Started = true;
                cancellationToken.Register(() =>
                {
                    CancellationObserved = true;
                });
                return completion.Task;
            }
        }
    }

    public sealed class ViewerModelRevealTestHost : MonoBehaviour
    {
    }
}
