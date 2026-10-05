# TUNG_DIROOT 1.3.0 RC1 — Revit 2024

Build time: 2026-10-02 10:37 (Asia/Saigon)  
Source commit: `9d71ce934d8649a2a0689c91564f49e51bb6e8a1`  
Scope: Revit 2024 only

## Automated verification

- Clean build: PASS, 0 warnings / 0 errors.
- Core tests: 317 PASS.
- Presentation tests: 60 PASS.
- Rendering tests: 10 PASS.
- Responsive WPF window regression: PASS, gồm FamilyReviser ở 1420×860 và 800×560.
- Revit 2024 runtime dependency compatibility: PASS.
- Add-in contracts: PASS cho TableGen, DataLink, Filter, PDF/Word Link, Bulk V/G, SheetLink/SheetGen, ReOrdering, ParaManager và FamilyReviser.
- Extensible Storage schema contract: PASS.
- Release layout và SHA-256 manifest: PASS.
- Portable install, install-over và uninstall trong sandbox: PASS.
- Revit-2024-only installer contract: PASS.

## Artifacts

- `dist/TUNG_DIROOT-Setup-Revit2024.exe` — 3,604,957 bytes  
  SHA-256: `D255DB846257DC9259A8C7F3899FDA1D8680035CEB4D6A736BCBD0BA84A2DBFC`
- `dist/TUNG_DIROOT-Portable-Revit2024.zip` — 2,354,349 bytes  
  SHA-256: `0EACCB647B3A8B79B8310CFCE3B6151252FF7ED9F6ECAFD57062A49A7C607439`
- `dist/SHA256SUMS.txt` chứa hai hash trên.

## Trạng thái phát hành

Đây là release candidate vì checklist thao tác trực tiếp trong Revit 2024 vẫn `NOT RUN`. Không cài đè môi trường Revit thật trong quy trình build. Sau khi người dùng chạy các checklist tương tác chính và xác nhận, có thể đổi release matrix sang PASS rồi dựng lại artifact chính thức từ cùng source.
