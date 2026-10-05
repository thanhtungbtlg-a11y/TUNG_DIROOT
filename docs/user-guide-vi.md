# Hướng dẫn sử dụng TUNG_DIROOT TableGen

## Thêm Excel

Mở tab `TUNG_DIROOT`, chọn `TableGen`, rồi bấm `Thêm Excel`. Chọn một hoặc nhiều file `.xlsx`/`.xlsm`, hoặc kéo thả nhiều file vào cửa sổ; macro không được chạy. Cửa sổ liệt kê worksheet, Named Range, Print Area hoặc Used Range. Kiểm tra vùng và tên view trước khi import. Bảng Revit trên 5.000 ô sẽ hiện cảnh báo vì số lượng element có thể lớn.

Chọn đúng một dòng rồi bấm `Xem trước` để xem ảnh vùng Excel và danh sách chẩn đoán trước khi tạo element. Tool cảnh báo công thức lỗi như `#REF!`, công thức chưa có cached value, merge bất thường, drawing chưa hỗ trợ và bảng quá lớn; đồng thời ước lượng số TextNote/Line/FilledRegion/Image sẽ tạo. Cảnh báo không tự sửa file Excel.

Hai chế độ import:

- `RevitTable`: tạo TextNote, đường biên, FilledRegion trong Drafting/Legend; với Schedule chỉ ghi phần Header. Dữ liệu là element Revit nên có thể kiểm tra/chỉnh theo khả năng của Revit.
- `Image`: render vùng thành PNG rồi đặt ImageInstance. Chọn 150, 300 hoặc 600 DPI; 300 DPI là cân bằng phổ biến. Chế độ này không dùng cho Schedule.

Drafting được tạo mới. Legend cần một Legend mẫu trong project để duplicate vì Revit API không tạo Legend trống trực tiếp. Schedule chỉ thay đổi Header, không chạm body/data fields.

## Renderer và độ trung thực

`Automatic` ưu tiên Excel High Fidelity khi máy có Excel và tự chuyển sang Built-in nếu Excel render thất bại. `BuiltIn` không cần cài Excel, phù hợp dữ liệu, font, border, fill, merge và ảnh cơ bản; chart, shape phức tạp và SmartArt có thể không giống Excel hoàn toàn. `ExcelHighFidelity` bắt buộc dùng Excel, mở workbook read-only, tắt macro và luôn đóng COM object sau khi hoàn tất; nếu máy không có Excel, dòng import báo `EXCEL_NOT_INSTALLED`. DPI và tùy chọn đen trắng được áp dụng chính xác bởi Built-in; Excel High Fidelity dùng kích thước ảnh do Excel xuất. Manager hiển thị renderer yêu cầu và renderer thực tế của lần chạy gần nhất. Nếu font không tồn tại, Built-in dùng font thay thế và trả cảnh báo `FONT_FALLBACK`.

## Manager, cập nhật và Auto Sync

TableGen là cửa sổ modeless duy nhất. Có thể tìm theo đường dẫn, worksheet, vùng hoặc view; lọc `Ready`, `Changed`, `Missing`, `Error`. Chọn link rồi cập nhật:

- `DataOnly`: chỉ đổi text khi cấu trúc đã xác nhận tương thích.
- `Recreate`: xóa đúng element có cùng `LinkId` và dựng lại. Element do người dùng tạo không có tag sẽ được giữ nguyên.
- Image luôn thay cặp ImageInstance/ImageType được tag thay vì data-only.

Mỗi view có transaction riêng. `Tạm dừng` hoàn tất view hiện tại rồi dừng trước view kế; `Tiếp tục` chạy phần còn lại; `Dừng` không khởi động view kế tiếp. Một view lỗi rollback riêng, batch vẫn giữ kết quả những view trước.

Auto Sync chỉ xét link đã bật, nguồn thay đổi và view có thể chỉnh sửa. Khi mở model, nó đợi document active/Idling rồi enqueue update; không tự lưu model và không Synchronize with Central.

