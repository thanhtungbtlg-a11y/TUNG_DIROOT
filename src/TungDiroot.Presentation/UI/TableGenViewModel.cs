using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using TungDiroot.Core.Batch;
using TungDiroot.Presentation.Links;

namespace TungDiroot.Presentation.UI;

public sealed class TableGenViewModel : BindableBase
{
    private readonly ITableGenLinkSource _source;
    private IReadOnlyList<LinkRowViewModel> _allRows = Array.Empty<LinkRowViewModel>();
    private string _searchText = string.Empty;
    private LinkStatusFilter _statusFilter;
    private bool _isBusy;
    private bool _uiActionActive;
    private bool _batchActive;
    private string _batchStatusText = "Sẵn sàng";

    public TableGenViewModel(
        ITableGenLinkSource source,
        Action? addExcel = null,
        Action? checkSources = null,
        Action<IReadOnlyList<Guid>>? updateSelected = null,
        Action<IReadOnlyList<Guid>>? syncAll = null,
        Action? pauseBatch = null,
        Action? resumeBatch = null,
        Action? stopBatch = null,
        Action? relinkFolder = null,
        Action<IReadOnlyDictionary<Guid, bool>>? saveAutoSync = null,
        Action<Guid>? openView = null,
        Action<Guid>? openSource = null,
        Action<Guid>? openSourceFolder = null,
        Action<Guid>? unlinkKeepView = null,
        Action<Guid>? deleteLinkAndView = null,
        Action<Guid>? changeTargetView = null,
        Action<Guid>? duplicateConfiguration = null,
        Action<Guid>? recreateLink = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        AddExcelCommand = new RelayCommand(addExcel ?? (() => { }), () => !IsBusy);
        CheckSourcesCommand = new RelayCommand(checkSources ?? (() => { }), () => !IsBusy);
        UpdateSelectedCommand = new RelayCommand(() => updateSelected?.Invoke(SelectedLinkIds), () => !IsBusy);
        SyncAllCommand = new RelayCommand(() =>
        {
            var ids = AllLinkIds;
            if (ids.Count == 0)
            {
                BatchStatusText = "Không có liên kết Excel để sync.";
                return;
            }
            syncAll?.Invoke(ids);
        }, () => !IsBusy);
        PauseBatchCommand = new RelayCommand(pauseBatch ?? (() => { }), () => _batchActive);
        ResumeBatchCommand = new RelayCommand(resumeBatch ?? (() => { }), () => _batchActive);
        StopBatchCommand = new RelayCommand(stopBatch ?? (() => { }), () => _batchActive);
        RelinkFolderCommand = new RelayCommand(relinkFolder ?? (() => { }), () => !IsBusy);
        SaveAutoSyncCommand = new RelayCommand(() => saveAutoSync?.Invoke(AutoSyncValues), () => !IsBusy);
        OpenViewCommand = CreateLinkAction(openView);
        OpenSourceCommand = CreateLinkAction(openSource);
        OpenSourceFolderCommand = CreateLinkAction(openSourceFolder);
        UnlinkKeepViewCommand = CreateLinkAction(unlinkKeepView);
        DeleteLinkAndViewCommand = CreateLinkAction(deleteLinkAndView);
        ChangeTargetViewCommand = CreateLinkAction(changeTargetView);
        DuplicateConfigurationCommand = CreateLinkAction(duplicateConfiguration);
        RecreateLinkCommand = CreateRecreateLinkAction(recreateLink);
        RefreshRows();
    }

