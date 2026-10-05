using System;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using TungDiroot.Core.Parameters;
using Xunit;

namespace TungDiroot.Core.Tests.Parameters;

public sealed class ParameterManagerTests
{
    [Fact]
    public void Workbook_round_trip_preserves_project_inventory_and_create_templates()
    {
        WithPath(path =>
        {
            var row = ExistingProject();
            ParameterManagerWorkbook.Write(path, ParameterManagerScope.Project, new[] { row }, blankRows: 2);

            var workbook = ParameterManagerWorkbook.Read(path);

            Assert.Equal(ParameterManagerScope.Project, workbook.Scope);
            Assert.Equal(3, workbook.Rows.Count);
            Assert.Equal(row with { CategoryKeys = workbook.Rows[0].CategoryKeys }, workbook.Rows[0]);
            Assert.Equal(row.CategoryKeys, workbook.Rows[0].CategoryKeys);
            Assert.All(workbook.Rows.Skip(1), item => Assert.Equal(ParameterManagerAction.CreateShared, item.Action));
        });
    }

    [Fact]
    public void Planner_never_changes_instance_binding_to_type_binding()
    {
        var current = ExistingProject();
        var edited = current with { Apply = true, BindingKind = ParameterBindingKind.Type };

        var item = Assert.Single(ParameterManagerPlanner.Plan(new ParameterManagerWorkbook(ParameterManagerScope.Project, new[] { edited }),
            Catalog(ParameterManagerScope.Project, current)).Items);

        Assert.Equal(ParameterManagerPreviewStatus.Conflict, item.Status);
        Assert.Equal("BINDING_KIND_CONFLICT", item.Code);
    }

    [Fact]
    public void Project_update_merges_new_categories_without_removing_existing_categories()
    {
        var current = ExistingProject();
        var edited = current with { Apply = true, CategoryKeys = new[] { "OST_Doors" } };

        var item = Assert.Single(ParameterManagerPlanner.Plan(new ParameterManagerWorkbook(ParameterManagerScope.Project, new[] { edited }),
            Catalog(ParameterManagerScope.Project, current)).Items);

        Assert.Equal(ParameterManagerPreviewStatus.Ready, item.Status);
        Assert.Equal(new[] { "OST_Doors", "OST_Walls" }, item.EffectiveCategoryKeys.OrderBy(value => value));
    }

    [Fact]
    public void Shared_or_builtin_family_parameter_cannot_be_renamed()
    {
        var current = new ParameterManagerRow(false, ParameterManagerAction.Update, ParameterManagerScope.Family,
            "shared:aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "Original", "", Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            ParameterBindingKind.Instance, "group:data", Array.Empty<string>(), false);
        var edited = current with { Apply = true, Name = "Renamed" };

        var item = Assert.Single(ParameterManagerPlanner.Plan(new ParameterManagerWorkbook(ParameterManagerScope.Family, new[] { edited }),
            Catalog(ParameterManagerScope.Family, current)).Items);

        Assert.Equal("SHARED_PARAMETER_RENAME_BLOCKED", item.Code);
    }

    [Fact]
    public void Create_shared_requires_matching_definition_group_guid_and_valid_references()
    {
        var guid = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var create = new ParameterManagerRow(true, ParameterManagerAction.CreateShared, ParameterManagerScope.Project,
            "", "Fire Rating", "Company", guid, ParameterBindingKind.Instance, "group:data", new[] { "OST_Doors" }, false);
        var catalog = new ParameterManagerReferenceCatalog(ParameterManagerScope.Project, Array.Empty<ParameterManagerRow>(),
            new[] { "OST_Doors" }, new[] { "group:data" },
            new[] { new SharedParameterDefinitionModel("Company", "Fire Rating", guid) });

        var item = Assert.Single(ParameterManagerPlanner.Plan(new ParameterManagerWorkbook(ParameterManagerScope.Project, new[] { create }), catalog).Items);

        Assert.Equal(ParameterManagerPreviewStatus.Ready, item.Status);
    }

    [Fact]
    public void Writer_includes_group_and_category_reference_tabs()
    {
        WithPath(path =>
        {
            ParameterManagerWorkbook.Write(path, ParameterManagerScope.Project, new[] { ExistingProject() }, 0,
                new[] { "group:data" }, new[] { "OST_Walls" });

            using var document = SpreadsheetDocument.Open(path, false);
            var names = document.WorkbookPart!.Workbook.Sheets!.Elements<Sheet>().Select(sheet => sheet.Name!.Value).ToArray();
            Assert.Equal(new[] { "PARAMETER_MANAGER", "GROUPS", "CATEGORIES" }, names);
        });
    }

    private static ParameterManagerRow ExistingProject() => new(true, ParameterManagerAction.Update, ParameterManagerScope.Project,
        "shared:aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "Comments", "", Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
        ParameterBindingKind.Instance, "group:data", new[] { "OST_Walls" }, false);

    private static ParameterManagerReferenceCatalog Catalog(ParameterManagerScope scope, params ParameterManagerRow[] current) => new(
        scope, current, new[] { "OST_Walls", "OST_Doors" }, new[] { "group:data", "group:geometry" },
        Array.Empty<SharedParameterDefinitionModel>());

    private static void WithPath(Action<string> action)
    {
        var path = Path.Combine(Path.GetTempPath(), "TUNG_DIROOT-ParaManager-" + Guid.NewGuid().ToString("N") + ".xlsx");
        try { action(path); }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
