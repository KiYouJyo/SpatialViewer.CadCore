# Changelog

All notable CadCore changes are recorded here.

## Unreleased

## 0.5.0 - 2026-08-31

### Added
- Added reader-independent `CadDimensionEntity` semantics for linear, aligned, angular, radius, diameter, ordinate and arc-length dimension families, retaining measurement text, definition/text points, style information and subtype-specific reference points instead of treating anonymous dimension blocks as opaque pictures.
- Added semantic `CadLeaderEntity`, `CadLeaderPath` and `CadMultiLeaderEntity` models that preserve classic LEADER vertices, linked-annotation identity, MLEADER multi-path geometry, doglegs, embedded text, content type and arrow sizing before scene translation.
- Added annotation scene translation for dimension/extension lines, radial and angular geometry, simple arrowheads, classic leader paths, multiple MLEADER paths, doglegs and embedded MLEADER text while retaining semantic metadata for later fidelity work.
- Added pickable/hit-testable annotation geometry and focused semantic/scene regression tests for linear and angular dimensions, LEADER and multi-path MLEADER.
- Added ACadSharp-writer-backed end-to-end DXF coverage for DIMENSION, LEADER and MLEADER, plus DXF-to-DWG round-trip coverage for DIMENSION and LEADER through the same CadCore import pipeline.

### Fidelity
- Angular dimensions keep analytic `ArcGeometry` where the semantic construction exposes an arc rather than permanently flattening the annotation to line segments.
- Dimension values and linked/embedded leader text remain semantic data, allowing later text-engine and DIMSTYLE fidelity upgrades without reworking the reader boundary.
- MLEADER leader roots, connection points and landing/dogleg geometry remain distinct paths instead of being collapsed into one arbitrary polyline.

### Compatibility
- All v0.5.0 model and translator changes are additive; the stable CLR ABI remains `1.0.0.0` while the product/file version advances to `0.5.0`.
- ACadSharp 3.7.1 has a known DWG MLEADER self-round-trip asymmetry: its writer emits the block-label count where its reader first expects an arrowhead count, so ACadSharp-generated MLEADER DWG fixtures are deliberately tracked as an upstream failure instead of being used to claim false CadCore DWG round-trip coverage. Real-reader MLEADER semantics are validated through DXF and the format-neutral adapter/scene pipeline.
- v0.5.0 intentionally does not claim full AutoCAD DIMSTYLE fidelity, custom arrow-block rendering, tolerance/alternate-unit layout, every associative annotation relationship, or exact MLEADER content framing. Those remain refinement work rather than being approximated as complete support.
- Paper Space/Layout/Viewport support remains scheduled for v0.6.0; SHX/text fidelity remains scheduled for v0.7.0.

## 0.4.0 - 2026-08-31

### Added
- Added reader-independent `CadSplineDefinition` / `CadSplineEntity` semantics that retain degree, control points, knot vector, weights, fit points, closed state and periodic state before scene approximation.
- Added HATCH semantic records for line, arc, ellipse, polyline/bulge and spline boundary edges plus compound hatch loops.
- Added `CompoundPathGeometry` with even-odd fill semantics so solid hatches preserve inner holes instead of filling every loop independently.
- Added `CadAttributeEntity` plus additive `CadBlockReferenceEntity.Attributes` support for ATTDEF/ATTRIB block metadata and instance values.
- Added deterministic NURBS and hatch-boundary tessellation at the CAD-to-Scene boundary, keeping reader import semantic-first.
- Added Win2D and deterministic software-renderer support for compound even-odd hatch fills.
- Added `entity-coverage-v040.dxf` and focused semantic, scene, hit-test and golden-render regression coverage.

### Fixed
- Variable ATTDEF text is suppressed when the corresponding INSERT carries an ATTRIB with the same tag, while constant ATTDEF content remains visible.
- INSERT attributes keep their reader-resolved world placement and are not transformed a second time by the parent block transform.
- Compound hatch hit testing now observes even-odd hole parity and still accepts boundary hits.

### Compatibility
- All new public model/geometry shapes are additive; the stable CLR ABI remains `1.0.0.0` and the product version advances independently to `0.4.0`.
- Patterned and gradient HATCH rendering is intentionally not claimed in this release: non-solid hatches retain pattern metadata and boundary geometry without pretending to be solid fills.
- DIMENSION, LEADER/MLEADER and Paper Space/Layout support remain scheduled for later entity-coverage milestones.

## 0.3.2 - 2026-08-31

### Fixed
- Arc hit testing now respects the analytic arc start/sweep range instead of accepting any point on the parent circle.
- Hit-test tolerance is converted from world space into local entity space through the inverse affine transform, fixing misses on compressed/scaled block contents.
- Degenerate ellipses now fall back to line/point hit testing instead of dividing by zero.

### Performance
- `Scene2D` now flattens immutable node transforms and bounds once at construction time and reuses the cached scene items for enumeration, bounds queries, and reverse-order hit testing while preserving live layer visibility.
- Added a dependency-free 100,000-item core benchmark covering scene construction, visible enumeration, bounds queries, and hit testing as the baseline for later spatial-index work.

### Compatibility
- Public API shapes remain compatible with 0.3.1 and the stable CLR ABI remains `1.0.0.0`.
- This is the final 0.3.x hardening pass before entity-coverage work begins in 0.4.0.

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
