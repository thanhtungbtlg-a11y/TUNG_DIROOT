# TUNG_DIROOT Link Manager and Synchronization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Biến luồng import thủ công thành hệ thống liên kết bền vững có manager UI, Refresh/Recreate, Data-only update, batch pause/stop, Auto Sync, Relink và chẩn đoán an toàn.

**Architecture:** Pure link/update state nằm trong Core và được unit test. Revit adapter lưu registry trong DataStorage và marker trên view bằng Extensible Storage schema versioned. Modeless WPF manager phát ExternalEvent theo từng view để Revit transaction luôn ngắn và có thể tạm dừng/dừng giữa các view.

**Tech Stack:** Core/Rendering/Addin từ Plan 1–2, Autodesk Revit Extensible Storage, ExternalEvent, Idling, WPF MVVM, xUnit.

## Global Constraints

- Metadata link nằm trong model; setting giao diện nằm theo user.
- Mỗi link có stable GUID và target View UniqueId.
- Chỉ sửa/xóa element mang đúng schema và Link ID.
- Auto Sync không save model và không Synchronize with Central.
- Một view lỗi phải rollback riêng, không hủy batch.
- Pause/stop chỉ có hiệu lực giữa các view, không giữa transaction.
- Log không ghi nội dung cell Excel.

---

### Task 1: Link record, schema migration và Revit registry

**Files:**
- Create: `src/TungDiroot.Core/Links/LinkRecord.cs`
- Create: `src/TungDiroot.Core/Links/LinkRegistryModel.cs`
- Create: `src/TungDiroot.Core/Links/LinkSchemaMigrator.cs`
- Test: `tests/TungDiroot.Core.Tests/Links/LinkSchemaMigratorTests.cs`
- Create: `src/TungDiroot.Addin/Links/ILinkRegistry.cs`
- Create: `src/TungDiroot.Addin/Links/RevitLinkSchema.cs`
- Create: `src/TungDiroot.Addin/Links/RevitLinkRegistry.cs`
- Create: `src/TungDiroot.Addin/Links/ViewLinkMarker.cs`
- Modify: `Directory.Packages.props`
- Modify: `src/TungDiroot.Addin/TungDiroot.Addin.csproj`

**Interfaces:**
- Produces: `LinkRecord`, `ILinkRegistry.Load/Save/RecoverFromViews`, schema GUID constants.

- [ ] **Step 1: Viết migration tests**

```csharp
[Fact]
public void V1_record_round_trips_without_data_loss()
{
    var source = LinkSamples.V1DraftingTable();
    var migrated = LinkSchemaMigrator.ToCurrent(source);
    Assert.Equal(source.LinkId, migrated.LinkId);
    Assert.Equal(1, migrated.SchemaVersion);
    Assert.Equal("PCCC", migrated.WorksheetName);
}

[Fact]
public void Unsupported_future_schema_is_read_only()
{
    var result = LinkSchemaMigrator.TryToCurrent(LinkSamples.WithSchemaVersion(99));
    Assert.False(result.CanWrite);
    Assert.Equal("SCHEMA_NEWER_THAN_ADDIN", result.WarningCode);
}
```

- [ ] **Step 2: Chạy test để xác nhận fail**

Run: `dotnet test tests/TungDiroot.Core.Tests -c Release --filter LinkSchemaMigratorTests`  
Expected: FAIL compile.

- [ ] **Step 3: Cài record và Extensible Storage adapter**

```csharp
public sealed record LinkRecord(
    int SchemaVersion,
    Guid LinkId,
    string TargetViewUniqueId,
    StoredPath SourcePath,
    string WorksheetName,
    string RegionReference,
    ImportMode ImportMode,
    TargetViewKind TargetViewKind,
    UpdateBehavior UpdateBehavior,
    bool AutoSync,
    SourceFingerprint LastFingerprint,
    DateTimeOffset? LastUpdatedUtc,
    string? LastResultCode);
```

