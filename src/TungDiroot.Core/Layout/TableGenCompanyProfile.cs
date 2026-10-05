using System.Collections.Generic;

namespace TungDiroot.Core.Layout;

public sealed record NumericDisplayOptions(
    bool KeepExcelFormat,
    string DecimalSymbol,
    bool UseThousandsSeparator,
    string ThousandsSeparator,
    int DecimalPlaces)
{
    public static NumericDisplayOptions ExcelDefault { get; } = new(true, ".", false, ",", 2);
}

public sealed record TableGenCompanyProfile(
    int SchemaVersion,
    string Name,
    NumericDisplayOptions Numbers,
    IReadOnlyList<TableGenStyleMapping> StyleMappings)
{
    public const int CurrentSchemaVersion = 1;

    public static TableGenCompanyProfile Default(string name) =>
        new(CurrentSchemaVersion, name, NumericDisplayOptions.ExcelDefault, new TableGenStyleMapping[0]);

    public StyleMappingProfile ToStyleMappingProfile() => StyleMappingProfile.FromEntries(StyleMappings);
}

