# TUNG_DIROOT Missing Features Roadmap

**Goal:** Hoàn thiện các chức năng còn thiếu đã được người dùng liệt kê, không làm trùng TableGen, DataLink, Filter và Parameter Loader đang có.

**Execution:** Native/inline execution đã được người dùng chọn trước đó. Mỗi đợt dùng TDD, có commit riêng, build Revit 2024, checklist trực tiếp và bộ cài RC có thể rollback.

**Status 2026-10-03:** Đã đóng gói RC2 Revit 2024 v1.3.0 và fast-forward master cục bộ sau review độc lập; 502 test tự động PASS, build 0 warning/error, kiểm tra cài/backup/restore trong sandbox PASS. RC1 được giữ nguyên. RC2 sửa style/DataOnly TableGen, thêm Rebuild có xác nhận, chặn xóa ảnh dùng chung, stale Preview/identity, tạo Revision, Preview Sheet cũ→mới và Bulk V/G Update Existing/giữ thuộc tính không chọn. Checklist tương tác trong Revit còn `NOT RUN`. Phạm vi Bulk V/G hiện là Category/Filter; sao chép toàn bộ các tab V/G (Revit Link/DWG/Workset/Point Cloud và thiết lập bổ sung) vẫn là phần mở rộng còn lại, chưa được tuyên bố hoàn tất. Xem [báo cáo RC2](../../testing/release-1.3.0-rc2-report.md).

## Phạm vi đã đối chiếu

- Đã có: TableGen Excel, Drafting/Legend/Schedule, Image renderer, DataOnly/Recreate, link registry, đường dẫn tương đối, Relink, Auto Sync khi mở model, Sync tất cả, DataLink, Filter và Parameter Loader.
- Không tạo lại OneFilter vì nút `Filter` hiện có đã đảm nhiệm chức năng này.
- Các tính năng dưới đây là phần còn thiếu thực sự hoặc mới chỉ có lõi nhưng chưa có giao diện sử dụng.

## Thứ tự triển khai

1. **TableGen Advanced**
   - Preview vùng Excel và chẩn đoán trước khi import.
   - Advanced Style Mapping và company profile.
   - Quy tắc hiển thị số.
   - Kéo thả nhiều file.
   - Quản lý liên kết: mở view/file/thư mục, unlink, xóa link + view, đổi view đích, nhân cấu hình.
   - Theo dõi thay đổi nguồn khi cửa sổ đang mở; không tự Save/Sync Central.

2. **PDF/Word Import**
   - PDF nhiều trang dùng Revit `ImageTypeOptions`, chọn trang và 150/300/600 DPI.
   - Word dùng Microsoft Word COM tùy chọn để xuất PDF tạm; luôn đóng COM object; báo rõ khi máy không có Word.
   - Theo dõi nguồn và cập nhật lại trang đã liên kết.

3. **Bulk Visibility & Graphics Manager**
   - Chọn View, View Template hoặc Sheet để lấy các viewport.
   - Copy Category/Filter hoặc toàn bộ VG.
   - Preview khác biệt, Replace/Merge/Update Existing.
   - Sao lưu trạng thái cũ và Restore trong một Undo an toàn.

4. **Sheet Tools**
   - SheetLink: xuất/nhập dữ liệu Sheet/View hai chiều qua Excel với preview thay đổi.
   - SheetGen: tạo Sheet/View/Revision hàng loạt, kiểm tra trùng số sheet và thiếu title block.

5. **Parameter Tools**
   - ParaManager: quản lý Project/Shared/Family parameter, category và binding, không tự ghi đè binding khác loại.
   - ReOrdering: đánh lại thứ tự parameter trong family bằng quy tắc Excel, có backup và báo phần không thể đổi qua API.

6. **FamilyReviser và phát hành hợp nhất**
   - Đổi tên family/type hàng loạt, kiểm tra naming rule, duplicate type và nested family.
   - Xuất báo cáo Excel, preview trước khi áp dụng và một Undo cho mỗi batch.
   - Regression toàn bộ lệnh, checklist Revit 2024 và bộ cài hợp nhất có backup.

## Quy tắc an toàn chung

- Chỉ hỗ trợ Revit 2024 trong chuỗi triển khai này.
- Mọi Revit API từ cửa sổ modeless đi qua `ExternalEvent`.
- Không gọi API từ watcher/background thread.
- Mỗi mục rollback riêng; batch lỗi một mục không làm mất phần đã hoàn tất.
- Không tự Save, Save As hoặc Synchronize with Central.
- Không dùng DLL, license, mã nguồn hay tài nguyên của DiRoots/ModPlus.
- Trước khi cài đè luôn sao lưu bản đang cài và xuất SHA-256 cho bộ cài mới.