    public ObservableCollection<LinkRowViewModel> VisibleLinks { get; } = new();
    public event Action? RowsChanged;
    public RelayCommand AddExcelCommand { get; }
    public RelayCommand CheckSourcesCommand { get; }
    public RelayCommand UpdateSelectedCommand { get; }
    public RelayCommand SyncAllCommand { get; }
    public RelayCommand PauseBatchCommand { get; }
    public RelayCommand ResumeBatchCommand { get; }
    public RelayCommand StopBatchCommand { get; }
    public RelayCommand RelinkFolderCommand { get; }
    public RelayCommand SaveAutoSyncCommand { get; }
    public RelayCommand OpenViewCommand { get; }
    public RelayCommand OpenSourceCommand { get; }
    public RelayCommand OpenSourceFolderCommand { get; }
    public RelayCommand UnlinkKeepViewCommand { get; }
    public RelayCommand DeleteLinkAndViewCommand { get; }
    public RelayCommand ChangeTargetViewCommand { get; }
    public RelayCommand DuplicateConfigurationCommand { get; }
    public RelayCommand RecreateLinkCommand { get; }
    public string BatchStatusText { get => _batchStatusText; private set => SetProperty(ref _batchStatusText, value); }
    public string SearchText { get => _searchText; set { if (SetProperty(ref _searchText, value ?? string.Empty)) ApplyFilter(); } }
    public LinkStatusFilter StatusFilter { get => _statusFilter; set { if (SetProperty(ref _statusFilter, value)) ApplyFilter(); } }
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (!SetProperty(ref _isBusy, value)) return;
            AddExcelCommand.RaiseCanExecuteChanged();
            CheckSourcesCommand.RaiseCanExecuteChanged();
            UpdateSelectedCommand.RaiseCanExecuteChanged();
            SyncAllCommand.RaiseCanExecuteChanged();
            PauseBatchCommand.RaiseCanExecuteChanged();
            ResumeBatchCommand.RaiseCanExecuteChanged();
            StopBatchCommand.RaiseCanExecuteChanged();
            RelinkFolderCommand.RaiseCanExecuteChanged();
            SaveAutoSyncCommand.RaiseCanExecuteChanged();
            RaiseLinkActionCanExecuteChanged();
        }
    }
    public int ReadyCount => _allRows.Count(item => item.Status == LinkStatus.Ready);
    public int ChangedCount => _allRows.Count(item => item.Status == LinkStatus.Changed);
    public int MissingCount => _allRows.Count(item => item.Status == LinkStatus.Missing);
    public int ErrorCount => _allRows.Count(item => item.Status == LinkStatus.Error);
    public IReadOnlyList<Guid> AllLinkIds => _allRows.Select(row => row.LinkId).ToArray();
    public IReadOnlyList<Guid> SelectedLinkIds => VisibleLinks.Where(row => row.IsSelected).Select(row => row.LinkId).ToArray();
    public IReadOnlyList<string> SourcePaths => _allRows.Select(row => row.SourcePath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    public IReadOnlyDictionary<Guid, bool> AutoSyncValues => _allRows.ToDictionary(row => row.LinkId, row => row.AutoSync);

    public bool TryBeginUiAction()
    {
        if (IsBusy) return false;
        _uiActionActive = true;
        IsBusy = true;
        return true;
    }

    public void CompleteUiAction()
    {
        _uiActionActive = false;
        IsBusy = _batchActive;
    }

    public void RefreshRows()
    {
        SetRows(_source.Load() ?? Array.Empty<TableGenLinkItem>());
    }

    public void SetRows(IReadOnlyList<TableGenLinkItem> links)
    {
        if (links == null) throw new ArgumentNullException(nameof(links));
        var previous = _allRows.ToDictionary(row => row.LinkId);
        foreach (var oldRow in _allRows) oldRow.PropertyChanged -= OnRowPropertyChanged;
        _allRows = links.Select(item =>
        {
            var row = new LinkRowViewModel(item);
            if (previous.TryGetValue(item.LinkId, out var old)) row.IsSelected = old.IsSelected;
            row.PropertyChanged += OnRowPropertyChanged;
            return row;
        }).ToArray();
        ApplyFilter();
        RaiseCounts();
        RaisePropertyChanged(nameof(SourcePaths));
        RowsChanged?.Invoke();
    }

    public void SetBatchProgress(BatchProgress progress)
    {
        if (progress == null) throw new ArgumentNullException(nameof(progress));
        _batchActive = progress.RunState is BatchRunState.Running or BatchRunState.RunningCurrentThenPause or BatchRunState.Paused or BatchRunState.StopRequested;
        IsBusy = _uiActionActive || _batchActive;
        PauseBatchCommand.RaiseCanExecuteChanged();
        ResumeBatchCommand.RaiseCanExecuteChanged();
        StopBatchCommand.RaiseCanExecuteChanged();
        BatchStatusText = $"{progress.RunState}: {progress.Completed}/{progress.Total} — thành công {progress.Succeeded}, lỗi {progress.Failed}, bỏ qua {progress.Skipped}";
    }

    private void ApplyFilter()
    {
        LinkFilter.Apply(_allRows, SearchText, StatusFilter, VisibleLinks);
        RaisePropertyChanged(nameof(SelectedLinkIds));
        RaiseLinkActionCanExecuteChanged();
    }

    private void RaiseCounts()
    {
        RaisePropertyChanged(nameof(ReadyCount));
        RaisePropertyChanged(nameof(ChangedCount));
        RaisePropertyChanged(nameof(MissingCount));
        RaisePropertyChanged(nameof(ErrorCount));
    }

    private RelayCommand CreateLinkAction(Action<Guid>? action) => new(
        () =>
        {
            var ids = SelectedLinkIds;
            if (ids.Count == 1) action?.Invoke(ids[0]);
        },
        () => !IsBusy && SelectedLinkIds.Count == 1);

    private RelayCommand CreateRecreateLinkAction(Action<Guid>? action)
    {
        bool CanRecreate() => action != null && !IsBusy && SelectedLinkIds.Count == 1 &&
            _allRows.Count(row => row.IsSelected) == 1;
        return new RelayCommand(() =>
        {
            if (CanRecreate()) action!(SelectedLinkIds[0]);
        }, CanRecreate);
    }

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(LinkRowViewModel.IsSelected), StringComparison.Ordinal)) return;
        RaisePropertyChanged(nameof(SelectedLinkIds));
        RaiseLinkActionCanExecuteChanged();
    }

    private void RaiseLinkActionCanExecuteChanged()
    {
        OpenViewCommand.RaiseCanExecuteChanged();
        OpenSourceCommand.RaiseCanExecuteChanged();
        OpenSourceFolderCommand.RaiseCanExecuteChanged();
        UnlinkKeepViewCommand.RaiseCanExecuteChanged();
        DeleteLinkAndViewCommand.RaiseCanExecuteChanged();
        ChangeTargetViewCommand.RaiseCanExecuteChanged();
        DuplicateConfigurationCommand.RaiseCanExecuteChanged();
        RecreateLinkCommand.RaiseCanExecuteChanged();
    }
}
