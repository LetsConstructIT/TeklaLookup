using System;
using System.Globalization;
using System.Windows.Data;
using TeklaLookup.App.Models;
using Wpf.Ui.Controls;

namespace TeklaLookup.App.Services;

/// <summary>
/// Maps a <see cref="PropertyEntry"/> to a glanceable category glyph. Errors and nulls win over
/// the category bucket so a malformed row is obvious without having to read the Value column.
/// </summary>
public sealed class PropertyEntryToIconConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not PropertyEntry entry)
            return SymbolRegular.Tag20;

        // Pinned rows deliberately keep their own kind icon rather than a star: the star already
        // has its own column, and a mixed "Pinned" group is easier to read when the glyphs still
        // say what each row is.
        if (!string.IsNullOrEmpty(entry.Value) && entry.Value!.StartsWith("<error:", StringComparison.Ordinal))
            return SymbolRegular.Warning20;
        if (entry.RawValue is null)
            return SymbolRegular.Subtract20;

        // Related-object template attributes carry a "· GROUP" suffix (e.g. "Template Attributes ·
        // NUT"), so the template bucket is matched by prefix rather than equality.
        if (entry.Category.StartsWith(PropertyCategories.TemplateAttributes, StringComparison.Ordinal))
            return SymbolRegular.ClipboardTextLtr20;

        return entry.Category switch
        {
            PropertyCategories.Properties     => SymbolRegular.Tag20,
            PropertyCategories.Extensions     => SymbolRegular.Sparkle20,
            PropertyCategories.UserProperties => SymbolRegular.PersonNote20,
            PropertyCategories.Items          => SymbolRegular.List20,
            _                                 => SymbolRegular.Tag20,
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Maps a <see cref="TeklaObjectSnapshot"/> to a domain glyph, so a long mixed list is scannable
/// at a glance (parts vs. assemblies vs. welds vs. drawings).
/// </summary>
public sealed class SnapshotTypeToIconConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not TeklaObjectSnapshot snapshot)
            return SymbolRegular.Cube20;

        var typeName = snapshot.Source?.GetType().Name ?? snapshot.TypeName ?? string.Empty;

        // Subtype-decorated names look like "Beam · COLUMN" — match on the leading type only.
        var separatorIdx = typeName.IndexOf('·');
        if (separatorIdx > 0)
            typeName = typeName.Substring(0, separatorIdx).Trim();

        return typeName switch
        {
            "Beam" or "PolyBeam"
                => SymbolRegular.Cube20,
            "ContourPlate"
                => SymbolRegular.RectangleLandscape20,
            "Assembly" or "CastUnit" or "BaseAssembly"
                => SymbolRegular.Stack20,
            "BoltGroup" or "BoltArray" or "BoltCircle" or "BoltXYList"
                => SymbolRegular.Diamond20,
            "BaseWeld" or "Weld" or "PolygonWeld"
                => SymbolRegular.Connector20,
            "Reinforcement" or "RebarGroup" or "SingleRebar" or "RebarSet" or "BaseRebarGroup"
                => SymbolRegular.LayerDiagonal20,
            "Component" or "BaseComponent" or "Connection" or "Detail" or "Seam"
                => SymbolRegular.PuzzlePiece20,
            "ReferenceModel" or "ReferenceModelObject"
                => SymbolRegular.DocumentLink20,
            "Drawing" or "AssemblyDrawing" or "SinglePartDrawing" or "CastUnitDrawing" or "GADrawing" or "MultiDrawing"
                => SymbolRegular.DocumentBulletList20,
            "DimensionBase" or "StraightDimension" or "StraightDimensionSet" or "RadiusDimension" or "AngleDimension" or "CurvedDimensionBase"
                => SymbolRegular.Form20,
            "Mark" or "Text" or "Symbol"
                => SymbolRegular.TextDescription20,
            "ViewBase" or "View"
                => SymbolRegular.Layer20,
            _ => SymbolRegular.Cube20,
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
