using System;
using System.Collections;
using System.Linq;
using Deucarian.PointerCapture;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deucarian.TemplateViewer.Tests
{
    public sealed class ViewerPointerCaptureScopeTests
    {
        private GameObject root;
        private FakeViewerBootstrap bootstrap;
        private FakeViewerPlatformAdapter adapter;
        private DeucarianPointerCaptureController[] before;

        [SetUp]
        public void SetUp()
        {
            before = CaptureHosts();
            root = new GameObject("Viewer capture lifetime test");
            bootstrap = root.AddComponent<FakeViewerBootstrap>();
            bootstrap.enabled = false;
            adapter = new FakeViewerPlatformAdapter();
            bootstrap.Adapter = adapter;
            bootstrap.UseReferencePresentation = true;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (root != null) Object.Destroy(root);
            yield return null;
            yield return null;
            Assert.That(NewHosts(), Is.Empty, "Viewer capture hosts must be released.");
        }

        [UnityTest]
        public IEnumerator CompositionLendsSessionWithoutAddingSceneCaptureController()
        {
            bootstrap.ComposeNow();
            var gate = bootstrap.NavigationInstaller.Controller.InteractionGate;
            Assert.That(gate.CaptureSession, Is.Not.Null);
            Assert.That(root.GetComponentsInChildren<DeucarianPointerCaptureController>(true),
                Is.Empty, "The template must use a scope instead of the legacy scene path.");
            var hosts = NewHosts();
            Assert.That(hosts, Has.Length.EqualTo(1));
            Assert.That(hosts[0].gameObject.scene.name, Is.EqualTo("DontDestroyOnLoad"));

            bootstrap.ReleaseNow();
            bootstrap.ReleaseNow();
            Assert.That(hosts[0] == null || !hosts[0].gameObject.activeSelf, Is.True,
                "Release must synchronously deactivate capture before deferred destruction.");
            yield return null;
            yield return null;
            Assert.That(NewHosts(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator RepeatedDisableEnableRetainsOneScopeAndBorrowedSession()
        {
            bootstrap.ComposeNow();
            var gate = bootstrap.NavigationInstaller.Controller.InteractionGate;
            IPointerCaptureSession session = gate.CaptureSession;
            var host = NewHosts().Single();
            for (int attempt = 0; attempt < 3; attempt++)
            {
                root.SetActive(false);
                yield return null;
                root.SetActive(true);
                yield return null;
                Assert.That(gate.CaptureSession, Is.SameAs(session));
                Assert.That(NewHosts(), Is.EqualTo(new[] { host }));
                Assert.That(root.GetComponentsInChildren<DeucarianPointerCaptureController>(true),
                    Is.Empty);
            }
        }

        [UnityTest]
        public IEnumerator InitializationFailureReleasesCaptureAndRetryUsesANewScope()
        {
            adapter.ReturnNullActivation = true;
            var failure = Assert.Throws<InvalidOperationException>(() => bootstrap.ComposeNow());
            Assert.That(failure.Message, Does.Contain("no command transport activation lease"));
            Assert.That(bootstrap.Application, Is.Null);
            Assert.That(NewHosts().All(host => !host.gameObject.activeSelf), Is.True);
            yield return null;
            yield return null;
            Assert.That(NewHosts(), Is.Empty);

            adapter.ReturnNullActivation = false;
            bootstrap.ComposeNow();
            Assert.That(bootstrap.Application, Is.Not.Null);
            Assert.That(NewHosts(), Has.Length.EqualTo(1));
            Assert.That(bootstrap.NavigationInstaller.Controller.InteractionGate.CaptureSession,
                Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator SceneUnloadReleasesThePersistentCaptureHost()
        {
            Scene scene = SceneManager.CreateScene("Viewer capture owner scene");
            SceneManager.MoveGameObjectToScene(root, scene);
            bootstrap.ComposeNow();
            var host = NewHosts().Single();
            Assert.That(host.gameObject.scene, Is.Not.EqualTo(scene));
            yield return SceneManager.UnloadSceneAsync(scene);
            yield return null;
            yield return null;
            Assert.That(root == null, Is.True);
            Assert.That(NewHosts(), Is.Empty);
        }

        private DeucarianPointerCaptureController[] NewHosts() =>
            CaptureHosts().Where(host => !before.Contains(host)).ToArray();

        private static DeucarianPointerCaptureController[] CaptureHosts() =>
            Resources.FindObjectsOfTypeAll<DeucarianPointerCaptureController>()
                .Where(host => host.gameObject.scene.IsValid()).ToArray();
    }
}
