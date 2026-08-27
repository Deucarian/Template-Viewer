# Changelog

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
