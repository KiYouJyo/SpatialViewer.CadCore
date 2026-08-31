# Changelog

All notable CadCore changes are recorded here.

## Unreleased

## 0.3.1 - 2026-08-31

### Fixed
- Decoupled CadCore product versioning from CLR assembly identity so independent kernel updates no longer fail with `0x80131040` manifest-definition mismatches.
- All public CadCore assemblies now share a stable ABI `AssemblyVersion` of `1.0.0.0`; release identity remains in package metadata, `FileVersion`, and `InformationalVersion`.

### Compatibility
- `cadcore-release.json` now publishes `abiVersion` and the release pipeline verifies that every required assembly uses that ABI while retaining the v0.3.1 product version in file metadata.
- SpatialViewer 0.2.x can preload a newer compatible CadCore before its first static reference, provided both bundled and downloaded kernels use the same ABI identity.

## 0.3.0 - 2026-08-31

### Added
- Per-vertex AutoCAD bulge preservation for `LWPOLYLINE` and `POLYLINE2D`, with bulged segments represented as analytic `ArcGeometry` instead of permanent polylines.
- Active CAD linetype pattern import, including signed dash/gap segments plus entity and global linetype scales.
- Transform-aware adaptive ellipse/circle tessellation using the full local-to-screen mapping.
- Backend-neutral text placement logic that derives screen rotation and glyph scale from the complete world transform.
- A focused `fidelity-v030.dxf` regression fixture covering bulges, dashed lines, rotated ellipses, rotated text, and scaled block text.

### Fixed
- Polyline arc segments encoded through bulge no longer render as straight chords.
- Non-continuous CAD linetypes no longer collapse to solid strokes in the Win2D renderer.
- Rotated and non-uniformly transformed ellipses/circles no longer reconstruct incorrect axis-aligned screen radii.
- Rotated CAD text now rotates on screen, and text inside scaled blocks now follows the effective block scale.
- Ellipse import now reads ACadSharp's `MajorAxisEndPoint` vector instead of accidentally treating the scalar `MajorAxis` length as a point.

### Compatibility
- Existing `CadPolylineEntity` primary-constructor shape is preserved; bulges are exposed through an additive `Bulges` init property.
- This release intentionally does not add HATCH, SPLINE, DIMENSION, LEADER/MLEADER, ATTRIB/ATTDEF, or Paper Space support; those remain for later entity-coverage work.

## 0.2.1 - 2026-08-31

### Changed
- Bumped CadCore assembly/package metadata to 0.2.1 so SpatialViewer can distinguish this release from the older bundled 0.2.0 kernel during independent runtime update checks.
- Kept the release package contract explicit for `SpatialViewer 0.2.x`: x64 payload, `cadcore-release.json`, project-separated binaries, and SHA-256 checksum asset.
- Release notes are now version-driven instead of hard-coding the previous tag.

### Notes
- Rendering behavior is unchanged from CadCore 0.2.0; this patch establishes a clean version boundary for restart-safe independent kernel updates.

## 0.2.0 - 2026-08-31

### Added
- Independent CadCore repository infrastructure.
- Chinese, Japanese and English repository documentation.
- Independent .NET 10 build/test boundary.
- Deterministic AutoCAD ACI 1–255 model-space palette coverage.
- Adaptive screen-space arc tessellation with a 0.25 px default error tolerance.
- CAD color and transformed-arc regression fixtures/tests.

### Fixed
- ACI colors above index 7 no longer collapse to a single fallback grey.
- TrueColor, ByLayer and nested ByBlock color semantics are preserved through scene translation.
- Layer 0 entities inside nested blocks inherit the effective insert-layer color for ByLayer rendering.
- ACI 7 adapts to light/dark viewer canvas backgrounds without rewriting explicit white/black colors.
- Circular arcs no longer use a fixed ~10-degree line segmentation and now refine with screen zoom/transforms.

### Migration
- Initial extraction from `KiYouJyo/SpatialViewer` keeps existing namespaces/API shapes where practical to reduce integration risk.
