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

Events are published through the selected platform adapter. `viewer_ready`
means the application model is loaded, prepared, indexed, and registered with
the selected navigation implementation. Product metadata readiness may use a
later product-specific event when it is asynchronous.