Khi cửa sổ TableGen đang mở, tool theo dõi các file Excel đã liên kết. Sau khi Excel ngừng ghi khoảng một giây, danh sách được kiểm tra lại và trạng thái chuyển sang `Changed`, `Missing` hoặc `Ready`. Đây chỉ là phát hiện thay đổi: tool không tự cập nhật bảng, không tự Save và không Synchronize with Central. Nếu đang chạy batch, yêu cầu kiểm tra được giữ lại đến điểm an toàn.

### Quản lý từng liên kết

Tích đúng một dòng để dùng nhóm lệnh liên kết:

- `Mở View`: chuyển đến View đích còn tồn tại.
- `Mở Excel`: mở file nguồn bằng ứng dụng mặc định.
- `Mở thư mục`: mở Explorer và chọn file nguồn.
- `Đổi View đích`: chọn một Drafting View khác cho link Drafting, hoặc một Legend khác cho link Legend. Tool dựng lại ở View mới trong TransactionGroup; nếu bất kỳ bước nào lỗi, registry, marker và nội dung View cũ được rollback. Schedule chưa hỗ trợ đổi View đích.
- `Nhân cấu hình`: mở một dòng mới trong `Thêm Excel`, giữ file/worksheet/vùng/chế độ/DPI/renderer/profile và thêm hậu tố `- Copy` cho tên View. Người dùng được sửa mọi trường trước khi bấm Import; thao tác này không tự tạo View.
- `Unlink — giữ View`: bỏ bản ghi liên kết và marker, nhưng giữ View cùng toàn bộ nội dung bảng hiện tại.
- `Xóa link + View`: sau bước xác nhận, xóa đúng View đích và bản ghi liên kết trong một transaction. Có thể dùng Undo của Revit.

Các lệnh bị khóa nếu không tích hoặc tích nhiều hơn một dòng. Nếu file hoặc View đã mất, tool báo trạng thái tương ứng thay vì tác động sang liên kết khác.

### Profile trình bày

Ở cửa sổ `Thêm Excel`, chọn một dòng rồi bấm `Quản lý profile…`. Tool đọc các style thực sự có trong vùng Excel và danh sách Text Type/Line Style của model hiện tại; nhập tên, đặt quy tắc số, ánh xạ từng style rồi bấm `Lưu profile`. Khi đóng cửa sổ quản lý, profile vừa lưu được chọn cho dòng hiện tại và xuất hiện trong cột `Profile công ty`.

Profile có thể quy định dấu thập phân/phân nhóm chữ số và ánh xạ font, cỡ chữ, Bold/Italic, kiểu đường Excel sang Text Type/Line Style sẵn có trong Revit. Profile được lưu theo tên; link nhớ profile đã chọn để lần Sync/Recreate tiếp theo dùng cùng quy tắc. Nếu profile hoặc Revit style đã bị xóa, tool fallback về định dạng Excel/tự tạo style mặc định và không làm hỏng link.

Ánh xạ được tra theo style Excel gốc, kể cả khi bật Đen trắng. Text Type/Line Style được chọn rõ trong profile có ưu tiên cao hơn chuyển màu Đen trắng; tool không sửa type/style dùng chung trong model. Nếu cần toàn bộ bảng đen trắng, hãy ánh xạ sang type/style màu đen. Schedule cũng dùng profile khi tạo và Recreate, sao chép font/cỡ chữ/Bold/Italic/Underline/màu từ Text Type và dùng Line Style đã ánh xạ cho border. Chế độ cập nhật được lưu cho các lần Sync; lần import đầu luôn khởi tạo đầy đủ bảng mới.

Với Drafting/Legend, DataOnly đối chiếu đúng các ô thực sự tạo TextNote (không tính ô trống và ô con của vùng gộp). Khi nội dung mới cần thêm/bớt TextNote hoặc tag bị thiếu/trùng, tool báo `DATA_ONLY_TEXT_SCOPE_CHANGED` trước khi sửa model. Nếu cấu trúc hàng/cột/merge đổi, tool báo `DATA_ONLY_STRUCTURE_CHANGED` và không tự dựng lại.

