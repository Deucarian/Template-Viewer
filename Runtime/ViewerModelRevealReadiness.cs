using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.Common;
using Deucarian.ViewerNavigation;
using UnityEngine;

namespace Deucarian.TemplateViewer
{
    internal static class ViewerModelReadinessComposition
    {
        public static IViewerModelReadinessFeature Create(
            MonoBehaviour coroutineHost,
            IViewerModelReadinessFeature productReadiness,
            out ViewerModelRevealReadinessFeature revealFeature)
        {
            revealFeature = new ViewerModelRevealReadinessFeature(coroutineHost);
            return productReadiness == null
                ? revealFeature
                : new ConcurrentViewerModelReadinessFeature(
                    revealFeature,
                    productReadiness);
        }
    }

    internal sealed class ConcurrentViewerModelReadinessFeature :
        IViewerModelReadinessFeature
    {
        private readonly IViewerModelReadinessFeature reveal;
        private readonly IViewerModelReadinessFeature product;

        public ConcurrentViewerModelReadinessFeature(
            IViewerModelReadinessFeature revealFeature,
            IViewerModelReadinessFeature productFeature)
        {
            reveal = revealFeature ??
                throw new ArgumentNullException(nameof(revealFeature));
            product = productFeature ??
                throw new ArgumentNullException(nameof(productFeature));
        }

        public async Task<ViewerModelReadinessResult> PrepareAsync(
            ViewerModelContext context,
            string remoteEndpoint,
            CancellationToken cancellationToken)
        {
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(
                       cancellationToken))
            {
                Task<ViewerModelReadinessResult> revealTask =
                    reveal.PrepareAsync(
                        context,
                        remoteEndpoint,
                        linked.Token);
                Task<ViewerModelReadinessResult> productTask = null;
                try
                {
                    productTask = product.PrepareAsync(
                        context,
                        remoteEndpoint,
                        linked.Token);
                    if (productTask == null)
                    {
                        linked.Cancel();
                        await ObserveCleanupAsync(revealTask);
                        return null;
                    }

                    Task<ViewerModelReadinessResult> completed =
                        await Task.WhenAny(productTask, revealTask);
                    if (ReferenceEquals(completed, revealTask))
                    {
                        ViewerModelReadinessResult revealResult =
                            await revealTask;
                        if (revealResult == null || !revealResult.Succeeded)
                        {
                            linked.Cancel();
                            ObserveEventually(productTask);
                            return revealResult;
                        }

                        ViewerModelReadinessResult pendingProductResult =
                            await productTask;
                        return pendingProductResult == null ||
                               !pendingProductResult.Succeeded
                            ? pendingProductResult
                            : revealResult;
                    }

                    ViewerModelReadinessResult productResult =
                        await productTask;
                    if (productResult == null || !productResult.Succeeded)
                    {
                        linked.Cancel();
                        await ObserveCleanupAsync(revealTask);
                        return productResult;
                    }

                    return await revealTask;
                }
                catch
                {
                    linked.Cancel();
                    await ObserveCleanupAsync(revealTask);
                    ObserveEventually(productTask);
                    throw;
                }
            }
        }

        private static async Task ObserveCleanupAsync(
            Task<ViewerModelReadinessResult> task)
        {
            if (task == null)
            {
                return;
            }

            try
            {
                await task;
            }
            catch (Exception)
            {
                // Cleanup completion is observed without replacing the product
                // result or the caller's cancellation/exception.
            }
        }

