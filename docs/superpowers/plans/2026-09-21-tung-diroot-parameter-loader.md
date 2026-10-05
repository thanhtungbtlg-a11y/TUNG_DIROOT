# TUNG_DIROOT Parameter Loader Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a safe, preview-first Shared Parameter loader to the existing TUNG_DIROOT Revit 2024 add-in, driven by a Revit Shared Parameter text file and an Excel assignment workbook.

**Architecture:** Pure parsing, validation, and planning live in `TungDiroot.Core.Parameters`; UI state lives in `TungDiroot.Presentation.UI`; Revit snapshots and writes stay in `TungDiroot.Addin.Parameters`. A modeless WPF window uses the existing external-event boundary for every Revit API read/write, and the executor uses one transaction per parameter inside one assimilated transaction group.

**Tech Stack:** C# 10, .NET Standard 2.0 core libraries, .NET Framework 4.8 Revit 2024 add-in, WPF, Autodesk Revit API 2024, DocumentFormat.OpenXml 3.3, xUnit 2.9, PowerShell release checks, Inno Setup 6.

**Spec:** `docs/superpowers/specs/2026-09-21-tung-diroot-parameter-loader-design.md`

## Global Constraints

- Autodesk Revit 2024 is the only supported Revit version.
- Parameter Loader never changes, extends, or replaces an existing parameter binding.
- A name/GUID conflict, Type/Instance mismatch, or missing existing category is review-only.
- Shared Parameters Filename is restored in `finally` after every Revit API operation.
- Excel is opened read-only through Open XML; `.xlsx` and `.xlsm` are supported and macros never run.
- Each new binding has its own transaction; the completed batch is one Revit Undo step.
- TableGen behavior, its singleton window, and its Excel links remain independent.
- No telemetry, network API, Save, Save As, or Synchronize with Central.

## Review Focus

- A parameter name exists with a different GUID: planner returns `ReviewRequired`, and the executor never receives it.
- The selected Shared Parameter file is malformed or lacks the configured group/name: analysis reports row-specific errors without changing Revit's configured Shared Parameters Filename.
- A category key parses as `BuiltInCategory` but is unavailable or does not allow bound parameters in the current document: the row is `InvalidCategory`.
- Existing `settings.json` lacks the two new path properties or contains a now-missing path: settings load succeeds and the window remains usable.
- Revit rejects one `BindingMap.Insert`: that parameter rolls back, later parameters continue, and the whole successful remainder is one Undo item.

---

### Task 1: Parameter domain and assignment planner

**Files:**
- Create: `src/TungDiroot.Core/Parameters/ParameterModels.cs`
- Create: `src/TungDiroot.Core/Parameters/ParameterAssignmentPlanner.cs`
- Create: `tests/TungDiroot.Core.Tests/Parameters/ParameterAssignmentPlannerTests.cs`

**Interfaces:**
- Produces: `ParameterBindingKind`, `ParameterPlanStatus`, `ParameterRuleDraft`, `SharedParameterDefinitionModel`, `ExistingParameterBindingModel`, `CategoryAvailability`, `ParameterAssignmentPlan`.
- Produces: `ParameterAssignmentPlanner.Build(IReadOnlyList<ParameterRuleDraft>, IReadOnlyList<SharedParameterDefinitionModel>, IReadOnlyList<ExistingParameterBindingModel>, IReadOnlyDictionary<string, CategoryAvailability>, IReadOnlyCollection<string>)`.
- Consumers in later tasks treat `ParameterAssignmentPlan.Status == ParameterPlanStatus.Ready` as the only executable state.

- [ ] **Step 1: Write failing planner tests**

Create table-driven tests whose literal expectations cover ready, already bound, name/GUID conflict, kind mismatch, missing requested category, missing definition, invalid category, invalid Revit group, invalid row, and duplicate enabled rules. The critical tests start with:

