using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.CommandRouting;
using Deucarian.ViewerRendering;
using Deucarian.ViewerShell;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Deucarian.TemplateViewer.Tests
{
    internal sealed class FakeViewerPlatformAdapter :
        IViewerPlatformAdapter,
        IViewerLifecycleStatusSink,
        IViewerEventPublisher
    {
        private readonly List<string> events = new List<string>();
        private readonly List<ViewerLifecycleState> lifecycles =
            new List<ViewerLifecycleState>();
        private readonly List<string> cleanupOrder = new List<string>();
        private readonly List<FakeViewerPublishedEvent> publishedEvents =
            new List<FakeViewerPublishedEvent>();
        private bool transportActive;

        public string PlatformId { get; set; } = "test";
        public string EventEndpoint { get; set; } = "test://viewer";
        public IViewerEventPublisher EventPublisher => this;
        public IViewerLifecycleStatusSink LifecycleStatusSink => this;
        public IReadOnlyList<string> Events => events;
        public IReadOnlyList<ViewerLifecycleState> Lifecycles => lifecycles;
        public IReadOnlyList<string> CleanupOrder => cleanupOrder;
        public IReadOnlyList<FakeViewerPublishedEvent> PublishedEvents =>
            publishedEvents;
        public int ActivationCount { get; private set; }
        public int ActivationDisposeCount { get; private set; }
        public int DisposeCount { get; private set; }
        public int ProgressCount { get; private set; }
        public bool ReturnNullActivation { get; set; }
        public bool RequireActiveTransportForEvents { get; set; }
        public bool RequireExactEventEndpoint { get; set; }
        public int RejectedEventCount { get; private set; }

        public IDisposable ActivateCommandTransport(
            CommandRoutingRuntime<ViewerApplication> commandRuntime)
        {
            if (commandRuntime == null)
            {
                throw new ArgumentNullException(nameof(commandRuntime));
            }

            ActivationCount++;
            if (ReturnNullActivation)
            {
                return null;
            }

            transportActive = true;
            return new CallbackLease(
                () =>
                {
                    transportActive = false;
                    ActivationDisposeCount++;
                    cleanupOrder.Add("activation");
                });
        }

        public Task PublishAsync(
            string eventName,
            JObject payload,
            string remoteEndpoint,
            CancellationToken cancellationToken = default)
        {
            if ((RequireActiveTransportForEvents && !transportActive) ||
                (RequireExactEventEndpoint &&
                 !string.Equals(
                     remoteEndpoint,
                     EventEndpoint,
                     StringComparison.Ordinal)))
            {
                RejectedEventCount++;
                throw new InvalidOperationException(
                    "The fake event route is not active or does not match.");
            }

            events.Add(eventName + "@" + remoteEndpoint);
            publishedEvents.Add(new FakeViewerPublishedEvent(
                eventName,
                payload,
                remoteEndpoint));
            return Task.CompletedTask;
        }

        public void ReportLifecycle(
            ViewerLifecycleState lifecycle,
            string message) =>
            lifecycles.Add(lifecycle);

        public void ReportLoadingProgress(
            string operationId,
            float normalized,
            string message) =>
            ProgressCount++;

        public void Dispose()
        {
            DisposeCount++;
            cleanupOrder.Add("adapter");
        }

        private sealed class CallbackLease : IDisposable
        {
            private readonly Action callback;
            private bool disposed;

            public CallbackLease(Action release)
            {
                callback = release;
            }

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                callback();
            }
        }
    }

    public sealed class FakeViewerPublishedEvent
    {
        public FakeViewerPublishedEvent(
            string eventName,
            JObject payload,
            string remoteEndpoint)
        {
            EventName = eventName ?? string.Empty;
            Payload = payload == null
                ? new JObject()
                : (JObject)payload.DeepClone();
            RemoteEndpoint = remoteEndpoint;
        }

        public string EventName { get; }
        public JObject Payload { get; }
        public string RemoteEndpoint { get; }
    }

    /// <summary>
    /// Runtime-compatible bootstrap component used by viewer composition tests.
    /// </summary>
    public class FakeViewerBootstrap : ViewerBootstrap
    {
        public IViewerPlatformAdapter Adapter { get; set; }
        public IViewerReferenceNavigation TestReferenceNavigation { get; set; } =
            new FakeViewerReferenceNavigation();
        public int FactoryCallCount { get; private set; }
        public bool PlatformConfigurationIsValid { get; set; } = true;
        public bool UseReferencePresentation { get; set; }

        public void ComposeNow() => Compose();

        public void ReleaseNow() => base.OnDestroy();

        protected override void OnDestroy()
        {
            base.OnDestroy();
        }

        protected override IViewerPlatformAdapter CreatePlatformAdapter()
        {
            FactoryCallCount++;
            return Adapter;
        }

        protected override bool TryValidatePlatformConfiguration(
            IViewerPlatformAdapter adapter,
            bool production,
            out string issue)
        {
            issue = PlatformConfigurationIsValid
                ? string.Empty
                : "The fake platform is invalid.";
            return PlatformConfigurationIsValid;
        }

        protected override ViewerRenderingInstaller ComposeRendering() =>
            UseReferencePresentation ? base.ComposeRendering() : null;

        protected override IViewerReferenceNavigation ComposeReferenceNavigation(
            ViewerRenderingInstaller rendering) =>
            UseReferencePresentation
                ? base.ComposeReferenceNavigation(rendering)
                : TestReferenceNavigation;

        protected override ViewerShellPresenter ComposeShell(
            ViewerRenderingInstaller rendering) =>
            UseReferencePresentation ? base.ComposeShell(rendering) : null;
    }

    public sealed class RevealEnabledFakeViewerBootstrap : FakeViewerBootstrap
    {
        protected override bool EnableModelRevealReadiness => true;
    }

    /// <summary>
    /// Concrete host used when tests need the production reference
    /// rendering, navigation, shell, or authentication composition.
    /// </summary>
    public sealed class ReferenceViewerBootstrap : ViewerBootstrap
    {
        protected override IViewerPlatformAdapter CreatePlatformAdapter() =>
            new FakeViewerPlatformAdapter();
    }

    internal sealed class FakeViewerReferenceNavigation :
        IViewerReferenceNavigation
    {
        public int BeginCount { get; private set; }
        public int RegisterCount { get; private set; }

        public void BeginReferenceLoad() => BeginCount++;

        public bool RegisterReference(
            GameObject referenceRoot,
            bool frame,
            bool captureOrigin)
        {
            RegisterCount++;
            return referenceRoot != null;
        }
    }
}
