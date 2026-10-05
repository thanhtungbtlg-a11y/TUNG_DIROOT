using System;
using System.Linq;
using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;
using TungDiroot.Addin.Sync;

namespace TungDiroot.Addin;

public sealed class Application : IExternalApplication
{
    private DocumentAutoSyncService? _autoSync;
    public const string RibbonTabName = "TUNG_DIROOT";
    public const string RibbonPanelName = "Excel Tools";
    public const string ParameterPanelName = "Parameter Tools";
    public const string ModelPanelName = "Model Tools";
    public const string TableGenButtonId = "TUNG_DIROOT.TableGen";
    public const string DataLinkButtonId = "TUNG_DIROOT.DataLink";
    public const string DocumentLinkButtonId = "TUNG_DIROOT.DocumentLink";
    public const string SheetToolsButtonId = "TUNG_DIROOT.SheetTools";
    public const string ParameterLoaderButtonId = "TUNG_DIROOT.ParameterLoader";
    public const string ParaManagerButtonId = "TUNG_DIROOT.ParaManager";
    public const string ReOrderingButtonId = "TUNG_DIROOT.ReOrdering";
    public const string FamilyReviserButtonId = "TUNG_DIROOT.FamilyReviser";
    public const string FilterButtonId = "TUNG_DIROOT.Filter";
    public const string BulkVgButtonId = "TUNG_DIROOT.BulkVG";