```csharp
[Fact]
public void Same_name_with_different_guid_requires_review()
{
    var requested = Guid.Parse("11111111-1111-1111-1111-111111111111");
    var existing = Guid.Parse("22222222-2222-2222-2222-222222222222");
    var plans = Build(
        Rule("Identity", "Asset_Code", ParameterBindingKind.Instance, "PG_DATA", "OST_Doors"),
        Definition("Identity", "Asset_Code", requested),
        Existing("Asset_Code", existing, ParameterBindingKind.Instance, "OST_Doors"));

    Assert.Equal(ParameterPlanStatus.ReviewRequired, Assert.Single(plans).Status);
    Assert.Contains("GUID", plans[0].Reason, StringComparison.OrdinalIgnoreCase);
}

[Fact]
public void Existing_binding_missing_a_requested_category_requires_review()
{
    var id = Guid.Parse("11111111-1111-1111-1111-111111111111");
    var plans = Build(
        Rule("Identity", "Asset_Code", ParameterBindingKind.Instance, "PG_DATA", "OST_Doors", "OST_Windows"),
        Definition("Identity", "Asset_Code", id),
        Existing("Asset_Code", id, ParameterBindingKind.Instance, "OST_Doors"));

    Assert.Equal(ParameterPlanStatus.ReviewRequired, Assert.Single(plans).Status);
    Assert.Contains("OST_Windows", plans[0].Reason);
}

[Fact]
public void Duplicate_enabled_rules_are_configuration_errors()
{
    var rules = new[]
    {
        Rule("Identity", "Asset_Code", ParameterBindingKind.Instance, "PG_DATA", "OST_Doors", row: 2),
        Rule("Identity", "Asset_Code", ParameterBindingKind.Instance, "PG_DATA", "OST_Doors", row: 8)
    };

    var plans = Planner.Build(rules, new[] { Definition("Identity", "Asset_Code", KnownGuid) },
        Array.Empty<ExistingParameterBindingModel>(), Categories("OST_Doors"), Groups("PG_DATA"));

    Assert.Equal(new[] { ParameterPlanStatus.ConfigurationError, ParameterPlanStatus.ConfigurationError }, plans.Select(x => x.Status));
}
```

- [ ] **Step 2: Run the planner tests and verify RED**

Run:

```powershell
dotnet test tests/TungDiroot.Core.Tests/TungDiroot.Core.Tests.csproj -c Release --filter FullyQualifiedName~ParameterAssignmentPlannerTests
```

Expected: compilation fails because `TungDiroot.Core.Parameters` does not exist.

- [ ] **Step 3: Implement immutable models and planner**

Use records and enums; normalize category keys with `StringComparer.OrdinalIgnoreCase`. `Build` first marks duplicate enabled `(DefinitionGroup, ParameterName)` pairs, then resolves the definition, category availability, Revit group, and existing binding in that order. Existing lookup checks GUID first and name second. A category superset is `AlreadyBound`; a category subset is `ReviewRequired`.

The public signature is:

```csharp
public sealed class ParameterAssignmentPlanner
{
    public IReadOnlyList<ParameterAssignmentPlan> Build(
        IReadOnlyList<ParameterRuleDraft> rules,
        IReadOnlyList<SharedParameterDefinitionModel> definitions,
        IReadOnlyList<ExistingParameterBindingModel> existingBindings,
        IReadOnlyDictionary<string, CategoryAvailability> categories,
        IReadOnlyCollection<string> supportedRevitGroups);
}
```

Disabled rules return no plan. Invalid fields already recorded in `ParameterRuleDraft.Issues` produce `ConfigurationError` without attempting further resolution.

- [ ] **Step 4: Run the focused tests and full Core suite**

Run the focused command from Step 2, then:

```powershell
dotnet test tests/TungDiroot.Core.Tests/TungDiroot.Core.Tests.csproj -c Release
```

Expected: all tests pass with no warnings or skipped new tests.

- [ ] **Step 5: Commit**

```powershell
git add src/TungDiroot.Core/Parameters tests/TungDiroot.Core.Tests/Parameters
git commit -m "feat: add parameter assignment planner"
```

---

### Task 2: Read and validate the Excel assignment workbook

**Files:**
- Create: `src/TungDiroot.Core/Parameters/ParameterRuleWorkbookReader.cs`
- Modify: `tests/TungDiroot.Core.Tests/Fixtures/TestWorkbookFactory.cs`
- Create: `tests/TungDiroot.Core.Tests/Parameters/ParameterRuleWorkbookReaderTests.cs`

**Interfaces:**
- Consumes: `ParameterRuleDraft` and validation issue types from Task 1.
- Produces: `ParameterRuleWorkbookReader.Read(string path) : IReadOnlyList<ParameterRuleDraft>`.
- The reader returns enabled and disabled non-empty data rows; the planner omits disabled rows.

- [ ] **Step 1: Add real workbook fixtures and failing reader tests**

Extend `TestWorkbookFactory` to create a real `.xlsx` containing `PARAMETER_LOADER` with inline/shared string and Boolean cells. Add tests with literal row expectations:

