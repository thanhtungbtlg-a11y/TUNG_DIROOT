# TUNG_DIROOT Packaging and Release Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build, cài đặt, kiểm thử và bàn giao `TUNG_DIROOT 1.0.0` cho Revit 2024, 2025 và 2026 bằng một setup EXE và một portable ZIP.

**Architecture:** Build matrix resolve Revit API theo từng máy/SDK nhưng không copy API DLL vào release. Staging folder tách binary theo version; manifest được sinh với path thực tại lúc cài. Inno Setup đóng gói per-user installer, còn portable dùng PowerShell installer cùng manifest generator.

**Tech Stack:** PowerShell 7/Windows PowerShell 5.1 compatible scripts, dotnet/MSBuild, Inno Setup 6, Revit 2024–2026, xUnit.

## Global Constraints

- Release version đầu: `1.0.0`.
- Artifact bắt buộc: `TUNG_DIROOT-Setup-Revit2024-2026.exe` và `TUNG_DIROOT-Portable-Revit2024-2026.zip`.
- Không đóng gói RevitAPI.dll, RevitAPIUI.dll, Microsoft Office hoặc DiRoots/TungMEP binaries.
- Setup mặc định per-user và không cần administrator.
- Cài đè 1.0.0 hoặc bản mới hơn không yêu cầu gỡ trước.
- Uninstall không xóa metadata trong RVT và không xóa file Excel.
- Không phát hành nếu một trong ba build 2024/2025/2026 hoặc test contract thất bại.

---

### Task 1: Versioned build matrix và release staging

**Files:**
- Create: `build/version.json`
- Create: `build/Build-Matrix.ps1`
- Create: `build/Stage-Release.ps1`
- Create: `build/RevitApiPaths.example.json`
- Create: `.gitignore`
- Modify: `Directory.Build.props`
- Modify: `src/TungDiroot.Addin/TungDiroot.Addin.csproj`
- Create: `tools/Test-ReleaseLayout.ps1`

**Interfaces:**
- Produces: `artifacts/staging/TUNG_DIROOT/{2024,2025,2026}` và release metadata JSON.

- [ ] **Step 1: Viết failing release-layout test**

```powershell
$years = 2024, 2025, 2026
foreach ($year in $years) {
    $dir = Join-Path $StageRoot $year
    if (-not (Test-Path (Join-Path $dir "TUNG_DIROOT.Revit$year.dll"))) { throw "Missing add-in $year" }
    if (Get-ChildItem $dir -Filter 'RevitAPI*.dll') { throw "Revit API must not ship: $year" }
}
$metadata = Get-Content (Join-Path $StageRoot 'release.json') -Raw | ConvertFrom-Json
if ($metadata.version -ne '1.0.0') { throw 'Unexpected release version' }
```

- [ ] **Step 2: Chạy test để xác nhận fail**

Run: `powershell -ExecutionPolicy Bypass -File tools/Test-ReleaseLayout.ps1 -StageRoot artifacts/staging/TUNG_DIROOT`  
Expected: FAIL vì staging chưa tồn tại.

- [ ] **Step 3: Cài build/staging scripts**

