# TUNG_DIROOT Revit 2024-Only Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Chuyển toàn bộ release tooling và tài liệu hiện hành của TUNG_DIROOT 1.0.0 sang Revit 2024-only và tạo EXE/ZIP có SHA-256.

**Architecture:** Giữ nguyên Core, Rendering, Presentation và chức năng add-in đã kiểm thử. Thu hẹp boundary phát hành tại PowerShell/Inno Setup: mọi default year là 2024, release gate chỉ đọc dòng 2024, staging chỉ chứa payload 2024 và artifact có tên 2024-only. Một contract test văn bản bảo vệ khỏi việc 2025/2026 quay lại các file phát hành hiện hành.

**Tech Stack:** PowerShell 5.1-compatible scripts, Inno Setup 6, .NET Framework 4.8, Autodesk Revit API 2024, xUnit/.NET SDK 8 cho test logic.

## Global Constraints

- Target add-in duy nhất là Autodesk Revit 2024 trên .NET Framework 4.8.
- Không phân phối `RevitAPI.dll` hoặc `RevitAPIUI.dll`.
- Setup phải có tên `TUNG_DIROOT-Setup-Revit2024.exe`.
- Portable phải có tên `TUNG_DIROOT-Portable-Revit2024.zip`.
- Không tự đánh dấu checklist tương tác PASS và không tạo tag chính thức khi chưa chạy trực tiếp trong Revit 2024.
- Cài đè và gỡ cài đặt phải giữ nguyên ownership-marker safety và không sửa RVT/Excel.

---

### Task 1: Contract kiểm tra phạm vi 2024-only

**Files:**
- Create: `tools/Test-2024OnlyRelease.ps1`

**Interfaces:**
- Consumes: các file active release trong `build/`, `installer/`, `README.md`, `docs/architecture.md`, `docs/install-guide-vi.md`, `docs/testing/release-matrix.md`.
- Produces: script thoát mã 0 khi release chỉ nhắc payload/artifact 2024; throw khi phát hiện tên artifact `2024-2026`, payload/manifest `2025` hoặc `2026` trong active release files.

- [ ] **Step 1: Viết contract test đang thất bại**

```powershell
$active = @(
  'build/version.json', 'build/Build-Matrix.ps1', 'build/Stage-Release.ps1',
  'build/Build-Release.ps1', 'installer/TUNG_DIROOT.iss',
  'installer/Install-TUNG_DIROOT.ps1', 'installer/Uninstall-TUNG_DIROOT.ps1',
  'tools/Test-ReleaseLayout.ps1', 'tools/Test-PortableInstall.ps1',
  'README.md', 'docs/architecture.md', 'docs/install-guide-vi.md',
  'docs/testing/release-matrix.md'
)
$content = $active | ForEach-Object { Get-Content (Join-Path $repoRoot $_) -Raw }
if ($content -match '2024-2026|Revit2025|Revit2026|Addins\\2025|Addins\\2026') {
  throw 'Active release surface still contains a 2025/2026 artifact or manifest.'
}
if ((Get-Content (Join-Path $repoRoot 'build/version.json') -Raw | ConvertFrom-Json).revitVersions -ne 2024) {
  throw 'version.json must declare only Revit 2024.'
}
```

- [ ] **Step 2: Chạy để xác nhận RED**

Run: `powershell -ExecutionPolicy Bypass -File tools/Test-2024OnlyRelease.ps1`  
Expected: FAIL vì active release files vẫn chứa `2024-2026`, Revit 2025 hoặc Revit 2026.

- [ ] **Step 3: Commit riêng contract test RED**

```powershell
git add tools/Test-2024OnlyRelease.ps1
git commit -m "test: require Revit 2024-only release surface"
```

### Task 2: Thu hẹp build, staging và installer

**Files:**
- Modify: `build/version.json`
- Modify: `build/Build-Matrix.ps1`
- Modify: `build/Stage-Release.ps1`
- Modify: `build/Build-Release.ps1`
- Modify: `installer/TUNG_DIROOT.iss`
- Modify: `installer/Install-TUNG_DIROOT.ps1`
- Modify: `installer/Uninstall-TUNG_DIROOT.ps1`
- Modify: `tools/Test-ReleaseLayout.ps1`
- Modify: `tools/Test-PortableInstall.ps1`
- Modify: `tools/Test-All.ps1`

**Interfaces:**
- Consumes: `Build-Addin.ps1 -RevitVersion 2024`, staging at `artifacts/addin/2024`, release matrix row `| 2024 |`.
- Produces: release payload `artifacts/staging/TUNG_DIROOT/2024`, Setup/ZIP 2024-only và `SHA256SUMS.txt`.

- [ ] **Step 1: Đổi default matrix và metadata thành đúng một năm**

```powershell
[ValidateSet(2024)][int[]]$RevitVersions = @(2024)
```

Set `build/version.json` thành `"revitVersions": [2024]`. Trong `Build-Release.ps1`, chỉ kiểm tra dòng 2024 của release matrix.

- [ ] **Step 2: Thu hẹp Inno Setup**

Giữ duy nhất source/install-delete/run cho `2024`; đổi:

```ini
OutputBaseFilename=TUNG_DIROOT-Setup-Revit2024
```

- [ ] **Step 3: Đổi tên artifact và portable payload**