```csharp
[Fact]
public void Reader_normalizes_headers_values_and_category_duplicates()
{
    using var fixture = TestWorkbookFactory.CreateParameterRuleWorkbook(
        headers: new[] { " enabled ", "Parameter_Group", "PARAMETER_NAME", "binding", "REVIT_GROUP", "categories" },
        rows: new[] { new[] { "YES", " Identity ", " Asset_Code ", " instance ", " PG_DATA ", " OST_Doors ; OST_Windows;OST_Doors " } });

    var rule = Assert.Single(new ParameterRuleWorkbookReader().Read(fixture.Path));

    Assert.True(rule.Enabled);
    Assert.Equal(2, rule.ExcelRow);
    Assert.Equal("Identity", rule.DefinitionGroup);
    Assert.Equal(ParameterBindingKind.Instance, rule.BindingKind);
    Assert.Equal(new[] { "OST_Doors", "OST_Windows" }, rule.CategoryKeys);
    Assert.Empty(rule.Issues);
}

[Theory]
[InlineData("missing-sheet", "PARAMETER_LOADER")]
[InlineData("missing-header", "CATEGORIES")]
public void Reader_reports_workbook_contract_errors(string fixtureKind, string expectedText)
{
    using var fixture = TestWorkbookFactory.CreateInvalidParameterRuleWorkbook(fixtureKind);
    var error = Assert.Throws<InvalidDataException>(() => new ParameterRuleWorkbookReader().Read(fixture.Path));
    Assert.Contains(expectedText, error.Message, StringComparison.OrdinalIgnoreCase);
}
```

Also test `.xls` rejection, `.xlsm` read-only preservation, blank rows, disabled markers, bad binding, empty categories, and an open shared read handle.

- [ ] **Step 2: Run the reader tests and verify RED**

```powershell
dotnet test tests/TungDiroot.Core.Tests/TungDiroot.Core.Tests.csproj -c Release --filter FullyQualifiedName~ParameterRuleWorkbookReaderTests
```

Expected: compilation fails because `ParameterRuleWorkbookReader` and the fixture methods are absent.

- [ ] **Step 3: Implement the read-only Open XML reader**

Open with `FileShare.ReadWrite | FileShare.Delete` and `SpreadsheetDocument.Open(stream, false)`. Resolve shared strings, inline strings and cached values without Excel COM. Require the six canonical headers case-insensitively, retain actual Excel row numbers, and create row-level issue codes such as `BINDING_INVALID`, `REVIT_GROUP_REQUIRED`, and `CATEGORIES_REQUIRED` rather than throwing for bad data rows.

The accepted enabled tokens are exactly `TRUE`, `YES`, `1`, and `X` after trimming/case normalization. Split categories on `;`, trim, remove empty entries, and distinct them case-insensitively while preserving first-seen order.

- [ ] **Step 4: Verify the reader and all Core tests**

Run the focused test, then the full Core suite from Task 1. Expected: all pass and fixture files are disposed/deleted.

- [ ] **Step 5: Commit**

```powershell
git add src/TungDiroot.Core/Parameters/ParameterRuleWorkbookReader.cs tests/TungDiroot.Core.Tests
git commit -m "feat: read parameter assignment workbooks"
```

---

### Task 3: Parse Shared Parameter metadata without touching Revit state

**Files:**
- Create: `src/TungDiroot.Core/Parameters/SharedParameterTextParser.cs`
- Create: `tests/TungDiroot.Core.Tests/Parameters/SharedParameterTextParserTests.cs`

**Interfaces:**
- Produces: `SharedParameterTextParser.Parse(string path) : IReadOnlyList<SharedParameterDefinitionModel>`.
- Consumes: `SharedParameterDefinitionModel` from Task 1.
- Revit execution later resolves the real `ExternalDefinition`; this parser is only for safe preview and source validation.

- [ ] **Step 1: Write failing parser tests using temporary UTF-8 and UTF-16 files**

```csharp
[Fact]
public void Parser_resolves_parameter_group_name_and_guid()
{
    using var file = TempTextFile.Utf16(
        "# This is a Revit shared parameter file.\r\n" +
        "*GROUP\tID\tNAME\r\nGROUP\t1\tIdentity\r\n" +
        "*PARAM\tGUID\tNAME\tDATATYPE\tDATACATEGORY\tGROUP\tVISIBLE\tDESCRIPTION\tUSERMODIFIABLE\r\n" +
        "PARAM\t11111111-1111-1111-1111-111111111111\tAsset_Code\tTEXT\t\t1\t1\tAsset code\t1\r\n");

    var definition = Assert.Single(new SharedParameterTextParser().Parse(file.Path));

    Assert.Equal("Identity", definition.GroupName);
    Assert.Equal("Asset_Code", definition.Name);
    Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), definition.Guid);
}
```

Add cases for group declared after the parameter, unknown group id, malformed GUID, duplicate `(group,name)`, tab-containing standard headers, blank/comment lines, missing file, and both BOM encodings.

