using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.CommandRouting;
using Deucarian.TemplateViewer.Commands;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Deucarian.TemplateViewer.Tests
{
    public sealed class ViewerCommandFailureProjectionTests
    {
        private GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null)
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [TestCase(
            "",
            null,
            "invalid_json",
            "Viewer command JSON could not be parsed.")]
        [TestCase(
            "{",
            null,
            "invalid_json",
            "Viewer command JSON could not be parsed.")]
        [TestCase(
            "{}",
            null,
            "missing_command",
            "Viewer command requires a command name.")]
        [TestCase(
            "{\"command\":\"unknown\"}",
            "unknown",
            "unsupported_command",
            "Unsupported viewer command: unknown.")]
        public async Task ProtocolAndUnsupportedFailuresUseCanonicalShape(
            string message,
            string expectedCommand,
            string expectedCode,
            string expectedMessage)
        {
            var context = new QueuedSynchronizationContext();
            var publications = new List<Publication>();
            var observations =
                new List<ViewerCommandFailureProjectionEventArgs>();
            using (CommandRoutingRuntime<ViewerApplication> runtime =
                   CreateRuntime())
            using (var projector = new ViewerCommandFailureProjector(
                       runtime,
                       context,
                       (name, payload, endpoint, token) =>
                       {
                           publications.Add(new Publication(
                               name,
                               payload,
                               endpoint,
                               token));
                           return Task.CompletedTask;
                       },
                       observations.Add))
            {
                const string endpoint = " route-endpoint ";
                await runtime.RouteJsonAsync(
                    message,
                    "test",
                    endpoint);

                Assert.That(publications, Is.Empty);
                context.DrainAll();
                await projector.WhenIdle;

                Assert.That(publications, Has.Count.EqualTo(1));
                Assert.That(observations, Has.Count.EqualTo(1));
                Publication published = publications[0];
                Assert.That(
                    observations[0].Command,
                    Is.EqualTo(expectedCommand));
                Assert.That(
                    published.EventName,
                    Is.EqualTo(
                        ViewerCommandFailureProjectionEventArgs.EventName));
                Assert.That(
                    published.Payload["command"].Type,
                    Is.EqualTo(expectedCommand == null
                        ? JTokenType.Null
                        : JTokenType.String));
                Assert.That(
                    published.Payload.Value<string>("command"),
                    Is.EqualTo(expectedCommand));
                Assert.That(
                    published.Payload.Value<string>("error_code"),
                    Is.EqualTo(expectedCode));
                Assert.That(
                    published.Payload.Value<string>("message"),
                    Is.EqualTo(expectedMessage));
                Assert.That(
                    published.RemoteEndpoint,
                    Is.EqualTo(expectedCommand == null
                        ? endpoint
                        : endpoint.Trim()));
            }
        }

        [Test]
        public async Task OversizedMessagesUseInvalidJsonProjection()
        {
            var context = new QueuedSynchronizationContext();
            var publications = new List<Publication>();
            using (CommandRoutingRuntime<ViewerApplication> runtime =
                   CreateRuntime())
            using (var projector = CreateProjector(
                       runtime,
                       context,
                       publications))
            {
                await runtime.RouteJsonAsync(
                    new string('x', 257),
                    remoteEndpoint: "oversized-endpoint");
                context.DrainAll();
                await projector.WhenIdle;

                Assert.That(publications, Has.Count.EqualTo(1));
                Assert.That(
                    publications[0].Payload.Value<string>("error_code"),
                    Is.EqualTo("invalid_json"));
                Assert.That(
                    publications[0].Payload["command"].Type,
                    Is.EqualTo(JTokenType.Null));
            }
        }

        [Test]
        public async Task DomainPayloadIsClonedThenCanonicalFieldsOverwrite()
        {
            var context = new QueuedSynchronizationContext();
            var publications = new List<Publication>();
            ViewerCommandFailureProjectionEventArgs observation = null;
            using (CommandRoutingRuntime<ViewerApplication> runtime =
                   CreateRuntime(new DomainFailureHandler()))
            using (var projector = new ViewerCommandFailureProjector(
                       runtime,
                       context,
                       (name, payload, endpoint, token) =>
                       {
                           publications.Add(new Publication(
                               name,
                               payload,
                               endpoint,
                               token));
                           return Task.CompletedTask;
                       },
                       value => observation = value))
            {
                await runtime.RouteJsonAsync(
                    "{\"command\":\"select_activity\"}",
                    remoteEndpoint: "activity-endpoint");
                context.DrainAll();
                await projector.WhenIdle;

                JObject payload = publications.Single().Payload;
                Assert.That(
                    payload.Value<string>("activity_id"),
                    Is.EqualTo("912"));
                Assert.That(
                    payload["details"]?["kept"]?.Value<bool>(),
                    Is.True);
                Assert.That(
                    payload.Value<string>("command"),
                    Is.EqualTo("select_activity"));
                Assert.That(
                    payload.Value<string>("error_code"),
                    Is.EqualTo("activity_not_found"));
                Assert.That(
                    payload.Value<string>("message"),
                    Is.EqualTo("No Activity exists with ID 912."));
                Assert.That(
                    publications[0].RemoteEndpoint,
                    Is.EqualTo("activity-endpoint"));

                JObject observerCopy = observation.Payload;
                observerCopy["activity_id"] = "mutated";
                Assert.That(
                    publications[0].Payload.Value<string>("activity_id"),
                    Is.EqualTo("912"));
                Assert.That(
                    observation.Payload.Value<string>("activity_id"),
                    Is.EqualTo("912"));
            }
        }

        [Test]
        public async Task PublicationsAreSerializedInCompletionOrder()
        {
            var context = new QueuedSynchronizationContext();
            var publications = new List<Publication>();
            var firstRelease = new TaskCompletionSource<bool>();
            using (CommandRoutingRuntime<ViewerApplication> runtime =
                   CreateRuntime())
            using (var projector = new ViewerCommandFailureProjector(
                       runtime,
                       context,
                       (name, payload, endpoint, token) =>
                       {
                           publications.Add(new Publication(
                               name,
                               payload,
                               endpoint,
                               token));
                           return publications.Count == 1
                               ? firstRelease.Task
                               : Task.CompletedTask;
                       },
                       _ => { }))
            {
                await runtime.RouteJsonAsync(
                    "{\"command\":\"first\"}");
                await runtime.RouteJsonAsync(
                    "{\"command\":\"second\"}");
                context.DrainAll();

                Assert.That(publications, Has.Count.EqualTo(1));
                firstRelease.TrySetResult(true);
                context.DrainAll();
                await projector.WhenIdle;

                Assert.That(
                    publications.Select(value =>
                        value.Payload.Value<string>("command")),
                    Is.EqualTo(new[] { "first", "second" }));
            }
        }

        [Test]
        public async Task DisposeCancelsAndIgnoresQueuedWork()
        {
            var context = new QueuedSynchronizationContext();
            var publications = new List<Publication>();
            var firstRelease = new TaskCompletionSource<bool>();
            using (CommandRoutingRuntime<ViewerApplication> runtime =
                   CreateRuntime())
            {
                var projector = new ViewerCommandFailureProjector(
                    runtime,
                    context,
                    (name, payload, endpoint, token) =>
                    {
                        publications.Add(new Publication(
                            name,
                            payload,
                            endpoint,
                            token));
                        return publications.Count == 1
                            ? firstRelease.Task
                            : Task.CompletedTask;
                    },
                    _ => { });

                await runtime.RouteJsonAsync(
                    "{\"command\":\"first\"}");
                await runtime.RouteJsonAsync(
                    "{\"command\":\"second\"}");
                context.DrainAll();

                Assert.That(publications, Has.Count.EqualTo(1));
                Assert.That(
                    publications[0].Payload.Value<string>("command"),
                    Is.EqualTo("first"));

                projector.Dispose();
                Assert.That(
                    publications[0].CancellationToken.IsCancellationRequested,
                    Is.True);
                firstRelease.TrySetResult(true);
                context.DrainAll();
                await projector.WhenIdle;

                Assert.That(publications, Has.Count.EqualTo(1));
                await runtime.RouteJsonAsync(
                    "{\"command\":\"third\"}");
                context.DrainAll();
                Assert.That(publications, Has.Count.EqualTo(1));
            }
        }

        [Test]
        public async Task WorkerCompletionAndThrowingSubscriberStayIsolated()
        {
            var context = new QueuedSynchronizationContext();
            int ownerThread = Thread.CurrentThread.ManagedThreadId;
            int publicationThread = 0;
            int observationThread = 0;
            int publicationCount = 0;
            using (CommandRoutingRuntime<ViewerApplication> runtime =
                   CreateRuntime())
            using (var projector = new ViewerCommandFailureProjector(
                       runtime,
                       context,
                       (name, payload, endpoint, token) =>
                       {
                           publicationThread =
                               Thread.CurrentThread.ManagedThreadId;
                           publicationCount++;
                           return Task.CompletedTask;
                       },
                       _ => observationThread =
                           Thread.CurrentThread.ManagedThreadId))
            {
                runtime.RouteCompleted += (sender, args) =>
                    throw new InvalidOperationException(
                        "Expected subscriber failure.");

                await Task.Run(async () =>
                    await runtime.RouteJsonAsync(
                        "{\"command\":\"worker_failure\"}"));

                Assert.That(publicationCount, Is.Zero);
                context.DrainAll();
                await projector.WhenIdle;
                Assert.That(publicationCount, Is.EqualTo(1));
                Assert.That(publicationThread, Is.EqualTo(ownerThread));
                Assert.That(observationThread, Is.EqualTo(ownerThread));
            }
        }

        [Test]
        public async Task BootstrapInvokesEveryFeatureOnceAndPublishesOnce()
        {
            root = new GameObject("Viewer failure projection");
            FakeViewerBootstrap bootstrap =
                root.AddComponent<FakeViewerBootstrap>();
            RecordingViewerFeature throwing =
                root.AddComponent<RecordingViewerFeature>();
            throwing.ThrowOnCommandFailureProjection = true;
            RecordingViewerFeature recording =
                root.AddComponent<RecordingViewerFeature>();
            var adapter = new FakeViewerPlatformAdapter();
            bootstrap.Adapter = adapter;
            bootstrap.ComposeNow();

            await bootstrap.LocalCommandPort.RouteMessageAsync(
                "{\"command\":\"unsupported_product_command\"}",
                "test",
                "exact://route");
            await bootstrap.CommandFailureProjectionIdle;

            FakeViewerPublishedEvent[] failures = adapter.PublishedEvents
                .Where(value => value.EventName ==
                    ViewerCommandFailureProjectionEventArgs.EventName)
                .ToArray();
            Assert.That(failures, Has.Length.EqualTo(1));
            Assert.That(
                failures[0].RemoteEndpoint,
                Is.EqualTo("exact://route"));
            Assert.That(throwing.CommandCompletedCount, Is.EqualTo(1));
            Assert.That(recording.CommandCompletedCount, Is.EqualTo(1));
            Assert.That(
                throwing.CommandFailureProjectedCount,
                Is.EqualTo(1));
            Assert.That(
                recording.CommandFailureProjectedCount,
                Is.EqualTo(1));
        }

        [Test]
        public async Task ProductProjectionPolicyPreservesResultAndCanonicalFields()
        {
            root = new GameObject("Viewer failure product policy");
            FakeViewerBootstrap bootstrap =
                root.AddComponent<FakeViewerBootstrap>();
            RecordingViewerFeature feature =
                root.AddComponent<RecordingViewerFeature>();
            feature.CommandHandlers = new[]
            {
                new DomainFailureHandler()
            };
            feature.FailureProjectionCommand = "select_activity";
            feature.FailureProjectionFieldToRemove = "activity_id";
            feature.MutateCanonicalFailureFields = true;
            var adapter = new FakeViewerPlatformAdapter();
            bootstrap.Adapter = adapter;
            bootstrap.ComposeNow();

            CommandRouteOutcome outcome =
                await bootstrap.LocalCommandPort.RouteMessageAsync(
                    "{\"command\":\"select_activity\"}",
                    "test",
                    "activity-endpoint");
            await bootstrap.CommandFailureProjectionIdle;

            Assert.That(
                outcome.Result.Payload.Value<string>("activity_id"),
                Is.EqualTo("912"),
                "Projection policy must not change the command response.");
            FakeViewerPublishedEvent failure = adapter.PublishedEvents.Single(
                value => value.EventName ==
                    ViewerCommandFailureProjectionEventArgs.EventName);
            Assert.That(failure.Payload["activity_id"], Is.Null);
            Assert.That(
                failure.Payload.Value<string>("command"),
                Is.EqualTo("select_activity"));
            Assert.That(
                failure.Payload.Value<string>("error_code"),
                Is.EqualTo("activity_not_found"));
            Assert.That(
                failure.Payload.Value<string>("message"),
                Is.EqualTo("No Activity exists with ID 912."));
            Assert.That(
                feature.FailureProjectionCustomizationCount,
                Is.EqualTo(1));
            Assert.That(
                feature.LastCommandFailureProjection.Payload["activity_id"],
                Is.Null);
        }

        private static ViewerCommandFailureProjector CreateProjector(
            CommandRoutingRuntime<ViewerApplication> runtime,
            SynchronizationContext context,
            ICollection<Publication> publications) =>
            new ViewerCommandFailureProjector(
                runtime,
                context,
                (name, payload, endpoint, token) =>
                {
                    publications.Add(new Publication(
                        name,
                        payload,
                        endpoint,
                        token));
                    return Task.CompletedTask;
                },
                _ => { });

        private static CommandRoutingRuntime<ViewerApplication> CreateRuntime(
            params ICommandHandler<ViewerApplication>[] handlers) =>
            new CommandRoutingRuntime<ViewerApplication>(
                null,
                handlers ?? Array.Empty<ICommandHandler<ViewerApplication>>(),
                new CommandRoutingOptions(
                    maximumMessageCharacters: 256,
                    logSuccessfulCommands: false,
                    logFailedCommands: false));

        private sealed class DomainFailureHandler :
            ICommandHandler<ViewerApplication>
        {
            public IReadOnlyList<string> CommandNames { get; } =
                new[] { "select_activity" };

            public Task<CommandResult> HandleAsync(
                CommandExecutionContext<ViewerApplication> context,
                CancellationToken cancellationToken) =>
                Task.FromResult(CommandResult.Failure(
                    "activity_not_found",
                    "No Activity exists with ID 912.",
                    new JObject
                    {
                        ["activity_id"] = "912",
                        ["command"] = "wrong",
                        ["error_code"] = "wrong",
                        ["message"] = "wrong",
                        ["details"] = new JObject { ["kept"] = true }
                    }));
        }

        private sealed class QueuedSynchronizationContext :
            SynchronizationContext
        {
            private readonly object gate = new object();
            private readonly Queue<Action> callbacks = new Queue<Action>();
            private readonly int ownerThread =
                Thread.CurrentThread.ManagedThreadId;

            public override void Post(SendOrPostCallback callback, object state)
            {
                lock (gate)
                {
                    callbacks.Enqueue(() => callback(state));
                }
            }

            internal void DrainAll()
            {
                Assert.That(
                    Thread.CurrentThread.ManagedThreadId,
                    Is.EqualTo(ownerThread));
                while (true)
                {
                    Action callback;
                    lock (gate)
                    {
                        if (callbacks.Count == 0)
                        {
                            return;
                        }

                        callback = callbacks.Dequeue();
                    }

                    callback();
                }
            }
        }

        private sealed class Publication
        {
            internal Publication(
                string eventName,
                JObject payload,
                string remoteEndpoint,
                CancellationToken cancellationToken)
            {
                EventName = eventName;
                Payload = (JObject)payload.DeepClone();
                RemoteEndpoint = remoteEndpoint;
                CancellationToken = cancellationToken;
            }

            internal string EventName { get; }
            internal JObject Payload { get; }
            internal string RemoteEndpoint { get; }
            internal CancellationToken CancellationToken { get; }
        }
    }
}
