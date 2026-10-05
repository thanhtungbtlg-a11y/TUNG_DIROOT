using System;
using System.Collections.Generic;
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
using TungDiroot.Addin.Families;
using TungDiroot.Core.Families;
using TungDiroot.Core.Runtime;
using TungDiroot.Presentation.UI;

namespace TungDiroot.Addin.UI;

public partial class FamilyReviserWindow : Window
{
    private static FamilyReviserWindow? _instance;
    private readonly Document _document;
    private readonly RevitFamilyReviserExecutor _executor;
    private readonly RevitActionExternalEvent _externalEvent;
    private readonly ObservableCollection<FamilyReviserPreviewRow> _preview = new();
    private readonly FamilyReviserPreviewState _state = new();

    private FamilyReviserWindow(UIApplication application)
    {
        _document = application.ActiveUIDocument?.Document ?? throw new InvalidOperationException("NO_ACTIVE_DOCUMENT");
        _executor = new RevitFamilyReviserExecutor(_document);
        _externalEvent = new RevitActionExternalEvent();
        InitializeComponent();
        ApplyInitialWindowSize();
        ModeText.Text = _document.IsFamilyDocument
            ? "Family Editor — các family loadable tìm thấy trong document được đánh dấu Nested."
            : "Project — xuất loadable family và toàn bộ family type; in-place family chỉ được kiểm tra, không đổi tên.";
        PreviewGrid.ItemsSource = _preview;
        WorkbookPathBox.TextChanged += PreviewInputs_Changed;
        NamingPatternBox.TextChanged += PreviewInputs_Changed;
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
            if (_instance._state.IsBusy)
            {
                TaskDialog.Show("TUNG_DIROOT — FamilyReviser", "Hãy chờ thao tác hiện tại hoàn tất trước khi chuyển document.");
                return;
            }
            _instance.Close();
        }
        _instance = new FamilyReviserWindow(application);
        _instance.Show();
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "Excel FamilyReviser (*.xlsx)|*.xlsx", AddExtension = true,
            DefaultExt = ".xlsx", FileName = "TUNG_FamilyReviser.xlsx" };
        if (dialog.ShowDialog(this) != true) return;
        var path = dialog.FileName;
        InvalidatePreview();
        RunExternal("Đang đọc Family và Type…", application =>
        {
            var inventory = _executor.ReadInventory(application);
            Dispatcher.BeginInvoke(new Action(() => WriteInventoryAsync(path, inventory)));
        });
    }

    private async void WriteInventoryAsync(string path, IReadOnlyList<FamilyReviserRow> inventory)
    {
        try
        {
            StatusText.Text = "Đang ghi workbook…";
            await Task.Run(() => FamilyReviserWorkbook.Write(path, inventory));
            WorkbookPathBox.Text = path;
            SetBusy(false, "Đã xuất " + inventory.Count + " dòng Family/Type. Chỉ chỉnh APPLY và tên đích.");
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Excel FamilyReviser (*.xlsx)|*.xlsx", CheckFileExists = true };
        if (dialog.ShowDialog(this) == true) WorkbookPathBox.Text = dialog.FileName;
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!File.Exists(WorkbookPathBox.Text)) throw new FileNotFoundException("Hãy chọn workbook đã tồn tại.");
            Process.Start(new ProcessStartInfo(WorkbookPathBox.Text) { UseShellExecute = true });
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private async void Preview_Click(object sender, RoutedEventArgs e)
    {
        InvalidatePreview();
        var path = WorkbookPathBox.Text.Trim();
        if (!File.Exists(path))
        {
            Browse_Click(sender, e);
            path = WorkbookPathBox.Text.Trim();
            if (!File.Exists(path)) return;
        }
        var pattern = NamingPatternBox.Text.Trim();
        _state.ChangeInputs(path, pattern);
        SetBusy(true, "Đang đọc workbook…");
        try
        {
            var edited = await Task.Run(() => FamilyReviserWorkbook.Read(path));
            RunExternal("Đang đối chiếu Family/Type với document…", application =>
            {
                var current = _executor.ReadInventory(application);
                var plan = FamilyReviserPlanner.Plan(edited, current, pattern);
                Dispatcher.BeginInvoke(new Action(() => ShowPreview(path, pattern, plan)));
            }, true);
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void ShowPreview(string path, string pattern, FamilyReviserPlan plan)
    {
        if (!_state.SetPreview(path, pattern, plan))
        {
            SetBusy(false, "Workbook hoặc regex đã đổi. Hãy Preview lại.");
            return;
        }
        _preview.Clear();
        foreach (var item in plan.Items)
            _preview.Add(new FamilyReviserPreviewRow(item.ExcelRow, item.Row.Action, item.Row.CurrentFamily,
                item.Row.CurrentType, item.Row.Action == FamilyReviserAction.RenameFamily ? item.Row.TargetFamily : item.Row.TargetType,
                item.Row.IsNested, item.Status, item.Code, item.Message));
        var errors = plan.Items.Count(item => item.Status is FamilyReviserPreviewStatus.Invalid or FamilyReviserPreviewStatus.Conflict);
        SetBusy(false, "Preview: " + plan.ReadyCount + " Ready, " +
            plan.Items.Count(item => item.Status == FamilyReviserPreviewStatus.NoChange) + " không đổi, " + errors + " lỗi.");
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        var confirmedPlan = _state.Plan;
        if (!_state.CanApply || confirmedPlan == null) return;
        var path = WorkbookPathBox.Text.Trim();
        var pattern = NamingPatternBox.Text.Trim();
        if (MessageBox.Show(this, "Sẽ đổi tên " + confirmedPlan.ReadyCount +
                " dòng Ready. Tool tạo backup Excel, mỗi dòng có transaction riêng và cả lượt có một Undo.\n\nTiếp tục?",
                "TUNG_DIROOT — Xác nhận FamilyReviser", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        var backup = BackupPath(path);
        RunExternal("Đang backup và đổi tên Family/Type…", application =>
        {
            var result = _executor.Apply(application, confirmedPlan, pattern, backup);
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var failed = result.Results.Where(item => !item.Success).ToArray();
                InvalidatePreview();
                SetBusy(false, "Apply: " + (result.Results.Count - failed.Length) + " thành công, " + failed.Length +
                    " lỗi. Backup: " + result.BackupPath);
                if (failed.Length > 0)
                    MessageBox.Show(this, string.Join(Environment.NewLine, failed.Select(item =>
                        "Dòng " + item.ExcelRow + ": " + item.Message)),
                        "TUNG_DIROOT — FamilyReviser", MessageBoxButton.OK, MessageBoxImage.Warning);
                else MessageBox.Show(this, "Đã hoàn tất. Hãy xuất workbook mới nếu muốn đổi tiếp.\n\nBackup: " + result.BackupPath,
                    "TUNG_DIROOT — FamilyReviser", MessageBoxButton.OK, MessageBoxImage.Information);
            }));
        });
    }

    private void RunExternal(string status, Action<UIApplication> action, bool preserveBusy = false)
    {
        if (!preserveBusy) SetBusy(true, status); else StatusText.Text = status;
        try
        {
            _externalEvent.Raise(application =>
            {
                try
                {
                    if (!NativeObjectIdentity.IsSame(_document, application.ActiveUIDocument?.Document))
                        throw new InvalidOperationException("Hãy quay lại đúng document đã mở FamilyReviser.");
                    action(application);
                }
                catch (Exception exception) { Dispatcher.BeginInvoke(new Action(() => ShowError(exception))); }
            });
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void SetBusy(bool value, string status)
    {
        _state.SetBusy(value);
        ExportButton.IsEnabled = !value; OpenButton.IsEnabled = !value; PreviewButton.IsEnabled = !value;
        ApplyButton.IsEnabled = _state.CanApply;
        BrowseButton.IsEnabled = _state.CanEditInputs;
        WorkbookPathBox.IsEnabled = _state.CanEditInputs;
        NamingPatternBox.IsEnabled = _state.CanEditInputs;
        StatusText.Text = status;
    }

    private void PreviewInputs_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!_state.ChangeInputs(WorkbookPathBox.Text, NamingPatternBox.Text)) return;
        _preview.Clear();
        ApplyButton.IsEnabled = false;
        if (!_state.IsBusy) StatusText.Text = "Workbook hoặc regex đã đổi. Hãy Preview lại.";
    }

    private void InvalidatePreview()
    {
        _state.Invalidate();
        _preview.Clear();
        ApplyButton.IsEnabled = false;
    }

    private void ShowError(Exception exception)
    {
        InvalidatePreview();
        SetBusy(false, "Không thể hoàn tất: " + Clean(exception.Message));
        MessageBox.Show(this, StatusText.Text, "TUNG_DIROOT — FamilyReviser", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private static string BackupPath(string workbookPath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(workbookPath)) ?? Path.GetTempPath();
        return Path.Combine(directory, Path.GetFileNameWithoutExtension(workbookPath) + ".TUNG_BACKUP_" +
            DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xlsx");
    }

    private void ApplyInitialWindowSize()
    {
        var size = WindowSizePolicy.Clamp(1420, 860, MinWidth, MinHeight,
            SystemParameters.WorkArea.Width, SystemParameters.WorkArea.Height, 32);
        MinWidth = Math.Min(MinWidth, size.Width); MinHeight = Math.Min(MinHeight, size.Height);
        Width = size.Width; Height = size.Height;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_state.IsBusy) return;
        e.Cancel = true;
        MessageBox.Show(this, "Hãy chờ thao tác FamilyReviser hoàn tất.", "TUNG_DIROOT", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private static string Clean(string? value) => (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
}

public sealed record FamilyReviserPreviewRow(
    int ExcelRow,
    FamilyReviserAction Action,
    string CurrentFamily,
    string CurrentType,
    string Target,
    bool IsNested,
    FamilyReviserPreviewStatus Status,
    string Code,
    string Message);