- [ ] **Step 2: Run the parser tests and verify RED**

```powershell
dotnet test tests/TungDiroot.Core.Tests/TungDiroot.Core.Tests.csproj -c Release --filter FullyQualifiedName~SharedParameterTextParserTests
```

Expected: compilation fails because the parser is absent.

- [ ] **Step 3: Implement a strict two-pass tab-separated parser**

First collect `GROUP` id/name records, then parse `PARAM` records and resolve group ids. Ignore `META`, header (`*`) and comment (`#`) records. Throw `InvalidDataException` with source line number for malformed active records; never silently invent a group or GUID.

- [ ] **Step 4: Verify focused and full Core suites**

Run the focused parser command and then the full Core test command. Expected: all pass.

- [ ] **Step 5: Commit**

```powershell
git add src/TungDiroot.Core/Parameters/SharedParameterTextParser.cs tests/TungDiroot.Core.Tests/Parameters/SharedParameterTextParserTests.cs
git commit -m "feat: parse shared parameter metadata"
```

---

### Task 4: Parameter Loader presentation state and commands

**Files:**
- Create: `src/TungDiroot.Presentation/UI/ParameterPlanRowViewModel.cs`
- Create: `src/TungDiroot.Presentation/UI/ParameterLoaderViewModel.cs`
- Create: `src/TungDiroot.Presentation/UI/ParameterPlanFilter.cs`
- Create: `tests/TungDiroot.Presentation.Tests/UI/ParameterLoaderViewModelTests.cs`

**Interfaces:**
- Consumes: `ParameterAssignmentPlan` from Task 1.
- Produces: properties `SharedParameterPath`, `WorkbookPath`, `SearchText`, `StatusFilter`, `VisibleRows`, `CanAssign`, `StatusText` and commands `AnalyzeCommand`, `AssignCommand`, `SelectReadyCommand`, `ClearSelectionCommand`, `InvertReadyCommand`.
- Produces: `SetPlans(IReadOnlyList<ParameterAssignmentPlan>)` and `ApplyBatchResult(IReadOnlyList<ParameterAssignmentResult>)`.
- Callbacks: `Action analyze` and `Action<IReadOnlyList<ParameterAssignmentPlan>> assign` are injected; file dialogs stay in the WPF layer.

- [ ] **Step 1: Write failing ViewModel behavior tests**

```csharp
[Fact]
public void Only_ready_rows_can_be_selected_and_assign_receives_visible_or_hidden_selected_rows()
{
    IReadOnlyList<ParameterAssignmentPlan>? requested = null;
    var vm = new ParameterLoaderViewModel(analyze: () => { }, assign: plans => requested = plans);
    vm.SetPlans(new[] { Plan("A", ParameterPlanStatus.Ready), Plan("B", ParameterPlanStatus.ReviewRequired) });

    vm.SelectReadyCommand.Execute(null);
    vm.SearchText = "B";
    vm.AssignCommand.Execute(null);

    Assert.Equal("A", Assert.Single(requested!).Definition!.Name);
    Assert.False(vm.VisibleRows[0].CanSelect);
}

[Fact]
public void Assign_is_disabled_without_a_selected_ready_row()
{
    var vm = new ParameterLoaderViewModel(() => { }, _ => { });
    vm.SetPlans(new[] { Plan("A", ParameterPlanStatus.AlreadyBound) });
    Assert.False(vm.AssignCommand.CanExecute(null));
}
```

Also test search across group/name/GUID/category/reason, status filtering, selection preservation across filters, invert-ready behavior, source-path changes clearing stale plans, and status counts.

- [ ] **Step 2: Run Presentation tests and verify RED**

```powershell
dotnet test tests/TungDiroot.Presentation.Tests/TungDiroot.Presentation.Tests.csproj -c Release --filter FullyQualifiedName~ParameterLoaderViewModelTests
```

Expected: compilation fails because the new ViewModel types are absent.

- [ ] **Step 3: Implement the ViewModel and filter**

Reuse `BindableBase` and `RelayCommand`. Keep a private `_allRows` array; filtering only rebuilds `VisibleRows` and never discards selection. `ParameterPlanRowViewModel.IsSelected` coerces false when `CanSelect` is false and notifies the owner so `AssignCommand.RaiseCanExecuteChanged()` runs immediately.

- [ ] **Step 4: Run focused and full Presentation suites**

Run the focused command, then:

```powershell
dotnet test tests/TungDiroot.Presentation.Tests/TungDiroot.Presentation.Tests.csproj -c Release
```

Expected: all tests pass.

