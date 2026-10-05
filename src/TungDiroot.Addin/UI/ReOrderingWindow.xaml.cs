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
using TungDiroot.Addin.Parameters;
using TungDiroot.Core.Parameters;
using TungDiroot.Core.Runtime;
using TungDiroot.Presentation.UI;

namespace TungDiroot.Addin.UI;

public partial class ReOrderingWindow : Window
{
    private static ReOrderingWindow? _instance;
    private readonly Document _document;
    private readonly RevitFamilyParameterOrderExecutor _executor;
    private readonly RevitActionExternalEvent _externalEvent;
    private readonly ObservableCollection<ReOrderingPreviewRow> _rows = new();
    private IReadOnlyList<FamilyParameterOrderRow>? _edited;
    private FamilyParameterOrderPlan? _plan;
    private bool _isBusy;

    private ReOrderingWindow(UIApplication application)
    {
        _document = application.ActiveUIDocument?.Document ?? throw new InvalidOperationException("NO_ACTIVE_DOCUMENT");
        _executor = new RevitFamilyParameterOrderExecutor(_document);
        _externalEvent = new RevitActionExternalEvent();
        InitializeComponent();
        ApplyInitialWindowSize();
        PreviewGrid.ItemsSource = _rows;
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
                TaskDialog.Show("TUNG_DIROOT — ReOrdering", "Hãy chờ thao tác hiện tại hoàn tất trước khi chuyển family.");
                return;
            }
            _instance.Close();
        }
        _instance = new ReOrderingWindow(application);
        _instance.Show();
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "Excel ReOrdering (*.xlsx)|*.xlsx", AddExtension = true,
            DefaultExt = ".xlsx", FileName = "TUNG_FamilyParameterOrder.xlsx" };
        if (dialog.ShowDialog(this) != true) return;
        var path = dialog.FileName;
        RunExternal("Đang đọc toàn bộ Family Parameter…", application =>
        {
            var snapshot = _executor.ReadSnapshot(application);
            Dispatcher.BeginInvoke(new Action(() => WriteAsync(path, snapshot)));
        });
    }

    private async void WriteAsync(string path, IReadOnlyList<FamilyParameterOrderRow> snapshot)
    {
        try
        {
            StatusText.Text = "Đang ghi workbook…";
            await Task.Run(() => FamilyParameterOrderWorkbook.Write(path, snapshot));
            WorkbookPathBox.Text = path;
            SetBusy(false, "Đã xuất " + snapshot.Count + " parameter. Chỉ chỉnh TARGET_ORDER_IN_GROUP.");
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Excel ReOrdering (*.xlsx)|*.xlsx", CheckFileExists = true };
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
        var path = WorkbookPathBox.Text.Trim();
        if (!File.Exists(path))
        {
            Browse_Click(sender, e);
            path = WorkbookPathBox.Text.Trim();
            if (!File.Exists(path)) return;
        }
        SetBusy(true, "Đang đọc workbook…");
        try
        {
            var edited = await Task.Run(() => FamilyParameterOrderWorkbook.Read(path));
            RunExternal("Đang đối chiếu parameter trong family…", application =>
            {
                var current = _executor.ReadSnapshot(application);
                var plan = FamilyParameterOrderPlanner.Plan(edited, current);
                Dispatcher.BeginInvoke(new Action(() => ShowPreview(edited, plan)));
            }, true);
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void ShowPreview(IReadOnlyList<FamilyParameterOrderRow> edited, FamilyParameterOrderPlan plan)
    {
        _edited = edited;
        _plan = plan;
        _rows.Clear();
        foreach (var row in edited)
        {
            var issues = plan.Issues.Where(issue => issue.Key == null ||
                string.Equals(issue.Key, row.Key, StringComparison.OrdinalIgnoreCase)).Select(issue => issue.Message).ToArray();
            var changed = row.CurrentOrderInGroup != row.TargetOrderInGroup;
            _rows.Add(new ReOrderingPreviewRow(row.Name, row.GroupKey, row.CurrentOrderInGroup, row.TargetOrderInGroup,
                issues.Length > 0 ? "Invalid" : changed ? "Changed" : "No change",
                issues.Length > 0 ? string.Join(" ", issues) : changed ? "Sẽ đổi thứ tự trong group." : "Giữ nguyên."));
        }
        ApplyButton.IsEnabled = plan.IsReady && plan.Changes.Count > 0;
        SetBusy(false, plan.IsReady
            ? "Preview: " + plan.Changes.Count + " parameter thay đổi vị trí."
            : "Preview có " + plan.Issues.Count + " lỗi; chưa thể Apply.");
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (_edited == null || _plan == null || !_plan.IsReady || _plan.Changes.Count == 0) return;
        if (MessageBox.Show(this, "Sẽ đổi vị trí " + _plan.Changes.Count +
                " parameter. Tool tạo backup Excel trước khi Apply và Revit có một Undo.\n\nTiếp tục?",
                "TUNG_DIROOT — Xác nhận ReOrdering", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        var backup = BackupPath(WorkbookPathBox.Text);
        RunExternal("Đang backup và sắp xếp Family Parameter…", application =>
        {
            var result = _executor.Apply(application, _edited, backup);
            Dispatcher.BeginInvoke(new Action(() =>
            {
                SetBusy(false, "Đã đổi " + result.ChangedCount + " parameter. Backup: " + result.BackupPath);
                MessageBox.Show(this, "Đã hoàn tất.\n\nBackup: " + result.BackupPath,
                    "TUNG_DIROOT — ReOrdering", MessageBoxButton.OK, MessageBoxImage.Information);
                Preview_Click(this, new RoutedEventArgs());
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
                        throw new InvalidOperationException("Hãy quay lại đúng family đã mở ReOrdering.");
                    action(application);
                }
                catch (Exception exception) { Dispatcher.BeginInvoke(new Action(() => ShowError(exception))); }
            });
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void SetBusy(bool value, string status)
    {
        _isBusy = value;
        ExportButton.IsEnabled = !value;
        OpenButton.IsEnabled = !value;
        PreviewButton.IsEnabled = !value;
        ApplyButton.IsEnabled = !value && _plan?.IsReady == true && _plan.Changes.Count > 0;
        StatusText.Text = status;
    }

    private void ShowError(Exception exception)
    {
        SetBusy(false, "Không thể hoàn tất: " + Clean(exception.Message));
        MessageBox.Show(this, StatusText.Text, "TUNG_DIROOT — ReOrdering", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private static string BackupPath(string workbookPath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(workbookPath)) ?? Path.GetTempPath();
        var name = Path.GetFileNameWithoutExtension(workbookPath);
        return Path.Combine(directory, name + ".TUNG_BACKUP_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xlsx");
    }

    private void ApplyInitialWindowSize()
    {
        var size = WindowSizePolicy.Clamp(1300, 820, MinWidth, MinHeight,
            SystemParameters.WorkArea.Width, SystemParameters.WorkArea.Height, 32);
        MinWidth = Math.Min(MinWidth, size.Width); MinHeight = Math.Min(MinHeight, size.Height);
        Width = size.Width; Height = size.Height;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_isBusy) return;
        e.Cancel = true;
        MessageBox.Show(this, "Hãy chờ thao tác ReOrdering hoàn tất.", "TUNG_DIROOT", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private static string Clean(string? value) => (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
}

public sealed record ReOrderingPreviewRow(
    string Name,
    string GroupKey,
    int CurrentOrder,
    int TargetOrder,
    string Status,
    string Message);