Muốn dựng lại có chủ ý: tích đúng một liên kết, bấm **Dựng lại bảng**, đọc cảnh báo rồi xác nhận. Tool đọc Excel lại kể cả nguồn không đổi; giữ nguyên View và chế độ cập nhật đã lưu. Phần tử thủ công không gắn thẻ được giữ; nội dung liên kết và header Schedule có thể mất chỉnh sửa thủ công. Nếu Image Type của ảnh Excel đang được dùng bởi ảnh ngoài liên kết, tool dừng với `LINKED_IMAGE_TYPE_SHARED` để không xóa ảnh dùng lại.

### Sync tất cả liên kết đã nạp

1. Mở model chứa các liên kết Excel đã tạo bằng TUNG_DIROOT.
2. Mở `TUNG_DIROOT > TableGen`.
3. Bấm `Sync tất cả`.

TableGen kiểm tra mọi liên kết trong model, kể cả liên kết đang bị ẩn bởi ô tìm kiếm hoặc bộ lọc. Nguồn không đổi được bỏ qua; nguồn thay đổi được cập nhật theo chế độ đã lưu. Một liên kết lỗi không dừng các liên kết còn lại. Lệnh không tự Save model và không Synchronize with Central.

Sau khi cài một DLL mới, cần khởi động lại Revit một lần để Revit nạp DLL. Không cần khởi động lại Revit trước các lần `Sync tất cả` tiếp theo.

## Đường dẫn và Relink

Khi model có đường dẫn local ổn định và cùng volume, nguồn có thể lưu tương đối; trường hợp cloud/UNC hoặc model chưa lưu sẽ dùng absolute path kèm cảnh báo. Nếu Excel bị chuyển, chọn `Relink thư mục`. Công cụ khớp tên file rồi xác minh worksheet + vùng. Nếu có nhiều ứng viên ngang nhau, kết quả là `Ambiguous` và không tự chọn.

Workbook mẫu ở [samples](../samples/README.md). Trước khi dùng project thật, nên thử đủ Drafting, Legend seed, Schedule, Data-only/Recreate và pause/stop trên bản sao model.

## PDF / Word Link

Mở `TUNG_DIROOT > PDF / Word Link`, hoặc bấm `PDF / Word` trong cửa sổ TableGen. Kéo thả hay chọn một hoặc nhiều file `.pdf`, `.doc`, `.docx`, `.docm`; mỗi tài liệu tạo một Drafting View và một liên kết riêng.

1. Nhập danh sách trang, ví dụ `1,3,5-7`. Trang trùng được bỏ và dải ngược `7-5` được chuẩn hóa. Mỗi liên kết tối đa 500 trang.
2. Chọn 150, 300 hoặc 600 DPI. 300 DPI là lựa chọn cân bằng; 600 DPI tạo ảnh nặng hơn.
3. Chọn số cột từ 1 đến 8. Các trang được xếp theo hàng, giữ kích thước và chừa khoảng cách cố định.
4. Đặt tên View và chọn ưu tiên đường dẫn tương đối nếu model đã lưu local ổn định.
5. Bấm `Nhập và liên kết`. PDF dùng trực tiếp API PDF của Revit 2024. Word được mở read-only, tắt macro, xuất sang PDF cache rồi đóng Word trước khi Revit tạo phần tử.

Cửa sổ quản lý hiển thị `Ready`, `Changed`, `Missing` hoặc `Error`. Theo dõi file chỉ cập nhật trạng thái; không tự sửa model. Tích link rồi dùng:

