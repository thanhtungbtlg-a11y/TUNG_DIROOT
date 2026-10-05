using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.Win32;
using TungDiroot.Addin.Sheets;
using TungDiroot.Core.Runtime;
using TungDiroot.Core.SheetTools;
using TungDiroot.Presentation.UI;

namespace TungDiroot.Addin.UI;

public partial class SheetToolsWindow : Window
{
    private static SheetToolsWindow? _instance;
    private readonly Document _document;
    private readonly RevitSheetToolExecutor _executor;
    private readonly RevitActionExternalEvent _externalEvent;
    private readonly ObservableCollection<SheetLinkPreviewItem> _preview = new();
    private readonly SheetLinkPreviewState _previewState = new();
    private bool _isBusy;

    private SheetToolsWindow(UIApplication application)
    {
        _document = application.ActiveUIDocument?.Document ?? throw new InvalidOperationException("NO_ACTIVE_DOCUMENT");
        _executor = new RevitSheetToolExecutor(_document);
        _externalEvent = new RevitActionExternalEvent();
        InitializeComponent();
        ApplyInitialWindowSize();
        PreviewGrid.ItemsSource = _preview;
        Closing += OnClosing;
        Closed += (_, _) => { _externalEvent.Dispose(); _instance = null; };
    }

    public static void ShowOrActivate(UIApplication application)
    {
        var document = application.ActiveUIDocument?.Document ?? throw new InvalidOperationException("NO_ACTIVE_DOCUMENT");
        if (_instance != null && NativeObjectIdentity.IsSame(_instance._document, document))
        {
            if (_instance.WindowState == WindowState.Minimized) _instance.WindowState = WindowState.Normal;
            _instance.Activate();
            return;
        }
        if (_instance != null)
        {
            if (_instance._isBusy)
            {
                TaskDialog.Show("TUNG_DIROOT — SheetLink / SheetGen", "Hãy chờ thao tác hiện tại hoàn tất trước khi chuyển model.");
                return;
            }
            _instance.Close();
        }
        _instance = new SheetToolsWindow(application);
        _instance.Show();
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;
        InvalidatePreview();
        var dialog = new SaveFileDialog
        {
            Filter = "Excel SheetLink (*.xlsx)|*.xlsx",
            AddExtension = true,
            DefaultExt = ".xlsx",
            FileName = "TUNG_SheetLink.xlsx"
        };
        if (dialog.ShowDialog(this) != true) return;
        var path = dialog.FileName;
        _previewState.BeginOperation(path);
        RunExternal("Đang đọc Sheet, View và revision từ Revit…", application =>
        {
            var snapshot = _executor.ReadWorkbookSnapshot(application);
            Dispatcher.BeginInvoke(new Action(() => WriteSnapshotAsync(path, snapshot)));
        });
    }

