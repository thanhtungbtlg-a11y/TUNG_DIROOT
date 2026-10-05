# TUNG_DIROOT 1.2.0 DataLink + Filter release candidate

Status: **RELEASE CANDIDATE — PENDING MANUAL REVIT 2024 CHECK**.

Ngày chuẩn bị: 2026-09-22. Nhánh: `codex/datalink-filter`. Source commit dùng để build: `c0416d4`.

## Phạm vi

Bản kết hợp giữ nguyên các lệnh `TableGen`, `Parameter Loader`, `DataLink` và bổ sung nút `Filter` độc lập trong panel `Model Tools`. Filter hỗ trợ ba scope, category/family/type, quy tắc Text/Number/Integer/Yes-No với AND/OR, batch 500 element, hủy an toàn, profile, chọn/zoom, temporary hide/isolate, Selection Filter, xuất Excel và view 3D riêng `TUNG_FILTER_*`. Phạm vi phát hành chỉ gồm Revit 2024.

## Kết quả tự động

- Core: 230/230 PASS; Presentation: 50/50 PASS; Rendering: 9/9 PASS.
- Revit 2024 Release build: PASS, 0 warning/error.
- Add-in, Parameter Loader, DataLink, Filter và Extensible Storage schema contracts: PASS.
- Responsive WPF tại 1366×768 và 760×520: PASS.
- Release layout, 2024-only payload, portable install-over/uninstall và Inno Setup: PASS.
- Audit network/telemetry: PASS; không phát hiện API mạng ngoài schema namespace cho phép.
- Checklist trực tiếp: `NOT RUN`; xem [Filter checklist](filter-revit-2024-checklist.md) và [DataLink checklist](datalink-revit-2024-checklist.md).

## Backup và artifact

Artifact DataLink-only cũ vẫn được giữ nguyên tại:

`C:\Users\tungnt2303\Downloads\TUNG_DIROOT_Releases\1.2.0-datalink-only-20260922-rc1`

Artifact kết hợp mới được lưu riêng tại:

`C:\Users\tungnt2303\Downloads\TUNG_DIROOT_Releases\1.2.0-datalink-filter-20260922-rc1`

- `TUNG_DIROOT-Setup-Revit2024.exe`: `DB23D3529265F687DEF52021C9EE836BA6F84A0C1EFA2BA4F6983438EBD47356`.
- `TUNG_DIROOT-Portable-Revit2024.zip`: `F928600A83E13D161A140059C62E21E45ED49FB6C63B3CBB0B8B2FBD0793466B`.
- `SHA256SUMS.txt`: `258A202D3B41B5A6CDFCC81AD49F25990B4C23D7CD4057791A1253E3109FBBDF`.
- `release.json`: `94DEC5C0C84E7E5CCEA70618799D9A19C5A7A370B4DEFB010AA887D2C7C2E0DD`.

Đây là release candidate vì chưa chạy thao tác trực tiếp trong Revit. Không đổi release matrix sang PASS và không coi artifact này là bản phát hành chính thức trước khi checklist được ký xác nhận.