        private static void ObserveEventually(
            Task<ViewerModelReadinessResult> task)
        {
            if (task == null)
            {
                return;
            }

            task.ContinueWith(
                completed =>
                {
                    _ = completed.Exception;
                },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted |
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    internal sealed class ViewerModelRevealReadinessFeature :
        IViewerModelReadinessFeature,
        IDisposable
    {
        internal const float RevealDurationSeconds = 0.48f;

        private readonly MonoBehaviour host;
        private readonly ViewerModelRevealLifecycleRelay lifecycleRelay;
        private readonly Func<bool> shouldAnimate;
        private readonly float duration;
        private RevealLease activeReveal;
        private bool disposed;

        public ViewerModelRevealReadinessFeature(
            MonoBehaviour coroutineHost,
            Func<bool> animationPreference = null,
            float revealDurationSeconds = RevealDurationSeconds)
        {
            host = coroutineHost ??
                throw new ArgumentNullException(nameof(coroutineHost));
            shouldAnimate = animationPreference ??
                (() => ViewerNavigationMotionPreferences.ShouldAnimate);
            duration = Mathf.Max(0f, revealDurationSeconds);
            lifecycleRelay = host.GetComponent<
                ViewerModelRevealLifecycleRelay>() ??
                host.gameObject.AddComponent<ViewerModelRevealLifecycleRelay>();
            lifecycleRelay.Interrupted += CancelActive;
        }

        public Task<ViewerModelReadinessResult> PrepareAsync(
            ViewerModelContext context,
            string remoteEndpoint,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            CancelActive();
            cancellationToken.ThrowIfCancellationRequested();
            Transform referenceRoot = context?.ReferenceRoot?.transform;
            if (referenceRoot == null ||
                duration <= 0f ||
                !shouldAnimate() ||
                !host.isActiveAndEnabled)
            {
                return Task.FromResult(ViewerModelReadinessResult.Success());
            }

            Vector3 targetScale = referenceRoot.localScale;
            var completion = new TaskCompletionSource<
                ViewerModelReadinessResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var reveal = new RevealLease(
                referenceRoot,
                targetScale,
                cancellationToken,
                completion);
            activeReveal = reveal;
            try
            {
                reveal.Routine = host.StartCoroutine(RevealRoutine(reveal));
            }
            catch (Exception exception)
            {
                Complete(
                    reveal,
                    canceled: false,
                    exception: exception);
            }

            return completion.Task;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            if (lifecycleRelay != null)
            {
                lifecycleRelay.Interrupted -= CancelActive;
            }

            CancelActive();
        }

        internal void CancelActive()
        {
            RevealLease reveal = activeReveal;
            if (reveal == null)
            {
                return;
            }

            activeReveal = null;
            if (host != null && reveal.Routine != null)
            {
                host.StopCoroutine(reveal.Routine);
            }

            RestoreScale(reveal);
            if (reveal.CancellationToken.IsCancellationRequested)
            {
                reveal.Completion.TrySetCanceled(reveal.CancellationToken);
            }
            else
            {
                reveal.Completion.TrySetCanceled();
            }
        }

        private IEnumerator RevealRoutine(RevealLease reveal)
        {
            bool canceled = false;
            try
            {
                if (reveal.CancellationToken.IsCancellationRequested)
                {
                    canceled = true;
                    yield break;
                }

                reveal.ReferenceRoot.localScale = Vector3.zero;
                float elapsed = 0f;
                while (elapsed < duration &&
                       reveal.ReferenceRoot != null &&
                       host.isActiveAndEnabled &&
                       shouldAnimate())
                {
                    if (reveal.CancellationToken.IsCancellationRequested)
                    {
                        canceled = true;
                        yield break;
                    }

                    float normalized = Mathf.Clamp01(elapsed / duration);
                    float eased = DeucarianEasingUtility.Evaluate(
                        DeucarianEasing.EaseOutSoftBack,
                        normalized);
                    reveal.ReferenceRoot.localScale = Vector3.LerpUnclamped(
                        Vector3.zero,
                        reveal.TargetScale,
                        eased);
                    elapsed += Time.deltaTime;
                    yield return null;
                }

                if (!host.isActiveAndEnabled)
                {
                    canceled = true;
                }
            }
            finally
            {
                Complete(
                    reveal,
                    canceled || reveal.CancellationToken.IsCancellationRequested,
                    exception: null);
            }
        }

        private void Complete(
            RevealLease reveal,
            bool canceled,
            Exception exception)
        {
            if (!ReferenceEquals(activeReveal, reveal))
            {
                return;
            }

            activeReveal = null;
            RestoreScale(reveal);
            if (exception != null)
            {
                reveal.Completion.TrySetException(exception);
            }
            else if (canceled)
            {
                if (reveal.CancellationToken.IsCancellationRequested)
                {
                    reveal.Completion.TrySetCanceled(
                        reveal.CancellationToken);
                }
                else
                {
                    reveal.Completion.TrySetCanceled();
                }
            }
            else
            {
                reveal.Completion.TrySetResult(
                    ViewerModelReadinessResult.Success());
            }
        }

        private static void RestoreScale(RevealLease reveal)
        {
            if (reveal.ReferenceRoot != null)
            {
                reveal.ReferenceRoot.localScale = reveal.TargetScale;
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }

        private sealed class RevealLease
        {
            public RevealLease(
                Transform referenceRoot,
                Vector3 targetScale,
                CancellationToken cancellationToken,
                TaskCompletionSource<ViewerModelReadinessResult> completion)
            {
                ReferenceRoot = referenceRoot;
                TargetScale = targetScale;
                CancellationToken = cancellationToken;
                Completion = completion;
            }

            public Transform ReferenceRoot { get; }
            public Vector3 TargetScale { get; }
            public CancellationToken CancellationToken { get; }
            public TaskCompletionSource<ViewerModelReadinessResult> Completion
            {
                get;
            }
            public Coroutine Routine { get; set; }
        }
    }
}
