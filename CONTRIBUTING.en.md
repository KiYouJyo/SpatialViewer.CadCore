# Contributing

[中文](CONTRIBUTING.md) · [日本語](CONTRIBUTING.ja.md) · [English](CONTRIBUTING.en.md)

## Development rules

1. Kernel code must not depend on SpatialViewer WinUI pages, controls, or application lifecycle.
2. Third-party CAD reader types must stay inside their adapter project.
3. Color, curve/arc, block, text, linetype, and lineweight changes require regression coverage.
4. Keep `TreatWarningsAsErrors=true`; run Release build and all tests before merging.
5. Breaking public API changes must document migration steps and update CHANGELOG.

Prefer small, verifiable pull requests. Correctness fixes should include a minimal fixture or focused unit test.
