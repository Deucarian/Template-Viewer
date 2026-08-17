using System;
using Deucarian.API.Configuration;
using Deucarian.API.Core;
using Deucarian.CommandRouting;
using Deucarian.CommandRouting.WebGLIntegration;
using Deucarian.Diagnostics;
using Deucarian.Logging;
using Deucarian.TemplateViewerWeb.Commands;
using Deucarian.TemplateViewerWeb.Diagnostics;
using Deucarian.TemplateViewerWeb.Loading;
using Deucarian.TemplateViewerWeb.Selection;
using Deucarian.ViewerNavigation;
using UnityEngine;

namespace Deucarian.TemplateViewerWeb
{
    [DisallowMultipleComponent]
    public sealed class WebViewerBootstrap : MonoBehaviour
    {
        private static readonly DLog Log = DLog.For("TemplateViewerWeb");

        [Header("Browser transport")]
        [SerializeField] private bool iframeMode;
        [SerializeField] private string parentOrigin = "http://localhost:8080";
        [SerializeField] private string transportId = "web-viewer";

        [Header("Viewer")]
        [SerializeField] private Camera viewerCamera;
        [SerializeField] private ViewerNavigationSettings navigationSettings;
        [SerializeField] private GameObject embeddedReferenceModel;
        [SerializeField] private Transform loadedModelParent;
        [SerializeField] private ApiClientConfig apiClientConfig;
        private Deucarian.ViewerNavigation.ViewerNavigationReferenceCompositionProfile
            _navigationComposition =
                Deucarian.ViewerNavigation.ViewerNavigationReferenceComposition.Resolve();

        private ObjectLoadingWebViewerModelLoader modelLoader;
        private WebViewerApplication application;
        private CommandRoutingRuntime<WebViewerApplication> commandRuntime;
        private CommandTransportBridge<WebViewerApplication> commandBridge;
        private DiagnosticProviderRegistration diagnosticRegistration;
        private WebViewerStatusOverlay statusOverlay;

        public bool IframeMode => iframeMode;
        public string ParentOrigin => parentOrigin;
        public WebViewerApplication Application => application;
        public ViewerNavigationSettings ResolvedNavigationSettings =>
            navigationSettings != null
                ? navigationSettings
                : _navigationComposition.Preset;

        private void Start()
        {
            statusOverlay = GetComponent<WebViewerStatusOverlay>();
            if (statusOverlay == null)
            {
                statusOverlay = gameObject.AddComponent<WebViewerStatusOverlay>();
            }

            try
            {
                Compose();
            }
            catch (Exception exception)
            {
                Log.Error(
                    "Web viewer composition failed with " +
                    exception.GetType().Name + ". Details were omitted.",
                    this);
                statusOverlay.ShowFatalConfigurationError();
            }
        }

        public bool TryValidateConfiguration(
            bool production,
            out string issue)
        {
            if (!iframeMode)
            {
                issue = string.Empty;
                return true;
            }

            if (!Uri.TryCreate(parentOrigin, UriKind.Absolute, out Uri origin) ||
                (origin.Scheme != Uri.UriSchemeHttp &&
                 origin.Scheme != Uri.UriSchemeHttps) ||
                origin.AbsolutePath != "/" ||
                !string.IsNullOrEmpty(origin.Query) ||
                !string.IsNullOrEmpty(origin.Fragment) ||
                !string.IsNullOrEmpty(origin.UserInfo))
            {
                issue = "Iframe mode requires an exact HTTP(S) parent origin.";
                return false;
            }

            if (production &&
                (origin.Scheme != Uri.UriSchemeHttps ||
                 origin.IsLoopback))
            {
                issue = "Production iframe mode requires an exact non-loopback HTTPS origin.";
                return false;
            }

            issue = string.Empty;
            return true;
        }

        private void OnDestroy()
        {
            commandBridge?.Dispose();
            commandBridge = null;
            application?.Dispose();
            application = null;
            commandRuntime?.Dispose();
            commandRuntime = null;
            diagnosticRegistration?.Dispose();
            diagnosticRegistration = null;
            if (modelLoader != null)
            {
                modelLoader.ProgressChanged -= OnModelLoadingProgress;
                modelLoader = null;
            }
        }

