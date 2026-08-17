# Web Viewer sample

Open `Scenes/WebViewer.unity` and enter Play Mode. The bootstrap creates a
camera, light, three generic elements (`red`, `green`, and `blue`), the Viewer
Navigation package's canonical toolbar, optional view cube (off by default),
status UI, Object Loading adapter, and secure browser command host. The toolbar
also owns its UI-input and movement-key suppression, and resolves its visual
style from the shared Theming package; the sample contains no toolbar or theme
fork.

The embedded model is used when `initialize_viewer` omits `model_url`. Supplying
`model_url` exercises the same API-backed Object Loading path intended for
production extension. No backend DTO or version-resolution policy is assumed.

Use `Browser~/harness.html` from a local HTTP server to exercise initialization,
selection, clear, invalid IDs, stale revisions, and disposal against a WebGL
build. Never commit real tokens or production URLs to this sample.
