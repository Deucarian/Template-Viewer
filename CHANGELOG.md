# Changelog

## [0.1.3] - 2026-08-14

- Removed the unused direct Common dependency so the template contract matches
  its actual assembly usage and passes the authoritative dependency audit.

## [0.1.2] - 2026-08-14

- Made the sample composition resolve Viewer Navigation's canonical reference preset,
  giving new viewer projects the same controls, transition timing, framing tuning,
  toolbar, and view-cube defaults as Report Viewer.
- Added parity coverage so the generic template cannot silently fall back to a
  different navigation configuration.

## [0.1.1] - 2026-08-14

- Replaced the provisional browser host with the canonical WebGL Integration 0.1.1 distributable.
- Updated the harness to the supported listener and `sendCommand` API and added executable lifecycle coverage.
- Aligned the template with the two-consumer-proven shared package versions.
- Declared Camera Navigation directly because the template composition consumes types exposed by Viewer Navigation's public installer API.

## [0.1.0] - 2026-08-13

- Added the generic Web Viewer template runtime and explicit composition root.
- Added secure WebGL commands, revisioned visibility, and sanitized diagnostics.
- Added a local browser harness, runnable sample, and Build Pipeline provider.
- Reserved accepted initialization revisions before asynchronous work and added
  generation guards so stale in-flight initialization cannot regain ownership.
- Made the local browser harness self-contained outside the source checkout.
