# TableGen Advanced Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Hoàn thiện preview/chẩn đoán, company style profile, định dạng số, kéo thả nhiều file, quản lý liên kết và theo dõi nguồn trực tiếp cho TableGen.

**Architecture:** Core thêm các mô hình chẩn đoán, profile và biến đổi hiển thị thuần .NET; Presentation quản lý trạng thái preview/link action; Addin chỉ đọc catalog hoặc thực thi Revit action qua `ExternalEvent`. File watcher chỉ phát tín hiệu thay đổi và UI chủ động kiểm tra lại, không gọi Revit API từ background thread.

**Tech Stack:** C# 12/7.3, .NET Standard 2.0, .NET Framework 4.8, WPF, Autodesk Revit API 2024, Open XML SDK 2.20, Newtonsoft.Json 13.0.1, xUnit, PowerShell.

**Spec:** Người dùng đã bỏ qua tài liệu đặc tả và yêu cầu qua thẳng kế hoạch/inline execution. Phạm vi nguồn nằm tại `docs/superpowers/plans/2026-09-30-tung-diroot-missing-features-roadmap.md`.

## Global Constraints

- Revit 2024 only; không thêm binary trùng version assembly Revit đã nạp.
- Không gọi Revit API từ WPF event hoặc `FileSystemWatcher`; mọi mutation đi qua `RevitActionExternalEvent`.
- Preview không mở workbook editable, không chạy VBA và phải hoạt động khi không có Excel.
- Profile hỏng được cô lập; một profile hỏng không ngăn profile khác nạp.
- Link action chỉ tác động đúng `LinkId`; unlink giữ view/element, delete xóa view sau xác nhận.
- Live watch chỉ báo `Changed`; không tự cập nhật model, Save hoặc Synchronize with Central.

## Review Focus

- Workbook đang mở trong Excel vẫn preview được bằng `FileShare.ReadWrite | FileShare.Delete`.
- Công thức lỗi `#REF!/#VALUE!/#DIV/0!` và công thức thiếu cache được báo nhưng không làm crash preview.
- Profile chứa UniqueId Revit đã bị xóa tự fallback về style mặc định.
- Watcher nhận rename/replace kiểu Excel save-temp-file và không refresh lặp vô hạn.
- Unlink/delete bị chặn nếu người dùng đang ở document khác hoặc link/view không còn tồn tại.

---

### Task 1: Workbook preview diagnostics core

**Files:**
- Create: `src/TungDiroot.Core/Preview/WorkbookPreviewModels.cs`
- Create: `src/TungDiroot.Core/Preview/WorkbookPreviewAnalyzer.cs`
- Test: `tests/TungDiroot.Core.Tests/Preview/WorkbookPreviewAnalyzerTests.cs`

**Interfaces:**
- Consumes: `WorkbookRangeModel`, `ImportMode`, row/column dimensions and workbook warnings.
- Produces: `WorkbookPreviewReport Analyze(WorkbookRangeModel model, ImportMode mode)` containing estimated element counts and ordered issues.

- [ ] Write tests for formula errors, missing cache, unsupported drawings, large tables, merged cells and a clean range.
- [ ] Run the filtered Core test and verify RED because preview types do not exist.
- [ ] Implement immutable preview models and deterministic analyzer.
- [ ] Run filtered and full Core suites; expect all pass.
- [ ] Commit `feat: add TableGen workbook preview diagnostics`.

### Task 2: Company style and number-format profiles

**Files:**
- Modify: `src/TungDiroot.Core/Layout/StyleMappingProfile.cs`
- Create: `src/TungDiroot.Core/Layout/TableGenCompanyProfile.cs`
- Create: `src/TungDiroot.Core/Layout/TableGenProfileRepository.cs`
- Create: `src/TungDiroot.Core/Excel/NumericDisplayFormatter.cs`
- Modify: `src/TungDiroot.Core/Excel/CellModel.cs`
- Modify: `src/TungDiroot.Core/Excel/OpenXmlWorkbookReader.cs`
- Test: `tests/TungDiroot.Core.Tests/Layout/TableGenCompanyProfileTests.cs`
- Test: `tests/TungDiroot.Core.Tests/Excel/NumericDisplayFormatterTests.cs`

**Interfaces:**
- Produces: versioned named profiles with decimal symbol, thousands grouping, decimal places, keep-Excel-format option and style mappings by `CellStyleKey`.
- Produces: a formatter that never changes nonnumeric/formula-error text.

