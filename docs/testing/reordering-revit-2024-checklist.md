# Checklist Parameter ReOrdering — Revit 2024

1. Mở một family test có tối thiểu hai parameter trong cùng group và một parameter ở group khác.
2. Mở `TUNG_DIROOT > Parameter Tools > Parameter ReOrdering`; xác nhận cửa sổ modeless và không che nút khi thu nhỏ.
3. Xuất workbook; xác nhận tab `FAMILY_PARAMETER_ORDER` có đủ mọi parameter, header/filter/freeze hoạt động.
4. Hoán đổi hai giá trị `TARGET_ORDER_IN_GROUP` trong cùng group, lưu và Preview. Hai dòng phải hiện `Changed`.
5. Tạo số thứ tự trùng, xóa một dòng, sửa NAME và sửa GROUP; từng trường hợp phải bị chặn trước Apply.
6. Apply cấu hình hợp lệ; xác nhận thứ tự trong Family Types đổi đúng nhưng group khác không bị chuyển.
7. Xác nhận file `.TUNG_BACKUP_<timestamp>.xlsx` được tạo cạnh workbook và đọc lại được.
8. Undo một lần trong Revit; thứ tự trở về trước Apply.
9. Chuyển sang document khác khi cửa sổ đang mở; Apply phải yêu cầu quay lại đúng family.
10. Xác nhận công cụ không Save/Save As family và không tạo kết nối mạng.

Trường hợp Rebar Shape family không hỗ trợ reorder phải báo lỗi Revit rõ ràng, rollback transaction và vẫn giữ file backup.
