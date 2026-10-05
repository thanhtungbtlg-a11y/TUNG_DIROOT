# TUNG_DIROOT Revit Writers and Hybrid Image Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Tạo add-in Revit đa phiên bản có thể nhập nhiều vùng Excel thủ công thành Bảng Revit hoặc Ảnh trong Drafting, Legend và Schedule theo đúng ma trận tương thích.

**Architecture:** Add-in project được build riêng theo `RevitVersion`, tham chiếu `TungDiroot.Core`. Geometry/layout được lập kế hoạch bằng pure models có unit test trước khi Revit writer tạo element. Image path dùng built-in WPF renderer mặc định và Excel COM renderer tùy chọn.

**Tech Stack:** Autodesk Revit API 2024–2026, .NET Framework 4.8, .NET 8 Windows, WPF, Open XML core từ Plan 1, xUnit, PowerShell.

## Global Constraints

- Không phân phối Autodesk Revit API assemblies.
- Revit 2024 build `net48`; Revit 2025/2026 build `net8.0-windows`.
- Legend chỉ được duplicate từ Legend mẫu do người dùng chọn.
- `Ảnh + Schedule` bị chặn trước transaction.
- Chỉ element do `TUNG_DIROOT` tạo được gắn Link ID và sửa/xóa.
- Không group bảng thành Revit Group.
- Image engine phải chạy được khi không có Microsoft Excel.

---

### Task 1: Add-in shell và build adapter đa phiên bản

**Files:**
- Create: `src/TungDiroot.Addin/TungDiroot.Addin.csproj`
- Create: `src/TungDiroot.Addin/Application.cs`
- Create: `src/TungDiroot.Addin/Command_TableGen.cs`
- Create: `src/TungDiroot.Addin/Resources/TableGen16.png`
- Create: `src/TungDiroot.Addin/Resources/TableGen32.png`
- Create: `build/Resolve-RevitApi.ps1`
- Create: `build/Build-Addin.ps1`
- Create: `tools/Test-AddinContract.ps1`
- Modify: `TUNG_DIROOT.sln`

**Interfaces:**
- Produces: `TungDiroot.Addin.Application : IExternalApplication`, `Command_TableGen : IExternalCommand`.

- [ ] **Step 1: Viết assembly contract test**

```powershell
$assembly = [Reflection.Assembly]::LoadFrom($AssemblyPath)
$app = $assembly.GetType('TungDiroot.Addin.Application', $true)
$command = $assembly.GetType('TungDiroot.Addin.Command_TableGen', $true)
if (-not $app.GetInterface('Autodesk.Revit.UI.IExternalApplication')) { throw 'Application contract missing' }
if (-not $command.GetInterface('Autodesk.Revit.UI.IExternalCommand')) { throw 'Command contract missing' }
```

- [ ] **Step 2: Chạy contract test để xác nhận fail**

Run: `powershell -ExecutionPolicy Bypass -File tools/Test-AddinContract.ps1 -RevitVersion 2024`  
Expected: FAIL vì add-in assembly chưa tồn tại.

- [ ] **Step 3: Tạo project và ribbon application**

```csharp
public Result OnStartup(UIControlledApplication application)
{
    const string tab = "TUNG_DIROOT";
    try { application.CreateRibbonTab(tab); } catch (Autodesk.Revit.Exceptions.ArgumentException) { }
    var panel = application.GetRibbonPanels(tab).FirstOrDefault(x => x.Name == "Excel Tools")
        ?? application.CreateRibbonPanel(tab, "Excel Tools");
    var data = new PushButtonData(
        "TUNG_DIROOT.TableGen", "TableGen",
        typeof(Application).Assembly.Location,
        typeof(Command_TableGen).FullName);
    panel.AddItem(data);
    return Result.Succeeded;
}
```

`Resolve-RevitApi.ps1` nhận `-RevitVersion`, ưu tiên biến `REVIT<year>_DIR`, sau đó `%ProgramFiles%\Autodesk\Revit <year>`, và fail với thông báo chính xác nếu thiếu `RevitAPI.dll/RevitAPIUI.dll`. Project đặt `Private=false` cho hai reference và nhúng icon dưới dạng Resource.

- [ ] **Step 4: Build và chạy contract test 2024**

Run: `powershell -ExecutionPolicy Bypass -File build/Build-Addin.ps1 -RevitVersion 2024 -Configuration Release`  
Run: `powershell -ExecutionPolicy Bypass -File tools/Test-AddinContract.ps1 -RevitVersion 2024`  
Expected: PASS và output không chứa Revit API DLL.

