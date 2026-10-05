# TUNG_DIROOT architecture

TUNG_DIROOT tách thành bốn assembly: `Core` chứa Excel model, fingerprint, planner và state machine không phụ thuộc Revit; `Rendering` tạo PNG built-in hoặc bridge Excel COM tùy chọn; `Presentation` chứa MVVM có thể unit test; `Addin` là adapter Revit/WPF. Bản được hỗ trợ build cho Revit 2024 trên .NET Framework 4.8.

## Import pipeline

OpenXml reader đọc catalog/range mà không chạy macro. ViewCreationPlanner chặn kết hợp không hợp lệ trước transaction. Mỗi request mở transaction riêng, tạo/duplicate view, chọn writer Drafting/Legend/Schedule/Image, tag element và lưu `LinkRecord`. Fingerprint canonical gồm worksheet, normalized range, displayed cell value/style, merge và drawing relationship ID trong vùng.

Link registry nằm trong một DataStorage tên `TUNG_DIROOT.LinkRegistry`, schema GUID cố định. Mỗi target view giữ marker đầy đủ để phục hồi registry. Schema version tương lai chỉ đọc; save từ bản cũ bị chặn. Setting/log nằm ngoài model theo user.

## Update safety

ElementLinkTagger gắn `LinkId`, `CellKey`, `Role`. `LinkedElementCollector` chỉ chọn schema đúng và exact LinkId. Data-only đổi TextNote/Schedule text; structure không xác minh hoặc image chuyển sang Recreate. Recreate xóa ID từ collector, không dùng view-wide delete và không tạo Revit Group. Mỗi link rollback riêng.

Manager modeless chỉ gửi yêu cầu qua `ExternalEvent`; không gọi Revit API trực tiếp từ WPF event. Coordinator xử lý tối đa một link mỗi lần Execute rồi raise event tiếp theo. State machine bảo đảm pause/stop chỉ chuyển trạng thái giữa transactions. Auto Sync nhận DocumentOpened, chỉ enqueue document key, sau đó kiểm tra tại Idling khi document active. Không có save/sync/network API.

## Parameter Loader pipeline

`ParameterRuleWorkbookReader` đọc sheet `PARAMETER_LOADER` read-only; `SharedParameterTextParser` đọc metadata TXT với kiểm tra UTF-8 rồi UTF-16. `ParameterAssignmentPlanner` kết hợp quy tắc, definition và snapshot binding của document để tạo kế hoạch không phụ thuộc Revit. WPF chỉ hiển thị kế hoạch; mọi truy cập Revit chạy qua `ExternalEvent`.

Executor tạm thời đặt `Application.SharedParametersFilename`, mở definition bằng API rồi luôn phục hồi giá trị cũ. Mỗi parameter `Ready` dùng transaction riêng bên trong một `TransactionGroup`; cuối batch `Assimilate` tạo một bước Undo. Binding đang tồn tại chỉ được báo `AlreadyBound`/`ReviewRequired`, không gọi `ReInsert` và không tự sửa category. Một dòng lỗi rollback riêng, những dòng khác tiếp tục.

## Build và release

Build resolver yêu cầu RevitAPI/UI 2024 thật và luôn đặt `Private=false`; contract test từ chối payload chứa Revit API. Contract Parameter Loader còn kiểm tra command, executor và resource icon trong assembly. Stage chỉ tạo thư mục `2024`, ghi size/SHA-256 và commit vào `release.json`; sample và tài liệu nằm ngoài thư mục runtime. Setup per-user và portable tạo manifest absolute tại thời điểm cài. Release gate chính thức fail nếu checklist Revit 2024 là FAIL/NOT RUN; cờ `-ReleaseCandidate` chỉ tạo artifact thử có nhận diện rõ ràng và không thay thế kiểm thử trực tiếp.

Xem [release matrix](testing/release-matrix.md) và [third-party licenses](third-party-licenses.md).
