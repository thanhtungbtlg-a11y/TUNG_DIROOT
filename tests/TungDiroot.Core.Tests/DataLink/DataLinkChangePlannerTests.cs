using System.Collections.Generic;
using System.Linq;
using TungDiroot.Core.DataLink;
using Xunit;

namespace TungDiroot.Core.Tests.DataLink;

public sealed class DataLinkChangePlannerTests
{
    [Fact]
    public void Conflicting_type_values_are_blocked_as_one_group()
    {
        var stamp = new DataLinkDocumentStamp("model-a");
        var parameter = new DataLinkParameterKey("builtin:comments", "Comments", true, "String", null);
        var rows = new[]
        {
            Row("element-1", "type-1", parameter, "A", "A"),
            Row("element-2", "type-1", parameter, "A", "B")
        };
        var workbook = new DataLinkWorkbook(1, stamp, "en-US", rows);
        var current = rows.ToDictionary(
            row => DataLinkValueAddress.From(row),
            row => DataLinkCurrentValue.Available("String", "A"));

        var plan = DataLinkChangePlanner.Create(workbook, stamp, current);

        Assert.Equal(2, plan.Changes.Count);
        Assert.All(plan.Changes, item => Assert.Equal(DataLinkChangeStatus.TypeConflict, item.Status));
    }

    [Fact]
    public void Stale_revit_value_is_not_overwritten()
    {
        var stamp = new DataLinkDocumentStamp("model-a");
        var parameter = new DataLinkParameterKey("builtin:comments", "Comments", false, "String", null);
        var row = Row("element-1", null, parameter, "A", "B");
        var workbook = new DataLinkWorkbook(1, stamp, "en-US", new[] { row });
        var current = new Dictionary<DataLinkValueAddress, DataLinkCurrentValue>
        {
            [DataLinkValueAddress.From(row)] = DataLinkCurrentValue.Available("String", "C")
        };

        var plan = DataLinkChangePlanner.Create(workbook, stamp, current);

        Assert.Equal(DataLinkChangeStatus.StaleModelValue, Assert.Single(plan.Changes).Status);
    }

    [Fact]
    public void Workbook_from_another_document_is_blocked_before_row_validation()
    {
        var row = InstanceRow("String", "A", "B");
        var workbook = new DataLinkWorkbook(1, new DataLinkDocumentStamp("model-a"), "en-US", new[] { row });
        var current = Current(row, DataLinkCurrentValue.Available("String", "A"));

        var plan = DataLinkChangePlanner.Create(workbook, new DataLinkDocumentStamp("model-b"), current);

        Assert.Equal(DataLinkChangeStatus.DifferentDocument, Assert.Single(plan.Changes).Status);
    }

    [Fact]
    public void Missing_element_is_reported_instead_of_throwing()
    {
        var row = InstanceRow("String", "A", "B");
        var workbook = Workbook(row);

        var plan = DataLinkChangePlanner.Create(workbook, workbook.DocumentStamp,
            new Dictionary<DataLinkValueAddress, DataLinkCurrentValue>());

        Assert.Equal(DataLinkChangeStatus.ElementMissing, Assert.Single(plan.Changes).Status);
    }

    [Fact]
    public void Missing_parameter_is_blocked()
    {
        var row = InstanceRow("String", "A", "B");
        var currentValue = new DataLinkCurrentValue(true, false, false, true, "String", "A");

        var plan = DataLinkChangePlanner.Create(Workbook(row), new DataLinkDocumentStamp("model-a"), Current(row, currentValue));

        Assert.Equal(DataLinkChangeStatus.ParameterMissing, Assert.Single(plan.Changes).Status);
    }

    [Fact]
    public void Read_only_parameter_is_blocked()
    {
        var row = InstanceRow("String", "A", "B");
        var currentValue = new DataLinkCurrentValue(true, true, true, true, "String", "A");

        var plan = DataLinkChangePlanner.Create(Workbook(row), new DataLinkDocumentStamp("model-a"), Current(row, currentValue));

        Assert.Equal(DataLinkChangeStatus.ReadOnly, Assert.Single(plan.Changes).Status);
    }

    [Fact]
    public void Workshared_element_owned_elsewhere_is_blocked()
    {
        var row = InstanceRow("String", "A", "B");
        var currentValue = new DataLinkCurrentValue(true, true, false, false, "String", "A");

        var plan = DataLinkChangePlanner.Create(Workbook(row), new DataLinkDocumentStamp("model-a"), Current(row, currentValue));

        Assert.Equal(DataLinkChangeStatus.NotEditable, Assert.Single(plan.Changes).Status);
    }

