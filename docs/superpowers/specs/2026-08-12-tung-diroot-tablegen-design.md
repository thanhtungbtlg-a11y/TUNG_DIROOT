# Đặc tả thiết kế TUNG_DIROOT TableGen

Ngày: 2026-08-12  
Trạng thái: Thiết kế đã được người dùng duyệt trong hội thoại; chờ duyệt lại tài liệu viết.

## 1. Mục tiêu

Tạo một Revit add-in độc lập tên `TUNG_DIROOT`, tập trung vào việc nhập và duy trì liên kết giữa bảng Excel với view Revit. Add-in phải:

- Hoạt động trên Revit 2024, 2025 và 2026.
- Không yêu cầu Microsoft Excel được cài trên máy.
- Nhập `.xlsx` và `.xlsm` theo hai chế độ: `Bảng Revit` và `Ảnh`.
- Tạo nội dung trong Drafting View, Legend View và Schedule View theo ma trận tương thích đã quy định.
- Hỗ trợ cập nhật thủ công và Auto Sync khi mở model.
- Có bộ cài độc lập, không phụ thuộc TungMEP2, TungPrintSheets hoặc DiRoots.
- Không yêu cầu license, kích hoạt hoặc kết nối Internet.

`TUNG_DIROOT` là một triển khai độc lập dựa trên yêu cầu chức năng. Dự án không sử dụng source, DLL, resource, biểu tượng hoặc thành phần nhị phân của DiRoots.

## 2. Phạm vi bản đầu

### 2.1 Định dạng nguồn

- Hỗ trợ `.xlsx` và `.xlsm`.
- Đọc `.xlsm` nhưng không chạy macro/VBA.
- Không hỗ trợ `.xls` trong bản đầu.
- Đọc giá trị đã lưu của ô và cached value của công thức.
- Không triển khai một calculation engine tương đương Microsoft Excel.
- File Excel được mở chỉ đọc; add-in không thay đổi file nguồn.
- File mã hóa hoặc yêu cầu mật khẩu sẽ được báo là không đọc được.

### 2.2 Vùng dữ liệu

Người dùng có thể chọn một hoặc nhiều worksheet/vùng trong một lần:

- Named Range.
- Print Area.
- Used Range.
- Vùng tùy chỉnh theo cú pháp như `A1:H25`.

Hàng/cột đang hidden trong Excel được bỏ qua theo mặc định. Giao diện cho phép bật tùy chọn nhập cả hàng/cột ẩn trong một lần tạo hoặc cập nhật lại toàn bộ.

### 2.3 Ma trận tạo view

| Chế độ | Drafting View | Legend View | Schedule View |
|---|---:|---:|---:|
| Bảng Revit | Có | Có | Có, trong phần header |
| Ảnh | Có | Có | Không |

Nếu người dùng chọn `Ảnh + Schedule`, add-in chặn thao tác, giải thích giới hạn Revit và yêu cầu chọn Drafting hoặc Legend. Add-in không tự ý đổi loại view.

### 2.4 Ngoài phạm vi bản đầu

- Nhập Word hoặc PDF.
- Nhập dữ liệu Excel thành element/parameter nghiệp vụ trong model.
- Tính lại workbook hoặc chạy VBA.
- Đồng bộ ngược nội dung từ Revit về Excel.
- Đặt ảnh trực tiếp trong Schedule View.
- Cam kết tái tạo hoàn hảo SmartArt, chart hoặc shape phức tạp khi máy không có Excel.

## 3. Nhận diện sản phẩm và bộ cài

