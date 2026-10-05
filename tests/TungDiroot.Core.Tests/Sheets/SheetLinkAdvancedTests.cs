using System;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using TungDiroot.Core.SheetTools;
using Xunit;

namespace TungDiroot.Core.Tests.SheetTools;

public sealed class SheetLinkAdvancedTests
{
    [Fact]
    public void Revision_workbook_round_trip_preserves_identifiers_and_metadata()
    {
        WithPath(path =>
        {
            var revision = Revision() with { Action = SheetLinkAction.Update, UniqueId = "revision-uid", SequenceNumber = 3,
                Issued = true, IssuedBy = "Designer", IssuedTo = "Owner", Visibility = SheetLinkRevisionVisibility.TagOnly };
            SheetLinkWorkbookWriter.Write(path, Workbook(revisions: new[] { revision }), 0);
            Assert.Equal(revision, Assert.Single(SheetLinkWorkbookReader.Read(path).Revisions));
        });
    }

    [Fact]
    public void Existing_two_tab_workbooks_remain_readable()
    {
        WithPath(path =>
        {
            SheetLinkWorkbookWriter.Write(path, Workbook(), 0);
            using (var document = SpreadsheetDocument.Open(path, true))
            {
                var part = document.WorkbookPart!;
                var sheet = part.Workbook.Sheets!.Elements<Sheet>().Single(item => item.Name == "REVISION");
                part.DeletePart(part.GetPartById(sheet.Id!));
                sheet.Remove();
                part.Workbook.Save();
            }
            Assert.Empty(SheetLinkWorkbookReader.Read(path).Revisions);
        });
    }

    [Fact]
    public void Revision_reader_accepts_excel_shared_strings()
    {
        WithPath(path =>
        {
            SheetLinkWorkbookWriter.Write(path, Workbook(revisions: new[] { Revision() }), 0);
            using (var document = SpreadsheetDocument.Open(path, true))
            {
                var part = document.WorkbookPart!;
                var shared = part.AddNewPart<SharedStringTablePart>();
                shared.SharedStringTable = new SharedStringTable(new SharedStringItem(new Text("Thiết kế phát hành")));
                shared.SharedStringTable.Save();
                var sheet = part.Workbook.Sheets!.Elements<Sheet>().Single(item => item.Name == "REVISION");
                var cell = ((WorksheetPart)part.GetPartById(sheet.Id!)).Worksheet.Descendants<Cell>().Single(item => item.CellReference == "F2");
                cell.InlineString = null;
                cell.DataType = CellValues.SharedString;
                cell.CellValue = new CellValue("0");
                ((WorksheetPart)part.GetPartById(sheet.Id!)).Worksheet.Save();
            }
            Assert.Equal("Thiết kế phát hành", Assert.Single(SheetLinkWorkbookReader.Read(path).Revisions).Description);
        });
    }

    [Fact]
    public void Sheet_can_reference_a_revision_created_in_the_same_batch_by_workbook_key()
    {
        var sheet = Sheet() with { Action = SheetLinkAction.Create, UniqueId = null, SheetNumber = "A102", RevisionSequences = "1;key:ISSUE_A" };
        var plan = SheetLinkPreviewPlanner.Plan(Workbook(new[] { sheet }, revisions: new[] { Revision() }), Catalog());
        Assert.Equal(2, plan.ReadyCount);
        Assert.False(plan.HasErrors);
        Assert.Contains(plan.Items, item => item.Kind == SheetLinkRowKind.Sheet && item.Message.Contains("key:ISSUE_A"));
    }

    [Fact]
    public void Failed_revision_validation_blocks_its_sheet_reference()
    {
        var sheet = Sheet() with { RevisionSequences = "key:ISSUE_A" };
        var plan = SheetLinkPreviewPlanner.Plan(Workbook(new[] { sheet }, revisions: new[] { Revision() with { Description = "" } }), Catalog());
        Assert.Contains(plan.Items, item => item.Kind == SheetLinkRowKind.Revision && item.Code.Contains("REVISION_DESCRIPTION_REQUIRED"));
        Assert.Contains(plan.Items, item => item.Kind == SheetLinkRowKind.Sheet && item.Code.Contains("REVISION_KEY_NOT_FOUND"));
    }

