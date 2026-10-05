# TUNG Filter — checklist trực tiếp trong Revit 2024

Status: **NOT RUN**. Ghi tester, Revit build, model thử, ngày và bằng chứng trước khi đổi trạng thái. Dùng bản sao model có ít nhất 10.000 element cho bài kiểm tra tải; không dùng project sản xuất cho lượt đầu.

## Chuẩn bị và phạm vi

- [ ] Cài release candidate 1.3.0, khởi động lại Revit 2024 và thấy các lệnh độc lập: `TableGen`, `DataLink`, `Parameter Loader`, `Filter`.
- [ ] Mở project thử có parameter Text, Number/Length, Integer, Yes/No, parameter Type, parameter trống và category không có parameter đang xét.
- [ ] `Toàn bộ model`: nạp catalog, không tích category/family/type, chạy một điều kiện và xác nhận đúng toàn model.
- [ ] `View hiện tại`: chạy cùng điều kiện và xác nhận không trả element chỉ tồn tại ngoài view.
- [ ] `Đối tượng đang chọn`: chọn trước một tập element, nạp lại danh mục, xác nhận không trả element ngoài selection.

## Quy tắc và kết quả

- [ ] Text: kiểm tra `Bằng`, `Khác`, `Chứa`, `Không chứa`, không phân biệt hoa/thường.
- [ ] Number/Integer: kiểm tra `Lớn hơn` và `Nhỏ hơn` dùng giá trị nội bộ, không so sánh chuỗi hiển thị theo locale.
- [ ] Yes/No: kiểm tra `true`/`false` và `1`/`0`; giá trị sai được báo inline và không chạy.
- [ ] `Đang trống` chỉ khớp parameter tồn tại nhưng rỗng; element thiếu parameter không bị coi là rỗng.
- [ ] Hai điều kiện ở chế độ `AND` và `OR` cho số lượng đúng, ổn định khi trộn Text/Number/missing.
- [ ] Category/family/type tích chọn được áp cùng điều kiện parameter; bỏ tích một nhóm nghĩa là không giới hạn nhóm đó.
- [ ] Kết quả bằng 0 hiển thị rõ, các nút cần selection bị khóa và cửa sổ không lỗi.
- [ ] `Chọn tất cả`/`Bỏ chọn` và checkbox từng hàng hoạt động bằng một lần bấm, danh sách cuộn mượt.

## Hủy, document guard và tải lớn

- [ ] Chạy trên ít nhất 10.000 element; tiến độ tăng theo lô, Revit nhận lại điều khiển giữa các lượt và không giữ `Element`/`Parameter` qua callback.
- [ ] Bấm `Hủy` giữa lượt; dừng ở điểm an toàn, không crash, không để transaction mở và có thể chạy lại ngay.
- [ ] Thử đóng cửa sổ khi batch/ExternalEvent còn chạy; cửa sổ từ chối đóng và hướng dẫn hủy.
- [ ] Mở Filter ở model A, chuyển model B rồi bấm action; thao tác bị chặn, model B không thay đổi.
- [ ] Đóng rồi mở lại Filter sau khi batch hoàn tất; không còn trạng thái pending/busy cũ.

## Hành động và Undo

- [ ] `Chọn + zoom` chỉ chọn các dòng đã tích và hiển thị chúng trong Revit.
- [ ] `Cô lập tạm`, `Ẩn tạm`, `Bỏ ẩn / cô lập tạm` hoạt động trên view hiện tại và không tạo override vĩnh viễn.
- [ ] `Lưu Selection Filter` tạo/cập nhật tên bắt đầu `TUNG_FILTER_`; Undo phục hồi thay đổi.
- [ ] `Xuất Excel` tạo `RESULTS` có hàng tiêu đề cố định, AutoFilter, identity và đúng giá trị parameter hiển thị.
- [ ] `Tạo / cập nhật view 3D` tạo view isometric không-template `TUNG_FILTER_<profile>`, chỉ hiện kết quả và màu nhóm ổn định sau hai lần chạy.
- [ ] View nguồn không nhận per-element override; refresh chỉ xóa/đặt override trong view `TUNG_FILTER_*` tương ứng.
- [ ] Profile Unicode lưu/nạp lại đúng; profile không chứa cached Element ID và lần chạy mới phản ánh model hiện tại.

## Hồi quy và giao diện

- [ ] Cửa sổ ở 1366×768, 760×520 và DPI Windows đang dùng không che `Chạy lọc`, nhóm action, `Hủy`, `Đóng`; danh sách có cuộn.
- [ ] Smoke TableGen: nạp và sync một vùng Excel.
- [ ] Smoke DataLink: xuất, preview và áp dụng một dòng hợp lệ; một Undo hoàn tác lượt áp dụng.
- [ ] Smoke Parameter Loader: kiểm tra workbook mẫu, không tạo parameter khi các dòng đang tắt.
- [ ] Xác nhận Filter không tự Save, Save As, Synchronize with Central, không ghi linked document và không kết nối mạng.
- [ ] Setup install-over, portable install và uninstall giữ nguyên RVT/Excel/profile/user data ngoài thư mục chương trình.
