using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Deucarian.TemplateViewer.Tests
{
    public sealed class ViewerFeatureCompositionTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<Scene> scenes = new List<Scene>();
        private Scene originalActiveScene;

        [SetUp]
        public void SetUp()
        {
            originalActiveScene = SceneManager.GetActiveScene();
        }

        [TearDown]
        public void TearDown()
        {
            for (int index = objects.Count - 1; index >= 0; index--)
            {
                if (objects[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(objects[index]);
                }
            }

            if (originalActiveScene.IsValid() && originalActiveScene.isLoaded)
            {
                SceneManager.SetActiveScene(originalActiveScene);
            }

            for (int index = scenes.Count - 1; index >= 0; index--)
            {
                if (!scenes[index].IsValid() || !scenes[index].isLoaded)
                {
                    continue;
                }

                if (EditorSceneManager.IsPreviewScene(scenes[index]))
                {
                    EditorSceneManager.ClosePreviewScene(scenes[index]);
                }
                else
                {
                    EditorSceneManager.CloseScene(scenes[index], true);
                }
            }
        }

        [Test]
        public void LocalFeaturesPrecedeExplicitFeaturesInDeterministicOrder()
        {
            GameObject root = CreateObject("Viewer feature order");
            FakeViewerBootstrap bootstrap =
                root.AddComponent<FakeViewerBootstrap>();
            RecordingViewerFeature localFirst =
                root.AddComponent<RecordingViewerFeature>();
            RecordingViewerFeature localSecond =
                root.AddComponent<RecordingViewerFeature>();
            RecordingViewerFeature explicitFirst = CreateFeature(
                "Explicit first");
            RecordingViewerFeature explicitSecond = CreateFeature(
                "Explicit second");

            SetExplicitFeatures(
                bootstrap,
                explicitSecond,
                localSecond,
                explicitFirst,
                explicitSecond);

            IReadOnlyList<ViewerFeatureBehaviour> resolved =
                bootstrap.ResolvedFeatureBehaviours;

            CollectionAssert.AreEqual(
                new ViewerFeatureBehaviour[]
                {
                    localFirst,
                    localSecond,
                    explicitSecond,
                    explicitFirst
                },
                resolved);
        }

        [Test]
        public void SerializedExplicitFeatureIsAttachedAndDetachedOnlyOnce()
        {
            GameObject root = CreateObject("Viewer explicit composition");
            FakeViewerBootstrap bootstrap =
                root.AddComponent<FakeViewerBootstrap>();
            RecordingViewerFeature local =
                root.AddComponent<RecordingViewerFeature>();
            RecordingViewerFeature explicitFeature = CreateFeature(
                "Explicit composed feature");
            bootstrap.Adapter = new FakeViewerPlatformAdapter();
            SetExplicitFeatures(
                bootstrap,
                explicitFeature,
                local,
                explicitFeature);

            bootstrap.ComposeNow();

            Assert.That(local.AttachCount, Is.EqualTo(1));
            Assert.That(explicitFeature.AttachCount, Is.EqualTo(1));

            bootstrap.ReleaseNow();
            UnityEngine.Object.DestroyImmediate(root);

            Assert.That(explicitFeature.DetachCount, Is.EqualTo(1));
        }

        [Test]
        public void NullExplicitFeatureFailsBeforePresentationComposition()
        {
            GameObject root = CreateObject("Viewer null feature");
            FakeViewerBootstrap bootstrap =
                root.AddComponent<FakeViewerBootstrap>();
            var adapter = new FakeViewerPlatformAdapter();
            var navigation = new FakeViewerReferenceNavigation();
            bootstrap.Adapter = adapter;
            bootstrap.TestReferenceNavigation = navigation;
            SetExplicitFeatures(
                bootstrap,
                new ViewerFeatureBehaviour[] { null });

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(bootstrap.ComposeNow);

            Assert.That(exception.Message, Does.Contain("index 0"));
            Assert.That(exception.Message, Does.Contain("null or destroyed"));
            Assert.That(navigation.BeginCount, Is.Zero);
            Assert.That(bootstrap.Application, Is.Null);
            Assert.That(adapter.ActivationCount, Is.Zero);
            Assert.That(adapter.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void CrossSceneExplicitFeatureReportsBothScenesBeforeComposition()
        {
            string suffix = Guid.NewGuid().ToString("N");
            Scene bootstrapScene = CreateScene("Viewer bootstrap " + suffix);
            Scene featureScene = CreateScene("Viewer feature " + suffix);
            GameObject root = CreateObject("Cross-scene viewer");
            SceneManager.MoveGameObjectToScene(root, bootstrapScene);
            FakeViewerBootstrap bootstrap =
                root.AddComponent<FakeViewerBootstrap>();
            RecordingViewerFeature explicitFeature = CreateFeature(
                "Cross-scene feature");
            SceneManager.MoveGameObjectToScene(
                explicitFeature.gameObject,
                featureScene);
            var adapter = new FakeViewerPlatformAdapter();
            var navigation = new FakeViewerReferenceNavigation();
            bootstrap.Adapter = adapter;
            bootstrap.TestReferenceNavigation = navigation;
            SetExplicitFeatures(bootstrap, explicitFeature);

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(bootstrap.ComposeNow);

            Assert.That(exception.Message, Does.Contain("index 0"));
            Assert.That(exception.Message, Does.Contain(bootstrapScene.name));
            Assert.That(exception.Message, Does.Contain(featureScene.name));
            Assert.That(exception.Message, Does.Contain("Move the feature"));
            Assert.That(navigation.BeginCount, Is.Zero);
            Assert.That(adapter.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void MultipleModelReadinessOwnersAreRejected()
        {
            RecordingViewerFeature first = CreateFeature("First readiness");
            RecordingViewerFeature second = CreateFeature("Second readiness");
            first.ReadinessFeature = new SuccessfulReadinessFeature();
            second.ReadinessFeature = new SuccessfulReadinessFeature();

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() =>
                    ViewerFeatureComposition.ResolveModelReadinessFeature(
                        new ViewerFeatureBehaviour[] { first, second }));

            Assert.That(exception.Message, Does.Contain("model readiness"));
        }

        [Test]
        public async Task ComposedFeatureObservesCompletedCommands()
        {
            GameObject root = CreateObject("Viewer command observation");
            FakeViewerBootstrap bootstrap =
                root.AddComponent<FakeViewerBootstrap>();
            RecordingViewerFeature feature =
                root.AddComponent<RecordingViewerFeature>();
            bootstrap.Adapter = new FakeViewerPlatformAdapter();
            bootstrap.ComposeNow();

            await bootstrap.LocalCommandPort.RouteMessageAsync(
                "{\"command\":\"unsupported_product_command\"}",
                "test",
                "test://viewer",
                CancellationToken.None);

            Assert.That(feature.CommandCompletedCount, Is.EqualTo(1));
            Assert.That(feature.LastCommandCompleted, Is.Not.Null);
            Assert.That(feature.LastCommandCompleted.Result.Succeeded, Is.False);
        }

        private GameObject CreateObject(string name)
        {
            var value = new GameObject(name);
            objects.Add(value);
            return value;
        }

        private RecordingViewerFeature CreateFeature(string name) =>
            CreateObject(name).AddComponent<RecordingViewerFeature>();

        private Scene CreateScene(string name)
        {
            Scene value = EditorSceneManager.NewPreviewScene();
            value.name = name;
            scenes.Add(value);
            return value;
        }

        private static void SetExplicitFeatures(
            ViewerBootstrap bootstrap,
            params ViewerFeatureBehaviour[] features)
        {
            FieldInfo field = typeof(ViewerBootstrap).GetField(
                "explicitFeatureBehaviours",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            Assert.That(
                field.IsDefined(typeof(SerializeField), false),
                Is.True);
            field.SetValue(bootstrap, features);
        }

        private sealed class SuccessfulReadinessFeature :
            IViewerModelReadinessFeature
        {
            public System.Threading.Tasks.Task<ViewerModelReadinessResult>
                PrepareAsync(
                    ViewerModelContext context,
                    string remoteEndpoint,
                    System.Threading.CancellationToken cancellationToken) =>
                System.Threading.Tasks.Task.FromResult(
                    ViewerModelReadinessResult.Success());
        }
    }
}
