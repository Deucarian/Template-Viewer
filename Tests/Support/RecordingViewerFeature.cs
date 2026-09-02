using System;
using System.Collections.Generic;
using Deucarian.CommandRouting;
using Newtonsoft.Json.Linq;

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
        public int CommandFailureProjectedCount { get; private set; }
        public ViewerCommandFailureProjectionEventArgs
            LastCommandFailureProjection { get; private set; }
        public bool ThrowOnCommandFailureProjection { get; set; }
        public int AuthenticationOutcomeCount { get; private set; }
        public ViewerAuthenticationOutcomeEventArgs
            LastAuthenticationOutcome { get; private set; }
        public bool ThrowOnAuthenticationOutcome { get; set; }
        public bool MutateAuthenticationOutcomePayload { get; set; }
        public IViewerModelReadinessFeature ReadinessFeature { get; set; }
        public IReadOnlyList<ICommandHandler<ViewerApplication>>
            CommandHandlers { get; set; } =
                Array.Empty<ICommandHandler<ViewerApplication>>();
        public string FailureProjectionCommand { get; set; }
        public string FailureProjectionFieldToRemove { get; set; }
        public bool MutateCanonicalFailureFields { get; set; }
        public int FailureProjectionCustomizationCount { get; private set; }

        public override IViewerModelReadinessFeature ModelReadinessFeature =>
            ReadinessFeature;

        public override IReadOnlyList<ICommandHandler<ViewerApplication>>
            CreateCommandHandlers() => CommandHandlers;

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

        public override void OnCommandFailureProjected(
            ViewerApplication application,
            ViewerCommandFailureProjectionEventArgs eventArgs)
        {
            CommandFailureProjectedCount++;
            LastCommandFailureProjection = eventArgs;
            if (ThrowOnCommandFailureProjection)
            {
                throw new InvalidOperationException(
                    "Expected feature-observer failure.");
            }
        }

        public override void CustomizeCommandFailureProjection(
            ViewerApplication application,
            string command,
            JObject payload)
        {
            FailureProjectionCustomizationCount++;
            if (string.Equals(
                    command,
                    FailureProjectionCommand,
                    StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(
                    FailureProjectionFieldToRemove))
            {
                payload.Remove(FailureProjectionFieldToRemove);
            }

            if (MutateCanonicalFailureFields)
            {
                payload["command"] = "mutated";
                payload["error_code"] = "mutated";
                payload["message"] = "mutated";
            }
        }

        public override void OnAuthenticationOutcome(
            ViewerApplication application,
            ViewerAuthenticationOutcomeEventArgs eventArgs)
        {
            AuthenticationOutcomeCount++;
            LastAuthenticationOutcome = eventArgs;
            if (MutateAuthenticationOutcomePayload)
            {
                Newtonsoft.Json.Linq.JObject payload = eventArgs.Payload;
                payload["status"] = "mutated";
                payload["access_token"] = "observer-only-test-value";
            }

            if (ThrowOnAuthenticationOutcome)
            {
                throw new InvalidOperationException(
                    "Expected authentication-observer failure.");
            }
        }
    }
}
