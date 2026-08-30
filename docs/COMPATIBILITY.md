# Compatibility policy

CadCore follows semantic versioning once the first public package/tag is published.

- Patch: rendering/import correctness fixes without intended API breakage.
- Minor: new CAD entities, optional capabilities, additive APIs.
- Major: intentional breaking API or data-model changes.

SpatialViewer should pin an explicit CadCore revision/version. Kernel updates are accepted only after CadCore CI and SpatialViewer integration tests both pass.
