using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using TungDiroot.Core.Excel;

namespace TungDiroot.Core.Links;

public sealed record SourceFingerprint(long Length, long LastWriteUtcTicks, string Sha256Hex)
{
    public static SourceFingerprint Create(string sourcePath, string worksheetName, ExcelRangeReference range, WorkbookRangeModel importedRegion)
    {
        if (sourcePath == null)
        {
            throw new ArgumentNullException(nameof(sourcePath));
        }

        return Create(new FileInfo(sourcePath), worksheetName, range, importedRegion);
    }

    public static SourceFingerprint Create(FileInfo sourceFile, string worksheetName, ExcelRangeReference range, WorkbookRangeModel importedRegion)
    {
        if (sourceFile == null)
        {
            throw new ArgumentNullException(nameof(sourceFile));
        }

        sourceFile.Refresh();
        return Create(sourceFile.Length, sourceFile.LastWriteTimeUtc.Ticks, worksheetName, range, importedRegion);
    }

    public static SourceFingerprint Create(long length, long lastWriteUtcTicks, string worksheetName, ExcelRangeReference range, WorkbookRangeModel importedRegion)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        if (lastWriteUtcTicks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lastWriteUtcTicks));
        }

        if (worksheetName == null)
        {
            throw new ArgumentNullException(nameof(worksheetName));
        }

        if (range == null)
        {
            throw new ArgumentNullException(nameof(range));
        }

        if (importedRegion == null)
        {
            throw new ArgumentNullException(nameof(importedRegion));
        }

        var normalizedRange = NormalizeRange(range);
        var canonical = new StringBuilder();
        AppendValue(canonical, "worksheet", worksheetName);
        AppendValue(canonical, "range", normalizedRange.ToA1());

        foreach (var cell in importedRegion.Cells
            .Where(cell => Contains(normalizedRange, cell.Address))
            .OrderBy(cell => cell.Address.Row)
            .ThenBy(cell => cell.Address.Column))
        {
            AppendValue(canonical, "cell.address", cell.Address.ToA1());
            AppendValue(canonical, "cell.display", cell.DisplayText);
            AppendStyle(canonical, cell.Style);
        }

        foreach (var merge in importedRegion.MergedRanges
            .Select(NormalizeRange)
            .Where(merge => Intersects(normalizedRange, merge))
            .Select(merge => Clip(normalizedRange, merge).ToA1())
            .OrderBy(value => value, StringComparer.Ordinal))
        {
            AppendValue(canonical, "merge", merge);
        }

        foreach (var relationshipId in importedRegion.Drawings.Items
            .Where(item => IsInRange(item.Anchor, normalizedRange))
            .Select(item => item.RelationshipId)
            .Where(relationshipId => !string.IsNullOrEmpty(relationshipId))
            .Select(relationshipId => relationshipId!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(relationshipId => relationshipId, StringComparer.Ordinal))
        {
            AppendValue(canonical, "drawing.relationshipId", relationshipId);
        }

        return new SourceFingerprint(length, lastWriteUtcTicks, ComputeSha256(canonical.ToString()));
    }

    public static SourceFingerprint Parse(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        var parts = value.Split(':');
        if (parts.Length != 3 ||
            !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var length) || length < 0 ||
            !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var lastWriteUtcTicks) || lastWriteUtcTicks < 0 ||
            parts[2].Length != 64 || !parts[2].All(IsHexadecimal))
        {
            throw new ArgumentException("The value is not a valid source fingerprint.", nameof(value));
        }

        return new SourceFingerprint(length, lastWriteUtcTicks, parts[2].ToUpperInvariant());
    }

    public override string ToString()
    {
        return Length.ToString(CultureInfo.InvariantCulture) + ":" +
            LastWriteUtcTicks.ToString(CultureInfo.InvariantCulture) + ":" +
            Sha256Hex;
    }

    private static void AppendStyle(StringBuilder canonical, CellStyleModel style)
    {
        if (style == null)
        {
            throw new ArgumentException("A cell style is required.", nameof(style));
        }

        AppendValue(canonical, "style.font.name", style.Font.Name);
        AppendValue(canonical, "style.font.size", style.Font.SizePoints.ToString("R", CultureInfo.InvariantCulture));
        AppendValue(canonical, "style.font.bold", style.Font.Bold ? "1" : "0");
        AppendValue(canonical, "style.font.italic", style.Font.Italic ? "1" : "0");
        AppendValue(canonical, "style.font.underline", style.Font.Underline ? "1" : "0");
        AppendValue(canonical, "style.font.color", style.Font.Color);
        AppendValue(canonical, "style.fill.pattern", style.Fill.Pattern);
        AppendValue(canonical, "style.fill.foreground", style.Fill.ForegroundColor);
        AppendValue(canonical, "style.fill.background", style.Fill.BackgroundColor);
        AppendBorder(canonical, "left", style.Border.Left);
        AppendBorder(canonical, "right", style.Border.Right);
        AppendBorder(canonical, "top", style.Border.Top);
        AppendBorder(canonical, "bottom", style.Border.Bottom);
        AppendValue(canonical, "style.horizontal", style.Horizontal.ToString());
        AppendValue(canonical, "style.vertical", style.Vertical.ToString());
        AppendValue(canonical, "style.rotation", style.RotationDegrees.ToString("R", CultureInfo.InvariantCulture));
        AppendValue(canonical, "style.numberFormat", style.NumberFormatCode);
    }

    private static void AppendBorder(StringBuilder canonical, string edge, BorderSideModel border)
    {
        AppendValue(canonical, "style.border." + edge + ".style", border.Style);
        AppendValue(canonical, "style.border." + edge + ".color", border.Color);
    }

    private static void AppendValue(StringBuilder canonical, string name, string? value)
    {
        var normalized = value == null ? string.Empty : value.Normalize(NormalizationForm.FormC);
        canonical.Append(name).Append(':').Append(value == null ? 'N' : 'S').Append(':')
            .Append(Encoding.UTF8.GetByteCount(normalized).ToString(CultureInfo.InvariantCulture))
            .Append(':').Append(normalized).Append('\n');
    }

    private static string ComputeSha256(string canonical)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical));
        return BitConverter.ToString(bytes).Replace("-", string.Empty);
    }

    private static bool IsHexadecimal(char value)
    {
        return (value >= '0' && value <= '9') ||
            (value >= 'a' && value <= 'f') ||
            (value >= 'A' && value <= 'F');
    }

    private static ExcelRangeReference NormalizeRange(ExcelRangeReference range)
    {
        return new ExcelRangeReference(
            new ExcelCellAddress(Math.Min(range.Start.Column, range.End.Column), Math.Min(range.Start.Row, range.End.Row)),
            new ExcelCellAddress(Math.Max(range.Start.Column, range.End.Column), Math.Max(range.Start.Row, range.End.Row)));
    }

    private static bool Contains(ExcelRangeReference range, ExcelCellAddress address)
    {
        return address.Column >= range.Start.Column && address.Column <= range.End.Column &&
            address.Row >= range.Start.Row && address.Row <= range.End.Row;
    }

    private static bool Intersects(ExcelRangeReference first, ExcelRangeReference second)
    {
        return first.Start.Column <= second.End.Column && first.End.Column >= second.Start.Column &&
            first.Start.Row <= second.End.Row && first.End.Row >= second.Start.Row;
    }

    private static ExcelRangeReference Clip(ExcelRangeReference range, ExcelRangeReference merge)
    {
        return new ExcelRangeReference(
            new ExcelCellAddress(Math.Max(range.Start.Column, merge.Start.Column), Math.Max(range.Start.Row, merge.Start.Row)),
            new ExcelCellAddress(Math.Min(range.End.Column, merge.End.Column), Math.Min(range.End.Row, merge.End.Row)));
    }

    private static bool IsInRange(string anchor, ExcelRangeReference range)
    {
        try
        {
            return Intersects(range, ExcelRangeReference.Parse(anchor));
        }
        catch (ArgumentException)
        {
            try
            {
                var cell = ExcelCellAddress.Parse(anchor);
                return Contains(range, cell);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }
}
