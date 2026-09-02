using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.CameraNavigation;
using Deucarian.CommandRouting;
using Deucarian.TemplateViewer.Commands;
using Deucarian.ViewerNavigation;
using Deucarian.ViewerRendering;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Deucarian.TemplateViewer.Tests
{
    public sealed class ViewerPresentationCommandHandlerTests
    {
        private GameObject navigationRoot;
        private GameObject cameraObject;
        private DeucarianCameraNavigationControls controls;
        private ViewerNavigationController navigation;
        private RecordingRenderingController rendering;

        [SetUp]
        public void SetUp()
        {
            navigationRoot = new GameObject("Viewer command navigation");
            cameraObject = new GameObject("Viewer command camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(3f, 4f, -12f);
            controls = ScriptableObject.CreateInstance<
                DeucarianCameraNavigationControls>();
            navigation = navigationRoot.AddComponent<ViewerNavigationController>();
            navigation.Initialize(
                camera,
                controls,
                navigationMotionProfile: new ImmediateMotionProfile());
            navigation.SetReferenceBounds(
                new Bounds(Vector3.zero, Vector3.one * 8f),
                Vector3.zero);
            navigation.CaptureOrigin();
            rendering = new RecordingRenderingController();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(navigationRoot);
            UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(controls);
        }

        [Test]
        public async Task AdvertisedCommandsFailSafelyWithoutControllers()
        {
            IReadOnlyList<ICommandHandler<ViewerApplication>> handlers =
                ViewerCommandHandlers.CreateDefault();

            CommandResult navigationResult = await ExecuteAsync(
                Find(handlers, "home"),
                "home");
            Assert.That(navigationResult.Succeeded, Is.False);
            Assert.That(navigationResult.ErrorCode, Is.EqualTo("invalid_payload"));
            Assert.That(
                navigationResult.Message,
                Is.EqualTo("Navigation is not initialized."));

            CommandResult displayResult = await ExecuteAsync(
                Find(handlers, "set_display_settings"),
                "set_display_settings",
                new JObject { ["rendering_mode"] = "realistic" });
            Assert.That(displayResult.Succeeded, Is.False);
            Assert.That(
                displayResult.ErrorCode,
                Is.EqualTo("invalid_display_settings"));
            Assert.That(
                displayResult.Message,
                Is.EqualTo("Viewer rendering is not initialized."));
        }

        [Test]
        public async Task NavigationObjectPayloadMutatesTheSharedController()
        {
            IReadOnlyList<ICommandHandler<ViewerApplication>> handlers =
                CreateHandlers();
            var navigationPayload = new JObject
            {
                ["mode"] = "fly",
                ["view"] = "right",
                ["sensitivity"] = 0.5f,
                ["global_sensitivity"] = 1.25f
            };
            var raw = new JObject
            {
                ["type"] = "navigation",
                ["navigation"] = navigationPayload
            };

            CommandResult result = await ExecuteAsync(
                Find(handlers, "navigation"),
                "navigation",
                rawEnvelope: raw);

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(navigation.Mode, Is.EqualTo(ViewerNavigationMode.Fly));
            Assert.That(controls.GlobalSensitivity, Is.EqualTo(1.25f));
            Assert.That(navigation.ReferenceBounds.center, Is.EqualTo(Vector3.zero));
        }

        [TestCase("returntoorigin")]
        [TestCase("return_to_origin")]
        [TestCase("origin")]
        [TestCase("home")]
        [TestCase("resetcamera")]
        [TestCase("reset_camera")]
        [TestCase("topdown")]
        [TestCase("top_down")]
        [TestCase("topview")]
        [TestCase("top_view")]
        [TestCase("toggletopdown")]
        [TestCase("toggle_top_down")]
        [TestCase("toggletop")]
        [TestCase("toggle_top")]
        [TestCase("orbit")]
        [TestCase("fly")]
        public async Task EveryDirectNavigationAliasRemainsRegistered(
            string commandName)
        {
            IReadOnlyList<ICommandHandler<ViewerApplication>> handlers =
                CreateHandlers();

            CommandResult result = await ExecuteAsync(
                Find(handlers, commandName),
                commandName);

            Assert.That(result.Succeeded, Is.True, result.Message);
        }

        [TestCase("navigation")]
        [TestCase("navigate")]
        [TestCase("nav")]
        [TestCase("setnavigationsensitivity")]
        [TestCase("set_navigation_sensitivity")]
        [TestCase("navigationsensitivity")]
        [TestCase("navigation_sensitivity")]
        public async Task EveryPayloadNavigationAliasAcceptsScalarSensitivity(
            string commandName)
        {
            CommandResult result = await ExecuteAsync(
                Find(CreateHandlers(), commandName),
                commandName,
                rawPayload: new JValue(2.5f));

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(controls.GlobalSensitivity, Is.EqualTo(2.5f));
        }

        [Test]
        public async Task NavigationPayloadAcceptsTheEstablishedStringAction()
        {
            Assert.That(navigation.Mode, Is.EqualTo(ViewerNavigationMode.Orbit));

            CommandResult result = await ExecuteAsync(
                Find(CreateHandlers(), "navigation"),
                "navigation",
                rawPayload: new JValue("fly"));

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(navigation.Mode, Is.EqualTo(ViewerNavigationMode.Fly));
        }

        [Test]
        public async Task NullNestedNavigationFallsBackToPayload()
        {
            var payload = new JObject { ["mode"] = "fly" };
            var raw = new JObject
            {
                ["type"] = "navigation",
                ["navigation"] = JValue.CreateNull(),
                ["payload"] = payload
            };

            CommandResult result = await ExecuteAsync(
                Find(CreateHandlers(), "navigation"),
                "navigation",
                payload,
                rawEnvelope: raw);

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(navigation.Mode, Is.EqualTo(ViewerNavigationMode.Fly));
        }

        [TestCase("setnavigationmode")]
        [TestCase("set_navigation_mode")]
        [TestCase("navigationmode")]
        [TestCase("navigation_mode")]
        public async Task EveryModeAliasAcceptsStringAndActionObjectPayloads(
            string commandName)
        {
            CommandResult direct = await ExecuteAsync(
                Find(CreateHandlers(), commandName),
                commandName,
                rawPayload: new JValue("fly"));
            Assert.That(direct.Succeeded, Is.True, direct.Message);
            Assert.That(navigation.Mode, Is.EqualTo(ViewerNavigationMode.Fly));

            CommandResult objectResult = await ExecuteAsync(
                Find(CreateHandlers(), commandName),
                commandName,
                new JObject { ["action"] = "orbit" });
            Assert.That(objectResult.Succeeded, Is.True, objectResult.Message);
            Assert.That(navigation.Mode, Is.EqualTo(ViewerNavigationMode.Orbit));
        }

        [Test]
        public async Task NavigationInvalidPayloadsReturnStableFailures()
        {
            IReadOnlyList<ICommandHandler<ViewerApplication>> handlers =
                CreateHandlers();

            CommandResult empty = await ExecuteAsync(
                Find(handlers, "navigation"),
                "navigation");
            Assert.That(empty.Succeeded, Is.False);
            Assert.That(empty.ErrorCode, Is.EqualTo("invalid_payload"));
            Assert.That(
                empty.Message,
                Is.EqualTo(
                    "Navigation command did not include an action, mode, view, " +
                    "or sensitivity."));

            CommandResult malformed = await ExecuteAsync(
                Find(handlers, "navigation"),
                "navigation",
                new JObject { ["sensitivity"] = "fast" });
            Assert.That(malformed.Succeeded, Is.False);
            Assert.That(malformed.ErrorCode, Is.EqualTo("invalid_payload"));
            Assert.That(
                malformed.Message,
                Is.EqualTo("sensitivity must be a number."));

            CommandResult unsupported = await ExecuteAsync(
                Find(handlers, "navigation"),
                "navigation",
                new JObject { ["action"] = "spin" });
            Assert.That(unsupported.Succeeded, Is.False);
            Assert.That(
                unsupported.Message,
                Is.EqualTo("Unsupported navigation action: spin"));
        }

        [Test]
        public async Task NonFiniteNavigationSensitivityFailsBeforeMutation()
        {
            controls.GlobalSensitivity = 0.75f;
            ViewerNavigationMode originalMode = navigation.Mode;

            CommandResult scalar = await ExecuteAsync(
                Find(CreateHandlers(), "navigation"),
                "navigation",
                rawPayload: new JValue(float.NaN));
            Assert.That(scalar.Succeeded, Is.False);
            Assert.That(scalar.ErrorCode, Is.EqualTo("invalid_payload"));
            Assert.That(
                scalar.Message,
                Is.EqualTo(
                    "Navigation sensitivity must be a finite number."));

            CommandResult property = await ExecuteAsync(
                Find(CreateHandlers(), "navigation"),
                "navigation",
                new JObject
                {
                    ["global_sensitivity"] = float.PositiveInfinity,
                    ["mode"] = "fly"
                });
            Assert.That(property.Succeeded, Is.False);
            Assert.That(property.ErrorCode, Is.EqualTo("invalid_payload"));
            Assert.That(
                property.Message,
                Is.EqualTo(
                    "global_sensitivity must be a finite number."));
            Assert.That(controls.GlobalSensitivity, Is.EqualTo(0.75f));
            Assert.That(navigation.Mode, Is.EqualTo(originalMode));
        }

        [TestCase("setdisplaysettings")]
        [TestCase("set_display_settings")]
        public async Task DisplayAliasesApplyPackageOwnedStateExactlyOnce(
            string commandName)
        {
            CommandResult result = await ExecuteAsync(
                Find(CreateHandlers(), commandName),
                commandName,
                new JObject
                {
                    ["renderingMode"] = "realistic",
                    ["cameraRelativeLight"] = true
                });

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(rendering.ApplyCount, Is.EqualTo(1));
            Assert.That(
                rendering.CurrentSettings.RenderingMode,
                Is.EqualTo(ViewerRenderingMode.Realistic));
            Assert.That(rendering.CurrentSettings.CameraRelativeLight, Is.True);
            Assert.That(
                rendering.LastSource,
                Is.EqualTo(ViewerDisplaySettingsChangeSource.Host));
        }

        [TestCaseSource(nameof(InvalidDisplayPayloads))]
        public async Task DisplayInvalidPayloadsPreserveFailureContract(
            JToken payload,
            string expectedMessage)
        {
            CommandResult result = await ExecuteAsync(
                Find(CreateHandlers(), "set_display_settings"),
                "set_display_settings",
                payload as JObject,
                payload is JObject ? null : payload);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(
                result.ErrorCode,
                Is.EqualTo("invalid_display_settings"));
            Assert.That(result.Message, Is.EqualTo(expectedMessage));
            Assert.That(rendering.ApplyCount, Is.Zero);
        }

        private static IEnumerable<TestCaseData> InvalidDisplayPayloads()
        {
            yield return new TestCaseData(
                new JValue("realistic"),
                "set_display_settings requires an object payload.");
            yield return new TestCaseData(
                new JObject(),
                "set_display_settings requires rendering_mode or " +
                "camera_relative_light.");
            yield return new TestCaseData(
                new JObject { ["rendering_mode"] = "wireframe" },
                "rendering_mode must be color_faithful or realistic.");
            yield return new TestCaseData(
                new JObject { ["camera_relative_light"] = "yes" },
                "camera_relative_light must be a boolean.");
            yield return new TestCaseData(
                new JObject
                {
                    ["rendering_mode"] = "realistic",
                    ["renderingMode"] = "color_faithful"
                },
                "rendering_mode and renderingMode cannot disagree.");
        }

        private IReadOnlyList<ICommandHandler<ViewerApplication>>
            CreateHandlers() => ViewerCommandHandlers.CreateWithPresentation(
                navigationController: navigation,
                renderingController: rendering);

        private static ICommandHandler<ViewerApplication> Find(
            IEnumerable<ICommandHandler<ViewerApplication>> handlers,
            string commandName) =>
            handlers.Single(handler =>
                handler.CommandNames.Contains(commandName));

        private static Task<CommandResult> ExecuteAsync(
            ICommandHandler<ViewerApplication> handler,
            string commandName,
            JObject payload = null,
            JToken rawPayload = null,
            JObject rawEnvelope = null)
        {
            JObject raw = rawEnvelope ?? new JObject
            {
                ["type"] = commandName
            };
            if (rawPayload != null)
            {
                raw["payload"] = rawPayload.DeepClone();
            }
            else if (payload != null)
            {
                raw["payload"] = payload.DeepClone();
            }

            var envelope = new CommandEnvelope(
                commandName,
                payload,
                metadata: new CommandMetadata(
                    "test",
                    "test",
                    "parent:https://viewer.example"),
                rawEnvelope: raw);
            return handler.HandleAsync(
                new CommandExecutionContext<ViewerApplication>(
                    null,
                    envelope,
                    commandName),
                CancellationToken.None);
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
            public ViewerDisplaySettingsChangeSource LastSource { get; private set; }

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
                LastSource = source;
                SettingsChanged?.Invoke(CurrentSettings, source);
            }
        }

        private sealed class ImmediateMotionProfile :
            IViewerNavigationMotionProfile
        {
            public bool AnimateTransitions => false;
            public float TransitionMatchFieldOfView => 0.1f;
            public float CalculateTransitionDuration(float distance) => 0f;
            public float EvaluateMovement(float normalizedTime) =>
                Mathf.Clamp01(normalizedTime);
            public float EvaluateRotation(float normalizedTime) =>
                Mathf.Clamp01(normalizedTime);
        }
    }
}
