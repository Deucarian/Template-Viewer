using System;
using NUnit.Framework;
using UnityEngine;

namespace Deucarian.TemplateViewer.Tests
{
    public sealed class ViewerPlatformAdapterTests
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

        [Test]
        public void BootstrapRejectsInvalidAdapterBeforeCoreComposition()
        {
            root = new GameObject("Viewer bootstrap test");
            FakeViewerBootstrap bootstrap =
                root.AddComponent<FakeViewerBootstrap>();
            var adapter = new FakeViewerPlatformAdapter
            {
                EventEndpoint = string.Empty
            };
            bootstrap.Adapter = adapter;

            Assert.Throws<InvalidOperationException>(bootstrap.ComposeNow);
            Assert.That(bootstrap.FactoryCallCount, Is.EqualTo(1));
            Assert.That(bootstrap.Application, Is.Null);
            Assert.That(adapter.ActivationCount, Is.Zero);
            Assert.That(adapter.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void BootstrapInvokesProductValidationAndRollsBackAdapter()
        {
            root = new GameObject("Viewer validation test");
            FakeViewerBootstrap bootstrap =
                root.AddComponent<FakeViewerBootstrap>();
            var adapter = new FakeViewerPlatformAdapter();
            bootstrap.Adapter = adapter;
            bootstrap.PlatformConfigurationIsValid = false;

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(bootstrap.ComposeNow);

            Assert.That(exception.Message, Does.Contain("fake platform"));
            Assert.That(bootstrap.FactoryCallCount, Is.EqualTo(1));
            Assert.That(adapter.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void PlatformValidationAcceptsCompleteAdapter()
        {
            var adapter = new FakeViewerPlatformAdapter();

            Assert.DoesNotThrow(
                () => ViewerPlatformAdapterValidation.Validate(adapter));
        }

        [Test]
        public void ValidAdapterComposesOnceAndReleasesTransportBeforeAdapter()
        {
            root = new GameObject("Viewer adapter composition test");
            FakeViewerBootstrap bootstrap =
                root.AddComponent<FakeViewerBootstrap>();
            var adapter = new FakeViewerPlatformAdapter();
            bootstrap.Adapter = adapter;

            bootstrap.ComposeNow();

            Assert.That(bootstrap.FactoryCallCount, Is.EqualTo(1));
            Assert.That(bootstrap.Application, Is.Not.Null);
            Assert.That(bootstrap.PlatformAdapter, Is.SameAs(adapter));
            Assert.That(bootstrap.LocalCommandPort, Is.Not.Null);
            Assert.That(adapter.ActivationCount, Is.EqualTo(1));
            Assert.That(
                adapter.Lifecycles,
                Is.EqualTo(new[] { ViewerLifecycleState.Created }));

            bootstrap.ReleaseNow();
            UnityEngine.Object.DestroyImmediate(root);
            root = null;

            Assert.That(adapter.ActivationDisposeCount, Is.EqualTo(1));
            Assert.That(adapter.DisposeCount, Is.EqualTo(1));
            Assert.That(
                adapter.CleanupOrder,
                Is.EqualTo(new[] { "activation", "adapter" }));
        }
    }
}