```powershell
$zip = Join-Path $dist 'TUNG_DIROOT-Portable-Revit2024.zip'
$setup = Join-Path $dist 'TUNG_DIROOT-Setup-Revit2024.exe'
```

Trước đóng gói, xóa an toàn ba artifact release đã biết trong `dist`; không xóa file ngoài danh sách đích. Copy ownership marker vào portable package.

- [ ] **Step 4: Chạy contract để xác nhận GREEN phần tooling**

Run: `powershell -ExecutionPolicy Bypass -File tools/Test-2024OnlyRelease.ps1`  
Expected: có thể vẫn FAIL chỉ vì tài liệu hiện hành chưa được cập nhật; không còn lỗi từ `build/` hoặc `installer/`.

- [ ] **Step 5: Chạy build/staging/portable tests**

```powershell
$env:TUNG_DIROOT_DOTNET='C:\Users\tungnt2303\Downloads\tungmep2\.dotnet\dotnet.exe'
.\build\Build-Matrix.ps1 -Configuration Release
.\build\Stage-Release.ps1 -Version 1.0.0
.\tools\Test-ReleaseLayout.ps1 -StageRoot .\artifacts\staging\TUNG_DIROOT
.\tools\Test-PortableInstall.ps1 -StageRoot .\artifacts\staging\TUNG_DIROOT -SandboxRoot .\artifacts\install-sandbox
```

Expected: PASS, chỉ báo Revit 2024.

- [ ] **Step 6: Commit tooling**

```powershell
git add build installer tools/Test-All.ps1 tools/Test-ReleaseLayout.ps1 tools/Test-PortableInstall.ps1
git commit -m "build: release TUNG_DIROOT for Revit 2024 only"
```

### Task 3: Đồng bộ tài liệu hỗ trợ hiện hành

**Files:**
- Modify: `README.md`
- Modify: `docs/architecture.md`
- Modify: `docs/install-guide-vi.md`
- Modify: `docs/testing/release-matrix.md`
- Modify: `docs/testing/release-1.0.0-report.md`

**Interfaces:**
- Consumes: artifact names và release behavior từ Task 2.
- Produces: tài liệu chỉ cam kết Revit 2024; release matrix có đúng một dòng 2024 và trạng thái interactive thực tế là NOT RUN.

- [ ] **Step 1: Cập nhật tài liệu**

Nêu rõ target `.NET Framework 4.8`, artifact 2024-only, install path `...\1.0.0\2024`, và không còn blocker 2025/2026. Không sửa các plan/spec lịch sử ngoài spec 2024-only.

- [ ] **Step 2: Chạy contract 2024-only**

Run: `powershell -ExecutionPolicy Bypass -File tools/Test-2024OnlyRelease.ps1`  
Expected: PASS.

- [ ] **Step 3: Chạy contract tài liệu**

Run: `powershell -ExecutionPolicy Bypass -File tools/Test-All.ps1 -DocsOnly`  
Expected: PASS, không link gãy.

- [ ] **Step 4: Commit tài liệu**

```powershell
git add README.md docs/architecture.md docs/install-guide-vi.md docs/testing
git commit -m "docs: support Revit 2024 only"
```

### Task 4: Tạo và xác minh release candidate 2024

**Files:**
- Generated: `dist/TUNG_DIROOT-Setup-Revit2024.exe`
- Generated: `dist/TUNG_DIROOT-Portable-Revit2024.zip`
- Generated: `dist/SHA256SUMS.txt`
- Modify: `docs/testing/release-1.0.0-report.md` nếu cần ghi hash/kết quả cuối.

**Interfaces:**
- Consumes: clean Git worktree, Revit API 2024 thật, Inno Setup 6.
- Produces: ba artifact bàn giao và bằng chứng test cuối.

- [ ] **Step 1: Chạy toàn bộ automated gate trên cây sạch**

Run: `powershell -ExecutionPolicy Bypass -File tools/Test-All.ps1`  
Expected: 140 Core + 9 Rendering + 7 Presentation PASS; Revit 2024 build/contract, staging và portable PASS.

- [ ] **Step 2: Tạo artifact 2024**

Run: `powershell -ExecutionPolicy Bypass -File build/Build-Release.ps1 -Version 1.0.0`  
Expected: tạo đúng EXE, ZIP và SHA256SUMS; không tạo tên `2024-2026`.

- [ ] **Step 3: Kiểm tra nội dung artifact**

```powershell
Get-ChildItem dist -File
Get-Content dist\SHA256SUMS.txt
Get-FileHash dist\TUNG_DIROOT-Setup-Revit2024.exe -Algorithm SHA256
Get-FileHash dist\TUNG_DIROOT-Portable-Revit2024.zip -Algorithm SHA256
```

Giải nén ZIP vào sandbox và xác nhận chỉ có `TUNG_DIROOT\2024`, không có `2025`, `2026` hoặc Revit API DLL.

- [ ] **Step 4: Chạy install/uninstall từ portable artifact đã tạo**

Run portable installer vào sandbox, chạy install-over, kiểm tra manifest 2024, rồi uninstall và xác nhận settings sentinel còn nguyên.

- [ ] **Step 5: Rà soát cuối và commit báo cáo**

```powershell
git diff --check
git status --short
git tag --list v1.0.0
```

Không tạo tag `v1.0.0`. Báo cáo ghi artifact là release candidate vì checklist Revit 2024 vẫn NOT RUN.
