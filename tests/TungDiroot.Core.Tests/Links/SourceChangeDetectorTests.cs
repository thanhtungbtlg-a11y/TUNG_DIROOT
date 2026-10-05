using System;
using System.Globalization;
using System.Linq;
using TungDiroot.Core.Excel;
using TungDiroot.Core.Links;
using Xunit;

namespace TungDiroot.Core.Tests.Links;

public sealed class SourceChangeDetectorTests
{
    private const string ValidHash = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";

    [Fact]
    public void Same_metadata_and_hash_is_unchanged()
    {
        var result = SourceChangeDetector.Compare(SourceFingerprint.Parse("10:42:" + ValidHash), SourceFingerprint.Parse("10:42:" + ValidHash));

        Assert.Equal(SourceChange.Unchanged, result);
    }

    [Fact]
    public void Parse_round_trips_the_portable_fingerprint_format()
    {
        var fingerprint = SourceFingerprint.Parse("10:42:" + new string('a', 64));

        Assert.Equal(10, fingerprint.Length);
        Assert.Equal(42, fingerprint.LastWriteUtcTicks);
        Assert.Equal(new string('A', 64), fingerprint.Sha256Hex);
        Assert.Equal("10:42:" + new string('A', 64), fingerprint.ToString());
    }

    [Theory]
    [InlineData(":42:0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF")]
    [InlineData("10:42:")]
    [InlineData("10:42:0123456789ABCDEG0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF")]
    [InlineData("10:42:0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF:EXTRA")]
    [InlineData("-1:42:0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF")]
    public void Parse_rejects_malformed_serialized_values(string value)
    {
        Assert.Throws<ArgumentException>(() => SourceFingerprint.Parse(value));
    }

    [Fact]
    public void Parse_requires_exactly_a_64_character_sha256_hex_value()
    {
        Assert.Throws<ArgumentException>(() => SourceFingerprint.Parse("10:42:" + new string('A', 63)));
        Assert.Throws<ArgumentException>(() => SourceFingerprint.Parse("10:42:" + new string('A', 65)));
        Assert.Throws<ArgumentException>(() => SourceFingerprint.Parse(" 10:42:" + ValidHash));
        Assert.Throws<ArgumentException>(() => SourceFingerprint.Parse("10:42:" + ValidHash + " "));
    }

    [Fact]
    public void Create_hashes_only_canonical_imported_region_state()
    {
        var expected = SourceFingerprint.Create(12, 34, "PCCC", ExcelRangeReference.Parse("D3:A1"), CreateRange(reverseOrder: false));
        var reordered = SourceFingerprint.Create(12, 34, "PCCC", ExcelRangeReference.Parse("A1:D3"), CreateRange(reverseOrder: true));

        Assert.Equal(expected, reordered);
    }