- [ ] Write profile round-trip/corrupt-file tests and numeric locale tests; verify RED.
- [ ] Extend the cell model with optional raw numeric value without changing existing callers.
- [ ] Implement repository on shared atomic `JsonProfileStore<T>` and numeric formatter.
- [ ] Run filtered and full Core suites; expect all pass.
- [ ] Commit `feat: add TableGen company formatting profiles`.

### Task 3: Preview UI and multi-file drag/drop

**Files:**
- Modify: `src/TungDiroot.Presentation/UI/AddExcelViewModel.cs`
- Create: `src/TungDiroot.Presentation/UI/WorkbookPreviewViewModel.cs`
- Modify: `src/TungDiroot.Addin/UI/AddExcelWindow.xaml`
- Modify: `src/TungDiroot.Addin/UI/AddExcelWindow.xaml.cs`
- Create: `src/TungDiroot.Addin/UI/WorkbookPreviewWindow.xaml`
- Create: `src/TungDiroot.Addin/UI/WorkbookPreviewWindow.xaml.cs`
- Test: `tests/TungDiroot.Presentation.Tests/UI/WorkbookPreviewViewModelTests.cs`
- Modify: `tools/Test-ResponsiveWindowLayout.ps1`

**Interfaces:**
- Consumes: Task 1 preview report and existing built-in renderer.
- Produces: Preview command/status per selected row and a common `AddWorkbookFiles(IEnumerable<string>)` path used by Browse and Drop.

- [ ] Write view-model tests for clean/warning/error reports, selection and busy state; verify RED.
- [ ] Implement preview presentation state.
- [ ] Add drop target, Browse/Drop shared ingestion, Preview button and responsive preview dialog.
- [ ] Run Presentation tests and responsive layout contract; expect all pass.
- [ ] Commit `feat: add TableGen preview and workbook drag drop`.

### Task 4: Style profile selection and Revit resolver integration

**Files:**
- Modify: `src/TungDiroot.Core/Import/WorkbookImportRequest.cs`
- Modify: `src/TungDiroot.Core/Links/LinkRecord.cs`
- Modify: `src/TungDiroot.Addin/Links/RevitLinkSchema.cs`
- Modify: `src/TungDiroot.Addin/Revit/RevitStyleResolver.cs`
- Modify: `src/TungDiroot.Addin/Import/ManualImportExecutor.cs`
- Modify: `src/TungDiroot.Addin/UI/AddExcelWindow.xaml`
- Modify: `src/TungDiroot.Addin/UI/AddExcelWindow.xaml.cs`
- Test: `tests/TungDiroot.Core.Tests/Layout/TableGenCompanyProfileTests.cs`
- Modify: `tools/Test-RevitSchemaContract.ps1`

**Interfaces:**
- Consumes: Task 2 profile name/settings.
- Produces: stable profile identity saved on each link; missing Revit type/line style falls back safely.

- [ ] Add schema/profile fallback tests and verify RED.
- [ ] Add profile selection to requests and version-compatible link migration defaults.
- [ ] Resolve mapped existing TextNoteType/GraphicsStyle; otherwise use current deterministic generated style.
- [ ] Run Core tests, schema contract and Revit 2024 build; expect all pass.
- [ ] Commit `feat: apply TableGen company style profiles`.

### Task 5: Link management actions

**Files:**
- Create: `src/TungDiroot.Core/Links/LinkManagementPlan.cs`
- Create: `src/TungDiroot.Addin/Links/RevitLinkManagementExecutor.cs`
- Modify: `src/TungDiroot.Presentation/UI/TableGenViewModel.cs`
- Modify: `src/TungDiroot.Presentation/UI/LinkRowViewModel.cs`
- Modify: `src/TungDiroot.Addin/UI/TableGenWindow.xaml`
- Modify: `src/TungDiroot.Addin/UI/TableGenWindow.xaml.cs`
- Test: `tests/TungDiroot.Core.Tests/Links/LinkManagementPlanTests.cs`
- Test: `tests/TungDiroot.Presentation.Tests/UI/TableGenViewModelTests.cs`
- Create: `tests/contracts/Test-TableGenLinkActionsContract.ps1`

**Interfaces:**
- Produces: Open View, Open Excel, Open Folder, Unlink Keep View and Delete Link + View commands gated by selected row/current document.
- Delete/unlink mutations run in one transaction and update registry/marker consistently.

- [ ] Write planner/view-model/contract tests and verify RED.
- [ ] Implement selection gating and pure action plan.
- [ ] Implement Revit executor and confirmation UI.
- [ ] Run Presentation/Core tests, add-in contract and Revit 2024 build; expect all pass.
- [ ] Commit `feat: add TableGen link management actions`.

