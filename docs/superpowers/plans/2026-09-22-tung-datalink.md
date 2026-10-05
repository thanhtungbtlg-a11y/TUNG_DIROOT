# TUNG_DataLink Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a modeless Revit 2024 tool that exports selected element parameters to a protected Excel workbook, previews safe changes, and imports approved values back with one Undo operation.

**Architecture:** Pure Core types own snapshots, workbook schema, conversion, conflicts, and change planning. The Addin project is the only layer that reads or writes Revit and does so through `RevitActionExternalEvent`; Open XML I/O operates on immutable DTOs outside the Revit API callback. Presentation exposes testable view models while WPF remains a thin shell.

**Tech Stack:** C#; .NET Standard 2.0 Core/Presentation; .NET Framework 4.8 Revit 2024 add-in; Autodesk Revit API 2024; DocumentFormat.OpenXml 3.3; WPF; xUnit.

**Spec:** In-chat DataLink design approved by the user on 2026-09-22; the user explicitly waived a separate specification document.

## Global Constraints

- Support Autodesk Revit 2024 only.
- Keep TableGen and Parameter Loader behavior and persisted data backward compatible.
- Never call Revit API objects outside an `ExternalEvent` callback.
- Never write linked documents, save the model, or synchronize with central.
- V1 writes ordinary writable instance/type parameters only; Family/Type, Level, Workset, Material, and other `ElementId` references remain preview-only.
- Excel metadata uses stable Revit `UniqueId` and parameter identity; visible row numbers and `ElementId` are informational only.
- One import batch is one Revit Undo item; one failed element must not cancel valid elements.
- All user-facing errors are Vietnamese and logs must not hide the original exception.

## Review Focus

- Workbook copied to a different model must be blocked before any transaction starts.
- Two rows that target the same TypeId/parameter with different values must be reported as a conflict and not written.
- A value changed in Revit after export must appear as stale/conflict instead of being overwritten silently.
- Locale-specific decimal text and Revit display units must convert deterministically or be rejected.
- Read-only, missing, deleted, linked, and non-editable workshared elements must remain selectable only as report rows, never executable changes.

---

### Task 1: DataLink domain and deterministic change planner

**Files:**
- Create: `src/TungDiroot.Core/DataLink/DataLinkModels.cs`
- Create: `src/TungDiroot.Core/DataLink/DataLinkChangePlanner.cs`
- Test: `tests/TungDiroot.Core.Tests/DataLink/DataLinkChangePlannerTests.cs`

**Interfaces:**
- Produces: `DataLinkDocumentStamp`, `DataLinkParameterKey`, `DataLinkElementRow`, `DataLinkWorkbook`, `DataLinkCandidateChange`, `DataLinkChangePlan`, `DataLinkChangePlanner.Create(...)`.
- Consumes: only BCL types so tests run without Revit.

- [ ] **Step 1: Write failing planner tests**

```csharp
[Fact]
public void Conflicting_type_values_are_blocked_as_one_group()
{
    var rows = DataLinkFixtures.TypeRows("type-1", "Comments", "A", "B");
    var plan = DataLinkChangePlanner.Create(rows, rows.DocumentStamp, rows.CurrentValues);
    Assert.All(plan.Changes, item => Assert.Equal(DataLinkChangeStatus.TypeConflict, item.Status));
}

[Fact]
public void Stale_revit_value_is_not_overwritten()
{
    var rows = DataLinkFixtures.InstanceRow(original: "A", edited: "B", current: "C");
    var plan = DataLinkChangePlanner.Create(rows.Rows, rows.DocumentStamp, rows.CurrentValues);
    Assert.Equal(DataLinkChangeStatus.StaleModelValue, Assert.Single(plan.Changes).Status);
}
```

- [ ] **Step 2: Run the focused tests and verify RED**

Run: `dotnet test tests/TungDiroot.Core.Tests/TungDiroot.Core.Tests.csproj --filter FullyQualifiedName~DataLinkChangePlannerTests`

Expected: FAIL because the `TungDiroot.Core.DataLink` types do not exist.

- [ ] **Step 3: Implement immutable domain types and planner**

```csharp
public enum DataLinkChangeStatus { Ready, NoChange, DifferentDocument, ElementMissing, ParameterMissing, ReadOnly, UnsupportedStorage, InvalidValue, StaleModelValue, TypeConflict, NotEditable }

public sealed record DataLinkParameterKey(string StableId, string Name, bool IsType, string StorageKind, string? SpecId);
public sealed record DataLinkElementRow(string ElementUniqueId, int ElementId, string? TypeUniqueId, string Category, string Family, string TypeName, DataLinkParameterKey Parameter, string OriginalInternal, string OriginalDisplay, string EditedDisplay);
public sealed record DataLinkCandidateChange(DataLinkElementRow Row, DataLinkChangeStatus Status, string? ParsedInternal, string Message);
public sealed record DataLinkChangePlan(IReadOnlyList<DataLinkCandidateChange> Changes)
{
    public IReadOnlyList<DataLinkCandidateChange> Ready => Changes.Where(item => item.Status == DataLinkChangeStatus.Ready).ToArray();
}
```

