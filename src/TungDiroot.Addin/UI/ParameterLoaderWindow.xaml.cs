using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.Win32;
using TungDiroot.Addin.Diagnostics;
using TungDiroot.Addin.Parameters;
using TungDiroot.Core.Diagnostics;
using TungDiroot.Core.Parameters;
using TungDiroot.Core.Runtime;
using TungDiroot.Core.Settings;
using TungDiroot.Presentation.UI;

namespace TungDiroot.Addin.UI;

public partial class ParameterLoaderWindow : Window
{
    private static ParameterLoaderWindow? _instance;
    private readonly Document _document;
    private readonly RevitActionExternalEvent _revitAction;
    private readonly ParameterLoaderViewModel _viewModel;
    private readonly UserSettingsStore _settingsStore = new();
    private readonly ITungDirootLogger _logger = new RollingFileLogger();

    private ParameterLoaderWindow(UIApplication uiApplication)
    {
        if (uiApplication == null) throw new ArgumentNullException(nameof(uiApplication));
        _document = uiApplication.ActiveUIDocument?.Document ?? throw new InvalidOperationException("NO_ACTIVE_DOCUMENT");
        if (_document.IsFamilyDocument) throw new InvalidOperationException("PROJECT_DOCUMENT_REQUIRED");
        InitializeComponent();
        ApplyInitialWindowSize();
        StatusFilterBox.ItemsSource = ParameterPlanStatusDisplay.FilterOptions;
        _revitAction = new RevitActionExternalEvent();
        _viewModel = new ParameterLoaderViewModel(RequestAnalyze, RequestAssign);
        DataContext = _viewModel;

        var settings = _settingsStore.Load();
        _viewModel.SharedParameterPath = settings.LastSharedParameterPath ?? string.Empty;
        _viewModel.WorkbookPath = settings.LastParameterWorkbookPath ?? string.Empty;
        _viewModel.StatusText = "Chọn nguồn rồi bấm Kiểm tra. Parameter hiện có sẽ không bị tự sửa.";
        Closing += OnClosing;
        Closed += (_, _) =>
        {
            _revitAction.Dispose();
            _instance = null;
        };
    }

    public static void ShowOrActivate(UIApplication uiApplication)
    {
        var activeDocument = uiApplication.ActiveUIDocument?.Document ?? throw new InvalidOperationException("NO_ACTIVE_DOCUMENT");
        if (activeDocument.IsFamilyDocument) throw new InvalidOperationException("PROJECT_DOCUMENT_REQUIRED");
        if (_instance != null && NativeObjectIdentity.IsSame(_instance._document, activeDocument))
        {
            if (_instance.WindowState == WindowState.Minimized) _instance.WindowState = WindowState.Normal;
            _instance.Activate();
            return;
        }
        if (_instance != null)
        {
            if (_instance._viewModel.IsBusy)
            {
                TaskDialog.Show("TUNG_DIROOT — Parameter Loader", "Hãy chờ thao tác Parameter Loader hiện tại hoàn tất trước khi chuyển model.");
                return;
            }
            _instance.Close();
        }
        _instance = new ParameterLoaderWindow(uiApplication);
        _instance.Show();
    }