- [ ] **Step 5: Commit**

```powershell
git add src/TungDiroot.Presentation/UI tests/TungDiroot.Presentation.Tests/UI/ParameterLoaderViewModelTests.cs
git commit -m "feat: add Parameter Loader view model"
```

---

### Task 5: Revit snapshot, shared-definition scope, and assignment executor

**Files:**
- Create: `src/TungDiroot.Addin/Parameters/RevitBindingSnapshotReader.cs`
- Create: `src/TungDiroot.Addin/Parameters/RevitCategoryCatalog.cs`
- Create: `src/TungDiroot.Addin/Parameters/RevitParameterGroupCatalog.cs`
- Create: `src/TungDiroot.Addin/Parameters/SharedDefinitionResolver.cs`
- Create: `src/TungDiroot.Addin/Parameters/RevitParameterAssignmentExecutor.cs`
- Create: `src/TungDiroot.Addin/Parameters/ParameterAssignmentResult.cs`
- Create: `src/TungDiroot.Core/Parameters/TemporaryValueScope.cs`
- Create: `tests/TungDiroot.Core.Tests/Parameters/TemporaryValueScopeTests.cs`
- Create: `tools/Test-ParameterLoaderAddinContract.ps1`
- Modify: `tools/Test-All.ps1`

**Interfaces:**
- `RevitBindingSnapshotReader.Read(Document) : IReadOnlyList<ExistingParameterBindingModel>`.
- `RevitCategoryCatalog.Read(Document) : IReadOnlyDictionary<string, CategoryAvailability>` and `CreateCategorySet(Application, Document, IEnumerable<string>)`.
- `RevitParameterGroupCatalog.Keys` and `Parse(string) : BuiltInParameterGroup` for Revit 2024 `PG_*` values.
- `SharedDefinitionResolver.Use<T>(Application, string, Func<DefinitionFile,T>) : T` restores the old path in `finally`.
- `RevitParameterAssignmentExecutor.Execute(Document, Application, string, IReadOnlyList<ParameterAssignmentPlan>) : IReadOnlyList<ParameterAssignmentResult>`.
- `TemporaryValueScope.Use<T>(Func<string?>, Action<string?>, string, Func<T>) : T` is the real, Revit-free restoration primitive used by `SharedDefinitionResolver`.

- [ ] **Step 1: Add a failing executable reflection contract**

`Test-ParameterLoaderAddinContract.ps1` loads the built add-in with RevitAPI resolution and verifies these exact integration types: `RevitBindingSnapshotReader`, `RevitCategoryCatalog`, `RevitParameterGroupCatalog`, `SharedDefinitionResolver`, and `RevitParameterAssignmentExecutor`.

Add real Core tests for the restoration behavior:

```csharp
[Fact]
public void Temporary_value_is_restored_after_action_throws()
{
    string? current = "old.txt";

    Assert.Throws<InvalidOperationException>(() => TemporaryValueScope.Use(
        () => current,
        value => current = value,
        "selected.txt",
        () => throw new InvalidOperationException("read failed")));

    Assert.Equal("old.txt", current);
}
```

Run:

```powershell
& .\build\Build-Addin.ps1 -RevitVersion 2024 -Configuration Release
& .\tools\Test-ParameterLoaderAddinContract.ps1 -RevitVersion 2024
```

Expected: contract fails because the integration types do not exist.

- [ ] **Step 2: Implement category/group and binding snapshots**

Iterate `Document.ParameterBindings.ForwardIterator()`. Pair each definition name with `SharedParameterElement` metadata to obtain the persisted GUID, detect `InstanceBinding` versus `TypeBinding`, and emit stable category keys using `Category.BuiltInCategory`. Categories that cannot produce a stable `OST_*` enum key are retained in diagnostics but never treated as satisfying a requested key.

`RevitCategoryCatalog` parses only `BuiltInCategory` names beginning `OST_`, resolves the category in the active document, and sets `CategoryAvailability(false, reason)` when missing or `AllowsBoundParameters == false`.

- [ ] **Step 3: Implement safe definition scoping and batch writes**

`SharedDefinitionResolver.Use` stores `application.SharedParametersFilename`, sets the selected path, calls `OpenSharedParameterFile`, invokes the callback, then restores the exact old string in `finally`.

For each selected Ready plan, resolve the `ExternalDefinition` by group/name and verify its GUID again. Build a fresh `CategorySet` and `InstanceBinding`/`TypeBinding`; call `Document.ParameterBindings.Insert(definition, binding, parsedGroup)`. A false return is a failed item. Use:

