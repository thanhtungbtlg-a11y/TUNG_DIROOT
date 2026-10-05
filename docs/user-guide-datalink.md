# Hướng dẫn DataLink — Revit ↔ Excel

DataLink xuất parameter của element Revit ra một workbook Excel có metadata được bảo vệ, sau đó đọc lại để xem trước trước khi ghi vào model. Công cụ không tự Save, Save As hoặc Synchronize with Central. Mọi thay đổi được kiểm tra lại ngay trước khi ghi và toàn bộ lượt áp dụng xuất hiện thành một bước Undo.

## 1. Chọn dữ liệu

Mở `TUNG_DIROOT > Excel Tools > DataLink`. Cửa sổ modeless gồm ba tab. Ở tab `1. Chọn dữ liệu`:

1. Chọn phạm vi `Toàn bộ model`, `View hiện tại` hoặc `Đối tượng đang chọn`.
2. Bấm `Nạp category và parameter` nếu vừa đổi phạm vi hoặc selection.
3. Tích category cần xuất. Nếu không tích category nào, DataLink xét mọi category trong phạm vi.
4. Tích ít nhất một parameter. Dòng `Instance` đọc/ghi trên từng element; dòng `Type` đọc/ghi trên type nên nhiều element cùng type phải yêu cầu cùng một giá trị.

DataLink v1 hỗ trợ parameter kiểu Text, Integer/Yes-No và Double có đơn vị. Parameter tham chiếu `ElementId` chưa được ghi ngược. Parameter chỉ đọc vẫn có thể xuất để đối chiếu nhưng sẽ bị chặn khi preview nếu bị sửa.

## 2. Xuất và chỉnh Excel

Sang tab `2. Xuất / Nhập Excel`, bấm `Xuất Excel` và chọn file `.xlsx`. Sheet `DATA` có các cột:

| Cột | Ý nghĩa |
| --- | --- |
| `Apply` | `TRUE` để đưa dòng vào đối chiếu; đổi thành `FALSE` để bỏ qua dòng. |
| `ElementId`, `Category`, `Family`, `Type`, `Parameter` | Thông tin nhận diện để người dùng kiểm tra; không được dùng thay metadata. |
| `Original` | Giá trị tại thời điểm xuất. |
| `Edited` | Cột duy nhất dùng để nhập giá trị mới. |
| `Status` | Cột ghi chú dành cho quy trình kiểm tra. |

Chỉ sửa `Apply` và `Edited`. Không hiện, sửa, xóa hoặc đổi tên sheet `_TUNG_META`; sheet này chứa Document Stamp, UniqueId, parameter ID, kiểu lưu trữ và checksum. Nếu metadata hoặc cột nhận diện bị sửa, DataLink từ chối workbook thay vì đoán.

Số thập phân được đọc theo locale tại thời điểm xuất. Với parameter có đơn vị, nên nhập theo cách Revit/Windows đang hiển thị trên máy xuất file. Lưu và đóng Excel trước khi đọc lại để tránh file chưa ghi hết.

## 3. Xem trước và áp dụng

Bấm `Chọn file…` nếu cần đổi workbook, rồi bấm `Đọc và xem trước`. DataLink chuyển sang tab `3. Xem trước & áp dụng` và phân loại từng dòng:

- `Sẵn sàng`: giá trị mới hợp lệ, model vẫn đúng bản đã xuất và có thể chọn.
- `Không đổi`: giá trị không thay đổi hoặc `Apply=FALSE`.
- `Khác model`: workbook được xuất từ model khác.
- `Thiếu element` / `Thiếu parameter`: đối tượng hoặc parameter đã bị xóa/thay đổi.
- `Chỉ đọc` / `Không có quyền sửa`: Revit không cho ghi hoặc element/type thuộc người dùng khác.
- `Revit đã đổi`: giá trị trong model khác giá trị gốc trong workbook; DataLink không ghi đè.
- `Xung đột Type`: các element cùng type yêu cầu nhiều giá trị mới khác nhau.
- `Sai giá trị` / `Không hỗ trợ`: nội dung Excel không chuyển được về kiểu parameter.

Bấm `Chọn tất cả hợp lệ` hoặc tích từng dòng, sau đó `Áp dụng dòng đã chọn`. Chỉ dòng `Sẵn sàng` mới có thể chọn. Mỗi element/type lỗi rollback riêng; các nhóm hợp lệ khác vẫn có thể hoàn tất. Dùng một lần `Undo` trong Revit để quay lại toàn bộ lượt áp dụng đã commit.

## An toàn khi dùng cửa sổ modeless

Có thể pan/zoom và kiểm tra model khi cửa sổ mở. Trước mỗi thao tác Revit, DataLink xác nhận đúng document đã mở cửa sổ; nếu chuyển model, lệnh dừng và yêu cầu quay lại model ban đầu. Không thể đóng DataLink trong lúc một ExternalEvent đang chạy. Nút `Hủy` ghi nhận yêu cầu và dừng ở điểm an toàn kế tiếp; transaction đang thực hiện không bị cắt ngang. Đóng DataLink không đóng TableGen hoặc Parameter Loader.

Trước khi dùng trên project thật, chạy [checklist Revit 2024](testing/datalink-revit-2024-checklist.md) trên bản sao model, gồm cả instance/type, xung đột, stale data, worksharing và Undo.