Registry dùng một DataStorage đặt tên `TUNG_DIROOT.LinkRegistry`, schema GUID cố định `B2C9E0DE-8CE5-4C3A-BE22-8D4938784A44`; view marker dùng GUID `30164CBF-C57D-43BA-BD49-ED0D79CE1B24`. Registry serialize danh sách record bằng `System.Text.Json 8.0.5` vào string field. Marker lưu LinkId, target UniqueId, source path, worksheet, region, mode, update behavior, Auto Sync và fingerprint để có đủ dữ liệu phục hồi registry.

- [ ] **Step 4: Chạy test/build**

Run: `powershell -ExecutionPolicy Bypass -File tools/Test-Core.ps1`  
Run: `powershell -ExecutionPolicy Bypass -File build/Build-Addin.ps1 -RevitVersion 2024 -Configuration Release`  
Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/TungDiroot.Core/Links tests/TungDiroot.Core.Tests/Links src/TungDiroot.Addin/Links
git commit -m "feat: persist versioned table links in Revit"
```

### Task 2: Update planner, data-only diff và safe element scope

**Files:**
- Create: `src/TungDiroot.Core/Updates/UpdatePlan.cs`
- Create: `src/TungDiroot.Core/Updates/UpdatePlanner.cs`
- Create: `src/TungDiroot.Core/Updates/CellValueDiff.cs`
- Test: `tests/TungDiroot.Core.Tests/Updates/UpdatePlannerTests.cs`
- Create: `src/TungDiroot.Addin/Updates/LinkedElementCollector.cs`
- Create: `src/TungDiroot.Addin/Updates/RevitLinkUpdater.cs`

**Interfaces:**
- Consumes: `LinkRecord`, `WorkbookRangeModel`, `StructureCompatibility`.
- Produces: `UpdatePlanner.Create`, `RevitLinkUpdater.Update(Document, LinkRecord, UpdatePlan)`.

- [ ] **Step 1: Viết tests cho no-change/data-only/recreate**

```csharp
[Theory]
[InlineData(false, true, UpdateAction.None)]
[InlineData(true, true, UpdateAction.DataOnly)]
[InlineData(true, false, UpdateAction.Recreate)]
public void Chooses_safe_update_action(bool sourceChanged, bool structureCompatible, UpdateAction expected)
{
    var input = UpdatePlanSamples.Create(sourceChanged, structureCompatible, UpdateBehavior.DataOnly);
    Assert.Equal(expected, UpdatePlanner.Create(input).Action);
}
```

- [ ] **Step 2: Chạy test để xác nhận fail**

Run: `dotnet test tests/TungDiroot.Core.Tests -c Release --filter UpdatePlannerTests`  
Expected: FAIL compile.

- [ ] **Step 3: Cài planner và updater**

```csharp
public static class UpdatePlanner
{
    public static UpdatePlan Create(UpdatePlanInput input)
    {
        if (!input.SourceChanged) return UpdatePlan.None;
        if (input.RequestedBehavior == UpdateBehavior.DataOnly && input.StructureCompatible)
            return UpdatePlan.DataOnly(CellValueDiff.Create(input.Before, input.After));
        return UpdatePlan.Recreate(input.StructureReasonCode);
    }
}
```

Planner tạo diff theo normalized cell key. Data-only chỉ chứa `CellValueChange`; nếu structure khác, action `Recreate` kèm reason. `LinkedElementCollector` lọc element theo schema GUID và exact LinkId; updater không dùng view-wide delete. Drafting/Legend data-only thay formatted text trong TextNote tương ứng; Schedule data-only gọi `SetCellText`; image luôn replace ImageType/Instance thuộc link.

- [ ] **Step 4: Chạy test/build và static delete-scope audit**

Run: `powershell -ExecutionPolicy Bypass -File tools/Test-Core.ps1`  
Run: `rg -n "Delete\(" src/TungDiroot.Addin/Updates src/TungDiroot.Addin/Revit`  
Expected: mọi `Document.Delete` nhận ID từ `LinkedElementCollector` hoặc exact linked ImageType; build PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/TungDiroot.Core/Updates tests/TungDiroot.Core.Tests/Updates src/TungDiroot.Addin/Updates
git commit -m "feat: update linked views without touching user elements"
```

### Task 3: Manager window và view-model trạng thái

