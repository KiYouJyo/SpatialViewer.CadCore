# Changelog

All notable CadCore changes are recorded here.

## Unreleased

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
