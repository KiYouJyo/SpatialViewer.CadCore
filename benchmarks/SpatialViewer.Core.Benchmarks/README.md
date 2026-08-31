# SpatialViewer.Core performance baseline

This lightweight benchmark records the pre-spatial-index cost of the immutable `Scene2D` pipeline without introducing a benchmark-framework dependency into CadCore CI.

Run from the repository root:

```powershell
dotnet run -c Release --project benchmarks/SpatialViewer.Core.Benchmarks/SpatialViewer.Core.Benchmarks.csproj -- 100000
```

The default scenario creates 100,000 line entities and reports:

- scene construction / one-time flattening;
- repeated visible-item enumeration;
- repeated scene bounds queries;
- reverse-order hit testing.

The benchmark is intentionally informational in v0.3.2: no wall-clock threshold is enforced in CI because GitHub-hosted runner timing is noisy. Future large-drawing work should compare against this scenario and add indexed viewport-query measurements rather than silently changing the workload.
