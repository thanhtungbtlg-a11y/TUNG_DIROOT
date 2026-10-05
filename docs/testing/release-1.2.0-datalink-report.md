# TUNG_DIROOT 1.2.0 DataLink-only release candidate

Status: **RELEASE CANDIDATE — PENDING MANUAL REVIT 2024 CHECK**.

Ngày chuẩn bị: 2026-09-22. Nhánh: `codex/datalink-filter`.

## Phạm vi

Bản DataLink-only bổ sung lệnh `DataLink` độc lập cạnh TableGen: chọn scope/category/parameter, xuất workbook có metadata bảo vệ, xem trước thay đổi an toàn và áp dụng bằng một Undo group. TableGen và Parameter Loader giữ nguyên command identity và dữ liệu đã lưu. Phạm vi phát hành chỉ gồm Revit 2024.

## Kết quả tự động

- Core: 203/203 PASS; Presentation: 40/40 PASS; Rendering: 9/9 PASS.
- Revit 2024 Release build: PASS, 0 warning/error; add-in/DataLink/schema contracts: PASS.
- Responsive WPF, release layout, portable install-over/uninstall và Inno Setup 2024-only: PASS.
- Manual checklist: `NOT RUN`; xem [datalink-revit-2024-checklist.md](datalink-revit-2024-checklist.md).

## Backup và artifact

Backup 1.1.0 hiện có, không bị di chuyển hoặc ghi đè:

`C:\Users\tungnt2303\Downloads\TUNG_DIROOT_Backups\1.0.0-before-parameter-loader-20260921-135108`

Artifact DataLink-only được lưu riêng tại:

`C:\Users\tungnt2303\Downloads\TUNG_DIROOT_Releases\1.2.0-datalink-only-20260922-rc1`

- `TUNG_DIROOT-Setup-Revit2024.exe`: `56AD469B10EAD08749D5ACE34E025CB6ED804051FBD6CC94DF6D29EA856203DD`.
- `TUNG_DIROOT-Portable-Revit2024.zip`: `C67AE8C8449F4820FB90A72437784D2208AF04790073C266F084BC236BD4D60F`.
- `SHA256SUMS.txt`: `EBB0784C73D3E1AFEB11292B5D528A2AB2A3C26D1A6F7B670C050480E9F158C9`.
- `release.json`: `0D97C8AB47DA56D9575647BFC24D52C5FAD814F306E7F6CB6F4055E38AAE0AA7`.

Các artifact trên là release candidate vì checklist Revit trực tiếp vẫn `NOT RUN`; chúng không thay thế bản 1.1.0 đã lưu trước đó.