```csharp
using var group = new TransactionGroup(document, "TUNG_DIROOT - Gán Shared Parameters");
group.Start();
foreach (var plan in plans)
{
    using var transaction = new Transaction(document, "Gán parameter - " + plan.Definition!.Name);
    transaction.Start();
    try
    {
        var inserted = document.ParameterBindings.Insert(definition, binding, parameterGroup);
        if (!inserted) throw new InvalidOperationException("REVIT_BINDING_INSERT_RETURNED_FALSE");
        if (transaction.Commit() != TransactionStatus.Committed)
            throw new InvalidOperationException("REVIT_TRANSACTION_NOT_COMMITTED");
        results.Add(ParameterAssignmentResult.Assigned(plan));
    }
    catch (Exception exception)
    {
        if (transaction.GetStatus() == TransactionStatus.Started) transaction.RollBack();
        results.Add(ParameterAssignmentResult.Failed(plan, Sanitize(exception.Message)));
    }
}
group.Assimilate();
```

Never call `ReInsert`. If the resolver no longer matches the previewed GUID, fail that item before starting its transaction.

- [ ] **Step 4: Run the contract, add-in build, and complete automated suite**

```powershell
& .\tools\Test-ParameterLoaderAddinContract.ps1 -RevitVersion 2024
& .\tools\Test-All.ps1
```

Expected: all pass; `artifacts/addin/2024` contains no `RevitAPI*.dll`.

- [ ] **Step 5: Commit**

```powershell
git add src/TungDiroot.Addin/Parameters src/TungDiroot.Core/Parameters/TemporaryValueScope.cs tests/TungDiroot.Core.Tests/Parameters/TemporaryValueScopeTests.cs tools/Test-ParameterLoaderAddinContract.ps1 tools/Test-All.ps1
git commit -m "feat: bind shared parameters in Revit 2024"
```

---

### Task 6: Modeless WPF window, external-event coordination, ribbon, and settings

**Files:**
- Create: `src/TungDiroot.Addin/Command_ParameterLoader.cs`
- Create: `src/TungDiroot.Addin/UI/ParameterLoaderWindow.xaml`
- Create: `src/TungDiroot.Addin/UI/ParameterLoaderWindow.xaml.cs`
- Create: `src/TungDiroot.Addin/Resources/ParameterLoader16.png`
- Create: `src/TungDiroot.Addin/Resources/ParameterLoader32.png`
- Modify: `src/TungDiroot.Addin/Application.cs`
- Modify: `src/TungDiroot.Addin/TungDiroot.Addin.csproj`
- Move: `src/TungDiroot.Addin/Settings/UserSettings.cs` to `src/TungDiroot.Core/Settings/UserSettings.cs`
- Move: `src/TungDiroot.Addin/Settings/UserSettingsStore.cs` to `src/TungDiroot.Core/Settings/UserSettingsStore.cs`
- Create: `tests/TungDiroot.Core.Tests/Settings/UserSettingsStoreTests.cs`
- Modify: `tools/Test-AddinContract.ps1`
- Modify: `tools/Test-ResponsiveWindowLayout.ps1`

**Interfaces:**
- `Command_ParameterLoader : IExternalCommand` calls `ParameterLoaderWindow.ShowOrActivate(commandData.Application)`.
- Window analysis combines Task 2 reader, Task 3 parser, Task 5 snapshot/catalog, then calls Task 1 planner.
- Every Revit read/write is raised through its own `RevitActionExternalEvent`; before work it compares the stored document with the active document using `NativeObjectIdentity.IsSame`.

- [ ] **Step 1: Write failing settings and UI contract tests**

Add a settings test that deserializes this legacy payload and asserts default null paths:

```csharp
var json = "{\"DefaultDpi\":2,\"RendererPreference\":\"Automatic\",\"PreferRelativePaths\":true,\"ConfirmLargeImports\":true}";
File.WriteAllText(path, json);
var settings = new UserSettingsStore(path).Load();
Assert.Null(settings.LastSharedParameterPath);
Assert.Null(settings.LastParameterWorkbookPath);
```

Extend the executable PowerShell contracts to require `Command_ParameterLoader`, two distinct ribbon items, two embedded icons, `MinWidth <= Width`, `MinHeight <= Height`, a star-sized DataGrid region, and bottom actions outside the scrolling grid.

- [ ] **Step 2: Run tests/contracts and verify RED**

```powershell
dotnet test tests/TungDiroot.Presentation.Tests/TungDiroot.Presentation.Tests.csproj -c Release
& .\build\Build-Addin.ps1 -RevitVersion 2024 -Configuration Release
& .\tools\Test-AddinContract.ps1 -RevitVersion 2024
& .\tools\Test-ResponsiveWindowLayout.ps1
```

