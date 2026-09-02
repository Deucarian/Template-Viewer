using System.Threading;

namespace Deucarian.TemplateViewer
{
    public sealed partial class ViewerApplication
    {
        private bool IsInitializationCurrent(
            int generation,
            CancellationToken cancellationToken)
        {
            if (generation != Volatile.Read(ref initializationGeneration))
            {
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();
            return true;
        }

        private bool IsInitializationSuperseded(
            int generation,
            CancellationToken callerCancellationToken) =>
            generation != Volatile.Read(ref initializationGeneration) &&
            !callerCancellationToken.IsCancellationRequested;

        private bool TryAdvanceRevision(long revision)
        {
            lock (initializationStateGate)
            {
                long current = Interlocked.Read(ref latestRevision);
                if (revision <= current)
                {
                    return false;
                }

                Interlocked.Exchange(ref latestRevision, revision);
                return true;
            }
        }

        private bool TryBeginInitialization(
            long revision,
            out int generation)
        {
            lock (initializationStateGate)
            {
                long current = Interlocked.Read(ref latestRevision);
                if (revision <= current)
                {
                    generation = initializationGeneration;
                    return false;
                }

                Interlocked.Exchange(ref latestRevision, revision);
                generation = ++initializationGeneration;
                return true;
            }
        }

        public bool TryRecordRevision(long revision) =>
            TryAdvanceRevision(revision);
    }
}