        private void Compose()
        {
            if (!TryValidateConfiguration(false, out string issue))
            {
                throw new InvalidOperationException(issue);
            }

            EnsureSceneDependencies();

            Deucarian.ViewerNavigation.ViewerNavigationReferenceCompositionProfile
                composition = _navigationComposition;
            if (navigationSettings == null)
            {
                composition = Deucarian.ViewerNavigation.ViewerNavigationReferenceComposition
                    .Resolve();
                _navigationComposition = composition;
            }

            ViewerNavigationInstaller navigation =
                navigationSettings == null
                    ? composition.Compose(transform, viewerCamera)
                    : ViewerNavigationInstaller.Create(
                        transform,
                        viewerCamera,
                        navigationSettings,
                        composition.InputBlocker,
                        composition.BoundsStrategy,
                        composition.AnimationPolicy);
            navigation.BeginReferenceLoad();

            IApiClient apiClient = ApiClientFactory.Create(apiClientConfig);
            modelLoader = new ObjectLoadingWebViewerModelLoader(
                this,
                apiClient,
                loadedModelParent);
            modelLoader.ProgressChanged += OnModelLoadingProgress;

            WebGlCommandTransportMode mode = iframeMode
                ? WebGlCommandTransportMode.ParentIframe
                : WebGlCommandTransportMode.DirectPage;
            string[] allowedOrigins = iframeMode
                ? new[] { parentOrigin }
                : Array.Empty<string>();
            var transportOptions = new WebGlCommandTransportOptions(
                transportId,
                mode,
                allowedOrigins,
                iframeMode ? parentOrigin : null);
            var transport = new WebGlCommandTransport(transportOptions);
            WebGlCommandTransportBehaviour behaviour =
                gameObject.AddComponent<WebGlCommandTransportBehaviour>();
            behaviour.Initialize(transport);

            application = new WebViewerApplication(
                new DirectWebViewerModelDescriptorResolver(),
                modelLoader,
                navigation,
                new WebGlWebViewerEventPublisher(transport),
                embeddedReferenceModel);
            commandRuntime = new CommandRoutingRuntime<WebViewerApplication>(
                application,
                WebViewerCommandHandlers.Create(),
                new CommandRoutingOptions(
                    historyCapacity: 64,
                    logSuccessfulCommands: false,
                    logFailedCommands: true));
            commandBridge = new CommandTransportBridge<WebViewerApplication>(
                commandRuntime,
                transport,
                shouldSendResponses: true,
                disposeTransport: true);
            diagnosticRegistration = DiagnosticProviderRegistry.Register(
                new WebViewerApplicationDiagnosticProvider(application));
            statusOverlay.Initialize(application);
            commandBridge.Start();
        }

        private void EnsureSceneDependencies()
        {
            if (viewerCamera == null)
            {
                GameObject cameraObject = new GameObject("Viewer Camera");
                cameraObject.transform.SetParent(transform, false);
                viewerCamera = cameraObject.AddComponent<Camera>();
                viewerCamera.tag = "MainCamera";
                viewerCamera.transform.position = new Vector3(0f, 3.2f, -8f);
                viewerCamera.transform.rotation = Quaternion.Euler(18f, 0f, 0f);
            }

            if (FindFirstObjectByType<Light>() == null)
            {
                GameObject lightObject = new GameObject("Viewer Light");
                lightObject.transform.SetParent(transform, false);
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.15f;
                light.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            }

            if (loadedModelParent == null)
            {
                GameObject parent = new GameObject("Loaded Model");
                parent.transform.SetParent(transform, false);
                loadedModelParent = parent.transform;
            }

            if (embeddedReferenceModel == null)
            {
                embeddedReferenceModel = CreateEmbeddedReferenceModel();
            }
        }

        private GameObject CreateEmbeddedReferenceModel()
        {
            GameObject root = new GameObject("Embedded Reference Model");
            root.transform.SetParent(transform, false);
            CreateElement(root.transform, "red", PrimitiveType.Cube, new Vector3(-2.2f, 0f, 0f));
            CreateElement(root.transform, "green", PrimitiveType.Sphere, Vector3.zero);
            CreateElement(root.transform, "blue", PrimitiveType.Capsule, new Vector3(2.2f, 0f, 0f));
            return root;
        }

        private static void CreateElement(
            Transform parent,
            string id,
            PrimitiveType primitiveType,
            Vector3 position)
        {
            GameObject element = GameObject.CreatePrimitive(primitiveType);
            element.name = "Element " + id;
            element.transform.SetParent(parent, false);
            element.transform.localPosition = position;
            element.AddComponent<WebViewerElement>().Initialize(id);
        }

        private void OnModelLoadingProgress(float normalized, string message)
        {
            application?.ReportLoadingProgress(normalized, message);
        }
    }
}
