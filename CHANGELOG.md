# Changelog

## [0.3.1] - 2026-09-02

### Added

- Added one Template-owned `command_failed` projection for every failed route,
  including protocol rejections that never reach a command handler.
- Added an immutable product feature hook for mirroring that exact canonical
  payload to local legacy observers without creating another remote route.
- Added an immutable authentication-outcome feature hook for mirroring the
  existing sanitized outcome to local legacy observers while retaining one
  platform publisher and the established public publisher constructor.

### Changed

- Failure events are serialized on the Unity composition context, retain the
  effective route endpoint, and stop accepting queued work during teardown.
- Command Routing was updated to 0.2.6 for its all-outcomes route-completion
  contract.
- Display-settings projections are now serialized, retain the endpoint captured
  when each change is enqueued, and cancel active or queued delivery during
  teardown before the platform transport is released.
- Model reveal readiness is now an explicit platform-bootstrap opt-in. Web
  enables it while custom and non-Web bootstraps retain their established
  readiness behavior.
- Product features may remove or normalize their own domain fields on a
  defensive failure-projection copy; Template reapplies the canonical command,
  error code, and message and remains the sole remote publisher.

### Fixed

- Locked the deliberate `Create`, `CreateDefault`, and null-controller
  `CreateWithPresentation` handler sets and strict presentation-command
  collision behavior with compatibility tests.

## [0.3.0] - 2026-09-02

### Added

- Added the proven generic navigation wire aliases to the default viewer command
  composition, delegating every request to Viewer Navigation's authoritative
  controller.
- Added the generic display-settings command aliases and one sanitized
  `display_settings_changed` projection over Viewer Rendering state.
- Added a package-owned 0.48-second model reveal that uses the shared soft-back
  easing and reduced-motion policy while running concurrently with product
  readiness.

### Changed

- Handler catalogs now advertise the complete reference presentation command set
  even without live controllers, while execution fails safely until those
  controllers are composed.
- The application remembers the endpoint committed by the newest successfully
  completed initialization so failed, canceled, stale, or superseded work cannot
  redirect generic presentation events.
- Viewer Navigation was updated to 0.1.14 and Common 0.2.1 is now an explicit
  dependency for the governed reveal easing. Direct Camera Navigation,
  Diagnostics, Logging, and Theming pins now match Viewer Navigation's required
  versions.

### Fixed

- Navigation payloads now reject non-finite sensitivities before any shared
  controller state can change.
- Restored the exact established three-parameter `ViewerCommandHandlers.Create`
  signature and handler set so existing callers can still append their own
  presentation handlers without collisions or direct navigation/rendering
  assembly references; the expanded reference set uses `CreateDefault` or
  `CreateWithPresentation`.
- Initial display projection now waits for platform transport activation and
  uses the adapter event endpoint until an initialization endpoint is accepted.
- Product readiness failure, supersession, and cancellation can no longer leave a
  partially revealed model at zero scale.
- Host teardown now completes concurrent reveal readiness even when a product
  readiness task ignores cancellation; eventual product faults remain observed.
- A wire-level `navigation: null` again falls back to the canonical `payload`,
  preserving the established Report command-envelope behavior.

## [0.2.1] - 2026-08-28

### Fixed

- Viewer composition failures now identify the failing startup stage, preserve
  a sanitized actionable cause, and tell developers how to retry after fixing
  the named configuration.

## [0.2.0] - 2026-08-27

### Breaking

- Replaced Viewer Authentication 0.5.1 with generic Authentication 1.0.0 and
  its optional viewer-integration assembly.
- Removed the package-resource token-endpoint fallback; standalone
  authentication acquisition now requires an explicitly assigned endpoint
  profile.
- Updated the coordinated API, Session API Integration, navigation,
  diagnostics, logging, and command-routing dependencies.

## [0.1.1] - 2026-08-27

### Added

- Added a platform-neutral product readiness feature that can complete
  asynchronous model preparation before the Ready lifecycle and
  `viewer_ready` event.
- Added a product command-completion observer without exposing or duplicating
  the command router.

### Changed

- Product readiness participates in initialization cancellation,
  supersession, and failure handling.

## [0.1.0] - 2026-08-26

### Added

- Introduced the platform-neutral `Deucarian.TemplateViewer` runtime.
- Added `IViewerPlatformAdapter` for event publication, command-transport
  activation, and lifecycle status projection.
- Added `IViewerReferenceNavigation` so desktop/Web compositions may frame with
  the reference orbit camera while XR compositions may register their own
  origin without inheriting camera behavior.
- Added overridable rendering, navigation, and shell composition hooks on the
  abstract `ViewerBootstrap`.
- Added transport-neutral command-harness scenario and catalog APIs.
- Added platform-neutral EditMode coverage and the PlayMode visibility/camera
  invariant, using generic platform and navigation fakes where appropriate.
- Added deterministic explicit same-scene feature composition while preserving
  automatic feature discovery on the bootstrap GameObject.

### Changed

- Renamed generic `WebViewer*` application types to `Viewer*` under
  `Deucarian.TemplateViewer` while preserving command, event, and JSON wire
  names.
- Split the application and bootstrap composition into focused production
  files below 500 lines.

### Removed

- Removed browser scripts, iframe/WebGL transport, `.jslib`, WebGL build
  provider/template dependencies, and Web-only samples from the core package.
  They remain responsibilities of the Web adapter package.
