using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using TungDiroot.Core.Layout;
using TungDiroot.Presentation.UI;

namespace TungDiroot.Addin.UI;

public partial class TableGenProfileManagerWindow : Window
{
    private readonly TableGenProfileRepository _repository = new();
    private readonly TableGenProfileEditorViewModel _viewModel;

    public TableGenProfileManagerWindow(
        IReadOnlyList<TableGenStyleCandidate> candidates,
        RevitStyleCatalogSnapshot styleCatalog)
    {
        if (candidates == null) throw new ArgumentNullException(nameof(candidates));
        if (styleCatalog == null) throw new ArgumentNullException(nameof(styleCatalog));
        InitializeComponent();
        ApplyInitialWindowSize();
        _viewModel = new TableGenProfileEditorViewModel(candidates, styleCatalog.TextNoteTypes, styleCatalog.GraphicsStyles);
        DataContext = _viewModel;
        RefreshSavedProfiles();
    }

    public string? LastSavedProfileName { get; private set; }

    private void ApplyInitialWindowSize()
    {
        var size = WindowSizePolicy.Clamp(1200, 760, MinWidth, MinHeight,
            SystemParameters.WorkArea.Width, SystemParameters.WorkArea.Height, 32);
        MinWidth = Math.Min(MinWidth, size.Width);
        MinHeight = Math.Min(MinHeight, size.Height);
        Width = size.Width;
        Height = size.Height;
    }

    private void RefreshSavedProfiles()
    {
        var profiles = _repository.LoadAll()
            .Where(result => result.IsSuccess)
            .Select(result => result.Profile!)
            .OrderBy(profile => profile.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        SavedProfilesBox.ItemsSource = profiles;
        if (LastSavedProfileName != null)
            SavedProfilesBox.SelectedItem = profiles.FirstOrDefault(profile => string.Equals(profile.Name, LastSavedProfileName, StringComparison.OrdinalIgnoreCase));
    }

    private void LoadProfile_Click(object sender, RoutedEventArgs e)
    {
        if (SavedProfilesBox.SelectedItem is TableGenCompanyProfile profile) _viewModel.Load(profile);
    }

    private void SaveProfile_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.TryBuild(out var profile, out var errorCode))
        {
            _viewModel.StatusText = ErrorMessage(errorCode);
            return;
        }
        try
        {
            _repository.Save(profile!);
            LastSavedProfileName = profile!.Name;
            _viewModel.StatusText = $"Đã lưu profile “{profile.Name}”.";
            RefreshSavedProfiles();
        }
        catch (Exception exception)
        {
            _viewModel.StatusText = exception.Message;
        }
    }

    private void DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if (SavedProfilesBox.SelectedItem is not TableGenCompanyProfile profile) return;
        if (MessageBox.Show(this,
                $"Xóa profile “{profile.Name}”? Link đang dùng profile này sẽ fallback về định dạng Excel mặc định.",
                "TUNG_DIROOT", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        _repository.Delete(profile.Name);
        if (string.Equals(LastSavedProfileName, profile.Name, StringComparison.OrdinalIgnoreCase)) LastSavedProfileName = null;
        _viewModel.StatusText = $"Đã xóa profile “{profile.Name}”.";
        RefreshSavedProfiles();
    }

    private static string ErrorMessage(string? code) => code switch
    {
        "TABLEGEN_PROFILE_NAME_REQUIRED" => "Hãy nhập tên profile.",
        "TABLEGEN_PROFILE_DECIMAL_PLACES_INVALID" => "Số chữ số thập phân phải từ 0 đến 12.",
        "TABLEGEN_PROFILE_DECIMAL_SYMBOL_REQUIRED" => "Hãy nhập dấu thập phân.",
        "TABLEGEN_PROFILE_GROUP_SYMBOL_REQUIRED" => "Hãy nhập dấu phân nhóm hàng nghìn.",
        "TABLEGEN_PROFILE_NUMBER_SYMBOLS_CONFLICT" => "Dấu thập phân và dấu phân nhóm phải khác nhau.",
        _ => code ?? "PROFILE_INVALID"
    };

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
