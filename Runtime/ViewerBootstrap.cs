using System;
using System.Collections;
using System.Collections.Generic;
using Deucarian.API.Configuration;
using Deucarian.CommandRouting;
using Deucarian.Diagnostics;
using Deucarian.Logging;
using Deucarian.Session.APIIntegration;
using Deucarian.TemplateViewer.Commands;
using Deucarian.TemplateViewer.Diagnostics;
using Deucarian.TemplateViewer.Loading;
using Deucarian.Theming;
using Deucarian.Authentication;
using Deucarian.ViewerNavigation;
using Deucarian.ViewerRendering;
using Deucarian.ViewerShell;
using UnityEngine;

namespace Deucarian.TemplateViewer
{
    /// <summary>
    /// Platform-neutral viewer composition root. A host package derives from
    /// this component and supplies exactly one platform adapter.
    /// </summary>
    [DisallowMultipleComponent]
    public abstract partial class ViewerBootstrap : MonoBehaviour
    {
        private static readonly DLog Log = DLog.For("TemplateViewer");

        [Header("Viewer")]
        [SerializeField] private Camera viewerCamera;
        [SerializeField] private Light keyLight;
        [SerializeField] private ViewerNavigationSettings navigationSettings;
        [SerializeField] private GameObject embeddedReferenceModel;
        [SerializeField] private Transform loadedModelParent;
        [SerializeField] private ApiClientConfig apiClientConfig;

        [Header("Features")]
        [Tooltip("Optional same-scene product features located away from " +
                 "this bootstrap. Features beside the bootstrap are always " +
                 "discovered automatically.")]
        [SerializeField] private ViewerFeatureBehaviour[]
            explicitFeatureBehaviours =
                Array.Empty<ViewerFeatureBehaviour>();

        [Header("Authentication")]
        [Tooltip("Optional credential-free token endpoint profile. Assign it explicitly when this standalone template owns token acquisition.")]
        [SerializeField] private SessionTokenEndpointProfile
            authenticationTokenEndpointProfile;
        [Tooltip("Additional exact HTTP(S) origins eligible for the live session provider. Unlisted absolute cross-origin URLs remain anonymous public downloads.")]
        [SerializeField] private List<string> authenticatedModelOrigins =
            new List<string>();

        private ViewerNavigationReferenceCompositionProfile
            navigationComposition;
        private bool hasResolvedNavigationComposition;
        private ViewerRenderingReferenceCompositionProfile
            renderingComposition;
        private bool hasResolvedRenderingComposition;
        private ObjectLoadingViewerModelLoader modelLoader;
        private ViewerApplication application;
        private CommandRoutingRuntime<ViewerApplication> commandRuntime;
        private CommandRoutePortBehaviour localCommandPort;
        private IDisposable commandTransportActivation;
        private DiagnosticProviderRegistration diagnosticRegistration;
        private IViewerReferenceNavigation referenceNavigation;
        private ViewerNavigationInstaller navigationInstaller;
        private ViewerRenderingInstaller renderingInstaller;
        private ViewerShellPresenter shellPresenter;
        private ViewerShellStatusAdapter shellStatusAdapter;
        private ViewerCommandFailureProjector commandFailureProjector;
        private ViewerDisplaySettingsEventPublisher displaySettingsEvents;
        private ViewerModelRevealReadinessFeature modelRevealReadiness;
        private DeucarianThemeProvider referenceThemeProvider;
        private DeucarianViewerReferenceThemeRuntime referenceThemeRuntime;
        private IAuthenticationSession authenticationSession;
        private IAuthenticationAcquisitionProvider
            authenticationAcquisitionProvider;
        private IDisposable authenticationTargetRegistration;
        private IDisposable runtimeConnection;
        private IViewerPlatformAdapter platformAdapter;
        private string compositionStage = "starting viewer composition";
        private ViewerFeatureBehaviour[] featureBehaviours =
            Array.Empty<ViewerFeatureBehaviour>();

