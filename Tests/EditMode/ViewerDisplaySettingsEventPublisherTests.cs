using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.CommandRouting;
using Deucarian.TemplateViewer.Commands;
using Deucarian.TemplateViewer.Loading;
using Deucarian.ViewerRendering;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Deucarian.TemplateViewer.Tests
{
    public sealed class ViewerDisplaySettingsEventPublisherTests
    {
        private GameObject model;
        private RecordingEventPublisher events;
        private RecordingRenderingController rendering;
        private AcceptingNavigation navigation;
        private ControllableReadiness readiness;
        private ViewerApplication application;

        [SetUp]
        public void SetUp()
        {
            model = new GameObject("Display event model");
            events = new RecordingEventPublisher();
            rendering = new RecordingRenderingController();
            navigation = new AcceptingNavigation();
            readiness = new ControllableReadiness();
            application = CreateApplication(events);
        }

        private ViewerApplication CreateApplication(
            IViewerEventPublisher eventPublisher) =>
            new ViewerApplication(
                new DirectViewerModelDescriptorResolver(),
                new EmbeddedOnlyLoader(),
                navigation,
                eventPublisher,
                model,
                customVisibilityFeatureFactory:
                    new EmptyVisibilityFeatureFactory(),
                customModelReadinessFeature: readiness);

        [TearDown]
        public void TearDown()
        {
            application?.Dispose();
            readiness?.ReleaseAll();
            UnityEngine.Object.DestroyImmediate(model);
        }

        [Test]
        public async Task InitialDisplayProjectionPublishesExactlyOnce()
        {
            const string fallbackEndpoint = "parent:https://viewer.example";
            events.RequiredEndpoint = fallbackEndpoint;
            using (var publisher = new ViewerDisplaySettingsEventPublisher(
                       application,
                       rendering,
                       fallbackEndpoint))
            {
                await publisher.PublishInitialAsync();
                await publisher.PublishInitialAsync();
            }

            EventRecord record = events.Records.Single();
            Assert.That(
                record.EventName,
                Is.EqualTo("display_settings_changed"));
            Assert.That(record.RemoteEndpoint, Is.EqualTo(fallbackEndpoint));
            CollectionAssert.AreEquivalent(
                new[]
                {
                    "rendering_mode",
                    "camera_relative_light",
                    "effects_active",
                    "source"
                },
                record.Payload.Properties()
                    .Select(property => property.Name)
                    .ToArray());
            Assert.That(
                record.Payload.Value<string>("rendering_mode"),
                Is.EqualTo("color_faithful"));
            Assert.That(
                record.Payload.Value<bool>("camera_relative_light"),
                Is.False);
            Assert.That(record.Payload.Value<bool>("effects_active"), Is.True);
            Assert.That(
                record.Payload.Value<string>("source"),
                Is.EqualTo("initialization"));
        }

        [Test]
        public async Task AcceptedEndpointIsVerbatimAndStaleRevisionCannotReplaceIt()
        {
            const string acceptedEndpoint =
                " parent:https://viewer.example/accepted ";
            CommandOperationResult initialized = await application.InitializeAsync(
                new ViewerInitializeRequest { Revision = 7 },
                acceptedEndpoint,
                CancellationToken.None);
            Assert.That(initialized.Succeeded, Is.True, initialized.Message);
            Assert.That(
                application.CurrentRemoteEndpoint,
                Is.EqualTo(acceptedEndpoint));

            CommandOperationResult stale = await application.InitializeAsync(
                new ViewerInitializeRequest { Revision = 6 },
                "parent:https://viewer.example/stale",
                CancellationToken.None);

            Assert.That(stale.Succeeded, Is.False);
            Assert.That(stale.ErrorCode, Is.EqualTo("stale_revision"));
            Assert.That(application.LatestRevision, Is.EqualTo(7));
            Assert.That(
                application.CurrentRemoteEndpoint,
                Is.EqualTo(acceptedEndpoint));
        }

        [Test]
        public async Task FailedInitializationKeepsPriorAcceptedEndpoint()
        {
            const string acceptedEndpoint =
                "parent:https://viewer.example/accepted";
            const string failedEndpoint =
                "parent:https://viewer.example/failed";
            CommandOperationResult accepted = await application.InitializeAsync(
                new ViewerInitializeRequest { Revision = 7 },
                acceptedEndpoint,
                CancellationToken.None);
            Assert.That(accepted.Succeeded, Is.True, accepted.Message);
            events.Clear();
            navigation.AcceptReferences = false;

            CommandOperationResult failed = await application.InitializeAsync(
                new ViewerInitializeRequest { Revision = 8 },
                failedEndpoint,
                CancellationToken.None);

            Assert.That(failed.Succeeded, Is.False);
            Assert.That(failed.ErrorCode, Is.EqualTo("initialization_failed"));
            Assert.That(
                application.CurrentRemoteEndpoint,
                Is.EqualTo(acceptedEndpoint));
            Assert.That(
                events.Records.Single(record =>
                    record.EventName == "viewer_failed").RemoteEndpoint,
                Is.EqualTo(failedEndpoint));
        }

        [Test]
        public async Task SuccessfulInitializationCommitsOnlyAtFinalReadyPoint()
        {
            const string acceptedEndpoint =
                "parent:https://viewer.example/accepted";
            const string pendingEndpoint =
                "parent:https://viewer.example/pending";
            CommandOperationResult accepted = await application.InitializeAsync(
                new ViewerInitializeRequest { Revision = 7 },
                acceptedEndpoint,
                CancellationToken.None);
            Assert.That(accepted.Succeeded, Is.True, accepted.Message);
            events.Clear();
            readiness.Hold = true;

            Task<CommandOperationResult> pending =
                application.InitializeAsync(
                    new ViewerInitializeRequest { Revision = 8 },
                    pendingEndpoint,
                    CancellationToken.None);

            Assert.That(readiness.HasStarted(8), Is.True);
            Assert.That(pending.IsCompleted, Is.False);
            Assert.That(
                application.CurrentRemoteEndpoint,
                Is.EqualTo(acceptedEndpoint));
            Assert.That(
                events.Records.Single(record =>
                    record.EventName == "viewer_loading").RemoteEndpoint,
                Is.EqualTo(pendingEndpoint));

            readiness.Complete(8);
            CommandOperationResult result = await pending;

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(
                application.CurrentRemoteEndpoint,
                Is.EqualTo(pendingEndpoint));
            Assert.That(
                events.Records.Single(record =>
                    record.EventName == "viewer_ready").RemoteEndpoint,
                Is.EqualTo(pendingEndpoint));
        }

        [Test]
        public async Task SupersededCompletionCannotReplaceNewerReadyEndpoint()
        {
            const string olderEndpoint =
                "parent:https://viewer.example/older";
            const string newerEndpoint =
                "parent:https://viewer.example/newer";
            readiness.Hold = true;
            Task<CommandOperationResult> older =
                application.InitializeAsync(
                    new ViewerInitializeRequest { Revision = 8 },
                    olderEndpoint,
                    CancellationToken.None);
            Assert.That(readiness.HasStarted(8), Is.True);

            Task<CommandOperationResult> newer =
                application.InitializeAsync(
                    new ViewerInitializeRequest { Revision = 9 },
                    newerEndpoint,
                    CancellationToken.None);
            Assert.That(readiness.HasStarted(9), Is.True);

            readiness.Complete(9);
            CommandOperationResult newerResult = await newer;
            Assert.That(newerResult.Succeeded, Is.True, newerResult.Message);
            Assert.That(
                application.CurrentRemoteEndpoint,
                Is.EqualTo(newerEndpoint));

            readiness.Complete(8);
            CommandOperationResult olderResult = await older;

            Assert.That(olderResult.Succeeded, Is.False);
            Assert.That(olderResult.ErrorCode, Is.EqualTo("superseded"));
            Assert.That(
                application.CurrentRemoteEndpoint,
                Is.EqualTo(newerEndpoint));
        }

        [Test]
        public async Task DisplayCommandPublishesOneEventToInitializationEndpoint()
        {
            CommandOperationResult initialized = await application.InitializeAsync(
                new ViewerInitializeRequest { Revision = 7 },
                "parent:https://viewer.example",
                CancellationToken.None);
            Assert.That(initialized.Succeeded, Is.True, initialized.Message);
            Assert.That(application.LatestRevision, Is.EqualTo(7));
            Assert.That(
                application.CurrentRemoteEndpoint,
                Is.EqualTo("parent:https://viewer.example"));

            using (var publisher = new ViewerDisplaySettingsEventPublisher(
                       application,
                       rendering,
                       "parent:https://viewer.example/fallback"))
            {
                events.RequiredEndpoint = "parent:https://viewer.example";
                await publisher.PublishInitialAsync();
                events.Clear();

                ICommandHandler<ViewerApplication> handler =
                    ViewerCommandHandlers.CreateWithPresentation(
                            renderingController: rendering)
                        .Single(candidate =>
                            candidate.CommandNames.Contains(
                                "set_display_settings"));
                var payload = new JObject
                {
                    ["rendering_mode"] = "realistic",
                    ["camera_relative_light"] = true
                };
                var envelope = new CommandEnvelope(
                    "set_display_settings",
                    payload,
                    metadata: new CommandMetadata(
                        "test",
                        "test",
                        "parent:https://other.example"),
                    rawEnvelope: new JObject
                    {
                        ["type"] = "set_display_settings",
                        ["payload"] = payload
                    });

                CommandResult result = await handler.HandleAsync(
                    new CommandExecutionContext<ViewerApplication>(
                        application,
                        envelope,
                        "set_display_settings"),
                    CancellationToken.None);

                Assert.That(result.Succeeded, Is.True, result.Message);
                Assert.That(rendering.ApplyCount, Is.EqualTo(1));
                Assert.That(application.LatestRevision, Is.EqualTo(7));
            }

            EventRecord record = events.Records.Single();
            Assert.That(
                record.RemoteEndpoint,
                Is.EqualTo("parent:https://viewer.example"),
                "Presentation state follows the accepted viewer lifecycle " +
                "endpoint, not a one-off command transport endpoint.");
            Assert.That(
                record.Payload.Value<string>("rendering_mode"),
                Is.EqualTo("realistic"));
            Assert.That(
                record.Payload.Value<bool>("camera_relative_light"),
                Is.True);
            Assert.That(
                record.Payload.Value<string>("source"),
                Is.EqualTo("host"));
            Assert.That(record.Payload.Property("revision"), Is.Null);
        }

        [Test]
        public async Task DisposedProjectionStopsPublishing()
        {
            var publisher = new ViewerDisplaySettingsEventPublisher(
                application,
                rendering,
                "parent:https://viewer.example");
            await publisher.PublishInitialAsync();
            events.Clear();
            publisher.Dispose();

            rendering.ApplyDisplaySettings(
                new ViewerDisplaySettingsRequest(
                    ViewerRenderingMode.Realistic,
                    true),
                ViewerDisplaySettingsChangeSource.ViewerUi);

            Assert.That(events.Records, Is.Empty);
        }

        [Test]
        public async Task DisplayChangesPublishInExactEnqueueOrder()
        {
            var controlled = ReplaceWithControlledPublisher();
            using (var publisher = new ViewerDisplaySettingsEventPublisher(
                       application,
                       rendering,
                       "parent:https://viewer.example"))
            {
                await publisher.PublishInitialAsync();
                controlled.Clear();
                controlled.HoldDisplayPublications = true;

                rendering.ApplyDisplaySettings(
                    new ViewerDisplaySettingsRequest(
                        ViewerRenderingMode.Realistic,
                        true),
                    ViewerDisplaySettingsChangeSource.ViewerUi);
                rendering.ApplyDisplaySettings(
                    new ViewerDisplaySettingsRequest(
                        ViewerRenderingMode.ColorFaithful,
                        false),
                    ViewerDisplaySettingsChangeSource.Host);

                Assert.That(controlled.DisplayPublications, Has.Count.EqualTo(1));
                Assert.That(
                    controlled.DisplayPublications[0].Payload
                        .Value<string>("rendering_mode"),
                    Is.EqualTo("realistic"));
                Assert.That(
                    controlled.DisplayPublications[0].Payload
                        .Value<string>("source"),
                    Is.EqualTo("viewer_ui"));

                controlled.DisplayPublications[0].Complete();
                await WaitForDisplayCountAsync(controlled, 2);

                Assert.That(
                    controlled.DisplayPublications[1].Payload
                        .Value<string>("rendering_mode"),
                    Is.EqualTo("color_faithful"));
                Assert.That(
                    controlled.DisplayPublications[1].Payload
                        .Value<string>("source"),
                    Is.EqualTo("host"));
                controlled.DisplayPublications[1].Complete();
                await publisher.WhenIdle;
            }
        }

        [Test]
        public async Task QueuedChangeKeepsEndpointCapturedAtEnqueue()
        {
            const string firstEndpoint =
                "parent:https://viewer.example/first";
            const string secondEndpoint =
                "parent:https://viewer.example/second";
            var controlled = ReplaceWithControlledPublisher();
            using (var publisher = new ViewerDisplaySettingsEventPublisher(
                       application,
                       rendering,
                       "parent:https://viewer.example/fallback"))
            {
                await publisher.PublishInitialAsync();
                CommandOperationResult firstInitialization =
                    await application.InitializeAsync(
                        new ViewerInitializeRequest { Revision = 1 },
                        firstEndpoint,
                        CancellationToken.None);
                Assert.That(
                    firstInitialization.Succeeded,
                    Is.True,
                    firstInitialization.Message);
                controlled.Clear();
                controlled.HoldDisplayPublications = true;

                rendering.ApplyDisplaySettings(
                    new ViewerDisplaySettingsRequest(
                        ViewerRenderingMode.Realistic,
                        true),
                    ViewerDisplaySettingsChangeSource.ViewerUi);
                rendering.ApplyDisplaySettings(
                    new ViewerDisplaySettingsRequest(
                        ViewerRenderingMode.ColorFaithful,
                        false),
                    ViewerDisplaySettingsChangeSource.Host);
                CommandOperationResult secondInitialization =
                    await application.InitializeAsync(
                        new ViewerInitializeRequest { Revision = 2 },
                        secondEndpoint,
                        CancellationToken.None);
                Assert.That(
                    secondInitialization.Succeeded,
                    Is.True,
                    secondInitialization.Message);
                rendering.ApplyDisplaySettings(
                    new ViewerDisplaySettingsRequest(
                        ViewerRenderingMode.Realistic,
                        false),
                    ViewerDisplaySettingsChangeSource.QualityChange);

                Assert.That(controlled.DisplayPublications, Has.Count.EqualTo(1));
                Assert.That(
                    controlled.DisplayPublications[0].RemoteEndpoint,
                    Is.EqualTo(firstEndpoint));
                controlled.DisplayPublications[0].Complete();
                await WaitForDisplayCountAsync(controlled, 2);
                Assert.That(
                    controlled.DisplayPublications[1].RemoteEndpoint,
                    Is.EqualTo(firstEndpoint));
                controlled.DisplayPublications[1].Complete();
                await WaitForDisplayCountAsync(controlled, 3);
                Assert.That(
                    controlled.DisplayPublications[2].RemoteEndpoint,
                    Is.EqualTo(secondEndpoint));
                controlled.DisplayPublications[2].Complete();
                await publisher.WhenIdle;
            }
        }

        [Test]
        public async Task DisposeCancelsActiveAndDropsQueuedChanges()
        {
            var controlled = ReplaceWithControlledPublisher();
            var publisher = new ViewerDisplaySettingsEventPublisher(
                application,
                rendering,
                "parent:https://viewer.example");
            try
            {
                await publisher.PublishInitialAsync();
                controlled.Clear();
                controlled.HoldDisplayPublications = true;

                rendering.ApplyDisplaySettings(
                    new ViewerDisplaySettingsRequest(
                        ViewerRenderingMode.Realistic,
                        true),
                    ViewerDisplaySettingsChangeSource.ViewerUi);
                rendering.ApplyDisplaySettings(
                    new ViewerDisplaySettingsRequest(
                        ViewerRenderingMode.ColorFaithful,
                        false),
                    ViewerDisplaySettingsChangeSource.Host);

                Assert.That(controlled.DisplayPublications, Has.Count.EqualTo(1));
                Assert.That(
                    controlled.DisplayPublications[0].CancellationToken
                        .CanBeCanceled,
                    Is.True);

                publisher.Dispose();
                await publisher.WhenIdle;

                Assert.That(
                    controlled.DisplayPublications[0].CancellationToken
                        .IsCancellationRequested,
                    Is.True);
                Assert.That(controlled.DisplayPublications, Has.Count.EqualTo(1));

                rendering.ApplyDisplaySettings(
                    new ViewerDisplaySettingsRequest(
                        ViewerRenderingMode.Realistic,
                        true),
                    ViewerDisplaySettingsChangeSource.ViewerUi);
                await Task.Yield();
                Assert.That(controlled.DisplayPublications, Has.Count.EqualTo(1));
            }
            finally
            {
                publisher.Dispose();
            }
        }

        private ControlledEventPublisher ReplaceWithControlledPublisher()
        {
            application.Dispose();
            var controlled = new ControlledEventPublisher();
            application = CreateApplication(controlled);
            return controlled;
        }

        private static async Task WaitForDisplayCountAsync(
            ControlledEventPublisher publisher,
            int expectedCount)
        {
            const int maximumYields = 100;
            for (int index = 0;
                 index < maximumYields &&
                 publisher.DisplayPublications.Count < expectedCount;
                 index++)
            {
                await Task.Yield();
            }

            Assert.That(
                publisher.DisplayPublications,
                Has.Count.EqualTo(expectedCount));
        }

        private sealed class RecordingRenderingController :
            IViewerRenderingController
        {
            public event Action<
                ViewerDisplaySettingsSnapshot,
                ViewerDisplaySettingsChangeSource> SettingsChanged;

            public ViewerDisplaySettingsSnapshot CurrentSettings { get; private set; } =
                new ViewerDisplaySettingsSnapshot(
                    ViewerRenderingMode.ColorFaithful,
                    false,
                    true);

            public int ApplyCount { get; private set; }

            public void ApplyDisplaySettings(
                ViewerDisplaySettingsRequest request,
                ViewerDisplaySettingsChangeSource source)
            {
                CurrentSettings = new ViewerDisplaySettingsSnapshot(
                    request.RenderingMode ?? CurrentSettings.RenderingMode,
                    request.CameraRelativeLight ??
                    CurrentSettings.CameraRelativeLight,
                    CurrentSettings.EffectsActive);
                ApplyCount++;
                SettingsChanged?.Invoke(CurrentSettings, source);
            }
        }

        private sealed class RecordingEventPublisher : IViewerEventPublisher
        {
            private readonly List<EventRecord> records =
                new List<EventRecord>();

            public IReadOnlyList<EventRecord> Records => records;
            public string RequiredEndpoint { get; set; }

            public Task PublishAsync(
                string eventName,
                JObject payload,
                string remoteEndpoint,
                CancellationToken cancellationToken = default)
            {
                if (RequiredEndpoint != null &&
                    !string.Equals(
                        remoteEndpoint,
                        RequiredEndpoint,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Unexpected event endpoint: " + remoteEndpoint);
                }

                records.Add(new EventRecord(
                    eventName,
                    payload,
                    remoteEndpoint));
                return Task.CompletedTask;
            }

            public void Clear() => records.Clear();
        }

        private sealed class ControlledEventPublisher : IViewerEventPublisher
        {
            private readonly List<ControlledPublication>
                displayPublications =
                    new List<ControlledPublication>();

            public IReadOnlyList<ControlledPublication> DisplayPublications =>
                displayPublications;
            public bool HoldDisplayPublications { get; set; }

            public Task PublishAsync(
                string eventName,
                JObject payload,
                string remoteEndpoint,
                CancellationToken cancellationToken = default)
            {
                if (!string.Equals(
                        eventName,
                        ViewerDisplaySettingsEventPublisher.EventName,
                        StringComparison.Ordinal))
                {
                    return Task.CompletedTask;
                }

                var publication = new ControlledPublication(
                    payload,
                    remoteEndpoint,
                    cancellationToken);
                displayPublications.Add(publication);
                if (!HoldDisplayPublications)
                {
                    publication.Complete();
                }

                return publication.Task;
            }

            public void Clear() => displayPublications.Clear();
        }

        private sealed class ControlledPublication
        {
            private readonly TaskCompletionSource<bool> completion =
                new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

            public ControlledPublication(
                JObject payload,
                string remoteEndpoint,
                CancellationToken cancellationToken)
            {
                Payload = (JObject)payload.DeepClone();
                RemoteEndpoint = remoteEndpoint;
                CancellationToken = cancellationToken;
                cancellationToken.Register(() =>
                    completion.TrySetCanceled(cancellationToken));
            }

            public JObject Payload { get; }
            public string RemoteEndpoint { get; }
            public CancellationToken CancellationToken { get; }
            public Task Task => completion.Task;

            public void Complete() => completion.TrySetResult(true);
        }

        private sealed class EventRecord
        {
            public EventRecord(
                string eventName,
                JObject payload,
                string remoteEndpoint)
            {
                EventName = eventName;
                Payload = (JObject)payload.DeepClone();
                RemoteEndpoint = remoteEndpoint;
            }

            public string EventName { get; }
            public JObject Payload { get; }
            public string RemoteEndpoint { get; }
        }

        private sealed class EmbeddedOnlyLoader : IViewerModelLoader
        {
            public Task<ViewerModelLoadResult> LoadAsync(
                ViewerModelDescriptor descriptor,
                CancellationToken cancellationToken) =>
                throw new InvalidOperationException(
                    "The test uses the embedded reference model.");

            public void Unload()
            {
            }

            public void Dispose()
            {
            }
        }

        private sealed class AcceptingNavigation : IViewerReferenceNavigation
        {
            public bool AcceptReferences { get; set; } = true;

            public void BeginReferenceLoad()
            {
            }

            public bool RegisterReference(
                GameObject referenceRoot,
                bool frame,
                bool captureOrigin) =>
                AcceptReferences && referenceRoot != null;
        }

        private sealed class ControllableReadiness :
            IViewerModelReadinessFeature
        {
            private readonly Dictionary<
                long,
                TaskCompletionSource<ViewerModelReadinessResult>> pending =
                new Dictionary<
                    long,
                    TaskCompletionSource<ViewerModelReadinessResult>>();

            public bool Hold { get; set; }

            public Task<ViewerModelReadinessResult> PrepareAsync(
                ViewerModelContext context,
                string remoteEndpoint,
                CancellationToken cancellationToken)
            {
                if (!Hold)
                {
                    return Task.FromResult(
                        ViewerModelReadinessResult.Success());
                }

                var completion = new TaskCompletionSource<
                    ViewerModelReadinessResult>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                pending.Add(context.InitialRevision, completion);
                return completion.Task;
            }

            public bool HasStarted(long revision) =>
                pending.ContainsKey(revision);

            public void Complete(long revision)
            {
                if (pending.TryGetValue(
                        revision,
                        out TaskCompletionSource<ViewerModelReadinessResult>
                            completion))
                {
                    completion.TrySetResult(
                        ViewerModelReadinessResult.Success());
                    pending.Remove(revision);
                }
            }

            public void ReleaseAll()
            {
                foreach (TaskCompletionSource<ViewerModelReadinessResult>
                         completion in pending.Values)
                {
                    completion.TrySetResult(
                        ViewerModelReadinessResult.Success());
                }

                pending.Clear();
            }
        }

        private sealed class EmptyVisibilityFeatureFactory :
            IViewerVisibilityFeatureFactory
        {
            public bool TryCreate(
                ViewerModelContext context,
                out IViewerVisibilityFeature feature,
                out string error)
            {
                feature = new EmptyVisibilityFeature();
                error = string.Empty;
                return true;
            }
        }

        private sealed class EmptyVisibilityFeature : IViewerVisibilityFeature
        {
            public int IndexedElementCount => 0;
            public int SelectedElementCount => 0;
            public void Dispose()
            {
            }
        }
    }
}
