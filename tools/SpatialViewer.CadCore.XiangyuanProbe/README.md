# Xiangyuan privacy-safe corpus probe

This local-only helper imports one or more Xiangyuan-generated DWG/DXF files through CadCore and writes a mergeable `CadXiangyuanSchemaCorpus` JSON report.

## Usage

```powershell
dotnet run --project tools/SpatialViewer.CadCore.XiangyuanProbe -c Release -- `
  --out .\\xiangyuan-corpus.json `
  .\\sample-a.dwg .\\sample-b.dwg
```

The JSON report is designed for sharing and comparison. It contains structural class/schema identities and aggregate compatibility coverage only. It does **not** include:

- source paths or drawing names;
- entity handles;
- coordinates or text/attribute values;
- raw DXF value lines;
- raw DWG object bytes or object-section offsets.

The console reports input ordinal numbers and diagnostic codes rather than source paths.

A profile or changed structure is evidence for compatibility research only. It is not a native parcel/control-index semantic claim.

## Discovery mode for known Xiangyuan drawings

If a drawing is already known to have been produced by Xiangyuan but the real CLASSES identities are not yet recognized by CadCore, use discovery mode:

```powershell
dotnet run --project tools/SpatialViewer.CadCore.XiangyuanProbe -c Release -- `
  --discovery `
  --out .\xiangyuan-discovery.json `
  .\known-xiangyuan-sample.dwg
```

Discovery mode inventories **all** application-defined CLASSES identities and custom-entity structural profiles in the supplied sample. Each entry keeps the normal `ClassifiedVendor` result; an unrecognized class remains `Unknown`. Inclusion in a known-Xiangyuan discovery report is a research lead only and never promotes that class to Xiangyuan support.

This mode is intentionally broader than the default strict corpus so it can reveal real class/application identities that do not contain `LzxSoft`, `Xiangyuan`, or `湘源`.
