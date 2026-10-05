using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using TungDiroot.Addin.Batch;
using TungDiroot.Addin.Import;
using TungDiroot.Addin.Links;
using TungDiroot.Addin.Revit;
using TungDiroot.Core.Import;
using TungDiroot.Core.Runtime;
using TungDiroot.Presentation.UI;

namespace TungDiroot.Addin.UI;

public partial class TableGenWindow : Window
{
    private static TableGenWindow? _instance;
    private readonly Document _document;
    private readonly RevitTableGenLinkSource _linkSource;
    private readonly TableGenViewModel _viewModel;
    private readonly RevitBatchCoordinator _batch;
    private readonly RevitActionExternalEvent _revitAction;
    private readonly RevitLinkManagementExecutor _linkManagement;
    private readonly TableGenSourceWatchService _sourceWatch;
    private bool _sourceRefreshPending;
    private AddExcelWindow? _addExcelWindow;

    private TableGenWindow(UIApplication uiApplication)
    {
        if (uiApplication == null) throw new ArgumentNullException(nameof(uiApplication));
        _document = uiApplication.ActiveUIDocument?.Document ?? throw new InvalidOperationException("NO_ACTIVE_DOCUMENT");
        _linkSource = new RevitTableGenLinkSource(_document);
        InitializeComponent();
        ApplyInitialWindowSize();
        StatusFilterBox.ItemsSource = Enum.GetValues(typeof(LinkStatusFilter));
        _batch = new RevitBatchCoordinator(new TableGenUpdateProcessor(expectedDocument: _document));
        _revitAction = new RevitActionExternalEvent();
        _linkManagement = new RevitLinkManagementExecutor(_document);
        _viewModel = new TableGenViewModel(
            _linkSource,
            addExcel: RequestAddExcel,
            checkSources: RequestRefresh,
            updateSelected: ids =>
            {
                if (ids.Count > 0) _batch.Enqueue(ids);
            },
            syncAll: ids =>
            {
                if (ids.Count > 0) _batch.Enqueue(ids);
            },
            pauseBatch: _batch.Pause,
            resumeBatch: _batch.Resume,
            stopBatch: _batch.RequestStop,
            relinkFolder: RelinkFolder,
            saveAutoSync: SaveAutoSync,
            openView: id => RunLinkAction((app, executor) => executor.OpenView(app, id)),
            openSource: id => RunLinkAction((app, executor) => executor.OpenSource(app, id)),
            openSourceFolder: id => RunLinkAction((app, executor) => executor.OpenSourceFolder(app, id)),
            unlinkKeepView: RequestUnlinkKeepView,
            deleteLinkAndView: RequestDeleteLinkAndView,
            changeTargetView: RequestChangeTargetView,
            duplicateConfiguration: RequestDuplicateConfiguration,
            recreateLink: RequestRecreateLink);
        _sourceWatch = new TableGenSourceWatchService(
            action => Dispatcher.BeginInvoke(action),
            OnWatchedSourcesChanged);
        _viewModel.RowsChanged += UpdateSourceWatch;
        UpdateSourceWatch();
        _batch.ProgressChanged += OnBatchProgressChanged;
        _batch.ItemCompleted += _ => _viewModel.SetRows(_linkSource.Load());
        DataContext = _viewModel;
        Closing += OnClosing;
        Closed += (_, _) =>
        {
            _addExcelWindow?.Close();
            _sourceWatch.Dispose();
            _batch.Dispose();
            _revitAction.Dispose();
            _instance = null;
        };
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

    public static void ShowOrActivate(UIApplication uiApplication)
    {
        var activeDocument = uiApplication.ActiveUIDocument?.Document ?? throw new InvalidOperationException("NO_ACTIVE_DOCUMENT");
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
                TaskDialog.Show("TUNG_DIROOT", "Hãy dừng batch của model hiện tại trước khi chuyển TableGen sang model khác.");
                return;
            }
            _instance.Close();
        }
        _instance = new TableGenWindow(uiApplication);
        _instance.Show();
    }

    private void RequestAddExcel()
    {
        RaiseUiAction(app =>
        {
            EnsureActiveDocument(app);
            var hasLegendSeed = new FilteredElementCollector(_document).OfClass(typeof(View)).Cast<View>()
                .Any(view => !view.IsTemplate && view.ViewType == ViewType.Legend);
            Dispatcher.BeginInvoke(new Action(() => ShowAddExcel(hasLegendSeed)));
        });
    }

    private void ShowAddExcel(bool hasLegendSeed, WorkbookImportRequest? initialRequest = null)
    {
        if (_addExcelWindow != null)
        {
            if (initialRequest != null) _addExcelWindow.AddImportRequest(initialRequest);
            _addExcelWindow.Activate();
            return;
        }
        _addExcelWindow = new AddExcelWindow(
            hasLegendSeed,
            (requests, complete) =>
            {
                if (!RaiseUiAction(app =>
                {
                    try
                    {
                        EnsureActiveDocument(app);
                        var results = new ManualImportExecutor().Execute(app, requests);
                        var rows = _linkSource.Load();
                        _viewModel.SetRows(rows);
                        complete(results, null);
                    }
                    catch (Exception exception)
                    {
                        complete(null, exception.Message);
                    }
                })) complete(null, "TableGen đang bận. Hãy chờ thao tác hiện tại hoàn tất rồi Import lại.");
            },
            complete => LoadStyleCatalog(complete)) { Owner = this };
        if (initialRequest != null) _addExcelWindow.AddImportRequest(initialRequest);
        _addExcelWindow.Closed += OnAddExcelClosed;
        _addExcelWindow.Show();
    }

    private void OnAddExcelClosed(object? sender, EventArgs e)
    {
        _addExcelWindow = null;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!IsVisible) return;
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Activate();
            Focus();
        }));
    }

    private void RowCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: LinkRowViewModel row })
            LinksGrid.SelectedItem = row;
    }

    private void RequestRefresh() => RaiseUiAction(app =>
    {
        EnsureActiveDocument(app);
        _viewModel.SetRows(_linkSource.Load());
    });

    private void UpdateSourceWatch() => _sourceWatch.UpdateSources(_viewModel.SourcePaths);

    private void OnWatchedSourcesChanged(IReadOnlyList<string> changedPaths)
    {
        if (changedPaths.Count == 0) return;
        _sourceRefreshPending = true;
        TryRefreshWatchedSources();
    }

    private void LoadStyleCatalog(Action<RevitStyleCatalogSnapshot?, string?> complete)
    {
        if (!RaiseUiAction(app =>
        {
            RevitStyleCatalogSnapshot? catalog = null;
            string? error = null;
            try
            {
                EnsureActiveDocument(app);
                catalog = new RevitStyleCatalogReader().Read(_document);
            }
            catch (Exception exception)
            {
                error = exception.Message;
            }
            Dispatcher.BeginInvoke(new Action(() => complete(catalog, error)));
        })) complete(null, "TableGen đang bận. Hãy chờ thao tác hiện tại hoàn tất rồi thử lại.");
    }

    private void OnBatchProgressChanged(TungDiroot.Core.Batch.BatchProgress progress)
    {
        _viewModel.SetBatchProgress(progress);
        if (!_viewModel.IsBusy) Dispatcher.BeginInvoke(new Action(TryRefreshWatchedSources));
    }

    private void TryRefreshWatchedSources()
    {
        if (!_sourceRefreshPending || _viewModel.IsBusy || !IsVisible) return;
        _sourceRefreshPending = false;
        RequestRefresh();
    }

    private void RelinkFolder()
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Chọn thư mục mới chứa các file Excel cần Relink",
            ShowNewFolderButton = false
        };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
        var folder = dialog.SelectedPath;
        RaiseUiAction(app =>
        {
            EnsureActiveDocument(app);
            using var transaction = new Transaction(_document, "TUNG_DIROOT - Relink Excel folder");
            transaction.Start();
            try
            {
                new RelinkService().ApplyFolder(_document, folder);
                transaction.Commit();
                _viewModel.SetRows(_linkSource.Load());
            }
            catch
            {
                if (transaction.GetStatus() == TransactionStatus.Started) transaction.RollBack();
                throw;
            }
        });
    }

    private void SaveAutoSync(IReadOnlyDictionary<Guid, bool> values)
    {
        RaiseUiAction(app =>
        {
            EnsureActiveDocument(app);
            var registry = new RevitLinkRegistry();
            using var transaction = new Transaction(_document, "TUNG_DIROOT - Save Auto Sync");
            transaction.Start();
            try
            {
                var updated = registry.Load(_document).Links.Select(link =>
                    values.TryGetValue(link.LinkId, out var enabled) ? link with { AutoSync = enabled } : link).ToArray();
                foreach (var link in updated)
                    if (_document.GetElement(link.TargetViewUniqueId) is View view) ViewLinkMarker.Write(view, link);
                registry.Save(_document, new TungDiroot.Core.Links.LinkRegistryModel(updated));
                transaction.Commit();
                _viewModel.SetRows(_linkSource.Load());
            }
            catch
            {
                if (transaction.GetStatus() == TransactionStatus.Started) transaction.RollBack();
                throw;
            }
        });
    }

    private void RequestUnlinkKeepView(Guid linkId)
    {
        var answer = MessageBox.Show(this,
            "Bỏ liên kết Excel nhưng giữ nguyên View và toàn bộ nội dung bảng hiện tại?",
            "TUNG_DIROOT — Unlink", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;
        RunLinkAction((app, executor) => executor.UnlinkKeepView(app, linkId));
    }

    private void RequestDeleteLinkAndView(Guid linkId)
    {
        var answer = MessageBox.Show(this,
            "Xóa liên kết Excel và View đích? Thao tác này có thể hoàn tác bằng Undo trong Revit.",
            "TUNG_DIROOT — Xóa link + View", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;
        RunLinkAction((app, executor) => executor.DeleteLinkAndView(app, linkId));
    }

    private void RequestRecreateLink(Guid linkId)
    {
        var answer = MessageBox.Show(this,
            "Dựng lại bảng từ Excel cho liên kết đã chọn? Chỉnh sửa thủ công trên nội dung liên kết có thể mất. " +
            "Chỉ các phần tử được gắn thẻ của liên kết này bị xóa; View và phần tử không gắn thẻ được giữ. " +
            "Với Schedule, vùng header của bảng liên kết sẽ được dựng lại. " +
            "Chế độ cập nhật đã lưu không thay đổi. Có thể Undo trong Revit.",
            "TUNG_DIROOT — Dựng lại bảng", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;
        RunLinkAction((app, executor) =>
        {
            var result = executor.RecreateLink(app, linkId);
            TaskDialog.Show("TUNG_DIROOT — Dựng lại bảng",
                result.Succeeded && !result.Skipped
                    ? "Đã dựng lại bảng. Chế độ cập nhật đã lưu được giữ nguyên."
                    : result.ResultCode + (string.IsNullOrWhiteSpace(result.Message) ? string.Empty : ": " + result.Message));
        });
    }

    private void RequestDuplicateConfiguration(Guid linkId)
    {
        RaiseUiAction(app =>
        {
            EnsureActiveDocument(app);
            var request = _linkManagement.CreateDuplicateRequest(app, linkId);
            var hasLegendSeed = new FilteredElementCollector(_document).OfClass(typeof(View)).Cast<View>()
                .Any(view => !view.IsTemplate && view.ViewType == ViewType.Legend);
            Dispatcher.BeginInvoke(new Action(() => ShowAddExcel(hasLegendSeed, request)));
        });
    }

    private void RequestChangeTargetView(Guid linkId)
    {
        RaiseUiAction(app =>
        {
            EnsureActiveDocument(app);
            var targets = _linkManagement.ReadTargetViews(app, linkId);
            Dispatcher.BeginInvoke(new Action(() => ShowTargetViewPicker(linkId, targets)));
        });
    }

    private void ShowTargetViewPicker(Guid linkId, IReadOnlyList<TungDiroot.Core.Links.LinkTargetViewOption> targets)
    {
        if (targets.Count == 0)
        {
            MessageBox.Show(this,
                "Không có View tương thích đang trống liên kết. Đổi View đích hiện hỗ trợ Drafting ↔ Drafting hoặc Legend ↔ Legend; Schedule chưa hỗ trợ.",
                "TUNG_DIROOT", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var picker = new TargetViewPickerWindow(targets) { Owner = this };
        if (picker.ShowDialog() != true || picker.SelectedTarget == null) return;
        var answer = MessageBox.Show(this,
            $"Dựng lại bảng ở View “{picker.SelectedTarget.Name}” và gỡ nội dung liên kết khỏi View cũ?",
            "TUNG_DIROOT — Đổi View đích", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;
        var targetId = picker.SelectedTarget.UniqueId;
        RunLinkAction((app, executor) => executor.ChangeTargetView(app, linkId, targetId));
    }

    private void RunLinkAction(Action<UIApplication, RevitLinkManagementExecutor> action)
    {
        RaiseUiAction(app =>
        {
            action(app, _linkManagement);
            _viewModel.SetRows(_linkSource.Load());
        });
    }

    private void EnsureActiveDocument(UIApplication application)
    {
        if (!NativeObjectIdentity.IsSame(_document, application.ActiveUIDocument?.Document))
            throw new InvalidOperationException("DOCUMENT_NOT_ACTIVE");
    }

    private bool RaiseUiAction(Action<UIApplication> action)
    {
        if (!_viewModel.TryBeginUiAction()) return false;
        try
        {
            _revitAction.Raise(app =>
            {
                try { action(app); }
                catch (Exception exception) { TaskDialog.Show("TUNG_DIROOT", exception.Message); }
                finally
                {
                    _viewModel.CompleteUiAction();
                    Dispatcher.BeginInvoke(new Action(TryRefreshWatchedSources));
                }
            });
            return true;
        }
        catch
        {
            _viewModel.CompleteUiAction();
            throw;
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_viewModel.IsBusy) return;
        e.Cancel = true;
        MessageBox.Show(this, "Hãy bấm Dừng và chờ view hiện tại hoàn tất trước khi đóng TableGen.", "TUNG_DIROOT",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void DocumentLink_Click(object sender, RoutedEventArgs e) =>
        RaiseUiAction(DocumentLinkWindow.ShowOrActivate);
}
