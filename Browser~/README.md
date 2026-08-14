# Local browser harness

The harness includes a verbatim distributable copy of the canonical browser
host from `com.deucarian.command-routing.webgl-integration` 0.1.1, so it works
from a normal installed package or an exported directory without sibling
checkouts. Update this generated copy from that package; do not maintain an
independent host implementation here.

Build the iframe-configured development scene into `Browser~/Build`, then serve
this directory from `http://localhost:8080`. The iframe's configured parent
origin must exactly match the server origin. Opening the file directly does not
exercise production `postMessage` origin checks.

The harness covers initialization, two distinct visibility results, clear,
invalid selection, stale revision rejection, and dispose. Its executable Node
tests instantiate the real copied host, exercise its full exported lifecycle,
and drive every harness action. The harness deliberately has no credentials or
production endpoint.
