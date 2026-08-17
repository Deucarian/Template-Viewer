using Deucarian.Theming;
using Deucarian.ViewerNavigation;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Deucarian.TemplateViewerWeb.Tests
{
    public sealed class WebViewerReferenceNavigationPresetTests
    {
        [Test]
        public void TemplateDefaultsToSharedReferenceNavigationComposition()
        {
            GameObject root = new GameObject("Template Reference Preset Test");
            try
            {
                WebViewerBootstrap bootstrap =
                    root.AddComponent<WebViewerBootstrap>();
                ViewerNavigationSettings preset =
                    ViewerNavigationSettings.LoadReferencePreset();
                ViewerNavigationReferenceCompositionProfile composition =
                    bootstrap.ResolvedNavigationComposition;
                ViewerNavigationReferenceCompositionProfile cachedComposition =
                    bootstrap.ResolvedNavigationComposition;

                Assert.That(preset, Is.Not.Null);
                Assert.That(composition.Preset, Is.SameAs(preset));
                Assert.That(
                    bootstrap.ResolvedNavigationSettings,
                    Is.SameAs(composition.Preset));
                Assert.That(
                    bootstrap.ResolvedNavigationSettings.Controls,
                    Is.SameAs(preset.Controls));
                Assert.That(
                    bootstrap.ResolvedNavigationSettings.FramingSettings,
                    Is.SameAs(preset.FramingSettings));
                Assert.That(
                    bootstrap.ResolvedNavigationSettings
                        .CalculateTransitionDuration(10f),
                    Is.EqualTo(0.5f));
                Assert.That(
                    bootstrap.ResolvedNavigationSettings.ShowToolbar,
                    Is.True);
                Assert.That(
                    bootstrap.ResolvedNavigationSettings.ShowViewCube,
                    Is.False);
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
                    ((ViewerNavigationAnimationPolicy)composition.AnimationPolicy)
                        .UsesSharedMotionPreference,
                    Is.True);
                Assert.That(composition.AnimationPolicy.ShouldAnimate, Is.False);
                DeucarianViewerReferenceThemeProfile themeProfile =
                    DeucarianViewerReferenceThemePreset.Resolve();
                Assert.That(composition.ThemeProfile, Is.SameAs(themeProfile));
                Assert.That(
                    composition.ThemeMode,
                    Is.EqualTo(DeucarianViewerReferenceThemePreset.DefaultMode));
                Assert.That(
                    composition.ThemeProfile.DefaultTheme,
                    Is.SameAs(themeProfile.DarkTheme));
                Assert.That(
                    composition.ThemeProfile.DarkTheme,
                    Is.SameAs(themeProfile.ThemeFamily.DarkTheme));
                Assert.That(
                    composition.ThemeProfile.VisualStyle,
                    Is.SameAs(themeProfile.DarkTheme.VisualStyle));
                Assert.That(
                    composition.ThemeProfile.VisualStyle.StyleId,
                    Is.EqualTo(DeucarianThemeStyleIds.FrostedGlass));
                Assert.That(
                    cachedComposition.Preset,
                    Is.SameAs(composition.Preset));
                Assert.That(
                    cachedComposition.InputBlocker,
                    Is.SameAs(composition.InputBlocker));
                Assert.That(
                    cachedComposition.BoundsStrategy,
                    Is.SameAs(composition.BoundsStrategy));
                Assert.That(
                    cachedComposition.AnimationPolicy,
                    Is.SameAs(composition.AnimationPolicy));
                Assert.That(
                    cachedComposition.ThemeProfile,
                    Is.SameAs(composition.ThemeProfile));
                Assert.That(
                    cachedComposition.ThemeMode,
                    Is.EqualTo(composition.ThemeMode));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ExplicitSettingsBecomeEffectivePresetWithoutForkingPolicies()
        {
            GameObject root = new GameObject("Template Explicit Preset Test");
            ViewerNavigationSettings settings =
                ScriptableObject.CreateInstance<ViewerNavigationSettings>();
            try
            {
                WebViewerBootstrap bootstrap =
                    root.AddComponent<WebViewerBootstrap>();
                FieldInfo settingsField = typeof(WebViewerBootstrap).GetField(
                    "navigationSettings",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(settingsField, Is.Not.Null);
                settingsField.SetValue(bootstrap, settings);

                ViewerNavigationReferenceCompositionProfile composition =
                    bootstrap.ResolvedNavigationComposition;

                Assert.That(composition.Preset, Is.SameAs(settings));
                Assert.That(
                    bootstrap.ResolvedNavigationSettings,
                    Is.SameAs(settings));
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
                    ((ViewerNavigationAnimationPolicy)composition.AnimationPolicy)
                        .UsesSharedMotionPreference,
                    Is.True);
                Assert.That(composition.AnimationPolicy.ShouldAnimate, Is.False);
                Assert.That(
                    composition.ThemeProfile,
                    Is.SameAs(DeucarianViewerReferenceThemePreset.Resolve()));
                Assert.That(
                    composition.ThemeMode,
                    Is.EqualTo(DeucarianViewerReferenceThemePreset.DefaultMode));
                Assert.That(
                    composition.ThemeProfile.VisualStyle.StyleId,
                    Is.EqualTo(DeucarianThemeStyleIds.FrostedGlass));
            }
            finally
            {
                Object.DestroyImmediate(settings);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BootstrapInstallsReferenceControllerProviderAndOverlayTheme()
        {
            GameObject root = new GameObject("Template Installed Preset Test");
            try
            {
                WebViewerBootstrap bootstrap =
                    root.AddComponent<WebViewerBootstrap>();
                MethodInfo installMethod = typeof(WebViewerBootstrap).GetMethod(
                    "InstallNavigation",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(installMethod, Is.Not.Null);

                ViewerNavigationInstaller installer =
                    (ViewerNavigationInstaller)installMethod.Invoke(
                        bootstrap,
                        null);
                ViewerNavigationReferenceCompositionProfile composition =
                    bootstrap.ResolvedNavigationComposition;

                Assert.That(installer, Is.Not.Null);
                Assert.That(
                    bootstrap.NavigationInstaller,
                    Is.SameAs(installer));
                Assert.That(installer.Controller, Is.Not.Null);
                Assert.That(
                    installer.Controller.Controls,
                    Is.SameAs(composition.Preset.Controls));
                Assert.That(
                    installer.Controller.FramingSettings,
                    Is.SameAs(composition.Preset.FramingSettings));
                Assert.That(
                    installer.Controller.ReferenceBoundsStrategy,
                    Is.SameAs(composition.BoundsStrategy));
                Assert.That(
                    installer.Controller.MotionProfile.AnimateTransitions,
                    Is.False);

                PropertyInfo inputBlockerProperty =
                    typeof(ViewerNavigationController).GetProperty(
                        "InputBlocker",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(inputBlockerProperty, Is.Not.Null);
                Assert.That(
                    inputBlockerProperty.GetValue(installer.Controller),
                    Is.SameAs(composition.InputBlocker));

                DeucarianThemeProvider provider = installer.ThemeProvider;
                DeucarianTheme expectedTheme =
                    composition.ThemeProfile.ResolveTheme(
                        composition.ThemeMode);
                Assert.That(provider, Is.Not.Null);
                Assert.That(
                    provider.CurrentThemeFamily,
                    Is.SameAs(composition.ThemeProfile.ThemeFamily));
                Assert.That(
                    provider.ThemeMode,
                    Is.EqualTo(composition.ThemeMode));
                Assert.That(
                    provider.CurrentTheme,
                    Is.SameAs(expectedTheme));
                Assert.That(
                    provider.CurrentStyle,
                    Is.SameAs(composition.ThemeProfile.VisualStyle));
                Assert.That(
                    bootstrap.CurrentTheme,
                    Is.SameAs(expectedTheme));

                WebViewerStatusOverlay overlay =
                    root.AddComponent<WebViewerStatusOverlay>();
                overlay.Initialize(null, provider);
                Assert.That(overlay.CurrentTheme, Is.SameAs(expectedTheme));
                Assert.That(
                    overlay.CurrentTheme,
                    Is.SameAs(bootstrap.CurrentTheme));
                Assert.That(
                    expectedTheme.TryGetColorById(
                        DeucarianBuiltinColorRoleIds.SurfaceRaised,
                        out Color surface),
                    Is.True);
                Assert.That(
                    expectedTheme.TryGetColorById(
                        DeucarianBuiltinColorRoleIds.TextPrimary,
                        out Color text),
                    Is.True);
                Assert.That(
                    expectedTheme.TryGetColorById(
                        DeucarianBuiltinColorRoleIds.Error,
                        out Color error),
                    Is.True);
                Assert.That(overlay.EffectiveSurfaceColor, Is.EqualTo(surface));
                Assert.That(overlay.EffectiveTextColor, Is.EqualTo(text));
                Assert.That(overlay.EffectiveErrorColor, Is.EqualTo(error));
                Assert.That(
                    overlay.RenderedSurfaceColor,
                    Is.EqualTo(
                        composition.ThemeProfile.VisualStyle
                            .ResolveSurfaceColor(surface)));
                Assert.That(overlay.RenderedStatusColor, Is.EqualTo(text));
                Assert.That(
                    overlay.CurrentTheme.VisualStyle.StyleId,
                    Is.EqualTo(DeucarianThemeStyleIds.FrostedGlass));

                overlay.ShowFatalConfigurationError();
                Assert.That(overlay.RenderedStatusColor, Is.EqualTo(error));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