- `Cập nhật đã chọn`: chuẩn bị lại Word/PDF và thay đúng các ImageType/ImageInstance mang LinkId trong View cũ. Mỗi link có transaction riêng, nên một link lỗi không đảo kết quả link trước.
- `Mở View` và `Mở nguồn`: mở đúng đối tượng đang quản lý.
- `Unlink — giữ View`: bỏ registry/marker/tag nhưng giữ View và hình hiện tại.
- `Xóa link + View`: xóa View và nội dung sinh bởi link trong một transaction, có thể Undo trong Revit.

Nếu máy chưa có Microsoft Word, tool báo `WORD_NOT_INSTALLED`; nhập PDF vẫn hoạt động. Tool không chạy macro, không tự Save model và không Synchronize with Central. Sau khi cài DLL mới cần khởi động lại Revit một lần.

## Bulk Visibility & Graphics

Mở `TUNG_DIROOT > Bulk V/G`. Chọn một View hoặc View Template làm nguồn. Tool đọc Category và View Filter đang có ở nguồn; sau đó chọn một hay nhiều đích:

- `View`: áp dụng trực tiếp cho View đó.
- `ViewTemplate`: áp dụng vào template.
- `Sheet`: tự mở rộng thành các View đang được đặt bằng Viewport trên Sheet; View trùng giữa nhiều Sheet chỉ chạy một lần.

Chọn Category/Filter và nhóm thuộc tính cần sao chép: Visibility, Projection line, Cut line, Fill/Pattern, Transparency/Halftone. Bấm `Xem trước` để xem từng điểm khác nhau hoặc trường hợp Category không hỗ trợ ở View đích.

- `Merge`: cập nhật Filter tích chọn đã có và thêm Filter tích chọn chưa có; giữ Filter khác và thứ tự hiện tại ở View đích. Filter mới được thêm cuối danh sách theo thứ tự nguồn.
- `Replace`: **chỉ các Filter tích chọn**, không phải toàn bộ Filter nguồn, trở thành bộ Filter của đích theo thứ tự nguồn; Filter ngoài lựa chọn bị loại. Preview có dòng riêng cho từng Filter sẽ xóa và cho việc sắp xếp lại, kể cả khi các nhóm override đang giống nhau. Apply yêu cầu xác nhận trước khi xóa. Nếu chỉ chọn Category, danh sách Filter không đổi.
- `Update Existing`: chỉ cập nhật Filter tích chọn đã có ở đích; Filter chưa có hiện `Skipped` trong Preview và bị bỏ qua khi Apply. Không thêm, xóa hay đổi thứ tự Filter. Category vẫn cập nhật bình thường.

Trong **mọi chế độ**, Category và Filter đã có chỉ thay các nhóm thuộc tính đã bật; các nhóm chưa bật được giữ nguyên, kể cả khi Replace phải tháo/thêm lại Filter để đổi thứ tự. Visibility bao gồm cả visibility và enabled của Filter. Filter mới ở Merge/Replace chưa có thiết lập đích để giữ nên dùng mặc định Revit cho nhóm chưa bật. Preview không dự đoán được mọi hạn chế của View Template; lỗi Apply vẫn được rollback riêng từng đích.

`Apply đã chọn` chạy từng View trong transaction riêng nhưng gom vào một TransactionGroup, nên một View lỗi không dừng View khác và một lệnh Undo có thể đảo cả lượt Apply. Trước khi sửa, tool giữ bản sao trong bộ nhớ; `Khôi phục lần Apply gần nhất` phục hồi Category đã tác động và toàn bộ danh sách Filter của các View đã thành công. Bản sao này chỉ tồn tại trong cửa sổ hiện tại; sau khi đóng cửa sổ vẫn có thể dùng Undo của Revit.

View đang bị View Template kiểm soát có thể từ chối override trực tiếp; hãy chọn chính View Template làm đích. Tool không tự Save model và không Synchronize with Central.

## SheetLink / SheetGen

