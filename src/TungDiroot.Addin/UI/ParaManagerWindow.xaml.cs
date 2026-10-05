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

public partial class ParaManagerWindow : Window
{
    private static ParaManagerWindow? _instance;
    private readonly Document _document;
    private readonly RevitParameterManagerExecutor _executor;
    private readonly RevitActionExternalEvent _externalEvent;
    private readonly ObservableCollection<ParaManagerPreviewRow> _preview = new();
    private ParameterManagerWorkbook? _workbook;
    private ParameterManagerPreviewPlan? _plan;
    private IReadOnlyList<SharedParameterDefinitionModel> _sharedDefinitions = Array.Empty<SharedParameterDefinitionModel>();
    private bool _isBusy;

    private ParaManagerWindow(UIApplication application)
    {
        _document = application.ActiveUIDocument?.Document ?? throw new InvalidOperationException("NO_ACTIVE_DOCUMENT");
        _executor = new RevitParameterManagerExecutor(_document);
        _externalEvent = new RevitActionExternalEvent();
        InitializeComponent();
        ApplyInitialWindowSize();
        ScopeText.Text = _executor.Scope == ParameterManagerScope.Project
            ? "Project mode — quản lý binding/group/category. Category chỉ merge thêm, không tự xóa."
            : "Family mode — quản lý shared family parameter, tên/group; dùng ReOrdering để đổi thứ tự.";
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
                TaskDialog.Show("TUNG_DIROOT — ParaManager", "Hãy chờ thao tác hiện tại hoàn tất trước khi chuyển document.");
                return;
            }
            _instance.Close();
        }
        _instance = new ParaManagerWindow(application);
        _instance.Show();
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "Excel ParaManager (*.xlsx)|*.xlsx", AddExtension = true,
            DefaultExt = ".xlsx", FileName = "TUNG_ParaManager_" + _executor.Scope + ".xlsx" };
        if (dialog.ShowDialog(this) != true) return;
        var path = dialog.FileName;
        RunExternal("Đang đọc parameter inventory…", application =>
        {
            var inventory = _executor.ReadInventory(application);
            var catalog = _executor.ReadReferenceCatalog(application, Array.Empty<SharedParameterDefinitionModel>());
            Dispatcher.BeginInvoke(new Action(() => WriteInventoryAsync(path, inventory, catalog)));
        });
    }

    private async void WriteInventoryAsync(string path, IReadOnlyList<ParameterManagerRow> inventory,
        ParameterManagerReferenceCatalog catalog)
    {
        try
        {
            StatusText.Text = "Đang ghi workbook…";
            await Task.Run(() => ParameterManagerWorkbook.Write(path, _executor.Scope, inventory, 10,
                catalog.ParameterGroupIds, catalog.CategoryKeys));
            WorkbookPathBox.Text = path;
            SetBusy(false, "Đã xuất " + inventory.Count + " parameter và 10 dòng CREATE_SHARED mẫu.");
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void BrowseShared_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Revit Shared Parameter (*.txt)|*.txt", CheckFileExists = true };
        if (dialog.ShowDialog(this) == true) SharedPathBox.Text = dialog.FileName;
    }

    private void BrowseWorkbook_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Excel ParaManager (*.xlsx)|*.xlsx", CheckFileExists = true };
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
            BrowseWorkbook_Click(sender, e);
            path = WorkbookPathBox.Text.Trim();
            if (!File.Exists(path)) return;
        }
        SetBusy(true, "Đang đọc workbook và shared definitions…");
        try
        {
            var sharedPath = SharedPathBox.Text.Trim();
            var loaded = await Task.Run(() =>
            {
                var workbook = ParameterManagerWorkbook.Read(path);
                var definitions = File.Exists(sharedPath)
                    ? new SharedParameterTextParser().Parse(sharedPath)
                    : (IReadOnlyList<SharedParameterDefinitionModel>)Array.Empty<SharedParameterDefinitionModel>();
                return (Workbook: workbook, Definitions: definitions);
            });
            RunExternal("Đang đối chiếu parameter với document…", application =>
            {
                var catalog = _executor.ReadReferenceCatalog(application, loaded.Definitions);
                var plan = ParameterManagerPlanner.Plan(loaded.Workbook, catalog);
                Dispatcher.BeginInvoke(new Action(() => ShowPreview(loaded.Workbook, loaded.Definitions, plan)));
            }, true);
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void ShowPreview(ParameterManagerWorkbook workbook, IReadOnlyList<SharedParameterDefinitionModel> definitions,
        ParameterManagerPreviewPlan plan)
    {
        _workbook = workbook;
        _sharedDefinitions = definitions;
        _plan = plan;
        _preview.Clear();
        foreach (var item in plan.Items)
            _preview.Add(new ParaManagerPreviewRow(item.ExcelRow, item.Row.Action, item.Row.Name,
                item.Row.BindingKind?.ToString() ?? "—", item.Status, item.Code, item.Message));
        var errors = plan.Items.Count(item => item.Status is ParameterManagerPreviewStatus.Invalid or ParameterManagerPreviewStatus.Conflict);
        SetBusy(false, "Preview: " + plan.ReadyCount + " Ready, " +
            plan.Items.Count(item => item.Status == ParameterManagerPreviewStatus.NoChange) + " không đổi, " + errors + " lỗi.");
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (_workbook == null || _plan == null || _plan.ReadyCount == 0) return;
        if (MessageBox.Show(this, "Sẽ áp dụng " + _plan.ReadyCount +
                " dòng Ready. Tool tạo backup Excel, mỗi dòng có transaction riêng và cả lượt có một Undo.\n\nTiếp tục?",
                "TUNG_DIROOT — Xác nhận ParaManager", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        var backup = BackupPath(WorkbookPathBox.Text);
        RunExternal("Đang backup và áp dụng parameter…", application =>
        {
            var result = _executor.Apply(application, _workbook, _sharedDefinitions, SharedPathBox.Text.Trim(), backup);
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var failed = result.Results.Where(item => !item.Success).ToArray();
                SetBusy(false, "Apply: " + (result.Results.Count - failed.Length) + " thành công, " + failed.Length +
                    " lỗi. Backup: " + result.BackupPath);
                if (failed.Length > 0)
                    MessageBox.Show(this, string.Join(Environment.NewLine, failed.Select(item =>
                        "Dòng " + item.ExcelRow + " — " + item.Name + ": " + item.Message)),
                        "TUNG_DIROOT — ParaManager", MessageBoxButton.OK, MessageBoxImage.Warning);
                else MessageBox.Show(this, "Đã hoàn tất.\n\nBackup: " + result.BackupPath,
                    "TUNG_DIROOT — ParaManager", MessageBoxButton.OK, MessageBoxImage.Information);
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
                        throw new InvalidOperationException("Hãy quay lại đúng document đã mở ParaManager.");
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
        ExportButton.IsEnabled = !value; OpenButton.IsEnabled = !value; PreviewButton.IsEnabled = !value;
        ApplyButton.IsEnabled = !value && _plan?.ReadyCount > 0;
        StatusText.Text = status;
    }

    private void ShowError(Exception exception)
    {
        SetBusy(false, "Không thể hoàn tất: " + Clean(exception.Message));
        MessageBox.Show(this, StatusText.Text, "TUNG_DIROOT — ParaManager", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private static string BackupPath(string workbookPath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(workbookPath)) ?? Path.GetTempPath();
        return Path.Combine(directory, Path.GetFileNameWithoutExtension(workbookPath) + ".TUNG_BACKUP_" +
            DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xlsx");
    }

    private void ApplyInitialWindowSize()
    {
        var size = WindowSizePolicy.Clamp(1500, 900, MinWidth, MinHeight,
            SystemParameters.WorkArea.Width, SystemParameters.WorkArea.Height, 32);
        MinWidth = Math.Min(MinWidth, size.Width); MinHeight = Math.Min(MinHeight, size.Height);
        Width = size.Width; Height = size.Height;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_isBusy) return;
        e.Cancel = true;
        MessageBox.Show(this, "Hãy chờ thao tác ParaManager hoàn tất.", "TUNG_DIROOT", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private static string Clean(string? value) => (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
}

public sealed record ParaManagerPreviewRow(
    int ExcelRow,
    ParameterManagerAction Action,
    string Name,
    string Binding,
    ParameterManagerPreviewStatus Status,
    string Code,
    string Message);
