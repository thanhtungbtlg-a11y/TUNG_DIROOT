using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using TungDiroot.Addin.Import;
using TungDiroot.Core.Excel;
using TungDiroot.Core.Import;
using TungDiroot.Core.Layout;
using TungDiroot.Core.Preview;
using TungDiroot.Presentation.UI;
using TungDiroot.Rendering;

namespace TungDiroot.Addin.UI;

public partial class AddExcelWindow : Window
{
    private readonly bool _legendSeedAvailable;
    private readonly Action<IReadOnlyList<WorkbookImportRequest>, Action<IReadOnlyList<ImportRowResult>?, string?>> _executeImport;
    private readonly Action<Action<RevitStyleCatalogSnapshot?, string?>> _loadStyleCatalog;
    private readonly AddExcelViewModel _viewModel = new AddExcelViewModel();

    public AddExcelWindow(
        bool legendSeedAvailable,
        Action<IReadOnlyList<WorkbookImportRequest>, Action<IReadOnlyList<ImportRowResult>?, string?>> executeImport,
        Action<Action<RevitStyleCatalogSnapshot?, string?>> loadStyleCatalog)
    {
        _legendSeedAvailable = legendSeedAvailable;
        _executeImport = executeImport ?? throw new ArgumentNullException(nameof(executeImport));
        _loadStyleCatalog = loadStyleCatalog ?? throw new ArgumentNullException(nameof(loadStyleCatalog));
        InitializeComponent();
        ApplyInitialWindowSize();
        DataContext = _viewModel;
        ModeColumn.ItemsSource = Enum.GetValues(typeof(ImportMode));
        ViewKindColumn.ItemsSource = Enum.GetValues(typeof(TargetViewKind));
        UpdateColumn.ItemsSource = Enum.GetValues(typeof(UpdateBehavior));
        DpiColumn.ItemsSource = Enum.GetValues(typeof(ImageDpi));
        RendererColumn.ItemsSource = Enum.GetValues(typeof(ImageRendererEngine));
        RefreshProfileChoices();
    }

