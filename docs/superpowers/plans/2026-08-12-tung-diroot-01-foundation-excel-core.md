# TUNG_DIROOT Foundation and Excel Core Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Tạo nền tảng build và thư viện core có thể đọc `.xlsx/.xlsm` thành mô hình bảng trung gian mà không cần Revit hoặc Microsoft Excel.

**Architecture:** `TungDiroot.Core` target `netstandard2.0`, giữ toàn bộ domain model và Open XML parsing không phụ thuộc UI/Revit. `TungDiroot.Core.Tests` target `net8.0`, tự tạo workbook fixture trong thư mục test để kiểm thử giá trị, style, merge, range, fingerprint và đường dẫn.

**Tech Stack:** C#, .NET Standard 2.0, .NET 8, DocumentFormat.OpenXml 3.3.0, xUnit 2.9.2, xunit.runner.visualstudio 2.8.2, Microsoft.NET.Test.Sdk 17.11.1, PowerShell.

## Global Constraints

- Hỗ trợ `.xlsx` và `.xlsm`; không chạy VBA và không hỗ trợ `.xls`.
- Core không được tham chiếu Autodesk Revit API, WPF hoặc Microsoft Office Interop.
- File nguồn luôn mở read-only với `FileShare.ReadWrite | FileShare.Delete`.
- Giá trị công thức lấy cached value; không tự tính công thức.
- Mọi public model dùng immutable record/class hoặc read-only collection.
- Test không phụ thuộc file nằm ngoài repository.

---

### Task 1: Solution, package pins và test contract đầu tiên

**Files:**
- Create: `TUNG_DIROOT.sln`
- Create: `Directory.Build.props`
- Create: `Directory.Packages.props`
- Create: `src/TungDiroot.Core/TungDiroot.Core.csproj`
- Create: `src/TungDiroot.Core/ProductInfo.cs`
- Create: `src/TungDiroot.Core/Compatibility/IsExternalInit.cs`
- Create: `tests/TungDiroot.Core.Tests/TungDiroot.Core.Tests.csproj`
- Create: `tests/TungDiroot.Core.Tests/ProductInfoTests.cs`
- Create: `tools/Test-Core.ps1`

**Interfaces:**
- Produces: `TungDiroot.Core.ProductInfo.Name`, `ProductInfo.SchemaVersion`.

- [ ] **Step 1: Tạo test project trước và viết test contract**

```csharp
using Xunit;

namespace TungDiroot.Core.Tests;

public sealed class ProductInfoTests
{
    [Fact]
    public void Product_identity_is_stable()
    {
        Assert.Equal("TUNG_DIROOT", ProductInfo.Name);
        Assert.Equal(1, ProductInfo.SchemaVersion);
    }
}
```

- [ ] **Step 2: Chạy test để xác nhận fail vì chưa có core project/type**

Run: `dotnet test tests/TungDiroot.Core.Tests/TungDiroot.Core.Tests.csproj -c Release`  
Expected: FAIL vì `TungDiroot.Core` hoặc `ProductInfo` chưa tồn tại.

- [ ] **Step 3: Tạo solution, central package versions và implementation tối thiểu**

```csharp
namespace TungDiroot.Core;

public static class ProductInfo
{
    public const string Name = "TUNG_DIROOT";
    public const int SchemaVersion = 1;
}
```

`Directory.Build.props` bật nullable, deterministic build và warnings-as-errors cho project core/test. `Directory.Packages.props` pin đúng các package trong Tech Stack. `tools/Test-Core.ps1` chạy restore, build và test Release.
`Compatibility/IsExternalInit.cs` chỉ compile cho `netstandard2.0` để record types hoạt động mà không kéo thêm runtime dependency.

- [ ] **Step 4: Chạy toàn bộ core test**

Run: `powershell -ExecutionPolicy Bypass -File tools/Test-Core.ps1`  
Expected: restore/build/test PASS, không warning.

- [ ] **Step 5: Commit**

```powershell
git add TUNG_DIROOT.sln Directory.Build.props Directory.Packages.props src tests tools/Test-Core.ps1
git commit -m "build: initialize TUNG_DIROOT core solution"
```

### Task 2: Domain model và Excel range parser

