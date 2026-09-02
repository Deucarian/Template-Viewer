using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.CommandRouting;
using Newtonsoft.Json.Linq;

namespace Deucarian.TemplateViewer.Commands
{
    /// <summary>
    /// Projects failed route outcomes in order on the Unity context captured
    /// by the composition root. Route-completion callbacks only clone and
    /// enqueue transport-neutral data.
    /// </summary>
    internal sealed class ViewerCommandFailureProjector : IDisposable
    {
        private readonly object gate = new object();
        private readonly CommandRoutingRuntime<ViewerApplication> runtime;
        private readonly SynchronizationContext synchronizationContext;
        private readonly int synchronizationThreadId;
        private readonly Func<
            string,
            JObject,
            string,
            CancellationToken,
            Task> publish;
        private readonly Action<ViewerCommandFailureProjectionEventArgs>
            notifyFeatures;
        private readonly Func<
            ViewerCommandFailureProjectionEventArgs,
            ViewerCommandFailureProjectionEventArgs> prepareProjection;
        private readonly Queue<ViewerCommandFailureProjectionEventArgs>
            pending =
                new Queue<ViewerCommandFailureProjectionEventArgs>();
        private readonly CancellationTokenSource lifetime =
            new CancellationTokenSource();

        private TaskCompletionSource<bool> idle = CompletedSource();
        private bool processing;
        private bool disposed;

        internal ViewerCommandFailureProjector(
            CommandRoutingRuntime<ViewerApplication> commandRuntime,
            SynchronizationContext unitySynchronizationContext,
            Func<
                string,
                JObject,
                string,
                CancellationToken,
                Task> publishEvent,
            Action<ViewerCommandFailureProjectionEventArgs>
                featureNotification,
            Func<
                ViewerCommandFailureProjectionEventArgs,
                ViewerCommandFailureProjectionEventArgs>
                projectionPreparation = null)
        {
            runtime = commandRuntime ??
                throw new ArgumentNullException(nameof(commandRuntime));
            synchronizationContext = unitySynchronizationContext ??
                throw new ArgumentNullException(
                    nameof(unitySynchronizationContext));
            publish = publishEvent ??
                throw new ArgumentNullException(nameof(publishEvent));
            notifyFeatures = featureNotification ??
                throw new ArgumentNullException(nameof(featureNotification));
            prepareProjection = projectionPreparation ??
                (value => value);
            synchronizationThreadId =
                Thread.CurrentThread.ManagedThreadId;
            runtime.RouteCompleted += OnRouteCompleted;
        }

        internal Task WhenIdle
        {
            get
            {
                lock (gate)
                {
                    return idle.Task;
                }
            }
        }

        public void Dispose()
        {
            runtime.RouteCompleted -= OnRouteCompleted;

            TaskCompletionSource<bool> complete = null;
            lock (gate)
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                pending.Clear();
                if (!processing)
                {
                    complete = idle;
                }
            }

            lifetime.Cancel();
            complete?.TrySetResult(true);
        }

        private void OnRouteCompleted(
            object sender,
            CommandRouteCompletedEventArgs eventArgs)
        {
            ViewerCommandFailureProjectionEventArgs projection;
            try
            {
                if (!ViewerCommandFailureProjectionEventArgs.TryCreate(
                        eventArgs,
                        out projection))
                {
                    return;
                }
            }
            catch (Exception)
            {
                return;
            }

            bool schedule = false;
            lock (gate)
            {
                if (disposed)
                {
                    return;
                }

                if (idle.Task.IsCompleted)
                {
                    idle = NewSource();
                }

                pending.Enqueue(projection);
                if (!processing)
                {
                    processing = true;
                    schedule = true;
                }
            }

            if (schedule)
            {
                Post(ProcessNextOnContext);
            }
        }

        private void ProcessNextOnContext(object state)
        {
            if (Thread.CurrentThread.ManagedThreadId !=
                synchronizationThreadId)
            {
                AbandonPending();
                return;
            }

            ViewerCommandFailureProjectionEventArgs projection;
            lock (gate)
            {
                if (disposed || pending.Count == 0)
                {
                    FinishProcessingLocked();
                    return;
                }

                projection = pending.Dequeue();
            }

            try
            {
                projection = prepareProjection(projection) ?? projection;
            }
            catch (Exception)
            {
                // Product payload policy cannot suppress the canonical event.
            }

            try
            {
                notifyFeatures(projection);
            }
            catch (Exception)
            {
                // A composition callback cannot suppress remote projection.
            }

            Task publication;
            try
            {
                publication = publish(
                    ViewerCommandFailureProjectionEventArgs.EventName,
                    projection.Payload,
                    projection.RemoteEndpoint,
                    lifetime.Token) ?? Task.CompletedTask;
            }
            catch (Exception)
            {
                CompletePublicationOnContext(null);
                return;
            }

            if (publication.IsCompleted)
            {
                CompletePublicationOnContext(publication);
                return;
            }

            _ = publication.ContinueWith(
                completed => Post(
                    _ => CompletePublicationOnContext(completed)),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        private void CompletePublicationOnContext(Task publication)
        {
            if (Thread.CurrentThread.ManagedThreadId !=
                synchronizationThreadId)
            {
                AbandonPending();
                return;
            }

            try
            {
                publication?.GetAwaiter().GetResult();
            }
            catch (Exception)
            {
                // The route result remains authoritative when delivery fails.
            }

            bool schedule;
            lock (gate)
            {
                if (disposed)
                {
                    pending.Clear();
                    FinishProcessingLocked();
                    return;
                }

                schedule = pending.Count > 0;
                if (!schedule)
                {
                    FinishProcessingLocked();
                }
            }

            if (schedule)
            {
                Post(ProcessNextOnContext);
            }
        }

        private void Post(SendOrPostCallback callback)
        {
            try
            {
                synchronizationContext.Post(callback, null);
            }
            catch (Exception)
            {
                AbandonPending();
            }
        }

        private void AbandonPending()
        {
            TaskCompletionSource<bool> complete;
            lock (gate)
            {
                pending.Clear();
                processing = false;
                complete = idle;
            }

            complete.TrySetResult(true);
        }

        private void FinishProcessingLocked()
        {
            processing = false;
            idle.TrySetResult(true);
        }

        private static TaskCompletionSource<bool> CompletedSource()
        {
            TaskCompletionSource<bool> source = NewSource();
            source.TrySetResult(true);
            return source;
        }

        private static TaskCompletionSource<bool> NewSource() =>
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
