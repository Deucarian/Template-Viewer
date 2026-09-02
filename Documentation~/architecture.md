# Architecture

The package is a reusable viewer core. Platform packages own transport and
deployment concerns; product packages own domain behavior.

```text
Web / desktop / XR adapter
  -> IViewerPlatformAdapter
       -> IViewerEventPublisher + endpoint
       -> command transport activation
       -> IViewerLifecycleStatusSink
       -> ViewerBootstrap
       -> ViewerApplication
            -> IViewerModelDescriptorResolver
            -> IViewerModelLoader
            -> IViewerReferenceNavigation
            -> IViewerVisibilityFeature
       -> Command Routing handlers
       -> authentication + diagnostics
       -> optional reference rendering/navigation/shell
       -> generic navigation/display command adapters
       -> optional platform reveal + product readiness
  -> product ViewerFeatureBehaviour components
```

## Ownership

`ViewerApplication` owns initialization, reinitialization, revision ordering,
model lifecycle, selection delegation, and application events. It depends on
small contracts and has no browser, desktop-window, XR-input, or concrete
orbit-camera dependency.

`ViewerBootstrap` is the generic composition root. Its adapter factory selects
one host boundary. Default rendering/navigation/shell hooks install the shared
reference viewer experience, while derived XR or specialist bootstraps may
replace any of those presentation hooks without replacing the application.
Features on the bootstrap remain implicit for compatibility. A serialized
explicit list may add features from other GameObjects in the same scene;
composition keeps local component order followed by explicit Inspector order,
deduplicates references, and rejects invalid scene-lifetime relationships
before presentation or authentication work begins.

Command handlers validate and map canonical envelopes before delegating to the
application. Product features may replace only the initialization handler and
visibility owner, preventing multiple systems from competing over model state.
The reference presentation handlers delegate directly to the Viewer Navigation
and Viewer Rendering controllers already composed by the bootstrap. They do not
create a second state owner. A platform bootstrap may explicitly enable the
package-owned reveal; the default remains disabled so custom and non-Web
bootstraps retain their established behavior. When enabled, it runs alongside,
not instead of, the one product readiness feature and restores authored scale on
every exit. The core composition owns its single instance and final disposal.

Display-setting projections use one lifecycle-owned FIFO. Each projection
captures its sanitized payload and accepted endpoint before enqueue, delivery is
serialized, and disposal cancels active work and drops queued work before the
platform transport activation lease is released.

The composition root subscribes once to Command Routing's all-outcomes route
completion and owns the sole remote `command_failed` projection. It queues those
projections onto the Unity synchronization context, serializes delivery, and
allows product features to normalize their own fields on a defensive copy
before reapplying the canonical command, error code, and message. It then
notifies product feature hooks from the same immutable canonical payload.
`CommandDispatcher.CommandCompleted` and
`ViewerFeatureBehaviour.OnCommandCompleted` remain the decoded-dispatch
compatibility path and do not own remote failures.

The authentication command handler still owns the only authentication outcome
publisher. Its adapter creates one immutable sanitized projection, notifies
feature-local compatibility hooks with defensive payload copies, and then sends
exactly one event through the configured platform endpoint. Features cannot
publish or redirect that remote route through the observer contract.

The transport-neutral command harness is runtime schema, not a browser asset.
Each adapter decides whether and how to display or execute its scenarios.

## Dependency direction

- Core never references adapter assemblies.
- Adapters reference core and their platform transport.
- Products reference core plus every adapter required by their build targets.
- Platform selection occurs in a product/adapter bootstrap, never in
  `ViewerApplication`.

This permits one product to build for Web, desktop, and XR while retaining one
model-loading, lifecycle, command, authentication, and selection implementation.
