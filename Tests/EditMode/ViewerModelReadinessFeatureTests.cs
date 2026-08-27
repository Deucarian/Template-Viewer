using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.TemplateViewer.Loading;
using Deucarian.TemplateViewer.Selection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Deucarian.TemplateViewer.Tests
{
    public sealed class ViewerModelReadinessFeatureTests
    {
        private GameObject model;
        private RecordingPublisher publisher;
        private ControlledReadinessFeature readiness;
        private ViewerApplication application;

        [SetUp]
        public void SetUp()
        {
            model = new GameObject("Readiness model");
            GameObject element = new GameObject("element");
            element.transform.SetParent(model.transform, false);
            element.AddComponent<ViewerElement>().Initialize("element");
            publisher = new RecordingPublisher();
            readiness = new ControlledReadinessFeature();
            application = new ViewerApplication(
                new DirectViewerModelDescriptorResolver(),
                new EmbeddedOnlyModelLoader(),
                new FakeViewerReferenceNavigation(),
                publisher,
                model,
                null,
                null,
                readiness);
        }

        [TearDown]
        public void TearDown()
        {
            readiness?.ReleaseSuccess();
            application?.Dispose();
            if (model != null)
            {
                Object.DestroyImmediate(model);
            }
        }

        [Test]
        public async Task ReadyWaitsForProductModelPreparation()
        {
            Task<CommandOperationResult> initialization = InitializeAsync(1);

            Assert.That(readiness.CallCount, Is.EqualTo(1));
            Assert.That(application.Lifecycle, Is.EqualTo(ViewerLifecycleState.Loading));
            Assert.That(application.CurrentModel, Is.Null);
            CollectionAssert.AreEqual(
                new[] { "viewer_loading" },
                publisher.Events);

            readiness.ReleaseSuccess();
            CommandOperationResult result = await initialization;

            Assert.That(result.Succeeded, Is.True);
            Assert.That(application.Lifecycle, Is.EqualTo(ViewerLifecycleState.Ready));
            Assert.That(application.CurrentModel, Is.Not.Null);
            CollectionAssert.AreEqual(
                new[] { "viewer_loading", "viewer_ready" },
                publisher.Events);
        }

        [Test]
        public async Task ProductPreparationFailureUsesInitializationFailurePath()
        {
            Task<CommandOperationResult> initialization = InitializeAsync(2);
            readiness.ReleaseFailure("Report preparation failed.");

            CommandOperationResult result = await initialization;

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("initialization_failed"));
            Assert.That(result.Message, Is.EqualTo("Report preparation failed."));
            Assert.That(application.Lifecycle, Is.EqualTo(ViewerLifecycleState.Failed));
            Assert.That(application.CurrentModel, Is.Null);
            Assert.That(model.activeSelf, Is.False);
            CollectionAssert.AreEqual(
                new[] { "viewer_loading", "viewer_failed" },
                publisher.Events);
        }

        private Task<CommandOperationResult> InitializeAsync(long revision) =>
            application.InitializeAsync(
                new ViewerInitializeRequest { Revision = revision },
                "test://viewer",
                CancellationToken.None);

        private sealed class ControlledReadinessFeature :
            IViewerModelReadinessFeature
        {
            private readonly TaskCompletionSource<ViewerModelReadinessResult>
                completion =
                    new TaskCompletionSource<ViewerModelReadinessResult>();

            public int CallCount { get; private set; }

            public Task<ViewerModelReadinessResult> PrepareAsync(
                ViewerModelContext context,
                string remoteEndpoint,
                CancellationToken cancellationToken)
            {
                CallCount++;
                return completion.Task;
            }

            public void ReleaseSuccess() =>
                completion.TrySetResult(ViewerModelReadinessResult.Success());

            public void ReleaseFailure(string message) =>
                completion.TrySetResult(
                    ViewerModelReadinessResult.Failure(message));
        }

        private sealed class RecordingPublisher : IViewerEventPublisher
        {
            public List<string> Events { get; } = new List<string>();

            public Task PublishAsync(
                string eventName,
                JObject payload,
                string remoteEndpoint,
                CancellationToken cancellationToken = default)
            {
                Events.Add(eventName);
                return Task.CompletedTask;
            }
        }

        private sealed class EmbeddedOnlyModelLoader : IViewerModelLoader
        {
            public Task<ViewerModelLoadResult> LoadAsync(
                ViewerModelDescriptor descriptor,
                CancellationToken cancellationToken) =>
                Task.FromResult(
                    ViewerModelLoadResult.Failure(
                        "External loading was not expected."));

            public void Unload()
            {
            }

            public void Dispose()
            {
            }
        }
    }
}