    public Result OnStartup(UIControlledApplication application)
    {
        _autoSync = new DocumentAutoSyncService();
        application.ControlledApplication.DocumentOpened += _autoSync.OnDocumentOpened;
        application.Idling += _autoSync.OnIdling;
        try
        {
            application.CreateRibbonTab(RibbonTabName);
        }
        catch (Autodesk.Revit.Exceptions.ArgumentException)
        {
            // The tab is shared safely when another TUNG_DIROOT component created it first.
        }

        var excelPanel = application.GetRibbonPanels(RibbonTabName)
            .FirstOrDefault(candidate => string.Equals(candidate.Name, RibbonPanelName, StringComparison.Ordinal))
            ?? application.CreateRibbonPanel(RibbonTabName, RibbonPanelName);
        if (!excelPanel.GetItems().Any(item => string.Equals(item.Name, TableGenButtonId, StringComparison.Ordinal)))
        {
            var tableData = new PushButtonData(
                TableGenButtonId,
                "TableGen",
                Assembly.GetExecutingAssembly().Location,
                typeof(Command_TableGen).FullName);
            var tableButton = (PushButton)excelPanel.AddItem(tableData);
            tableButton.ToolTip = "Nhập và đồng bộ bảng Excel trong Revit.";
            tableButton.Image = LoadIcon("TableGen16.png");
            tableButton.LargeImage = LoadIcon("TableGen32.png");
        }
        if (!excelPanel.GetItems().Any(item => string.Equals(item.Name, DataLinkButtonId, StringComparison.Ordinal)))
        {
            var dataLinkData = new PushButtonData(
                DataLinkButtonId,
                "DataLink",
                Assembly.GetExecutingAssembly().Location,
                typeof(Command_DataLink).FullName);
            var dataLinkButton = (PushButton)excelPanel.AddItem(dataLinkData);
            dataLinkButton.ToolTip = "Xuất/nhập dữ liệu Revit qua Excel, xem trước và chỉ áp dụng các thay đổi an toàn.";
            dataLinkButton.Image = LoadIcon("DataLink16.png");
            dataLinkButton.LargeImage = LoadIcon("DataLink32.png");
        }
        if (!excelPanel.GetItems().Any(item => string.Equals(item.Name, DocumentLinkButtonId, StringComparison.Ordinal)))
        {
            var documentLinkData = new PushButtonData(
                DocumentLinkButtonId,
                "PDF / Word\nLink",
                Assembly.GetExecutingAssembly().Location,
                typeof(Command_DocumentLink).FullName);
            var documentLinkButton = (PushButton)excelPanel.AddItem(documentLinkData);
            documentLinkButton.ToolTip = "Nhập các trang PDF/Word vào Drafting View, theo dõi nguồn và cập nhật liên kết.";
            documentLinkButton.Image = LoadIcon("TableGen16.png");
            documentLinkButton.LargeImage = LoadIcon("TableGen32.png");
        }
        if (!excelPanel.GetItems().Any(item => string.Equals(item.Name, SheetToolsButtonId, StringComparison.Ordinal)))
        {
            var sheetToolsData = new PushButtonData(
                SheetToolsButtonId,
                "SheetLink\n/ Gen",
                Assembly.GetExecutingAssembly().Location,
                typeof(Command_SheetTools).FullName);
            var sheetToolsButton = (PushButton)excelPanel.AddItem(sheetToolsData);
            sheetToolsButton.ToolTip = "Xuất/nhập Sheet và View qua Excel; preview rồi tạo/cập nhật Sheet, View, Viewport và revision.";
            sheetToolsButton.Image = LoadIcon("DataLink16.png");
            sheetToolsButton.LargeImage = LoadIcon("DataLink32.png");
        }

        var parameterPanel = application.GetRibbonPanels(RibbonTabName)
            .FirstOrDefault(candidate => string.Equals(candidate.Name, ParameterPanelName, StringComparison.Ordinal))
            ?? application.CreateRibbonPanel(RibbonTabName, ParameterPanelName);
        if (!parameterPanel.GetItems().Any(item => string.Equals(item.Name, ParameterLoaderButtonId, StringComparison.Ordinal)))
        {
            var parameterData = new PushButtonData(
                ParameterLoaderButtonId,
                "Parameter\nLoader",
                Assembly.GetExecutingAssembly().Location,
                typeof(Command_ParameterLoader).FullName);
            var parameterButton = (PushButton)parameterPanel.AddItem(parameterData);
            parameterButton.ToolTip = "Kiểm tra và gán Shared Parameter theo file TXT + Excel cấu hình.";
            parameterButton.Image = LoadIcon("ParameterLoader16.png");
            parameterButton.LargeImage = LoadIcon("ParameterLoader32.png");
        }
        if (!parameterPanel.GetItems().Any(item => string.Equals(item.Name, ReOrderingButtonId, StringComparison.Ordinal)))
        {
            var reorderData = new PushButtonData(
                ReOrderingButtonId,
                "Parameter\nReOrdering",
                Assembly.GetExecutingAssembly().Location,
                typeof(Command_ReOrdering).FullName);
            var reorderButton = (PushButton)parameterPanel.AddItem(reorderData);
            reorderButton.ToolTip = "Xuất thứ tự Family Parameter ra Excel, preview, backup rồi sắp lại trong từng parameter group.";
            reorderButton.Image = LoadIcon("ParameterLoader16.png");
            reorderButton.LargeImage = LoadIcon("ParameterLoader32.png");
        }
        if (!parameterPanel.GetItems().Any(item => string.Equals(item.Name, ParaManagerButtonId, StringComparison.Ordinal)))
        {
            var managerData = new PushButtonData(
                ParaManagerButtonId,
                "ParaManager",
                Assembly.GetExecutingAssembly().Location,
                typeof(Command_ParaManager).FullName);
            var managerButton = (PushButton)parameterPanel.AddItem(managerData);
            managerButton.ToolTip = "Xuất, preview, tạo shared parameter và cập nhật an toàn group/category cho Project hoặc Family.";
            managerButton.Image = LoadIcon("ParameterLoader16.png");
            managerButton.LargeImage = LoadIcon("ParameterLoader32.png");
        }
        if (!parameterPanel.GetItems().Any(item => string.Equals(item.Name, FamilyReviserButtonId, StringComparison.Ordinal)))
        {
            var reviserData = new PushButtonData(
                FamilyReviserButtonId,
                "Family\nReviser",
                Assembly.GetExecutingAssembly().Location,
                typeof(Command_FamilyReviser).FullName);
            var reviserButton = (PushButton)parameterPanel.AddItem(reviserData);
            reviserButton.ToolTip = "Xuất Family/Type ra Excel, kiểm tra tên và đổi tên hàng loạt có preview, backup và một Undo.";
            reviserButton.Image = LoadIcon("ParameterLoader16.png");
            reviserButton.LargeImage = LoadIcon("ParameterLoader32.png");
        }

        var modelPanel = application.GetRibbonPanels(RibbonTabName)
            .FirstOrDefault(candidate => string.Equals(candidate.Name, ModelPanelName, StringComparison.Ordinal))
            ?? application.CreateRibbonPanel(RibbonTabName, ModelPanelName);
        if (!modelPanel.GetItems().Any(item => string.Equals(item.Name, FilterButtonId, StringComparison.Ordinal)))
        {
            var filterData = new PushButtonData(
                FilterButtonId,
                "Filter",
                Assembly.GetExecutingAssembly().Location,
                typeof(Command_Filter).FullName);
            var filterButton = (PushButton)modelPanel.AddItem(filterData);
            filterButton.ToolTip = "Lọc phần tử theo category, family, type và parameter; chọn, cô lập, xuất Excel hoặc tạo view 3D riêng.";
            filterButton.Image = LoadIcon("Filter16.png");
            filterButton.LargeImage = LoadIcon("Filter32.png");
        }
        if (!modelPanel.GetItems().Any(item => string.Equals(item.Name, BulkVgButtonId, StringComparison.Ordinal)))
        {
            var bulkVgData = new PushButtonData(
                BulkVgButtonId,
                "Bulk V/G",
                Assembly.GetExecutingAssembly().Location,
                typeof(Command_BulkVG).FullName);
            var bulkVgButton = (PushButton)modelPanel.AddItem(bulkVgData);
            bulkVgButton.ToolTip = "Sao chép Visibility/Graphics của Category và Filter sang nhiều View, Template hoặc Viewport trên Sheet.";
            bulkVgButton.Image = LoadIcon("Filter16.png");
            bulkVgButton.LargeImage = LoadIcon("Filter32.png");
        }

        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        if (_autoSync != null)
        {
            application.ControlledApplication.DocumentOpened -= _autoSync.OnDocumentOpened;
            application.Idling -= _autoSync.OnIdling;
            _autoSync.Dispose();
            _autoSync = null;
        }
        return Result.Succeeded;
    }

    private static BitmapImage LoadIcon(string fileName)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri($"pack://application:,,,/{Assembly.GetExecutingAssembly().GetName().Name};component/Resources/{fileName}", UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        return image;
    }
}
