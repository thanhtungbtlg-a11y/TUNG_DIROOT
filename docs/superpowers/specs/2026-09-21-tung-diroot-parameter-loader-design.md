# TUNG_DIROOT Parameter Loader Design

## 1. Mục tiêu

Bổ sung công cụ **Parameter Loader** vào add-in TUNG_DIROOT cho Autodesk Revit 2024. Công cụ đọc định nghĩa từ file Revit Shared Parameter (`.txt`), đọc quy tắc gán từ Excel, kiểm tra trạng thái hiện tại của project và chỉ tạo những binding mới an toàn mà người dùng đã chọn.

Parameter Loader không nhập giá trị parameter, không tự sửa binding đã tồn tại và không thay đổi chức năng TableGen.

## 2. Phạm vi phát hành

- Chỉ hỗ trợ Autodesk Revit 2024 trên .NET Framework 4.8, giống sản phẩm TUNG_DIROOT hiện tại.
- Chỉ chạy trong Revit project document đang hoạt động; family document và trạng thái không có active document phải được từ chối bằng thông báo tiếng Việt rõ ràng.
- Hỗ trợ workbook `.xlsx` và `.xlsm` theo chế độ chỉ đọc; macro không được thực thi.
- Không kết nối mạng, không telemetry và không phụ thuộc phần mềm DiRoots.
- File cấu hình mẫu được đóng gói cùng tài liệu/bộ cài, không phải dependency runtime.

## 3. Trải nghiệm người dùng

### 3.1 Ribbon

Trong tab `TUNG_DIROOT`, tạo panel `Parameter Tools` và nút `Parameter Loader`. Nút này độc lập với `TableGen`; lỗi hoặc trạng thái cửa sổ của công cụ này không được đóng hay thay đổi cửa sổ TableGen.

### 3.2 Luồng thao tác

1. Người dùng mở một Revit project và chạy `Parameter Loader`.
2. Chọn file Shared Parameter `.txt` và file Excel cấu hình.
3. Nhấn `Kiểm tra`.
4. Công cụ đọc hai nguồn, đối chiếu binding hiện tại và hiển thị từng parameter cùng trạng thái/hành động dự kiến.
5. Người dùng tìm kiếm, lọc, chọn/bỏ chọn/đảo lựa chọn.
6. Nút `Gán parameter` chỉ được bật khi có ít nhất một dòng ở trạng thái có thể gán được chọn.
7. Hộp xác nhận cuối cùng nêu số parameter sẽ tạo và số dòng bị bỏ qua.
8. Công cụ thực hiện gán và hiển thị báo cáo tổng hợp; người dùng có thể xem nguyên nhân của từng dòng không thành công.

Đường dẫn hai file gần nhất được lưu trong user settings. Nếu đường dẫn không còn tồn tại, giao diện vẫn mở bình thường và yêu cầu chọn lại file.

### 3.3 Bố cục cửa sổ

Cửa sổ WPF có kích thước mặc định đủ lớn, có `MinWidth`/`MinHeight`, co giãn theo màn hình làm việc và không che nút ở DPI 100–200%. Thanh chọn nguồn nằm phía trên, bảng kết quả chiếm phần diện tích co giãn ở giữa, bộ lọc/tìm kiếm nằm ngay trên bảng và thanh thao tác luôn nhìn thấy ở cuối.

Bảng xem trước có các cột:

- Chọn
- Trạng thái
- Parameter Group
- Parameter Name
- GUID
- Binding (`Instance`/`Type`)
- Revit Group
- Categories
- Hành động dự kiến hoặc nguyên nhân

Các thao tác hàng loạt gồm `Chọn tất cả hợp lệ`, `Bỏ chọn` và `Đảo lựa chọn hợp lệ`. Dòng không thể gán không được checkbox chọn.

## 4. Hợp đồng Excel

Workbook phải có worksheet tên `PARAMETER_LOADER`. Hàng đầu tiên chứa các header không phân biệt hoa thường và được chuẩn hóa khoảng trắng:

| Header | Bắt buộc | Nội dung |
|---|---:|---|
| `ENABLED` | Không | `TRUE/YES/1/X` để bật; ô trống hoặc giá trị khác là tắt. |
| `PARAMETER_GROUP` | Có | Tên group trong file Shared Parameter. |
| `PARAMETER_NAME` | Có | Tên definition trong group tương ứng. |
| `BINDING` | Có | Chỉ chấp nhận `INSTANCE` hoặc `TYPE`. |
| `REVIT_GROUP` | Có | Mã nhóm Revit ổn định, ví dụ `PG_DATA`, được ánh xạ sang group API Revit 2024. |
| `CATEGORIES` | Có | Một hoặc nhiều mã `BuiltInCategory`, phân cách bằng dấu chấm phẩy, ví dụ `OST_Doors;OST_Windows`. |