- Tên sản phẩm: `TUNG_DIROOT`.
- Ribbon tab: `TUNG_DIROOT`.
- Ribbon panel: `Excel Tools`.
- Lệnh chính: `TableGen`.
- Assembly, namespace, AddInId và manifest riêng cho từng phiên bản Revit.
- Icon và resource giao diện được nhúng trong assembly để tránh lỗi thiếu đường dẫn.
- Một bộ cài: `TUNG_DIROOT-Setup-Revit2024-2026.exe`.
- Một gói cài thủ công: `TUNG_DIROOT-Portable-Revit2024-2026.zip`.
- Bộ cài tự phát hiện Revit 2024–2026 và chỉ bật manifest cho các phiên bản đã cài.
- Mặc định cài theo user vào `%LocalAppData%\TUNG_DIROOT` và `%AppData%\Autodesk\Revit\Addins\<year>`, không cần quyền quản trị.
- Cài phiên bản mới được phép nâng cấp tại chỗ; không bắt buộc gỡ bản cũ.
- Gỡ add-in chỉ xóa chương trình/manifest, không xóa metadata liên kết đã lưu trong model.

## 4. Kiến trúc

### 4.1 Nền tảng build

- Revit 2024: `.NET Framework 4.8`.
- Revit 2025 và 2026: `.NET 8 Windows`.
- Phần nghiệp vụ Excel, mô hình dữ liệu và quy tắc cập nhật dùng chung source.
- Lớp giao tiếp Revit được tách theo adapter phiên bản để cô lập khác biệt API.
- Mỗi phiên bản có thư mục binary/dependency riêng để tránh tải nhầm assembly.

### 4.2 Thành phần chính

1. `Workbook Reader`
   - Đọc Open XML package mà không cần Excel.
   - Liệt kê worksheet, Named Range, Print Area và Used Range.
   - Chuẩn hóa giá trị, style, merged cell, row/column size và drawing metadata.

2. `Import Model`
   - Chuyển workbook thành mô hình trung gian độc lập với Revit.
   - Mỗi ô có địa chỉ, giá trị hiển thị, style key, merge key và kích thước.
   - Mô hình trung gian được tái sử dụng cho cả renderer và Revit writer.

3. `Revit Table Writer`
   - Dựng bảng trong Drafting/Legend bằng TextNote, DetailCurve và FilledRegion.
   - Gộp các đoạn border đồng tuyến và vùng fill giống nhau để giảm số element.
   - Dựng bảng vào phần header đối với Schedule View.
   - Chỉ sửa/xóa element có Link ID do `TUNG_DIROOT` tạo.

4. `Image Renderer`
   - `Excel High Fidelity`: dùng late-bound COM khi phát hiện Microsoft Excel.
   - `Built-in Renderer`: dùng Open XML data và WPF rendering, không cần Office.
   - Tạo ảnh tạm theo DPI rồi nhập vào Drafting/Legend bằng ImageType/ImageInstance.

5. `Link Registry`
   - Lưu registry trung tâm trong một Revit DataStorage.
   - Lưu dấu nhận dạng dự phòng trên view để phục hồi registry khi cần.
   - Lưu Link ID, nguồn, worksheet, vùng, chế độ, loại view, thiết lập cập nhật và dấu kiểm tra nội dung.

6. `Sync Coordinator`
   - Kiểm tra thay đổi, xếp hàng cập nhật và phát tiến trình.
   - Chạy thao tác Revit API qua ExternalEvent/Idling hợp lệ.
   - Quản lý tạm dừng, dừng và kết quả từng view.

7. `User Interface`
   - WPF dùng chung phong cách cho ba bản Revit.
   - Màn hình quản lý liên kết và hộp `Thêm Excel`.

### 4.3 Thư viện

- Dùng Open XML SDK hoặc thư viện tương đương có giấy phép cho phép phân phối để đọc `.xlsx/.xlsm`.
- Renderer tích hợp ưu tiên WPF để tránh phụ thuộc native rendering không cần thiết.
- Excel COM chỉ là khả năng tùy chọn, được gọi bằng late binding; add-in không tham chiếu bắt buộc Microsoft Office Interop.
- Bộ cài đi kèm danh sách và giấy phép của thành phần bên thứ ba.

