# Architecture

The template is an application example, not a reusable viewer-core package.

```text
browser
  -> WebGL ICommandTransport
  -> Command Routing handlers
  -> WebViewerApplication (lifecycle owner)
       -> model descriptor strategy
       -> Object Loading adapter
       -> selection state owner
            -> identifier index
            -> visibility side-effect adapter
       -> Viewer Navigation (initial reference registration only)
  -> sanitized events and Diagnostics
```

The composition root chooses concrete implementations explicitly. Command
handlers validate/map and delegate. Model descriptor resolution is a pure
strategy. Asset loading and `GameObject.SetActive` are narrow side-effect
adapters. The application owns initialization/reinitialization/disposal; the
selection state owner owns only monotonic selection state.

There is no service locator and no reflection in runtime code. The only
discovery is Build Pipeline's documented editor-only provider discovery.
