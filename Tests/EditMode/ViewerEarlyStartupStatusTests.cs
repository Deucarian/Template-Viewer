using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Deucarian.TemplateViewer.Tests
{
    public sealed class ViewerEarlyStartupStatusTests
    {
        private GameObject root;
        private FakeViewerBootstrap bootstrap;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Early viewer startup status");
            bootstrap = root.AddComponent<FakeViewerBootstrap>();
            bootstrap.enabled = false;
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(root);

        [Test]
        public void FactoryFailureIsPublishedWithoutAnyPlatformOrUi()
        {
            var failure = new InvalidOperationException("synthetic private detail");
            var sink = new RecordingSink();
            bootstrap.EarlyStatusSink = sink;
            bootstrap.FactoryFailure = failure;

            Assert.That(Assert.Throws<InvalidOperationException>(
                bootstrap.ComposeNow), Is.SameAs(failure));
            Assert.That(bootstrap.EarlySinkFactoryCallCount, Is.EqualTo(1));
            Assert.That(bootstrap.FactoryCallCount, Is.EqualTo(1));
            Assert.That(bootstrap.Application, Is.Null);
            Assert.That(bootstrap.PlatformAdapter, Is.Null);
            Assert.That(sink.States, Is.EqualTo(new[] { ViewerLifecycleState.Failed }));
            Assert.That(sink.Messages, Is.EqualTo(new[] { "viewer_composition_failed" }));
        }

        [Test]
        public void LateCompositionFailurePublishesAfterCleanupUsingTheEarlySink()
        {
            var adapter = new FakeViewerPlatformAdapter { ReturnNullActivation = true };
            var sink = new RecordingSink();
            bool cleanupObserved = false;
            sink.OnFailed = () => cleanupObserved =
                bootstrap.Application == null && bootstrap.PlatformAdapter == null &&
                adapter.DisposeCount == 1;
            bootstrap.Adapter = adapter;
            bootstrap.EarlyStatusSink = sink;

            Assert.Throws<InvalidOperationException>(bootstrap.ComposeNow);

            Assert.That(sink.States, Is.EqualTo(new[]
                { ViewerLifecycleState.Created, ViewerLifecycleState.Failed }));
            Assert.That(adapter.Lifecycles, Is.Empty, "There must not be a second lifecycle publisher.");
            Assert.That(bootstrap.EarlySinkFactoryCallCount, Is.EqualTo(1));
            Assert.That(cleanupObserved, Is.True);
        }

        [Test]
        public void SuccessReusesOneSinkAndDoesNotAnnounceApplicationReady()
        {
            var adapter = new FakeViewerPlatformAdapter();
            var sink = new RecordingSink();
            bootstrap.Adapter = adapter;
            bootstrap.EarlyStatusSink = sink;

            bootstrap.ComposeNow();

            Assert.That(sink.States, Is.EqualTo(new[] { ViewerLifecycleState.Created }));
            Assert.That(adapter.Lifecycles, Is.Empty);
            Assert.That(bootstrap.EarlySinkFactoryCallCount, Is.EqualTo(1));
        }

        [Test]
        public void BrokenStatusSinkCannotMaskTheOriginalCompositionException()
        {
            var failure = new InvalidOperationException("original failure");
            bootstrap.FactoryFailure = failure;
            bootstrap.EarlyStatusSink = new RecordingSink
            {
                OnFailed = () => throw new ArgumentException("observer failure")
            };

            Assert.That(Assert.Throws<InvalidOperationException>(
                bootstrap.ComposeNow), Is.SameAs(failure));
        }

        private sealed class RecordingSink : IViewerLifecycleStatusSink
        {
            internal readonly List<ViewerLifecycleState> States =
                new List<ViewerLifecycleState>();
            internal readonly List<string> Messages = new List<string>();
            internal Action OnFailed;

            public void ReportLifecycle(ViewerLifecycleState state, string message)
            {
                States.Add(state);
                Messages.Add(message);
                if (state == ViewerLifecycleState.Failed) OnFailed?.Invoke();
            }

            public void ReportLoadingProgress(string operationId, float normalized, string message) { }
        }
    }
}