**Files:**
- Create: `src/TungDiroot.Core/Excel/ExcelCellAddress.cs`
- Create: `src/TungDiroot.Core/Excel/ExcelRangeReference.cs`
- Create: `src/TungDiroot.Core/Import/ImportEnums.cs`
- Create: `src/TungDiroot.Core/Import/WorkbookImportRequest.cs`
- Test: `tests/TungDiroot.Core.Tests/Excel/ExcelRangeReferenceTests.cs`
- Test: `tests/TungDiroot.Core.Tests/Import/WorkbookImportRequestTests.cs`

**Interfaces:**
- Produces: `ExcelCellAddress.Parse(string)`, `ExcelRangeReference.Parse(string)`, `WorkbookImportRequest`.

- [ ] **Step 1: Viết test địa chỉ/range và validation**

```csharp
[Theory]
[InlineData("A1", 1, 1)]
[InlineData("$BC$27", 55, 27)]
public void Parse_cell_address(string text, int column, int row)
{
    var value = ExcelCellAddress.Parse(text);
    Assert.Equal(column, value.Column);
    Assert.Equal(row, value.Row);
}

[Fact]
public void Normalize_reversed_range()
{
    var range = ExcelRangeReference.Parse("H25:A1");
    Assert.Equal("A1:H25", range.ToA1());
}
```

- [ ] **Step 2: Chạy test và xác nhận fail do type chưa tồn tại**

Run: `dotnet test tests/TungDiroot.Core.Tests -c Release --filter "ExcelRangeReferenceTests|WorkbookImportRequestTests"`  
Expected: FAIL compile.

- [ ] **Step 3: Cài đặt parser và request model**

```csharp
public enum ImportMode { RevitTable, Image }
public enum TargetViewKind { Drafting, Legend, Schedule }
public enum UpdateBehavior { Recreate, DataOnly }
public enum ImageDpi { Dpi150 = 150, Dpi300 = 300, Dpi600 = 600 }

public sealed record WorkbookImportRequest(
    string FilePath,
    string WorksheetName,
    ExcelRangeReference Range,
    ImportMode Mode,
    TargetViewKind TargetView,
    UpdateBehavior UpdateBehavior,
    ImageDpi Dpi,
    bool IncludeHiddenRowsAndColumns,
    bool BlackAndWhite);
```

Parser loại `$`, chuyển tên cột base-26, reject row/column nhỏ hơn 1 và chuẩn hóa hai đầu range.

- [ ] **Step 4: Chạy test**

