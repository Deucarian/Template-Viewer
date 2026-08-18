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
            -> package-owned canonical toolbar and input surface
            -> UI-owned canonical panel, semantic depth, and tooltip overlay
            -> Theming-owned reference family/style resolution
  -> sanitized events and Diagnostics
```

The composition root chooses concrete implementations explicitly. Command
handlers validate/map and delegate. Model descriptor resolution is a pure
strategy. Asset loading and `GameObject.SetActive` are narrow side-effect
adapters. The application owns initialization/reinitialization/disposal; the
selection state owner owns only monotonic selection state.

There is no service locator and no reflection in runtime code. The only
discovery is Build Pipeline's documented editor-only provider discovery.

The template composes `ViewerNavigationReferenceComposition` as one unit. That
unit owns the navigation settings, bounds strategy, reduced-motion policy,
UI-input blocker, theme profile, and canonical toolbar presentation. Consumers
may supply an intentional navigation-settings override, but must not copy the
toolbar UXML/USS, colors, control-island styling, pointer handling, or movement-
key guard into application code. Theme values are resolved by
`com.deucarian.theming`, not duplicated in this package.

`com.deucarian.ui` owns the cross-package surface contract. The navigation
document requests `PrimaryControls`, the application-owned lifecycle status
document requests `Status`, and transient tooltips request `Tooltip`. Runtime UI
must use `DeucarianUIRuntime` for those roles instead of assigning raw sorting
orders or introducing package-private PanelSettings assets. Feature packages
continue to own their content and interactions; UI alone owns their relative
screen-space composition. Keeping these surfaces in UI Toolkit also makes the
tooltip panel's cross-document ordering deterministic instead of relying on
numeric similarities between unrelated rendering systems.