- [ ] **Step 5: Commit**

```powershell
git add TUNG_DIROOT.sln src/TungDiroot.Addin build tools/Test-AddinContract.ps1
git commit -m "feat: add multi-version Revit add-in shell"
```

### Task 2: View creation planner và Revit view factory

**Files:**
- Create: `src/TungDiroot.Core/RevitPlanning/ViewCreationPlan.cs`
- Create: `src/TungDiroot.Core/RevitPlanning/ViewCreationPlanner.cs`
- Test: `tests/TungDiroot.Core.Tests/RevitPlanning/ViewCreationPlannerTests.cs`
- Create: `src/TungDiroot.Addin/Revit/IRevitViewFactory.cs`
- Create: `src/TungDiroot.Addin/Revit/RevitViewFactory.cs`

**Interfaces:**
- Consumes: `TargetViewKind`, `ImportMode`.
- Produces: `ViewCreationPlanner.Plan(ViewCreationRequest)`, `IRevitViewFactory.Create(Document, ViewCreationPlan)`.

- [ ] **Step 1: Viết test rule cho Drafting/Legend/Schedule**

```csharp
[Theory]
[InlineData(ImportMode.Image, TargetViewKind.Schedule, false, "IMAGE_SCHEDULE_UNSUPPORTED")]
[InlineData(ImportMode.RevitTable, TargetViewKind.Legend, false, "LEGEND_SEED_REQUIRED")]
[InlineData(ImportMode.RevitTable, TargetViewKind.Drafting, true, null)]
public void Validate_view_request(ImportMode mode, TargetViewKind kind, bool hasLegendSeed, string? expectedError)
{
    var plan = ViewCreationPlanner.Plan(new(mode, kind, hasLegendSeed, "DM PCCC"));
    Assert.Equal(expectedError, plan.ErrorCode);
}
```

- [ ] **Step 2: Chạy test để xác nhận fail**

Run: `dotnet test tests/TungDiroot.Core.Tests -c Release --filter ViewCreationPlannerTests`  
Expected: FAIL compile.

- [ ] **Step 3: Cài planner và Revit factory**

```csharp
public sealed record ViewCreationPlan(
    TargetViewKind Kind,
    string RequestedName,
    bool DuplicateLegendSeed,
    string? ErrorCode);
```

Factory dùng `ViewDrafting.Create` cho Drafting, `seedLegend.Duplicate(ViewDuplicateOption.Duplicate)` cho Legend, và `ViewSchedule.CreateSchedule` rồi chuẩn bị header cho Schedule. Factory kiểm tra `CanViewBeDuplicated`, loại view seed và tên trùng trước khi transaction commit.

- [ ] **Step 4: Test planner và build add-in**

Run: `powershell -ExecutionPolicy Bypass -File tools/Test-Core.ps1`  
Run: `powershell -ExecutionPolicy Bypass -File build/Build-Addin.ps1 -RevitVersion 2024 -Configuration Release`  
Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/TungDiroot.Core/RevitPlanning tests/TungDiroot.Core.Tests/RevitPlanning src/TungDiroot.Addin/Revit
git commit -m "feat: create compatible Revit target views"
```

### Task 3: Table layout planner và Drafting/Legend writer

**Files:**
- Create: `src/TungDiroot.Core/Layout/TableLayoutPlan.cs`
- Create: `src/TungDiroot.Core/Layout/TableLayoutPlanner.cs`
- Create: `src/TungDiroot.Core/Layout/BorderSegmentCoalescer.cs`
- Create: `src/TungDiroot.Core/Layout/FillRectangleCoalescer.cs`
- Create: `src/TungDiroot.Core/Layout/StyleMappingProfile.cs`
- Create: `src/TungDiroot.Core/Layout/BlackAndWhiteTransform.cs`
- Test: `tests/TungDiroot.Core.Tests/Layout/TableLayoutPlannerTests.cs`
- Test: `tests/TungDiroot.Core.Tests/Layout/CoalescerTests.cs`
- Test: `tests/TungDiroot.Core.Tests/Layout/StyleMappingProfileTests.cs`
- Create: `src/TungDiroot.Addin/Revit/ElementLinkTagger.cs`
- Create: `src/TungDiroot.Addin/Revit/RevitStyleResolver.cs`
- Create: `src/TungDiroot.Addin/Revit/DraftingLegendTableWriter.cs`

**Interfaces:**
- Consumes: `WorkbookRangeModel`.
- Produces: `TableLayoutPlanner.Create(WorkbookRangeModel, LayoutOptions)`, `DraftingLegendTableWriter.Write(Document, View, TableLayoutPlan, Guid linkId)`.

- [ ] **Step 1: Viết test tọa độ, merge và coalescing**

```csharp
[Fact]
public void Merged_header_creates_one_text_and_outer_border()
{
    var model = RangeModelSamples.MergedHeader("A1:D1", "DANH MỤC");
    var plan = TableLayoutPlanner.Create(model, LayoutOptions.Default);
    Assert.Single(plan.Texts);
    Assert.Equal(4, plan.Borders.Count(x => x.Role == BorderRole.Outer));
}