    [Fact]
    public void Invalid_revision_create_cannot_alias_an_existing_revision_by_identity()
    {
        var sheet = Sheet() with { RevisionSequences = "key:NEW" };
        var revision = Revision() with { UniqueId = "rev-1", RevisionKey = "NEW" };
        var plan = SheetLinkPreviewPlanner.Plan(Workbook(new[] { sheet }, revisions: new[] { revision }), Catalog());
        Assert.Contains(plan.Items, item => item.Kind == SheetLinkRowKind.Revision && item.Code.Contains("REVISION_CREATE_ID_NOT_EMPTY"));
        var dependent = Assert.Single(plan.Items, item => item.Kind == SheetLinkRowKind.Sheet);
        Assert.Equal(SheetLinkPreviewStatus.Conflict, dependent.Status);
        Assert.Contains("REVISION_KEY_NOT_FOUND", dependent.Code);
        Assert.Empty(plan.ExistingRevisionAliases);
    }

    [Fact]
    public void Invalid_revision_update_cannot_supply_an_existing_alias()
    {
        var sheet = Sheet() with { RevisionSequences = "key:R1" };
        var revision = ExistingRevision() with { Apply = true, SequenceNumber = 99 };
        var plan = SheetLinkPreviewPlanner.Plan(Workbook(new[] { sheet }, revisions: new[] { revision }), Catalog());
        Assert.Contains(plan.Items, item => item.Kind == SheetLinkRowKind.Revision && item.Code.Contains("REVISION_SEQUENCE_READ_ONLY"));
        Assert.Contains(plan.Items, item => item.Kind == SheetLinkRowKind.Sheet && item.Status == SheetLinkPreviewStatus.Conflict && item.Code.Contains("REVISION_KEY_NOT_FOUND"));
    }

    [Theory]
    [InlineData(SheetLinkAction.Update)]
    [InlineData(SheetLinkAction.Skip)]
    public void Unselected_existing_export_row_can_supply_a_valid_alias(SheetLinkAction action)
    {
        var sheet = Sheet() with { RevisionSequences = "key:R1" };
        var plan = SheetLinkPreviewPlanner.Plan(Workbook(new[] { sheet }, revisions: new[] { ExistingRevision() with { Action = action } }), Catalog());
        Assert.False(plan.HasErrors);
        Assert.Contains(plan.Items, item => item.Kind == SheetLinkRowKind.Sheet && item.Status == SheetLinkPreviewStatus.Ready);
        Assert.Equal("rev-1", plan.ExistingRevisionAliases["R1"]);
    }

    [Fact]
    public void Duplicate_active_sheet_update_identities_conflict_even_with_different_numbers()
    {
        var plan = SheetLinkPreviewPlanner.Plan(Workbook(new[] { Sheet(), Sheet() with { SheetNumber = "A102", SheetName = "Other name" } }), Catalog());
        Assert.Equal(2, plan.Items.Count);
        Assert.All(plan.Items, item =>
        {
            Assert.Equal(SheetLinkPreviewStatus.Conflict, item.Status);
            Assert.Contains("SHEET_UPDATE_ID_DUPLICATE", item.Code);
        });
        Assert.Equal(0, plan.ReadyCount);
    }

    [Fact]
    public void Duplicate_active_view_update_identities_conflict_even_with_different_names()
    {
        var plan = SheetLinkPreviewPlanner.Plan(Workbook(views: new[] { View(), View() with { ViewName = "Other name" } }), Catalog());
        Assert.Equal(2, plan.Items.Count);
        Assert.All(plan.Items, item =>
        {
            Assert.Equal(SheetLinkPreviewStatus.Conflict, item.Status);
            Assert.Contains("VIEW_UPDATE_ID_DUPLICATE", item.Code);
        });
        Assert.Equal(0, plan.ReadyCount);
    }