**Files:**
- Create: `src/TungDiroot.Addin/UI/TableGenWindow.xaml`
- Create: `src/TungDiroot.Addin/UI/TableGenWindow.xaml.cs`
- Create: `src/TungDiroot.Presentation/Links/ITableGenLinkSource.cs`
- Create: `src/TungDiroot.Presentation/UI/TableGenViewModel.cs`
- Create: `src/TungDiroot.Presentation/UI/LinkRowViewModel.cs`
- Create: `src/TungDiroot.Presentation/UI/LinkFilter.cs`
- Test: `tests/TungDiroot.Presentation.Tests/UI/TableGenViewModelTests.cs`
- Modify: `src/TungDiroot.Addin/Command_TableGen.cs`

**Interfaces:**
- Consumes: `ITableGenLinkSource`, `SourceChangeDetector`.
- Produces: commands `AddExcel`, `CheckSources`, `UpdateSelected`, `BatchAction`, `Search`.

- [ ] **Step 1: Viết view-model tests với fake registry**

```csharp
[Fact]
public void Filter_and_counts_follow_link_status()
{
    var vm = new TableGenViewModel(new FakeTableGenLinkSource(LinkSamples.ReadyChangedMissing()));
    Assert.Equal(1, vm.ReadyCount);
    Assert.Equal(1, vm.ChangedCount);
    Assert.Equal(1, vm.MissingCount);
    vm.StatusFilter = LinkStatusFilter.Changed;
    Assert.Single(vm.VisibleLinks);
}
```

- [ ] **Step 2: Chạy test để xác nhận fail**

Run: `dotnet test tests/TungDiroot.Presentation.Tests -c Release --filter TableGenViewModelTests`  
Expected: FAIL compile.

- [ ] **Step 3: Cài UI theo mockup đã duyệt**

```csharp
public sealed class TableGenViewModel : BindableBase
{
    private readonly ITableGenLinkSource _source;
    public ObservableCollection<LinkRowViewModel> VisibleLinks { get; } = new();
    public TableGenViewModel(ITableGenLinkSource source) => _source = source;
    public void RefreshRows() => LinkFilter.Apply(_source.Load(), SearchText, StatusFilter, VisibleLinks);
}
```

Window có toolbar, filter row, DataGrid với các cột trạng thái/nguồn/view/chế độ/view kind/DPI/Auto Sync, detail pane và validation pane. Search dùng ordinal-ignore-case trên file, worksheet, region và view. Commands disabled khi export/update đang chạy. Window là modeless và giữ một instance theo Revit application session.

- [ ] **Step 4: Chạy UI logic tests và build**

Run: `dotnet test tests/TungDiroot.Presentation.Tests -c Release`  
Run: `powershell -ExecutionPolicy Bypass -File build/Build-Addin.ps1 -RevitVersion 2024 -Configuration Release`  
Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/TungDiroot.Presentation src/TungDiroot.Addin/UI src/TungDiroot.Addin/Command_TableGen.cs tests/TungDiroot.Presentation.Tests/UI
git commit -m "feat: add TableGen link manager window"
```

### Task 4: Batch state machine, ExternalEvent và pause/stop

**Files:**
- Create: `src/TungDiroot.Core/Batch/BatchStateMachine.cs`
- Create: `src/TungDiroot.Core/Batch/BatchProgress.cs`
- Test: `tests/TungDiroot.Core.Tests/Batch/BatchStateMachineTests.cs`
- Create: `src/TungDiroot.Addin/Batch/TableGenExternalEventHandler.cs`
- Create: `src/TungDiroot.Addin/Batch/RevitBatchCoordinator.cs`
- Create: `src/TungDiroot.Addin/Batch/BatchResultReport.cs`
- Modify: `src/TungDiroot.Presentation/UI/TableGenViewModel.cs`
- Modify: `src/TungDiroot.Addin/UI/TableGenWindow.xaml`

**Interfaces:**
- Produces: `BatchStateMachine.Start/Pause/Resume/RequestStop/BeginNext/CompleteCurrent`, `RevitBatchCoordinator.Enqueue`.

- [ ] **Step 1: Viết state-machine tests**

```csharp
[Fact]
public void Stop_requested_during_item_stops_before_next_item()
{
    var state = BatchStateMachine.Start(total: 3);
    state = state.BeginNext().RequestStop().CompleteCurrent(BatchItemOutcome.Succeeded);
    Assert.Equal(BatchRunState.Stopped, state.RunState);
    Assert.Equal(1, state.Completed);
}

