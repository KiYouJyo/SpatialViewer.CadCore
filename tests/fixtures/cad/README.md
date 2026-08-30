# CAD fixtures

These minimal fixtures were migrated from `KiYouJyo/SpatialViewer` together with the CAD kernel. They are intentionally small and deterministic.

- `dxf/mixed-basic.dxf`: layers, line/circle/arc/ellipse/polyline, text/MTEXT, nested blocks and one unsupported HATCH.
- `dxf/large-coordinate.dxf`: double-precision coordinate regression.
- `negative/invalid.dxf`: malformed numeric content.
- `negative/missing-block.dxf`: invalid block reference diagnostics.

DWG coverage is generated from the DXF fixtures during tests through ACadSharp.
