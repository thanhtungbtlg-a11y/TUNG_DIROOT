using System;
using System.IO;
using System.Linq;
using TungDiroot.Core.Families;
using Xunit;

namespace TungDiroot.Core.Tests.Families;

public sealed class FamilyReviserTests
{
    [Fact]
    public void Workbook_round_trip_preserves_family_type_and_nested_flags()
    {
        WithPath(path =>
        {
            var rows = SampleRows();
            FamilyReviserWorkbook.Write(path, rows);

            var actual = FamilyReviserWorkbook.Read(path);

            Assert.Equal(rows, actual);
        });
    }

    [Fact]
    public void Planner_detects_duplicate_family_and_duplicate_type_targets()
    {
        var current = SampleRows();
        var edited = current.Select(row => row with { Apply = true }).ToArray();
        edited[0] = edited[0] with { TargetFamily = "Family B" };
        edited[1] = edited[1] with { TargetType = "Type 2" };

        var plan = FamilyReviserPlanner.Plan(edited, current, null);

        Assert.Contains(plan.Items, item => item.Code == "FAMILY_NAME_DUPLICATE");
        Assert.Contains(plan.Items, item => item.Code == "TYPE_NAME_DUPLICATE");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Planner_rejects_multiple_active_edits_for_the_same_identity(int rowIndex)
    {
        var current = SampleRows();
        var original = current[rowIndex];
        var edited = new[]
        {
            original with { Apply = true, TargetFamily = "New Family A", TargetType = "New Type A" },
            original with { Apply = true, TargetFamily = "New Family B", TargetType = "New Type B" }
        };

        var plan = FamilyReviserPlanner.Plan(edited, current, null);

        Assert.Equal(0, plan.ReadyCount);
        Assert.All(plan.Items, item =>
        {
            Assert.Equal(FamilyReviserPreviewStatus.Conflict, item.Status);
            Assert.Equal("IDENTITY_DUPLICATE", item.Code);
        });
    }

    [Fact]
    public void Planner_ignores_unselected_duplicate_identity()
    {
        var current = SampleRows();
        var edited = new[]
        {
            current[0] with { Apply = true, TargetFamily = "New Family" },
            current[0]
        };

        var plan = FamilyReviserPlanner.Plan(edited, current, null);

        Assert.Equal(1, plan.ReadyCount);
        Assert.Equal(FamilyReviserPreviewStatus.Ready, plan.Items[0].Status);
        Assert.Equal(FamilyReviserPreviewStatus.Skipped, plan.Items[1].Status);
    }

    [Fact]
    public void Revalidation_never_adds_previously_conflicting_rows_to_the_confirmed_ready_set()
    {
        var current = SampleRows();
        var edited = new[]
        {
            current[0] with { Apply = true, TargetFamily = "Family B" },
            current[1] with { Apply = true, TargetType = "New Type" }
        };
        var confirmed = FamilyReviserPlanner.Plan(edited, current, null);
        var changed = current.Select(row => row.UniqueId == "family-b"
            ? row with { CurrentFamily = "Family C" } : row).ToArray();

        var revalidated = FamilyReviserPlanner.RevalidateConfirmed(confirmed, changed, null);

        var item = Assert.Single(revalidated.Items);
        Assert.Equal("type-a1", item.Row.UniqueId);
        Assert.Equal(3, item.ExcelRow);
        Assert.Equal(FamilyReviserPreviewStatus.Ready, item.Status);
    }

    [Theory]
    [InlineData(true, "ELEMENT_STALE")]
    [InlineData(false, "CURRENT_NAME_STALE")]
    public void Revalidation_retains_each_confirmed_row_rejected_as_stale(bool deleted, string expectedCode)
    {
        var current = SampleRows();
        var edited = new[]
        {
            current[0],
            current[1] with { Apply = true, TargetType = "New Type" },
            current[3] with { Apply = true, TargetFamily = "New Family" }
        };
        var confirmed = FamilyReviserPlanner.Plan(edited, current, null);
        var changed = current.Where(row => !deleted || row.UniqueId != "type-a1")
            .Select(row => !deleted && row.UniqueId == "type-a1" ? row with { CurrentType = "Manual Rename" } : row).ToArray();

        var revalidated = FamilyReviserPlanner.RevalidateConfirmed(confirmed, changed, null);

        Assert.Equal(2, revalidated.Items.Count);
        Assert.Equal(1, revalidated.ReadyCount);
        Assert.Equal("type-a1", revalidated.Items[0].Row.UniqueId);
        Assert.Equal(3, revalidated.Items[0].ExcelRow);
        Assert.Equal(FamilyReviserPreviewStatus.Conflict, revalidated.Items[0].Status);
        Assert.Equal(expectedCode, revalidated.Items[0].Code);
        Assert.Equal(4, revalidated.Items[1].ExcelRow);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Bad/Name")]
    [InlineData("Bad\nName")]
    public void Planner_rejects_invalid_target_names(string target)
    {
        var current = SampleRows();
        var edited = new[] { current[0] with { Apply = true, TargetFamily = target } };

        var item = Assert.Single(FamilyReviserPlanner.Plan(edited, current, null).Items);

        Assert.Equal(FamilyReviserPreviewStatus.Invalid, item.Status);
        Assert.Contains("NAME_INVALID", item.Code);
    }

    [Fact]
    public void Planner_applies_optional_company_naming_regex()
    {
        var current = SampleRows();
        var edited = new[] { current[0] with { Apply = true, TargetFamily = "Wrong" } };

        var item = Assert.Single(FamilyReviserPlanner.Plan(edited, current, "^TUNG_[A-Z0-9_]+$").Items);

        Assert.Equal("NAMING_RULE_MISMATCH", item.Code);
    }

    [Fact]
    public void In_place_family_is_blocked_before_apply()
    {
        var current = SampleRows();
        var inPlace = current[0] with { Apply = true, IsInPlace = true, TargetFamily = "Renamed" };

        var item = Assert.Single(FamilyReviserPlanner.Plan(new[] { inPlace }, new[] { current[0] with { IsInPlace = true } }, null).Items);

        Assert.Equal("INPLACE_FAMILY_RENAME_BLOCKED", item.Code);
    }

    private static FamilyReviserRow[] SampleRows() => new[]
    {
        new FamilyReviserRow(false, FamilyReviserAction.RenameFamily, "family-a", "Mechanical Equipment", "Family A", "Family A", "", "", false, false, true),
        new FamilyReviserRow(false, FamilyReviserAction.RenameType, "type-a1", "Mechanical Equipment", "Family A", "Family A", "Type 1", "Type 1", false, false, true),
        new FamilyReviserRow(false, FamilyReviserAction.RenameType, "type-a2", "Mechanical Equipment", "Family A", "Family A", "Type 2", "Type 2", false, false, true),
        new FamilyReviserRow(false, FamilyReviserAction.RenameFamily, "family-b", "Mechanical Equipment", "Family B", "Family B", "", "", true, false, true)
    };

    private static void WithPath(Action<string> action)
    {
        var path = Path.Combine(Path.GetTempPath(), "TUNG_DIROOT-FamilyReviser-" + Guid.NewGuid().ToString("N") + ".xlsx");
        try { action(path); }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