[Fact]
public void Pause_does_not_interrupt_current_item()
{
    var state = BatchStateMachine.Start(2).BeginNext().Pause();
    Assert.Equal(BatchRunState.RunningCurrentThenPause, state.RunState);
}
```

- [ ] **Step 2: Chạy test để xác nhận fail**

Run: `dotnet test tests/TungDiroot.Core.Tests -c Release --filter BatchStateMachineTests`  
Expected: FAIL compile.

- [ ] **Step 3: Cài state machine và ExternalEvent coordinator**

```csharp
public void Execute(UIApplication app)
{
    if (!_state.TryBeginNext(out var item)) return;
    var outcome = _processor.ProcessOne(app, item);
    _state.CompleteCurrent(outcome);
    ProgressChanged?.Invoke(_state.Progress);
    if (_state.ShouldRaiseNext) _externalEvent.Raise();
}
```

Coordinator giữ queue immutable; mỗi `Execute(UIApplication)` chỉ xử lý tối đa một link trong một transaction, publish `BatchProgress`, rồi raise ExternalEvent tiếp theo nếu state cho phép. Pause/stop buttons chỉ thay state thread-safe; không gọi API Revit từ WPF event handler.

- [ ] **Step 4: Chạy test và batch contract audit**

Run: `powershell -ExecutionPolicy Bypass -File tools/Test-Core.ps1`  
Run: `powershell -ExecutionPolicy Bypass -File build/Build-Addin.ps1 -RevitVersion 2024 -Configuration Release`  
Expected: PASS; manual 3-link batch dừng sau link đang chạy và giữ hai kết quả trước đó.

- [ ] **Step 5: Commit**

```powershell
git add src/TungDiroot.Core/Batch tests/TungDiroot.Core.Tests/Batch src/TungDiroot.Addin/Batch src/TungDiroot.Addin/UI src/TungDiroot.Presentation/UI/TableGenViewModel.cs
git commit -m "feat: add pausable per-view update batches"
```

### Task 5: Auto Sync sau DocumentOpened

**Files:**
- Create: `src/TungDiroot.Core/Sync/AutoSyncPlanner.cs`
- Test: `tests/TungDiroot.Core.Tests/Sync/AutoSyncPlannerTests.cs`
- Create: `src/TungDiroot.Addin/Sync/DocumentAutoSyncService.cs`
- Create: `src/TungDiroot.Addin/Sync/PendingDocumentQueue.cs`
- Modify: `src/TungDiroot.Addin/Application.cs`

**Interfaces:**
- Produces: `AutoSyncPlanner.SelectChanged`, `DocumentAutoSyncService.OnDocumentOpened/OnIdling`.

- [ ] **Step 1: Viết planner tests**

```csharp
[Fact]
public void Selects_only_auto_sync_changed_editable_links()
{
    var result = AutoSyncPlanner.SelectChanged(AutoSyncSamples.MixedLinks());
    Assert.Equal(new[] { AutoSyncSamples.ChangedEditableLinkId }, result.SelectedLinkIds);
    Assert.Contains(result.Skipped, x => x.ReasonCode == "VIEW_NOT_EDITABLE");
}
```

- [ ] **Step 2: Chạy test để xác nhận fail**

Run: `dotnet test tests/TungDiroot.Core.Tests -c Release --filter AutoSyncPlannerTests`  
Expected: FAIL compile.

- [ ] **Step 3: Cài event handoff an toàn**

```csharp
public void OnDocumentOpened(object sender, DocumentOpenedEventArgs e) =>
    _pending.Enqueue(e.Document.UniqueId);