## 5. Giao diện đã duyệt

### 5.1 Màn hình chính

Màn hình chính là trình quản lý liên kết, gồm:

- Thanh lệnh: `Thêm Excel`, `Kiểm tra nguồn`, `Cập nhật đã chọn`, `Tác vụ hàng loạt` và tìm kiếm.
- Bộ lọc theo trạng thái, chế độ và loại view.
- Bộ đếm `Hợp lệ`, `Đã thay đổi`, `Mất nguồn` và `Lỗi`.
- Bảng dữ liệu với các cột tối thiểu:
  - Chọn.
  - Trạng thái.
  - File/worksheet/vùng.
  - Tên view Revit.
  - Chế độ Bảng/Ảnh.
  - Loại view.
  - DPI.
  - Auto Sync.
- Vùng chi tiết liên kết và kết quả kiểm tra trước cập nhật.
- Thanh tiến trình cùng nút `Tạm dừng`, `Tiếp tục` và `Dừng` khi chạy batch.

### 5.2 Hộp Thêm Excel

Một cửa sổ với ba bước ngắn:

1. Chọn một hoặc nhiều file Excel.
2. Chọn nhiều worksheet/vùng và chỉnh tên view cho từng dòng.
3. Chọn mặc định chế độ, loại view, DPI, Auto Sync, đen trắng, đường dẫn và hành vi cập nhật.

Người dùng có thể chỉnh riêng thiết lập của từng dòng trước khi tạo hàng loạt.

Khi chọn Legend View, giao diện hiển thị thêm `Legend mẫu`. Revit API không cung cấp lệnh tạo Legend mới trực tiếp; add-in phải nhân bản một Legend có sẵn trong model. Nếu model chưa có Legend, add-in chặn dòng đó và hướng dẫn người dùng tạo một Legend mẫu trước.

### 5.3 Tác vụ hàng loạt

- Cập nhật view.
- Tạo lại toàn bộ.
- Bật/tắt Auto Sync.
- Relink theo file hoặc theo thư mục và khớp tên.
- Đổi absolute/relative path.
- Mở file hoặc thư mục nguồn.
- Mở view Revit.
- Unlink nhưng giữ view.
- Xóa view và metadata sau khi xác nhận.

## 6. Chế độ Bảng Revit

### 6.1 Nội dung hỗ trợ

- Giá trị ô và xuống dòng.
- Merged cell.
- Font, cỡ chữ, bold, italic và underline.
- Căn ngang, căn dọc và xoay chữ cơ bản.
- Màu chữ, màu nền và border.
- Chiều rộng cột, chiều cao hàng.
- Định dạng số, ngày tháng và phần trăm dựa trên cached/display value.
- Text Style và Line Style mapping.
- Tùy chọn chuyển đen trắng.

### 6.2 Drafting và Legend

- TextNote biểu diễn nội dung ô.
- DetailCurve biểu diễn đường viền; các đoạn có cùng style được hợp nhất khi có thể.
- FilledRegion biểu diễn nền; các vùng liền kề cùng màu được hợp nhất khi an toàn.
- Mỗi element có Link ID và cell/role key để cập nhật có mục tiêu.
- Không group toàn bộ bảng thành Revit Group vì điều đó làm cập nhật từng element khó và dễ gây cảnh báo group.
- Drafting View được tạo trực tiếp bằng Revit API.
- Legend View được tạo bằng cách duplicate một Legend mẫu do người dùng chọn. Add-in không giả định rằng Revit API có thể tạo Legend từ rỗng.
- Khi dùng một view có sẵn làm target, add-in chỉ nhận view tương thích, chưa có link xung đột và yêu cầu người dùng xác nhận trước khi thêm nội dung.

### 6.3 Schedule

