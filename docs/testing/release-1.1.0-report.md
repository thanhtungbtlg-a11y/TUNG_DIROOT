# TUNG_DIROOT 1.1.0 release report

Status: **RELEASE CANDIDATE — PENDING MANUAL REVIT 2024 CHECK**.

Ngày chuẩn bị: 2026-09-21. Nhánh: `codex/parameter-loader`.

## Phạm vi

Phiên bản 1.1.0 bổ sung Parameter Loader độc lập trong tab TUNG_DIROOT: đọc Shared Parameters TXT và quy tắc Excel, phân tích trước khi gán, tạo Instance/Type binding còn thiếu và giữ nguyên binding đã tồn tại. Phạm vi phát hành chỉ gồm Revit 2024.

## Kết quả tự động

- Core: 176/176 PASS.
- Presentation: 34/34 PASS.
- Rendering: 9/9 PASS.
- Revit 2024 Release build: PASS, 0 warning/error.
- Parameter Loader/add-in/schema assembly contracts: PASS.
- Release layout, SHA-256 manifest, workbook mẫu và payload Docs/Samples: PASS.
- Portable install, install-over, uninstall sandbox và Inno Setup 2024-only contract: PASS.
- Payload không chứa `RevitAPI.dll`/`RevitAPIUI.dll`; source audit không có network/telemetry API: PASS.

## Backup và artifact

Backup có thể khôi phục (copy, không di chuyển/xóa bản cũ):

`C:\Users\tungnt2303\Downloads\TUNG_DIROOT_Backups\1.0.0-before-parameter-loader-20260921-135108`

- Setup cũ: `8AF0DB14EBABD6B992AD440C6C8874BAB4C523C3C24A064F753CB2528E97045C`.
- Portable cũ: `9551C8FA907B34A85D5CB3E0F13474FF5D14DC61A44C4993276AA30F13146E24`.
- `SHA256SUMS.txt` cũ: `3481FC2A490868FE686C93BA30C7473C8EFFD83EBF3AE90D41E66A93EF6CF821`.

Release candidate được tạo tại thư mục `dist` của worktree:

- `TUNG_DIROOT-Setup-Revit2024.exe`: `FA2A8B32AFF29684639A415B91DB6711093479FBE669E2BE7B0E12EB1405FF2F`.
- `TUNG_DIROOT-Portable-Revit2024.zip`: `AEAC2CDA47E766C427CC8516F4BCA15C24110B80B7261699C719D8A27B414B81`.
- Portable chứa workbook mẫu và tài liệu, không chứa `RevitAPI.dll`/`RevitAPIUI.dll`.

## Cổng thủ công còn lại

Checklist tại [revit-2024-checklist.md](revit-2024-checklist.md) đang `NOT RUN`, gồm cả Ribbon/UI, TableGen, Parameter Loader, một bước Undo, phục hồi Shared Parameters path, cài đè và gỡ. Vì vậy artifact 1.1.0 chỉ được gọi là release candidate cho đến khi tester ghi PASS, Revit build, ngày và bằng chứng vào báo cáo/matrix.