        public ViewerApplication Application => application;
        public IViewerPlatformAdapter PlatformAdapter => platformAdapter;
        public ViewerNavigationReferenceCompositionProfile
            ResolvedNavigationComposition => ResolveNavigationComposition();
        public ViewerNavigationSettings ResolvedNavigationSettings =>
            ResolvedNavigationComposition.Preset;
        public ViewerRenderingReferenceCompositionProfile
            ResolvedRenderingComposition => ResolveRenderingComposition();
        public ViewerShellReferenceProfile ResolvedShellProfile =>
            ViewerShellReferencePreset.Profile;
        public IViewerReferenceNavigation ReferenceNavigation =>
            referenceNavigation;
        public ViewerNavigationInstaller NavigationInstaller =>
            navigationInstaller;
        public ViewerRenderingInstaller RenderingInstaller =>
            renderingInstaller;
        public ViewerShellPresenter ShellPresenter => shellPresenter;
        public IReadOnlyList<ViewerFeatureBehaviour>
            ResolvedFeatureBehaviours =>
                ViewerFeatureComposition.ResolveBehaviours(
                    this,
                    explicitFeatureBehaviours);
        public DeucarianThemeProvider ThemeProvider =>
            referenceThemeProvider ??
            renderingInstaller?.ThemeProvider ??
            navigationInstaller?.ThemeProvider ??
            shellPresenter?.ThemeProvider;
        public DeucarianViewerReferenceThemeRuntime ThemeRuntime =>
            referenceThemeRuntime;
        public DeucarianTheme CurrentTheme =>
            ThemeProvider?.CurrentTheme ??
            ResolvedNavigationComposition.ThemeProfile.ResolveTheme(
                ResolvedNavigationComposition.ThemeMode);
        public SessionTokenEndpointProfile
            ResolvedAuthenticationTokenEndpointProfile =>
                authenticationTokenEndpointProfile;
        public IAuthenticationAcquisitionProvider
            AuthenticationAcquisitionProvider =>
                authenticationAcquisitionProvider;
        public CommandRoutePortBehaviour LocalCommandPort => localCommandPort;
        internal System.Threading.Tasks.Task CommandFailureProjectionIdle =>
            commandFailureProjector?.WhenIdle ??
            System.Threading.Tasks.Task.CompletedTask;

        protected abstract IViewerPlatformAdapter CreatePlatformAdapter();

        /// <summary>
        /// Enables the reference model reveal for platform bootstraps that
        /// explicitly own that presentation contract. Custom and non-Web
        /// bootstraps retain their established readiness behavior by default.
        /// </summary>
        protected virtual bool EnableModelRevealReadiness => false;

        protected virtual bool TryValidatePlatformConfiguration(
            IViewerPlatformAdapter adapter,
            bool production,
            out string issue)
        {
            issue = string.Empty;
            return true;
        }

        protected virtual IEnumerator Start()
        {
            // Optional connection providers register as Play Mode starts.
            yield return null;

            try
            {
                Compose();
            }
            catch (Exception exception)
            {
                string diagnostic =
                    ViewerCompositionDiagnostic.Format(
                        compositionStage,
                        exception);
                Log.Error(
                    diagnostic,
                    this);
                shellPresenter?.ApplyStatus(
                    ViewerShellStatusSnapshot.Error(
                        "Viewer configuration failed",
                        diagnostic));
            }
        }

        protected virtual void OnDestroy()
        {
            ReleaseComposition();
        }

        protected virtual void OnDisable()
        {
            // The reveal feature's lifecycle relay is the single owner of
            // host interruption. Keep this virtual hook source-compatible for
            // existing derived bootstraps.
        }

        /// <summary>
        /// Composes the viewer once. Derived test and adapter bootstraps may
        /// expose this protected operation through their own API.
        /// </summary>
        protected void Compose()
        {
            if (application != null || platformAdapter != null)
            {
                throw new InvalidOperationException(
                    "The viewer is already composed.");
            }

            try
            {
                compositionStage = "creating the platform adapter";
                platformAdapter = CreatePlatformAdapter();
                compositionStage = "validating the platform adapter";
                ViewerPlatformAdapterValidation.Validate(platformAdapter);
                compositionStage = "validating the platform configuration";
                if (!TryValidatePlatformConfiguration(
                        platformAdapter,
                        false,
                        out string platformIssue))
                {
                    throw new InvalidOperationException(
                        string.IsNullOrWhiteSpace(platformIssue)
                            ? "The viewer platform configuration is invalid."
                            : platformIssue.Trim());
                }

                ComposeCore();
                compositionStage = "completed viewer composition";
            }
            catch
            {
                ReleaseComposition();
                throw;
            }
        }
    }
}