[Fact]
public void Adjacent_equal_horizontal_borders_are_coalesced()
{
    var result = BorderSegmentCoalescer.Coalesce(BorderSamples.ThreeAdjacent());
    Assert.Single(result);
}
```

- [ ] **Step 2: Chạy test để xác nhận fail**

Run: `dotnet test tests/TungDiroot.Core.Tests -c Release --filter "TableLayoutPlannerTests|CoalescerTests"`  
Expected: FAIL compile.

- [ ] **Step 3: Cài pure layout planner và Revit writer**

```csharp
public sealed record TableLayoutPlan(
    IReadOnlyList<TextPlacement> Texts,
    IReadOnlyList<LinePlacement> Borders,
    IReadOnlyList<FillPlacement> Fills,
    double WidthPoints,
    double HeightPoints,
    IReadOnlyList<ImportWarning> Warnings);
```

Planner đổi Excel point sang internal feet tại boundary adapter, tính anchor theo horizontal/vertical alignment và chỉ tạo một text placement cho top-left cell của merge. Writer map font/style qua `RevitStyleResolver`, tạo TextNote/DetailCurve/FilledRegion, rồi gắn entity `{LinkId, CellKey, Role}` bằng `ElementLinkTagger`.
Element tag dùng schema GUID cố định `D155A83C-7DA5-460F-89C7-1627F1158951` để updater về sau chỉ thu thập đúng element thuộc add-in.
`StyleMappingProfile` dùng Excel style key làm khóa và lưu Revit TextNoteType/GraphicsStyle UniqueId; entry mất target dùng resolver mặc định cùng warning. `BlackAndWhiteTransform` chuyển text/border sang đen, bỏ background màu và giữ grayscale theo option đã chọn.

- [ ] **Step 4: Chạy unit test và build 2024**

Run: `powershell -ExecutionPolicy Bypass -File tools/Test-Core.ps1`  
Run: `powershell -ExecutionPolicy Bypass -File build/Build-Addin.ps1 -RevitVersion 2024 -Configuration Release`  
Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/TungDiroot.Core/Layout tests/TungDiroot.Core.Tests/Layout src/TungDiroot.Addin/Revit
git commit -m "feat: write Excel tables to drafting and legend views"
```

### Task 4: Schedule header writer và structure compatibility

**Files:**
- Create: `src/TungDiroot.Core/Updates/StructureSignature.cs`
- Create: `src/TungDiroot.Core/Updates/StructureCompatibility.cs`
- Test: `tests/TungDiroot.Core.Tests/Updates/StructureCompatibilityTests.cs`
- Create: `src/TungDiroot.Addin/Revit/ScheduleHeaderTableWriter.cs`

**Interfaces:**
- Produces: `StructureSignature.From(WorkbookRangeModel)`, `StructureCompatibility.Compare`, `ScheduleHeaderTableWriter.Write`.

- [ ] **Step 1: Viết test cấu trúc tương thích**

```csharp
[Fact]
public void Same_rows_columns_and_merges_support_data_only()
{
    var before = new StructureSignature(10, 4, new[] { "A1:D1" });
    var after = new StructureSignature(10, 4, new[] { "A1:D1" });
    Assert.True(StructureCompatibility.Compare(before, after).CanUpdateDataOnly);
}

[Fact]
public void Changed_merge_requires_recreate()
{
    var result = StructureCompatibility.Compare(
        new(10, 4, new[] { "A1:D1" }), new(10, 4, new[] { "A1:C1" }));
    Assert.Equal("MERGE_STRUCTURE_CHANGED", result.ReasonCode);
}
```

- [ ] **Step 2: Chạy test để xác nhận fail**

Run: `dotnet test tests/TungDiroot.Core.Tests -c Release --filter StructureCompatibilityTests`  
Expected: FAIL compile.

