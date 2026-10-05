# TUNG_DIROOT TableGen Sync All Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a `Sync tất cả` command that rechecks and updates every Excel link already stored in the active Revit 2024 model without requiring a Revit restart before each sync.

**Architecture:** Extend `TableGenViewModel` with a filter-independent all-link command and route that command from the modeless `TableGenWindow` into the existing `RevitBatchCoordinator`. Reuse the current one-link-per-`ExternalEvent` pipeline and `TableGenUpdateProcessor`, so unchanged fingerprints are skipped, each changed view has its own transaction, and failures remain isolated.

**Tech Stack:** C# 10, .NET Framework 4.8, WPF/MVVM, Autodesk Revit 2024 API, xUnit, PowerShell release scripts.

## Global Constraints

- Target Autodesk Revit 2024 only.
- `Sync tất cả` must include every stored link, independent of search, status filter, visible rows, or selected checkboxes.
- Unchanged Excel links must return `UNCHANGED` and must not rebuild their Revit views.
- A failed or missing link must not stop the remaining batch.
- Do not automatically save the Revit model or run Synchronize with Central.
- Keep TableGen modeless and run Revit API mutations through the existing `ExternalEvent` coordinator.
- Do not modify selection checkboxes when Sync all starts or completes.
- Back up the currently installed add-in and manifest before installing the new build.

---

## File map

- `tests/TungDiroot.Presentation.Tests/UI/TableGenViewModelTests.cs`: behavioral regression tests for all-link selection, filter independence, empty state, and busy state.
- `src/TungDiroot.Presentation/UI/TableGenViewModel.cs`: owns `SyncAllCommand`, all-link ID projection, command state, and empty-list status.
- `src/TungDiroot.Addin/UI/TableGenWindow.xaml.cs`: connects the command callback to `RevitBatchCoordinator.Enqueue`.
- `src/TungDiroot.Addin/UI/TableGenWindow.xaml`: adds the visible `Sync tất cả` button.
- `docs/user-guide-vi.md`: documents usage and the one-time restart required only after installing a new DLL.

### Task 1: Add filter-independent Sync all behavior to the ViewModel

**Files:**
- Modify: `tests/TungDiroot.Presentation.Tests/UI/TableGenViewModelTests.cs`
- Modify: `src/TungDiroot.Presentation/UI/TableGenViewModel.cs`

**Interfaces:**
- Consumes: `ITableGenLinkSource.Load()`, `RelayCommand`, `LinkRowViewModel.LinkId`, `TableGenViewModel.IsBusy`.
- Produces: constructor parameter `Action<IReadOnlyList<Guid>>? syncAll = null`, property `RelayCommand SyncAllCommand`, property `IReadOnlyList<Guid> AllLinkIds`.

- [ ] **Step 1: Write failing behavior tests**

Add these tests to `TableGenViewModelTests`:

```csharp
[Fact]
public void Sync_all_uses_every_source_link_in_source_order_despite_filter_and_preserves_selection()
{
    var first = Item(LinkStatus.Ready, "A.xlsx", view: "VIEW A");
    var second = Item(LinkStatus.Changed, "B.xlsx", view: "VIEW B");
    IReadOnlyList<Guid>? requested = null;
    var vm = new TableGenViewModel(
        new FakeSource(new[] { first, second }),
        syncAll: ids => requested = ids);

    vm.VisibleLinks[0].IsSelected = true;
    vm.StatusFilter = LinkStatusFilter.Changed;
    Assert.Single(vm.VisibleLinks);

    vm.SyncAllCommand.Execute(null);

    Assert.Equal(new[] { first.LinkId, second.LinkId }, requested);
    vm.StatusFilter = LinkStatusFilter.All;
    Assert.True(vm.VisibleLinks[0].IsSelected);
    Assert.False(vm.VisibleLinks[1].IsSelected);
}

[Fact]
public void Sync_all_does_not_start_an_empty_batch_and_reports_the_reason()
{
    var callbackCount = 0;
    var vm = new TableGenViewModel(
        new FakeSource(Array.Empty<TableGenLinkItem>()),
        syncAll: _ => callbackCount++);

    vm.SyncAllCommand.Execute(null);

    Assert.Equal(0, callbackCount);
    Assert.Contains("Không có liên kết", vm.BatchStatusText);
}

[Fact]
public void Sync_all_is_disabled_while_a_batch_is_busy()
{
    var vm = new TableGenViewModel(
        new FakeSource(new[] { Item(LinkStatus.Ready, "A.xlsx") }));

    Assert.True(vm.SyncAllCommand.CanExecute(null));
    vm.IsBusy = true;
    Assert.False(vm.SyncAllCommand.CanExecute(null));
    vm.IsBusy = false;
    Assert.True(vm.SyncAllCommand.CanExecute(null));
}
```

