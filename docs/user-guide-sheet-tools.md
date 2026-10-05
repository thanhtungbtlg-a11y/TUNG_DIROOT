# Hướng dẫn SheetLink / SheetGen

`SheetLink / Gen` là công cụ Excel hai chiều cho Revit 2024. Công cụ không tự lưu model và không Synchronize with Central.

## Quy trình khuyên dùng

1. Mở đúng Revit project và bấm `TUNG_DIROOT > Excel Tools > SheetLink / Gen`.
2. Bấm **Xuất model ra Excel**. File có ba tab `SHEETS`, `VIEWS`, `REVISION`; các dòng hiện có dùng `UPDATE`, cuối mỗi tab có các dòng `CREATE` mẫu. Workbook cũ chỉ có `SHEETS`/`VIEWS` vẫn đọc được.
3. Trong Excel, chỉ đặt `APPLY=TRUE` cho dòng thật sự muốn chạy. Không sửa `UNIQUE_ID` của dòng `UPDATE`.
4. Với Sheet, nhập số, tên, đúng tên Title Block Type và các revision sequence, ví dụ `1;3`. Revision mới trong cùng workbook được tham chiếu bằng `key:ISSUE_A`; có thể trộn `1;key:ISSUE_A`.
5. Với View, chọn `DRAFTING`, `FLOOR_PLAN` hoặc `CEILING_PLAN`. Floor/Ceiling phải có Level. `VIEW_TEMPLATE` có thể để trống.
6. Muốn đặt View lên Sheet, nhập `SHEET_NUMBER`, `CENTER_X_MM` và `CENTER_Y_MM`. Tọa độ dùng milimét.
7. Với Revision, nhập dòng trong tab `REVISION` như hướng dẫn dưới đây. Lưu Excel, quay lại công cụ, bấm **Đọc & xem trước**. Preview hiện thay đổi cũ → mới theo từng thuộc tính, gồm Title Block và revision bị bỏ khỏi Sheet; dòng `NoChange` không ghi lại model.
8. Sửa mọi dòng `Invalid` hoặc `Conflict`, Preview lại, rồi bấm **Apply các dòng Ready**.

## Quy tắc an toàn

- `CREATE` tạo phần tử mới; `UPDATE` tìm chính xác bằng `UNIQUE_ID`; `SKIP` luôn bỏ qua.
- Số Sheet và tên View không được trùng trong workbook hoặc model.
- Mỗi Sheet/View chỉ có một dòng UPDATE đang Apply cho cùng UNIQUE_ID; các dòng lặp bị Conflict, không dùng cơ chế dòng sau ghi đè dòng trước. Preview/báo cáo giữ số dòng Excel thật dù workbook có dòng trống.
- Title Block, Level, View Template và revision sequence dạng số phải tồn tại tại lúc Preview/Apply. Sheet/Revision mới có thể tạo trước các dòng View/Sheet phụ thuộc trong cùng batch.
- Mỗi dòng chạy trong transaction riêng; một dòng lỗi không làm mất các dòng thành công. Toàn lượt được gom thành một Undo.
- `UPDATE` không đổi `VIEW_KIND` hoặc `LEVEL_NAME`; nếu sửa hai trường này, Preview báo Conflict. Dùng `CREATE` để tạo View khác. Công cụ không xóa Sheet/View/Revision, không tự chuyển hoặc bỏ viewport đang nằm trên Sheet và không tự Save/Sync.
- Ngay trước Apply, tool đọc lại model và kiểm tra Preview còn đúng. Thay đổi trạng thái hoặc revision identity sau Preview yêu cầu Preview lại, không áp dụng âm thầm dữ liệu cũ.
- Đổi/chọn đường dẫn workbook, xuất file mới, bắt đầu Preview hoặc gặp lỗi sẽ xóa Preview cũ và vô hiệu Apply. Không đổi đường dẫn trong lúc tool đang đọc/ghi; kết quả đọc bất đồng bộ chỉ được dùng cho đúng đường dẫn/lượt Preview đã bắt đầu.
- Nếu Excel đang khóa file, đóng file hoặc lưu xong rồi Preview lại.

## Tab REVISION

- `APPLY`, `ACTION`: như Sheet/View; chỉ `CREATE`, `UPDATE`, `SKIP`, không có DELETE.
- `UNIQUE_ID`: giữ đúng định danh đã xuất khi UPDATE; để trống khi CREATE.
- `REVISION_KEY`: key duy nhất trong workbook, ví dụ `ISSUE_A`; không chứa `; , | :`. Key chỉ dùng để liên kết trong workbook/batch, không thay tên hoặc định danh Revit. Dòng UPDATE hợp lệ có thể không Apply nhưng key vẫn tham chiếu revision hiện có bằng UNIQUE_ID; SKIP chỉ làm alias nếu metadata vẫn đúng snapshot đã xuất. CREATE có UNIQUE_ID/sequence hoặc dòng Revision không hợp lệ không được dùng làm alias, Sheet phụ thuộc báo Conflict.
- `SEQUENCE_NUMBER`: số thứ tự do Revit cấp, chỉ đọc; CREATE để trống. Không đoán sequence cho revision mới, dùng `key:...` trên Sheet.
- `DESCRIPTION`, `REVISION_DATE`, `ISSUED_BY`, `ISSUED_TO`: metadata. DESCRIPTION bắt buộc; ngày là text tự do theo Revit, có thể để trống.
- `ISSUED`: TRUE/FALSE. Tool điền metadata trước rồi mới đánh dấu Issued. Revision đã Issued không được sửa/unissue qua tool; xử lý trực tiếp trong Revit rồi Preview lại.
- `VISIBILITY`: `CLOUD_AND_TAG`, `TAG_ONLY`, `HIDDEN`; tác động visibility cloud/tag của revision trên toàn project, hiển thị trong Preview.

Tool chặn CREATE trùng cặp DESCRIPTION/REVISION_DATE đã có trong model hoặc workbook để tránh tạo lại revision sau một lượt Apply. Dùng UPDATE theo UNIQUE_ID nếu muốn sửa revision đó. Revision được xử lý trước Sheet; nếu một revision CREATE lỗi runtime, Sheet dùng key của nó sẽ rollback riêng, các dòng độc lập vẫn hoàn tất. Tool không đổi revision numbering scheme hoặc thứ tự revision; Revit cấp sequence cho revision mới.

Sau Apply, công cụ tự đọc lại workbook và model để hiện trạng thái mới. Dòng `CREATE` vẫn còn trong Excel; đổi sang `SKIP` hoặc xuất workbook mới trước lượt kế tiếp. Khi xuất lại, revision mới có UNIQUE_ID thật; dùng UPDATE và key xuất mới cho các lượt tiếp theo.