- [ ] **Step 3: Cài compatibility và writer**

```csharp
public sealed class ScheduleHeaderTableWriter
{
    public void Write(ViewSchedule schedule, WorkbookRangeModel model, UpdateBehavior behavior)
    {
        var header = schedule.GetTableData().GetSectionData(SectionType.Header);
        ScheduleHeaderResizer.Resize(header, model.RowCount, model.ColumnCount, behavior);
        ScheduleMergeWriter.Apply(header, model.MergedRanges, behavior);
        ScheduleCellWriter.Apply(header, model, behavior);
    }
}
```

Writer lấy `ViewSchedule.GetTableData().GetSectionData(SectionType.Header)`, resize header, merge cell theo normalized range, đặt text/style/row height/column width. Data-only path chỉ gọi `SetCellText`; recreate path unmerge/resize/reapply style trong transaction của riêng view.

- [ ] **Step 4: Chạy test/build**

Run: `powershell -ExecutionPolicy Bypass -File tools/Test-Core.ps1`  
Run: `powershell -ExecutionPolicy Bypass -File build/Build-Addin.ps1 -RevitVersion 2024 -Configuration Release`  
Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/TungDiroot.Core/Updates tests/TungDiroot.Core.Tests/Updates src/TungDiroot.Addin/Revit/ScheduleHeaderTableWriter.cs
git commit -m "feat: write Excel data to schedule headers"
```

### Task 5: Built-in WPF image renderer

**Files:**
- Create: `src/TungDiroot.Rendering/TungDiroot.Rendering.csproj`
- Create: `src/TungDiroot.Rendering/IWorkbookImageRenderer.cs`
- Create: `src/TungDiroot.Rendering/BuiltInWorkbookImageRenderer.cs`
- Create: `src/TungDiroot.Rendering/FontFallbackResolver.cs`
- Create: `tests/TungDiroot.Rendering.Tests/TungDiroot.Rendering.Tests.csproj`
- Create: `tests/TungDiroot.Rendering.Tests/BuiltInWorkbookImageRendererTests.cs`
- Create: `tests/TungDiroot.Rendering.Tests/Golden/README.md`
- Create: `src/TungDiroot.Addin/Revit/RevitImageWriter.cs`
- Modify: `TUNG_DIROOT.sln`

**Interfaces:**
- Produces: `IWorkbookImageRenderer.Render(WorkbookRangeModel, ImageRenderOptions, Stream)` và `ImageRenderResult`.

- [ ] **Step 1: Viết test DPI, kích thước và cảnh báo font/drawing**

```csharp
[Theory]
[InlineData(ImageDpi.Dpi150, 300)]
[InlineData(ImageDpi.Dpi300, 600)]
[InlineData(ImageDpi.Dpi600, 1200)]
public void Two_inch_table_has_expected_pixel_width(ImageDpi dpi, int expectedWidth)
{
    var result = RenderSamples.TwoInchTable(dpi);
    Assert.Equal(expectedWidth, result.PixelWidth);
    Assert.True(PngHeader.IsValid(result.Bytes));
}
```

- [ ] **Step 2: Chạy test để xác nhận fail**

Run: `dotnet test tests/TungDiroot.Rendering.Tests -c Release`  
Expected: FAIL compile.

- [ ] **Step 3: Cài WPF renderer và image writer**

```csharp
public interface IWorkbookImageRenderer
{
    ImageRenderResult Render(WorkbookRangeModel model, ImageRenderOptions options, Stream destination);
}
```

Renderer dùng `DrawingVisual`, `DrawingContext`, `FormattedText` và `RenderTargetBitmap`; áp black/white trước khi draw; encode `PngBitmapEncoder`. Font thiếu được resolve theo chuỗi `Arial -> Segoe UI -> sans-serif` và warning chứa font gốc/fallback. RevitImageWriter tạo file temp GUID, `ImageType.Create`, đặt ImageInstance tại origin view và xóa temp trong `finally`.

- [ ] **Step 4: Chạy renderer tests và add-in build**

Run: `dotnet test tests/TungDiroot.Rendering.Tests -c Release`  
Run: `powershell -ExecutionPolicy Bypass -File build/Build-Addin.ps1 -RevitVersion 2024 -Configuration Release`  
Expected: PASS và PNG hợp lệ ở 150/300/600 DPI.

- [ ] **Step 5: Commit**

```powershell
git add TUNG_DIROOT.sln src/TungDiroot.Rendering tests/TungDiroot.Rendering.Tests src/TungDiroot.Addin/Revit/RevitImageWriter.cs
git commit -m "feat: render Excel ranges without Microsoft Excel"
```

### Task 6: Excel High Fidelity renderer và engine selector

**Files:**
- Create: `src/TungDiroot.Rendering/RendererPreference.cs`
- Create: `src/TungDiroot.Rendering/RendererSelector.cs`
- Create: `src/TungDiroot.Rendering/ExcelAvailabilityProbe.cs`
- Create: `src/TungDiroot.Rendering/ExcelComWorkbookImageRenderer.cs`
- Test: `tests/TungDiroot.Rendering.Tests/RendererSelectorTests.cs`
- Test: `tests/TungDiroot.Rendering.Tests/ExcelComCleanupContractTests.cs`

**Interfaces:**
- Produces: `RendererSelector.Select(RendererPreference, IExcelAvailabilityProbe)`, `ExcelComWorkbookImageRenderer.RenderFileRange`.

- [ ] **Step 1: Viết selector/fallback tests bằng fake probe**

```csharp
[Theory]
[InlineData(RendererPreference.Automatic, true, RendererKind.ExcelHighFidelity)]
[InlineData(RendererPreference.Automatic, false, RendererKind.BuiltIn)]
[InlineData(RendererPreference.BuiltInOnly, true, RendererKind.BuiltIn)]
public void Selects_expected_engine(RendererPreference preference, bool excelAvailable, RendererKind expected)
{
    Assert.Equal(expected, RendererSelector.Select(preference, new FakeExcelProbe(excelAvailable)));
}
```

- [ ] **Step 2: Chạy test để xác nhận fail**

Run: `dotnet test tests/TungDiroot.Rendering.Tests -c Release --filter "RendererSelectorTests|ExcelComCleanupContractTests"`  
Expected: FAIL compile.

- [ ] **Step 3: Cài late-bound COM renderer**

```csharp
public sealed class ExcelAvailabilityProbe : IExcelAvailabilityProbe
{
    public bool IsAvailable => Type.GetTypeFromProgID("Excel.Application", throwOnError: false) is not null;
}