Mở `TUNG_DIROOT > Excel Tools > SheetLink / Gen`. Bấm **Xuất model ra Excel** để tạo các tab `SHEETS`, `VIEWS`, `REVISION`; workbook hai tab cũ vẫn đọc được. Chỉnh dòng cần chạy thành `APPLY=TRUE`, sau đó **Đọc & xem trước** để đối chiếu giá trị cũ → mới trước Apply. Công cụ hỗ trợ tạo/cập nhật Sheet và Revision, gán revision có sẵn hoặc vừa tạo trong cùng lượt, tạo Drafting/Floor/Ceiling View và đặt Viewport theo tọa độ mm. Xem quy trình và quy tắc cột tại [hướng dẫn SheetLink / SheetGen](user-guide-sheet-tools.md).

## Parameter Loader

Mở `TUNG_DIROOT > Parameter Loader`. Công cụ cần hai nguồn riêng:

- File Shared Parameters `.txt` là nguồn định nghĩa chính thức: GUID, tên parameter và kiểu dữ liệu. Công cụ chỉ đọc file này trong lúc kiểm tra/gán và trả lại đúng đường dẫn Shared Parameters mà Revit đang dùng.
- Workbook `.xlsx`/`.xlsm` là danh sách quy tắc: parameter nào được bật, gán Instance hay Type, thuộc nhóm nào trong Properties và áp dụng cho category nào. Macro không được chạy.

Dùng [workbook mẫu](../samples/TUNG_DIROOT-ParameterLoader.xlsx) hoặc sheet tên chính xác `PARAMETER_LOADER` với sáu cột:

| Cột | Nội dung |
| --- | --- |
| `ENABLED` | `TRUE`, `YES`, `1` hoặc `X` để bật; giá trị khác là tắt. |
| `PARAMETER_GROUP` | Tên group trong file Shared Parameters `.txt`. |
| `PARAMETER_NAME` | Tên definition trong group trên. |
| `BINDING` | `INSTANCE` hoặc `TYPE`. |
| `REVIT_GROUP` | Mã BuiltInParameterGroup, ví dụ `PG_DATA`. |
| `CATEGORIES` | Một hoặc nhiều mã `OST_*`, ngăn cách bằng dấu chấm phẩy. |

Quy trình an toàn:

1. Chọn file Shared Parameters `.txt` và workbook Excel.
2. Bấm `Kiểm tra`. Xem trạng thái, lỗi cấu hình và category trên từng dòng.
3. Chỉ bật những dòng cần gán trong Excel, lưu file rồi kiểm tra lại.
4. Bấm `Gán parameter`. Chỉ dòng bật có trạng thái `Ready` mới được thực thi.
5. Nếu cần quay lại toàn bộ lần gán, dùng một lần `Undo` trong Revit.

Các trạng thái chính: `Ready` có thể gán; `AlreadyBound` đã tồn tại và được giữ nguyên; `ReviewRequired` cần người dùng xác nhận khác biệt; `MissingDefinition` không tìm thấy definition trong TXT; `InvalidCategory` có mã category sai/không bind được; `ConfigurationError` là quy tắc Excel không hợp lệ; `AssignmentFailed` là Revit từ chối dòng đó. Công cụ không tự đổi Instance thành Type, không tự thêm category vào binding đang có và không ghi đè binding cũ.

## ParaManager và Parameter ReOrdering

`ParaManager` xuất inventory Project/Family Parameter, tạo shared parameter mới và cập nhật an toàn group/category bằng quy trình Excel → Preview → Apply. `Parameter ReOrdering` chạy trong Family Editor để đổi thứ tự parameter trong từng group, luôn tạo workbook backup trước Apply. Xem [hướng dẫn Parameter Tools](user-guide-parameter-tools.md) để biết quy tắc cột và giới hạn API.

## FamilyReviser

`FamilyReviser` xuất loadable Family và Family Type ra Excel, kiểm tra tên trùng, ký tự cấm, dữ liệu stale, in-place family và quy ước tên regex trước khi đổi hàng loạt. Công cụ tạo workbook backup, dùng transaction riêng từng dòng và gom thành một Undo. Xem [hướng dẫn FamilyReviser](user-guide-family-reviser.md).
