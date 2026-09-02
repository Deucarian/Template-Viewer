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

Add product behavior by deriving from `ViewerFeatureBehaviour`. Components on
the bootstrap GameObject remain auto-discovered. The bootstrap's serialized
explicit feature list can also reference components elsewhere in the same
scene, which is useful when one product scene composes distinct desktop, Web,
or XR presentation roots. Local features run first; explicit entries follow
in Inspector order, and duplicate references are composed only once. Null,
destroyed, prefab-asset, unloaded-scene, and cross-scene entries fail with a
configuration error before presentation composition begins.
`ResolvedFeatureBehaviours` exposes that same validated order to generic
adapter tooling without giving it ownership of feature discovery.

A feature may contribute command handlers, replace the generic
`initialize_viewer` handler, provide one domain visibility owner, and
contribute command-harness scenarios. A product may also provide one
`IViewerModelReadinessFeature` for asynchronous work that must finish after
shared model presentation and navigation registration but before
`viewer_ready`, such as loading product annotations. Features may observe
completed commands to publish domain-specific events, while routing and
transport ownership remain in the core and active platform adapter. The
`OnAuthenticationOutcome` hook exposes the exact immutable sanitized
authentication projection for local compatibility observers only; it never
creates another remote publisher.

## Commands and events

Wire names remain stable across adapters. Generic commands are:

- `initialize_viewer`
- `select_elements`
- `clear_selection`
- `dispose_viewer`
- `update_access_token` and compatibility alias `updateaccesstoken`
- `refresh_access_token`
- `clear_access_token`

The reference presentation also registers the established generic navigation
commands (`navigation`, `navigate`, `nav`, navigation mode/sensitivity aliases,
and the direct Home, Top, Orbit, and Fly aliases) plus
`setdisplaysettings`/`set_display_settings`. Every navigation command delegates
to the one Viewer Navigation controller; every display command delegates to the
one Viewer Rendering controller. `CreateDefault` and null-controller
`CreateWithPresentation` advertise the same wire set for harness generation and
fail safely if a custom composition did not supply those reference controllers.
The established `Create` method retains its smaller legacy handler set so
existing products may continue owning presentation aliases during migration;
combining two owners is rejected as a command collision.

Generic application events are `viewer_loading`, `viewer_ready`,
`viewer_failed`, `selection_applied`, `viewer_disposed`,
`display_settings_changed`, and the sanitized authentication lifecycle events.
Display events contain only the rendering mode, camera-relative-light state,
effects state, and change source. They are published in enqueue order and retain
the endpoint that was current when each change was observed. The initial
projection uses the active platform event endpoint until an initialization
succeeds.

Authentication success events remain `access_token_updated`,
`access_token_refreshed`, and `access_token_cleared`. They contain only status,
token-presence, refresh capability, and optional expiry metadata. The feature
hook receives a defensive copy before the sole platform publication, so a local
legacy observer cannot mutate, suppress, redirect, or duplicate the remote
event.

`viewer_ready` is emitted only after loading, presentation, element indexing,
reference registration, any platform-enabled shared model reveal, and any
configured product readiness feature complete.

Template Viewer is the sole remote owner of `command_failed`. It projects one
event for every failed command route, including invalid JSON, missing command
names, unsupported commands, cancellations, and domain-handler failures. The
payload preserves a domain failure's object fields, then authoritatively sets
`command`, `error_code`, and `message`, and uses the exact effective route
endpoint. A feature may use `CustomizeCommandFailureProjection` to remove or
normalize only its own fields on a defensive copy; the three canonical fields
are reapplied afterward. A feature may override `OnCommandFailureProjected` to mirror the
immutable canonical payload to local legacy observers; it must not publish a
second remote event. The existing `OnCommandCompleted` dispatch observer remains
unchanged and still receives decoded dispatch outcomes only.

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

Platforms opt into the reference reveal explicitly. The Web adapter enables it
for both Web viewers; custom desktop and XR bootstraps retain their prior
readiness behavior unless their adapter contract opts in. When enabled, each
prepared model reveals from zero to its authored scale over 0.48 seconds with
the shared soft-back easing. It honors the same runtime and browser
reduced-motion policy as navigation. Product readiness begins concurrently,
while `viewer_ready` waits for both; failure, cancellation, host interruption,
or a motion-preference change always restores the authored scale.

## Validation

Run the Package Registry validator, Unity EditMode and PlayMode tests, and
`git diff --check`. Platform-adapter packages own their transport, build, and
end-to-end tests.

See [Documentation~/architecture.md](Documentation~/architecture.md) for the
dependency boundaries and [Documentation~/protocol.md](Documentation~/protocol.md)
for the application payload contract.

## License

See [LICENSE.md](LICENSE.md).
