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

Command handlers validate and map canonical envelopes before delegating to the
application. Product features may replace only the initialization handler and
visibility owner, preventing multiple systems from competing over model state.

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