    [Fact]
    public void Duplicate_revision_keys_and_existing_description_date_are_conflicts()
    {
        var plan = SheetLinkPreviewPlanner.Plan(Workbook(revisions: new[] { Revision(), Revision() with { Description = "Other" } }), Catalog());
        Assert.All(plan.Items, item => Assert.Contains("REVISION_KEY_DUPLICATE", item.Code));
        var duplicate = Revision() with { Description = "Existing issue", RevisionDate = "2026-09-01" };
        Assert.Contains("REVISION_ALREADY_EXISTS", Assert.Single(SheetLinkPreviewPlanner.Plan(Workbook(revisions: new[] { duplicate }), Catalog()).Items).Code);
    }

    [Fact]
    public void Revision_update_requires_exact_existing_identity_and_keeps_sequence_read_only()
    {
        var revision = Revision() with { Action = SheetLinkAction.Update, UniqueId = "missing", SequenceNumber = 1 };
        Assert.Contains("REVISION_UPDATE_NOT_FOUND", Assert.Single(SheetLinkPreviewPlanner.Plan(Workbook(revisions: new[] { revision }), Catalog()).Items).Code);
        revision = revision with { UniqueId = "rev-1", SequenceNumber = 99 };
        Assert.Contains("REVISION_SEQUENCE_READ_ONLY", Assert.Single(SheetLinkPreviewPlanner.Plan(Workbook(revisions: new[] { revision }), Catalog()).Items).Code);
    }

    [Fact]
    public void Issued_revision_cannot_be_silently_edited_or_unissued()
    {
        var current = ExistingRevision() with { Issued = true };
        var revision = current with { Apply = true, Description = "Changed", Issued = false };
        var item = Assert.Single(SheetLinkPreviewPlanner.Plan(Workbook(revisions: new[] { revision }), Catalog() with { CurrentRevisions = new[] { current } }).Items);
        Assert.Equal(SheetLinkPreviewStatus.Conflict, item.Status);
        Assert.Contains("REVISION_ISSUED_READ_ONLY", item.Code);
    }

    [Theory]
    [InlineData(SheetLinkViewKind.FloorPlan, "Level 2", "VIEW_LEVEL_UPDATE_BLOCKED")]
    [InlineData(SheetLinkViewKind.CeilingPlan, "Level 1", "VIEW_KIND_UPDATE_BLOCKED")]
    public void View_update_rejects_unsupported_kind_or_level_changes(SheetLinkViewKind kind, string level, string code)
    {
        var row = View() with { ViewKind = kind, LevelName = level };
        var item = Assert.Single(SheetLinkPreviewPlanner.Plan(Workbook(views: new[] { row }), Catalog()).Items);
        Assert.Equal(SheetLinkPreviewStatus.Conflict, item.Status);
        Assert.Contains(code, item.Code);
    }

    [Fact]
    public void Sheet_preview_exposes_titleblock_change_and_revision_removal()
    {
        var row = Sheet() with { TitleBlockType = "A3", RevisionSequences = "1" };
        var item = Assert.Single(SheetLinkPreviewPlanner.Plan(Workbook(new[] { row }), Catalog()).Items);
        Assert.Contains("TITLE_BLOCK_TYPE: A1 → A3", item.Message);
        Assert.Contains("REVISION_SEQUENCES: 1;3 → 1", item.Message);
    }

    [Fact]
    public void View_preview_exposes_name_template_and_viewport_changes()
    {
        var row = View() with { ViewName = "New name", ViewTemplate = "MEP", CenterXMillimeters = 150 };
        var item = Assert.Single(SheetLinkPreviewPlanner.Plan(Workbook(views: new[] { row }), Catalog()).Items);
        Assert.Contains("VIEW_NAME: Existing view → New name", item.Message);
        Assert.Contains("VIEW_TEMPLATE: <empty> → MEP", item.Message);
        Assert.Contains("CENTER_X_MM: 100 → 150", item.Message);
    }

    [Fact]
    public void Unchanged_rows_are_not_applied_and_stale_preview_is_rejected()
    {
        var plan = SheetLinkPreviewPlanner.Plan(Workbook(new[] { Sheet() }), Catalog());
        Assert.Equal(SheetLinkPreviewStatus.NoChange, Assert.Single(plan.Items).Status);
        var workbook = Workbook(new[] { Sheet() with { SheetName = "Changed" } });
        var before = SheetLinkPreviewPlanner.Plan(workbook, Catalog());
        var after = SheetLinkPreviewPlanner.Plan(workbook, Catalog() with { CurrentSheets = new[] { Sheet() with { SheetName = "Someone else changed it" } } });
        Assert.Throws<InvalidOperationException>(() => SheetLinkPreviewPlanner.EnsurePreviewStillCurrent(before, after));
    }

