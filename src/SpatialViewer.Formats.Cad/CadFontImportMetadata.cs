namespace SpatialViewer.Formats.Cad;

/// <summary>Stable metadata keys used by CAD import hosts without coupling the core to a particular desktop application.</summary>
public static class CadFontImportMetadata
{
    /// <summary>
    /// Optional import metadata value containing additional directories in which SHX files may be resolved.
    /// Directories are separated with <see cref="Path.PathSeparator"/>. The drawing directory is always searched first.
    /// CadCore deliberately does not scan the Windows registry or AutoCAD installation directories on its own.
    /// </summary>
    public const string ShxSearchPaths = "Cad.ShxSearchPaths";
}
