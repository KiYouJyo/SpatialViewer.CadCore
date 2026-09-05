# Changelog

All notable CadCore changes are recorded here.

## Unreleased

## 0.12.10 - 2026-09-05

### Added
- Added a privacy-safe raw ObjectARX Proxy Graphics command inventory that scans each retained DWG proxy stream only for record framing (`type id`, `record size`, sequence/occurrence and whether ACadSharp 3.7.1 knows the command).
- Added per-custom-entity aggregate metadata for raw proxy command signatures and unknown command IDs/counts.
- Added a document-level `CAD_PROXY_GRAPHICS_UNKNOWN_COMMANDS` diagnostic when raw proxy streams contain command types the current ACadSharp reader does not implement.
- Added a reflection bridge over ACadSharp entity templates so command framing can be inspected after the normal reader completes without replacing DWG parsing.

### Privacy and evidence boundary
- Raw proxy payload bytes are never retained by the command inventory and are not emitted in diagnostics.
- The inventory does not export drawing paths/names, handles, coordinates, colors, text, property values or proprietary payload contents.
- Unknown command IDs remain anonymous structural evidence. v0.12.10 does not guess whether type 39+, 51 or any other ID represents polyPolygon, solid fill, gradient fill, hatch grouping or a Xiangyuan-specific command.

### Real-device motivation
- SpatialViewer v0.3.10 still showed the real Xiangyuan land-use plan as outlines only after standard ProxyPolygon, Mesh/Shell face and FillOn paths were restored. v0.12.10 is therefore a diagnostic release intended to identify commands that may be discarded inside the upstream proxy reader before CadCore sees them.

### Compatibility
- CLR ABI remains `1.0.0.0`.
- Host Contract remains `SpatialViewer.CadHost >=1.0.0,<2.0.0`.
- Release manifest schema remains `2`.

## 0.12.9 - 2026-09-04

### Fixed
- Restored explicit ObjectARX fill-state handling from `ProxySubentFillon`.
- Added strict planar `ProxyMesh` / `ProxyShell` face preservation using face color, layer, marker and visibility evidence while retaining the original edge fallback.
- Filled proxy surfaces now render face polygons first and their original edge evidence on top; malformed/non-planar face data falls back to edge-only rather than inventing fill geometry.
- Explicit fill-off now suppresses `ProxyPolygon` fill while polyline primitives remain non-fillable.
- Proxy surface faces participate in privacy-safe geometry layout/diff evidence and remain transform/clip aware.

### Real-device regression
- Addresses the v0.3.9 Xiangyuan acceptance result where land-use blocks remained outline-only after generic `ProxyPolygon` fill was restored. Investigation showed that ACadSharp already parses proxy fill state and mesh/shell face traits, while CadCore was discarding the entire face layer and preserving edges only.

### Compatibility
- CLR ABI remains `1.0.0.0`.
- Host Contract remains `SpatialViewer.CadHost >=1.0.0,<2.0.0`.
- Release manifest schema remains `2`.

## 0.12.8 - 2026-09-04

### Fixed
- Restored ObjectARX `ProxyPolygon` fill semantics in the reader-independent scene path. Proxy polygons now use the effective proxy color as both stroke and fill instead of being rendered as outline-only polygons.
- Primitive ACI/TrueColor overrides now propagate to the proxy polygon fill as well as its stroke.
- Kept `ProxyPolyline`, closed `ProxyLwPolyline`, circles/arcs and mesh/shell edge fallback unfilled, avoiding accidental flood-fill of ordinary linework.

### Real-device regression
- Fixes the SpatialViewer v0.3.8 / CadCore v0.12.7 Xiangyuan acceptance case where land-use parcel boundaries and legend colors were visible but the main land-use color blocks were missing.
- The fix restores generic ObjectARX polygon display semantics only. It does not infer that every proxy polygon is a Xiangyuan parcel and does not add proprietary parcel semantics.

### Compatibility
- CLR ABI remains `1.0.0.0`.
- Host Contract remains `SpatialViewer.CadHost >=1.0.0,<2.0.0`.
- Release manifest schema remains `2`.

## 0.12.7 - 2026-09-04

### Added
- Added the Xiangyuan Control Planning compatibility research pipeline from conservative vendor discovery through native-vs-converted diff, multi-pair repeated-candidate consensus and case-bound parcel experiments.
- Added privacy-safe DXF/DWG value evidence, proxy-geometry evidence, anonymous object-reference evidence and reference-endpoint structural evidence for Xiangyuan parcel research.
- Added strict whole-document A/B matching across model space, blocks and paper space using only the same unique retained handle plus exact class identity, with no geometric/content fallback pairing.
- Added local whole-document A/B CLI modes for explicitly classified Xiangyuan objects and validated repeated-Unknown conversion candidates.