Run: `powershell -ExecutionPolicy Bypass -File tools/Test-Core.ps1`  
Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/TungDiroot.Core/Excel src/TungDiroot.Core/Import tests/TungDiroot.Core.Tests
git commit -m "feat: add Excel range and import domain models"
```

### Task 3: Workbook fixture và catalog reader

**Files:**
- Create: `src/TungDiroot.Core/Excel/IWorkbookReader.cs`
- Create: `src/TungDiroot.Core/Excel/WorkbookCatalog.cs`
- Create: `src/TungDiroot.Core/Excel/OpenXmlWorkbookReader.cs`
- Create: `tests/TungDiroot.Core.Tests/Fixtures/TestWorkbookFactory.cs`
- Test: `tests/TungDiroot.Core.Tests/Excel/OpenXmlWorkbookCatalogTests.cs`

**Interfaces:**
- Produces: `IWorkbookReader.ReadCatalog(string path)`, `WorkbookCatalog`, `WorksheetCatalog`, `WorkbookRegion`.

- [ ] **Step 1: Tạo workbook fixture trong test và viết test catalog**

```csharp
[Fact]
public void Catalog_lists_sheets_print_area_named_range_and_used_range()
{
    using var fixture = TestWorkbookFactory.CreateCatalogWorkbook();
    var catalog = new OpenXmlWorkbookReader().ReadCatalog(fixture.Path);

    Assert.Equal(new[] { "PCCC", "Thiết bị" }, catalog.Worksheets.Select(x => x.Name));
    Assert.Contains(catalog.Regions, x => x.Kind == RegionKind.PrintArea && x.Reference.ToA1() == "A1:H25");
    Assert.Contains(catalog.Regions, x => x.Name == "Equipment" && x.Reference.ToA1() == "A2:F18");
}
```

- [ ] **Step 2: Chạy test để thấy fail**

Run: `dotnet test tests/TungDiroot.Core.Tests -c Release --filter OpenXmlWorkbookCatalogTests`  
Expected: FAIL compile vì reader/model chưa tồn tại.

- [ ] **Step 3: Cài đặt catalog reader bằng SpreadsheetDocument**

```csharp
public interface IWorkbookReader
{
    WorkbookCatalog ReadCatalog(string path);
}
```

`ReadCatalog` mở stream read-only/share, map relationship worksheet, parse defined names `_xlnm.Print_Area`, Named Range và tính Used Range từ `SheetDimension` hoặc cell references. Macro part không được thực thi hoặc thay đổi.

- [ ] **Step 4: Chạy test catalog và test toàn bộ**

Run: `powershell -ExecutionPolicy Bypass -File tools/Test-Core.ps1`  
Expected: PASS với fixture `.xlsx` và `.xlsm`.

- [ ] **Step 5: Commit**

```powershell
git add src/TungDiroot.Core/Excel tests/TungDiroot.Core.Tests
git commit -m "feat: read Excel workbook catalog without Office"
```

### Task 4: Range values, styles, merges và hidden dimensions

**Files:**
- Create: `src/TungDiroot.Core/Excel/WorkbookRangeModel.cs`
- Create: `src/TungDiroot.Core/Excel/CellModel.cs`
- Create: `src/TungDiroot.Core/Excel/CellStyleModel.cs`
- Create: `src/TungDiroot.Core/Excel/WorkbookDrawingModel.cs`
- Create: `src/TungDiroot.Core/Excel/OpenXmlValueFormatter.cs`
- Create: `src/TungDiroot.Core/Excel/OpenXmlStyleReader.cs`
- Modify: `src/TungDiroot.Core/Excel/IWorkbookReader.cs`
- Modify: `src/TungDiroot.Core/Excel/OpenXmlWorkbookReader.cs`
- Modify: `tests/TungDiroot.Core.Tests/Fixtures/TestWorkbookFactory.cs`
- Test: `tests/TungDiroot.Core.Tests/Excel/OpenXmlWorkbookRangeTests.cs`

**Interfaces:**
- Consumes: `ExcelRangeReference` và catalog reader từ Task 2–3.
- Produces: `IWorkbookReader.ReadRange(string path, string worksheetName, ExcelRangeReference range, RangeReadOptions options)`, `WorkbookRangeModel.Rows`, `.Columns`, `.Cells`, `.MergedRanges`, `.Warnings`.

- [ ] **Step 1: Viết test mô hình trung gian**

```csharp
[Fact]
public void ReadRange_preserves_display_data_and_layout()
{
    using var fixture = TestWorkbookFactory.CreateFormattedWorkbook();
    var model = new OpenXmlWorkbookReader().ReadRange(
        fixture.Path, "PCCC", ExcelRangeReference.Parse("A1:D6"), RangeReadOptions.Default);

    Assert.Equal("DANH MỤC BẢN VẼ", model.Cell("A1").DisplayText);
    Assert.True(model.Cell("A1").Style.Font.Bold);
    Assert.Equal("A1:D1", Assert.Single(model.MergedRanges).ToA1());
    Assert.Equal(25.0, model.Rows[0].HeightPoints, 1);
    Assert.True(model.Columns[2].IsHidden);
}
```

- [ ] **Step 2: Chạy test để xác nhận fail**

Run: `dotnet test tests/TungDiroot.Core.Tests -c Release --filter OpenXmlWorkbookRangeTests`  
Expected: FAIL compile.

- [ ] **Step 3: Cài đặt value/style mapping**

```csharp
public sealed record CellModel(
    ExcelCellAddress Address,
    string DisplayText,
    string? Formula,
    CellStyleModel Style,
    bool HasCachedFormulaValue);

public sealed record CellStyleModel(
    FontModel Font,
    FillModel Fill,
    BorderModel Border,
    HorizontalAlignment Horizontal,
    VerticalAlignment Vertical,
    double RotationDegrees,
    string NumberFormatCode);