public void OnIdling(object sender, IdlingEventArgs e)
{
    if (!_pending.TryDequeueReady(_uiApplication.ActiveUIDocument?.Document, out var document)) return;
    _batch.Enqueue(AutoSyncPlanner.SelectChanged(_registry.Load(document)));
}
```

`OnDocumentOpened` chỉ enqueue Document UniqueId/path và không start transaction. `OnIdling` đợi document active, load registry, kiểm tra metadata/fingerprint ngoài transaction, rồi enqueue changed links vào `RevitBatchCoordinator`. Service đánh dấu một document đang chạy để tránh duplicate Idling requests; không gọi `Save`, `SaveAs` hoặc sync command.

- [ ] **Step 4: Chạy tests/build và source audit**

Run: `powershell -ExecutionPolicy Bypass -File tools/Test-Core.ps1`  
Run: `rg -n "SaveAs|SynchronizeWithCentral|PostCommand" src/TungDiroot.Addin/Sync`  
Expected: không có lệnh save/sync; build PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/TungDiroot.Core/Sync tests/TungDiroot.Core.Tests/Sync src/TungDiroot.Addin/Sync src/TungDiroot.Addin/Application.cs
git commit -m "feat: auto-sync changed Excel links after model open"
```

### Task 6: Relink, settings và privacy-safe logging

**Files:**
- Create: `src/TungDiroot.Core/Links/RelinkMatcher.cs`
- Test: `tests/TungDiroot.Core.Tests/Links/RelinkMatcherTests.cs`
- Create: `src/TungDiroot.Addin/Settings/UserSettings.cs`
- Create: `src/TungDiroot.Addin/Settings/UserSettingsStore.cs`
- Create: `src/TungDiroot.Addin/Diagnostics/ITungDirootLogger.cs`
- Create: `src/TungDiroot.Addin/Diagnostics/RollingFileLogger.cs`
- Create: `src/TungDiroot.Core/Diagnostics/DiagnosticEvent.cs`
- Test: `tests/TungDiroot.Core.Tests/Diagnostics/DiagnosticEventTests.cs`
- Create: `src/TungDiroot.Addin/Links/RelinkService.cs`
- Modify: `src/TungDiroot.Presentation/UI/TableGenViewModel.cs`

**Interfaces:**
- Produces: `RelinkMatcher.Match`, settings JSON at `%LocalAppData%\TUNG_DIROOT\settings.json`, logs at `%LocalAppData%\TUNG_DIROOT\Logs`.

- [ ] **Step 1: Viết relink và redaction tests**

```csharp
[Fact]
public void Folder_relink_matches_filename_then_validates_sheet()
{
    var result = RelinkMatcher.Match(LinkSamples.MissingA(), CandidateSamples.SameNameWithSheet());
    Assert.Equal(RelinkMatchKind.ExactFileAndWorksheet, result.Kind);
}

[Fact]
public void Log_event_never_contains_cell_values()
{
    var line = DiagnosticEvent.ForLinkFailure(Guid.Parse("11111111-1111-1111-1111-111111111111"), @"X:\A.xlsx", "FILE_LOCKED").ToJson();
    Assert.DoesNotContain("CellValues", line, StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 2: Chạy test để xác nhận fail**

Run: `dotnet test tests/TungDiroot.Core.Tests -c Release --filter RelinkMatcherTests`  
Expected: FAIL compile.

- [ ] **Step 3: Cài services**

```csharp
public static RelinkResult Match(LinkRecord link, IReadOnlyList<CandidateWorkbook> candidates)
{
    var sameName = candidates.Where(x => StringComparer.OrdinalIgnoreCase.Equals(x.FileName, link.SourcePath.FileName)).ToList();
    var valid = sameName.Where(x => x.Contains(link.WorksheetName, link.RegionReference)).ToList();
    return valid.Count switch { 0 => RelinkResult.None, 1 => RelinkResult.Exact(valid[0]), _ => RelinkResult.Ambiguous(valid) };
}
```

Relink folder group candidates bằng filename ordinal-ignore-case, sau đó mở catalog để xác nhận worksheet/region; nhiều candidate bằng nhau trả `Ambiguous` và không tự chọn. Settings write atomic qua temp + replace, invalid JSON đổi tên `.corrupt-<timestamp>`. Logger rotate 5 file × 2 MB, chỉ ghi timestamp, add-in version, Revit version, LinkId, source path, step, result code và exception type/message.

- [ ] **Step 4: Chạy tests và build**

Run: `powershell -ExecutionPolicy Bypass -File tools/Test-Core.ps1`  
Run: `powershell -ExecutionPolicy Bypass -File build/Build-Addin.ps1 -RevitVersion 2024 -Configuration Release`  
Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/TungDiroot.Core/Links src/TungDiroot.Core/Diagnostics tests/TungDiroot.Core.Tests/Links tests/TungDiroot.Core.Tests/Diagnostics src/TungDiroot.Addin/Settings src/TungDiroot.Addin/Diagnostics src/TungDiroot.Addin/Links src/TungDiroot.Presentation/UI/TableGenViewModel.cs
git commit -m "feat: relink sources and persist safe user settings"
```

