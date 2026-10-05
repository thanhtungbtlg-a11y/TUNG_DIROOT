using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.Win32;
using TungDiroot.Addin.DataLink;
using TungDiroot.Addin.Diagnostics;
using TungDiroot.Core.DataLink;
using TungDiroot.Core.Diagnostics;
using TungDiroot.Core.Runtime;
using TungDiroot.Presentation.DataLink;
using TungDiroot.Presentation.UI;

namespace TungDiroot.Addin.UI;

public partial class DataLinkWindow : Window
{
    private static DataLinkWindow? _instance;
    private readonly Document _document;
    private readonly RevitActionExternalEvent _revitAction;
    private readonly DataLinkViewModel _viewModel;
    private readonly ITungDirootLogger _logger = new RollingFileLogger();
    private bool _cancelRequested;

    private DataLinkWindow(UIApplication uiApplication)
    {
        if (uiApplication == null) throw new ArgumentNullException(nameof(uiApplication));
        _document = uiApplication.ActiveUIDocument?.Document ?? throw new InvalidOperationException("NO_ACTIVE_DOCUMENT");
        if (_document.IsFamilyDocument) throw new InvalidOperationException("PROJECT_DOCUMENT_REQUIRED");
        InitializeComponent();
        ApplyInitialWindowSize();
        ScopeBox.ItemsSource = new[]
        {
            new ScopeOption(DataLinkScope.WholeModel, "Toàn bộ model"),
            new ScopeOption(DataLinkScope.ActiveView, "View hiện tại"),
            new ScopeOption(DataLinkScope.CurrentSelection, "Đối tượng đang chọn")
        };
        _revitAction = new RevitActionExternalEvent();
        _viewModel = new DataLinkViewModel(RequestExport, RequestApply, RequestImport);
        DataContext = _viewModel;
        Loaded += (_, _) => RequestCatalog();
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
            if (!_instance._viewModel.CanClose)
            {
                TaskDialog.Show("TUNG_DIROOT — DataLink", "Hãy chờ thao tác DataLink hiện tại hoàn tất trước khi chuyển model.");
                return;
            }
            _instance.Close();
        }
        _instance = new DataLinkWindow(uiApplication);
        _instance.Show();
    }

    private void RequestCatalog()
    {
        BeginWork("Đang nạp category và parameter từ Revit…");
        RaiseRevitAction(application =>
        {
            if (_cancelRequested) return;
            var reader = new RevitDataLinkCatalogReader((elementId, stage, exception) =>
                TryLogCatalogSkip(application, elementId, stage, exception));
            var catalog = reader.Read(application.ActiveUIDocument, _viewModel.Scope);
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_cancelRequested) return;
                _viewModel.SetCatalog(catalog);
                if (reader.SkippedElementCount > 0)
                    _viewModel.StatusText += $" Đã bỏ qua an toàn {reader.SkippedElementCount} phần tử Revit lỗi; chi tiết nằm trong Logs.";
            }));
        });
    }

    private void RequestExport(DataLinkQuery query)
    {
        if (query.Parameters.Count == 0)
        {
            _viewModel.StatusText = "Hãy chọn ít nhất một parameter trước khi xuất.";
            WorkflowTabs.SelectedIndex = 0;
            return;
        }

        var path = ChooseSavePath();
        if (path == null) return;
        BeginWork("Đang đọc dữ liệu Revit và tạo file Excel…");
        RaiseRevitAction(application =>
        {
            if (_cancelRequested) return;
            var workbook = new RevitDataLinkSnapshotReader().Read(application.ActiveUIDocument, query);
            if (_cancelRequested) return;
            DataLinkWorkbookWriter.Write(path, workbook);
            Dispatcher.BeginInvoke(new Action(() =>
            {
                WorkbookPathBox.Text = path;
                _viewModel.StatusText = $"Đã xuất {workbook.Rows.Count} dòng vào {Path.GetFileName(path)}.";
            }));
        });
    }

    private void RequestImport()
    {
        var path = ChooseOpenPath();
        if (path == null) return;

        DataLinkWorkbook workbook;
        try
        {
            workbook = DataLinkWorkbookReader.Read(path);
        }
        catch (Exception exception)
        {
            ShowFailure(exception);
            return;
        }

        WorkbookPathBox.Text = path;
        BeginWork("Đang đối chiếu Excel với model hiện tại…");
        RaiseRevitAction(application =>
        {
            if (_cancelRequested) return;
            var values = new RevitDataLinkCurrentValueReader().Read(_document, workbook);
            var plan = DataLinkChangePlanner.Create(workbook, RevitDataLinkDocumentStamp.Create(_document), values);
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_cancelRequested) return;
                _viewModel.SetPlan(plan);
                WorkflowTabs.SelectedIndex = 2;
            }));
        });
    }

    private void RequestApply(DataLinkChangePlan plan)
    {
        var answer = MessageBox.Show(this,
            $"Sẽ cập nhật {plan.Changes.Count} giá trị đã chọn. Mỗi nhóm lỗi sẽ rollback riêng và toàn bộ lượt có một Undo.\n\nTiếp tục?",
            "TUNG_DIROOT — Xác nhận DataLink", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        BeginWork("Đang áp dụng các dòng đã chọn vào Revit…");
        RaiseRevitAction(application =>
        {
            if (_cancelRequested) return;
            var results = new RevitDataLinkAssignmentExecutor().Execute(_document, plan);
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var succeeded = results.Count(result => result.Success);
                var failed = results.Count - succeeded;
                _viewModel.StatusText = $"Hoàn tất: {succeeded} giá trị đã cập nhật; {failed} thất bại.";
                MessageBox.Show(this, _viewModel.StatusText, "TUNG_DIROOT — DataLink",
                    MessageBoxButton.OK, failed == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }));
        });
    }

    private void RaiseRevitAction(Action<UIApplication> action)
    {
        _viewModel.IsExternalActionPending = true;
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
                    ShowFailure(exception);
                }
                finally
                {
                    Dispatcher.BeginInvoke(new Action(EndWork));
                }
            });
        }
        catch (Exception exception)
        {
            EndWork();
            ShowFailure(exception);
        }
    }

    private void EnsureActiveDocument(UIApplication application)
    {
        if (!NativeObjectIdentity.IsSame(_document, application.ActiveUIDocument?.Document))
            throw new InvalidOperationException("Hãy quay lại đúng model đã mở DataLink rồi thử lại.");
    }

    private void BeginWork(string status)
    {
        _cancelRequested = false;
        _viewModel.IsBusy = true;
        _viewModel.StatusText = status;
    }

    private void EndWork()
    {
        _viewModel.IsBusy = false;
        _viewModel.IsExternalActionPending = false;
        if (_cancelRequested) _viewModel.StatusText = "Đã hủy thao tác DataLink.";
    }

    private void ShowFailure(Exception exception)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _viewModel.StatusText = "Không thể hoàn tất: " + Clean(exception.Message);
            MessageBox.Show(this, _viewModel.StatusText, "TUNG_DIROOT — DataLink", MessageBoxButton.OK, MessageBoxImage.Error);
        }));
    }

    private void TryLogCatalogSkip(UIApplication application, long elementId, string stage, Exception exception)
    {
        try
        {
            _logger.Write(DiagnosticEvent.ForLinkFailure(
                Guid.Empty,
                elementId < 0 ? "element:unknown" : "element:" + elementId,
                "DATALINK_CATALOG_ELEMENT_SKIPPED",
                exception,
                typeof(DataLinkWindow).Assembly.GetName().Version?.ToString() ?? "unknown",
                application.Application.VersionNumber,
                "Catalog/" + stage));
        }
        catch
        {
            // Diagnostics are best effort and must never replace the catalog result.
        }
    }

    private string? ChooseSavePath()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Excel DataLink (*.xlsx)|*.xlsx",
            AddExtension = true,
            DefaultExt = ".xlsx",
            FileName = string.IsNullOrWhiteSpace(WorkbookPathBox.Text) ? "TUNG_DataLink.xlsx" : Path.GetFileName(WorkbookPathBox.Text)
        };
        return dialog.ShowDialog(this) == true ? dialog.FileName : null;
    }

    private string? ChooseOpenPath()
    {
        if (File.Exists(WorkbookPathBox.Text)) return WorkbookPathBox.Text;
        var dialog = new OpenFileDialog { Filter = "Excel DataLink (*.xlsx)|*.xlsx", CheckFileExists = true };
        return dialog.ShowDialog(this) == true ? dialog.FileName : null;
    }

    private void RefreshCatalog_Click(object sender, RoutedEventArgs e) => RequestCatalog();

    private void BrowseWorkbook_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Excel DataLink (*.xlsx)|*.xlsx", CheckFileExists = true };
        if (dialog.ShowDialog(this) == true) WorkbookPathBox.Text = dialog.FileName;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _cancelRequested = true;
        _viewModel.StatusText = "Đã ghi nhận yêu cầu hủy; thao tác Revit hiện tại sẽ dừng tại điểm an toàn.";
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_viewModel.RequestClose()) return;
        e.Cancel = true;
        MessageBox.Show(this, _viewModel.StatusText, "TUNG_DIROOT — DataLink", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void ApplyInitialWindowSize()
    {
        var size = WindowSizePolicy.Clamp(1500, 900, MinWidth, MinHeight,
            SystemParameters.WorkArea.Width, SystemParameters.WorkArea.Height, 32);
        MinWidth = Math.Min(MinWidth, size.Width);
        MinHeight = Math.Min(MinHeight, size.Height);
        Width = size.Width;
        Height = size.Height;
    }

    private static string Clean(string? value) => (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();

    private sealed record ScopeOption(DataLinkScope Value, string Label);
}
