using System;
using System.IO;
using System.Linq;
using TungDiroot.Core.Excel;
using TungDiroot.Core.Tests.Fixtures;
using Xunit;

namespace TungDiroot.Core.Tests.Excel;

public sealed class OpenXmlWorkbookCatalogTests
{
    [Fact]
    public void Catalog_rejects_legacy_xls_extension()
    {
        using var fixture = TestWorkbookFactory.CreateCatalogWorkbook();
        var legacyPath = Path.ChangeExtension(fixture.Path, ".xls");
        File.Move(fixture.Path, legacyPath);

        var exception = Assert.Throws<NotSupportedException>(() => new OpenXmlWorkbookReader().ReadCatalog(legacyPath));

        Assert.Contains(".xlsx", exception.Message);
        Assert.Contains(".xlsm", exception.Message);
    }

    [Fact]
    public void Catalog_lists_sheets_print_area_named_range_and_used_range()
    {
        using var fixture = TestWorkbookFactory.CreateCatalogWorkbook();

        var catalog = new OpenXmlWorkbookReader().ReadCatalog(fixture.Path);

        Assert.Equal(new[] { "PCCC", "Thiết bị" }, catalog.Worksheets.Select(x => x.Name));
        Assert.Contains(catalog.Regions, x => x.Kind == RegionKind.PrintArea && x.Reference.ToA1() == "A1:H25");
        Assert.Contains(catalog.Regions, x => x.Name == "Equipment" && x.Reference.ToA1() == "A2:F18");
        Assert.Contains(catalog.Regions, x => x.Kind == RegionKind.UsedRange && x.WorksheetName == "PCCC" && x.Reference.ToA1() == "A1:H25");
        Assert.Contains(catalog.Regions, x => x.Kind == RegionKind.UsedRange && x.WorksheetName == "Thiết bị" && x.Reference.ToA1() == "A2:F18");
    }

    [Fact]
    public void Catalog_reads_macro_enabled_workbook_without_changing_its_contents()
    {
        using var fixture = TestWorkbookFactory.CreateMacroEnabledCatalogWorkbook();
        var before = File.ReadAllBytes(fixture.Path);

        var catalog = new OpenXmlWorkbookReader().ReadCatalog(fixture.Path);

        Assert.Equal(new[] { "PCCC", "Thiết bị" }, catalog.Worksheets.Select(x => x.Name));
        Assert.Equal(before, File.ReadAllBytes(fixture.Path));
    }

    [Fact]
    public void Catalog_reads_when_another_process_has_a_shared_read_handle()
    {
        using var fixture = TestWorkbookFactory.CreateCatalogWorkbook();
        using var openHandle = new FileStream(fixture.Path, FileMode.Open, FileAccess.Read, FileShare.Read);

        var catalog = new OpenXmlWorkbookReader().ReadCatalog(fixture.Path);

        Assert.Equal(2, catalog.Worksheets.Count);
    }

    [Fact]
    public void Catalog_reader_allows_renaming_the_file_while_its_read_handle_is_open()
    {
        using var fixture = TestWorkbookFactory.CreateCatalogWorkbook();
        var renamedPath = Path.Combine(Path.GetDirectoryName(fixture.Path)!, "renamed.xlsx");
        var reader = new OpenXmlWorkbookReader((path, share) =>
        {
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, share);
            File.Move(path, renamedPath);
            File.Move(renamedPath, path);
            return stream;
        });

        var catalog = reader.ReadCatalog(fixture.Path);

        Assert.Equal(2, catalog.Worksheets.Count);
    }

    [Fact]
    public void Catalog_derives_used_range_from_cell_references_when_dimension_is_absent()
    {
        using var fixture = TestWorkbookFactory.CreateCellReferenceCatalogWorkbook();

        var catalog = new OpenXmlWorkbookReader().ReadCatalog(fixture.Path);

        Assert.Contains(catalog.Regions, x => x.Kind == RegionKind.UsedRange && x.WorksheetName == "PCCC" && x.Reference.ToA1() == "B3:D9");
    }

    [Fact]
    public void Catalog_lists_each_print_area_region()
    {
        using var fixture = TestWorkbookFactory.CreateMultiAreaCatalogWorkbook();

        var catalog = new OpenXmlWorkbookReader().ReadCatalog(fixture.Path);

        Assert.Contains(catalog.Regions, x => x.Kind == RegionKind.PrintArea && x.Reference.ToA1() == "A1:B2");
        Assert.Contains(catalog.Regions, x => x.Kind == RegionKind.PrintArea && x.Reference.ToA1() == "D4:E5");
    }

    [Fact]
    public void Catalog_reads_a_quoted_sheet_single_cell_named_range()
    {
        using var fixture = TestWorkbookFactory.CreateCatalogWorkbook();

        var catalog = new OpenXmlWorkbookReader().ReadCatalog(fixture.Path);

        Assert.Contains(catalog.Regions, x => x.Kind == RegionKind.NamedRange && x.Name == "SingleCell" && x.Reference.ToA1() == "F18:F18");
    }
}