### Safety and compatibility
- Unknown Xiangyuan candidate classes remain globally `Unknown`; v0.12.7 does not guess proprietary class names or promote candidates to Xiangyuan without independent evidence.
- Parcel-number, land-use, FAR, density, green-rate and height experiment IDs name operator intent only; no proprietary raw-field semantic mapping is claimed by this release.
- AREA/BOUNDARY use geometry evidence and control-indicator relationships use anonymous reference/endpoint evidence; unsupported or structurally inconsistent inputs fail closed.
- Privacy-safe reports exclude source drawing paths/names, entity and target handles, coordinates, raw property values and raw DWG bytes.
- CLR ABI remains `1.0.0.0`; host contract remains `SpatialViewer.CadHost >=1.0.0,<2.0.0`; release manifest schema remains `2`.

### Acceptance scope
- v0.12.7 is an **on-device Xiangyuan compatibility acceptance build**. It makes the complete evidence and fallback pipeline available to SpatialViewer for real drawing verification.
- It does not claim that native Xiangyuan parcel semantics are complete; v0.13.0 remains gated on real Xiangyuan sample evidence plus Reader/display regression.

### Added
- Added conservative Xiangyuan Control Planning custom-object vendor classification from explicit application/C++ identities, without guessing DXF class names from public command prefixes.
- Added Xiangyuan class/entity detection metadata while reusing the existing reader-independent custom-object, raw DXF/DWG evidence and ObjectARX Proxy Graphics fallback pipeline.
- Added reader-level regression coverage and a Xiangyuan compatibility roadmap for parcel, atlas/street-block and utility-object follow-up work.
- Added a privacy-safe Xiangyuan schema corpus with deterministic structural clustering, proxy/opaque coverage, raw-evidence coverage, generic relationship coverage, JSON validation and multi-sample merge support.
- Added a Xiangyuan-only privacy-safe A/B experiment gate over the generic DXF/DWG differ and repeatability-consensus pipeline, rejecting non-Xiangyuan observations before proprietary-field candidates can be produced.
- Added a local Xiangyuan corpus CLI that imports one or more DWG/DXF samples and emits only mergeable privacy-safe JSON, plus CI smoke coverage for source-name redaction and false-positive rejection.
- Added known-Xiangyuan discovery mode that inventories every application-defined CLASSES identity and custom-object structural profile while preserving `Unknown` vendor status instead of guessing proprietary class names.
- Added a privacy-safe native-vs-converted Xiangyuan diff that highlights custom classes/profiles removed by controlled all-explode/result-output conversion without promoting those candidates to vendor or parcel semantics.
- Added conversion-candidate-scoped A/B experiments for removed unknown entity classes, with exact CLASSES identity matching and a fresh global-classifier check so candidate research cannot silently promote unknown objects to Xiangyuan.
- Added multi-pair Xiangyuan conversion consensus with validated privacy-safe JSON and CLI support; repeated unknown candidates require at least two independent removals and no retained/added contradiction before stronger A/B research.
- Added a canonical P1 Xiangyuan parcel experiment catalog and case-bound DXF/DWG consensus gate for parcel number, land-use identity, area, FAR/density/green-rate/height ranges, while keeping boundary and control-indicator relationships outside raw-value semantic inference.
- Added privacy-safe proxy-geometry diff/repeatability consensus and case-bound Xiangyuan AREA/BOUNDARY research, reporting only anonymous primitive paths and changed geometry slots while failing closed on proxy-layout changes or missing proxy graphics.
- Added privacy-safe custom-object handle-reference diff/repeatability consensus and case-bound Xiangyuan control-indicator relationship research, exposing only reference group code/occurrence while never exporting source or target handles.
- Added privacy-safe reference-endpoint structure evidence that resolves stable anonymous reference slots locally and retains only coarse target entity kind or custom CLASSES identity, excluding block names, text, coordinates, layers and handles.
- Added strict privacy-safe whole-document Xiangyuan A/B matching by unique retained handle and exact class identity, collecting only changed DXF/DWG/proxy-geometry/reference structural evidence with no geometric/content pairing fallback.
- Added local whole-document A/B CLI modes for explicit Xiangyuan objects and deterministic repeated-Unknown conversion candidates, with candidate provenance loaded from validated conversion-consensus JSON rather than user-supplied class names.

## 0.7.0 - 2026-09-01

