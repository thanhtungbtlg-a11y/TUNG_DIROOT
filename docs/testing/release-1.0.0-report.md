# TUNG_DIROOT 1.0.0 release report

Status: **REVIT 2024 RELEASE CANDIDATE — INTERACTIVE CHECKLIST NOT RUN**.

Ngày đánh giá: 2026-08-12. Branch triển khai: `feature/tablegen-implementation`.

## Kết quả tự động

- Core: 140 tests PASS.
- Rendering: 9 tests PASS.
- Presentation: 7 tests PASS.
- Revit 2024 add-in build Release: PASS, không warning/error.
- Revit 2024 assembly contract: PASS; payload không chứa RevitAPI.dll/RevitAPIUI.dll.
- Staging, metadata/hash, manifest và portable install/install-over/uninstall sandbox cho 2024: PASS.
- Contract 2024-only xác nhận staging không có 2025/2026 và Inno Setup tạo đúng tên EXE: PASS.
- Workbook mẫu deterministic và audit không network/telemetry: PASS.

## Phạm vi phát hành

TUNG_DIROOT 1.0.0 chỉ hỗ trợ Autodesk Revit 2024 trên .NET Framework 4.8. Revit 2025 và 2026 không còn là blocker vì đã được loại khỏi phạm vi sản phẩm và không có payload/manifest tương ứng.

Artifact release candidate đã được tạo bởi release gate:

- `TUNG_DIROOT-Setup-Revit2024.exe`
- `TUNG_DIROOT-Portable-Revit2024.zip`
- `SHA256SUMS.txt`

Portable ZIP thực tế đã được giải nén và kiểm tra lại: chỉ có payload `2024`, không chứa `RevitAPI.dll`/`RevitAPIUI.dll`; luồng cài mới, cài đè và gỡ trong sandbox đều PASS.

## Cổng còn lại

Checklist trực tiếp trong Revit 2024 đang `NOT RUN`. Vì vậy artifact được gọi là release candidate, chưa tạo tag `v1.0.0` và chưa được mô tả là bản phát hành chính thức. Cần mở Revit 2024, kiểm tra Ribbon, import Drafting/Legend/Schedule, Image 150/300/600 DPI, DataOnly/Recreate, Auto Sync, Relink, pause/stop, cài đè và gỡ; sau đó ghi tester, exact Revit build và ngày vào release matrix.

Dependency runtime: DocumentFormat.OpenXml 3.3.0, System.Text.Json 8.0.5 và các thư viện .NET supporting được stage theo output. Không có Autodesk API, Microsoft Office, DiRoots, TungMEP/TUNGMEP2 binary trong release.

## Cập nhật release candidate — Sync tất cả (2026-08-14)

- Nhánh: `feature/sync-all`.
- Core: 140/140 PASS.
- Presentation: 10/10 PASS, gồm ca Sync tất cả độc lập filter, giữ selection, danh sách rỗng và trạng thái batch bận.
- Rendering: 9/9 PASS.
- Revit 2024 build, assembly/schema contract, release layout, portable install/uninstall và contract 2024-only: PASS.
- DLL đã cài từ artifact cuối trên `master`: `C:\Users\tungnt2303\AppData\Local\TUNG_DIROOT\1.0.3\2024\TUNG_DIROOT.Revit2024.dll`.
- SHA-256 DLL đã cài: `3E832B80FA54BE20D30B4472B6F38715263F2679D37B324FD2B70BE58085E649`.
- Backup có thể khôi phục: `C:\Users\tungnt2303\Downloads\TUNG_DIROOT_Backups\before-sync-all-20260814-083208`.
- Setup SHA-256: `2FDF31136EB4F4D2DFF973A03D2AB81C5C84585E0CB5F10C1479A89F52FA4402`.
- Portable ZIP SHA-256: `ACBECC69FAD6D2CA5F969F1E1E2FD808395E7C9B2D9487AC3C8166F9804DB622`.

Checklist trực tiếp cho `Sync tất cả` đang `NOT RUN` vì Revit 2024 hiện vẫn giữ DLL cũ trong bộ nhớ. Cần khởi động lại Revit một lần, sau đó xác nhận hai lần Sync liên tiếp trong cùng phiên Revit; không cần khởi động lại giữa hai lần Sync.
