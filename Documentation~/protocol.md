# Viewer application protocol

Command Routing owns the canonical envelope and each platform adapter owns its
transport. This package owns only application command names and payloads.

`initialize_viewer` accepts:

- required monotonic `revision`;
- optional `model_url` (HTTP(S) or API-relative);
- optional `model_id`, `model_version`, `cache_version`, and `cache_hash`; and
- optional `append_platform_query`.

A missing `model_url` selects the composition's embedded model. Products that
receive project/model/version identifiers should replace the initialization
handler, resolve an exact model descriptor, and then delegate to
`ViewerApplication`.

`select_elements` accepts a newer `revision` and stable `element_ids`.
`clear_selection` accepts a newer `revision` and restores the captured
visibility baseline. Unknown IDs and stale revisions fail without altering the
last valid state. Neither command changes navigation or camera state.

`dispose_viewer` accepts a newer `revision`, cancels active work, unloads the
model, and ends the lifecycle.

Reference viewer compositions also accept the established host presentation
commands without advancing the application revision:

- `navigation`, `navigate`, `nav`, and the navigation-sensitivity aliases accept
  an object with optional `action`, `mode`, `view`, `sensitivity`, and
  `global_sensitivity`; string action and numeric sensitivity payloads remain
  compatible;
- the navigation-mode aliases accept a mode string or the same navigation object;
- the direct Home, Top, toggle-Top, Orbit, and Fly aliases select their action from
  the command name; and
- `setdisplaysettings` and `set_display_settings` accept
  `rendering_mode`/`renderingMode` and
  `camera_relative_light`/`cameraRelativeLight`.

Display changes publish one `display_settings_changed` event with
`rendering_mode`, `camera_relative_light`, `effects_active`, and `source`. The
event uses the endpoint committed when that change is enqueued and does not
contain or advance a revision itself. Multiple changes publish in enqueue order.
Before the first successful initialization, the initial projection uses the
active platform adapter's configured event endpoint.

Successful authentication commands publish exactly one of
`access_token_updated`, `access_token_refreshed`, or `access_token_cleared`.
Their payload contains `status`, `has_access_token`, `can_refresh`,
`expiry_known`, and optional `expires_at_utc`; it never contains a token. Local
feature observers receive defensive copies of this same canonical projection
and do not own another transport route.

Events are published through the selected platform adapter. `viewer_ready`
means the application model is loaded, prepared, indexed, and registered with
the selected navigation implementation. On platforms that explicitly enable
the package-owned model reveal, it and the single product readiness feature run
concurrently and both finish before Ready. Failure or cancellation restores the
model's authored scale. Product metadata readiness may use a later
product-specific event when it is asynchronous.

Every failed route publishes exactly one `command_failed` event. Empty,
malformed, or oversized command messages map to `invalid_json`; a missing name
maps to `missing_command`; unsupported commands map to `unsupported_command`;
and handler error codes, messages, and object payload fields are otherwise
preserved. The canonical projection then adds or overwrites `command`,
`error_code`, and `message` and uses the route's effective remote endpoint.
Product features may normalize their own domain fields on a defensive payload
copy, but cannot replace those canonical fields or create another remote send.
