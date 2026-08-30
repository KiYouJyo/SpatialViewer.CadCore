# CadCore Architecture / 内核架构 / アーキテクチャ

## Boundary

`SpatialViewer.CadCore` owns the CAD ingestion-to-render pipeline. `SpatialViewer` owns product UI and interaction.

```text
DWG/DXF
  -> Reader Adapter (ACadSharp)
  -> CAD semantic records
  -> CAD-to-Scene translator
  -> double-precision Scene2D
  -> backend-neutral RenderFrame
  -> Win2D renderer (optional Windows backend)
  -> SpatialViewer UI surface
```

## Dependency direction

- Core primitives know nothing about CAD readers or UI.
- CAD model depends only on Core primitives.
- Reader adapters depend on CAD model + third-party reader.
- Rendering abstraction depends only on Core primitives.
- Windows rendering backend depends on rendering abstraction + Win2D.
- SpatialViewer depends on CadCore; CadCore must never reference SpatialViewer.App/Presentation.

## Migration rule

During the initial extraction, existing namespaces are retained where practical to minimize behavioral changes. Namespace/package cleanup is a separate, test-backed migration and must not be mixed with correctness fixes.
