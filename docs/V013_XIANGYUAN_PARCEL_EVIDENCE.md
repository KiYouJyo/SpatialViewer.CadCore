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
