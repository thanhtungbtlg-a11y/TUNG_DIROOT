# TUNG_DIROOT

TUNG_DIROOT là add-in độc lập cho Autodesk Revit 2024. `TableGen` đưa vùng dữ liệu Excel vào Drafting View, Legend hoặc phần Header của Schedule và có chế độ render thành ảnh. `DataLink` xuất parameter Revit sang workbook bảo vệ, xem trước rồi ghi lại các thay đổi an toàn. `SheetLink / Gen` xuất/nhập Sheet và View qua Excel để tạo/cập nhật Sheet, View, Viewport và gán revision theo batch. `Filter` lọc element theo category/family/type/parameter, chọn hoặc cô lập kết quả, xuất Excel và trực quan hóa trong view 3D riêng. `Parameter Loader` tạo shared parameter theo cấu hình; `ParaManager` quản lý inventory/binding/group/category và `Parameter ReOrdering` sắp lại parameter trong Family bằng Excel có backup. Add-in không dùng DLL, license, mã mở khóa hay dữ liệu của DiRoots, TungMEP hoặc TUNGMEP2. Add-in không có telemetry và không kết nối mạng.

## Trạng thái hỗ trợ

- Phiên bản được hỗ trợ duy nhất: Autodesk Revit 2024 trên .NET Framework 4.8.
- Build Release và assembly contract đã PASS với Revit API 2024 thật trên máy phát triển.
- Revit 2025 và 2026 nằm ngoài phạm vi sản phẩm hiện tại; bộ cài không tạo payload hoặc manifest cho hai phiên bản này.
- Checklist chạy trực tiếp trong Revit 2024 vẫn phải được người kiểm thử thực hiện trước khi đổi release candidate thành bản phát hành chính thức.

## Bắt đầu

Xem [hướng dẫn cài đặt](docs/install-guide-vi.md), [hướng dẫn TableGen/Parameter Loader](docs/user-guide-vi.md), [hướng dẫn DataLink](docs/user-guide-datalink.md), [hướng dẫn SheetLink / SheetGen](docs/user-guide-sheet-tools.md), [hướng dẫn Parameter Tools](docs/user-guide-parameter-tools.md), [hướng dẫn FamilyReviser](docs/user-guide-family-reviser.md) và [hướng dẫn Filter](docs/user-guide-filter.md). Workbook Parameter Loader mẫu nằm tại [samples/TUNG_DIROOT-ParameterLoader.xlsx](samples/TUNG_DIROOT-ParameterLoader.xlsx); hai dòng mẫu đều tắt để người dùng chủ động kiểm tra trước khi bật. Khi có lỗi, đối chiếu [troubleshooting](docs/troubleshooting-vi.md) và log tại `%LocalAppData%\TUNG_DIROOT\Logs`.

Nhà phát triển có thể chạy `tools\Test-All.ps1 -SkipRevitBuild` để kiểm tra Core, renderer, Presentation, workbook mẫu và audit không mạng. Chạy không có cờ để build/kiểm tra payload Revit 2024 bằng API thật. Cấu trúc và ranh giới an toàn được mô tả tại [architecture](docs/architecture.md); thông tin thư viện ở [third-party licenses](docs/third-party-licenses.md).

## Nguyên tắc an toàn

Mỗi import/update dùng transaction riêng. Recreate chỉ xóa element mang schema TUNG_DIROOT và đúng `LinkId`, vì vậy TextNote do người dùng tạo cùng view không nằm trong phạm vi xóa. Pause/stop chỉ có hiệu lực giữa hai view, không cắt ngang transaction. Auto Sync không Save, Save As hoặc Synchronize with Central. DataLink revalidate document/element/parameter/value trước khi ghi, chỉ cho chọn dòng `Sẵn sàng` và gom lượt áp dụng thành một Undo. Filter xử lý theo lô 500 element, chặn document khác, không sửa linked document và chỉ đặt màu trong view `TUNG_FILTER_*` do công cụ quản lý. Parameter Loader không tự sửa binding đang có và chỉ gán các dòng đã bật, đang ở trạng thái `Ready`. Uninstall không mở/chỉnh RVT và không xóa file Excel/TXT/profile người dùng.
