using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.Win32;
using TungDiroot.Addin.Filter;
using TungDiroot.Core.Filter;
using TungDiroot.Core.Runtime;
using TungDiroot.Presentation.Filter;
using TungDiroot.Presentation.UI;

namespace TungDiroot.Addin.UI;

public partial class FilterWindow : Window
{
    private static FilterWindow? _instance;
    private readonly Document _document;
    private readonly string _documentIdentity;
    private readonly RevitActionExternalEvent _revitAction;
    private readonly FilterProfileRepository _profiles = new();
    private readonly FilterViewModel _viewModel;
    private readonly List<FilterElementSnapshot> _snapshots = new();
    private RevitFilterSnapshotBatch? _batch;
    private FilterQuery? _activeQuery;
    private bool _cancelRequested;

    private FilterWindow(UIApplication application)
    {
        _document = application.ActiveUIDocument?.Document ?? throw new InvalidOperationException("NO_ACTIVE_DOCUMENT");
        if (_document.IsFamilyDocument) throw new InvalidOperationException("PROJECT_DOCUMENT_REQUIRED");
        _documentIdentity = RevitFilterDocumentIdentity.Create(_document);
        InitializeComponent();
        ApplyInitialWindowSize();
        ScopeBox.ItemsSource = new[]
        {
            new EnumOption<FilterScope>(FilterScope.WholeModel, "Toàn bộ model"),
            new EnumOption<FilterScope>(FilterScope.ActiveView, "View hiện tại"),
            new EnumOption<FilterScope>(FilterScope.CurrentSelection, "Đối tượng đang chọn")
        };
        JoinBox.ItemsSource = new[]
        {
            new EnumOption<FilterJoin>(FilterJoin.And, "Tất cả điều kiện (AND)"),
            new EnumOption<FilterJoin>(FilterJoin.Or, "Bất kỳ điều kiện (OR)")
        };
        _revitAction = new RevitActionExternalEvent();
        _viewModel = new FilterViewModel(RequestRun, RequestCancel, RequestResultAction, SaveProfile, _documentIdentity);
        DataContext = _viewModel;
        Loaded += (_, _) =>
        {
            RefreshProfiles();
            RequestCatalog();
        };
        Closing += OnClosing;
        Closed += (_, _) =>
        {
            _revitAction.Dispose();
            _instance = null;
        };
    }

    public static void ShowOrActivate(UIApplication application)
    {
        var document = application.ActiveUIDocument?.Document ?? throw new InvalidOperationException("NO_ACTIVE_DOCUMENT");
        if (document.IsFamilyDocument) throw new InvalidOperationException("PROJECT_DOCUMENT_REQUIRED");
        if (_instance != null && NativeObjectIdentity.IsSame(_instance._document, document))
        {
            if (_instance.WindowState == WindowState.Minimized) _instance.WindowState = WindowState.Normal;
            _instance.Activate();
            return;
        }
        if (_instance != null)
        {
            if (!_instance._viewModel.CanClose)
            {
                _instance._viewModel.StatusText = "Hãy hủy và chờ bộ lọc hiện tại dừng trước khi chuyển model.";
                _instance.Activate();
                return;
            }
            _instance.Close();
        }
        _instance = new FilterWindow(application);
        _instance.Show();
    }