- [ ] **Step 2: Run the focused test and verify RED**

Run:

```powershell
$env:TUNG_DIROOT_DOTNET = 'C:\Users\tungnt2303\Downloads\tungmep2\.dotnet\dotnet.exe'
& $env:TUNG_DIROOT_DOTNET test '.\tests\TungDiroot.Presentation.Tests\TungDiroot.Presentation.Tests.csproj' -c Release --filter 'FullyQualifiedName~TableGenViewModelTests'
```

Expected: FAIL at compile time because `TableGenViewModel` has no named parameter `syncAll` and no `SyncAllCommand` property.

- [ ] **Step 3: Implement the minimal ViewModel command**

In `TableGenViewModel`:

```csharp
public TableGenViewModel(
    ITableGenLinkSource source,
    Action? addExcel = null,
    Action? checkSources = null,
    Action<IReadOnlyList<Guid>>? updateSelected = null,
    Action<IReadOnlyList<Guid>>? syncAll = null,
    Action? pauseBatch = null,
    Action? resumeBatch = null,
    Action? stopBatch = null,
    Action? relinkFolder = null,
    Action<IReadOnlyDictionary<Guid, bool>>? saveAutoSync = null)
{
    // Keep the existing assignments.
    SyncAllCommand = new RelayCommand(() =>
    {
        var ids = AllLinkIds;
        if (ids.Count == 0)
        {
            BatchStatusText = "Không có liên kết Excel để sync.";
            return;
        }
        syncAll?.Invoke(ids);
    }, () => !IsBusy);
}

public RelayCommand SyncAllCommand { get; }
public IReadOnlyList<Guid> AllLinkIds => _allRows.Select(row => row.LinkId).ToArray();
```

Add `SyncAllCommand.RaiseCanExecuteChanged();` to the existing `IsBusy` setter. Keep `SelectedLinkIds` unchanged so Sync all cannot mutate manual selection.

- [ ] **Step 4: Run the focused test and verify GREEN**

Run the same `dotnet test ... --filter 'FullyQualifiedName~TableGenViewModelTests'` command.

Expected: all `TableGenViewModelTests` PASS with zero warnings caused by the change.

- [ ] **Step 5: Run all Presentation tests**

```powershell
& $env:TUNG_DIROOT_DOTNET test '.\tests\TungDiroot.Presentation.Tests\TungDiroot.Presentation.Tests.csproj' -c Release
```

Expected: PASS.

- [ ] **Step 6: Commit Task 1**

```powershell
git add -- 'tests/TungDiroot.Presentation.Tests/UI/TableGenViewModelTests.cs' 'src/TungDiroot.Presentation/UI/TableGenViewModel.cs'
git commit -m 'feat: add TableGen sync all command'
```

### Task 2: Wire Sync all into the modeless Revit TableGen window

**Files:**
- Modify: `src/TungDiroot.Addin/UI/TableGenWindow.xaml.cs`
- Modify: `src/TungDiroot.Addin/UI/TableGenWindow.xaml`

**Interfaces:**
- Consumes: `TableGenViewModel(..., syncAll: Action<IReadOnlyList<Guid>>)`, `RevitBatchCoordinator.Enqueue(IEnumerable<Guid>)`, `SyncAllCommand`.
- Produces: a visible `Sync tất cả` button whose command queues every stored Link ID through the existing coordinator.

- [ ] **Step 1: Add the window callback**

Change the ViewModel construction to named arguments at the two batch entry points so positional parameter drift cannot swap callbacks:

```csharp
_viewModel = new TableGenViewModel(
    _linkSource,
    addExcel: RequestAddExcel,
    checkSources: RequestRefresh,
    updateSelected: ids =>
    {
        if (ids.Count > 0) _batch.Enqueue(ids);
    },
    syncAll: ids =>
    {
        if (ids.Count > 0) _batch.Enqueue(ids);
    },
    pauseBatch: _batch.Pause,
    resumeBatch: _batch.Resume,
    stopBatch: _batch.RequestStop,
    relinkFolder: RelinkFolder,
    saveAutoSync: SaveAutoSync);
```

