# Thiết kế Sync tất cả cho TUNG_DIROOT TableGen

Ngày: 2026-08-14  
Trạng thái: Thiết kế đã được người dùng duyệt trong hội thoại; chờ duyệt tài liệu viết.

## 1. Mục tiêu

Bổ sung thao tác `Sync tất cả` ngay trong cửa sổ TableGen của add-in độc lập `TUNG_DIROOT` cho Revit 2024. Người dùng có thể mở TableGen trong phiên Revit hiện tại và đồng bộ hàng loạt toàn bộ liên kết Excel đã nạp trước đó, không phải đóng rồi mở lại Revit trước mỗi lần đồng bộ.

Việc khởi động lại Revit chỉ cần thiết một lần sau khi cài DLL có tính năng mới. Các lần sử dụng sau chạy trực tiếp trong TableGen.

## 2. Hành vi người dùng

- Thêm nút `Sync tất cả` cạnh nút `Cập nhật đã chọn`.
- `Sync tất cả` lấy toàn bộ liên kết đang lưu trong model Revit, không phụ thuộc:
  - nội dung tìm kiếm;
  - bộ lọc trạng thái;
  - dòng đang hiển thị;
  - checkbox `Chọn` của từng dòng.
- Thứ tự xử lý giữ theo thứ tự liên kết do nguồn dữ liệu TableGen cung cấp.
- Checkbox và lựa chọn hiện tại của người dùng không bị thay đổi khi bắt đầu hoặc kết thúc Sync tất cả.
- Khi không có liên kết, lệnh không tạo batch rỗng và hiển thị trạng thái dễ hiểu trong giao diện.
- Khi batch đang chạy, nút `Sync tất cả` bị vô hiệu hóa giống các lệnh khởi tạo batch khác.

## 3. Quy tắc cập nhật

Mọi Link ID hiện có được đưa vào `RevitBatchCoordinator`. Với từng liên kết, `TableGenUpdateProcessor` đọc lại vùng Excel và tính fingerprint:

- Nguồn không đổi: trả về `UNCHANGED`, không dựng lại view.
- Nguồn thay đổi: cập nhật view theo `UpdateBehavior` đã lưu của liên kết.
- Mất file, view bị xóa hoặc view không editable: bỏ qua liên kết đó và ghi nguyên nhân.
- Một liên kết lỗi không hủy các liên kết còn lại.

Tính năng không cưỡng bức tạo lại các bảng không đổi, không tự lưu model và không tự chạy `Synchronize with Central`.

## 4. Luồng Revit và khả năng tương tác

- Cửa sổ TableGen tiếp tục là modeless.
- Mỗi liên kết được xử lý qua một lượt `ExternalEvent`, dùng transaction riêng cho view tương ứng.
- Các nút `Tạm dừng`, `Tiếp tục` và `Dừng` tiếp tục điều khiển batch tại ranh giới giữa hai liên kết.
- Trong lúc transaction của một liên kết đang chạy, Revit có thể bận trong thời gian ngắn; giữa các lượt ExternalEvent, Revit trả quyền điều khiển cho giao diện.
- Nếu người dùng chuyển sang document khác, bộ xử lý không cập nhật nhầm model và trả kết quả `DOCUMENT_NOT_ACTIVE` theo cơ chế hiện có.

## 5. Thay đổi thành phần

### Presentation

`TableGenViewModel` cung cấp:

- danh sách toàn bộ Link ID độc lập với `VisibleLinks`;
- `SyncAllCommand` gọi callback với danh sách đó;
- điều kiện thực thi phù hợp với `IsBusy` và trường hợp không có liên kết.

### Add-in UI

`TableGenWindow` nối callback Sync tất cả vào `RevitBatchCoordinator.Enqueue`. XAML thêm nút `Sync tất cả` cạnh `Cập nhật đã chọn` và giữ nguyên các điều khiển batch hiện có.

### Batch và cập nhật

Không tạo một engine đồng bộ mới. Tính năng tái sử dụng `RevitBatchCoordinator`, `TableGenExternalEventHandler` và `TableGenUpdateProcessor` để giữ nguyên transaction isolation, fingerprint, báo cáo tiến độ và xử lý lỗi hiện có.

## 6. Xử lý lỗi

- Batch đang chạy: lệnh bị vô hiệu hóa; coordinator vẫn giữ kiểm tra phòng vệ `BATCH_ALREADY_RUNNING`.
- Không có liên kết: không gọi `Enqueue`; trạng thái UI báo không có bảng để sync.
- Lỗi riêng lẻ: ghi kết quả theo Link ID, tiếp tục batch.
- Mất document đang làm việc: không cập nhật document khác.

## 7. Kiểm thử

Kiểm thử Presentation phải chứng minh:

1. Sync tất cả gửi đủ Link ID theo thứ tự nguồn.
2. Một bộ lọc làm ẩn dòng không làm thiếu Link ID trong Sync tất cả.
3. Sync tất cả không thay đổi các checkbox lựa chọn hiện tại.
4. Không có liên kết thì callback batch không được gọi.
5. `IsBusy = true` vô hiệu hóa Sync tất cả; kết thúc batch bật lại lệnh khi có liên kết.

Kiểm thử hồi quy hiện có tiếp tục xác nhận coordinator xử lý tuần tự, tạm dừng/tiếp tục/dừng, bỏ qua nguồn không đổi và cô lập lỗi từng view.

## 8. Tiêu chí nghiệm thu

1. Sau khi cài bản mới và khởi động Revit 2024 một lần, người dùng mở TableGen và bấm `Sync tất cả` mà không cần khởi động lại Revit cho các lần sync tiếp theo.
2. Tất cả liên kết đã nạp được xét, kể cả dòng đang bị ẩn bởi tìm kiếm hoặc filter.
3. Bảng Excel không đổi không bị dựng lại.
4. Bảng thay đổi được cập nhật theo thiết lập đã lưu.
5. Một bảng lỗi không chặn các bảng còn lại.
6. Không tự Save và không Synchronize with Central.
7. Bản đang cài được backup trước khi cài đè bản mới.