`DataLinkChangePlanner.Create` must first compare the document stamp, then resolve current values by `(ElementUniqueId, Parameter.StableId, IsType)`, reject stale originals, parse supported String/Integer/Double values with invariant internal storage, and finally group type changes by `(TypeUniqueId, Parameter.StableId)` to mark inconsistent edited values as `TypeConflict`.

- [ ] **Step 4: Add tests for all Review Focus planner cases and run GREEN**

Run: `dotnet test tests/TungDiroot.Core.Tests/TungDiroot.Core.Tests.csproj --filter FullyQualifiedName~DataLink`

Expected: PASS for document mismatch, stale value, type conflict, read-only/missing state, numeric parsing, and no-change cases.

- [ ] **Step 5: Commit the domain slice**

```powershell
git add src/TungDiroot.Core/DataLink tests/TungDiroot.Core.Tests/DataLink
git commit -m "feat: add DataLink change planning core"
```

### Task 2: Protected Open XML export/import contract

**Files:**
- Create: `src/TungDiroot.Core/DataLink/DataLinkWorkbookSchema.cs`
- Create: `src/TungDiroot.Core/DataLink/DataLinkWorkbookWriter.cs`
- Create: `src/TungDiroot.Core/DataLink/DataLinkWorkbookReader.cs`
- Test: `tests/TungDiroot.Core.Tests/DataLink/DataLinkWorkbookRoundTripTests.cs`

**Interfaces:**
- Consumes: `DataLinkWorkbook` and `DataLinkElementRow` from Task 1.
- Produces: `DataLinkWorkbookWriter.Write(string path, DataLinkWorkbook workbook)` and `DataLinkWorkbookReader.Read(string path)`.

- [ ] **Step 1: Write a failing round-trip test**

```csharp
[Fact]
public void Round_trip_preserves_hidden_identity_and_edited_visible_value()
{
    var path = TempWorkbook.Path();
    var source = DataLinkFixtures.Workbook();
    DataLinkWorkbookWriter.Write(path, source);
    WorkbookEditor.SetCell(path, "DATA", "H2", "Edited");
    var imported = DataLinkWorkbookReader.Read(path);
    Assert.Equal(source.Rows[0].ElementUniqueId, imported.Rows[0].ElementUniqueId);
    Assert.Equal("Edited", imported.Rows[0].EditedDisplay);
}
```

- [ ] **Step 2: Run the test and verify RED**

Run: `dotnet test tests/TungDiroot.Core.Tests/TungDiroot.Core.Tests.csproj --filter FullyQualifiedName~DataLinkWorkbookRoundTripTests`

Expected: FAIL because writer/reader do not exist.

- [ ] **Step 3: Implement workbook schema**

Create visible worksheet `DATA` with columns `Apply`, `ElementId`, `Category`, `Family`, `Type`, `Parameter`, `Original`, `Edited`, `Status`; create very-hidden worksheet `_TUNG_META` with `SchemaVersion`, `DocumentStamp`, `ElementUniqueId`, `TypeUniqueId`, `ParameterStableId`, `IsType`, `StorageKind`, `SpecId`, `OriginalInternal`, and `RowChecksum`. Protect `_TUNG_META`, freeze the header, enable filters, and store schema version `1`.

- [ ] **Step 4: Reject tampering and malformed files**

Add tests proving the reader rejects missing metadata, a duplicate metadata row, mismatched checksums, unsupported schema versions, and edited read-only rows with Vietnamese diagnostic codes.

- [ ] **Step 5: Run DataLink Core tests and commit**

Run: `dotnet test tests/TungDiroot.Core.Tests/TungDiroot.Core.Tests.csproj --filter FullyQualifiedName~DataLink`

Expected: PASS.

```powershell
git add src/TungDiroot.Core/DataLink tests/TungDiroot.Core.Tests/DataLink
git commit -m "feat: add protected DataLink workbook format"
```

### Task 3: Revit snapshot, revalidation, and safe assignment

**Files:**
- Create: `src/TungDiroot.Addin/DataLink/RevitDataLinkSnapshotReader.cs`
- Create: `src/TungDiroot.Addin/DataLink/RevitDataLinkAssignmentExecutor.cs`
- Create: `src/TungDiroot.Addin/DataLink/RevitParameterValueCodec.cs`
- Test: `tests/contracts/Test-DataLinkAddinContract.ps1`
- Modify: `tools/Test-All.ps1`

