using System.IO;
using System.Reflection;
using Deucarian.Theming;
using Deucarian.UI;
using Deucarian.UI.Editor;
using Deucarian.ViewerNavigation;
using Deucarian.ViewerNavigation.UI;
using Deucarian.ViewerRendering;
using Deucarian.ViewerShell;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Deucarian.TemplateViewerWeb.Tests
{
    public sealed class WebViewerReferenceNavigationPresetTests
    {
        [Test]
        public void TemplateResolvesTheSharedReferenceProfiles()
        {
            GameObject root = new GameObject("Template Reference Profiles");
            try
            {
                WebViewerBootstrap bootstrap =
                    root.AddComponent<WebViewerBootstrap>();
                ViewerNavigationReferenceCompositionProfile navigation =
                    bootstrap.ResolvedNavigationComposition;
                ViewerRenderingReferenceCompositionProfile rendering =
                    bootstrap.ResolvedRenderingComposition;
                DeucarianViewerReferenceThemeProfile theme =
                    DeucarianViewerReferenceThemePreset.Resolve();

                Assert.That(
                    navigation.Preset,
                    Is.SameAs(ViewerNavigationSettings.LoadReferencePreset()));
                Assert.That(
                    bootstrap.ResolvedNavigationSettings,
                    Is.SameAs(navigation.Preset));
                Assert.That(
                    navigation.InputBlocker,
                    Is.TypeOf<ViewerNavigationUiInputBlocker>());
                Assert.That(
                    navigation.BoundsStrategy,
                    Is.TypeOf<ViewerNavigationMeshBoundsStrategy>());
                Assert.That(
                    navigation.AnimationPolicy,
                    Is.TypeOf<ViewerNavigationAnimationPolicy>());
                Assert.That(
                    ((ViewerNavigationAnimationPolicy)navigation.AnimationPolicy)
                        .UsesSharedMotionPreference,
                    Is.True);
                Assert.That(navigation.ThemeProfile, Is.SameAs(theme));
                Assert.That(rendering.ThemeProfile, Is.SameAs(theme));
                Assert.That(
                    navigation.ThemeMode,
                    Is.EqualTo(rendering.ThemeMode));
                Assert.That(
                    bootstrap.ResolvedShellProfile,
                    Is.SameAs(ViewerShellReferencePreset.Profile));
                Assert.That(
                    bootstrap.ResolvedShellProfile.MenuHorizontalGap,
                    Is.EqualTo(
                        DeucarianViewerMenuClusterLayout
                            .ReferenceHorizontalGap));
                Assert.That(
                    rendering.Settings,
                    Is.SameAs(
                        ViewerRenderingSettings.LoadReferencePreset()));
                Assert.That(rendering.Settings.IsComplete, Is.True);
                Assert.That(
                    rendering.QualityProfile.DesktopDefaultTier,
                    Is.EqualTo(ViewerRenderingQualityTier.Full));
                Assert.That(
                    rendering.QualityProfile.WebGlDefaultTier,
                    Is.EqualTo(ViewerRenderingQualityTier.Full));
                Assert.That(
                    rendering.ResolveDefaultDisplaySettings(false),
                    Is.EqualTo(
                        rendering.ResolveDefaultDisplaySettings(true)));
                Assert.That(
                    rendering.ResolveDefaultDisplaySettings(false)
                        .EffectsActive,
                    Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ExplicitNavigationSettingsDoNotForkSharedPolicies()
        {
            GameObject root = new GameObject("Template Explicit Navigation");
            ViewerNavigationSettings settings =
                ScriptableObject.CreateInstance<ViewerNavigationSettings>();
            try
            {
                WebViewerBootstrap bootstrap =
                    root.AddComponent<WebViewerBootstrap>();
                typeof(WebViewerBootstrap)
                    .GetField(
                        "navigationSettings",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(bootstrap, settings);

                ViewerNavigationReferenceCompositionProfile composition =
                    bootstrap.ResolvedNavigationComposition;

                Assert.That(composition.Preset, Is.SameAs(settings));
                Assert.That(
                    composition.InputBlocker,
                    Is.TypeOf<ViewerNavigationUiInputBlocker>());
                Assert.That(
                    composition.BoundsStrategy,
                    Is.TypeOf<ViewerNavigationMeshBoundsStrategy>());
                Assert.That(
                    composition.AnimationPolicy,
                    Is.TypeOf<ViewerNavigationAnimationPolicy>());
                Assert.That(
                    composition.ThemeProfile,
                    Is.SameAs(DeucarianViewerReferenceThemePreset.Resolve()));
            }
            finally
            {
                Object.DestroyImmediate(settings);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InstalledTemplateUsesOneThemeAndCompleteSharedShell()
        {
            GameObject root = new GameObject("Template Shared Viewer Stack");
            try
            {
                WebViewerBootstrap bootstrap =
                    root.AddComponent<WebViewerBootstrap>();
                InstallReferenceStack(
                    bootstrap,
                    out ViewerRenderingInstaller rendering,
                    out ViewerNavigationInstaller navigation,
                    out ViewerShellPresenter shell);

                DeucarianThemeProvider provider = rendering.ThemeProvider;
                Assert.That(provider, Is.Not.Null);
                Assert.That(provider, Is.SameAs(bootstrap.ThemeProvider));
                Assert.That(provider, Is.SameAs(navigation.ThemeProvider));
                Assert.That(provider, Is.SameAs(shell.ThemeProvider));
                Assert.That(
                    provider,
                    Is.SameAs(root.GetComponent<DeucarianThemeProvider>()));
                Assert.That(
                    navigation.GetComponent<DeucarianThemeProvider>(),
                    Is.Null);
                Assert.That(
                    provider.CurrentThemeFamily,
                    Is.SameAs(
                        bootstrap.ResolvedNavigationComposition
                            .ThemeProfile.ThemeFamily));
                Assert.That(
                    provider.CurrentStyle.StyleId,
                    Is.EqualTo(DeucarianThemeStyleIds.FrostedGlass));

                Assert.That(rendering.Camera, Is.Not.Null);
                Assert.That(rendering.KeyLight, Is.Not.Null);
                Assert.That(rendering.Controller, Is.Not.Null);
                Assert.That(rendering.Environment, Is.Not.Null);
                Assert.That(
                    rendering.Controller.ActiveQualityTier,
                    Is.EqualTo(ViewerRenderingQualityTier.Full));
                Assert.That(
                    rendering.Controller.CurrentSettings.EffectsActive,
                    Is.True);
                Assert.That(
                    rendering.Composition.Settings,
                    Is.SameAs(bootstrap.ResolvedRenderingComposition.Settings));
                Assert.That(
                    rendering.Camera.transform.position,
                    Is.EqualTo(
                        bootstrap.ResolvedRenderingComposition.CameraProfile
                            .Position));
                Assert.That(
                    rendering.KeyLight.intensity,
                    Is.EqualTo(
                        bootstrap.ResolvedRenderingComposition.LightProfile
                            .Intensity));

                Assert.That(shell.Profile, Is.SameAs(
                    ViewerShellReferencePreset.Profile));
                Assert.That(shell.StatusDocument, Is.Not.Null);
                Assert.That(shell.StatusCard, Is.Not.Null);
                Assert.That(shell.DiagnosticsView, Is.Not.Null);
                Assert.That(shell.DisplaySettingsView, Is.Not.Null);
                Assert.That(shell.MenuCluster, Is.Not.Null);
                Assert.That(shell.DiagnosticsMenu, Is.Not.Null);
                Assert.That(shell.DisplaySettingsMenu, Is.Not.Null);
                Assert.That(
                    shell.MenuCluster.InformationMenu.RightInset -
                    shell.MenuCluster.SettingsMenu.RightInset -
                    DeucarianMorphingMenuMotion.CollapsedSize,
                    Is.EqualTo(shell.Profile.MenuHorizontalGap));
                Assert.That(
                    typeof(WebViewerBootstrap).Assembly.GetType(
                        "Deucarian.TemplateViewerWeb.WebViewerStatusOverlay"),
                    Is.Null,
                    "The template must not carry a local shell UI fork.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SharedDocumentsUseCanonicalDepthAndTooltipsStayOnTop()
        {
            GameObject root = new GameObject("Template Shared UI Depth");
            try
            {
                WebViewerBootstrap bootstrap =
                    root.AddComponent<WebViewerBootstrap>();
                InstallReferenceStack(
                    bootstrap,
                    out _,
                    out ViewerNavigationInstaller navigation,
                    out ViewerShellPresenter shell);

                UIDocument navigationDocument = navigation.Toolbar.Document;
                UIDocument statusDocument = shell.StatusDocument;
                UIDocument diagnosticsDocument =
                    shell.DiagnosticsMenu.Document;
                UIDocument settingsDocument =
                    shell.DisplaySettingsMenu.Document;
                UIDocument tooltipDocument =
                    shell.DiagnosticsMenu.RuntimeTooltip.OverlayDocument;

                Assert.That(
                    DeucarianUIRuntime.IsConfigured(
                        navigationDocument,
                        DeucarianUISurfaceRole.PrimaryControls),
                    Is.True);
                Assert.That(
                    DeucarianUIRuntime.IsConfigured(
                        statusDocument,
                        DeucarianUISurfaceRole.Status),
                    Is.True);
                Assert.That(
                    DeucarianUIRuntime.IsConfigured(
                        diagnosticsDocument,
                        DeucarianUISurfaceRole.Menu),
                    Is.True);
                Assert.That(
                    DeucarianUIRuntime.IsConfigured(
                        settingsDocument,
                        DeucarianUISurfaceRole.Menu),
                    Is.True);
                Assert.That(
                    DeucarianUIRuntime.IsConfigured(
                        tooltipDocument,
                        DeucarianUISurfaceRole.Tooltip),
                    Is.True);
                Assert.That(
                    statusDocument.sortingOrder,
                    Is.GreaterThan(navigationDocument.sortingOrder));
                Assert.That(
                    tooltipDocument.sortingOrder,
                    Is.GreaterThan(statusDocument.sortingOrder));
                Assert.That(
                    tooltipDocument.sortingOrder,
                    Is.GreaterThan(diagnosticsDocument.sortingOrder));
                Assert.That(
                    tooltipDocument.sortingOrder,
                    Is.GreaterThan(settingsDocument.sortingOrder));
                Assert.That(
                    shell.DiagnosticsMenu.RuntimeTooltip.OverlayDocument,
                    Is.SameAs(
                        shell.DisplaySettingsMenu.RuntimeTooltip
                            .OverlayDocument));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TemplateDelegatesScreenSpaceLayeringToUiPackage()
        {
            UnityEditor.PackageManager.PackageInfo package =
                UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                    typeof(WebViewerBootstrap).Assembly);
            Assert.That(package, Is.Not.Null);
            string runtimeRoot = Path.Combine(
                package.resolvedPath,
                "Runtime");

            var violations = DeucarianUILayeringArchitectureValidator
                .ValidateRuntimeRoot(runtimeRoot);

            Assert.That(
                violations,
                Is.Empty,
                "Web Viewer Template runtime must delegate screen-space " +
                "layering to com.deucarian.ui:\n" +
                string.Join("\n", violations));
        }

        private static void InstallReferenceStack(
            WebViewerBootstrap bootstrap,
            out ViewerRenderingInstaller rendering,
            out ViewerNavigationInstaller navigation,
            out ViewerShellPresenter shell)
        {
            MethodInfo installRendering = typeof(WebViewerBootstrap).GetMethod(
                "InstallRendering",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo installNavigation = typeof(WebViewerBootstrap).GetMethod(
                "InstallNavigation",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo installShell = typeof(WebViewerBootstrap).GetMethod(
                "InstallShell",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(installRendering, Is.Not.Null);
            Assert.That(installNavigation, Is.Not.Null);
            Assert.That(installShell, Is.Not.Null);

            rendering = (ViewerRenderingInstaller)installRendering.Invoke(
                bootstrap,
                null);
            navigation = (ViewerNavigationInstaller)installNavigation.Invoke(
                bootstrap,
                null);
            shell = (ViewerShellPresenter)installShell.Invoke(
                bootstrap,
                new object[] { rendering });
        }
    }
}
