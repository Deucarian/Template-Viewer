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

        public string PlatformId { get; set; } = "test";
        public string EventEndpoint { get; set; } = "test://viewer";
        public IViewerEventPublisher EventPublisher => this;
        public IViewerLifecycleStatusSink LifecycleStatusSink => this;
        public IReadOnlyList<string> Events => events;
        public IReadOnlyList<ViewerLifecycleState> Lifecycles => lifecycles;
        public IReadOnlyList<string> CleanupOrder => cleanupOrder;
        public int ActivationCount { get; private set; }
        public int ActivationDisposeCount { get; private set; }
        public int DisposeCount { get; private set; }
        public int ProgressCount { get; private set; }
        public bool ReturnNullActivation { get; set; }

        public IDisposable ActivateCommandTransport(
            CommandRoutingRuntime<ViewerApplication> commandRuntime)
        {
            if (commandRuntime == null)
            {
                throw new ArgumentNullException(nameof(commandRuntime));
            }

            ActivationCount++;
            return ReturnNullActivation
                ? null
                : new CallbackLease(
                    () =>
                    {
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
            events.Add(eventName + "@" + remoteEndpoint);
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

    public sealed class FakeViewerBootstrap : ViewerBootstrap
    {
        public IViewerPlatformAdapter Adapter { get; set; }
        public IViewerReferenceNavigation TestReferenceNavigation { get; set; } =
            new FakeViewerReferenceNavigation();
        public int FactoryCallCount { get; private set; }
        public bool PlatformConfigurationIsValid { get; set; } = true;

        public void ComposeNow() => Compose();

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

        protected override ViewerRenderingInstaller ComposeRendering() => null;

        protected override IViewerReferenceNavigation ComposeReferenceNavigation(
            ViewerRenderingInstaller rendering) =>
            TestReferenceNavigation;

        protected override ViewerShellPresenter ComposeShell(
            ViewerRenderingInstaller rendering) =>
            null;
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
