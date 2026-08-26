# Deucarian Viewer Template Agent Notes

Package ID: `com.deucarian.template.viewer`

Follow Package Registry architecture, dependency, distribution, and release
policies.

## Ownership

This template owns the platform-neutral viewer application, lifecycle and
wire contracts, generic commands and visibility state, Object Loading/API
adapter, shared rendering/navigation/shell/authentication composition,
sanitized diagnostics, and the platform-adapter boundary.

It must not own camera math, raw input, pointer capture, browser, desktop, or
XR transport implementations, generic command routing, AssetBundle loading
internals, product DTOs, or backend-specific model/version lookup.

## Invariants

- Selection changes element visibility only. It never invokes camera movement.
- The application publishes `viewer_ready` only after the model, identifier
  index, navigation reference, and command host are ready.
- Revisions are monotonic. Invalid or stale selection keeps the last valid state.
- Reinitialization and disposal release prior loads and listeners idempotently.
- Exactly one platform adapter owns the event route, command transport, and
  external lifecycle-status sink for one composition.
- No direct `UnityEngine.Debug`; use Deucarian Logging.
- Operational diagnostics contain no source URLs, tokens, or command payloads.
- Platform build profiles and host-specific UI belong to adapter packages.

## Validation

Run the Package Registry validator, EditMode and PlayMode tests on Unity 6000, and
`git diff --check`.
