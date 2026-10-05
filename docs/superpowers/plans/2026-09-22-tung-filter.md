# TUNG_Filter Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a responsive Revit 2024 query tool that filters elements by category/family/type/parameter rules, selects or isolates matches, exports results, and visualizes them safely in a dedicated 3D view.

**Architecture:** A pure Core rule engine evaluates immutable element snapshots and persists query profiles through the shared generic profile store introduced by DataLink. Revit adapters collect only requested fields in cancellable batches and perform selection/view actions through `ExternalEvent`. Presentation owns query/result state; WPF is a thin modeless shell.

**Tech Stack:** C#; .NET Standard 2.0 Core/Presentation; .NET Framework 4.8 Revit 2024 add-in; Autodesk Revit API 2024; WPF; DocumentFormat.OpenXml through the shared Excel layer; xUnit.

**Spec:** In-chat Filter design approved by the user on 2026-09-22; the user explicitly waived a separate specification document. This plan starts only after the DataLink plan is complete and green.

## Global Constraints

- Support Autodesk Revit 2024 only.
- Ship as a separate `TUNG_Filter` ribbon button; do not merge its window with DataLink.
- Reuse `JsonProfileStore<TProfile>`, `RevitActionExternalEvent`, batch state conventions, diagnostics, and Excel infrastructure.
- Query scopes are Whole Model, Active View, and Current Selection.
- V1 supports category/family/type plus parameter rules with Equals, NotEquals, Contains, NotContains, GreaterThan, LessThan, IsEmpty, and IsNotEmpty joined by all-AND or all-OR.
- Revit API reads/writes occur only inside `ExternalEvent`; pure evaluation and Excel output use detached DTOs.
- Do not save or synchronize the model and do not modify linked documents.
- Visualization writes only to a tool-owned 3D view named from a sanitized profile name; never overwrite graphics in the user's working view.
- Room/Space containment, legends, persistent Revit rule filters, and parameter writing are out of scope for v1.

## Review Focus

- A missing parameter is distinct from an existing empty parameter and must obey explicit rule semantics.
- Numeric comparisons must compare normalized internal numbers, not localized display strings.
- OR queries with mixed string/numeric/missing values must be deterministic and never throw.
- Changing/closing the source document while a batched query is running must cancel safely and discard stale results.
- Visualization reset/update must touch only the dedicated `TUNG_FILTER_*` view and elements managed by the current profile.

---

### Task 1: Pure Filter rule model and evaluator

**Files:**
- Create: `src/TungDiroot.Core/Filter/FilterModels.cs`
- Create: `src/TungDiroot.Core/Filter/FilterRuleEvaluator.cs`
- Test: `tests/TungDiroot.Core.Tests/Filter/FilterRuleEvaluatorTests.cs`

**Interfaces:**
- Produces: `FilterScope`, `FilterJoin`, `FilterOperator`, `FilterRule`, `FilterElementSnapshot`, `FilterValue`, `FilterQuery`, `FilterMatch`, and `FilterRuleEvaluator.Evaluate(...)`.
- Consumes: no Autodesk types.

- [ ] **Step 1: Write failing evaluator tests**

```csharp
[Fact]
public void Missing_value_is_not_the_same_as_empty_value()
{
    var rule = new FilterRule("Comments", FilterOperator.IsEmpty, null, FilterValueKind.String);
    Assert.False(FilterRuleEvaluator.Matches(FilterFixtures.Without("Comments"), rule));
    Assert.True(FilterRuleEvaluator.Matches(FilterFixtures.With("Comments", ""), rule));
}

[Fact]
public void Numeric_comparison_uses_internal_value()
{
    var rule = new FilterRule("Length", FilterOperator.GreaterThan, "3.5", FilterValueKind.Number);
    Assert.True(FilterRuleEvaluator.Matches(FilterFixtures.Number("Length", 4.0), rule));
}
```

- [ ] **Step 2: Run focused tests and verify RED**

Run: `dotnet test tests/TungDiroot.Core.Tests/TungDiroot.Core.Tests.csproj --filter FullyQualifiedName~FilterRuleEvaluatorTests`

Expected: FAIL because Filter types do not exist.

