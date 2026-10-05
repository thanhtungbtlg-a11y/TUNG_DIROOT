# Hướng dẫn TUNG Filter — lọc và thao tác phần tử Revit

TUNG Filter tìm phần tử theo category, family, type và giá trị parameter. Công cụ chỉ hỗ trợ Revit 2024, không sửa linked document, không tự Save và không Synchronize with Central. Cửa sổ là modeless nên có thể pan/zoom và kiểm tra model trong lúc thiết lập; mọi lần đọc hoặc thay đổi Revit đều đi qua `ExternalEvent` và xác nhận đúng model đã mở cửa sổ.

## 1. Chọn phạm vi

Mở `TUNG_DIROOT > Model Tools > Filter`. Trong mục `1. Phạm vi`:

1. Chọn `Toàn bộ model`, `View hiện tại` hoặc `Đối tượng đang chọn`.
2. Bấm `Nạp lại danh mục` sau khi đổi view, selection hoặc phạm vi.
3. Tích category, family hoặc type cần giới hạn. Không tích mục nào trong một nhóm nghĩa là không giới hạn theo nhóm đó.

Danh sách dùng checkbox một lần bấm và có cuộn/virtualization. Với `Đối tượng đang chọn`, hãy chọn element trong Revit trước rồi nạp lại danh mục.

## 2. Tạo điều kiện

Trong mục `2. Điều kiện`, chọn parameter, phép so sánh và giá trị. Bấm `+ Thêm điều kiện` để thêm dòng; chọn `AND` nếu mọi điều kiện đều phải đúng hoặc `OR` nếu chỉ cần một điều kiện đúng.

- Text hỗ trợ `Bằng`, `Khác`, `Chứa`, `Không chứa`, `Đang trống`, `Không trống`.
- Number/Integer hỗ trợ `Bằng`, `Khác`, `Lớn hơn`, `Nhỏ hơn`, `Đang trống`, `Không trống`. Số so sánh nhập theo dạng nội bộ như `12.5`, không dùng chuỗi hiển thị có đơn vị.
- Yes/No hỗ trợ `Bằng`, `Khác`, `Đang trống`, `Không trống`; có thể nhập `true`/`false` hoặc `1`/`0`.

Parameter không tồn tại trên một element khác với parameter có tồn tại nhưng đang trống. Điều kiện `Đang trống` và `Không trống` chỉ xét element thực sự có parameter đó. Lỗi nhập liệu xuất hiện ngay trong cửa sổ, không mở hộp thoại chặn Revit.

## 3. Chạy và kiểm tra kết quả

Bấm `Chạy lọc`. Add-in đọc tối đa 500 element cho mỗi lượt Revit rồi trả quyền điều khiển, hiển thị tiến độ và tiếp tục lượt kế. Phần đánh giá điều kiện dùng dữ liệu đã tách khỏi Revit API. Bấm `Hủy` để dừng ở ranh giới lô an toàn; không có transaction đang mở bị cắt ngang.

Kết quả hiển thị Element ID, Unique ID, category, family, type, level và số điều kiện khớp. Có thể tích từng dòng, `Chọn tất cả` hoặc `Bỏ chọn`. Nếu kết quả bằng 0, hãy kiểm tra lại phạm vi, reload danh mục và logic AND/OR.

## 4. Hành động với kết quả

Các nút trong `4. Hành động` chỉ dùng các dòng đã tích:

- `Chọn + zoom`: đặt Revit selection và zoom đến các phần tử.
- `Cô lập tạm` / `Ẩn tạm`: dùng Temporary Hide/Isolate trên view đang hoạt động; không ghi override đồ họa vĩnh viễn.
- `Bỏ ẩn / cô lập tạm`: tắt trạng thái Temporary Hide/Isolate của view hiện tại, không cần chọn kết quả.
- `Lưu Selection Filter`: tạo hoặc cập nhật `SelectionFilterElement` tên `TUNG_FILTER_<profile>` trong một transaction và có thể Undo.
- `Xuất Excel`: tạo sheet `RESULTS` phẳng, có AutoFilter, gồm identity và các giá trị parameter đã đọc.
- `Tạo / cập nhật view 3D`: tạo hoặc dùng lại view isometric riêng `TUNG_FILTER_<profile>`, cô lập kết quả và tô màu ổn định theo parameter `Nhóm màu theo`. Add-in không đặt override lên view làm việc của người dùng.

View 3D trực quan hóa là view do công cụ quản lý. Mỗi lần refresh, công cụ xóa override trong chính view đó rồi áp lại; không nên dùng view này để đặt override thủ công cần giữ lâu dài.

## 5. Profile

Nhập `Tên profile`, chọn parameter `Nhóm màu theo` và bấm `Lưu profile`. Profile lưu tại `%LocalAppData%\TUNG_DIROOT\profiles\filter`, gồm phạm vi, điều kiện, bảng màu và cách nhóm; không lưu danh sách Element ID cũ. Chọn profile ở góc trên rồi `Nạp profile` để phục hồi thiết lập, sau đó chạy lại trên trạng thái model hiện tại.

Nếu chuyển sang model khác, mọi ExternalEvent/action của cửa sổ cũ bị chặn. Hãy quay lại model ban đầu hoặc đóng cửa sổ khi không còn lô đang chạy rồi mở Filter từ model mới. Trước khi dùng trên project thật, chạy [checklist trực tiếp Revit 2024](testing/filter-revit-2024-checklist.md) trên bản sao model.
