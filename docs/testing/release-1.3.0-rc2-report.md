# TUNG_DIROOT 1.3.0 RC2 — Revit 2024

Build time: 2026-10-03 08:14 (Asia/Saigon)  
Source commit: `2d62b84448b4b6abffb21f6c07b040405caaff4c`  
Scope: Revit 2024 only; corrective RC2 batch, not completion of every roadmap extension.

## Nội dung đợt này

- TableGen: tra mapping theo style Excel gốc kể cả Đen trắng; Schedule dùng company profile khi tạo/Recreate; sửa giá trị cỡ chữ/góc xoay/màu ở biên Revit API. DataOnly dừng khi cấu trúc hoặc phạm vi TextNote không tương thích, không tự chuyển sang Recreate.
- Nút **Dựng lại bảng**: chọn đúng một link và xác nhận; đọc lại nguồn kể cả không đổi, giữ chế độ cập nhật đã lưu. Chặn xóa Image Type nếu có ảnh ngoài link đang dùng chung. Chỉ báo thành công khi transaction thực sự Committed; tách trạng thái bận UI/batch, không cho lệnh phụ mở khóa ExternalEvent đang chờ.
- SheetLink/SheetGen: tab REVISION tùy chọn, tham chiếu revision trong cùng batch, Preview cũ→mới; chặn trùng identity và thay đổi chưa hỗ trợ. Apply gắn với workbook/Preview hiện tại và giữ đúng số dòng Excel kể cả có dòng trống.
- FamilyReviser: vô hiệu Preview cũ, chặn trùng identity; chỉ Apply các dòng Ready đã được người dùng xem/xác nhận, báo lỗi revalidation riêng.
- Bulk V/G Category/Filter: giữ nhóm thuộc tính không chọn, Replace chỉ tác động filter đã chọn, giữ thứ tự nguồn; Preview phản ánh thêm/xóa/đổi thứ tự/bỏ qua và Update Existing.
- Installer: chép đầy đủ Docs/Samples, backup bản cài có ownership trước khi thay; restore kiểm tra đầy đủ hash/phạm vi đường dẫn và không ghi đè runtime hiện tại. Chặn nâng cấp/khôi phục vào manifest thật khi Revit đang chạy.

## Automated verification

`tools/Test-All.ps1` đã chạy đầy đủ trên worktree và lần nữa trên master sau fast-forward, qua `build/Build-Release.ps1 -ReleaseCandidate`.

- Core: 419 PASS, 0 failed/0 skipped.
- Presentation: 73 PASS, 0 failed/0 skipped.
- Rendering: 10 PASS, 0 failed/0 skipped.
- Tổng: 502 test PASS; clean build và Revit 2024 build: 0 warnings / 0 errors.
- Responsive WPF layout: PASS, gồm toolbar TableGen có nút Rebuild và cửa sổ nhỏ; importer loại bỏ event handler code-behind khi nạp XAML test độc lập.
- Runtime dependencies, các add-in contracts và Extensible Storage schema: PASS.
- Release layout và hash manifest: PASS.
- Portable install/install-over/uninstall trong sandbox: PASS.
- Backup/restore sandbox: PASS; từ chối thư mục không sở hữu, metadata thiếu, đường dẫn chồng lấn, file ngoài manifest hash và backup bị sửa trước khi thay manifest đích.
- Revit-2024-only installer contract và Inno Setup compilation: PASS.
- ZIP RC2 cuối được giải nén và cài trong sandbox riêng; toàn bộ payload đã cài khớp `release.json`, source commit đúng và có cả script Backup/Restore.
- Review độc lập các sửa Sheet/Bulk V/G/installer/TableGen: findings quan trọng đã được xử lý và rà soát lại. Review không thay thế kiểm thử live.

Các regression chạy red→green bao gồm DataOnly text/structure, mapping gốc, chuyển đổi style, stale Preview/identity, shared Image Type, transaction outcome và UI-busy ownership.

## Artifacts

Stable copy: `C:\Users\tungnt2303\Downloads\TUNG_DIROOT_Releases\1.3.0-rc2`

- `TUNG_DIROOT-Setup-Revit2024.exe` — 3,627,429 bytes  
  SHA-256: `C7F71AA5953A1DB04BE6F47D2DA7CEFBE45056D84C6DC0F5A80E878F4AEE7CDE`
- `TUNG_DIROOT-Portable-Revit2024.zip` — 2,383,939 bytes  
  SHA-256: `DBFB5262133F73F5EA07B57B0CDB47D009723D78059DE8004E39C9EA3EB2630D`
- `SHA256SUMS.txt` chứa hai hash trên; bản sao stable được đối chiếu lại với `dist`.

RC1 được giữ riêng tại `TUNG_DIROOT_Releases\1.3.0-rc1`, hai hash vẫn khớp [báo cáo RC1](release-1.3.0-rc1-report.md). `dist` trước khi build RC2 cũng được lưu ở `artifacts/pre-rc2-dist-20261003_081347`.

## Giới hạn và bước kế tiếp

- Chưa cài vào môi trường Revit thật, chưa mở/sửa model người dùng. Tất cả checklist thao tác trực tiếp Revit vẫn **NOT RUN**. Cài khi đã đóng Revit và khởi động lại để nạp DLL mới; hướng dẫn rollback ở [install guide](../install-guide-vi.md).
- Chưa xác nhận trực quan cỡ chữ vật lý, font, bố cục bảng trên các model thật; build/API contracts không chứng minh độ chính xác render trong Revit.
- Bulk V/G mới hỗ trợ Category/Filter. Sao chép đầy đủ Revit Link/DWG/Workset/Point Cloud và các tab/thiết lập V/G bổ sung vẫn là phần roadmap chưa triển khai trong RC2.
- Master đã fast-forward cục bộ; không push/PR. Worktree được giữ cho phần roadmap còn lại, không xóa các kết quả kiểm chứng đang cần.