- [ ] **Step 3: Implement explicit rule semantics**

```csharp
public enum FilterOperator { Equals, NotEquals, Contains, NotContains, GreaterThan, LessThan, IsEmpty, IsNotEmpty }
public enum FilterJoin { And, Or }
public enum FilterValueKind { String, Number, Integer, Boolean }
public sealed record FilterValue(bool Exists, FilterValueKind Kind, string Display, string Internal);
public sealed record FilterRule(string ParameterStableId, FilterOperator Operator, string? Operand, FilterValueKind Kind);
public sealed record FilterElementSnapshot(string UniqueId, int ElementId, string Category, string Family, string TypeName, string? Level, IReadOnlyDictionary<string, FilterValue> Values);
public sealed record FilterQuery(FilterScope Scope, IReadOnlyList<int> CategoryIds, IReadOnlyList<string> FamilyNames, IReadOnlyList<string> TypeNames, IReadOnlyList<FilterRule> Rules, FilterJoin Join);
```

`IsEmpty` and `IsNotEmpty` require `Exists == true`; string operations use `OrdinalIgnoreCase`; numeric/integer operations parse invariant `Internal`; unsupported operator/kind combinations return a validation error before evaluation.

- [ ] **Step 4: Add AND/OR and malformed-input tests; run GREEN**

Cover zero rules, mixed kinds, missing values, null display text, invalid operands, and 10,000 deterministic snapshots.

Run: `dotnet test tests/TungDiroot.Core.Tests/TungDiroot.Core.Tests.csproj --filter FullyQualifiedName~Filter`

- [ ] **Step 5: Commit the evaluator**

```powershell
git add src/TungDiroot.Core/Filter tests/TungDiroot.Core.Tests/Filter
git commit -m "feat: add TUNG Filter rule engine"
```

### Task 2: Versioned Filter profiles using the shared profile core

**Files:**
- Create: `src/TungDiroot.Core/Filter/FilterProfile.cs`
- Create: `src/TungDiroot.Core/Filter/FilterProfileRepository.cs`
- Test: `tests/TungDiroot.Core.Tests/Filter/FilterProfileRepositoryTests.cs`

**Interfaces:**
- Consumes: `JsonProfileStore<TProfile>` delivered by DataLink Task 4.
- Produces: profiles under `%LocalAppData%\TUNG_DIROOT\profiles\filter` containing query definition and visualization palette, never cached ElementIds.

- [ ] **Step 1: Write failing profile tests**

Test Unicode profile names, invalid filename characters, duplicate names, corrupt JSON isolation, unsupported future schema, and proof that no element ID list is serialized.

- [ ] **Step 2: Run tests and verify RED**

Run: `dotnet test tests/TungDiroot.Core.Tests/TungDiroot.Core.Tests.csproj --filter FullyQualifiedName~FilterProfile`

- [ ] **Step 3: Implement profile schema and repository**

```csharp
public sealed record FilterProfile(int SchemaVersion, string Name, FilterQuery Query, string GroupParameterStableId, IReadOnlyList<string> PaletteHex);

public sealed class FilterProfileRepository
{
    public IReadOnlyList<ProfileLoadResult<FilterProfile>> LoadAll();
    public void Save(FilterProfile profile);
    public void Delete(string name);
}
```

Validate schema version `1`, non-empty names, valid six-digit RGB palette values, and query validity before save.

- [ ] **Step 4: Run Filter Core tests and commit**

Expected: PASS.

```powershell
git add src/TungDiroot.Core/Filter tests/TungDiroot.Core.Tests/Filter
git commit -m "feat: persist TUNG Filter profiles"
```

### Task 3: Batched Revit snapshot collector

**Files:**
- Create: `src/TungDiroot.Addin/Filter/RevitFilterCatalogReader.cs`
- Create: `src/TungDiroot.Addin/Filter/RevitFilterSnapshotBatch.cs`
- Create: `src/TungDiroot.Addin/Filter/RevitFilterSnapshotProjector.cs`
- Test: `tests/contracts/Test-FilterAddinContract.ps1`
- Modify: `tools/Test-All.ps1`