### Task 6: Live source watcher and release candidate

**Files:**
- Create: `src/TungDiroot.Core/Links/SourceWatchDebouncer.cs`
- Create: `src/TungDiroot.Addin/Links/TableGenSourceWatchService.cs`
- Modify: `src/TungDiroot.Addin/UI/TableGenWindow.xaml.cs`
- Modify: `src/TungDiroot.Presentation/UI/TableGenViewModel.cs`
- Test: `tests/TungDiroot.Core.Tests/Links/SourceWatchDebouncerTests.cs`
- Modify: `docs/user-guide-vi.md`
- Create: `docs/testing/tablegen-advanced-revit-2024-checklist.md`
- Modify: `tools/Test-All.ps1`

**Interfaces:**
- Produces: debounced source-change notifications marshalled to the WPF dispatcher; refreshes link state only.

- [ ] Write create/change/rename/burst/dispose debounce tests and verify RED.
- [ ] Implement watcher service with directory grouping and dispatcher callback.
- [ ] Update docs/checklist and full test contract.
- [ ] Run `tools/Test-All.ps1`, build setup/portable RC and SHA-256; expect zero failures/warnings.
- [ ] Commit `chore: prepare TableGen Advanced release candidate`.

### Task 7: Usable company profile editor

**Files:**
- Create: `src/TungDiroot.Core/Layout/StyleMappingCandidateExtractor.cs`
- Create: `src/TungDiroot.Presentation/UI/TableGenProfileEditorViewModel.cs`
- Create: `src/TungDiroot.Addin/Revit/RevitStyleCatalogReader.cs`
- Create: `src/TungDiroot.Addin/UI/TableGenProfileManagerWindow.xaml`
- Create: `src/TungDiroot.Addin/UI/TableGenProfileManagerWindow.xaml.cs`
- Modify: `src/TungDiroot.Addin/UI/AddExcelWindow.xaml`
- Modify: `src/TungDiroot.Addin/UI/AddExcelWindow.xaml.cs`
- Modify: `src/TungDiroot.Addin/UI/TableGenWindow.xaml.cs`
- Test: `tests/TungDiroot.Core.Tests/Layout/StyleMappingCandidateExtractorTests.cs`
- Test: `tests/TungDiroot.Presentation.Tests/UI/TableGenProfileEditorViewModelTests.cs`

**Interfaces:**
- Produces: create/load/save/delete workflow for named TableGen company profiles using styles detected from the selected Excel range and Text/Line types read from the active Revit document.

- [ ] Write extractor/editor tests and verify RED.
- [ ] Implement deterministic style candidates and profile validation/build.
- [ ] Read Revit TextNoteType/GraphicsStyle catalog through ExternalEvent only.
- [ ] Add responsive profile manager UI and refresh Add Excel profile choices after save/delete.
- [ ] Run full test/build/release contracts and commit `feat: add TableGen company profile editor`.

### Task 8: Change target view and duplicate configuration

**Files:**
- Create: `src/TungDiroot.Core/Links/LinkTargetChangePlanner.cs`
- Create: `src/TungDiroot.Core/Links/LinkDuplicateRequestFactory.cs`
- Modify: `src/TungDiroot.Addin/Links/RevitLinkManagementExecutor.cs`
- Create: `src/TungDiroot.Addin/UI/TargetViewPickerWindow.xaml`
- Create: `src/TungDiroot.Addin/UI/TargetViewPickerWindow.xaml.cs`
- Modify: `src/TungDiroot.Addin/UI/TableGenWindow.xaml`
- Modify: `src/TungDiroot.Addin/UI/TableGenWindow.xaml.cs`
- Modify: `src/TungDiroot.Addin/UI/AddExcelWindow.xaml.cs`
- Modify: `src/TungDiroot.Presentation/UI/TableGenViewModel.cs`
- Test: Core/Presentation/link-action/responsive contracts.

**Interfaces:**
- Produces: recreate an existing non-Schedule link into a compatible existing target view with TransactionGroup rollback; open a copied import configuration in Add Excel without creating anything until the user confirms Import.

- [ ] Write planner/factory/command tests and verify RED.
- [ ] Add one-row target view picker and ExternalEvent catalog/action flow.
- [ ] Move target with copy-first transaction group semantics and preserve saved update behavior.
- [ ] Duplicate config into Add Excel, editable before import.
- [ ] Run full tests/build/RC and commit `feat: complete TableGen link workflows`.