Quy tắc đọc:

- Bỏ qua hàng hoàn toàn trống.
- Giữ số dòng Excel để thông báo lỗi chính xác.
- Trim khoảng trắng quanh mọi giá trị và mỗi category key.
- Không dùng tên category đã dịch làm khóa cấu hình; chỉ mã `OST_*` bảo đảm ổn định trên mọi ngôn ngữ Revit.
- Một dòng có category trùng được chuẩn hóa thành một tập duy nhất.
- Hai dòng cấu hình cùng trỏ tới một definition được đánh dấu `Cấu hình trùng`; không dòng nào trong nhóm trùng được tự động gán.
- Thiếu worksheet/header, giá trị binding/group không hợp lệ hoặc danh sách category rỗng là lỗi kiểm tra nguồn, không làm Revit crash.

Bộ cài cung cấp workbook mẫu có header, chú thích và một số dòng ví dụ đang tắt.

## 5. Trạng thái và quy tắc đối chiếu

Definition được xác định từ cặp `PARAMETER_GROUP + PARAMETER_NAME`; GUID lấy từ Shared Parameter definition, không lấy từ Excel.

Mỗi dòng sau khi kiểm tra nhận đúng một trạng thái:

1. `Sẵn sàng gán`: definition tồn tại, GUID/tên chưa xung đột, binding và toàn bộ category hợp lệ.
2. `Đã gán`: project đã có cùng GUID, cùng kiểu Type/Instance và tập category hiện tại chứa toàn bộ category yêu cầu. Category thừa trong project không phải lỗi.
3. `Cần xem xét`: cùng GUID nhưng khác tên, trùng tên nhưng khác GUID, sai Type/Instance, hoặc binding hiện tại thiếu category yêu cầu. Công cụ không tự sửa.
4. `Thiếu definition`: không tìm thấy đúng group/name trong Shared Parameter.
5. `Category không hợp lệ`: mã category không tồn tại trong Revit 2024, category không có trong document hoặc `AllowsBoundParameters` là false.
6. `Cấu hình lỗi`: Excel thiếu/sai trường hoặc có definition trùng dòng.
7. `Gán thất bại`: chỉ phát sinh khi Revit từ chối một dòng vốn đã vượt qua kiểm tra; lưu thông báo exception đã làm sạch.

Chỉ trạng thái `Sẵn sàng gán` được chọn để thực thi. Parameter có cùng tên nhưng khác GUID tuyệt đối không được tạo tự động.

## 6. Kiến trúc

### 6.1 Core không phụ thuộc Revit

Tạo vùng chức năng `TungDiroot.Core.Parameters` gồm:

- model cho rule Excel, shared definition metadata, existing-binding snapshot, validation issue và assignment plan;
- reader cấu hình Excel dựa trên Open XML hiện có;
- parser metadata Shared Parameter `.txt` chỉ phục vụ xem trước/đối chiếu nguồn;
- planner thuần xác định trạng thái và hành động dự kiến từ ba đầu vào: definition, rule, binding snapshot.

Planner không gọi Revit API nên toàn bộ ma trận trạng thái được kiểm thử tự động.

### 6.2 Presentation

Tạo ViewModel riêng cho Parameter Loader. ViewModel quản lý đường dẫn, collection dòng, filter, lựa chọn hợp lệ, command và báo cáo hiển thị; không giữ `Document`, `UIApplication` hoặc object Revit API.

### 6.3 Revit add-in

Tạo command, WPF window và adapter Revit riêng:

- `RevitBindingSnapshotReader` chuyển `Document.ParameterBindings` thành model Core.
- `SharedDefinitionResolver` tạm đặt `Application.SharedParametersFilename`, gọi API mở definition file và luôn khôi phục giá trị cũ trong `finally`, kể cả khi đọc/gán lỗi.
- `RevitParameterAssignmentExecutor` tạo `CategorySet`, `InstanceBinding` hoặc `TypeBinding`, ánh xạ `REVIT_GROUP` và gọi `BindingMap.Insert`.
- `Command_ParameterLoader` kiểm tra document và điều phối giao diện/kiểm tra/thực thi trong Revit API context hợp lệ.

