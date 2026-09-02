# Compatibility policy

CadCore follows semantic versioning once the first public package/tag is published.

- Patch: rendering/import correctness fixes without intended API breakage.
- Minor: new CAD entities, optional capabilities, additive APIs.
- Major: intentional breaking API or data-model changes.

SpatialViewer should pin an explicit CadCore revision/version. Kernel updates are accepted only after CadCore CI and SpatialViewer integration tests both pass.

## In-progress Tianzheng milestone

The v0.12 Tianzheng Architecture work uses explicit evidence and release gates rather than treating object preservation or Proxy Graphics as native semantic support. See [v0.12 Tianzheng Architecture 2D acceptance matrix](V012_TIANZHENG_ARCHITECTURE.md).

Product version remains at the latest completed milestone until all declared v0.12 core-category gates are satisfied. The acceptance matrix does not silently reduce the scope previously declared by the v0.11.0 release notes.
