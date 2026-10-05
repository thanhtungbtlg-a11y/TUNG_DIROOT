# Hướng dẫn FamilyReviser

`FamilyReviser` đổi tên hàng loạt loadable Family và Family Type trong Revit 2024 qua quy trình Excel → Preview → Apply. Công cụ chạy trong Project và Family Editor, không Save model và không Synchronize with Central.

## Quy trình sử dụng

1. Mở đúng document, vào `TUNG_DIROOT > Parameter Tools > Family Reviser`.
2. Bấm **Xuất inventory**, chọn nơi lưu workbook `.xlsx` rồi mở Excel.
3. Trong tab `FAMILY_REVISER`, chỉ sửa `APPLY`, `TARGET_FAMILY` hoặc `TARGET_TYPE`. Đặt `APPLY=TRUE` cho đúng dòng cần đổi.
4. Lưu và đóng Excel, quay lại Revit, bấm **Đọc & xem trước**.
5. Chỉ các dòng `Ready` được Apply. Sửa mọi dòng `Invalid/Conflict` trong Excel rồi Preview lại.
6. Bấm **Apply các dòng Ready**. Tool tạo workbook backup có timestamp cạnh file nguồn; mỗi dòng dùng transaction riêng và toàn lượt thành một Undo.

`RENAME_FAMILY` dùng `TARGET_FAMILY`; `RENAME_TYPE` dùng `TARGET_TYPE`. Không sửa `ACTION`, `UNIQUE_ID`, tên hiện tại hay các cột trạng thái vì chúng dùng để phát hiện workbook cũ/stale và tránh ghi nhầm element.

## Quy tắc an toàn

- Chặn tên rỗng, dài hơn 255 ký tự, khoảng trắng ở đầu/cuối, ký tự `&lt; &gt; : &quot; / \ | ? *` và ký tự điều khiển.
- Chặn trùng tên Family trong document và trùng Type trong cùng Family, kể cả trùng giữa các dòng Excel đang chọn.
- Chặn nhiều dòng Apply cùng `UNIQUE_ID` bằng `IDENTITY_DUPLICATE`; không cho đổi một element hai lần trong cùng lượt.
- Chặn đổi tên in-place Family. Tool không xóa, duplicate, load hoặc overwrite Family.
- Nếu Family/Type đã bị đổi hoặc xóa sau lúc xuất workbook, Preview báo `ELEMENT_STALE` hoặc `CURRENT_NAME_STALE`; hãy xuất workbook mới.
- Trong Family Editor, các loadable Family tìm thấy được đánh dấu `IS_NESTED=TRUE`. Đây là thông tin kiểm tra, không phải cột để chỉnh.

Ô **Regex quy ước tên** là tùy chọn. Ví dụ `^TUNG_[A-Z0-9_]+$` chỉ cho tên bắt đầu bằng `TUNG_` và theo mẫu chữ hoa/số/gạch dưới. Bỏ trống nếu công ty chưa có quy tắc tên. Regex sai được chặn trước Apply.

Cửa sổ là modeless nên vẫn xem được model, nhưng khi Apply phải quay lại đúng document đã mở tool. Nếu chuyển document, công cụ dừng và báo rõ thay vì tác động nhầm model.

Đổi đường dẫn workbook, regex, xuất file mới hoặc Preview lỗi sẽ vô hiệu hóa Preview trước đó; phải Preview lại mới bật Apply. Khi đang xử lý, đường dẫn/Chọn file/regex được khóa. Apply kiểm tra lại **chỉ các dòng Ready đã xác nhận** với model hiện tại; dòng trở nên stale hoặc không hợp lệ sẽ được báo lỗi theo đúng dòng Excel, không tự mở rộng sang dòng chưa được duyệt.