- [ ] **Step 2: Add the XAML button**

Place this button immediately before `Cập nhật đã chọn`:

```xml
<Button Content="Sync tất cả"
        Command="{Binding SyncAllCommand}"
        Padding="14,7"
        Margin="0,0,8,0"
        ToolTip="Kiểm tra và cập nhật mọi liên kết Excel đã nạp; tự bỏ qua nguồn không đổi."/>
```

Do not bind the command to DataGrid selection and do not alter any checkbox binding.

- [ ] **Step 3: Build the Presentation and Revit 2024 add-in projects**

```powershell
& $env:TUNG_DIROOT_DOTNET build '.\src\TungDiroot.Presentation\TungDiroot.Presentation.csproj' -c Release
& '.\build\Build-Addin.ps1' -RevitVersion 2024 -Configuration Release
```

Expected: both commands exit 0; `artifacts/addin/2024/TUNG_DIROOT.Revit2024.dll` is generated.

- [ ] **Step 4: Run the Revit assembly contract and focused tests**

```powershell
& '.\tools\Test-AddinContract.ps1' -RevitVersion 2024
& $env:TUNG_DIROOT_DOTNET test '.\tests\TungDiroot.Presentation.Tests\TungDiroot.Presentation.Tests.csproj' -c Release --filter 'FullyQualifiedName~TableGenViewModelTests'
```

Expected: add-in contract PASS and focused tests PASS.

- [ ] **Step 5: Commit Task 2**

```powershell
git add -- 'src/TungDiroot.Addin/UI/TableGenWindow.xaml.cs' 'src/TungDiroot.Addin/UI/TableGenWindow.xaml'
git commit -m 'feat: expose sync all in TableGen'
```

### Task 3: Document, verify, package, back up, and install

**Files:**
- Modify: `docs/user-guide-vi.md`
- Generated: `artifacts/staging/TUNG_DIROOT/2024/*`
- Generated: `dist/TUNG_DIROOT-Setup-Revit2024.exe`
- Generated: `dist/TUNG_DIROOT-Portable-Revit2024.zip`
- Backup: `C:/Users/tungnt2303/Downloads/TUNG_DIROOT_Backups/before-sync-all-<timestamp>/`
- Install: `C:/Users/tungnt2303/AppData/Local/TUNG_DIROOT/1.0.2/2024/*`
- Manifest: `C:/Users/tungnt2303/AppData/Roaming/Autodesk/Revit/Addins/2024/TUNG_DIROOT.addin`

**Interfaces:**
- Consumes: tested add-in assemblies and existing installer scripts.
- Produces: verified Revit 2024 setup/portable artifacts, a restorable backup, and an installed `1.0.2` payload referenced by the Revit manifest.

- [ ] **Step 1: Update the Vietnamese user guide**

Add a `Sync tất cả` section stating:

```markdown
### Sync tất cả liên kết đã nạp

1. Mở model chứa các liên kết Excel đã tạo bằng TUNG_DIROOT.
2. Mở `TUNG_DIROOT > TableGen`.
3. Bấm `Sync tất cả`.

TableGen kiểm tra mọi liên kết trong model, kể cả liên kết đang bị ẩn bởi ô tìm kiếm hoặc bộ lọc. Nguồn không đổi được bỏ qua; nguồn thay đổi được cập nhật theo chế độ đã lưu. Một liên kết lỗi không dừng các liên kết còn lại. Lệnh không tự Save model và không Synchronize with Central.

Sau khi cài một DLL mới, cần khởi động lại Revit một lần để Revit nạp DLL. Không cần khởi động lại Revit trước các lần `Sync tất cả` tiếp theo.
```

- [ ] **Step 2: Run the full automated suite**

```powershell
$env:TUNG_DIROOT_DOTNET = 'C:\Users\tungnt2303\Downloads\tungmep2\.dotnet\dotnet.exe'
& '.\tools\Test-All.ps1'
```

Expected: `TUNG_DIROOT automated test suite passed.` and no failed test/build step.

- [ ] **Step 3: Commit the user guide**

```powershell
git add -- 'docs/user-guide-vi.md'
git commit -m 'docs: explain TableGen sync all'
```