    private void RefreshProfileChoices()
    {
        ProfileColumn.ItemsSource = new[] { string.Empty }
            .Concat(new TableGenProfileRepository().LoadAll()
                .Where(result => result.IsSuccess)
                .Select(result => result.Profile!.Name)
                .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase))
            .ToArray();
    }

    private void ApplyInitialWindowSize()
    {
        var initialSize = WindowSizePolicy.Clamp(
            preferredWidth: 1800,
            preferredHeight: 980,
            minimumWidth: MinWidth,
            minimumHeight: MinHeight,
            workAreaWidth: SystemParameters.WorkArea.Width,
            workAreaHeight: SystemParameters.WorkArea.Height,
            margin: 32);
        MinWidth = Math.Min(MinWidth, initialSize.Width);
        MinHeight = Math.Min(MinHeight, initialSize.Height);
        Width = initialSize.Width;
        Height = initialSize.Height;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Excel workbook (*.xlsx;*.xlsm)|*.xlsx;*.xlsm", Multiselect = true };
        if (dialog.ShowDialog(this) != true) return;
        AddWorkbookFiles(dialog.FileNames);
    }

    private void AddWorkbookFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths.Where(IsWorkbookPath).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var catalog = new OpenXmlWorkbookReader().ReadCatalog(path);
                var preferred = catalog.Regions.Where(region => region.Kind != RegionKind.UsedRange).ToArray();
                var regions = preferred.Length > 0 ? preferred : catalog.Regions.ToArray();
                if (regions.Length == 0) throw new InvalidDataException("Workbook không có worksheet hoặc vùng dữ liệu có thể import.");
                foreach (var region in regions)
                    _viewModel.Rows.Add(new AddExcelRowViewModel
                    {
                        FilePath = path,
                        WorksheetName = region.WorksheetName,
                        RangeReference = region.Reference.ToA1(),
                        RequestedViewName = Path.GetFileNameWithoutExtension(path) + " - " + (region.Name ?? region.WorksheetName),
                        EstimatedRows = region.Reference.End.Row - region.Reference.Start.Row + 1,
                        EstimatedColumns = region.Reference.End.Column - region.Reference.Start.Column + 1,
                        LegendSeedAvailable = _legendSeedAvailable
                    });
            }
            catch (Exception exception) { StatusText.Text = exception.Message; }
        }
    }

    public void AddImportRequest(WorkbookImportRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        var row = new AddExcelRowViewModel
        {
            FilePath = request.FilePath,
            WorksheetName = request.WorksheetName,
            RangeReference = request.Range.ToA1(),
            RequestedViewName = request.RequestedViewName ?? request.WorksheetName + " - Copy",
            ImportMode = request.Mode,
            TargetViewKind = request.TargetView,
            UpdateBehavior = request.UpdateBehavior,
            Dpi = request.Dpi,
            RendererEngine = request.RendererEngine,
            IncludeHiddenRowsAndColumns = request.IncludeHiddenRowsAndColumns,
            BlackAndWhite = request.BlackAndWhite,
            StyleProfileName = request.StyleProfileName,
            EstimatedRows = request.Range.End.Row - request.Range.Start.Row + 1,
            EstimatedColumns = request.Range.End.Column - request.Range.Start.Column + 1,
            LegendSeedAvailable = _legendSeedAvailable,
            IsSelected = true
        };
        _viewModel.Rows.Add(row);
        _viewModel.SelectedRow = row;
        ImportRowsGrid.ScrollIntoView(row);
        StatusText.Text = "Đã nhân cấu hình. Hãy sửa vùng/view nếu cần rồi bấm Import.";
    }

    private static bool IsWorkbookPath(string path)
    {
        var extension = Path.GetExtension(path);
        return string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".xlsm", StringComparison.OrdinalIgnoreCase);
    }

    private void Window_PreviewDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) &&
            ((string[]?)e.Data.GetData(DataFormats.FileDrop))?.Any(IsWorkbookPath) == true
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
            AddWorkbookFiles(files);
    }

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        var row = _viewModel.SelectedRow;
        if (row == null || !_viewModel.CanPreview) return;
        _viewModel.IsPreviewing = true;
        StatusText.Text = "Đang tạo preview…";
        try
        {
            var range = ExcelRangeReference.Parse(row.RangeReference);
            var model = new OpenXmlWorkbookReader().ReadRange(
                row.FilePath,
                row.WorksheetName,
                range,
                new RangeReadOptions(row.IncludeHiddenRowsAndColumns));
            ImageRenderResult rendered;
            using (var stream = new MemoryStream())
                rendered = new BuiltInWorkbookImageRenderer().Render(
                    model,
                    new ImageRenderOptions(ImageDpi.Dpi150, row.BlackAndWhite, true),
                    stream);
            var report = WorkbookPreviewAnalyzer.Analyze(model, row.ImportMode);
            if (rendered.Warnings.Count > 0)
            {
                var issues = report.Issues.Concat(rendered.Warnings.Select(warning =>
                    new WorkbookPreviewIssue(PreviewIssueSeverity.Warning, warning.Code, warning.Message)));
                report = new WorkbookPreviewReport(
                    report.RowCount,
                    report.ColumnCount,
                    report.CellCount,
                    report.MergedRangeCount,
                    report.Estimate,
                    issues);
            }
            var preview = new WorkbookPreviewWindow(
                new WorkbookPreviewViewModel($"{Path.GetFileName(row.FilePath)} · {row.WorksheetName}!{range.ToA1()}", report),
                rendered.Bytes) { Owner = this };
            preview.ShowDialog();
            StatusText.Text = report.CanImport ? "Preview hoàn tất." : "Preview phát hiện lỗi cần sửa.";
        }
        catch (Exception exception)
        {
            StatusText.Text = exception.Message;
        }
        finally
        {
            _viewModel.IsPreviewing = false;
        }
    }

    private void ManageProfiles_Click(object sender, RoutedEventArgs e)
    {
        var row = _viewModel.SelectedRow;
        if (row == null)
        {
            StatusText.Text = "Hãy chọn một dòng Excel để lấy danh sách style cần ánh xạ.";
            return;
        }
        _viewModel.IsPreviewing = true;
        StatusText.Text = "Đang đọc Text Type và Line Style từ model…";
        try
        {
            _loadStyleCatalog((catalog, error) =>
            {
                _viewModel.IsPreviewing = false;
                if (!IsVisible) return;
                if (error != null || catalog == null)
                {
                    StatusText.Text = error ?? "STYLE_CATALOG_UNAVAILABLE";
                    return;
                }
                try
                {
                    var range = ExcelRangeReference.Parse(row.RangeReference);
                    var model = new OpenXmlWorkbookReader().ReadRange(
                        row.FilePath, row.WorksheetName, range,
                        new RangeReadOptions(row.IncludeHiddenRowsAndColumns));
                    var candidates = StyleMappingCandidateExtractor.Extract(model);
                    var manager = new TableGenProfileManagerWindow(candidates, catalog) { Owner = this };
                    manager.ShowDialog();
                    RefreshProfileChoices();
                    if (!string.IsNullOrWhiteSpace(manager.LastSavedProfileName))
                        row.StyleProfileName = manager.LastSavedProfileName;
                    StatusText.Text = manager.LastSavedProfileName == null
                        ? "Đã đóng quản lý profile."
                        : $"Đã chọn profile “{manager.LastSavedProfileName}” cho dòng hiện tại.";
                }
                catch (Exception exception)
                {
                    StatusText.Text = exception.Message;
                }
            });
        }
        catch (Exception exception)
        {
            _viewModel.IsPreviewing = false;
            StatusText.Text = exception.Message;
        }
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var errors = _viewModel.Validate();
        var blocking = errors.Where(message => message.Code != "LARGE_REVIT_TABLE_CONFIRMATION_REQUIRED").ToArray();
        if (blocking.Length > 0) { StatusText.Text = blocking[0].Message; return; }
        if (errors.Any(message => message.Code == "LARGE_REVIT_TABLE_CONFIRMATION_REQUIRED") &&
            MessageBox.Show(this, "Bảng vượt 5.000 ô và có thể tạo nhiều element. Tiếp tục?", "TUNG_DIROOT", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        var requests = _viewModel.Rows.Where(row => row.IsSelected).Select(row => new WorkbookImportRequest(
            row.FilePath, row.WorksheetName, ExcelRangeReference.Parse(row.RangeReference), row.ImportMode, row.TargetViewKind,
            row.UpdateBehavior, row.Dpi, row.IncludeHiddenRowsAndColumns, row.BlackAndWhite, row.RequestedViewName,
            row.RendererEngine, row.StyleProfileName)).ToArray();
        _viewModel.IsImporting = true;
        StatusText.Text = "Đang chờ Revit xử lý…";
        try
        {
            _executeImport(requests, (results, error) =>
            {
                _viewModel.IsImporting = false;
                if (error != null) { StatusText.Text = error; return; }
                var completed = results ?? Array.Empty<ImportRowResult>();
                StatusText.Text = $"Hoàn tất: {completed.Count(result => result.Succeeded)}/{completed.Count} thành công.";
            });
        }
        catch
        {
            _viewModel.IsImporting = false;
            throw;
        }
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e) => _viewModel.SetAllSelected(true);

    private void ClearSelection_Click(object sender, RoutedEventArgs e) => _viewModel.SetAllSelected(false);

    private void RowCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: AddExcelRowViewModel row })
            ImportRowsGrid.SelectedItem = row;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_viewModel.IsImporting)
        {
            e.Cancel = true;
            MessageBox.Show(this, "Hãy chờ Revit hoàn tất import hiện tại.", "TUNG_DIROOT", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        base.OnClosing(e);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
