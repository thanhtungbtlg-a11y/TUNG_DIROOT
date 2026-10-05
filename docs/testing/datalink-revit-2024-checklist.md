# DataLink — checklist trực tiếp trong Revit 2024

Status: **NOT RUN**. Ghi tester, Revit build, model thử, ngày và bằng chứng trước khi đổi trạng thái. Dùng bản sao model; không dùng project sản xuất cho lần kiểm tra đầu tiên.

## Chuẩn bị

- [ ] Cài bản 1.3.0 release candidate, khởi động lại Revit 2024 và thấy các lệnh độc lập `TableGen`, `DataLink`, `Parameter Loader`, `Filter`.
- [ ] Mở project thử có parameter Text, Integer/Yes-No, Double có đơn vị, parameter Type, parameter read-only và ít nhất hai element chung một Type.
- [ ] Nếu kiểm tra worksharing, mở hai user/session để tạo trường hợp element/type do người khác sở hữu.

## Xuất, đọc và ghi

- [ ] `Đối tượng đang chọn`: nạp catalog, chọn một category + parameter Instance, xuất Excel; sheet `DATA` đúng element đã chọn và `_TUNG_META` VeryHidden/protected.
- [ ] Sửa `Edited` của một parameter Instance, đọc preview thành `Sẵn sàng`, áp dụng và xác nhận đúng element được đổi.
- [ ] Sửa một parameter Type với các element chung Type cùng yêu cầu một giá trị; preview/applied đúng và mọi instance cùng Type phản ánh kết quả.
- [ ] Tạo hai giá trị khác nhau cho cùng parameter Type; tất cả dòng liên quan báo `Xung đột Type` và không thể chọn.
- [ ] Đổi `Apply` thành `FALSE`; dòng báo bỏ chọn/không đổi và không thể áp dụng.
- [ ] Kiểm tra số thập phân và parameter có đơn vị theo locale Windows/Revit hiện tại.

## Các chốt an toàn

- [ ] Sau khi xuất, sửa giá trị tương ứng trực tiếp trong Revit rồi đọc Excel; dòng báo `Revit đã đổi` và không ghi đè.
- [ ] Mở workbook từ model A trong model B; mọi dòng báo `Khác model`.
- [ ] Xóa một element sau khi xuất; dòng báo `Thiếu element`, các dòng hợp lệ khác vẫn preview được.
- [ ] Xóa/đổi parameter sau khi xuất; dòng báo `Thiếu parameter` hoặc kiểu không hỗ trợ.
- [ ] Sửa `Edited` của parameter read-only; dòng báo `Chỉ đọc` và không thể chọn.
- [ ] Với worksharing, element/type do user khác sở hữu báo `Không có quyền sửa`; element có quyền vẫn áp dụng.
- [ ] Sửa metadata/cột nhận diện hoặc xóa `_TUNG_META`; workbook bị từ chối bằng thông báo rõ ràng, Revit không thay đổi.
- [ ] Một nhóm bị lỗi trong lúc apply rollback riêng; nhóm hợp lệ còn lại commit.
- [ ] Sau một lượt có nhiều thay đổi, một lần Undo hoàn tác toàn bộ lượt DataLink.
- [ ] Xác nhận DataLink không tự Save model và không Synchronize with Central.

## Cửa sổ, hủy và hồi quy

- [ ] Trong lúc nạp/xuất/đối chiếu, bấm `Hủy`; thao tác dừng tại điểm an toàn, Revit không crash và không để transaction mở.
- [ ] Thử đóng cửa sổ khi ExternalEvent đang chạy; cửa sổ từ chối đóng bằng thông báo tiếng Việt.
- [ ] Đóng rồi mở lại DataLink; singleton được tạo lại, không còn trạng thái busy/pending cũ.
- [ ] Mở DataLink ở model A, chuyển sang model B rồi bấm lệnh; thao tác bị chặn và yêu cầu quay lại đúng model.
- [ ] DataLink ở kích thước 760×520 và DPI Windows đang dùng không che nút `Áp dụng`/`Đóng`; danh sách vẫn cuộn được.
- [ ] Smoke TableGen: thêm/đồng bộ một bảng Excel thành công; đóng DataLink không đóng TableGen.
- [ ] Smoke Parameter Loader: `Kiểm tra` workbook mẫu và xác nhận không có regression; đóng DataLink không đóng Parameter Loader.
- [ ] Setup install-over 1.1.0, portable install và uninstall; RVT/Excel/TXT và `%LocalAppData%\TUNG_DIROOT` user data ngoài thư mục chương trình được giữ nguyên.