    [Fact]
    public void Changed_revision_identity_behind_a_numeric_sequence_invalidates_reviewed_preview()
    {
        var workbook = Workbook(new[] { Sheet() with { TitleBlockType = "A3" } });
        var before = SheetLinkPreviewPlanner.Plan(workbook, Catalog());
        var after = SheetLinkPreviewPlanner.Plan(workbook, Catalog() with
        {
            CurrentRevisions = new[] { ExistingRevision() with { UniqueId = "a-different-revision" } }
        });
        Assert.Throws<InvalidOperationException>(() => SheetLinkPreviewPlanner.EnsurePreviewStillCurrent(before, after));
    }

    [Fact]
    public void Blank_excel_rows_do_not_shift_the_reported_source_row()
    {
        WithPath(path =>
        {
            SheetLinkWorkbookWriter.Write(path, Workbook(new[] { Sheet() with { SheetName = "Changed" } }), 0);
            using (var document = SpreadsheetDocument.Open(path, true))
            {
                var part = document.WorkbookPart!;
                var sheet = part.Workbook.Sheets!.Elements<Sheet>().Single(item => item.Name == "SHEETS");
                var worksheet = ((WorksheetPart)part.GetPartById(sheet.Id!)).Worksheet;
                var data = worksheet.GetFirstChild<SheetData>()!;
                var row = data.Elements<Row>().Single(item => item.RowIndex?.Value == 2U);
                row.RowIndex = 10U;
                foreach (var cell in row.Elements<Cell>())
                    cell.CellReference = new string(cell.CellReference!.Value!.TakeWhile(char.IsLetter).ToArray()) + "10";
                data.InsertBefore(new Row { RowIndex = 2U }, row);
                worksheet.Save();
            }
            var workbook = SheetLinkWorkbookReader.Read(path);
            Assert.Equal(10, Assert.Single(SheetLinkPreviewPlanner.Plan(workbook, Catalog()).Items).ExcelRow);
        });
    }

    private static SheetLinkRevisionRow Revision() => new(true, SheetLinkAction.Create, null, "ISSUE_A", null,
        "New issue", "2026-10-02", false, "", "", SheetLinkRevisionVisibility.CloudAndTag);
    private static SheetLinkRevisionRow ExistingRevision() => new(false, SheetLinkAction.Update, "rev-1", "R1", 1,
        "Existing issue", "2026-09-01", false, "", "", SheetLinkRevisionVisibility.CloudAndTag);
    private static SheetLinkSheetRow Sheet() => new(true, SheetLinkAction.Update, "sheet-1", "A101", "Existing sheet", "A1", "1;3");
    private static SheetLinkViewRow View() => new(true, SheetLinkAction.Update, "view-1", "Existing view", SheetLinkViewKind.FloorPlan,
        "Level 1", "", "A101", 100, 200);
    private static SheetLinkWorkbook Workbook(SheetLinkSheetRow[]? sheets = null, SheetLinkViewRow[]? views = null, SheetLinkRevisionRow[]? revisions = null) =>
        new(sheets ?? Array.Empty<SheetLinkSheetRow>(), views ?? Array.Empty<SheetLinkViewRow>(), revisions ?? Array.Empty<SheetLinkRevisionRow>());
    private static SheetLinkReferenceCatalog Catalog() => new(
        new[] { new SheetLinkExistingItem("sheet-1", "A101") }, new[] { new SheetLinkExistingItem("view-1", "Existing view") },
        new[] { "A1", "A3" }, new[] { 1, 3 }, new[] { "Level 1", "Level 2" }, new[] { "MEP" },
        new[] { Sheet() }, new[] { View() }, new[] { ExistingRevision() });
    private static void WithPath(Action<string> action)
    {
        var path = Path.Combine(Path.GetTempPath(), "TUNG-SheetAdvanced-" + Guid.NewGuid().ToString("N") + ".xlsx");
        try { action(path); }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