Không chia sẻ singleton cửa sổ với TableGen. Không chạy COM Excel; chỉ Open XML read-only.

## 7. Transaction và phục hồi lỗi

- Một `TransactionGroup` bao toàn bộ lượt gán để Revit hiển thị một bước Undo.
- Mỗi parameter dùng một `Transaction` riêng.
- Nếu một parameter lỗi, transaction đó rollback, lỗi được ghi vào kết quả và các parameter sau tiếp tục.
- Sau khi xử lý hết, group được `Assimilate`; nếu lỗi cấp hệ thống khiến không thể tiếp tục an toàn, group rollback và báo thất bại toàn lượt.
- Không Save, Save As hoặc Synchronize with Central.
- Không gọi `BindingMap.ReInsert` trong phiên bản này.

## 8. Logging và thông báo

Dùng rolling logger hiện có, bổ sung event cho: kiểm tra nguồn, trạng thái planner, bắt đầu/kết thúc batch, kết quả từng parameter và việc khôi phục Shared Parameter path. Log không ghi nội dung workbook ngoài những trường cần chẩn đoán và không ghi dữ liệu model ngoài tên/GUID parameter.

Thông báo người dùng bằng tiếng Việt, còn mã trạng thái nội bộ ổn định bằng tiếng Anh để test/log không phụ thuộc bản dịch.

## 9. Settings và tương thích

Mở rộng `UserSettings` bằng các trường nullable/default cho đường dẫn gần nhất. Deserialization file settings cũ phải tiếp tục thành công. Không lưu lựa chọn checkbox hoặc dữ liệu binding của model vào settings.

Ribbon startup phải tránh tạo nút trùng khi add-in khởi động lại. Bộ kiểm tra assembly contract phải xác nhận cả `Command_TableGen` và `Command_ParameterLoader` triển khai `IExternalCommand`.

## 10. Kiểm thử

### 10.1 Tự động

- Excel hợp lệ, header khác hoa thường, khoảng trắng, category trùng và hàng tắt.
- Thiếu worksheet/header, binding/group sai, category rỗng và cấu hình definition trùng.
- Shared Parameter parser xử lý group, GUID, definition không tồn tại và file lỗi.
- Planner bao phủ đầy đủ bảy trạng thái, đặc biệt xung đột cùng tên khác GUID, Type/Instance mismatch và thiếu category.
- ViewModel chỉ cho chọn dòng hợp lệ, filter/search đúng và chỉ bật Assign khi có lựa chọn.
- Settings cũ vẫn đọc được sau khi thêm trường mới.
- Kiểm tra layout cửa sổ ở kích thước/DPI mục tiêu bằng contract test hiện có.
- Release/manifest/assembly contract có nút, icon, command mới và không đóng gói RevitAPI DLL.

### 10.2 Checklist Revit 2024

- Project trống và project đã có binding.
- Project tiếng Anh và Revit UI ngôn ngữ khác nếu có môi trường thử.
- Instance và Type binding trên một/multiple categories.
- Family document, không có active document và category không bind được.
- Hủy hộp thoại, hủy xác nhận và Undo cả batch.
- Một dòng bị Revit từ chối nhưng các dòng sau vẫn hoàn tất.
- Shared Parameters Filename trước và sau lệnh giống nhau trong cả luồng thành công lẫn lỗi.
- TableGen vẫn mở, nạp và sync bình thường sau khi cài bản mới.

## 11. Bộ cài và tài liệu

- Tăng phiên bản release theo semantic version cho tính năng mới.
- Cập nhật hướng dẫn cài đặt/sử dụng/troubleshooting và checklist Revit 2024.
- Stage workbook mẫu cùng tài liệu cài đặt; không đặt workbook mẫu vào thư mục add-in runtime nếu Revit không cần đọc nó.
- Tạo backup bộ cài/release trước đó trước khi cài đè bản thử nghiệm.

## 12. Tiêu chí hoàn thành

Tính năng hoàn thành khi:

- build Revit 2024 và toàn bộ test tự động pass;
- workbook mẫu được đọc và tạo plan đúng;
- command/nút mới xuất hiện mà TableGen không thay đổi hành vi;
- checklist trực tiếp trong Revit xác nhận tạo mới, bỏ qua binding đã có, chặn mismatch và Undo batch;
- Shared Parameter path của Revit luôn được khôi phục;
- tạo được bộ cài TUNG_DIROOT mới có backup và hướng dẫn sử dụng.