    private void RequestCatalog()
    {
        _viewModel.IsBusy = true;
        _viewModel.StatusText = "Đang nạp category, family, type và parameter…";
        RaiseRevitAction(application =>
        {
            var catalog = new RevitFilterCatalogReader().Read(application.ActiveUIDocument, _viewModel.Scope);
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _viewModel.SetCatalog(catalog);
                _viewModel.IsBusy = false;
            }));
        });
    }

    private void RequestRun(FilterQuery query)
    {
        _cancelRequested = false;
        _activeQuery = query;
        _snapshots.Clear();
        _batch = null;
        RaiseRevitAction(application =>
        {
            _batch = RevitFilterSnapshotBatch.Create(application.ActiveUIDocument, query);
            ProcessBatchSlice(application);
        });
    }

    private void ProcessBatchSlice(UIApplication application)
    {
        if (_batch == null || _activeQuery == null) return;
        if (_cancelRequested) _batch.Cancel();
        var slice = _batch.ProcessNext(application, _activeQuery, () => _cancelRequested);
        _snapshots.AddRange(slice.Snapshots);
        Dispatcher.BeginInvoke(new Action(() => _viewModel.SetProgress(slice.ProcessedCount, slice.TotalCount)));
        if (slice.Status == FilterBatchStatus.StaleDocument)
        {
            Dispatcher.BeginInvoke(new Action(() => _viewModel.Fail("Model đang hoạt động đã thay đổi. Hãy chạy lại bộ lọc.")));
            return;
        }
        if (slice.Status == FilterBatchStatus.Cancelled)
        {
            Dispatcher.BeginInvoke(new Action(_viewModel.CompleteCancelled));
            return;
        }
        if (slice.HasMore)
        {
            Dispatcher.BeginInvoke(new Action(QueueNextBatch));
            return;
        }
        EvaluateDetachedResults();
    }

    private void QueueNextBatch()
    {
        if (_cancelRequested)
        {
            _batch?.Cancel();
            _viewModel.CompleteCancelled();
            return;
        }
        RaiseRevitAction(ProcessBatchSlice);
    }

    private void EvaluateDetachedResults()
    {
        var query = _activeQuery!;
        var snapshots = _snapshots.ToArray();
        Task.Run(() => FilterRuleEvaluator.Evaluate(snapshots, query))
            .ContinueWith(task => Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_cancelRequested)
                {
                    _viewModel.CompleteCancelled();
                    return;
                }
                if (task.IsFaulted)
                {
                    _viewModel.Fail(Clean(task.Exception?.GetBaseException().Message));
                    return;
                }
                _viewModel.SetResults(task.Result);
            })));
    }

    private void RequestCancel()
    {
        _cancelRequested = true;
        _batch?.Cancel();
    }

    private void RequestResultAction(FilterResultAction action, IReadOnlyList<FilterMatch> matches)
    {
        if (action == FilterResultAction.ExportExcel)
        {
            ExportResults(matches);
            return;
        }
        var uniqueIds = matches.Select(match => match.Element.UniqueId).ToArray();
        _viewModel.IsBusy = true;
        _viewModel.StatusText = "Đang thực hiện hành động trong Revit…";
        RaiseRevitAction(application =>
        {
            var actions = new RevitFilterActions();
            switch (action)
            {
                case FilterResultAction.SelectAndShow:
                    actions.SelectAndShow(application, _documentIdentity, uniqueIds);
                    break;
                case FilterResultAction.TemporaryIsolate:
                    actions.TemporaryIsolate(application, _documentIdentity, uniqueIds);
                    break;
                case FilterResultAction.TemporaryHide:
                    actions.TemporaryHide(application, _documentIdentity, uniqueIds);
                    break;
                case FilterResultAction.ResetTemporaryIsolation:
                    actions.ResetTemporaryIsolation(application, _documentIdentity);
                    break;
                case FilterResultAction.SaveSelectionSet:
                    actions.SaveSelectionSet(application, _documentIdentity, _viewModel.ProfileName, uniqueIds);
                    break;
                case FilterResultAction.Visualize:
                    var profile = _viewModel.CreateProfile() ?? throw new InvalidOperationException(_viewModel.ValidationText);
                    new RevitFilterVisualization().Update(application, _documentIdentity, profile.Name, uniqueIds,
                        profile.GroupParameterStableId, profile.PaletteHex);
                    break;
                default:
                    throw new InvalidOperationException("Hành động Filter không được hỗ trợ.");
            }
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _viewModel.IsBusy = false;
                _viewModel.StatusText = $"Đã xử lý {uniqueIds.Length:N0} phần tử.";
            }));
        });
    }

    private void ExportResults(IReadOnlyList<FilterMatch> matches)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Excel TUNG Filter (*.xlsx)|*.xlsx",
            AddExtension = true,
            DefaultExt = ".xlsx",
            FileName = "TUNG_Filter_Results.xlsx"
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var stableIds = matches.SelectMany(match => match.Element.Values.Keys)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var columns = stableIds.Select(id => new FilterExportColumn(
                id,
                _viewModel.Parameters.FirstOrDefault(parameter =>
                    string.Equals(parameter.StableId, id, StringComparison.OrdinalIgnoreCase))?.Name ?? id)).ToArray();
            new RevitFilterActions().ExportExcel(dialog.FileName, matches, columns);
            _viewModel.StatusText = $"Đã xuất {matches.Count:N0} phần tử vào {Path.GetFileName(dialog.FileName)}.";
        }
        catch (Exception exception)
        {
            _viewModel.Fail(Clean(exception.Message));
        }
    }

    private void SaveProfile(FilterProfile profile)
    {
        try
        {
            _profiles.Save(profile);
            RefreshProfiles();
            _viewModel.StatusText = $"Đã lưu profile “{profile.Name}”.";
        }
        catch (Exception exception)
        {
            _viewModel.Fail(Clean(exception.Message));
        }
    }

    private void RefreshProfiles()
    {
        var loaded = _profiles.LoadAll();
        ProfileBox.ItemsSource = loaded.Where(item => item.IsSuccess).Select(item => item.Profile).ToArray();
        var failed = loaded.Count(item => !item.IsSuccess);
        if (failed > 0) _viewModel.StatusText = $"Có {failed} profile lỗi đã được bỏ qua.";
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
                    Dispatcher.BeginInvoke(new Action(() => _viewModel.Fail(Clean(exception.Message))));
                }
                finally
                {
                    Dispatcher.BeginInvoke(new Action(() => _viewModel.IsExternalActionPending = false));
                }
            });
        }
        catch (Exception exception)
        {
            _viewModel.IsExternalActionPending = false;
            _viewModel.Fail(Clean(exception.Message));
        }
    }

    private void EnsureActiveDocument(UIApplication application)
    {
        var activeDocument = application.ActiveUIDocument?.Document;
        if (!NativeObjectIdentity.IsSame(_document, activeDocument) || activeDocument == null ||
            !_viewModel.IsCurrentDocument(RevitFilterDocumentIdentity.Create(activeDocument)))
            throw new InvalidOperationException("Hãy quay lại đúng model đã mở TUNG Filter rồi thử lại.");
    }

    private void RefreshCatalog_Click(object sender, RoutedEventArgs e) => RequestCatalog();

    private void LoadProfile_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileBox.SelectedItem is FilterProfile profile) _viewModel.LoadProfile(profile);
        else _viewModel.StatusText = "Hãy chọn một profile để nạp.";
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_viewModel.RequestClose()) return;
        e.Cancel = true;
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

    private static string Clean(string? value) =>
        (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();

    private sealed record EnumOption<T>(T Value, string Label);
}