**Interfaces:**
- Produces: `RevitDataLinkSnapshotReader.Read(UIDocument, DataLinkQuery)` returning Core DTOs with no live Revit objects.
- Produces: `RevitDataLinkAssignmentExecutor.Execute(Document, DataLinkChangePlan)` returning per-row `DataLinkApplyResult`.
- Consumes: only plans whose status is `Ready` and whose document stamp matches the active document.

- [ ] **Step 1: Add a failing structural contract test**

The PowerShell test must assert that snapshot/assignment classes exist, commands use `RevitActionExternalEvent`, `TransactionGroup` is named `TUNG_DIROOT - DataLink`, linked documents are rejected, and no `Document.Save`/`SynchronizeWithCentral` call exists.

- [ ] **Step 2: Run the contract and verify RED**

Run: `powershell -ExecutionPolicy Bypass -File tests/contracts/Test-DataLinkAddinContract.ps1`

Expected: FAIL because the Revit adapter files do not exist.

- [ ] **Step 3: Implement snapshot and value codec**

Snapshot only requested categories/parameters and emit stable IDs, raw internal values, formatted display values, read-only state, type ownership, and worksharing checkout state. `RevitParameterValueCodec` supports `StorageType.String`, `Integer`, and `Double`; `StorageType.ElementId` returns `UnsupportedStorage` in v1.

- [ ] **Step 4: Implement the assignment executor**

Start one `TransactionGroup`; re-resolve each element by `UniqueId`, re-resolve parameter identity, re-read the original value, and create one `Transaction` per element or type group. Roll back the local transaction on failure, collect the failure, then `Assimilate` the group so successful changes form one Undo item.

- [ ] **Step 5: Run add-in build/contracts and commit**

Run: `powershell -ExecutionPolicy Bypass -File tools/Test-AddinContract.ps1`

Run: `powershell -ExecutionPolicy Bypass -File tests/contracts/Test-DataLinkAddinContract.ps1`

Expected: PASS.

```powershell
git add src/TungDiroot.Addin/DataLink tests/contracts/Test-DataLinkAddinContract.ps1 tools/Test-All.ps1
git commit -m "feat: add safe Revit DataLink adapter"
```

### Task 4: Testable DataLink presentation model

**Files:**
- Create: `src/TungDiroot.Presentation/DataLink/DataLinkViewModel.cs`
- Create: `src/TungDiroot.Presentation/DataLink/DataLinkRows.cs`
- Create: `src/TungDiroot.Core/DataLink/DataLinkProfile.cs`
- Create: `src/TungDiroot.Core/Profiles/JsonProfileStore.cs`
- Create: `src/TungDiroot.Core/DataLink/DataLinkProfileRepository.cs`
- Test: `tests/TungDiroot.Presentation.Tests/DataLink/DataLinkViewModelTests.cs`
- Test: `tests/TungDiroot.Core.Tests/Profiles/JsonProfileStoreTests.cs`
- Test: `tests/TungDiroot.Core.Tests/DataLink/DataLinkProfileRepositoryTests.cs`

**Interfaces:**
- Produces: selected scope/category/parameter query, preview rows, command readiness, and progress state without referencing Autodesk assemblies.
- Produces: reusable `JsonProfileStore<TProfile>` with atomic writes plus a DataLink repository rooted at `%LocalAppData%\TUNG_DIROOT\profiles\datalink`.

- [ ] **Step 1: Write failing view-model/profile tests**

Cover: only `Ready` changes can be applied; Select All never selects blocked rows; busy state disables export/import; closing is rejected while an external action is pending; corrupted profile JSON loads as an isolated error and does not prevent other profiles loading.

- [ ] **Step 2: Run focused tests and verify RED**

Run: `dotnet test tests/TungDiroot.Presentation.Tests/TungDiroot.Presentation.Tests.csproj --filter FullyQualifiedName~DataLink`

Run: `dotnet test tests/TungDiroot.Core.Tests/TungDiroot.Core.Tests.csproj --filter FullyQualifiedName~DataLinkProfile`

- [ ] **Step 3: Implement profile and view-model state**

`JsonProfileStore<TProfile>` accepts a directory and a validated name selector, saves atomically through a temporary file and replace, and isolates corrupt files during load. `DataLinkProfile` contains `SchemaVersion`, `Name`, `Scope`, selected built-in/category IDs, family/type filters, and parameter stable IDs; `DataLinkProfileRepository` binds the generic store to the DataLink directory and schema validation.

- [ ] **Step 4: Run focused tests and commit**

Expected: all DataLink presentation/profile tests PASS.

