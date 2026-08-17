# Deucarian Web Viewer Template

`com.deucarian.template.viewer.web` is a ready-to-run generic starting point
for browser-hosted Unity viewers. It uses the reusable Deucarian stack instead
of copying a viewer application:

- Viewer Navigation supplies Orbit/Fly, Top Down, Return to Origin, the polished
  shared-reference icon toolbar and interactions, pointer/input coordination,
  browser reduced-motion handling, and an optional six-face view cube that is
  off by default. With no intentional
  override, it loads the package's complete reference navigation composition,
  including its canonical dark Frosted Glass theme and theme provider;
- Command Routing and its WebGL Integration supply canonical envelopes,
  direct-page and secure iframe transport, ready handshake, and cleanup;
- API, Object Loading, and their integration load AssetBundle content;
- Diagnostics reports sanitized lifecycle, revision, and element counts; and
- Build Pipeline owns the shared policy while this template supplies the
  project-specific development/production provider and profiles.

## Quick start

1. Import the **Web Viewer** sample and open `Scenes/WebViewer.unity`.
2. Enter Play Mode. The bootstrap creates an embedded three-element model and
   waits for `initialize_viewer`.
3. For a WebGL build, open the Deucarian Build Pipeline Manager and choose
   **Sync Profiles** for the Web Viewer Template provider.
4. Build **Development** and serve the self-contained `Browser~` harness at
   `http://localhost:8080`.

The sample is credential-free and defaults to an embedded model. A host can
instead supply an HTTP(S) or API-relative `model_url`. Replace
`IWebViewerModelDescriptorResolver` in the composition root when an application
must resolve a project/model/version context. Do not put backend DTOs in this
template.

## Commands

All commands use the canonical Command Routing envelope:

```json
{
  "protocol_version": 1,
  "command_id": "host-42",
  "command": "initialize_viewer",
  "payload": { "revision": 1 },
  "metadata": { "source": "host" }
}
```

Supported generic commands:

- `initialize_viewer`: `revision`, optional `model_url`, `model_id`,
  `model_version`, `cache_version`, and `cache_hash`;
- `select_elements`: `revision` and one or more stable `element_ids`;
- `clear_selection`: `revision`, restoring the captured visibility baseline;
- `dispose_viewer`: `revision`, unloading the model and cancelling work.

The browser receives `viewer_loading`, application-level `viewer_ready`,
`viewer_failed`, `selection_applied`, and `viewer_disposed` events. Transport
readiness only means listeners are installed; `viewer_ready` is emitted after
model loading, identifier indexing, and navigation reference/origin capture.

## State and camera guarantees

`WebViewerSelectionStateOwner` is authoritative for generic selection. Newer
revisions supersede older state; stale revisions and unknown IDs preserve the
last valid visibility plan. Clearing restores the baseline captured after load.

Selection updates only call `WebViewerVisibilityController`. They never call
Viewer Navigation, so camera transform, projection, pivot, navigation mode,
and current user position remain unchanged. Initial model registration frames
once and captures Return to Origin after model placement.

`WebViewerBootstrap.ResolvedNavigationComposition` exposes the exact cached
composition used at runtime. `NavigationInstaller` exposes the installed
controller/provider, while `CurrentTheme` and `WebViewerStatusOverlay.CurrentTheme`
resolve to the same canonical theme. Supplying custom navigation settings only
replaces the preset; input, bounds, animation, and theme policies stay shared.

## Browser security

Direct-page mode uses same-page events. Iframe mode requires an exact HTTP(S)
allowed and target origin, validates the parent source window, and never sends
to `*`. Production validation additionally requires a non-loopback HTTPS origin.
The host owns the Unity instance and disposes its listeners on teardown.

## Build profiles

`WebViewerBuildManagerProvider` is discovered by Deucarian Build Pipeline. Its
explicit **Sync Profiles** action creates project-owned scenes and WebGL Build
Profile assets under `Assets/Deucarian/WebViewer` and applies the shared dev or
production policy. Production validation rejects local/insecure iframe origins;
Build Pipeline excludes development diagnostics and development-context files.

## Extension points

- implement `IWebViewerModelDescriptorResolver` for application API/model
  version resolution;
- implement `IWebViewerModelLoader` only when Object Loading cannot represent
  the source;
- replace the example `WebViewerElement` index/controller with a domain-owned
  visibility capability;
- add application commands through Command Routing handlers, not the transport.

## Validation

Run the Package Registry validator, Unity EditMode/PlayMode tests, browser tests
with `npm test` in `Browser~`, and `git diff --check`.

## License

See [LICENSE.md](LICENSE.md).