### Added
- Added reader-independent CAD text-presentation semantics for TEXT, MTEXT and ATTRIB/ATTDEF, preserving text style name, font and big-font filenames, alignment point, horizontal/vertical justification, width factor, oblique angle, mirror flags, MTEXT attachment point, layout width and line spacing.
- Extended `TextGeometry` with reusable text-layout metadata for font family, width factor, oblique angle, layout width, multiline state, horizontal/vertical alignment and X/Y mirroring while preserving the existing constructor surface.
- Added a CAD font resolver that recognizes TrueType font filenames and SHX/shape-font references, retains the original CAD font identity in scene metadata and selects a deterministic Windows fallback when an SHX glyph engine is unavailable.
- Added CAD text normalization for common AutoCAD control sequences used by the current viewing path, including paragraph breaks, non-breaking spaces and legacy %% symbol escapes.
- Added Win2D text rendering support for alignment anchors, width scaling, oblique shear, mirrored text and the resolved font family while continuing to derive text scale/rotation from the complete scene transform.
- Added ACadSharp-writer-backed TEXT/MTEXT DXF round-trip regression coverage through Writer → Reader → reader-independent CAD semantics → Scene, including SHX style identity, justification, mirror, oblique, width factor, MTEXT attachment/layout width/line spacing and CJK TrueType fallback mapping.

### Fixed
- Center/right and vertical TEXT justification now uses the CAD alignment point instead of always rendering from the insertion point.
- MTEXT bottom/middle/right attachment modes now anchor the scene geometry consistently instead of behaving as top-left text.
- TEXT width factor, style width, oblique angle and mirror flags are no longer discarded at the ACadSharp adapter boundary.
- ATTRIB/ATTDEF text now shares the same presentation pipeline as ordinary TEXT, retaining style/alignment semantics without reintroducing block double-transform behavior.

### Compatibility
- v0.7.0 remains additive and retains stable CLR ABI `1.0.0.0`; product/file version advances independently to `0.7.0` for SpatialViewer 0.2.x kernel updates.
- SHX filenames and shape-font identity are preserved and surfaced as explicit fallback metadata, but v0.7.0 does not claim a complete AutoCAD SHX vector-glyph interpreter. When a native TrueType equivalent is not available, Win2D renders with a deterministic fallback font rather than pretending that glyph outlines are exact.
- MTEXT inline formatting is normalized for the viewing path rather than claimed as full AutoCAD rich-text fidelity; columns, stacked fractions, per-span font/color overrides and every control code remain later refinement work.
- Large-drawing spatial indexing remains scheduled for v0.8.0.

## 0.6.0 - 2026-09-01

### Added
- Added reader-independent `CadLayoutDefinition` and `CadViewportDefinition` semantics for Model/Paper Space, layout order, paper size, limits/extents, paper-space entities, viewport paper geometry, model view center/target/height, twist, frozen layers, active state and clipping-boundary identity.
- Added `CadDocument.Layouts` and `GetLayoutScene(...)` while leaving the existing model-space `Scene` path unchanged for existing callers.
- Added paper-space scene composition that keeps native paper entities separate from model entities projected through each active viewport, including viewport scale/twist transforms and per-viewport frozen-layer filtering.
- Added inherited scene clip bounds through `SceneNode`, `SceneItem`, `RenderCommand`, hit testing and the Win2D backend so rectangular viewport clipping is enforced consistently for display and picking instead of being metadata-only.
- Added viewport-boundary scene geometry and metadata without allowing the boundary frame to steal picks from model geometry inside the viewport.
- Added ACadSharp adapter coverage for `CadDocument.Layouts`, associated paper-space block entities, `Viewport` geometry/view properties, frozen layers and supported polyline clipping-boundary data.
- Added ACadSharp-writer-backed end-to-end Layout/Paper Space/Viewport DXF regression coverage through Writer → Reader → reader-independent CAD model → layout Scene → clipping/render preparation.

### Fixed
- Projected scene-item bounds now represent the actual projected geometry intersected with inherited viewport clipping rather than incorrectly expanding to the entire viewport rectangle.
- Hit testing rejects geometry outside an inherited viewport clip and preserves expected model-entity selection inside the viewport.
- Viewport frames are ordered below ordinary paper/model content so a visible frame does not mask CAD entity selection.

### Compatibility
- v0.6.0 is additive at the CAD/layout API boundary and retains stable CLR ABI `1.0.0.0`; product/file version advances independently to `0.6.0` for SpatialViewer 0.2.x kernel updates.
- Model-space behavior remains the default `CadDocument.Scene`; paper layouts are opt-in through `GetLayoutScene(...)`, reducing regression risk for existing SpatialViewer integration.
- This release deliberately targets the 2D CAD viewing path. Non-rectangular viewport boundary semantics are preserved when ACadSharp exposes a supported polyline boundary, but exact polygonal clipping is not claimed yet; rectangular paper bounds remain the enforced render/hit-test clip in v0.6.0. Arbitrary 3D viewport direction/perspective projection is likewise not claimed as 2D support.
- SHX/text fidelity remains scheduled for v0.7.0; large-drawing spatial indexing remains scheduled for v0.8.0.

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
