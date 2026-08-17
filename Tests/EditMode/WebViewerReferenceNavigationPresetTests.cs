using Deucarian.ViewerNavigation;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Deucarian.TemplateViewerWeb.Tests
{
    public sealed class WebViewerReferenceNavigationPresetTests
    {
        [Test]
        public void TemplateDefaultsToSharedReportViewerReferencePreset()
        {
            GameObject root = new GameObject("Template Reference Preset Test");
            try
            {
                WebViewerBootstrap bootstrap =
                    root.AddComponent<WebViewerBootstrap>();
                ViewerNavigationSettings preset =
                    ViewerNavigationSettings.LoadReferencePreset();

                Assert.That(preset, Is.Not.Null);
                Assert.That(
                    bootstrap.ResolvedNavigationSettings,
                    Is.SameAs(preset));
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
                    bootstrap.ResolvedNavigationSettings,
                    Is.SameAs(preset),
                    "Template defaults to the shared navigation preset.");

                FieldInfo compositionField = typeof(WebViewerBootstrap)
                    .GetField(
                        "_navigationComposition",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(compositionField, Is.Not.Null);
                var composition =
                    (ViewerNavigationReferenceCompositionProfile)compositionField
                        .GetValue(bootstrap);
                Assert.That(composition.Preset, Is.SameAs(preset));
                Assert.That(
                    composition.InputBlocker,
                    Is.TypeOf<ViewerNavigationUiInputBlocker>());
                Assert.That(
                    composition.BoundsStrategy,
                    Is.TypeOf<DeucarianMeshBoundsStrategy>());
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