    private void RequestAnalyze()
    {
        var sharedPath = _viewModel.SharedParameterPath.Trim();
        var workbookPath = _viewModel.WorkbookPath.Trim();
        if (!File.Exists(sharedPath) || !File.Exists(workbookPath))
        {
            _viewModel.StatusText = "Hãy chọn đúng file Shared Parameter và Excel cấu hình còn tồn tại.";
            return;
        }

        _viewModel.IsBusy = true;
        _viewModel.StatusText = "Đang kiểm tra definition, binding và category…";
        RaiseRevitAction(application =>
        {
            var plans = Analyze(application, sharedPath, workbookPath);
            TryLog(application, workbookPath, "ParameterLoader.Analyze", "PARAMETER_ANALYSIS_COMPLETED", Guid.Empty, null);
            foreach (var plan in plans)
            {
                TryLog(application, workbookPath, "ParameterLoader.Plan",
                    "PARAMETER_PLAN_" + plan.Status.ToString().ToUpperInvariant(),
                    plan.Definition?.Guid ?? Guid.Empty,
                    plan.Reason);
            }
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _viewModel.SetPlans(plans);
                SavePaths(sharedPath, workbookPath);
                _viewModel.IsBusy = false;
            }));
        }, "Analyze", sharedPath);
    }

    private void RequestAssign(IReadOnlyList<ParameterAssignmentPlan> plans)
    {
        if (plans.Count == 0) return;
        var total = _viewModel.ReadyCount + _viewModel.AlreadyBoundCount + _viewModel.ReviewCount + _viewModel.ErrorCount;
        var skipped = Math.Max(0, total - plans.Count);
        var answer = MessageBox.Show(this,
            $"Sẽ gán mới {plans.Count} parameter.\nBỏ qua {skipped} dòng không chọn hoặc không an toàn.\n\nTiếp tục?",
            "TUNG_DIROOT — Xác nhận gán parameter", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        var sharedPath = _viewModel.SharedParameterPath.Trim();
        var workbookPath = _viewModel.WorkbookPath.Trim();
        _viewModel.IsBusy = true;
        _viewModel.StatusText = "Đang gán Shared Parameter vào project…";
        RaiseRevitAction(application =>
        {
            TryLog(application, sharedPath, "ParameterLoader.Assign", "PARAMETER_ASSIGNMENT_STARTED", Guid.Empty, null);
            var results = new RevitParameterAssignmentExecutor().Execute(
                _document, application.Application, sharedPath, plans);
            foreach (var result in results)
            {
                TryLog(application, sharedPath, "ParameterLoader.Assign",
                    result.Outcome == ParameterAssignmentOutcome.Assigned
                        ? "PARAMETER_ASSIGNED"
                        : "PARAMETER_ASSIGNMENT_FAILED",
                    result.Plan.Definition?.Guid ?? Guid.Empty,
                    result.Error);
            }
            TryLog(application, sharedPath, "ParameterLoader.Assign", "SHARED_PARAMETER_PATH_RESTORED", Guid.Empty, null);
            IReadOnlyList<ParameterAssignmentPlan>? refreshed = null;
            string? refreshWarning = null;
            try
            {
                refreshed = Analyze(application, sharedPath, workbookPath);
            }
            catch (Exception exception)
            {
                refreshWarning = Clean(exception.Message);
                TryLogFailure(application, "Assign.Refresh", workbookPath, exception);
            }
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (refreshed != null) _viewModel.SetPlans(refreshed);
                _viewModel.ApplyBatchResult(results);
                _viewModel.IsBusy = false;
                var assigned = results.Count(result => result.Outcome == ParameterAssignmentOutcome.Assigned);
                var failed = results.Count - assigned;
                var warning = string.IsNullOrWhiteSpace(refreshWarning)
                    ? string.Empty
                    : "\n\nCảnh báo: đã gán xong nhưng không thể kiểm tra lại nguồn: " + refreshWarning;
                MessageBox.Show(this,
                    $"Hoàn tất.\n\nĐã gán mới: {assigned}\nThất bại: {failed}\nĐã tồn tại: {_viewModel.AlreadyBoundCount}\nCần xem xét: {_viewModel.ReviewCount}\nLỗi nguồn: {_viewModel.ErrorCount}{warning}",
                    "TUNG_DIROOT — Kết quả Parameter Loader", MessageBoxButton.OK,
                    failed == 0 && refreshWarning == null ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }));
        }, "Assign", sharedPath);
    }

    private IReadOnlyList<ParameterAssignmentPlan> Analyze(
        UIApplication application,
        string sharedPath,
        string workbookPath)
    {
        EnsureActiveDocument(application);
        var rules = new ParameterRuleWorkbookReader().Read(workbookPath);
        var definitions = new SharedParameterTextParser().Parse(sharedPath);
        var bindings = new RevitBindingSnapshotReader().Read(_document);
        var categories = new RevitCategoryCatalog().Read(_document);
        var groups = new RevitParameterGroupCatalog().Keys;
        return new ParameterAssignmentPlanner().Build(rules, definitions, bindings, categories, groups);
    }

    private void RaiseRevitAction(Action<UIApplication> action, string step, string sourcePath)
    {
        try
        {
            _revitAction.Raise(application =>
            {
                try
                {
                    EnsureActiveDocument(application);
                    action(application);
                }
                catch (Exception exception)
                {
                    TryLogFailure(application, step, sourcePath, exception);
                    ShowFailure(exception);
                }
            });
        }
        catch (Exception exception)
        {
            ShowFailure(exception);
        }
    }

    private void ShowFailure(Exception exception)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _viewModel.IsBusy = false;
            _viewModel.StatusText = "Không thể hoàn tất: " + Clean(exception.Message);
            MessageBox.Show(this, _viewModel.StatusText, "TUNG_DIROOT — Parameter Loader", MessageBoxButton.OK, MessageBoxImage.Error);
        }));
    }

    private void TryLogFailure(UIApplication application, string step, string sourcePath, Exception exception)
    {
        try
        {
            _logger.Write(DiagnosticEvent.ForLinkFailure(
                Guid.Empty, sourcePath, "PARAMETER_LOADER_FAILED", exception,
                typeof(ParameterLoaderWindow).Assembly.GetName().Version?.ToString() ?? "unknown",
                application.Application.VersionNumber, step));
        }
        catch
        {
            // Logging is diagnostic only and must never hide the original Revit/user-facing failure.
        }
    }

    private void TryLog(
        UIApplication application,
        string sourcePath,
        string step,
        string resultCode,
        Guid parameterGuid,
        string? message)
    {
        try
        {
            _logger.Write(new DiagnosticEvent(
                DateTimeOffset.UtcNow,
                typeof(ParameterLoaderWindow).Assembly.GetName().Version?.ToString() ?? "unknown",
                application.Application.VersionNumber,
                parameterGuid,
                sourcePath,
                step,
                resultCode,
                null,
                string.IsNullOrWhiteSpace(message) ? null : Clean(message)));
        }
        catch
        {
            // Logging is best effort; assignment and path restoration remain authoritative.
        }
    }

    private void EnsureActiveDocument(UIApplication application)
    {
        if (!NativeObjectIdentity.IsSame(_document, application.ActiveUIDocument?.Document))
            throw new InvalidOperationException("Hãy quay lại đúng model đã mở Parameter Loader rồi thử lại.");
    }

    private void SavePaths(string sharedPath, string workbookPath)
    {
        var current = _settingsStore.Load();
        _settingsStore.Save(current with
        {
            LastSharedParameterPath = sharedPath,
            LastParameterWorkbookPath = workbookPath
        });
    }

    private void BrowseSharedParameter_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Revit Shared Parameter (*.txt)|*.txt", CheckFileExists = true };
        if (dialog.ShowDialog(this) == true) _viewModel.SharedParameterPath = dialog.FileName;
    }

    private void BrowseWorkbook_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Excel cấu hình (*.xlsx;*.xlsm)|*.xlsx;*.xlsm", CheckFileExists = true };
        if (dialog.ShowDialog(this) == true) _viewModel.WorkbookPath = dialog.FileName;
    }

    private void RowCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ParameterPlanRowViewModel row }) PlansGrid.SelectedItem = row;
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
        if (!_viewModel.IsBusy) return;
        e.Cancel = true;
        MessageBox.Show(this, "Hãy chờ thao tác Parameter Loader hoàn tất trước khi đóng.",
            "TUNG_DIROOT", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private static string Clean(string? value) => (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
}
