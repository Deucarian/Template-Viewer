namespace Deucarian.TemplateViewer.Tests
{
    /// <summary>
    /// Runtime-compatible feature component used by viewer composition tests.
    /// </summary>
    public sealed class RecordingViewerFeature : ViewerFeatureBehaviour
    {
        public int AttachCount { get; private set; }
        public int DetachCount { get; private set; }

        public override void Attach(ViewerApplication application)
        {
            AttachCount++;
        }

        public override void Detach(ViewerApplication application)
        {
            DetachCount++;
        }
    }
}
