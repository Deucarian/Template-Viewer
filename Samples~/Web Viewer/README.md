# Web Viewer sample

Open `Scenes/WebViewer.unity` and enter Play Mode. The bootstrap creates a
camera, light, three generic elements (`red`, `green`, and `blue`), the Viewer
Navigation toolbar and view cube, status UI, Object Loading adapter, and secure
browser command host.

The embedded model is used when `initialize_viewer` omits `model_url`. Supplying
`model_url` exercises the same API-backed Object Loading path intended for
production extension. No backend DTO or version-resolution policy is assumed.

Use `Browser~/harness.html` from a local HTTP server to exercise initialization,
selection, clear, invalid IDs, stale revisions, and disposal against a WebGL
build. Never commit real tokens or production URLs to this sample.
