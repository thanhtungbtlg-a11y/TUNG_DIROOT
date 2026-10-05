# TableGen Advanced — checklist trực tiếp trong Revit 2024

Status: **NOT RUN**. Chạy trên bản sao model và workbook thử; ghi Revit build, tester, ngày và bằng chứng trước khi đổi trạng thái.

## Chuẩn bị và nạp nguồn

- [ ] Cài release candidate mới, khởi động lại Revit 2024 và xác nhận tab `TUNG_DIROOT` có `TableGen`.
- [ ] Mở một model thử đã lưu local; chuẩn bị hai file `.xlsx`/`.xlsm` có Named Range, Print Area, merge, border, màu, công thức và ít nhất một công thức lỗi `#REF!`.
- [ ] Kéo thả đồng thời hai file vào `Thêm Excel`; danh sách nạp đủ, không nhân đôi do sự kiện drag/drop và không crash.
- [ ] Chọn đúng một dòng, mở `Xem trước`; ảnh đúng vùng và chẩn đoán nêu đúng công thức lỗi/merge/bảng lớn.
- [ ] Đóng cửa sổ Xem trước và Thêm Excel; cửa sổ TableGen chính vẫn mở và dùng được.

## Profile và độ trung thực

- [ ] Chọn một dòng, mở `Quản lý profile…`; danh sách chỉ chứa style có trong vùng Excel và Text Type/Line Style của đúng model đang active.
- [ ] Tạo, lưu, nạp lại và xóa một profile Unicode; profile vừa lưu xuất hiện ngay trong cột `Profile công ty` mà không cần đóng/mở Revit.
- [ ] Chọn một profile có quy tắc số, font, Text Type và Line Style; import RevitTable vào Drafting View.
- [ ] Đối chiếu dấu thập phân, phân nhóm chữ số, font, cỡ chữ, Bold/Italic, border và màu với profile.
- [ ] Schedule với font Excel 10/11 pt, Text Type công ty, text xoay 90°/−45° và màu đỏ/xanh dương: kiểm tra font size hiển thị và kích thước vật lý trên Sheet (table API không dùng feet cho TextSize), góc đúng và không đảo màu đỏ/xanh. Đây là bước hiệu chuẩn trực tiếp còn NOT RUN.
- [ ] Sửa Excel rồi chạy DataOnly; nội dung đổi nhưng kích thước/vị trí/chỉnh sửa thủ công được giữ theo phạm vi hỗ trợ.
- [ ] Chạy Recreate; bảng được dựng lại theo cùng profile và không xóa element thủ công không mang đúng LinkId.
- [ ] Import Schedule khi chọn DataOnly: lần đầu bảng vẫn khởi tạo đủ hàng/cột/merge/style; Sync sau giữ kích thước đã chỉnh trong Schedule.
- [ ] Import/Recreate Schedule với Text Type/Line Style công ty và Đen trắng; mapping vẫn khớp Excel gốc. Style đã ánh xạ có ưu tiên, không bị đổi màu/type dùng chung trong model.
- [ ] DataOnly trên bảng có ô trống/ô gộp chỉ đổi nội dung TextNote hiện có, không tự Recreate. Đổi ô trống thành có text hoặc làm mất/tag trùng một TextNote: báo `DATA_ONLY_TEXT_SCOPE_CHANGED`, giữ nguyên toàn bộ bảng trước khi người dùng chọn Recreate.
- [ ] DataOnly khi đổi hàng/cột/merge báo `DATA_ONLY_STRUCTURE_CHANGED`, không tự dựng lại. Tích đúng một link rồi bấm **Dựng lại bảng**: hủy xác nhận không đổi model; chấp nhận đọc lại nguồn kể cả không đổi, giữ chế độ Sync đã lưu.
- [ ] Đặt thêm một ImageInstance dùng Image Type của ảnh Excel ở view khác: Sync/Rebuild báo `LINKED_IMAGE_TYPE_SHARED`, không xóa ảnh nào. Ảnh dùng type khác không làm chặn cập nhật.
- [ ] Khi Dựng lại bảng đang chờ ExternalEvent, PDF/Word bị khóa; bấm lặp không mở khóa cửa sổ hoặc hủy event đang chờ. Revit rollback transaction phải báo lỗi, không báo đã dựng lại.
- [ ] Chọn tên profile không còn tồn tại; link fallback an toàn về định dạng Excel, không crash và không lặp trạng thái Changed vô hạn.

## Theo dõi nguồn và Sync

- [ ] Giữ TableGen mở, sửa rồi Save Excel nhiều lần liên tiếp; sau khoảng một giây chỉ một lượt kiểm tra diễn ra và dòng chuyển `Changed`.
- [ ] Rename hoặc xóa file Excel; dòng chuyển `Missing`/`Error` đúng link. Đưa file về tên cũ và bấm `Kiểm tra nguồn`; trạng thái phục hồi.
- [ ] Khi một batch đang chạy, Save Excel; batch không bị chen ngang và yêu cầu kiểm tra chạy sau điểm an toàn.
- [ ] Xác nhận phát hiện thay đổi không tự Sync, không Save model và không Synchronize with Central.
- [ ] `Sync tất cả` xét cả dòng bị ẩn bởi tìm kiếm/bộ lọc; nguồn không đổi được bỏ qua và một link lỗi không dừng link khác.

## Quản lý liên kết

- [ ] Không tích hoặc tích hai dòng: bảy nút quản lý link đều bị khóa. Tích đúng một dòng: bảy nút được bật.
- [ ] `Mở View`, `Mở Excel`, `Mở thư mục` mở đúng đích của dòng đã tích.
- [ ] `Nhân cấu hình` mở đúng một dòng trong Thêm Excel, giữ các tùy chọn cũ, cho phép sửa trước Import và chưa tạo View khi chỉ đóng cửa sổ.
- [ ] `Đổi View đích` chỉ liệt kê View cùng loại chưa gắn link; chọn Drafting/Legend mới thì nội dung được recreate, View cũ được gỡ đúng LinkId. Gây lỗi có chủ ý và xác nhận TransactionGroup rollback cả registry/marker/nội dung.
- [ ] Link Schedule báo chưa hỗ trợ đổi View đích; link Drafting không cho chọn Legend và ngược lại.
- [ ] `Unlink — giữ View`: chọn Yes; dòng biến mất khỏi manager nhưng View và bảng hiện tại còn nguyên. Undo phục hồi transaction nếu Revit cho phép.
- [ ] `Xóa link + View`: chọn No không thay đổi gì; chạy lại và chọn Yes thì đúng link + View bị xóa, link khác giữ nguyên; một Undo phục hồi.
- [ ] Với View đã bị xóa trước hoặc file mất, lệnh báo rõ `VIEW_DELETED`/`FILE_MISSING`, không tác động nhầm dòng khác.

## Ổn định và hồi quy

- [ ] Cửa sổ ở 1366×768 và 760×520, DPI Windows đang dùng: toolbar có wrap, danh sách cuộn và không che nút.
- [ ] Đóng TableGen rồi sửa Excel: không còn callback, dialog hoặc crash; mở lại TableGen vẫn theo dõi bình thường.
- [ ] Chuyển sang model khác khi cửa sổ đang mở: document guard chặn action đối với model cũ.
- [ ] Smoke DataLink, Filter và Parameter Loader sau lượt TableGen; không có hồi quy ribbon hoặc dependency.
- [ ] Setup install-over, portable install và uninstall không xóa RVT, workbook hoặc profile người dùng.