**Interfaces:**
- Produces category/family/type/parameter catalogs and batches of detached `FilterElementSnapshot` objects.
- Consumes a validated `FilterQuery`, expected document identity, cancellation token/state, and batch size `500`.

- [ ] **Step 1: Add a failing Revit contract**

Assert that collectors use active document identity checks, respect Whole Model/Active View/Selection element sets, never retain `Element`/`Parameter` beyond the callback, read only requested parameter IDs, cap each batch at 500, and have explicit cancel/completed states.

- [ ] **Step 2: Run contract and verify RED**

Run: `powershell -ExecutionPolicy Bypass -File tests/contracts/Test-FilterAddinContract.ps1`

- [ ] **Step 3: Implement catalogs and batched snapshot projection**

Catalog reader returns lightweight descriptors. Batch collector stores only remaining `ElementId` integers between external events; each callback re-resolves up to 500 elements, projects requested instance/type values to Core DTOs, reports progress, then re-raises only when more work remains and the expected document is still active.

- [ ] **Step 4: Add cancellation/document-switch contract cases and run GREEN**

Run: `powershell -ExecutionPolicy Bypass -File tests/contracts/Test-FilterAddinContract.ps1`

- [ ] **Step 5: Commit snapshot collection**

```powershell
git add src/TungDiroot.Addin/Filter tests/contracts/Test-FilterAddinContract.ps1 tools/Test-All.ps1
git commit -m "feat: collect Revit filter snapshots in batches"
```

### Task 4: Safe result actions and dedicated visualization view

**Files:**
- Create: `src/TungDiroot.Addin/Filter/RevitFilterActions.cs`
- Create: `src/TungDiroot.Addin/Filter/RevitFilterVisualization.cs`
- Create: `src/TungDiroot.Core/Filter/FilterResultWorkbookWriter.cs`
- Test: `tests/TungDiroot.Core.Tests/Filter/FilterResultWorkbookWriterTests.cs`
- Modify: `tests/contracts/Test-FilterAddinContract.ps1`

**Interfaces:**
- Produces: select, show/zoom, temporary isolate/hide, Revit `SelectionFilterElement`, Excel result export, and dedicated 3D visualization update.
- Consumes: only element UniqueIds from the active expected document and a sanitized profile name.

- [ ] **Step 1: Write failing Excel and Revit action contracts**

Verify exported columns and values; require visualization view names beginning `TUNG_FILTER_`; reject templates and linked documents; require transactions for saved selection and view changes; prohibit setting per-element overrides on the source view.

- [ ] **Step 2: Run tests and verify RED**

Run: `dotnet test tests/TungDiroot.Core.Tests/TungDiroot.Core.Tests.csproj --filter FullyQualifiedName~FilterResultWorkbookWriterTests`

Run: `powershell -ExecutionPolicy Bypass -File tests/contracts/Test-FilterAddinContract.ps1`

- [ ] **Step 3: Implement selection/isolation/export actions**

Selection and zoom use `UIDocument`; temporary hide/isolate targets the active view and performs no persistent graphics edits. Saved sets create/update a tool-owned `SelectionFilterElement` inside one transaction. Excel export writes a flat, filterable table containing identity and visible result columns.

- [ ] **Step 4: Implement visualization view**

Create or reuse a non-template isometric 3D view named `TUNG_FILTER_<sanitized-profile>`, isolate matching elements, group by the configured parameter display value, apply deterministic palette colors only in that view, and clear only overrides created there before each refresh.

- [ ] **Step 5: Run tests/contracts and commit**

Expected: PASS.

```powershell
git add src/TungDiroot.Core/Filter src/TungDiroot.Addin/Filter tests/TungDiroot.Core.Tests/Filter tests/contracts/Test-FilterAddinContract.ps1
git commit -m "feat: add TUNG Filter result actions"
```

### Task 5: Filter presentation model and modeless window