Expected: new settings properties, command, ribbon item, resources, and window are absent.

- [ ] **Step 3: Implement the window and coordinator**

Use `WindowSizePolicy.Clamp(1500, 900, MinWidth, MinHeight, workArea...)`. Browse buttons use `OpenFileDialog` filters for `.txt` and `.xlsx;*.xlsm`. Changing either source clears stale plans. Analysis catches `IOException`, `InvalidDataException`, and Revit adapter errors into Vietnamese status text and rolling log; it does not close the window.

Before assignment, show a confirmation containing selected count and skipped count. After executor completion, call `ApplyBatchResult` and show a single summary with Assigned/Already bound/Review/Missing/Failed counts. Closing the Parameter Loader never closes TableGen, and reopening for the same document activates the existing instance.

- [ ] **Step 4: Add ribbon and backward-compatible settings**

Move the two currently unused settings types into `TungDiroot.Core.Settings` so compatibility can be tested without loading Revit assemblies. Create/find panel `Parameter Tools`; add button id `TUNG_DIROOT.ParameterLoader`, label `Parameter\nLoader`, tooltip describing TXT + Excel preview, and the two resources. Change `UserSettings` to:

```csharp
public sealed record UserSettings(
    ImageDpi DefaultDpi = ImageDpi.Dpi300,
    string RendererPreference = "Automatic",
    bool PreferRelativePaths = true,
    bool ConfirmLargeImports = true,
    string? LastSharedParameterPath = null,
    string? LastParameterWorkbookPath = null);
```

Save paths only after a successful analysis; missing persisted files populate the text boxes but do not auto-analyze or throw.

- [ ] **Step 5: Verify all tests and build contracts**

Run the commands from Step 2 followed by:

```powershell
& .\tools\Test-All.ps1
```

Expected: all pass and both commands implement `IExternalCommand`.

- [ ] **Step 6: Commit**

```powershell
git add src/TungDiroot.Addin src/TungDiroot.Core/Settings tests/TungDiroot.Core.Tests/Settings tools/Test-AddinContract.ps1 tools/Test-ResponsiveWindowLayout.ps1
git commit -m "feat: add Parameter Loader window and ribbon command"
```

---

### Task 7: Sample workbook, user documentation, and Revit checklist

**Files:**
- Create: `samples/Create-TUNG_DIROOT-ParameterLoader.ps1`
- Generate: `samples/TUNG_DIROOT-ParameterLoader.xlsx`
- Create: `tools/TungDiroot.ParameterSample/TungDiroot.ParameterSample.csproj`
- Create: `tools/TungDiroot.ParameterSample/Program.cs`
- Modify: `TUNG_DIROOT.sln`
- Modify: `docs/user-guide-vi.md`
- Modify: `docs/troubleshooting-vi.md`
- Modify: `docs/architecture.md`
- Modify: `docs/testing/revit-2024-checklist.md`
- Modify: `README.md`
- Modify: `tools/Test-All.ps1`

**Interfaces:**
- The sample generator creates worksheet `PARAMETER_LOADER` and exact six-header contract from the spec.
- `Test-All.ps1` regenerates the sample and runs a small Core-backed probe or test against the generated file, proving the shipped sample is consumable rather than grepping its source.

- [ ] **Step 1: Add a failing sample-consumption release check**

Extend `Test-All.ps1` to invoke the generator, then run a filtered Core integration test with `TUNG_DIROOT_PARAMETER_SAMPLE` set to the generated absolute path. The test asserts the sample contains at least two disabled valid example rules, one Instance and one Type, and zero row issues.

Run:

```powershell
& .\tools\Test-All.ps1 -SkipRevitBuild
```

Expected: failure because the generator and workbook do not exist.

- [ ] **Step 2: Implement the generator and sample workbook**

Implement `tools/TungDiroot.ParameterSample` as the exact .NET console generator using `DocumentFormat.OpenXml`; `Create-TUNG_DIROOT-ParameterLoader.ps1` invokes that project and accepts `-OutputPath`. Example rows are disabled and use stable keys:

```text
FALSE | Identity | Asset_Code    | INSTANCE | PG_DATA     | OST_Doors;OST_Windows
FALSE | Product  | Manufacturer  | TYPE     | PG_IDENTITY | OST_Doors;OST_Windows;OST_Furniture
```

Do not include real project/customer data.

- [ ] **Step 3: Document the full user flow and failures**

