using System;
using Deucarian.ViewerNavigation;
using UnityEngine;

namespace Deucarian.TemplateViewer
{
    /// <summary>
    /// Minimal model-reference lifecycle required by the viewer application.
    /// Platform compositions may frame with an orbit camera, register an XR
    /// origin, or provide another readiness implementation.
    /// </summary>
    public interface IViewerReferenceNavigation
    {
        void BeginReferenceLoad();

        bool RegisterReference(
            GameObject referenceRoot,
            bool frame,
            bool captureOrigin);
    }

    public sealed class ViewerNavigationReferenceAdapter :
        IViewerReferenceNavigation
    {
        private readonly ViewerNavigationInstaller installer;

        public ViewerNavigationReferenceAdapter(
            ViewerNavigationInstaller navigationInstaller)
        {
            installer = navigationInstaller ??
                throw new ArgumentNullException(nameof(navigationInstaller));
        }

        public void BeginReferenceLoad() => installer.BeginReferenceLoad();

        public bool RegisterReference(
            GameObject referenceRoot,
            bool frame,
            bool captureOrigin) =>
            installer.RegisterReference(referenceRoot, frame, captureOrigin);
    }
}
