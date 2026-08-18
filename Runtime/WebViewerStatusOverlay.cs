using Deucarian.Theming;
using Deucarian.Theming.UIToolkit;
using Deucarian.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Deucarian.TemplateViewerWeb
{
    [DisallowMultipleComponent]
    public sealed class WebViewerStatusOverlay : MonoBehaviour
    {
        public const string StatusPanelName = "WebViewerStatusPanel";
        public const string StatusLabelName = "WebViewerStatusLabel";

        private static readonly Color FallbackSurfaceColor =
            new Color(0.035f, 0.055f, 0.09f, 0.92f);
        private static readonly Color FallbackTextColor =
            new Color(0.86f, 0.91f, 1f, 1f);
        private static readonly Color FallbackErrorColor =
            new Color(1f, 0.55f, 0.52f, 1f);

        private UIDocument statusDocument;
        private VisualElement panel;
        private Label statusLabel;
        private WebViewerApplication application;
        private DeucarianThemeProvider themeProvider;
        private DeucarianTheme currentTheme;
        private bool currentFailed;

        public DeucarianTheme CurrentTheme => currentTheme;
        public UIDocument StatusDocument => statusDocument;
        public VisualElement StatusPanel => panel;
        public Label StatusLabel => statusLabel;
        public Color EffectiveSurfaceColor { get; private set; } =
            FallbackSurfaceColor;
        public Color EffectiveTextColor { get; private set; } =
            FallbackTextColor;
        public Color EffectiveErrorColor { get; private set; } =
            FallbackErrorColor;
        public Color RenderedSurfaceColor { get; private set; } =
            FallbackSurfaceColor;
        public Color RenderedStatusColor { get; private set; } =
            FallbackTextColor;

        public void Initialize(WebViewerApplication viewerApplication)
        {
            Initialize(viewerApplication, null);
        }

        public void Initialize(
            WebViewerApplication viewerApplication,
            DeucarianThemeProvider provider)
        {
            UnbindApplication();

            application = viewerApplication;
            BindThemeProvider(provider);
            EnsureUi();
            if (application != null)
            {
                application.LifecycleChanged += OnLifecycleChanged;
                application.LoadingProgressChanged += OnLoadingProgressChanged;
                OnLifecycleChanged(application.Lifecycle);
            }
        }

        public void ApplyTheme(DeucarianTheme theme)
        {
            currentTheme = theme;
            EnsureUi();
        }

        public void ShowFatalConfigurationError()
        {
            EnsureUi();
            SetStatus("Viewer configuration failed", true);
        }

        private void OnDestroy()
        {
            UnbindApplication();
            UnbindThemeProvider();
            statusDocument?.rootVisualElement.Clear();
            statusDocument = null;
            panel = null;
            statusLabel = null;
        }

        private void UnbindApplication()
        {
            if (application == null)
            {
                return;
            }

            application.LifecycleChanged -= OnLifecycleChanged;
            application.LoadingProgressChanged -= OnLoadingProgressChanged;
            application = null;
        }

        private void BindThemeProvider(DeucarianThemeProvider provider)
        {
            UnbindThemeProvider();
            themeProvider = provider;
            if (themeProvider == null)
            {
                return;
            }

            themeProvider.ThemeChanged += OnThemeChanged;
            themeProvider.StyleChanged += OnThemeStyleChanged;
            currentTheme = themeProvider.CurrentTheme;
        }

        private void UnbindThemeProvider()
        {
            if (themeProvider == null)
            {
                return;
            }

            themeProvider.ThemeChanged -= OnThemeChanged;
            themeProvider.StyleChanged -= OnThemeStyleChanged;
            themeProvider = null;
        }

        private void OnThemeChanged(DeucarianTheme theme)
        {
            ApplyTheme(theme);
        }

        private void OnThemeStyleChanged(DeucarianThemeStyle style)
        {
            if (themeProvider != null)
            {
                currentTheme = themeProvider.CurrentTheme;
            }

            EnsureUi();
        }

        private void OnLifecycleChanged(WebViewerLifecycleState lifecycle)
        {
            switch (lifecycle)
            {
                case WebViewerLifecycleState.Created:
                    SetStatus("Waiting for browser host", false);
                    break;
                case WebViewerLifecycleState.Loading:
                    SetStatus("Loading model\u2026", false);
                    break;
                case WebViewerLifecycleState.Ready:
                    SetStatus(
                        "Ready \u2022 " + application.IndexedElementCount +
                        " elements",
                        false);
                    break;
                case WebViewerLifecycleState.Failed:
                    SetStatus("Viewer initialization failed", true);
                    break;
                case WebViewerLifecycleState.Disposed:
                    SetStatus("Viewer disposed", false);
                    break;
            }
        }

        private void OnLoadingProgressChanged(float normalized, string message)
        {
            string label = string.IsNullOrWhiteSpace(message)
                ? "Loading model"
                : message.Trim();
            SetStatus(
                label + " \u2022 " + Mathf.RoundToInt(normalized * 100f) + "%",
                false);
        }

        private void EnsureUi()
        {
            if (panel != null)
            {
                ApplyResolvedTheme();
                return;
            }

            statusDocument = GetComponent<UIDocument>();
            if (statusDocument == null)
            {
                statusDocument = gameObject.AddComponent<UIDocument>();
            }

            DeucarianUIRuntime.Configure(
                statusDocument,
                DeucarianUISurfaceRole.Status);

            VisualElement root = statusDocument.rootVisualElement;
            root.Clear();
            root.pickingMode = PickingMode.Ignore;
            root.style.position = Position.Absolute;
            root.style.left = 0f;
            root.style.right = 0f;
            root.style.top = 0f;
            root.style.bottom = 0f;
            root.style.backgroundColor = StyleKeyword.Null;

            panel = new VisualElement
            {
                name = StatusPanelName,
                pickingMode = PickingMode.Ignore
            };
            panel.style.position = Position.Absolute;
            panel.style.left = 18f;
            panel.style.bottom = 18f;
            panel.style.width = 300f;
            panel.style.height = 42f;
            panel.style.paddingLeft = 14f;
            panel.style.paddingRight = 14f;
            panel.style.flexDirection = FlexDirection.Row;
            panel.style.alignItems = Align.Center;
            panel.style.justifyContent = Justify.FlexStart;

            statusLabel = new Label(string.Empty)
            {
                name = StatusLabelName,
                pickingMode = PickingMode.Ignore
            };
            statusLabel.style.flexGrow = 1f;
            statusLabel.style.height = Length.Percent(100f);
            statusLabel.style.fontSize = 14f;
            statusLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            statusLabel.style.whiteSpace = WhiteSpace.NoWrap;
            statusLabel.style.marginLeft = 0f;
            statusLabel.style.marginRight = 0f;
            statusLabel.style.marginTop = 0f;
            statusLabel.style.marginBottom = 0f;

            panel.Add(statusLabel);
            root.Add(panel);
            ApplyResolvedTheme();
        }

        private void SetStatus(string value, bool failed)
        {
            currentFailed = failed;
            EnsureUi();
            statusLabel.text = value;
            ApplyStatusTextColor();
        }

        private void ApplyResolvedTheme()
        {
            EffectiveSurfaceColor = ResolveThemeColor(
                DeucarianBuiltinColorRoleIds.SurfaceRaised,
                FallbackSurfaceColor);
            EffectiveTextColor = ResolveThemeColor(
                DeucarianBuiltinColorRoleIds.TextPrimary,
                FallbackTextColor);
            EffectiveErrorColor = ResolveThemeColor(
                DeucarianBuiltinColorRoleIds.Error,
                FallbackErrorColor);

            DeucarianThemeStyle style = themeProvider != null
                ? themeProvider.CurrentStyle
                : currentTheme != null ? currentTheme.VisualStyle : null;
            DeucarianUIToolkitThemeTypography.Apply(
                panel,
                currentTheme,
                this);
            DeucarianGlassPanelStyle.ApplyPanel(
                panel,
                currentTheme,
                style,
                this);

            RenderedSurfaceColor = style != null
                ? style.ResolveSurfaceColor(EffectiveSurfaceColor)
                : EffectiveSurfaceColor;
            if (panel != null)
            {
                panel.style.backgroundColor = RenderedSurfaceColor;
            }

            ApplyStatusTextColor();
        }

        private void ApplyStatusTextColor()
        {
            RenderedStatusColor = currentFailed
                ? EffectiveErrorColor
                : EffectiveTextColor;
            if (statusLabel != null)
            {
                statusLabel.style.color = RenderedStatusColor;
            }
        }

        private Color ResolveThemeColor(string roleId, Color fallback)
        {
            return currentTheme != null &&
                   currentTheme.TryGetColorById(roleId, out Color color)
                ? color
                : fallback;
        }
    }
}