- Dữ liệu được đặt vào phần header của Schedule.
- Nội dung này phục vụ trình bày, không trở thành dữ liệu element có thể filter/tính toán như body của Schedule.
- `Chỉ cập nhật dữ liệu` thay giá trị cell nhưng giữ kích thước và style Revit khi cấu trúc còn tương thích.
- Nếu số hàng/cột hoặc merge structure thay đổi, add-in yêu cầu `Tạo lại toàn bộ`.

## 7. Chế độ Ảnh và Hybrid engine

### 7.1 Excel High Fidelity

- Được dùng khi Microsoft Excel khả dụng và người dùng để chế độ engine là `Tự động` hoặc chọn High Fidelity.
- Excel được mở ẩn, read-only, không chạy macro và không hiển thị alert.
- Export vùng đã chọn thành ảnh tạm.
- Phải đóng workbook/application do add-in tạo và giải phóng COM reference trong `finally`.
- Nếu COM thất bại, add-in chuyển sang Built-in Renderer và ghi cảnh báo, trừ khi người dùng chọn `Chỉ High Fidelity`.

### 7.2 Built-in Renderer

- Luôn khả dụng và không yêu cầu Excel.
- Giữ bảng, font có trên Windows, màu, border, merged cell và ảnh nhúng được hỗ trợ.
- Chart/SmartArt/shape phức tạp chỉ được giữ nếu file có preview/cache mà renderer đọc được.
- Đối tượng không hỗ trợ được liệt kê trước khi tạo và trong báo cáo kết quả.
- DPI: 150, 300 hoặc 600; mặc định 300.

### 7.3 Quản lý ảnh

- Ảnh tạm được tạo trong thư mục `%TEMP%\TUNG_DIROOT` và dọn sau khi import.
- Tên file tạm không dựa trực tiếp vào tên worksheet để tránh lỗi ký tự và đường dẫn dài.
- Update ưu tiên tái sử dụng ImageType thuộc Link ID nếu API cho phép; nếu phải thay mới thì element cũ do add-in tạo được xóa trong cùng transaction.

## 8. Liên kết, đường dẫn và phát hiện thay đổi

### 8.1 Metadata liên kết

Mỗi link lưu tối thiểu:

- Stable Link ID.
- Target View UniqueId.
- File path và path mode.
- Worksheet ID/name và region definition.
- Import mode, view type, DPI và renderer preference.
- Black/white, hidden row/column và mapping profile.
- Update behavior và Auto Sync.
- File last-write time, length và content fingerprint gần nhất.
- Ngày, kết quả và phiên bản add-in cập nhật gần nhất.

Schema Extensible Storage dùng GUID cố định và có trường schema version để hỗ trợ migration.

### 8.2 Đường dẫn

- Absolute path phù hợp local, UNC và mapped drive.
- Relative path tính từ thư mục model khi model có đường dẫn file ổn định.
- Với model chưa lưu hoặc cloud model, add-in dùng absolute path và thông báo.
- Relink một dòng cho phép chọn file mới.
- Relink hàng loạt theo thư mục khớp tên file trước, sau đó kiểm tra worksheet/vùng.

### 8.3 Phát hiện thay đổi

- Kiểm tra nhanh bằng thời gian sửa và kích thước.
- Khi metadata khác, tính fingerprint nội dung liên quan để tránh dựng lại vì thay đổi không liên quan.
- Một workbook được parse một lần trong một batch, kể cả khi nhiều view dùng chung file.

## 9. Cập nhật và Auto Sync

### 9.1 Hai hành vi cập nhật

1. `Tạo lại toàn bộ`
   - Đồng bộ dữ liệu và định dạng từ Excel.
   - Thay thế element thuộc Link ID, không đụng element khác.

2. `Chỉ cập nhật dữ liệu`
   - Cập nhật giá trị ô.
   - Giữ Text Style, Line Style, màu, column width, row height và vị trí đã chỉnh trong Revit.
   - Chỉ áp dụng khi cấu trúc cell/merge còn tương thích.

