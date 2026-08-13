# Changelog

## [0.1.0] - 2026-08-13

- Added the generic Web Viewer template runtime and explicit composition root.
- Added secure WebGL commands, revisioned visibility, and sanitized diagnostics.
- Added a local browser harness, runnable sample, and Build Pipeline provider.
- Reserved accepted initialization revisions before asynchronous work and added
  generation guards so stale in-flight initialization cannot regain ownership.
- Made the local browser harness self-contained outside the source checkout.
