# Hướng dẫn Parameter Tools

## ReOrdering

`Parameter ReOrdering` chỉ chạy khi đang mở một family trong Family Editor của Revit 2024.

1. Bấm **Xuất thứ tự hiện tại** và lưu workbook `.xlsx`.
2. Mở tab `FAMILY_PARAMETER_ORDER`. Chỉ sửa cột `TARGET_ORDER_IN_GROUP`; không sửa `KEY`, `NAME`, `GROUP` hay xóa/thêm dòng.
3. Trong mỗi `GROUP`, giá trị mục tiêu phải đủ liên tục từ `1` đến `N` và không trùng.
4. Lưu Excel, quay lại tool và bấm **Đọc & xem trước**.
5. Khi Preview không còn lỗi, bấm **Apply thứ tự**.

Revit chỉ cho đổi thứ tự parameter bên trong cùng một parameter group; vị trí các group không đổi. Tool đọc cả parameter ẩn vì `FamilyManager.ReorderParameters` yêu cầu đúng toàn bộ danh sách do Revit trả về. Trước Apply, tool tự tạo file `.TUNG_BACKUP_yyyyMMdd_HHmmss.xlsx` cạnh workbook nguồn. Toàn bộ lượt Apply nằm trong một transaction nên có thể Undo một lần. Tool không Save hoặc Save As family.

Nếu family đã thêm/xóa/đổi parameter sau khi xuất Excel, Preview báo `PARAMETER_SET_CHANGED`; hãy xuất workbook mới. Không dùng ReOrdering để đổi tên hay đổi group—những thao tác đó thuộc ParaManager.

## ParaManager

ParaManager chạy được trong Project hoặc Family Editor và tự nhận `SCOPE` tương ứng.

1. Bấm **Xuất inventory**, mở tab `PARAMETER_MANAGER`.
2. Dòng hiện có dùng `UPDATE`; dòng mới dùng `CREATE_SHARED`. Chỉ đặt `APPLY=TRUE` cho dòng cần chạy.
3. Chọn file Shared Parameter `.txt` nếu có dòng `CREATE_SHARED`, rồi bấm **Đọc & xem trước**.
4. Chỉ Apply các dòng `Ready`; sửa `Invalid/Conflict` trong Excel rồi Preview lại.

Các cột chính:

- `IDENTITY`: định danh do tool xuất; không sửa trên dòng UPDATE.
- `SHARED_GROUP`, `NAME`, `GUID`: phải khớp definition trong Shared Parameter TXT khi CREATE_SHARED.
- `BINDING`: `INSTANCE` hoặc `TYPE`. Tool không tự chuyển loại binding của parameter hiện có.
- `PARAMETER_GROUP_ID`: Forge group id do Revit xuất. Copy giá trị hợp lệ từ tab `GROUPS`.
- `CATEGORIES`: các mã `OST_*` ngăn bằng dấu chấm phẩy; chỉ dùng ở Project.

Workbook còn có tab `CATEGORIES` chứa toàn bộ category bindable trong document hiện tại để copy key chính xác.

Trong Project, UPDATE dùng chế độ merge category: category mới được thêm nhưng category cũ không bị xóa. Tên project/shared parameter không được đổi. Trong Family, parameter nội bộ có thể đổi tên/group khi Revit cho phép; shared và built-in không được đổi tên. Mỗi dòng có transaction riêng, toàn lượt có một Undo và workbook backup timestamp được tạo trước Apply. Tool không Save/Save As/Sync Central.
