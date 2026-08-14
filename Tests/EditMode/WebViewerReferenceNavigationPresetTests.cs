using Deucarian.ViewerNavigation;
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
                    Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
