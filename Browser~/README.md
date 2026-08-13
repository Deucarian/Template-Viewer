# Local browser harness

The harness includes a self-contained distribution snapshot of the browser
host from `com.deucarian.command-routing.webgl-integration`, so it works from a
normal installed package or an exported directory without sibling checkouts or
manual file copying. Keep that snapshot aligned when the integration package's
browser-host contract changes.

Build the iframe-configured development scene into `Browser~/Build`, then serve
this directory from `http://localhost:8080`. The iframe's configured parent
origin must exactly match the server origin. Opening the file directly does not
exercise production `postMessage` origin checks.

The harness covers initialization, two distinct visibility results, clear,
invalid selection, stale revision rejection, and dispose. It deliberately has
no credentials or production endpoint.