**Files:**
- Create: `src/TungDiroot.Presentation/Filter/FilterViewModel.cs`
- Create: `src/TungDiroot.Presentation/Filter/FilterRows.cs`
- Create: `tests/TungDiroot.Presentation.Tests/Filter/FilterViewModelTests.cs`
- Create: `src/TungDiroot.Addin/UI/FilterWindow.xaml`
- Create: `src/TungDiroot.Addin/UI/FilterWindow.xaml.cs`
- Create: `src/TungDiroot.Addin/Command_Filter.cs`
- Create: `src/TungDiroot.Addin/Resources/Filter16.png`
- Create: `src/TungDiroot.Addin/Resources/Filter32.png`
- Modify: `src/TungDiroot.Addin/Application.cs`
- Modify: `src/TungDiroot.Addin/TungDiroot.Addin.csproj`
- Modify: `tools/Test-ResponsiveWindowLayout.ps1`

**Interfaces:**
- `FilterViewModel` builds validated queries, owns cancellable progress and immutable result rows, and gates actions by current document/state.
- `FilterWindow` queues Revit work through `RevitActionExternalEvent` and activates a single instance per source document.

- [ ] **Step 1: Write failing presentation/contract tests**

Cover adding/removing rules, operator availability by value kind, AND/OR result counts, cancel state, Select All, zero results, profile load/save, stale document guard, and closing while a batch is running. Extend responsive UI checks for a scrollable/virtualized results grid.

- [ ] **Step 2: Run tests and verify RED**

Run: `dotnet test tests/TungDiroot.Presentation.Tests/TungDiroot.Presentation.Tests.csproj --filter FullyQualifiedName~Filter`

Run: `powershell -ExecutionPolicy Bypass -File tools/Test-ResponsiveWindowLayout.ps1`

- [ ] **Step 3: Implement view model and WPF workflow**

Use four visible sections: `Phạm vi`, `Điều kiện`, `Kết quả`, and `Hành động`. Virtualize category/result lists; allow keyboard selection; expose progress and `Hủy`; keep results readable at 1366x768 and Windows display scaling; show Vietnamese validation inline instead of modal dialogs for routine input errors.

- [ ] **Step 4: Add separate ribbon button**

Add `TUNG_DIROOT.Filter` to a `Model Tools` panel with its own icon/command and tooltip. Do not change DataLink/TableGen/Parameter Loader command identity.

- [ ] **Step 5: Run tests/contracts/build and commit**

Run: `dotnet test tests/TungDiroot.Presentation.Tests/TungDiroot.Presentation.Tests.csproj --filter FullyQualifiedName~Filter`

Run: `powershell -ExecutionPolicy Bypass -File tests/contracts/Test-FilterAddinContract.ps1`

Run: `powershell -ExecutionPolicy Bypass -File tools/Test-ResponsiveWindowLayout.ps1`

Run: `powershell -ExecutionPolicy Bypass -File tools/Test-AddinContract.ps1`

```powershell
git add src tests tools
git commit -m "feat: add TUNG Filter modeless workflow"
```

### Task 6: Filter documentation, full regression, and combined release candidate

**Files:**
- Create: `docs/testing/filter-revit-2024-checklist.md`
- Create: `docs/user-guide-filter.md`
- Modify: `README.md`
- Modify: `installer/TUNG_DIROOT.iss`
- Modify: release/version files identified from the completed DataLink candidate.

**Interfaces:**
- Produces one Revit 2024 release candidate containing TableGen, Parameter Loader, DataLink, and Filter as independent commands.

- [ ] **Step 1: Write the direct Revit checklist**

Cover Whole Model/Active View/Selection; string/numeric/empty rules; AND/OR; missing parameter; 10,000-element cancel; selection; temporary isolate/reset; saved selection; result Excel; dedicated 3D colors; profile reopen; document switch; Undo; and smoke tests for all existing commands.

- [ ] **Step 2: Run the complete automated suite**

Run: `powershell -ExecutionPolicy Bypass -File tools/Test-All.ps1`

Expected: every Core, Presentation, Rendering, add-in contract, schema, responsive layout, Revit 2024 build, release layout, portable install, install-over, and uninstall check PASS.

- [ ] **Step 3: Build release artifacts and record exact hashes**

Use the repository release script only after a clean test run; retain the prior 1.1.0 and DataLink-only artifacts; write SHA-256 hashes and manual-checklist status in a release report.

- [ ] **Step 4: Commit the combined release candidate**

```powershell
git add README.md docs installer src tests tools
git commit -m "chore: prepare DataLink and Filter release candidate"
```