```powershell
git add src/TungDiroot.Core/DataLink src/TungDiroot.Core/Profiles src/TungDiroot.Presentation/DataLink tests/TungDiroot.Core.Tests/DataLink tests/TungDiroot.Core.Tests/Profiles tests/TungDiroot.Presentation.Tests/DataLink
git commit -m "feat: add DataLink profiles and presentation state"
```

### Task 5: Modeless WPF window, command, and ribbon integration

**Files:**
- Create: `src/TungDiroot.Addin/Command_DataLink.cs`
- Create: `src/TungDiroot.Addin/UI/DataLinkWindow.xaml`
- Create: `src/TungDiroot.Addin/UI/DataLinkWindow.xaml.cs`
- Create: `src/TungDiroot.Addin/Resources/DataLink16.png`
- Create: `src/TungDiroot.Addin/Resources/DataLink32.png`
- Modify: `src/TungDiroot.Addin/Application.cs`
- Modify: `src/TungDiroot.Addin/TungDiroot.Addin.csproj`
- Modify: `tests/contracts/Test-DataLinkAddinContract.ps1`
- Modify: `tools/Test-ResponsiveWindowLayout.ps1`

**Interfaces:**
- `Command_DataLink.Execute` opens or activates a single window bound to the originating document.
- `DataLinkWindow` queues snapshot/preview/apply callbacks through its own `RevitActionExternalEvent`.

- [ ] **Step 1: Extend contracts so ribbon/UI tests fail**

Assert separate button ID `TUNG_DIROOT.DataLink`, command class, 16/32 icons, modeless singleton behavior, `MinWidth <= 900`, `MinHeight <= 600`, scrollable content, and a document-identity guard before every external action.

- [ ] **Step 2: Run contracts and verify RED**

Run: `powershell -ExecutionPolicy Bypass -File tests/contracts/Test-DataLinkAddinContract.ps1`

Run: `powershell -ExecutionPolicy Bypass -File tools/Test-ResponsiveWindowLayout.ps1`

- [ ] **Step 3: Implement the three-step UI**

Use tabs or a step header for `Chọn dữ liệu`, `Xuất/Nhập Excel`, and `Xem trước & áp dụng`. Keep category/parameter lists virtualized; show visible Vietnamese state text and progress/cancel controls; prevent window close while a Revit callback is executing; never close TableGen when DataLink closes.

- [ ] **Step 4: Integrate the ribbon button and resources**

Add `DataLink` to `Excel Tools` beside TableGen with tooltip describing local Excel export/import and safe preview. Product title becomes `TUNG_DIROOT` without changing assembly identity.

- [ ] **Step 5: Run presentation, contract, and Revit 2024 build checks; commit**

Run: `dotnet test tests/TungDiroot.Presentation.Tests/TungDiroot.Presentation.Tests.csproj --filter FullyQualifiedName~DataLink`

Run: `powershell -ExecutionPolicy Bypass -File tests/contracts/Test-DataLinkAddinContract.ps1`

Run: `powershell -ExecutionPolicy Bypass -File tools/Test-ResponsiveWindowLayout.ps1`

Run: `powershell -ExecutionPolicy Bypass -File tools/Test-AddinContract.ps1`

```powershell
git add src/TungDiroot.Addin tests/contracts tools/Test-ResponsiveWindowLayout.ps1
git commit -m "feat: add DataLink modeless Revit workflow"
```

### Task 6: DataLink release hardening and direct Revit checklist

**Files:**
- Create: `docs/testing/datalink-revit-2024-checklist.md`
- Create: `docs/user-guide-datalink.md`
- Modify: `README.md`
- Modify: `installer/TUNG_DIROOT.iss`
- Modify: release/version files identified by `rg "1\.1\.0"`.

**Interfaces:**
- Produces: release candidate containing DataLink while preserving TableGen and Parameter Loader.

- [ ] **Step 1: Write manual checklist before packaging**

Checklist must cover: instance value round-trip; type value round-trip; type conflict; stale model value; different model workbook; deleted element; read-only parameter; workshared owner; Undo once; cancel; window close; reopen; TableGen and Parameter Loader smoke tests.

- [ ] **Step 2: Run the complete automated suite**

Run: `powershell -ExecutionPolicy Bypass -File tools/Test-All.ps1`

Expected: Core, Presentation, Rendering, responsive UI, Revit 2024 add-in, manifest/schema/contracts, release layout, portable install, install-over, and uninstall tests PASS.

- [ ] **Step 3: Bump the release candidate version and build artifacts**

Use the repository release script after all tests pass; retain the existing 1.1.0 backup and write SHA-256 hashes to the release report.

- [ ] **Step 4: Commit release candidate documentation**

```powershell
git add README.md docs installer src tests tools
git commit -m "chore: prepare DataLink release candidate"
```
