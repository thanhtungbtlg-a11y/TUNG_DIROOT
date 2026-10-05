using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using TungDiroot.Core.Excel;

if (args.Length != 1) throw new ArgumentException("Usage: TungDiroot.CatalogProbe <workbook.xlsx>");
var catalog = new OpenXmlWorkbookReader().ReadCatalog(Path.GetFullPath(args[0]));
var requiredSheets = new[] { "PCCC", "Thiết bị", "Định dạng", "Đồ họa", "Công thức" };
var result = new
{
    sampleContractValid = requiredSheets.All(required => catalog.Worksheets.Any(sheet => string.Equals(sheet.Name, required, StringComparison.Ordinal))) &&
        catalog.Regions.Any(region => string.Equals(region.Name, "Large_Table", StringComparison.Ordinal)),
    worksheets = catalog.Worksheets.Select(sheet => new { name = sheet.Name }).ToArray(),
    regions = catalog.Regions.Select(region => new
    {
        kind = region.Kind.ToString(), worksheet = region.WorksheetName, name = region.Name, reference = region.Reference.ToA1()
    }).ToArray()
};
Console.WriteLine(JsonSerializer.Serialize(result));
