# SpatialViewer.CadCore

[中文](README.md) · [日本語](README.ja.md) · [English](README.en.md)

Independent 2D CAD viewing kernel for SpatialViewer. This repository owns CAD reader adapters, the CAD semantic model, geometry/scene translation, rendering abstractions, Windows rendering backend integration, and regression tests. The WinUI 3 product UI remains in `KiYouJyo/SpatialViewer`.

## Principles

- **UI independent** — parsing, CAD semantics, geometry and scene translation must not depend on WinUI pages or controls.
- **Reader isolation** — ACadSharp is confined to its adapter project; third-party types must not leak through the public boundary.
- **Preserve semantics** — ARC/CIRCLE/ELLIPSE remain curve primitives instead of being permanently flattened during import.
- **Regression first** — changes to color, curves, blocks, text and line styles require automated coverage.
- **Independent versioning** — CadCore and SpatialViewer UI evolve on separate release lines and integrate through an explicit dependency revision.

## Repository boundary

This repository is the source of truth for the CAD kernel. `SpatialViewer` owns application shell, tabs, toolbars, panels and user interaction.

## License

MIT. See `THIRD-PARTY-NOTICES.md` for third-party notices.
