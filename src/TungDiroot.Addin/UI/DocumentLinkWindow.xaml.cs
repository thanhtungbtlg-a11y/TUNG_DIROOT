using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.Win32;
using TungDiroot.Addin.Documents;
using TungDiroot.Addin.Links;
using TungDiroot.Core.Documents;
using TungDiroot.Core.Runtime;
using TungDiroot.Presentation.UI;

namespace TungDiroot.Addin.UI;

public partial class DocumentLinkWindow : Window
{
    private static DocumentLinkWindow? _instance;
    private readonly Document _document;
    private readonly RevitDocumentLinkExecutor _executor;
    private readonly RevitActionExternalEvent _externalEvent;
    private readonly TableGenSourceWatchService _sourceWatch;
    private readonly ObservableCollection<DocumentLinkRow> _rows = new();
    private IReadOnlyList<string> _pendingSources = Array.Empty<string>();
    private bool _isBusy;

    private DocumentLinkWindow(UIApplication application)
    {
        _document = application.ActiveUIDocument?.Document ?? throw new InvalidOperationException("NO_ACTIVE_DOCUMENT");
        _executor = new RevitDocumentLinkExecutor(_document);
        _externalEvent = new RevitActionExternalEvent();
        InitializeComponent();
        ApplyInitialWindowSize();
        DpiBox.ItemsSource = Enum.GetValues(typeof(DocumentImportDpi));
        DpiBox.SelectedItem = DocumentImportDpi.Dpi300;
        LinksGrid.ItemsSource = _rows;
        _sourceWatch = new TableGenSourceWatchService(
            action => Dispatcher.BeginInvoke(action),
            _ => RefreshFileStatuses());
        Closing += OnClosing;
        Closed += (_, _) =>
        {
            _sourceWatch.Dispose();
            _externalEvent.Dispose();
            _instance = null;
        };
        RequestReload();
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
                TaskDialog.Show("TUNG_DIROOT", "Hãy chờ thao tác PDF/Word hiện tại hoàn tất trước khi chuyển model.");
                return;
            }
            _instance.Close();
        }
        _instance = new DocumentLinkWindow(application);
        _instance.Show();
    }

    private void ApplyInitialWindowSize()
    {
        var size = WindowSizePolicy.Clamp(1320, 820, MinWidth, MinHeight,
            SystemParameters.WorkArea.Width, SystemParameters.WorkArea.Height, 32);
        MinWidth = Math.Min(MinWidth, size.Width);
        MinHeight = Math.Min(MinHeight, size.Height);
        Width = size.Width;
        Height = size.Height;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Chọn PDF hoặc Word",
            Filter = "PDF và Word|*.pdf;*.doc;*.docx;*.docm|PDF|*.pdf|Word|*.doc;*.docx;*.docm",
            Multiselect = true,
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == true) SetPendingSources(dialog.FileNames);
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths) SetPendingSources(paths);
    }

    private void SetPendingSources(IEnumerable<string> paths)
    {
        var accepted = paths.Where(IsSupportedSource).Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (accepted.Length == 0)
        {
            MessageBox.Show(this, "Chỉ hỗ trợ PDF, DOC, DOCX và DOCM.", "TUNG_DIROOT", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        _pendingSources = accepted;
        SourcePathBox.Text = accepted.Length == 1 ? accepted[0] : accepted.Length + " tài liệu đã chọn";
        ViewNameBox.Text = accepted.Length == 1 ? Path.GetFileNameWithoutExtension(accepted[0]) : "";
        StatusText.Text = accepted.Length + " nguồn sẵn sàng để nhập.";
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        if (_pendingSources.Count == 0)
        {
            MessageBox.Show(this, "Hãy chọn ít nhất một file PDF hoặc Word.", "TUNG_DIROOT", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        try
        {
            var pages = PagesBox.Text;
            var dpi = DpiBox.SelectedItem is DocumentImportDpi selected ? selected : DocumentImportDpi.Dpi300;
            if (!int.TryParse(ColumnsBox.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var columns))
                throw new ArgumentException("Số cột phải là số từ 1 đến 8.");
            var customName = ViewNameBox.Text.Trim();
            var relative = RelativePathCheck.IsChecked == true;
            var requests = _pendingSources.Select(path => DocumentImportRequest.Create(
                path, pages, dpi, columns,
                _pendingSources.Count == 1 && customName.Length > 0 ? customName : Path.GetFileNameWithoutExtension(path),
                relative)).ToArray();
            PrepareInSta(
                requests.Select(request => (Guid.NewGuid(), request)).ToArray(),
                prepared => RunExternalBatch(prepared, update: false));
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private void UpdateSelected_Click(object sender, RoutedEventArgs e)
    {
        var selected = _rows.Where(row => row.IsSelected).ToArray();
        if (selected.Length == 0)
        {
            MessageBox.Show(this, "Hãy tích chọn ít nhất một liên kết.", "TUNG_DIROOT", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        try
        {
            var requests = selected.Select(row =>
            {
                var record = row.Record;
                var request = DocumentImportRequest.Create(row.SourcePath, record.PageExpression, record.Dpi, record.Columns,
                    row.TargetViewName, record.SourcePath.Mode == TungDiroot.Core.Links.PathMode.Relative);
                return (record.LinkId, request);
            }).ToArray();
            PrepareInSta(requests, prepared => RunExternalBatch(prepared, update: true));
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private void PrepareInSta(
        IReadOnlyList<(Guid LinkId, DocumentImportRequest Request)> requests,
        Action<IReadOnlyList<PreparedDocumentSource>> complete)
    {
        SetBusy(true, "Đang chuẩn bị nguồn PDF/Word…");
        var thread = new Thread(() =>
        {
            try
            {
                var preparer = new DocumentSourcePreparer();
                var prepared = requests.Select(item => preparer.Prepare(item.Request, item.LinkId)).ToArray();
                Dispatcher.BeginInvoke(new Action(() => complete(prepared)));
            }
            catch (Exception exception)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    SetBusy(false, "Chuẩn bị nguồn thất bại.");
                    ShowError(exception);
                }));
            }
        }) { IsBackground = true, Name = "TUNG_DIROOT Word/PDF preparation" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    private void RunExternalBatch(IReadOnlyList<PreparedDocumentSource> prepared, bool update)
    {
        StatusText.Text = update ? "Đang cập nhật liên kết trong Revit…" : "Đang tạo liên kết trong Revit…";
        try
        {
            _externalEvent.Raise(application =>
            {
                var errors = new List<string>();
                foreach (var source in prepared)
                {
                    try
                    {
                        if (update) _executor.UpdatePrepared(application, source);
                        else _executor.ImportPrepared(application, source);
                    }
                    catch (Exception exception)
                    {
                        errors.Add(Path.GetFileName(source.Request.SourcePath) + ": " + exception.Message);
                    }
                }
                var records = ReadSnapshots();
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    ApplyRecords(records);
                    SetBusy(false, errors.Count == 0 ? "Hoàn tất." : "Hoàn tất với " + errors.Count + " lỗi.");
                    if (errors.Count > 0) MessageBox.Show(this, string.Join(Environment.NewLine, errors), "TUNG_DIROOT", MessageBoxButton.OK, MessageBoxImage.Warning);
                }));
            });
        }
        catch (Exception exception)
        {
            SetBusy(false, "Không thể gửi yêu cầu sang Revit.");
            ShowError(exception);
        }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RequestReload();
    private void OpenView_Click(object sender, RoutedEventArgs e) => RunSelected((app, row) => _executor.OpenView(app, row.Record.LinkId));
    private void OpenSource_Click(object sender, RoutedEventArgs e) => RunSelected((app, row) => _executor.OpenSource(row.Record.LinkId));

    private void Unlink_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, "Bỏ liên kết nhưng giữ View và các trang hiện tại?", "TUNG_DIROOT",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        RunSelected((app, row) => _executor.UnlinkKeepView(app, row.Record.LinkId));
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, "Xóa liên kết và View đích? Có thể Undo trong Revit.", "TUNG_DIROOT",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        RunSelected((app, row) => _executor.DeleteLinkAndView(app, row.Record.LinkId));
    }

    private void RunSelected(Action<UIApplication, DocumentLinkRow> action)
    {
        var selected = _rows.Where(row => row.IsSelected).ToArray();
        if (selected.Length == 0 && LinksGrid.SelectedItem is DocumentLinkRow current) selected = new[] { current };
        if (selected.Length == 0)
        {
            MessageBox.Show(this, "Hãy chọn hoặc tích một liên kết.", "TUNG_DIROOT", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        SetBusy(true, "Đang thực hiện trong Revit…");
        try
        {
            _externalEvent.Raise(application =>
            {
                string? error = null;
                try { foreach (var row in selected) action(application, row); }
                catch (Exception exception) { error = exception.Message; }
                var records = ReadSnapshots();
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    ApplyRecords(records);
                    SetBusy(false, error == null ? "Hoàn tất." : "Thao tác thất bại.");
                    if (error != null) ShowError(new InvalidOperationException(error));
                }));
            });
        }
        catch (Exception exception)
        {
            SetBusy(false, "Không thể gửi yêu cầu sang Revit.");
            ShowError(exception);
        }
    }

    private void RequestReload()
    {
        SetBusy(true, "Đang đọc liên kết…");
        try
        {
            _externalEvent.Raise(_ =>
            {
                var records = ReadSnapshots();
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    ApplyRecords(records);
                    SetBusy(false, records.Count + " liên kết.");
                }));
            });
        }
        catch (Exception exception)
        {
            SetBusy(false, "Không thể đọc liên kết.");
            ShowError(exception);
        }
    }

    private IReadOnlyList<DocumentLinkSnapshot> ReadSnapshots() => _executor.Load().Select(record =>
    {
        string source;
        try { source = _executor.ResolveSource(record); }
        catch { source = record.SourcePath.Value; }
        return new DocumentLinkSnapshot(record, source, _executor.TargetViewName(record));
    }).ToArray();

    private void ApplyRecords(IReadOnlyList<DocumentLinkSnapshot> records)
    {
        _rows.Clear();
        foreach (var item in records) _rows.Add(new DocumentLinkRow(item.Record, item.SourcePath, item.TargetViewName));
        RefreshFileStatuses();
        _sourceWatch.UpdateSources(_rows.Select(row => row.SourcePath));
    }

    private void RefreshFileStatuses()
    {
        foreach (var row in _rows) row.RefreshStatus();
        LinksGrid.Items.Refresh();
    }

    private void SetBusy(bool value, string text)
    {
        _isBusy = value;
        ImportButton.IsEnabled = !value;
        StatusText.Text = text;
    }

    private void ShowError(Exception exception) =>
        MessageBox.Show(this, FriendlyMessage(exception), "TUNG_DIROOT", MessageBoxButton.OK, MessageBoxImage.Error);

    private static string FriendlyMessage(Exception exception) => exception.Message switch
    {
        "WORD_NOT_INSTALLED" => "Máy chưa cài Microsoft Word. PDF vẫn có thể nhập trực tiếp.",
        "SOURCE_NOT_FOUND" => "Không tìm thấy file nguồn.",
        "PAGE_SELECTION_INVALID" => "Danh sách trang không hợp lệ. Ví dụ đúng: 1,3,5-7.",
        "PAGE_SELECTION_TOO_LARGE" => "Một liên kết chỉ hỗ trợ tối đa 500 trang.",
        _ => exception.Message
    };

    private static bool IsSupportedSource(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return File.Exists(path) && (extension == ".pdf" || extension == ".doc" || extension == ".docx" || extension == ".docm");
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_isBusy) return;
        e.Cancel = true;
        MessageBox.Show(this, "Hãy chờ thao tác hiện tại hoàn tất trước khi đóng.", "TUNG_DIROOT",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

internal sealed record DocumentLinkSnapshot(DocumentLinkRecord Record, string SourcePath, string TargetViewName);

public sealed class DocumentLinkRow
{
    public DocumentLinkRow(DocumentLinkRecord record, string sourcePath, string targetViewName)
    {
        Record = record;
        SourcePath = sourcePath;
        TargetViewName = targetViewName;
        RefreshStatus();
    }

    public DocumentLinkRecord Record { get; }
    public bool IsSelected { get; set; }
    public string Status { get; private set; } = "Ready";
    public string SourcePath { get; }
    public string SourceKind => Record.SourceKind.ToString();
    public string Pages => Record.PageExpression;
    public int Dpi => (int)Record.Dpi;
    public int Columns => Record.Columns;
    public string TargetViewName { get; }
    public string LastUpdated => Record.LastUpdatedUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);

    public void RefreshStatus()
    {
        try
        {
            var file = new FileInfo(SourcePath);
            file.Refresh();
            if (!file.Exists) { Status = "Missing"; return; }
            Status = file.Length == Record.LastFingerprint.Length && file.LastWriteTimeUtc.Ticks == Record.LastFingerprint.LastWriteUtcTicks
                ? "Ready" : "Changed";
        }
        catch
        {
            Status = "Error";
        }
    }
}