    private async void WriteSnapshotAsync(string path, SheetLinkWorkbook snapshot)
    {
        try
        {
            StatusText.Text = "Đang ghi workbook Excel…";
            await Task.Run(() => SheetLinkWorkbookWriter.Write(path, snapshot));
            WorkbookPathBox.Text = path;
            SetBusy(false, "Đã xuất " + snapshot.Sheets.Count + " sheet, " + snapshot.Views.Count + " view và " + snapshot.Revisions.Count + " revision.");
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;
        var dialog = new OpenFileDialog { Filter = "Excel SheetLink (*.xlsx)|*.xlsx", CheckFileExists = true };
        if (dialog.ShowDialog(this) == true) WorkbookPathBox.Text = dialog.FileName;
    }

    private void OpenWorkbook_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!File.Exists(WorkbookPathBox.Text)) throw new FileNotFoundException("Hãy chọn workbook đã tồn tại.", WorkbookPathBox.Text);
            Process.Start(new ProcessStartInfo(WorkbookPathBox.Text) { UseShellExecute = true });
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private async void Preview_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;
        InvalidatePreview();
        var path = WorkbookPathBox.Text.Trim();
        if (!File.Exists(path))
        {
            Browse_Click(sender, e);
            path = WorkbookPathBox.Text.Trim();
            if (!File.Exists(path)) return;
        }
        var version = _previewState.BeginOperation(path);
        SetBusy(true, "Đang đọc workbook…");
        try
        {
            var workbook = await Task.Run(() => SheetLinkWorkbookReader.Read(path));
            RunExternal("Đang đối chiếu workbook với model…", application =>
            {
                var catalog = _executor.ReadReferenceCatalog(application);
                var plan = SheetLinkPreviewPlanner.Plan(workbook, catalog);
                Dispatcher.BeginInvoke(new Action(() => ShowPreview(path, version, workbook, plan)));
            }, preserveBusy: true, version: version);
        }
        catch (Exception exception) { if (version == _previewState.Version) ShowError(exception); }
    }

    private void ShowPreview(string path, long version, SheetLinkWorkbook workbook, SheetLinkPreviewPlan plan)
    {
        if (!_previewState.CompletePreview(path, version, workbook, plan)) return;
        _preview.Clear();
        foreach (var item in plan.Items) _preview.Add(item);
        ApplyButton.IsEnabled = plan.ReadyCount > 0;
        var errors = plan.Items.Count(item => item.Status is SheetLinkPreviewStatus.Invalid or SheetLinkPreviewStatus.Conflict);
        SetBusy(false, "Preview: " + plan.ReadyCount + " Ready, " + errors + " lỗi, " +
            plan.Items.Count(item => item.Status == SheetLinkPreviewStatus.NoChange) + " không đổi, " +
            plan.Items.Count(item => item.Status == SheetLinkPreviewStatus.Skipped) + " bỏ qua.");
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        var workbook = _previewState.Workbook;
        var plan = _previewState.Plan;
        if (!_previewState.CanApply || workbook == null || plan == null) return;
        var answer = MessageBox.Show(this,
            "Sẽ áp dụng " + plan.ReadyCount + " dòng Ready. Mỗi dòng có transaction riêng và toàn lượt chỉ tạo một Undo.\n\nTiếp tục?",
            "TUNG_DIROOT — Xác nhận SheetLink / SheetGen", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;
        InvalidatePreview();
        RunExternal("Đang tạo/cập nhật Revision, Sheet, View và Viewport…", application =>
        {
            var results = _executor.Apply(application, workbook, plan);
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var failed = results.Where(result => !result.Success).ToArray();
                SetBusy(false, "Apply: " + (results.Count - failed.Length) + " thành công, " + failed.Length + " lỗi.");
                if (failed.Length > 0)
                    MessageBox.Show(this, string.Join(Environment.NewLine, failed.Select(result =>
                        result.Kind + " dòng " + result.ExcelRow + " — " + result.Key + ": " + result.Message)),
                        "TUNG_DIROOT — SheetLink / SheetGen", MessageBoxButton.OK, MessageBoxImage.Warning);
                Preview_Click(this, new RoutedEventArgs());
            }));
        });
    }

    private void RunExternal(string status, Action<UIApplication> action, bool preserveBusy = false, long? version = null)
    {
        var operationVersion = version ?? _previewState.Version;
        if (!preserveBusy) SetBusy(true, status); else StatusText.Text = status;
        try
        {
            _externalEvent.Raise(application =>
            {
                try
                {
                    if (!NativeObjectIdentity.IsSame(_document, application.ActiveUIDocument?.Document))
                        throw new InvalidOperationException("Hãy quay lại đúng model đã mở SheetLink / SheetGen.");
                    action(application);
                }
                catch (Exception exception) { Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (operationVersion == _previewState.Version) ShowError(exception);
                })); }
            });
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void SetBusy(bool value, string status)
    {
        _isBusy = value;
        _previewState.SetBusy(value);
        ExportButton.IsEnabled = !value;
        PreviewButton.IsEnabled = !value;
        OpenWorkbookButton.IsEnabled = !value;
        BrowseWorkbookButton.IsEnabled = _previewState.CanEditInputs;
        WorkbookPathBox.IsEnabled = _previewState.CanEditInputs;
        ApplyButton.IsEnabled = _previewState.CanApply;
        StatusText.Text = status;
    }

    private void ShowError(Exception exception)
    {
        InvalidatePreview();
        SetBusy(false, "Không thể hoàn tất: " + Clean(exception.Message));
        MessageBox.Show(this, StatusText.Text, "TUNG_DIROOT — SheetLink / SheetGen", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void WorkbookPath_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!_previewState.ChangePath(WorkbookPathBox.Text)) return;
        _preview.Clear();
        if (ApplyButton != null) ApplyButton.IsEnabled = false;
    }

    private void InvalidatePreview()
    {
        _previewState.Invalidate();
        _preview.Clear();
        ApplyButton.IsEnabled = false;
    }

    private void ApplyInitialWindowSize()
    {
        var size = WindowSizePolicy.Clamp(1500, 900, MinWidth, MinHeight,
            SystemParameters.WorkArea.Width, SystemParameters.WorkArea.Height, 32);
        MinWidth = Math.Min(MinWidth, size.Width);
        MinHeight = Math.Min(MinHeight, size.Height);
        Width = size.Width;
        Height = size.Height;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_isBusy) return;
        e.Cancel = true;
        MessageBox.Show(this, "Hãy chờ thao tác hiện tại hoàn tất.", "TUNG_DIROOT — SheetLink / SheetGen",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private static string Clean(string? value) => (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
}
