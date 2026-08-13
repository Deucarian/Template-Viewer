# Generic browser protocol

The transport wire format is owned by Command Routing WebGL Integration. This
template adds only application command names and payload schemas.

`initialize_viewer` accepts a complete model context rather than arbitrary
patches. A missing `model_url` selects the embedded sample model. Applications
should replace the descriptor resolver to turn their model context into an
exact source/version.

`select_elements` is the example visibility command. IDs must be unique,
stable, and registered by `WebViewerElement`. Unknown IDs fail without clearing
or altering the last valid state. Each successful change emits its applied
revision. There is no camera command in the selection path.

Iframe hosts must configure one exact origin on both sides and send to the
viewer iframe's `contentWindow`. Hosts queue application commands until the
application's `viewer_ready` event and resend current context/state after a
viewer recreation.
