# Xiangyuan parcel semantic evidence gate

> Status: P1 research protocol. This gate names **experiment intent only**. It does not publish any Xiangyuan proprietary DXF group, DWG byte offset, class name, proxy primitive, or relationship as a parcel semantic until the complete evidence chain below is satisfied.

## Purpose

The Xiangyuan compatibility line now distinguishes three levels of evidence:

1. **Discovery provenance** — a custom class/profile is observed in a known-Xiangyuan drawing and may disappear after a controlled ordinary-CAD conversion.
2. **Repeatable candidate provenance** — the same exact unknown class/profile disappears in at least two independent native→converted pairs with no retained/added contradiction.
3. **Named parcel semantic** — a controlled single-variable parcel experiment repeatedly identifies the same structural slot/range and is independently verified before Reader mapping is added.

Level 1 or 2 never implies level 3.

## Canonical P1 experiment cases

Raw-payload value experiments:

- `PARCEL_NUMBER` — displayed parcel number / parcel identifier;
- `LAND_USE_CODE` — land-use classification code;
- `LAND_USE_NATURE` — displayed land-use nature/designation;
- `FAR_MIN` / `FAR_MAX` — floor-area-ratio bounds;
- `BUILDING_DENSITY_MIN` / `BUILDING_DENSITY_MAX` — building-density bounds;
- `GREEN_RATE_MIN` / `GREEN_RATE_MAX` — green-rate bounds;
- `HEIGHT_MIN` / `HEIGHT_MAX` — building-height bounds.

Geometry/derived/relationship experiments:

- `AREA` — parcel area / derived area output; do not assume it can be independently edited while boundary geometry is unchanged;
- `BOUNDARY` — parcel boundary geometry while non-geometric attributes are held constant;
- `CONTROL_INDICATOR_RELATIONSHIP` — parcel-to-control-indicator object relationship.

The geometry/relationship cases are deliberately rejected by the raw DXF/DWG value-consensus API. A boundary or relationship must be researched through geometry/reference evidence instead of forcing an anonymous raw-value slot to carry that meaning.

`AREA` and `BOUNDARY` now have a dedicated proxy-geometry evidence path. `CadProxyGeometryDiffer` compares equal-layout proxy trees in memory and emits only anonymous locations such as primitive path + point/field index. Coordinates, text values and other source values are not returned. `CadProxyGeometryExperimentAnalyzer` requires at least two independent equal-layout observations before a changed geometry slot becomes repeatable evidence. A primitive/vertex-count change is `LayoutMismatch` and fails closed rather than being coerced into a coordinate mapping. `CONTROL_INDICATOR_RELATIONSHIP` remains outside this path and still requires object-reference evidence.

`CONTROL_INDICATOR_RELATIONSHIP` now has a separate privacy-safe object-reference path. `CadCustomHandleReferenceDiffer` compares retained reference slots only when both sides have the same custom-object identity and the same ordered reference group-code layout. It uses target handles only for in-memory equality and emits only `GroupCode + CodeOccurrence`; source/target handles are never returned. Missing references or a changed reference-code/count layout fail closed. `CadCustomHandleReferenceExperimentAnalyzer` requires at least two independent comparable observations before a reference slot becomes repeatable evidence. A stable `330#1` (for example) is still anonymous reference evidence, **not** proof that the target is a control-indicator block; target type/role requires independent real-sample verification.

The next endpoint-evidence layer resolves only an already-stable changed reference slot against entities inside each local document. `CadCustomReferenceEndpointExperimentAnalyzer` never retains source or target handles. For ordinary CAD targets it records only a coarse entity kind such as `BlockReference`, `Text`, `Polyline`, or `Line`; block names, text, layers, coordinates and metadata remain private. For custom-object targets it may retain the CLASSES structural identity (`DXF/C++/Application + current vendor classification`). An endpoint observation is comparable only when both before/after targets resolve and have the same structural descriptor. At least two independent observations must agree on the same source identity, anonymous reference slot and target descriptor before endpoint structure becomes repeatable evidence. Even a repeatable `330#1 -> BlockReference` remains **endpoint-type evidence**, not proof that the block is a Xiangyuan control-indicator object.

## Required gate before a named semantic mapper

A proposed parcel property mapping is accepted only when all of the following are true:

1. The object identity is either explicitly Xiangyuan by the conservative global classifier **or** a repeated unknown conversion candidate.
2. The experiment uses one canonical case ID and changes only that declared parameter.
3. At least two independent A/B observations have the same exact class identity and compatible schema/capture framing.
4. DXF experiments yield one or more stable anonymous group slots, or DWG experiments yield stable anonymous byte ranges.
5. No other canonical parcel case has been shown to produce the same candidate mapping under a conflicting interpretation.
6. Independent evidence verifies the intended semantic (for example Xiangyuan UI/property output or GIS/attribute export), without importing private drawing values into the repository.
7. A Reader-level regression fixture proves the mapping against anonymized/synthetic evidence derived from the verified structure.
8. The parser fails closed when the expected identity/schema/framing is absent.

Only after all eight conditions are satisfied may a property receive a named reader-independent semantic.

## Privacy boundary

Shareable evidence may include:

- CLASSES identity;
- structural schema fingerprints;
- DXF group index/code/occurrence;
- DWG changed byte ranges and capture method;
- proxy primitive kind signatures;
- aggregate sample/observation counts.

Shareable evidence must not include:

- source drawing paths/names;
- entity handles or target handles;
- coordinates;
- parcel numbers, land-use values, indicator values, notes, labels, or other raw before/after values;
- raw DWG bytes or object-section offsets.

## Current release consequence

This protocol does not change the product release from v0.12.6 and does not declare Xiangyuan P1 semantic support complete. v0.13.0 remains gated on real Xiangyuan sample evidence plus Reader/display regression.

## Whole-document A/B matching

`CadXiangyuanDocumentPairEvidenceAnalyzer` allows controlled experiments to operate on two complete copies of the same drawing instead of manually extracting one custom entity.

The matcher is intentionally strict:

- only the exact retained non-empty CAD handle may pair an entity across the two documents;
- custom handles must be globally unique across model space, block definitions and paper-space layouts;
- a same-handle class-identity change is recorded as an identity mismatch and produces no payload/geometry/reference evidence;
- empty handles and missing counterparts remain unmatched;
- geometry, layer, text, coordinates, block membership and content similarity are **never** used as fallback matching heuristics.

For each exact same-handle/same-identity pair, the analyzer can collect privacy-safe changed observations from all evidence already implemented: raw DXF slots, bounded DWG byte ranges, proxy-geometry positions and object-reference slots. Only changed comparable observations are serialized. Source drawing names/paths, entity handles, raw values, coordinates, target handles and raw DWG bytes are absent from the report.

Two provenance modes remain separate: explicit Xiangyuan identity, or one exact repeated-removal Unknown conversion candidate. Serialized reports re-check these identity rules so hand-edited JSON cannot promote an Unknown class into explicit Xiangyuan evidence.