```

Reader resolve shared strings, inline strings, boolean, number/date/percent, style index, merged cells, widths/heights và hidden flags. Formula không có cached value tạo warning chứa địa chỉ ô và để `DisplayText` rỗng thay vì đoán.
Drawing reader map image relationship và bytes vào `WorkbookDrawingModel`; chart/shape/SmartArt chỉ lưu loại, anchor, relationship/cache preview nếu tồn tại và warning code khi không có preview.

- [ ] **Step 4: Chạy test**

Run: `powershell -ExecutionPolicy Bypass -File tools/Test-Core.ps1`  
Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/TungDiroot.Core/Excel tests/TungDiroot.Core.Tests
git commit -m "feat: preserve Excel values styles and merged cells"
```

### Task 5: Source fingerprint, change detection và path resolver

**Files:**
- Create: `src/TungDiroot.Core/Links/SourceFingerprint.cs`
- Create: `src/TungDiroot.Core/Links/SourceChangeDetector.cs`
- Create: `src/TungDiroot.Core/Links/LinkPathResolver.cs`
- Test: `tests/TungDiroot.Core.Tests/Links/SourceChangeDetectorTests.cs`
- Test: `tests/TungDiroot.Core.Tests/Links/LinkPathResolverTests.cs`

**Interfaces:**
- Produces: `SourceFingerprint.Create`, `SourceChangeDetector.Compare`, `LinkPathResolver.ToStoredPath`, `ResolveStoredPath`.

- [ ] **Step 1: Viết test fingerprint/path**

```csharp
[Fact]
public void Relative_path_round_trips_against_saved_model_folder()
{
    var stored = LinkPathResolver.ToStoredPath(@"C:\BIM\Data\A.xlsx", @"C:\BIM\Model.rvt", preferRelative: true);
    Assert.Equal(@"Data\A.xlsx", stored.Value);
    Assert.Equal(PathMode.Relative, stored.Mode);
    Assert.Equal(@"C:\BIM\Data\A.xlsx", LinkPathResolver.ResolveStoredPath(stored, @"C:\BIM\Model.rvt"));
}

[Fact]
public void Same_metadata_and_hash_is_unchanged()
{
    var result = SourceChangeDetector.Compare(SourceFingerprint.Parse("10:42:ABCD"), SourceFingerprint.Parse("10:42:ABCD"));
    Assert.Equal(SourceChange.Unchanged, result);
}
```

- [ ] **Step 2: Chạy test để xác nhận fail**

Run: `dotnet test tests/TungDiroot.Core.Tests -c Release --filter "SourceChangeDetectorTests|LinkPathResolverTests"`  
Expected: FAIL compile.

- [ ] **Step 3: Cài đặt SHA-256 theo vùng liên quan và quy tắc path**

```csharp
public sealed record SourceFingerprint(long Length, long LastWriteUtcTicks, string Sha256Hex);

public static class SourceChangeDetector
{
    public static SourceChange Compare(SourceFingerprint before, SourceFingerprint after) =>
        before == after ? SourceChange.Unchanged : SourceChange.Changed;
}

public static class LinkPathResolver
{
    public static StoredPath ToStoredPath(string sourcePath, string? modelPath, bool preferRelative);
    public static string ResolveStoredPath(StoredPath stored, string? modelPath);
}
```

Fingerprint serialization: `<length>:<lastWriteUtcTicks>:<sha256>`. Hash được tính từ worksheet name, normalized range, cell display values, style keys, merge ranges và drawing relationship IDs của vùng; không hash phần workbook không liên quan. Relative path chỉ dùng khi model có đường dẫn local ổn định; nếu không trả `PathMode.Absolute` cùng warning code `MODEL_PATH_UNAVAILABLE`.

- [ ] **Step 4: Chạy toàn bộ test và build deterministic hai lần**

Run: `powershell -ExecutionPolicy Bypass -File tools/Test-Core.ps1`  
Expected: PASS; hai build liên tiếp tạo cùng assembly hash.

- [ ] **Step 5: Commit và checkpoint plan 1**

```powershell
git add src/TungDiroot.Core/Links tests/TungDiroot.Core.Tests
git commit -m "feat: detect source changes and resolve link paths"
git status --short
```

Expected: working tree sạch. Exit criteria: test fixture `.xlsx/.xlsm` được đọc đầy đủ bằng core trên máy không có Excel/Revit process.
