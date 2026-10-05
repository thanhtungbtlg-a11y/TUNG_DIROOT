# PDF / Word Link — checklist trực tiếp trong Revit 2024

Status: **NOT RUN**. Chạy trên bản sao model. Ghi Revit build, ngày, tester và ảnh/log khi có lỗi.

## Nạp PDF

- [ ] Cài release candidate, khởi động lại Revit 2024; tab `TUNG_DIROOT` có nút `PDF / Word Link` và TableGen có nút `PDF / Word`.
- [ ] Kéo thả đồng thời hai PDF; giao diện nhận đủ hai file, không nhân đôi và không khóa Revit trước khi bấm Import.
- [ ] Nhập `1,3,5-7`, chọn 300 DPI, 2 cột; mỗi PDF tạo một Drafting View riêng, đúng năm trang, đúng thứ tự theo hàng.
- [ ] Thử `7-5,3,3`; kết quả chuẩn hóa, không tạo trang trùng.
- [ ] Thử trang không tồn tại; link đó rollback hoàn toàn, link khác trong batch vẫn được tạo.
- [ ] Thử 150/300/600 DPI và 1/2/4 cột; trang không chồng nhau.

## Nạp Word

- [ ] Với Microsoft Word đã cài: nạp DOCX nhiều trang; macro bị vô hiệu, tài liệu mở read-only và WINWORD.EXE kết thúc sau chuẩn bị.
- [ ] Với DOCM có macro: không có prompt/run macro; nội dung được xuất PDF rồi nhập.
- [ ] Gây lỗi Word giữa chừng; không còn WINWORD.EXE do tool tạo, không có PDF `.tmp.pdf` còn sót và Revit không crash.
- [ ] Trên máy không có Word: báo rõ `WORD_NOT_INSTALLED`; đóng cảnh báo rồi nhập PDF vẫn dùng được.

## Theo dõi và cập nhật

- [ ] Sửa nguồn khi cửa sổ mở; sau debounce, trạng thái đổi `Changed`, nhưng model không tự thay đổi.
- [ ] Xóa/đổi tên nguồn; trạng thái `Missing`; khôi phục file và bấm `Kiểm tra nguồn` thì trạng thái phục hồi.
- [ ] Tích nhiều link rồi `Cập nhật đã chọn`; mỗi link cập nhật trong transaction riêng. Một nguồn lỗi không rollback link thành công trước đó.
- [ ] Sau update, View cũ được giữ, chỉ phần tử đúng LinkId bị thay; element người dùng thêm thủ công vẫn còn.
- [ ] Xác nhận không tự Save RVT và không Synchronize with Central.

## Quản lý link và an toàn

- [ ] `Mở View` mở đúng Drafting View; `Mở nguồn` mở đúng PDF/Word.
- [ ] `Unlink — giữ View`: link biến mất nhưng View và các trang hiện tại còn nguyên; các phần tử không còn tag liên kết.
- [ ] `Xóa link + View`: No không đổi gì; Yes xóa đúng link/View; một Undo phục hồi transaction.
- [ ] Model chưa lưu hoặc nguồn khác volume: lưu absolute path an toàn. Model local cùng volume và bật tùy chọn: lưu relative path.
- [ ] Chuyển active document trước khi action: document guard báo lỗi, không sửa nhầm model.

## Giao diện và hồi quy

- [ ] Ở 1320×820 và 780×560, DPI Windows thực tế: không che nút, DataGrid còn đủ chiều cao và có cuộn.
- [ ] Đóng cửa sổ khi đang chuẩn bị Word: tool yêu cầu chờ; hoàn tất xong đóng được.
- [ ] Đóng/reopen cửa sổ không cần restart Revit; các link cũ được nạp lại từ registry.
- [ ] Smoke TableGen Excel, DataLink, Filter và Parameter Loader; không có hồi quy ribbon/dependency.
