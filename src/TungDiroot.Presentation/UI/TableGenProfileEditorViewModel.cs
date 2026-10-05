using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using TungDiroot.Core.Layout;

namespace TungDiroot.Presentation.UI;

public sealed record RevitStyleOption(string UniqueId, string Name);

public sealed record RevitStyleCatalogSnapshot(
    IReadOnlyList<RevitStyleOption> TextNoteTypes,
    IReadOnlyList<RevitStyleOption> GraphicsStyles);

public sealed class TableGenStyleMappingRowViewModel : BindableBase
{
    private string? _selectedTextNoteTypeUniqueId;
    private string? _selectedGraphicsStyleUniqueId;

    public TableGenStyleMappingRowViewModel(TableGenStyleCandidate candidate)
    {
        Candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
    }

    public TableGenStyleCandidate Candidate { get; }
    public TableGenStyleCandidateKind Kind => Candidate.Kind;
    public bool IsTextStyle => Kind == TableGenStyleCandidateKind.Text;
    public bool IsLineStyle => Kind == TableGenStyleCandidateKind.Line;
    public string Key => Candidate.Key;
    public string Description => Candidate.Description;
    public string? SelectedTextNoteTypeUniqueId
    {
        get => _selectedTextNoteTypeUniqueId;
        set => SetProperty(ref _selectedTextNoteTypeUniqueId, value);
    }
    public string? SelectedGraphicsStyleUniqueId
    {
        get => _selectedGraphicsStyleUniqueId;
        set => SetProperty(ref _selectedGraphicsStyleUniqueId, value);
    }
}

public sealed class TableGenProfileEditorViewModel : BindableBase
{
    private string _profileName = string.Empty;
    private bool _keepExcelFormat = true;
    private string _decimalSymbol = ".";
    private bool _useThousandsSeparator;
    private string _thousandsSeparator = ",";
    private int _decimalPlaces = 2;
    private string _statusText = "Chọn quy tắc rồi lưu profile.";

    public TableGenProfileEditorViewModel(
        IEnumerable<TableGenStyleCandidate> candidates,
        IEnumerable<RevitStyleOption> textNoteTypes,
        IEnumerable<RevitStyleOption> graphicsStyles)
    {
        if (candidates == null) throw new ArgumentNullException(nameof(candidates));
        TextNoteTypes = Options(textNoteTypes);
        GraphicsStyles = Options(graphicsStyles);
        Rows = new ObservableCollection<TableGenStyleMappingRowViewModel>(
            candidates.Select(candidate => new TableGenStyleMappingRowViewModel(candidate)));
    }

    public ObservableCollection<TableGenStyleMappingRowViewModel> Rows { get; }
    public IReadOnlyList<RevitStyleOption> TextNoteTypes { get; }
    public IReadOnlyList<RevitStyleOption> GraphicsStyles { get; }
    public string ProfileName { get => _profileName; set => SetProperty(ref _profileName, value ?? string.Empty); }
    public bool KeepExcelFormat { get => _keepExcelFormat; set => SetProperty(ref _keepExcelFormat, value); }
    public string DecimalSymbol { get => _decimalSymbol; set => SetProperty(ref _decimalSymbol, value ?? string.Empty); }
    public bool UseThousandsSeparator { get => _useThousandsSeparator; set => SetProperty(ref _useThousandsSeparator, value); }
    public string ThousandsSeparator { get => _thousandsSeparator; set => SetProperty(ref _thousandsSeparator, value ?? string.Empty); }
    public int DecimalPlaces { get => _decimalPlaces; set => SetProperty(ref _decimalPlaces, value); }
    public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value ?? string.Empty); }

    public bool TryBuild(out TableGenCompanyProfile? profile, out string? errorCode)
    {
        profile = null;
        errorCode = Validate();
        if (errorCode != null) return false;
        var mappings = Rows.Select(row => row.Kind == TableGenStyleCandidateKind.Text
                ? new TableGenStyleMapping(row.Key, EmptyToNull(row.SelectedTextNoteTypeUniqueId), null)
                : new TableGenStyleMapping(row.Key, null, EmptyToNull(row.SelectedGraphicsStyleUniqueId)))
            .Where(mapping => mapping.TextNoteTypeUniqueId != null || mapping.GraphicsStyleUniqueId != null)
            .ToArray();
        profile = new TableGenCompanyProfile(
            TableGenCompanyProfile.CurrentSchemaVersion,
            ProfileName.Trim(),
            new NumericDisplayOptions(KeepExcelFormat, DecimalSymbol, UseThousandsSeparator, ThousandsSeparator, DecimalPlaces),
            mappings);
        return true;
    }

    public void Load(TableGenCompanyProfile profile)
    {
        if (profile == null) throw new ArgumentNullException(nameof(profile));
        ProfileName = profile.Name;
        KeepExcelFormat = profile.Numbers.KeepExcelFormat;
        DecimalSymbol = profile.Numbers.DecimalSymbol;
        UseThousandsSeparator = profile.Numbers.UseThousandsSeparator;
        ThousandsSeparator = profile.Numbers.ThousandsSeparator;
        DecimalPlaces = profile.Numbers.DecimalPlaces;
        foreach (var mapping in profile.StyleMappings)
        {
            var row = Rows.FirstOrDefault(item => string.Equals(item.Key, mapping.CellStyleKey, StringComparison.Ordinal));
            if (row == null)
            {
                var kind = mapping.CellStyleKey.StartsWith("LINE|", StringComparison.Ordinal)
                    ? TableGenStyleCandidateKind.Line
                    : TableGenStyleCandidateKind.Text;
                row = new TableGenStyleMappingRowViewModel(new TableGenStyleCandidate(kind, mapping.CellStyleKey, "Từ profile đã lưu"));
                Rows.Add(row);
            }
            row.SelectedTextNoteTypeUniqueId = mapping.TextNoteTypeUniqueId;
            row.SelectedGraphicsStyleUniqueId = mapping.GraphicsStyleUniqueId;
        }
        StatusText = $"Đã nạp profile “{profile.Name}”.";
    }

    private string? Validate()
    {
        if (string.IsNullOrWhiteSpace(ProfileName)) return "TABLEGEN_PROFILE_NAME_REQUIRED";
        if (DecimalPlaces < 0 || DecimalPlaces > 12) return "TABLEGEN_PROFILE_DECIMAL_PLACES_INVALID";
        if (string.IsNullOrEmpty(DecimalSymbol)) return "TABLEGEN_PROFILE_DECIMAL_SYMBOL_REQUIRED";
        if (UseThousandsSeparator && string.IsNullOrEmpty(ThousandsSeparator)) return "TABLEGEN_PROFILE_GROUP_SYMBOL_REQUIRED";
        if (string.Equals(DecimalSymbol, ThousandsSeparator, StringComparison.Ordinal)) return "TABLEGEN_PROFILE_NUMBER_SYMBOLS_CONFLICT";
        return null;
    }

    private static IReadOnlyList<RevitStyleOption> Options(IEnumerable<RevitStyleOption> values)
    {
        if (values == null) throw new ArgumentNullException(nameof(values));
        return new[] { new RevitStyleOption(string.Empty, "— Mặc định / tự tạo —") }
            .Concat(values.Where(value => !string.IsNullOrWhiteSpace(value.UniqueId))
                .GroupBy(value => value.UniqueId, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(value => value.Name, StringComparer.CurrentCultureIgnoreCase))
            .ToArray();
    }

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
