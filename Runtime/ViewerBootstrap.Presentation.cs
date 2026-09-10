using System;
using Deucarian.Theming;
using Deucarian.ViewerNavigation;
using Deucarian.ViewerNavigation.UI;
using Deucarian.ViewerRendering;
using Deucarian.ViewerShell;
using UnityEngine;

namespace Deucarian.TemplateViewer
{
    public abstract partial class ViewerBootstrap
    {
        /// <summary>
        /// Composes the default reference rendering. XR and other hosts may
        /// override this hook and return their own compatible presentation.
        /// </summary>
        protected virtual ViewerRenderingInstaller ComposeRendering()
        {
            renderingInstaller = ResolvedRenderingComposition.Compose(
                transform,
                viewerCamera,
                keyLight,
                referenceThemeProvider);
            viewerCamera = renderingInstaller.Camera;
            keyLight = renderingInstaller.KeyLight;
            referenceThemeProvider = renderingInstaller.ThemeProvider;
            referenceThemeRuntime =
                DeucarianViewerReferenceThemeComposition.Install(
                    gameObject,
                    referenceThemeProvider);
            referenceThemeProvider = referenceThemeRuntime.Provider;
            return renderingInstaller;
        }

        /// <summary>
        /// Creates the minimum reference lifecycle used by ViewerApplication.
        /// The default implementation uses orbit-camera navigation and frames
        /// the reference. XR may supply an origin-aware implementation.
        /// </summary>
        protected virtual IViewerReferenceNavigation
            ComposeReferenceNavigation(ViewerRenderingInstaller rendering)
        {
            if (rendering == null)
            {
                throw new InvalidOperationException(
                    "Default viewer navigation requires reference rendering.");
            }

            navigationInstaller = ResolvedNavigationComposition.Compose(
                transform,
                viewerCamera,
                rendering.ThemeProvider);
            return new ViewerNavigationReferenceAdapter(navigationInstaller);
        }

        /// <summary>
        /// Composes the default in-viewer shell. A host may return null when
        /// its platform owns all status presentation.
        /// </summary>
        protected virtual ViewerShellPresenter ComposeShell(
            ViewerRenderingInstaller rendering)
        {
            if (rendering == null)
            {
                return null;
            }

            ViewerShellConfiguration configuration =
                ViewerShellReferenceComposition.CreateConfiguration(
                    rendering.ThemeProvider,
                    () => ViewerNavigationMotionPreferences.ShouldAnimate,
                    root => ViewerNavigationMovementKeyGuard.Bind(root),
                    showDiagnostics: true);
            return ViewerShellReferenceComposition.Install(
                transform,
                rendering.Controller,
                configuration);
        }

        private ViewerNavigationReferenceCompositionProfile
            ResolveNavigationComposition()
        {
            if (hasResolvedNavigationComposition)
            {
                return navigationComposition;
            }

            ViewerNavigationReferenceCompositionProfile reference =
                ViewerNavigationReferenceComposition.Resolve();
            navigationComposition = navigationSettings == null
                ? reference
                : reference.WithPreset(navigationSettings);
            hasResolvedNavigationComposition = true;
            return navigationComposition;
        }

        private ViewerRenderingReferenceCompositionProfile
            ResolveRenderingComposition()
        {
            if (!hasResolvedRenderingComposition)
            {
                renderingComposition =
                    ViewerRenderingReferenceComposition.Resolve();
                hasResolvedRenderingComposition = true;
            }

            return renderingComposition;
        }

        private void EnsureSceneDependencies() =>
            ViewerReferenceSceneComposition.EnsureDependencies(
                transform,
                ref loadedModelParent,
                ref embeddedReferenceModel);

        private void OnModelLoadingProgress(float normalized, string message)
        {
            application?.ReportLoadingProgress(normalized, message);
        }
    }
}
