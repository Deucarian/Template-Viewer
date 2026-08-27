namespace Deucarian.TemplateViewer.Tests
{
    /// <summary>
    /// Runtime-compatible feature component used by viewer composition tests.
    /// </summary>
    public sealed class RecordingViewerFeature : ViewerFeatureBehaviour
    {
        public int AttachCount { get; private set; }
        public int DetachCount { get; private set; }
        public int CommandCompletedCount { get; private set; }
        public Deucarian.CommandRouting.CommandDispatchEventArgs
            LastCommandCompleted { get; private set; }
        public IViewerModelReadinessFeature ReadinessFeature { get; set; }

        public override IViewerModelReadinessFeature ModelReadinessFeature =>
            ReadinessFeature;

        public override void Attach(ViewerApplication application)
        {
            AttachCount++;
        }

        public override void Detach(ViewerApplication application)
        {
            DetachCount++;
        }

        public override void OnCommandCompleted(
            ViewerApplication application,
            Deucarian.CommandRouting.CommandDispatchEventArgs eventArgs)
        {
            CommandCompletedCount++;
            LastCommandCompleted = eventArgs;
        }
    }
}