### Task 7: Failure isolation và plan 3 integration verification

**Files:**
- Create: `src/TungDiroot.Core/Import/ImportExceptionClassifier.cs`
- Create: `src/TungDiroot.Addin/Revit/WorksharingEditabilityChecker.cs`
- Test: `tests/TungDiroot.Core.Tests/Import/ImportExceptionClassifierTests.cs`
- Create: `docs/testing/plan3-sync-smoke.md`
- Create: `tools/Test-Plan3.ps1`

**Interfaces:**
- Produces: stable result codes `FILE_MISSING`, `FILE_LOCKED`, `FORMULA_CACHE_MISSING`, `FONT_FALLBACK`, `VIEW_NOT_EDITABLE`, `VIEW_DELETED`, `UNKNOWN_ERROR`.

- [ ] **Step 1: Viết classifier tests**

```csharp
[Theory]
[InlineData(typeof(FileNotFoundException), "FILE_MISSING")]
[InlineData(typeof(UnauthorizedAccessException), "FILE_LOCKED")]
[InlineData(typeof(IOException), "FILE_LOCKED")]
public void Classifies_expected_exception(Type exceptionType, string code)
{
    var exception = (Exception)Activator.CreateInstance(exceptionType, "test")!;
    Assert.Equal(code, ImportExceptionClassifier.Classify(exception).Code);
}
```

- [ ] **Step 2: Chạy test để xác nhận fail**

Run: `dotnet test tests/TungDiroot.Core.Tests -c Release --filter ImportExceptionClassifierTests`  
Expected: FAIL compile.

- [ ] **Step 3: Cài classifier/editability check và exact smoke checklist**

```csharp
public static ImportFailure Classify(Exception exception) => exception switch
{
    FileNotFoundException => new("FILE_MISSING"),
    UnauthorizedAccessException => new("FILE_LOCKED"),
    IOException => new("FILE_LOCKED"),
    _ => new("UNKNOWN_ERROR")
};
```

Checklist tạo 5 links: unchanged, changed, missing, locked và view owned by another user. Chạy Refresh rồi Auto Sync; expected lần lượt `Không đổi`, `Thành công`, `Mất nguồn`, `Bỏ qua FILE_LOCKED`, `Bỏ qua VIEW_NOT_EDITABLE`. Xác nhận user-created TextNote trong cùng view còn nguyên sau Recreate; đóng/mở model và kiểm tra registry recover.

- [ ] **Step 4: Chạy plan verification**

Run: `powershell -ExecutionPolicy Bypass -File tools/Test-Plan3.ps1 -RevitVersion 2024`  
Expected: core/rendering/addin-logic tests PASS, 2024 build PASS, contract PASS. Thực hiện `docs/testing/plan3-sync-smoke.md` trong Revit 2024 và ghi kết quả PASS/FAIL từng dòng.

- [ ] **Step 5: Commit và checkpoint plan 3**

```powershell
git add src/TungDiroot.Core/Import tests/TungDiroot.Core.Tests/Import src/TungDiroot.Addin/Revit docs/testing/plan3-sync-smoke.md tools/Test-Plan3.ps1
git commit -m "test: verify isolated link synchronization failures"
git status --short
```

Expected: working tree sạch; link manager, manual sync, Auto Sync, Relink, pause/stop và error isolation hoạt động trên Revit 2024.
