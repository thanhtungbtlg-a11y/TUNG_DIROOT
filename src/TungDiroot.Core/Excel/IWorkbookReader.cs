namespace TungDiroot.Core.Excel;

public interface IWorkbookReader
{
    WorkbookCatalog ReadCatalog(string path);

    WorkbookRangeModel ReadRange(string path, string worksheetName, ExcelRangeReference range, RangeReadOptions options);
}