    [Theory]
    [InlineData("ElementId", "12")]
    [InlineData("Double", "12")]
    public void Unsupported_or_mismatched_storage_is_blocked(string currentStorage, string currentInternal)
    {
        var row = InstanceRow("ElementId", "12", "13");
        var currentValue = DataLinkCurrentValue.Available(currentStorage, currentInternal);

        var plan = DataLinkChangePlanner.Create(Workbook(row), new DataLinkDocumentStamp("model-a"), Current(row, currentValue));

        Assert.Equal(DataLinkChangeStatus.UnsupportedStorage, Assert.Single(plan.Changes).Status);
    }

    [Fact]
    public void Invalid_integer_text_is_blocked()
    {
        var row = InstanceRow("Integer", "1", "one");

        var plan = DataLinkChangePlanner.Create(Workbook(row), new DataLinkDocumentStamp("model-a"),
            Current(row, DataLinkCurrentValue.Available("Integer", "1")));

        Assert.Equal(DataLinkChangeStatus.InvalidValue, Assert.Single(plan.Changes).Status);
    }

    [Fact]
    public void Vietnamese_decimal_text_is_normalized_to_invariant_internal_value()
    {
        var row = InstanceRow("Double", "3", "3,5");
        var workbook = new DataLinkWorkbook(1, new DataLinkDocumentStamp("model-a"), "vi-VN", new[] { row });

        var plan = DataLinkChangePlanner.Create(workbook, workbook.DocumentStamp,
            Current(row, DataLinkCurrentValue.Available("Double", "3")));

        var change = Assert.Single(plan.Changes);
        Assert.Equal(DataLinkChangeStatus.Ready, change.Status);
        Assert.Equal("3.5", change.ParsedInternal);
    }

    [Fact]
    public void Numerically_equivalent_display_value_is_no_change()
    {
        var row = InstanceRow("Double", "3.5", "3,50");
        var workbook = new DataLinkWorkbook(1, new DataLinkDocumentStamp("model-a"), "vi-VN", new[] { row });

        var plan = DataLinkChangePlanner.Create(workbook, workbook.DocumentStamp,
            Current(row, DataLinkCurrentValue.Available("Double", "3.5")));

        Assert.Equal(DataLinkChangeStatus.NoChange, Assert.Single(plan.Changes).Status);
    }

    [Fact]
    public void Plan_carries_the_active_document_stamp_for_assignment_revalidation()
    {
        var row = InstanceRow("String", "A", "B");
        var stamp = new DataLinkDocumentStamp("model-a");

        var plan = DataLinkChangePlanner.Create(Workbook(row), stamp,
            Current(row, DataLinkCurrentValue.Available("String", "A")));

        Assert.Equal(stamp, plan.DocumentStamp);
    }

    [Fact]
    public void Row_disabled_in_excel_is_not_ready_for_assignment()
    {
        var enabled = InstanceRow("String", "A", "B");
        var row = enabled with { IncludeInImport = false };

        var plan = DataLinkChangePlanner.Create(Workbook(row), new DataLinkDocumentStamp("model-a"),
            Current(row, DataLinkCurrentValue.Available("String", "A")));

        var change = Assert.Single(plan.Changes);
        Assert.Equal(DataLinkChangeStatus.NoChange, change.Status);
        Assert.Equal("Đã bỏ chọn trong Excel.", change.Message);
    }

    [Fact]
    public void Different_document_is_reported_even_when_excel_row_is_disabled()
    {
        var row = InstanceRow("String", "A", "B") with { IncludeInImport = false };
        var workbook = new DataLinkWorkbook(1, new DataLinkDocumentStamp("model-a"), "en-US", new[] { row });

        var plan = DataLinkChangePlanner.Create(workbook, new DataLinkDocumentStamp("model-b"),
            Current(row, DataLinkCurrentValue.Available("String", "A")));

        Assert.Equal(DataLinkChangeStatus.DifferentDocument, Assert.Single(plan.Changes).Status);
    }

    private static DataLinkElementRow InstanceRow(string storageKind, string original, string edited) =>
        Row("element-1", null, new DataLinkParameterKey("parameter-1", "Parameter", false, storageKind, null), original, edited);

    private static DataLinkWorkbook Workbook(DataLinkElementRow row) =>
        new(1, new DataLinkDocumentStamp("model-a"), "en-US", new[] { row });

    private static Dictionary<DataLinkValueAddress, DataLinkCurrentValue> Current(
        DataLinkElementRow row,
        DataLinkCurrentValue value) => new() { [DataLinkValueAddress.From(row)] = value };

    private static DataLinkElementRow Row(
        string elementUniqueId,
        string? typeUniqueId,
        DataLinkParameterKey parameter,
        string original,
        string edited) =>
        new(elementUniqueId, 100, typeUniqueId, "Pipes", "Pipe", "Standard", parameter, original, original, edited);
}
