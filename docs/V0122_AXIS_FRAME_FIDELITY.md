# v0.12.2 Axis / sheet fidelity

This patch is driven by real architectural drawing comparison after v0.12.1.

## Scope

- Standard AutoCAD DIMENSION presentation used around architectural grids:
  - preserve built-in oblique / architectural-tick arrow identity as actual scene geometry instead of metadata-only fallback;
  - keep dimension text readable when the stored rotation is equivalent to an upside-down orientation.
- Do not infer proprietary Tianzheng `TCH_AXIS_LABEL`, index, or dimension raw fields.
- Sheet / title-frame visibility is a host scene-selection concern when Paper Space layouts already exist in `CadDocument.Layouts`; the product host must render a layout scene rather than always forcing model `Document.Scene`.

## Non-claims

- Arbitrary custom DIMBLK geometry is not reverse engineered. Unknown custom arrow blocks retain the existing conservative generic-arrow fallback.
- `TCH_AXIS_LABEL`, `TCH_DRAWINGINDEX`, `TCH_INDEXPOINTER`, `TCH_DIMENSION2`, and modern `TCH_DIMENSION` remain under the existing evidence policy.
- Xrefs are not followed implicitly by CadCore.
