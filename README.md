# Deucarian Viewer Template

`com.deucarian.template.viewer` is the platform-neutral core for Deucarian
viewers. It owns the reusable application lifecycle, model loading and
presentation, selection, commands, authentication, diagnostics, rendering,
navigation, and shell composition.

Browser, desktop, and XR integrations belong in small adapter packages. A
product derives from `ViewerBootstrap` and returns exactly one
`IViewerPlatformAdapter` for the active build. The adapter supplies:

- an event publisher and endpoint;
- command-transport activation; and
- a lifecycle/progress status sink.

The core has no browser scripts, iframe policy, WebGL plug-in, build-profile
provider, or runnable Web sample. Those assets live with the Web adapter, so a
single viewer product may reference Web, desktop, and XR adapters without
duplicating its application logic.

## Composition

Create a platform bootstrap by deriving from `ViewerBootstrap`:

```csharp
public sealed class DesktopViewerBootstrap : ViewerBootstrap
{
    protected override IViewerPlatformAdapter CreatePlatformAdapter() =>
        new DesktopViewerPlatformAdapter();
}
```

The default composition installs the shared reference rendering, orbit-camera
navigation, and viewer shell. Platforms such as XR may override
`ComposeRendering`, `ComposeReferenceNavigation`, or `ComposeShell` while
reusing loading, commands, authentication, lifecycle, and product features.
`ViewerApplication` depends only on `IViewerEventPublisher` and
`IViewerReferenceNavigation`; it never selects a platform or camera system.

Add product behavior beside the platform bootstrap by deriving from
`ViewerFeatureBehaviour`. A feature may contribute command handlers, replace
the generic `initialize_viewer` handler, provide one domain visibility owner,
and contribute command-harness scenarios.

## Commands and events

Wire names remain stable across adapters. Generic commands are:

- `initialize_viewer`
- `select_elements`
- `clear_selection`
- `dispose_viewer`
- `update_access_token` and compatibility alias `updateaccesstoken`
- `refresh_access_token`
- `clear_access_token`

Generic application events are `viewer_loading`, `viewer_ready`,
`viewer_failed`, `selection_applied`, `viewer_disposed`, and the sanitized
authentication lifecycle events. `viewer_ready` is emitted only after loading,
presentation, element indexing, and reference registration complete.

`ViewerCommandHarnessScenario`, `ViewerCommandHarnessCatalog`, and
`ViewerCommandHarnessCatalogBuilder` describe transport-neutral command
examples. An adapter may render that catalog in a browser page, desktop tool,
or another development surface.

## Model and camera behavior

The default `DirectViewerModelDescriptorResolver` accepts an optional HTTP(S)
or API-relative `model_url`. Products may replace the initialization handler to
resolve project/model/version identifiers before delegating to the application.

`IViewerReferenceNavigation` is the only navigation dependency required by the
application. The default `ViewerNavigationReferenceAdapter` registers and
frames a model with shared Viewer Navigation. XR can provide an origin-aware
implementation and choose not to apply orbit-camera framing.

Selection is revisioned and changes only visibility. It never mutates camera,
navigation mode, pivot, projection, or user position. Clearing restores the
visibility baseline captured after model load.

## Validation

Run the Package Registry validator, Unity EditMode tests, and
`git diff --check`. Platform-adapter packages own their transport, build, and
end-to-end tests.

See [Documentation~/architecture.md](Documentation~/architecture.md) for the
dependency boundaries and [Documentation~/protocol.md](Documentation~/protocol.md)
for the application payload contract.

## License

See [LICENSE.md](LICENSE.md).