    [Fact]
    public void Create_is_independent_of_the_current_culture()
    {
        var previous = CultureInfo.CurrentCulture;
        var previousUi = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("tr-TR");
            var turkish = SourceFingerprint.Create(12, 34, "PCCC", ExcelRangeReference.Parse("A1:D3"), CreateRange(reverseOrder: false));

            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
            var french = SourceFingerprint.Create(12, 34, "PCCC", ExcelRangeReference.Parse("A1:D3"), CreateRange(reverseOrder: true));

            Assert.Equal(turkish, french);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
            CultureInfo.CurrentUICulture = previousUi;
        }
    }

    [Fact]
    public void Create_normalizes_merge_endpoints_before_hashing()
    {
        var expected = SourceFingerprint.Create(12, 34, "PCCC", ExcelRangeReference.Parse("A1:D3"), CreateRange(reverseOrder: false));
        var reversedMerge = SourceFingerprint.Create(12, 34, "PCCC", ExcelRangeReference.Parse("A1:D3"), CreateRange(reverseOrder: false, reverseMergeEndpoints: true));

        Assert.Equal(expected, reversedMerge);
    }

    [Fact]
    public void Create_distinguishes_a_missing_style_value_from_its_literal_text()
    {
        var missing = SourceFingerprint.Create(12, 34, "PCCC", ExcelRangeReference.Parse("A1:D3"), CreateRange(reverseOrder: false));
        var literal = SourceFingerprint.Create(12, 34, "PCCC", ExcelRangeReference.Parse("A1:D3"), CreateRange(reverseOrder: false, literalNullStyleToken: true));

        Assert.NotEqual(missing, literal);
    }

    [Fact]
    public void Create_hashes_an_in_range_one_cell_drawing_anchor()
    {
        var withoutDrawing = CreateFingerprint();
        var withInRangeDrawing = CreateFingerprint(new WorkbookDrawingItem(WorkbookDrawingKind.Image, "B2", "rId-in", null, null));

        Assert.NotEqual(withoutDrawing, withInRangeDrawing);
    }

    [Fact]
    public void Create_excludes_an_out_of_range_one_cell_drawing_anchor()
    {
        var withoutDrawing = CreateFingerprint();
        var withOutOfRangeDrawing = CreateFingerprint(new WorkbookDrawingItem(WorkbookDrawingKind.Image, "E4", "rId-out", null, null));

        Assert.Equal(withoutDrawing, withOutOfRangeDrawing);
    }

    [Fact]
    public void Create_excludes_an_unparseable_drawing_anchor()
    {
        var withoutDrawing = CreateFingerprint();
        var withUnknownDrawing = CreateFingerprint(new WorkbookDrawingItem(WorkbookDrawingKind.Image, "absoluteAnchor", "rId-unknown", null, null));

        Assert.Equal(withoutDrawing, withUnknownDrawing);
    }

    [Fact]
    public void Compare_ignores_preflight_metadata_when_the_relevant_region_hash_is_identical()
    {
        var before = SourceFingerprint.Parse("10:42:" + ValidHash);

        Assert.Equal(SourceChange.Unchanged, SourceChangeDetector.Compare(before, SourceFingerprint.Parse("11:43:" + ValidHash.ToLowerInvariant())));
    }

    [Fact]
    public void Compare_reports_changed_when_the_relevant_region_hash_differs()
    {
        var before = SourceFingerprint.Parse("10:42:" + ValidHash);

        Assert.Equal(SourceChange.Changed, SourceChangeDetector.Compare(before, SourceFingerprint.Parse("10:42:" + new string('F', 64))));
    }

    private static SourceFingerprint CreateFingerprint(params WorkbookDrawingItem[] drawings)
    {
        return SourceFingerprint.Create(12, 34, "PCCC", ExcelRangeReference.Parse("A1:D3"), CreateRange(false, drawings: drawings));
    }

    private static WorkbookRangeModel CreateRange(bool reverseOrder, bool reverseMergeEndpoints = false, bool literalNullStyleToken = false, WorkbookDrawingItem[]? drawings = null)
    {
        var style = new CellStyleModel(
            new FontModel("Calibri", 11D, false, false, false, "FF000000"),
            new FillModel("solid", "FFFFFFFF", literalNullStyleToken ? "<null>" : null),
            new BorderModel(new BorderSideModel("thin", "FF000000"), new BorderSideModel("none", null), new BorderSideModel("none", null), new BorderSideModel("none", null)),
            HorizontalAlignment.Center,
            VerticalAlignment.Bottom,
            90D,
            "0.00%");
        var cells = new[]
        {
            new CellModel(new ExcelCellAddress(4, 3), "2,5%", null, style, false),
            new CellModel(new ExcelCellAddress(1, 1), "Title", null, CellStyleModel.Default, false)
        };
        var merged = new[]
        {
            ExcelRangeReference.Parse("A1:B1"),
            reverseMergeEndpoints
                ? new ExcelRangeReference(new ExcelCellAddress(4, 2), new ExcelCellAddress(3, 2))
                : ExcelRangeReference.Parse("C2:D2")
        };
        var defaultDrawings = new[]
        {
            new WorkbookDrawingItem(WorkbookDrawingKind.Image, "C2:D2", "rId2", null, null),
            new WorkbookDrawingItem(WorkbookDrawingKind.Image, "A1:B1", "rId1", null, null)
        };

        return new WorkbookRangeModel(
            Array.Empty<WorkbookRowModel>(),
            Array.Empty<WorkbookColumnModel>(),
            reverseOrder ? cells.Reverse() : cells,
            reverseOrder ? merged.Reverse() : merged,
            Array.Empty<WorkbookReadWarning>(),
            new WorkbookDrawingModel(reverseOrder ? (drawings ?? defaultDrawings).Reverse() : (drawings ?? defaultDrawings)));
    }
}