public static class RendererSelector
{
    public static RendererKind Select(RendererPreference preference, IExcelAvailabilityProbe probe) =>
        preference == RendererPreference.BuiltInOnly || !probe.IsAvailable
            ? RendererKind.BuiltIn
            : RendererKind.ExcelHighFidelity;
}
```

Probe dùng `Type.GetTypeFromProgID("Excel.Application", throwOnError: false)`. Renderer đặt `Visible=false`, `DisplayAlerts=false`, mở workbook read-only, chọn worksheet/range, `CopyPicture`, xuất chart tạm thành PNG, đóng workbook `Close(false)`, gọi `Quit`, release COM objects theo thứ tự đảo trong `finally`. Macro security được đặt force-disable trước khi mở workbook và phục hồi trước khi Quit.

- [ ] **Step 4: Chạy tests trên máy có/không có Excel**

Run: `dotnet test tests/TungDiroot.Rendering.Tests -c Release`  
Expected: unit tests PASS; integration test COM tự skip với reason `EXCEL_NOT_INSTALLED` nếu probe false, không để lại process Excel do test tạo.

- [ ] **Step 5: Commit**

```powershell
git add src/TungDiroot.Rendering tests/TungDiroot.Rendering.Tests
git commit -m "feat: add optional Excel high-fidelity renderer"
```

### Task 7: Hộp Thêm Excel và import executor thủ công

**Files:**
- Create: `src/TungDiroot.Presentation/TungDiroot.Presentation.csproj`
- Create: `src/TungDiroot.Presentation/UI/AddExcelViewModel.cs`
- Create: `src/TungDiroot.Presentation/UI/BindableBase.cs`
- Create: `src/TungDiroot.Presentation/UI/RelayCommand.cs`
- Create: `src/TungDiroot.Core/Import/ImportSizeEstimator.cs`
- Create: `src/TungDiroot.Addin/UI/AddExcelWindow.xaml`
- Create: `src/TungDiroot.Addin/UI/AddExcelWindow.xaml.cs`
- Create: `src/TungDiroot.Addin/Import/ManualImportExecutor.cs`
- Create: `src/TungDiroot.Addin/Import/ImportRowResult.cs`
- Create: `tests/TungDiroot.Presentation.Tests/TungDiroot.Presentation.Tests.csproj`
- Create: `tests/TungDiroot.Presentation.Tests/UI/AddExcelViewModelTests.cs`
- Create: `tests/TungDiroot.Core.Tests/Import/ImportSizeEstimatorTests.cs`
- Create: `docs/testing/plan2-revit-smoke.md`
- Modify: `src/TungDiroot.Addin/Command_TableGen.cs`
- Modify: `TUNG_DIROOT.sln`
- Create: `tools/Test-Plan2.ps1`

**Interfaces:**
- Consumes: workbook catalog/range reader, view factory, table writers và image renderers.
- Produces: `ManualImportExecutor.Execute(UIApplication, IReadOnlyList<WorkbookImportRequest>)`.

- [ ] **Step 1: Viết view-model validation tests trong Presentation test project**

```csharp
[Theory]
[InlineData(ImportMode.Image, TargetViewKind.Schedule, false, "IMAGE_SCHEDULE_UNSUPPORTED")]
[InlineData(ImportMode.RevitTable, TargetViewKind.Legend, false, "LEGEND_SEED_REQUIRED")]
public void Invalid_rows_surface_the_planner_error(
    ImportMode mode, TargetViewKind target, bool hasLegendSeed, string expectedCode)
{
    var row = AddExcelRowSamples.Valid() with
    {
        ImportMode = mode,
        TargetViewKind = target,
        LegendSeedAvailable = hasLegendSeed
    };
    Assert.Equal(expectedCode, AddExcelViewModel.ValidateRow(row).Single().Code);
}

