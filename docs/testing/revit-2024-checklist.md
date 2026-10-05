# Revit 2024 release checklist

Status: **NOT RUN**. Ghi tester, build number, model, ngày và bằng chứng trước khi đổi trạng thái.

- [ ] Clean project: Drafting, Legend seed và Schedule header.
- [ ] Revit Table và Image ở 150/300/600 DPI.
- [ ] Refresh, Data-only, Recreate; user TextNote còn nguyên.
- [ ] Auto Sync, Relink, missing/locked source và workshared editability.
- [ ] Batch pause/resume/stop giữa các view; một view lỗi không hủy batch.
- [ ] Parameter Loader mở modeless; chọn TXT + workbook mẫu và `Kiểm tra` không chỉnh model.
- [ ] Workbook mẫu có Instance + Type đều đang tắt; bật từng dòng, lưu Excel, kiểm tra lại thành `Ready`.
- [ ] Gán Instance/Type đúng group/category; dòng đã có báo `AlreadyBound` hoặc `ReviewRequired` và không bị sửa.
- [ ] Một rule sai/thiếu definition/category rollback riêng; rule hợp lệ còn lại vẫn chạy.
- [ ] Sau batch, một lần Undo gỡ toàn bộ parameter vừa gán; Shared Parameters path cũ được phục hồi.
- [ ] Đóng/mở cửa sổ không crash; đổi active document trước lúc chạy được chặn bằng thông báo rõ ràng.
- [ ] Setup install, install-over, uninstall và portable install.