### 9.2 Auto Sync

- Handler DocumentOpened chỉ đọc metadata và đưa yêu cầu vào hàng đợi.
- Việc sửa document diễn ra sau khi model mở xong qua Revit Idling/ExternalEvent.
- Chỉ xử lý link có Auto Sync và nguồn thực sự thay đổi.
- Không tự save model, không tự Synchronize with Central.
- Nếu document/view không editable, link đó được bỏ qua và báo nguyên nhân.

### 9.3 Tạm dừng và dừng

- Chỉ tạm dừng/dừng tại ranh giới giữa các view.
- Không hủy giữa một Revit transaction đang chạy.
- View đã hoàn tất được giữ nguyên; view đang lỗi được rollback riêng.
- Báo cáo cuối cùng phân biệt Thành công, Không đổi, Bỏ qua, Dừng và Lỗi.

## 10. An toàn và xử lý lỗi

- Mỗi view chạy trong transaction riêng.
- Một view lỗi không hủy các view khác trong batch.
- Chỉ element mang đúng schema và Link ID của `TUNG_DIROOT` mới được sửa/xóa.
- Mất file: trạng thái `Mất nguồn`, cung cấp `Relink`.
- File đang khóa: thử mở read-share an toàn; nếu không được thì bỏ qua và báo rõ.
- Cached formula value không có: đánh dấu cell, cảnh báo; không thay bằng giá trị đoán.
- Font thiếu: dùng fallback có cấu hình và liệt kê font đã thay thế.
- View bị xóa: cho phép unlink hoặc chọn/tạo target view mới.
- Trùng tên view: hiển thị trước khi tạo; người dùng chọn dùng view có sẵn, tự thêm hậu tố hoặc đổi tên.
- Worksharing: kiểm tra checkout/editability trước transaction và báo owner khi API cung cấp được.
- Không ghi nội dung dự án ra Internet.

Nhật ký kỹ thuật theo user được lưu trong `%LocalAppData%\TUNG_DIROOT\Logs`, có giới hạn dung lượng và không chứa dữ liệu ô Excel; chỉ chứa đường dẫn, Link ID, bước xử lý và exception cần thiết để chẩn đoán.

## 11. Hiệu năng

- Cảnh báo mặc định khi vùng vượt 5.000 ô ở chế độ Bảng Revit.
- Hiển thị trước số hàng, cột và số element ước tính.
- Cho phép người dùng tiếp tục sau cảnh báo; không đặt hard limit.
- Hợp nhất border/fill để giảm element count.
- Cache workbook model theo batch, không giữ workbook mở sau khi lệnh kết thúc.
- Chỉ dựng lại link có thay đổi.
- UI chuẩn bị dữ liệu ngoài Revit transaction; mọi gọi Revit API vẫn thực hiện trên Revit UI thread theo API contract.

## 12. Thiết lập người dùng

Thiết lập không thuộc model được lưu theo user, gồm:

- Thư mục Excel gần nhất.
- DPI mặc định.
- Renderer preference.
- Profile mapping Text/Line Style.
- Ngưỡng cảnh báo số ô.
- Kích thước/vị trí cửa sổ.

Thiết lập lỗi hoặc thiếu được thay bằng default an toàn, không ngăn add-in khởi động.

## 13. Kiểm thử

### 13.1 Ma trận nền tảng

- Revit 2024, 2025 và 2026.
- Máy có và không có Microsoft Excel.
- Model thường, local workshared và central/workshared.
- Cài mới, nâng cấp tại chỗ, portable và gỡ cài đặt.

### 13.2 Dữ liệu Excel