- [ ] **Step 4: Build clean release artifacts**

```powershell
& '.\build\Build-Release.ps1' -Version '1.0.0' -ReleaseCandidate
```

Expected: setup and portable ZIP are recreated under `dist`; release layout and 2024-only gates pass. The source release contract remains `1.0.0`; the local immutable install folder below uses `1.0.2` to avoid overwriting the DLL currently loaded by Revit.

- [ ] **Step 5: Create a restorable backup before installation**

Resolve the timestamp once, then copy the exact installed payload, manifest, and distributables:

```powershell
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$backup = "C:\Users\tungnt2303\Downloads\TUNG_DIROOT_Backups\before-sync-all-$stamp"
New-Item -ItemType Directory -Path $backup -Force | Out-Null
Copy-Item -LiteralPath 'C:\Users\tungnt2303\AppData\Local\TUNG_DIROOT\1.0.1' -Destination (Join-Path $backup 'installed-1.0.1') -Recurse -Force
Copy-Item -LiteralPath 'C:\Users\tungnt2303\AppData\Roaming\Autodesk\Revit\Addins\2024\TUNG_DIROOT.addin' -Destination (Join-Path $backup 'TUNG_DIROOT.addin') -Force
Copy-Item -LiteralPath '.\dist' -Destination (Join-Path $backup 'dist') -Recurse -Force
```

Before any future restore, validate that the restore target remains inside `C:\Users\tungnt2303\AppData\Local\TUNG_DIROOT` or the exact Revit 2024 Addins directory.

- [ ] **Step 6: Install the new payload into a new immutable folder**

```powershell
& '.\installer\Install-TUNG_DIROOT.ps1' `
    -StageRoot '.\artifacts\staging\TUNG_DIROOT' `
    -InstallRoot 'C:\Users\tungnt2303\AppData\Local\TUNG_DIROOT\1.0.2' `
    -ManifestRoot 'C:\Users\tungnt2303\AppData\Roaming\Autodesk\Revit\Addins' `
    -Version '1.0.2' `
    -RevitVersions 2024
```

Expected: manifest points to `C:\Users\tungnt2303\AppData\Local\TUNG_DIROOT\1.0.2\2024\TUNG_DIROOT.Revit2024.dll`. If Revit is open, it continues using the old in-memory DLL until the next restart; do not attempt to unload or overwrite that assembly.

- [ ] **Step 7: Verify installed hashes and manifest**

```powershell
$stageDll = '.\artifacts\staging\TUNG_DIROOT\2024\TUNG_DIROOT.Revit2024.dll'
$installedDll = 'C:\Users\tungnt2303\AppData\Local\TUNG_DIROOT\1.0.2\2024\TUNG_DIROOT.Revit2024.dll'
if ((Get-FileHash $stageDll).Hash -ne (Get-FileHash $installedDll).Hash) { throw 'Installed DLL hash mismatch.' }
[xml]$manifest = Get-Content -Raw 'C:\Users\tungnt2303\AppData\Roaming\Autodesk\Revit\Addins\2024\TUNG_DIROOT.addin'
if ($manifest.RevitAddIns.AddIn.Assembly -ne [IO.Path]::GetFullPath($installedDll)) { throw 'Manifest does not point to the new DLL.' }
```

Expected: no exception.

- [ ] **Step 8: Run the direct Revit checklist after one restart**

In Revit 2024:

1. Open a model containing at least two existing TUNG_DIROOT Excel links.
2. Edit one linked Excel range and leave the other unchanged.
3. Open TableGen and apply a filter that hides one link.
4. Press `Sync tất cả`.
5. Confirm both links are considered, only the changed view is rebuilt, selection checkboxes stay unchanged, and the batch completes.
6. Edit the Excel file again without closing Revit, press `Sync tất cả` again, and confirm the second sync works in the same Revit session.
7. Confirm the model was not automatically saved and no Synchronize with Central occurred.

- [ ] **Step 9: Record the final verification commit**

Update `docs/testing/release-1.0.0-report.md` with automated results, artifact hashes, backup path, installed DLL hash, and the manual checklist result. Then run:

```powershell
git add -- 'docs/testing/release-1.0.0-report.md'
git commit -m 'docs: verify TableGen sync all release'
git status --short
```

Expected: clean working tree after the commit, excluding intentionally ignored generated artifacts.
