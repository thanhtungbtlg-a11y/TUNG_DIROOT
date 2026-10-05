using System.Collections.Generic;
using TungDiroot.Core.Layout;
using TungDiroot.Presentation.UI;
using Xunit;

namespace TungDiroot.Presentation.Tests.UI;

public sealed class TableGenProfileEditorViewModelTests
{
    [Fact]
    public void Build_creates_numeric_rules_and_only_relevant_style_mappings()
    {
        var vm = new TableGenProfileEditorViewModel(
            new[]
            {
                new TableGenStyleCandidate(TableGenStyleCandidateKind.Text, "CELL-A", "Arial 10 Bold"),
                new TableGenStyleCandidate(TableGenStyleCandidateKind.Line, "LINE-A", "thin #000000")
            },
            new[] { new RevitStyleOption("text-1", "Text 2.5mm") },
            new[] { new RevitStyleOption("line-1", "Thin Lines") });
        vm.ProfileName = "MEP Công ty";
        vm.KeepExcelFormat = false;
        vm.DecimalSymbol = ",";
        vm.UseThousandsSeparator = true;
        vm.ThousandsSeparator = ".";
        vm.DecimalPlaces = 2;
        vm.Rows[0].SelectedTextNoteTypeUniqueId = "text-1";
        vm.Rows[1].SelectedGraphicsStyleUniqueId = "line-1";

        Assert.True(vm.TryBuild(out var profile, out var error), error);
        Assert.Equal("MEP Công ty", profile!.Name);
        Assert.Equal(2, profile.StyleMappings.Count);
        Assert.Equal("text-1", profile.StyleMappings[0].TextNoteTypeUniqueId);
        Assert.Equal("line-1", profile.StyleMappings[1].GraphicsStyleUniqueId);
    }

    [Theory]
    [InlineData("", ",", ".", 2, "TABLEGEN_PROFILE_NAME_REQUIRED")]
    [InlineData("MEP", ",", ",", 2, "TABLEGEN_PROFILE_NUMBER_SYMBOLS_CONFLICT")]
    [InlineData("MEP", ",", ".", 13, "TABLEGEN_PROFILE_DECIMAL_PLACES_INVALID")]
    public void Invalid_profile_fields_are_rejected(string name, string decimalSymbol, string groupSymbol, int places, string expected)
    {
        var vm = new TableGenProfileEditorViewModel(
            new List<TableGenStyleCandidate>(),
            new List<RevitStyleOption>(),
            new List<RevitStyleOption>())
        {
            ProfileName = name,
            KeepExcelFormat = false,
            DecimalSymbol = decimalSymbol,
            UseThousandsSeparator = true,
            ThousandsSeparator = groupSymbol,
            DecimalPlaces = places
        };

        Assert.False(vm.TryBuild(out _, out var error));
        Assert.Equal(expected, error);
    }

    [Fact]
    public void Load_restores_profile_and_keeps_mapping_not_present_in_current_region()
    {
        var vm = new TableGenProfileEditorViewModel(
            new[] { new TableGenStyleCandidate(TableGenStyleCandidateKind.Text, "CELL-A", "A") },
            new[] { new RevitStyleOption("text-1", "Text") },
            new[] { new RevitStyleOption("line-1", "Line") });
        var profile = new TableGenCompanyProfile(
            TableGenCompanyProfile.CurrentSchemaVersion,
            "Loaded",
            NumericDisplayOptions.ExcelDefault,
            new[] { new TableGenStyleMapping("LINE-OLD", null, "line-1") });

        vm.Load(profile);

        Assert.Equal("Loaded", vm.ProfileName);
        Assert.Contains(vm.Rows, row => row.Key == "LINE-OLD" && row.SelectedGraphicsStyleUniqueId == "line-1");
    }
}