The Vietnamese guide must explain: TXT and Excel roles; required headers; `OST_*` and `PG_*`; Check before Assign; seven statuses; why existing mismatches are not auto-fixed; Undo; log location; and how to restore/reselect missing files. The checklist adds creation, conflict, rollback/continue, family/no-active-document, path restoration, DPI layout, and TableGen regression cases.

- [ ] **Step 4: Run docs and automated checks**

```powershell
& .\tools\Test-All.ps1 -SkipRevitBuild
& .\tools\Test-All.ps1 -DocsOnly
```

Expected: generated sample is parsed successfully and all documentation links resolve.

- [ ] **Step 5: Commit**

```powershell
git add samples docs README.md tools/TungDiroot.ParameterSample tools/Test-All.ps1 TUNG_DIROOT.sln
git commit -m "docs: add Parameter Loader sample and guide"
```

---

### Task 8: Version 1.1.0 release candidate, backup, and installable packaging

**Files:**
- Modify: `build/version.json`
- Modify: `build/Build-Release.ps1`
- Modify: `installer/TUNG_DIROOT.iss`
- Modify: `tools/Test-ReleaseLayout.ps1`
- Modify: `tools/Test-PortableInstall.ps1`
- Modify: `docs/testing/release-matrix.md`
- Create: `docs/testing/release-1.1.0-report.md`

**Interfaces:**
- `Build-Release.ps1 -Version 1.1.0 -ReleaseCandidate` creates `dist/TUNG_DIROOT-Setup-Revit2024.exe`, portable zip, and hashes.
- Staging includes only runtime DLL/JSON under `2024`; the sample workbook and user docs live in an explicit `Samples`/`Docs` payload path.

- [ ] **Step 1: Write failing release-layout assertions**

Extend release checks to require version `1.1.0`, `Samples/TUNG_DIROOT-ParameterLoader.xlsx`, updated user guide, Parameter Loader command/icon resources in the assembly contract, and absence of `RevitAPI*.dll` everywhere.

Run:

```powershell
& .\tools\Test-ReleaseLayout.ps1 -StageRoot .\artifacts\staging\TUNG_DIROOT
```

Expected: failure because the current staged release remains 1.0.0 and has no Parameter Loader sample/docs payload.

- [ ] **Step 2: Update version and packaging**

Change all locked `1.0.0` release values to `1.1.0`, update the Inno Setup display version/uninstall run id, and stage the sample plus Vietnamese docs. Preserve the product `AppId` so installing 1.1.0 upgrades the existing TUNG_DIROOT product rather than creating a duplicate Windows application entry.

- [ ] **Step 3: Run the complete verification gate**

```powershell
& .\tools\Test-All.ps1
git status --short
```

Expected: automated suite passes. Commit all release metadata before building because `Build-Release.ps1` requires a clean tree.

- [ ] **Step 4: Commit release metadata**

```powershell
git add build installer tools docs/testing
git commit -m "chore: prepare TUNG_DIROOT 1.1.0"
```

- [ ] **Step 5: Create a recoverable backup of the prior installer**

Resolve exact source files first. If `dist/TUNG_DIROOT-Setup-Revit2024.exe` or the portable zip from 1.0.0 exists, copy them (never move/delete) into a timestamped directory under `C:\Users\tungnt2303\Downloads\TUNG_DIROOT_Backups\1.0.0-before-parameter-loader-<yyyyMMdd-HHmmss>`. Record the backup path and hashes in the final report.

- [ ] **Step 6: Build the 1.1.0 release candidate**

```powershell
& .\build\Build-Release.ps1 -Version 1.1.0 -ReleaseCandidate
Get-FileHash .\dist\TUNG_DIROOT-Setup-Revit2024.exe -Algorithm SHA256
Get-FileHash .\dist\TUNG_DIROOT-Portable-Revit2024.zip -Algorithm SHA256
```

Expected: setup, portable zip, and `SHA256SUMS.txt` exist and hashes match.

- [ ] **Step 7: Execute the Revit 2024 manual checklist before final-release status**

Run every new section in `docs/testing/revit-2024-checklist.md`. Record PASS/FAIL and evidence in `docs/testing/release-1.1.0-report.md`. Until these checks pass, label artifacts `release candidate`; do not claim the Revit integration is fully verified merely because compilation succeeded.

- [ ] **Step 8: Commit the verified report or report the blocker**

If direct Revit checks pass:

```powershell
git add docs/testing/release-1.1.0-report.md docs/testing/release-matrix.md
git commit -m "test: verify TUNG_DIROOT 1.1.0 in Revit 2024"
```

If Revit cannot be driven in the session, leave the report explicitly `PENDING MANUAL REVIT CHECK`, provide the release-candidate installer, and state exactly which checklist cases remain for the user.
