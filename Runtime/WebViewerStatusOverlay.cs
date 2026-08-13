using Deucarian.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Deucarian.TemplateViewerWeb
{
    [DisallowMultipleComponent]
    public sealed class WebViewerStatusOverlay : MonoBehaviour
    {
        private GameObject panel;
        private Text statusText;
        private WebViewerApplication application;

        public void Initialize(WebViewerApplication viewerApplication)
        {
            if (application != null)
            {
                application.LifecycleChanged -= OnLifecycleChanged;
                application.LoadingProgressChanged -= OnLoadingProgressChanged;
            }

            application = viewerApplication;
            EnsureUi();
            if (application != null)
            {
                application.LifecycleChanged += OnLifecycleChanged;
                application.LoadingProgressChanged += OnLoadingProgressChanged;
                OnLifecycleChanged(application.Lifecycle);
            }
        }

        public void ShowFatalConfigurationError()
        {
            EnsureUi();
            SetStatus("Viewer configuration failed", true);
        }

        private void OnDestroy()
        {
            if (application != null)
            {
                application.LifecycleChanged -= OnLifecycleChanged;
                application.LoadingProgressChanged -= OnLoadingProgressChanged;
                application = null;
            }
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

            Image background = panel.GetComponent<Image>();
            Color surface = new Color(0.035f, 0.055f, 0.09f, 0.92f);
            DeucarianUGUIGlassPanel.ApplyImage(background, null, surface);
            DeucarianUGUIGlassPanel.ApplyOutline(
                panel.GetComponent<Outline>(),
                surface,
                null);

            GameObject textObject = new GameObject(
                "Label",
                typeof(RectTransform),
                typeof(Text));
            textObject.transform.SetParent(panel.transform, false);
            statusText = textObject.GetComponent<Text>();
            statusText.font = GetBuiltinFont();
            statusText.fontSize = 14;
            statusText.alignment = TextAnchor.MiddleLeft;
            statusText.color = new Color(0.86f, 0.91f, 1f, 1f);
            RectTransform textRect = statusText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(14f, 0f);
            textRect.offsetMax = new Vector2(-14f, 0f);
        }

        private void SetStatus(string value, bool failed)
        {
            EnsureUi();
            statusText.text = value;
            statusText.color = failed
                ? new Color(1f, 0.55f, 0.52f, 1f)
                : new Color(0.86f, 0.91f, 1f, 1f);
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
