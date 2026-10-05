using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using TungDiroot.Addin.Visibility;
using TungDiroot.Core.Runtime;
using TungDiroot.Core.Visibility;
using TungDiroot.Presentation.UI;

namespace TungDiroot.Addin.UI;

public partial class BulkVgWindow : Window
{
    private static BulkVgWindow? _instance;
    private readonly Document _document;
    private readonly RevitBulkVgExecutor _executor;
    private readonly RevitActionExternalEvent _externalEvent;
    private readonly ObservableCollection<BulkVgTargetRow> _targets = new();
    private readonly ObservableCollection<BulkVgItemRow> _items = new();
    private readonly ObservableCollection<BulkVgPreviewResult> _preview = new();
    private bool _isBusy;

    private BulkVgWindow(UIApplication application)
    {
        _document = application.ActiveUIDocument?.Document ?? throw new InvalidOperationException("NO_ACTIVE_DOCUMENT");
        _executor = new RevitBulkVgExecutor(_document);
        _externalEvent = new RevitActionExternalEvent();
        InitializeComponent();
        ApplyInitialWindowSize();
        ModeBox.ItemsSource = new[]
        {
            new BulkVgModeDescriptor(BulkVgApplyMode.Merge, "Merge",
                "Merge: cập nhật Filter đã chọn, thêm Filter chưa có; giữ Filter khác và thứ tự hiện tại. Mọi chế độ chỉ sao chép nhóm thuộc tính đã bật."),
            new BulkVgModeDescriptor(BulkVgApplyMode.Replace, "Replace",
                "Replace: chỉ các Filter tích chọn trở thành bộ Filter đích theo thứ tự nguồn; xóa Filter ngoài lựa chọn. Category và Filter đã có giữ nhóm thuộc tính chưa bật. Không chọn Filter thì không đổi danh sách Filter."),
            new BulkVgModeDescriptor(BulkVgApplyMode.UpdateExisting, "Update Existing",
                "Update Existing: chỉ cập nhật Filter tích chọn đã có ở đích; bỏ qua Filter chưa có, không thêm/xóa/đổi thứ tự. Category vẫn cập nhật nhóm thuộc tính đã bật.")
        };
        ModeBox.SelectedIndex = 0;
        TargetsGrid.ItemsSource = _targets;
        ItemsGrid.ItemsSource = _items;
        PreviewGrid.ItemsSource = _preview;
        Closing += OnClosing;
        Closed += (_, _) => { _externalEvent.Dispose(); _instance = null; };
        LoadCatalog();
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
                TaskDialog.Show("TUNG_DIROOT", "Hãy chờ Bulk V/G hoàn tất trước khi chuyển model.");
                return;
            }
            _instance.Close();
        }
        _instance = new BulkVgWindow(application);
        _instance.Show();
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

    private void LoadCatalog()
    {
        RunExternal("Đang đọc View, Template và Sheet…", application =>
        {
            var catalog = _executor.ReadCatalog(application);
            Dispatcher.BeginInvoke(new Action(() =>
            {
                SourceViewBox.ItemsSource = catalog.Sources;
                _targets.Clear();
                foreach (var target in catalog.Targets) _targets.Add(new BulkVgTargetRow(target));
                SetBusy(false, catalog.Sources.Count + " nguồn, " + catalog.Targets.Count + " đích.");
                if (catalog.Sources.Count > 0) SourceViewBox.SelectedIndex = 0;
            }));
        });
    }

    private void SourceView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isBusy || SourceViewBox.SelectedItem is not BulkVgViewDescriptor source) return;
        RunExternal("Đang đọc Category và Filter nguồn…", application =>
        {
            var items = _executor.ReadSourceItems(application, source.Id);
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _items.Clear();
                foreach (var item in items) _items.Add(new BulkVgItemRow(item));
                _preview.Clear();
                SetBusy(false, items.Count + " Category/Filter từ nguồn.");
            }));
        });
    }

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var request = BuildRequest();
            RunExternal("Đang so sánh V/G…", application =>
            {
                var result = _executor.Preview(application, request);
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    _preview.Clear();
                    foreach (var row in result) _preview.Add(row);
                    SetBusy(false, result.Count + " dòng khác biệt đã kiểm tra.");
                }));
            });
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var request = BuildRequest();
            if (request.Mode == BulkVgApplyMode.Replace && request.Items.Any(item => item.Kind == BulkVgItemKind.Filter) &&
                MessageBox.Show(this,
                    "Replace sẽ xóa mọi Filter ngoài các Filter tích chọn ở từng đích và sắp xếp lại theo thứ tự nguồn. Nhóm thuộc tính chưa bật của Category/Filter đã có được giữ. Hãy kiểm tra Xem trước trước khi tiếp tục.\n\nApply Replace?",
                    "TUNG_DIROOT — Replace Filter", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;
            RunExternal("Đang Apply V/G…", application =>
            {
                var result = _executor.Apply(application, request);
                Dispatcher.BeginInvoke(new Action(() => ShowApplyResults(result, "Apply")));
            });
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        RunExternal("Đang khôi phục V/G…", application =>
        {
            IReadOnlyList<BulkVgApplyResult> result;
            try { result = _executor.RestoreLast(application); }
            catch (Exception exception)
            {
                Dispatcher.BeginInvoke(new Action(() => { SetBusy(false, "Khôi phục thất bại."); ShowError(exception); }));
                return;
            }
            Dispatcher.BeginInvoke(new Action(() => ShowApplyResults(result, "Khôi phục")));
        });
    }

    private void ShowApplyResults(IReadOnlyList<BulkVgApplyResult> result, string action)
    {
        var failed = result.Where(item => !item.Succeeded).ToArray();
        RestoreButton.IsEnabled = _executor.CanRestore;
        var skippedFilters = result.Where(item => item.Succeeded).Sum(item => item.SkippedFilters);
        SetBusy(false, action + ": " + (result.Count - failed.Length) + " thành công, " + failed.Length + " lỗi." +
            (skippedFilters > 0 ? " Bỏ qua " + skippedFilters + " Filter chưa có (Update Existing)." : ""));
        if (failed.Length > 0)
            MessageBox.Show(this, string.Join(Environment.NewLine, failed.Select(item => item.TargetName + ": " + item.Message)),
                "TUNG_DIROOT", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private BulkVgRequest BuildRequest()
    {
        if (SourceViewBox.SelectedItem is not BulkVgViewDescriptor source) throw new InvalidOperationException("Hãy chọn View nguồn.");
        var targets = _targets.Where(row => row.IsSelected)
            .Select(row => new BulkVgTargetSelection(row.Descriptor.Id, row.Descriptor.Kind, row.Descriptor.ViewportViewIds)).ToArray();
        var items = _items.Where(row => row.IsSelected)
            .Select(row => new BulkVgSelectedItem(row.Descriptor.Id, row.Descriptor.Kind, row.Descriptor.Name)).ToArray();
        var options = new BulkVgApplyOptions(
            VisibilityCheck.IsChecked == true,
            ProjectionCheck.IsChecked == true,
            CutCheck.IsChecked == true,
            FillCheck.IsChecked == true,
            TransparencyCheck.IsChecked == true);
        if (targets.Length == 0) throw new InvalidOperationException("Hãy chọn ít nhất một đích.");
        if (items.Length == 0) throw new InvalidOperationException("Hãy chọn ít nhất một Category hoặc Filter.");
        if (!options.HasAny) throw new InvalidOperationException("Hãy chọn ít nhất một nhóm thuộc tính V/G.");
        var mode = ModeBox.SelectedItem is BulkVgModeDescriptor selected ? selected.Mode : BulkVgApplyMode.Merge;
        return new BulkVgRequest(source.Id, targets, items, options, mode);
    }

    private void Mode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModeBox.SelectedItem is BulkVgModeDescriptor selected) ModeHelpText.Text = selected.Description;
        _preview.Clear();
    }

    private void RunExternal(string status, Action<UIApplication> action)
    {
        SetBusy(true, status);
        try
        {
            _externalEvent.Raise(application =>
            {
                try { action(application); }
                catch (Exception exception)
                {
                    Dispatcher.BeginInvoke(new Action(() => { SetBusy(false, "Thao tác thất bại."); ShowError(exception); }));
                }
            });
        }
        catch (Exception exception)
        {
            SetBusy(false, "Không thể gửi yêu cầu sang Revit.");
            ShowError(exception);
        }
    }

    private void SetBusy(bool value, string status)
    {
        _isBusy = value;
        ApplyButton.IsEnabled = !value;
        SourceViewBox.IsEnabled = !value;
        ModeBox.IsEnabled = !value;
        StatusText.Text = status;
    }

    private void ShowError(Exception exception) =>
        MessageBox.Show(this, exception.Message, "TUNG_DIROOT", MessageBoxButton.OK, MessageBoxImage.Error);

    private void SelectAllTargets_Click(object sender, RoutedEventArgs e) { foreach (var row in _targets) row.IsSelected = true; TargetsGrid.Items.Refresh(); }
    private void ClearTargets_Click(object sender, RoutedEventArgs e) { foreach (var row in _targets) row.IsSelected = false; TargetsGrid.Items.Refresh(); }
    private void SelectAllItems_Click(object sender, RoutedEventArgs e) { foreach (var row in _items) row.IsSelected = true; ItemsGrid.Items.Refresh(); }
    private void ClearItems_Click(object sender, RoutedEventArgs e) { foreach (var row in _items) row.IsSelected = false; ItemsGrid.Items.Refresh(); }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_isBusy) return;
        e.Cancel = true;
        MessageBox.Show(this, "Hãy chờ thao tác V/G hiện tại hoàn tất.", "TUNG_DIROOT", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

public sealed class BulkVgTargetRow
{
    public BulkVgTargetRow(BulkVgViewDescriptor descriptor) => Descriptor = descriptor;
    public BulkVgViewDescriptor Descriptor { get; }
    public bool IsSelected { get; set; }
    public string DisplayName => Descriptor.DisplayName;
    public BulkVgTargetKind Kind => Descriptor.Kind;
    public int ViewportCount => Descriptor.ViewportViewIds.Count;
}

public sealed class BulkVgItemRow
{
    public BulkVgItemRow(BulkVgItemDescriptor descriptor) => Descriptor = descriptor;
    public BulkVgItemDescriptor Descriptor { get; }
    public bool IsSelected { get; set; }
    public BulkVgItemKind Kind => Descriptor.Kind;
    public string Name => Descriptor.Name;
}
