using System;
using System.Collections.Generic;
using TungDiroot.Core.Excel;
using Xunit;

namespace TungDiroot.Core.Tests.Excel;

public sealed class WorkbookCatalogTests
{
    [Fact]
    public void Catalog_snapshots_and_exposes_read_only_collections()
    {
        var worksheets = new[] { new WorksheetCatalog("Source") };
        var regions = new[]
        {
            new WorkbookRegion(RegionKind.UsedRange, "Source", null, ExcelRangeReference.Parse("A1:B2"))
        };

        var catalog = new WorkbookCatalog(worksheets, regions);
        worksheets[0] = new WorksheetCatalog("Changed");
        regions[0] = new WorkbookRegion(RegionKind.NamedRange, "Changed", "Name", ExcelRangeReference.Parse("C3:D4"));

        Assert.Equal("Source", catalog.Worksheets[0].Name);
        Assert.Equal(RegionKind.UsedRange, catalog.Regions[0].Kind);
        Assert.Throws<NotSupportedException>(() => ((IList<WorksheetCatalog>)catalog.Worksheets)[0] = new WorksheetCatalog("Mutated"));
        Assert.Throws<NotSupportedException>(() => ((IList<WorkbookRegion>)catalog.Regions)[0] = regions[0]);
    }
}
