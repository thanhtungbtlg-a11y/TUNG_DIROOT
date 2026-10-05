# TUNG_DIROOT — Xử lý lỗi

Log theo user nằm ở `%LocalAppData%\TUNG_DIROOT\Logs`, quay vòng tối đa 5 file × 2 MB. Log chỉ gồm thời gian, phiên bản add-in/Revit, LinkId, đường dẫn nguồn, bước, mã kết quả và exception; không ghi nội dung cell Excel.

## Mã kết quả

- `FILE_MISSING`: đường dẫn nguồn không còn tồn tại hoặc relative path không resolve được. Dùng Relink thư mục hoặc chọn lại file.
- `FILE_LOCKED`: thiếu quyền hoặc workbook đang bị ứng dụng khác khóa. Đóng ứng dụng giữ lock, kiểm tra quyền rồi chạy lại.
- `FORMULA_CACHE_MISSING`: công thức không có cached value trong OpenXML. Mở/lưu bằng Excel hoặc dùng Excel High Fidelity.
- `FONT_FALLBACK`: máy thiếu font. Cài font hợp lệ hoặc chấp nhận font thay thế và kiểm tra kích thước text.
- `EXCEL_NOT_INSTALLED`: đã chọn bắt buộc Excel High Fidelity nhưng Microsoft Excel không được đăng ký trên máy. Cài/repair Excel hoặc đổi renderer sang `Automatic`/`BuiltIn`.
- `VIEW_NOT_EDITABLE`: view/element do user khác sở hữu trong workshared model. Relinquish/borrow đúng quy trình rồi cập nhật lại.
- `VIEW_DELETED`: view đích đã bị xóa. Tạo/import lại hoặc gỡ link hỏng.
- `SCHEMA_NEWER_THAN_ADDIN`: model chứa metadata từ add-in mới hơn; bản hiện tại chỉ đọc và từ chối ghi đè. Nâng cấp add-in.
- `UNKNOWN_ERROR`: lỗi chưa phân loại. Gửi log, Revit build, workbook mẫu tối giản và bước tái hiện.

## Add-in không xuất hiện

Đóng Revit, kiểm tra `%AppData%\Autodesk\Revit\Addins\<year>\TUNG_DIROOT.addin`, xác nhận `Assembly` là đường dẫn tuyệt đối và DLL tồn tại. Không copy RevitAPI.dll vào thư mục add-in. Nếu tải ZIP từ Internet, giải nén trước và kiểm tra Windows có block file hay không.

## Hình thức không giống Excel

Built-in renderer ưu tiên hoạt động không cần Excel. Chart, SmartArt và shape phức tạp có thể bị đơn giản hóa; dùng Excel High Fidelity khi cần hình thức giống Excel và kiểm tra Excel đã cài/hoạt động. 600 DPI rõ hơn nhưng file và thời gian lớn hơn khi dùng Built-in. Excel High Fidelity dùng kích thước raster do Excel xuất, nên hãy chọn Built-in nếu bắt buộc kiểm soát đúng DPI hoặc đen trắng. Font fallback có thể thay đổi xuống dòng.

## Update hoặc Auto Sync không chạy

Kiểm tra link có bật Auto Sync, fingerprint thực sự thay đổi và view có thể edit. Auto Sync chỉ enqueue khi document active; nó không Save hoặc Synchronize. Pause/stop không cắt ngang transaction hiện tại, vì vậy hãy chờ view hiện tại hoàn tất. Nếu registry DataStorage mất, add-in thử phục hồi từ marker trên view.

Xem [hướng dẫn sử dụng](user-guide-vi.md) và [kiến trúc](architecture.md) để hiểu phạm vi update an toàn.

## Parameter Loader không gán được

- `MissingDefinition`: kiểm tra `PARAMETER_GROUP` và `PARAMETER_NAME` có trùng chính xác với file Shared Parameters `.txt` đã chọn hay không.
- `InvalidCategory`: dùng mã Revit bắt đầu bằng `OST_`; category phải hỗ trợ project parameter trong model hiện tại.
- `ConfigurationError`: kiểm tra sheet `PARAMETER_LOADER`, đủ sáu header, `BINDING` chỉ là `INSTANCE`/`TYPE` và `CATEGORIES` không rỗng.
- `AlreadyBound`: parameter đã có trong model nên công cụ giữ nguyên, không tự sửa loại binding hoặc danh sách category.
- `ReviewRequired`: binding hiện tại khác với quy tắc Excel. Hãy kiểm tra thủ công; add-in chủ ý không `ReInsert` để tránh thay đổi âm thầm model.
- Nút gán không chạy: dòng phải vừa được bật trong Excel vừa có trạng thái `Ready` sau lần `Kiểm tra` mới nhất.

Nếu Shared Parameters TXT hoặc Excel đã bị chuyển, chọn lại đường dẫn rồi chạy `Kiểm tra`. Log chi tiết nằm tại `%LocalAppData%\TUNG_DIROOT\Logs`; gửi kèm hai file nguồn đã tối giản và phiên bản Revit khi cần tái hiện lỗi.
