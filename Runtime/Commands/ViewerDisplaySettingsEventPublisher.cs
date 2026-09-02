using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.ViewerRendering;
using Newtonsoft.Json.Linq;

namespace Deucarian.TemplateViewer.Commands
{
    internal sealed class ViewerDisplaySettingsEventPublisher : IDisposable
    {
        internal const string EventName = "display_settings_changed";

        private readonly ViewerApplication application;
        private readonly IViewerRenderingController rendering;
        private readonly string fallbackRemoteEndpoint;
        private readonly object gate = new object();
        private readonly Queue<PendingPublication> pending =
            new Queue<PendingPublication>();
        private readonly CancellationTokenSource lifetime =
            new CancellationTokenSource();

        private TaskCompletionSource<bool> idle = CompletedSource();
        private int initialPublished;
        private bool processing;
        private bool disposed;

        public ViewerDisplaySettingsEventPublisher(
            ViewerApplication viewerApplication,
            IViewerRenderingController renderingController,
            string defaultRemoteEndpoint)
        {
            application = viewerApplication ??
                throw new ArgumentNullException(nameof(viewerApplication));
            rendering = renderingController ??
                throw new ArgumentNullException(nameof(renderingController));
            fallbackRemoteEndpoint = defaultRemoteEndpoint ?? string.Empty;
            rendering.SettingsChanged += OnSettingsChanged;
        }

        public Task PublishInitialAsync()
        {
            lock (gate)
            {
                if (disposed || initialPublished != 0)
                {
                    return Task.CompletedTask;
                }

                initialPublished = 1;
            }

            return Enqueue(
                rendering.CurrentSettings,
                ViewerDisplaySettingsChangeSource.Initialization);
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
            PendingPublication[] abandoned;
            lock (gate)
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                abandoned = pending.ToArray();
                pending.Clear();
                if (!processing)
                {
                    idle.TrySetResult(true);
                }
            }

            rendering.SettingsChanged -= OnSettingsChanged;
            try
            {
                lifetime.Cancel();
            }
            catch (Exception)
            {
                // A transport cancellation callback cannot interrupt the
                // remaining composition teardown.
            }

            for (int index = 0; index < abandoned.Length; index++)
            {
                abandoned[index].Completion.TrySetResult(true);
            }
        }

        internal static JObject CreatePayload(
            ViewerDisplaySettingsSnapshot snapshot,
            ViewerDisplaySettingsChangeSource source)
        {
            return new JObject
            {
                ["rendering_mode"] =
                    ViewerDisplaySettingsPayload.ToPayloadValue(
                        snapshot.RenderingMode),
                ["camera_relative_light"] = snapshot.CameraRelativeLight,
                ["effects_active"] = snapshot.EffectsActive,
                ["source"] = ViewerDisplaySettingsPayload.ToPayloadValue(source)
            };
        }

        private void OnSettingsChanged(
            ViewerDisplaySettingsSnapshot snapshot,
            ViewerDisplaySettingsChangeSource source)
        {
            _ = Enqueue(snapshot, source);
        }

        private Task Enqueue(
            ViewerDisplaySettingsSnapshot snapshot,
            ViewerDisplaySettingsChangeSource source)
        {
            PendingPublication publication;
            try
            {
                publication = new PendingPublication(
                    CreatePayload(snapshot, source),
                    ResolveRemoteEndpoint());
            }
            catch (Exception)
            {
                // Presentation observers cannot invalidate authoritative
                // rendering state or the viewer lifecycle.
                return Task.CompletedTask;
            }

            bool startProcessing = false;
            lock (gate)
            {
                if (disposed)
                {
                    return Task.CompletedTask;
                }

                if (idle.Task.IsCompleted)
                {
                    idle = NewSource();
                }

                pending.Enqueue(publication);
                if (!processing)
                {
                    processing = true;
                    startProcessing = true;
                }
            }

            if (startProcessing)
            {
                _ = DrainAsync();
            }

            return publication.Completion.Task;
        }

        private string ResolveRemoteEndpoint()
        {
            string acceptedEndpoint = application.CurrentRemoteEndpoint;
            return string.IsNullOrEmpty(acceptedEndpoint)
                ? fallbackRemoteEndpoint
                : acceptedEndpoint;
        }

        private async Task DrainAsync()
        {
            while (true)
            {
                PendingPublication publication;
                lock (gate)
                {
                    if (disposed || pending.Count == 0)
                    {
                        processing = false;
                        idle.TrySetResult(true);
                        return;
                    }

                    publication = pending.Dequeue();
                }

                try
                {
                    lifetime.Token.ThrowIfCancellationRequested();
                    await application.PublishEventAsync(
                        EventName,
                        publication.Payload,
                        publication.RemoteEndpoint,
                        lifetime.Token);
                }
                catch (Exception)
                {
                    // Presentation observers cannot invalidate authoritative
                    // rendering state or the viewer lifecycle.
                }
                finally
                {
                    publication.Completion.TrySetResult(true);
                }
            }
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

        private sealed class PendingPublication
        {
            public PendingPublication(JObject payload, string remoteEndpoint)
            {
                Payload = payload ?? throw new ArgumentNullException(
                    nameof(payload));
                RemoteEndpoint = remoteEndpoint;
                Completion = NewSource();
            }

            public JObject Payload { get; }
            public string RemoteEndpoint { get; }
            public TaskCompletionSource<bool> Completion { get; }
        }
    }
}
