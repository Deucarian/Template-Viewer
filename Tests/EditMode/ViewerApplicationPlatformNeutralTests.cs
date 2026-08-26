using System;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.TemplateViewer.Loading;
using Deucarian.TemplateViewer.Selection;
using NUnit.Framework;
using UnityEngine;

namespace Deucarian.TemplateViewer.Tests
{
    public sealed class ViewerApplicationPlatformNeutralTests
    {
        private GameObject model;
        private ViewerApplication application;

        [TearDown]
        public void TearDown()
        {
            application?.Dispose();
            if (model != null)
            {
                UnityEngine.Object.DestroyImmediate(model);
            }
        }

        [Test]
        public async Task FakeAdapterPublisherSupportsGenericSelection()
        {
            model = new GameObject("Reference model");
            GameObject red = CreateElement(model.transform, "red");
            GameObject blue = CreateElement(model.transform, "blue");
            var adapter = new FakeViewerPlatformAdapter();
            var navigation = new FakeViewerReferenceNavigation();
            application = new ViewerApplication(
                new DirectViewerModelDescriptorResolver(),
                new EmbeddedModelLoader(),
                navigation,
                adapter.EventPublisher,
                model);

            CommandOperationResult initialized =
                await application.InitializeAsync(
                    new ViewerInitializeRequest { Revision = 1 },
                    adapter.EventEndpoint,
                    CancellationToken.None);
            CommandOperationResult selected = await application.SelectAsync(
                new ViewerSelectionRequest
                {
                    Revision = 2,
                    ElementIds = { "blue" }
                },
                adapter.EventEndpoint,
                CancellationToken.None);

            Assert.That(initialized.Succeeded, Is.True);
            Assert.That(selected.Succeeded, Is.True);
            Assert.That(application.Lifecycle, Is.EqualTo(ViewerLifecycleState.Ready));
            Assert.That(application.SelectedElementCount, Is.EqualTo(1));
            Assert.That(red.activeSelf, Is.False);
            Assert.That(blue.activeSelf, Is.True);
            Assert.That(navigation.RegisterCount, Is.EqualTo(1));
            Assert.That(
                adapter.Events,
                Does.Contain("viewer_ready@test://viewer"));
            Assert.That(
                adapter.Events,
                Does.Contain("selection_applied@test://viewer"));
        }

        [Test]
        public void LifecycleStatusAdapterForwardsCoreStatusToPlatformSink()
        {
            var adapter = new FakeViewerPlatformAdapter();
            application = new ViewerApplication(
                new DirectViewerModelDescriptorResolver(),
                new EmbeddedModelLoader(),
                new FakeViewerReferenceNavigation(),
                adapter.EventPublisher);

            using (var status = new ViewerShellStatusAdapter(
                       application,
                       null,
                       adapter.LifecycleStatusSink))
            {
                application.ReportLoadingProgress(0.5f, "Halfway");
            }

            Assert.That(
                adapter.Lifecycles,
                Is.EqualTo(new[] { ViewerLifecycleState.Created }));
            Assert.That(adapter.ProgressCount, Is.EqualTo(1));
        }

        private static GameObject CreateElement(
            Transform parent,
            string id)
        {
            var element = new GameObject(id);
            element.transform.SetParent(parent, false);
            element.AddComponent<ViewerElement>().Initialize(id);
            return element;
        }

        private sealed class EmbeddedModelLoader : IViewerModelLoader
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