[Fact]
public void Duplicate_view_names_are_rejected_before_import()
{
    var vm = new AddExcelViewModel(new[]
    {
        AddExcelRowSamples.Named("DM PCCC"),
        AddExcelRowSamples.Named("DM PCCC")
    });
    Assert.Contains(vm.Validate(), x => x.Code == "DUPLICATE_VIEW_NAME");
}

[Fact]
public void Revit_table_over_5000_cells_requires_confirmation()
{
    var estimate = ImportSizeEstimator.Estimate(rows: 101, columns: 50, ImportMode.RevitTable);
    Assert.True(estimate.RequiresConfirmation);
    Assert.Equal(5050, estimate.CellCount);
    Assert.True(estimate.EstimatedElementCount > estimate.CellCount);
}
```

- [ ] **Step 2: Chạy test để xác nhận fail**

Run: `dotnet test tests/TungDiroot.Presentation.Tests -c Release`  
Expected: FAIL vì test project/view-model chưa tồn tại.

- [ ] **Step 3: Cài UI và executor**

```csharp
public IReadOnlyList<ImportRowResult> Execute(
    UIApplication uiApplication,
    IReadOnlyList<WorkbookImportRequest> requests)
{
    var results = new List<ImportRowResult>();
    foreach (var request in requests)
        results.Add(ExecuteOneInOwnTransaction(uiApplication, request));
    return results;
}
```

`AddExcelWindow` có ba khu vực Nguồn, Worksheet/Vùng, Tạo view; DataGrid cho phép tick nhiều dòng và override từng row. UI hiển thị engine ảnh thực tế, số hàng/cột, số element ước tính và hộp xác nhận khi Bảng Revit vượt 5.000 ô. Executor parse mỗi workbook một lần, chạy một transaction cho mỗi row, catch exception thành `ImportRowResult`, rollback row lỗi và tiếp tục. Kết quả cuối hiển thị Thành công/Bỏ qua/Lỗi cùng warning renderer/font/formula.

- [ ] **Step 4: Chạy plan 2 verification**

Run: `powershell -ExecutionPolicy Bypass -File tools/Test-Plan2.ps1 -RevitVersion 2024`  
Expected: core/rendering/UI logic tests PASS, add-in build PASS, assembly contract PASS.

Manual Revit 2024 smoke test theo `docs/testing/plan2-revit-smoke.md`: tạo 2 Drafting, duplicate 1 Legend mẫu, tạo 1 Schedule header, nhập ảnh 300 DPI, xác nhận `Ảnh + Schedule` bị chặn.

- [ ] **Step 5: Commit và checkpoint plan 2**

```powershell
git add TUNG_DIROOT.sln src/TungDiroot.Presentation src/TungDiroot.Addin tests/TungDiroot.Presentation.Tests tools/Test-Plan2.ps1 docs/testing/plan2-revit-smoke.md
git commit -m "feat: import Excel ranges into Revit views"
git status --short
```

Expected: working tree sạch và add-in import thủ công hoạt động trên Revit 2024.