```powershell
foreach ($year in 2024, 2025, 2026) {
    $api = & $PSScriptRoot/Resolve-RevitApi.ps1 -RevitVersion $year -ApiPathsFile $ApiPathsFile
    & dotnet build src/TungDiroot.Addin/TungDiroot.Addin.csproj -c Release `
        -p:RevitVersion=$year -p:RevitApiDir=$api
    if ($LASTEXITCODE -ne 0) { throw "Build failed for Revit $year" }
}
```

`Build-Matrix.ps1` lặp 2024/2025/2026, gọi `Resolve-RevitApi.ps1`, build Release và dừng ngay khi thiếu reference. `Stage-Release.ps1` copy main DLL, dependency, `.deps.json` nếu có, embedded resource output và license notices; reject filename khớp `^RevitAPI(UI)?\.dll$`. `release.json` ghi version, UTC build time, git commit và SHA-256 từng file.

- [ ] **Step 4: Build/stage/test cả ba version**

Run: `powershell -ExecutionPolicy Bypass -File build/Build-Matrix.ps1 -Configuration Release -ApiPathsFile build/RevitApiPaths.local.json`  
Run: `powershell -ExecutionPolicy Bypass -File build/Stage-Release.ps1 -Version 1.0.0`  
Run: `powershell -ExecutionPolicy Bypass -File tools/Test-ReleaseLayout.ps1 -StageRoot artifacts/staging/TUNG_DIROOT`  
Expected: PASS cho 2024/2025/2026. Nếu máy thiếu API path của một year, task dừng ở đây và không tạo artifact phát hành.

- [ ] **Step 5: Commit**

```powershell
git add .gitignore Directory.Build.props src/TungDiroot.Addin/TungDiroot.Addin.csproj build tools/Test-ReleaseLayout.ps1
git commit -m "build: stage Revit 2024 to 2026 release matrix"
```

### Task 2: Manifest generator, portable installer và Inno Setup

**Files:**
- Create: `installer/New-RevitManifest.ps1`
- Create: `installer/Install-TUNG_DIROOT.ps1`
- Create: `installer/Uninstall-TUNG_DIROOT.ps1`
- Create: `installer/Install-TUNG_DIROOT.cmd`
- Create: `installer/Uninstall-TUNG_DIROOT.cmd`
- Create: `installer/TUNG_DIROOT.iss`
- Create: `installer/THIRD-PARTY-NOTICES.txt`
- Create: `tools/Test-Manifest.ps1`
- Create: `tools/Test-PortableInstall.ps1`

**Interfaces:**
- Produces: manifest application class `TungDiroot.Addin.Application`, AddInId `8A93D06F-E03B-4A7C-9A96-DF5A3D5B0E24`, VendorId `TUNG`.

- [ ] **Step 1: Viết manifest/install contract tests**

```powershell
[xml]$manifest = Get-Content $ManifestPath
$addin = $manifest.RevitAddIns.AddIn
if ($addin.Type -ne 'Application') { throw 'Manifest type must be Application' }
if ($addin.FullClassName -ne 'TungDiroot.Addin.Application') { throw 'Wrong application class' }
if ($addin.AddInId -ne '8A93D06F-E03B-4A7C-9A96-DF5A3D5B0E24') { throw 'Wrong AddInId' }
if (-not (Test-Path $addin.Assembly)) { throw 'Assembly path does not exist' }
```

- [ ] **Step 2: Chạy test để xác nhận fail**

Run: `powershell -ExecutionPolicy Bypass -File tools/Test-Manifest.ps1 -RevitVersion 2024 -ManifestPath artifacts/test-install/2024/TUNG_DIROOT.addin`  
Expected: FAIL vì manifest chưa tồn tại.

- [ ] **Step 3: Cài installer scripts và Inno definition**

```powershell
function New-RevitManifest([int]$Year, [string]$AssemblyPath, [string]$OutputPath) {
    $xml = @"
<RevitAddIns><AddIn Type="Application"><Name>TUNG_DIROOT</Name><Assembly>$AssemblyPath</Assembly><AddInId>8A93D06F-E03B-4A7C-9A96-DF5A3D5B0E24</AddInId><FullClassName>TungDiroot.Addin.Application</FullClassName><VendorId>TUNG</VendorId><VendorDescription>TUNG_DIROOT</VendorDescription></AddIn></RevitAddIns>
"@
    [IO.File]::WriteAllText($OutputPath, $xml, [Text.UTF8Encoding]::new($false))
}
```

Portable installer copy version folder vào `%LocalAppData%\TUNG_DIROOT\1.0.0\<year>`, sinh manifest vào `%AppData%\Autodesk\Revit\Addins\<year>\TUNG_DIROOT.addin` với absolute assembly path. Uninstaller chỉ xóa manifest/binary do installer quản lý và giữ `%LocalAppData%\TUNG_DIROOT\Logs/settings.json` theo lựa chọn người dùng; không mở/chỉnh RVT. Inno Setup gọi cùng logic manifest trong `[Code]`, dùng AppId cố định và upgrade existing install.

- [ ] **Step 4: Cài thử vào sandbox path và kiểm tra uninstall**

Run: `powershell -ExecutionPolicy Bypass -File tools/Test-PortableInstall.ps1 -StageRoot artifacts/staging/TUNG_DIROOT -SandboxRoot artifacts/install-sandbox`  
Expected: ba manifest hợp lệ, assembly path tồn tại, uninstall xóa program/manifest nhưng giữ file sentinel `UserData\settings.json`.

- [ ] **Step 5: Commit**

```powershell
git add installer tools/Test-Manifest.ps1 tools/Test-PortableInstall.ps1
git commit -m "feat: package per-user setup and portable installer"
```

### Task 3: Test fixtures, automated suite và Revit release matrix

**Files:**
- Create: `samples/Create-TUNG_DIROOT-TestWorkbook.ps1`
- Create: `samples/README.md`
- Create: `tools/TungDiroot.SampleWorkbook/TungDiroot.SampleWorkbook.csproj`
- Create: `tools/TungDiroot.SampleWorkbook/Program.cs`
- Create: `tools/TungDiroot.CatalogProbe/TungDiroot.CatalogProbe.csproj`
- Create: `tools/TungDiroot.CatalogProbe/Program.cs`
- Create: `docs/testing/release-matrix.md`
- Create: `docs/testing/revit-2024-checklist.md`
- Create: `docs/testing/revit-2025-checklist.md`
- Create: `docs/testing/revit-2026-checklist.md`
- Create: `tools/Test-All.ps1`
- Modify: `tests/TungDiroot.Core.Tests/Fixtures/TestWorkbookFactory.cs`
- Modify: `tests/TungDiroot.Rendering.Tests/BuiltInWorkbookImageRendererTests.cs`
- Modify: `TUNG_DIROOT.sln`

**Interfaces:**
- Produces: deterministic sample workbook and one-command automated test suite.

- [ ] **Step 1: Viết failing sample contract test trong Test-All**

```powershell
$requiredSheets = 'PCCC','Thiết bị','Định dạng','Đồ họa','Công thức'
$catalogJson = & dotnet run --project tools/TungDiroot.CatalogProbe -- $SampleWorkbook
$catalog = $catalogJson | ConvertFrom-Json
foreach ($sheet in $requiredSheets) {
    if ($catalog.worksheets.name -notcontains $sheet) { throw "Missing sample sheet: $sheet" }
}
```

- [ ] **Step 2: Chạy Test-All để xác nhận fail**

Run: `powershell -ExecutionPolicy Bypass -File tools/Test-All.ps1 -SkipRevitBuild`  
Expected: FAIL vì sample/probe chưa tồn tại.

- [ ] **Step 3: Tạo deterministic workbook/probe/checklists**

```csharp
var output = args.Single();
using var document = SpreadsheetDocument.Create(output, SpreadsheetDocumentType.Workbook);
TestWorkbookBuilder.AddWorksheet(document, "PCCC", TestSheetData.Pccc());
TestWorkbookBuilder.AddWorksheet(document, "Thiết bị", TestSheetData.Equipment());
TestWorkbookBuilder.AddWorksheet(document, "Định dạng", TestSheetData.Formatting());
TestWorkbookBuilder.AddWorksheet(document, "Đồ họa", TestSheetData.Drawings());
TestWorkbookBuilder.AddWorksheet(document, "Công thức", TestSheetData.Formulas());
```

`Create-TUNG_DIROOT-TestWorkbook.ps1` gọi `TungDiroot.SampleWorkbook`, tạo workbook chứa merged headers, hidden row/column, Unicode tiếng Việt, formula có/không cached value, border/fill/font mapping, image, chart/shape marker, Named Range, Print Area và custom range lớn hơn 5.000 ô. `TungDiroot.CatalogProbe` đọc catalog qua Core và in JSON deterministic cho test script. Mỗi Revit checklist ghi chính xác: clean project, workshared project, Drafting/Legend seed/Schedule, Table/Image, 150/300/600 DPI, Refresh, Recreate, Data-only, Auto Sync, Relink, missing/locked source, pause/stop và preservation của user TextNote.
`Test-All.ps1` cũng fail nếu source code chứa `HttpClient`, `WebRequest`, `TcpClient`, `Socket` hoặc URL HTTP trong `src/`, bảo đảm bản 1.0.0 không có telemetry, kích hoạt hoặc kết nối mạng.

- [ ] **Step 4: Chạy automated suite và điền matrix từng version**

Run: `powershell -ExecutionPolicy Bypass -File samples/Create-TUNG_DIROOT-TestWorkbook.ps1 -OutputPath samples/TUNG_DIROOT-Test.xlsx`  
Run: `powershell -ExecutionPolicy Bypass -File tools/Test-All.ps1 -ApiPathsFile build/RevitApiPaths.local.json`  
Expected: unit/renderer/UI logic tests, build matrix, assembly contract, release-layout và portable-install tests PASS. Revit checklist phải ghi tester, Revit build number, thời gian và PASS/FAIL từng case; không ghi PASS cho version chưa chạy.

- [ ] **Step 5: Commit**

```powershell
git add TUNG_DIROOT.sln samples docs/testing tools/Test-All.ps1 tools/TungDiroot.SampleWorkbook tools/TungDiroot.CatalogProbe tests
git commit -m "test: add cross-version TableGen release matrix"
```

### Task 4: Tài liệu người dùng và troubleshooting

**Files:**
- Create: `README.md`
- Create: `docs/user-guide-vi.md`
- Create: `docs/install-guide-vi.md`
- Create: `docs/troubleshooting-vi.md`
- Create: `docs/architecture.md`
- Create: `docs/third-party-licenses.md`

**Interfaces:**
- Produces: tài liệu bàn giao tiếng Việt cho cài đặt, sử dụng và chẩn đoán.

- [ ] **Step 1: Viết documentation contract test vào Test-All**

```powershell
$required = 'README.md','docs/user-guide-vi.md','docs/install-guide-vi.md','docs/troubleshooting-vi.md','docs/architecture.md','docs/third-party-licenses.md'
foreach ($file in $required) {
    if (-not (Test-Path $file)) { throw "Missing documentation: $file" }
    if ((Get-Item $file).Length -lt 500) { throw "Documentation too short: $file" }
}
```

- [ ] **Step 2: Chạy documentation contract để xác nhận fail**

Run: `powershell -ExecutionPolicy Bypass -File tools/Test-All.ps1 -DocsOnly`  
Expected: FAIL vì tài liệu chưa tồn tại.

- [ ] **Step 3: Viết tài liệu theo hành vi đã kiểm chứng**

```markdown
# TUNG_DIROOT TableGen
## Cài đặt Revit 2024–2026
## Thêm Excel: Bảng Revit và Ảnh
## Drafting, Legend mẫu và Schedule header
## Refresh, Data Only, Recreate và Auto Sync
## Relink, đường dẫn và xử lý lỗi
```

User guide mô tả hai mode, ba view kinds, Legend mẫu, engine indicator, DPI, Update Data Only/Recreate, Auto Sync, Relative/Absolute path, Relink, pause/stop và cảnh báo 5.000 ô. Troubleshooting có mã lỗi ổn định và vị trí log; nêu rõ chart/SmartArt built-in limitation và cách dùng Excel High Fidelity. Install guide có setup, portable, upgrade tại chỗ và uninstall giữ metadata model.

- [ ] **Step 4: Chạy docs contract và kiểm tra link nội bộ**

Run: `powershell -ExecutionPolicy Bypass -File tools/Test-All.ps1 -DocsOnly`  
Expected: PASS; mọi relative Markdown link resolve tới file tồn tại.

- [ ] **Step 5: Commit**

```powershell
git add README.md docs tools/Test-All.ps1
git commit -m "docs: add TUNG_DIROOT install and user guides"
```

### Task 5: Build artifacts, cài thử và release checkpoint

**Files:**
- Create: `build/Build-Release.ps1`
- Create: `docs/testing/release-1.0.0-report.md`
- Output: `dist/TUNG_DIROOT-Setup-Revit2024-2026.exe`
- Output: `dist/TUNG_DIROOT-Portable-Revit2024-2026.zip`
- Output: `dist/SHA256SUMS.txt`

**Interfaces:**
- Produces: release artifacts 1.0.0 và báo cáo có thể truy vết đến git commit.

- [ ] **Step 1: Viết release gate trước khi tạo artifact**

```powershell
$allowedDirty = 'build/Build-Release.ps1','docs/testing/release-1.0.0-report.md'
$unexpectedDirty = git status --porcelain | Where-Object {
    $path = $_.Substring(3).Replace('\', '/')
    $allowedDirty -notcontains $path
}
if ($unexpectedDirty) { throw "Unexpected dirty paths before release: $unexpectedDirty" }
& powershell -ExecutionPolicy Bypass -File tools/Test-All.ps1 -ApiPathsFile build/RevitApiPaths.local.json
if ($LASTEXITCODE -ne 0) { throw 'Automated release gate failed' }
if (Select-String docs/testing/release-matrix.md -Pattern '\| (2024|2025|2026) \|.*\| FAIL \|') { throw 'Revit matrix contains failure' }
```

- [ ] **Step 2: Chạy release gate trước artifact**

Run: `powershell -ExecutionPolicy Bypass -File build/Build-Release.ps1 -Version 1.0.0 -VerifyOnly`  
Expected: PASS chỉ khi source sạch, ba build/test và matrix đều đạt.

- [ ] **Step 3: Tạo setup, portable ZIP và hash**

```powershell
& $PSScriptRoot/Build-Matrix.ps1 -Configuration Release -ApiPathsFile $ApiPathsFile
& $PSScriptRoot/Stage-Release.ps1 -Version $Version
& $IsccPath installer/TUNG_DIROOT.iss
Compress-Archive -Path artifacts/portable/TUNG_DIROOT/* -DestinationPath dist/TUNG_DIROOT-Portable-Revit2024-2026.zip -Force
Get-ChildItem dist -File | ForEach-Object { "$(Get-FileHash $_.FullName -Algorithm SHA256 | Select-Object -ExpandProperty Hash)  $($_.Name)" } | Set-Content dist/SHA256SUMS.txt
```

Script stage release, gọi Inno `ISCC.exe`, tạo portable tree gồm binaries, installer scripts, README và notices, rồi `Compress-Archive`. SHA-256 file dùng `Get-FileHash -Algorithm SHA256`; report ghi commit, dependency versions, Revit build numbers, artifact sizes/hashes và mọi warning được chấp nhận.

- [ ] **Step 4: Cài thử artifact cuối và kiểm chứng**

Run: `powershell -ExecutionPolicy Bypass -File build/Build-Release.ps1 -Version 1.0.0`  
Run setup silent trên test user hoặc sandbox, mở từng Revit 2024/2025/2026, xác nhận tab `TUNG_DIROOT`, chạy workbook mẫu, đóng Revit, chạy setup lần hai để kiểm tra upgrade, uninstall và xác nhận manifest/binary bị xóa nhưng RVT/link metadata không bị chỉnh.

Expected: hai artifact tồn tại, hash khớp, cả ba Revit load được add-in và checklist release report không có FAIL.

- [ ] **Step 5: Commit và tag release**

```powershell
git add build/Build-Release.ps1 docs/testing/release-1.0.0-report.md
git commit -m "release: prepare TUNG_DIROOT 1.0.0"
git tag -a v1.0.0 -m "TUNG_DIROOT 1.0.0"
git status --short
```

Expected: working tree sạch. `dist` có EXE, ZIP và SHA256SUMS; tag `v1.0.0` trỏ tới commit release.
