using System;
using System.IO;
using System.Linq;
using TungDiroot.Core.Parameters;
using Xunit;

namespace TungDiroot.Core.Tests.Parameters;

public sealed class FamilyParameterOrderTests
{
    [Fact]
    public void Workbook_round_trip_preserves_all_parameter_identity_and_target_order()
    {
        WithPath(path =>
        {
            var source = SampleRows();
            FamilyParameterOrderWorkbook.Write(path, source);

            var actual = FamilyParameterOrderWorkbook.Read(path);

            Assert.Equal(source, actual);
        });
    }

    [Fact]
    public void Planner_reorders_only_slots_inside_each_parameter_group()
    {
        var current = SampleRows();
        var edited = new[]
        {
            current[0] with { TargetOrderInGroup = 2 },
            current[1],
            current[2] with { TargetOrderInGroup = 1 }
        };

        var plan = FamilyParameterOrderPlanner.Plan(edited, current);

        Assert.True(plan.IsReady);
        Assert.Equal(new[] { "k3", "k2", "k1" }, plan.OrderedKeys);
        Assert.Equal(2, plan.Changes.Count);
    }

    [Fact]
    public void Planner_rejects_missing_parameter_and_duplicate_order()
    {
        var current = SampleRows();
        var edited = new[]
        {
            current[0] with { TargetOrderInGroup = 1 },
            current[2] with { TargetOrderInGroup = 1 }
        };

        var plan = FamilyParameterOrderPlanner.Plan(edited, current);

        Assert.False(plan.IsReady);
        Assert.Contains(plan.Issues, issue => issue.Code == "PARAMETER_SET_CHANGED");
        Assert.Contains(plan.Issues, issue => issue.Code == "TARGET_ORDER_DUPLICATE");
    }

    [Fact]
    public void Planner_rejects_group_edit_even_when_name_is_unchanged()
    {
        var current = SampleRows();
        var edited = current.ToArray();
        edited[0] = edited[0] with { GroupKey = "PG_GEOMETRY" };

        var plan = FamilyParameterOrderPlanner.Plan(edited, current);

        Assert.Contains(plan.Issues, issue => issue.Code == "PARAMETER_GROUP_CHANGED");
    }

    private static FamilyParameterOrderRow[] SampleRows() => new[]
    {
        new FamilyParameterOrderRow("k1", "Width", "PG_DATA", 1, 1, false, null, true),
        new FamilyParameterOrderRow("k2", "Offset", "PG_GEOMETRY", 1, 1, false, null, false),
        new FamilyParameterOrderRow("k3", "Height", "PG_DATA", 2, 2, true, Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), true)
    };

    private static void WithPath(Action<string> action)
    {
        var path = Path.Combine(Path.GetTempPath(), "TUNG_DIROOT-ReOrdering-" + Guid.NewGuid().ToString("N") + ".xlsx");
        try { action(path); }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