- `.xlsx`, `.xlsm` không chạy macro.
- Local path, UNC/mapped drive, Unicode/tiếng Việt và đường dẫn dài.
- Nhiều worksheet, Named Range, Print Area, Used Range và custom range.
- Merged cells, hidden rows/columns, font, border, fill, number/date/percent, rotation và formula cache.
- Ảnh nhúng, chart, shape và SmartArt để xác nhận cảnh báo/fallback.
- File bị khóa, bị mất, đổi tên, mã hóa hoặc hỏng package.

### 13.3 Luồng Revit

- Tạo Drafting, Legend và Schedule theo ma trận tương thích.
- Tạo Legend bằng cách duplicate Legend mẫu; kiểm tra thông báo khi model không có Legend mẫu.
- Cập nhật dữ liệu-only và tạo lại toàn bộ.
- Auto Sync sau khi mở model.
- Relink đơn lẻ và hàng loạt.
- Tạm dừng/dừng batch.
- Rollback một view trong khi các view khác vẫn hoàn tất.
- Không sửa/xóa element không thuộc add-in.
- Phục hồi registry từ metadata trên view.

### 13.4 Kiểm tra hình ảnh

- So sánh ảnh chuẩn cho 150/300/600 DPI.
- So sánh Built-in Renderer với Excel High Fidelity trên cùng bộ mẫu.
- Xác nhận danh sách đối tượng không được renderer tích hợp hỗ trợ.

## 14. Tiêu chí nghiệm thu

1. Một bộ cài có thể cài và tải add-in thành công trên Revit 2024–2026 đã phát hiện.
2. Máy không có Excel vẫn nhập được `.xlsx/.xlsm` ở chế độ Bảng và Ảnh tích hợp.
3. Drafting, Legend và Schedule hoạt động đúng ma trận tương thích; Legend được duplicate từ Legend mẫu và có cảnh báo rõ nếu chưa có mẫu.
4. Nhiều worksheet/vùng được tạo trong một batch và lỗi một dòng không hủy dòng khác.
5. Refresh chỉ cập nhật link thay đổi; Auto Sync không tự save/sync model.
6. Data-only update giữ định dạng Revit khi cấu trúc tương thích.
7. Add-in không sửa/xóa element không mang Link ID của nó.
8. Mất nguồn, file khóa, formula thiếu cache, font thiếu và đối tượng đồ họa không hỗ trợ đều có cảnh báo cụ thể.
9. Cài đè phiên bản mới không yêu cầu gỡ trước và không làm mất metadata trong model.
10. Bộ bàn giao gồm source, tài liệu sử dụng, EXE setup, portable ZIP, workbook mẫu và báo cáo kiểm thử.

## 15. Rủi ro và biện pháp

- **Model nặng do quá nhiều Revit element:** ước tính trước, cảnh báo 5.000 ô, hợp nhất border/fill và khuyến nghị chế độ Ảnh.
- **Khác biệt API giữa Revit 2024 và 2025+:** adapter riêng và test matrix theo version.
- **Excel COM bị treo hoặc để lại Excel.exe:** timeout, `finally` cleanup, fallback built-in và không dùng COM trong Auto Sync nếu phiên trước thất bại.
- **Không tái tạo đầy đủ đồ họa Excel khi không có Excel:** báo khả năng trước khi tạo và liệt kê phần tử fallback.
- **Đường dẫn mạng chậm/mất kết nối:** metadata check nhanh, timeout đọc hợp lý và bỏ qua từng link.
- **Schema thay đổi ở bản sau:** lưu schema version và migration không phá dữ liệu cũ.

## 16. Bàn giao dự kiến

- Repository `C:\Users\tungnt2303\Downloads\TUNG_DIROOT`.
- Source và script build cho Revit 2024–2026.
- `TUNG_DIROOT-Setup-Revit2024-2026.exe`.
- `TUNG_DIROOT-Portable-Revit2024-2026.zip`.
- Hướng dẫn cài/gỡ/sử dụng và xử lý lỗi.
- File Excel mẫu và model/test checklist.
- Báo cáo build, unit/integration test và kiểm tra cài đặt.
