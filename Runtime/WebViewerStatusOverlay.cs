using Deucarian.Theming;
using Deucarian.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Deucarian.TemplateViewerWeb
{
    [DisallowMultipleComponent]
    public sealed class WebViewerStatusOverlay : MonoBehaviour
    {
        private static readonly Color FallbackSurfaceColor =
            new Color(0.035f, 0.055f, 0.09f, 0.92f);
        private static readonly Color FallbackTextColor =
            new Color(0.86f, 0.91f, 1f, 1f);
        private static readonly Color FallbackErrorColor =
            new Color(1f, 0.55f, 0.52f, 1f);

        private GameObject panel;
        private Image background;
        private Outline outline;
        private Text statusText;
        private WebViewerApplication application;
        private DeucarianThemeProvider themeProvider;
        private DeucarianTheme currentTheme;
        private bool currentFailed;

        public DeucarianTheme CurrentTheme => currentTheme;
        public Color EffectiveSurfaceColor { get; private set; } =
            FallbackSurfaceColor;
        public Color EffectiveTextColor { get; private set; } =
            FallbackTextColor;
        public Color EffectiveErrorColor { get; private set; } =
            FallbackErrorColor;
        public Color RenderedSurfaceColor => background != null
            ? background.color
            : EffectiveSurfaceColor;
        public Color RenderedStatusColor => statusText != null
            ? statusText.color
            : currentFailed ? EffectiveErrorColor : EffectiveTextColor;

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
                    SetStatus("Loading model…", false);
                    break;
                case WebViewerLifecycleState.Ready:
                    SetStatus(
                        "Ready • " + application.IndexedElementCount + " elements",
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
            SetStatus(label + " • " + Mathf.RoundToInt(normalized * 100f) + "%", false);
        }

        private void EnsureUi()
        {
            if (panel != null)
            {
                ApplyResolvedTheme();
                return;
            }

            GameObject canvasObject = new GameObject(
                "Web Viewer Status Canvas",
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440f, 900f);

            panel = new GameObject(
                "Status",
                typeof(RectTransform),
                typeof(Image),
                typeof(Outline));
            panel.transform.SetParent(canvasObject.transform, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(18f, 18f);
            rect.sizeDelta = new Vector2(300f, 42f);

            background = panel.GetComponent<Image>();
            outline = panel.GetComponent<Outline>();

            GameObject textObject = new GameObject(
                "Label",
                typeof(RectTransform),
                typeof(Text));
            textObject.transform.SetParent(panel.transform, false);
            statusText = textObject.GetComponent<Text>();
            statusText.font = GetBuiltinFont();
            statusText.fontSize = 14;
            statusText.alignment = TextAnchor.MiddleLeft;
            RectTransform textRect = statusText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(14f, 0f);
            textRect.offsetMax = new Vector2(-14f, 0f);
            ApplyResolvedTheme();
        }

        private void SetStatus(string value, bool failed)
        {
            currentFailed = failed;
            EnsureUi();
            statusText.text = value;
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
            bool appliedSurface = DeucarianUGUIGlassPanel.ApplyImage(
                background,
                currentTheme,
                EffectiveSurfaceColor,
                style);
            if (!appliedSurface && background != null)
            {
                background.sprite = null;
                background.type = Image.Type.Simple;
                background.color = EffectiveSurfaceColor;
            }

            bool appliedOutline = DeucarianUGUIGlassPanel.ApplyOutline(
                outline,
                EffectiveSurfaceColor,
                currentTheme,
                style);
            if (!appliedOutline && outline != null)
            {
                outline.effectColor = Color.clear;
                outline.effectDistance = Vector2.zero;
                outline.useGraphicAlpha = false;
            }

            ApplyStatusTextColor();
        }

        private void ApplyStatusTextColor()
        {
            if (statusText != null)
            {
                statusText.color = currentFailed
                    ? EffectiveErrorColor
                    : EffectiveTextColor;
            }
        }

        private Color ResolveThemeColor(string roleId, Color fallback)
        {
            return currentTheme != null &&
                   currentTheme.TryGetColorById(roleId, out Color color)
                ? color
                : fallback;
        }

        private static Font GetBuiltinFont()
        {
            try
            {
                return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            catch (UnityException)
            {
                return Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
        }
    }
}
