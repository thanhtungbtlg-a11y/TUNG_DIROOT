# Checklist FamilyReviser — Revit 2024

Checklist này cần chạy trên file test, không dùng model sản xuất. Automated contract không thay thế smoke test tương tác trong Revit.

## Project

1. Mở project có ít nhất hai loadable Family, mỗi Family có từ hai Type và một in-place Family.
2. Mở `TUNG_DIROOT > Parameter Tools > Family Reviser`; thu cửa sổ xuống 800×560 và xác nhận các nút Preview, Apply, Đóng vẫn nhìn thấy.
3. Xuất workbook. Kiểm tra mỗi Family có một dòng `RENAME_FAMILY`, mỗi Type có một dòng `RENAME_TYPE`, `UNIQUE_ID` không rỗng.
4. Chọn một Family và một Type, đặt tên đích hợp lệ, Preview phải `Ready`. Apply, kiểm tra tên trong Project Browser và thử một Undo.
5. Đặt Family đích trùng Family khác và Type đích trùng Type cùng Family; Preview phải báo `FAMILY_NAME_DUPLICATE` và `TYPE_NAME_DUPLICATE`.
6. Thử tên có `/`, xuống dòng, khoảng trắng cuối và regex không khớp; tất cả phải bị chặn trước transaction.
7. Chọn in-place Family; Preview phải báo `INPLACE_FAMILY_RENAME_BLOCKED`.
8. Nhân đôi dòng có cùng `UNIQUE_ID`, cả hai Apply và tên đích khác nhau; Preview phải chặn bằng `IDENTITY_DUPLICATE`.
9. Preview workbook A có Ready, đổi đường dẫn sang B hoặc regex; Apply lập tức bị khóa. Cho B lỗi khi đọc: không được Apply lại dữ liệu A.
10. Sau Preview, sửa model để một dòng Ready trở thành stale; Apply báo lỗi dòng đó và không áp dụng dòng từng bị Conflict chỉ vì model đã thay đổi.

## Family Editor và an toàn

1. Mở family có nested loadable family; xuất workbook và xác nhận `IS_NESTED=TRUE` cho các dòng tìm thấy.
2. Sau khi xuất, đổi tên một type thủ công trong Revit rồi Preview workbook cũ; phải báo `CURRENT_NAME_STALE`.
3. Chuyển sang document khác khi cửa sổ đang mở rồi bấm Preview/Apply; tool phải chặn ghi nhầm document.
4. Xác nhận backup `.TUNG_BACKUP_<timestamp>.xlsx` được tạo trước Apply và đọc lại được.
5. Cho một dòng hợp lệ và một dòng Revit từ chối; dòng lỗi rollback riêng, dòng đúng vẫn thành công, cả lượt còn một Undo.
6. Xác nhận tool không Save, Save As, Synchronize with Central, gọi mạng, xóa hoặc duplicate Family.
