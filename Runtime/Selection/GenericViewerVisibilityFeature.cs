using System;

namespace Deucarian.TemplateViewer.Selection
{
    internal sealed class GenericViewerVisibilityFeature :
        IViewerVisibilityFeature
    {
        private readonly ViewerVisibilityController visibility;
        private bool disposed;

        private GenericViewerVisibilityFeature(
            ViewerElementIndex index,
            long initialRevision)
        {
            visibility = new ViewerVisibilityController(index);
            Selection = new ViewerSelectionStateOwner(
                initialRevision,
                visibility);
            IndexedElementCount = index.Count;
        }

        public int IndexedElementCount { get; }
        public int SelectedElementCount => Selection.SelectedIds.Count;
        public ViewerSelectionStateOwner Selection { get; }

        public static bool TryCreate(
            ViewerModelContext context,
            out GenericViewerVisibilityFeature feature,
            out string error)
        {
            feature = null;
            if (!ViewerElementIndex.TryCreate(
                    context.ReferenceRoot,
                    out ViewerElementIndex index,
                    out error))
            {
                return false;
            }

            feature = new GenericViewerVisibilityFeature(
                index,
                context.InitialRevision);
            return true;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            visibility.RestoreBaseline();
            disposed = true;
        }
    }
}
