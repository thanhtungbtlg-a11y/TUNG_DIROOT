# Checklist SheetLink / SheetGen — Revit 2024

Status: **NOT RUN**. Automated planner/workbook tests và build không thay thế kiểm tra thao tác trong Revit.

## Chuẩn bị

- Dùng một file test có ít nhất hai Level, hai Title Block Type, một View Template và hai Revision.
- Save As bản test riêng. Công cụ không tự Save hoặc Sync Central.

## Xuất workbook

1. Mở `SheetLink / Gen`, bấm **Xuất model ra Excel**.
2. Xác nhận workbook có ba tab `SHEETS`, `VIEWS`, `REVISION`; header màu, freeze dòng 1 và filter hoạt động.
3. Xác nhận Sheet/View hiện có là `UPDATE`, `UNIQUE_ID` không rỗng, tọa độ viewport là số theo mm.
4. Revision hiện có xuất đủ UNIQUE_ID, sequence, description/date, issued/by/to và visibility; CREATE mẫu không có UNIQUE_ID/sequence.
5. Đọc workbook cũ chỉ có `SHEETS`/`VIEWS` vẫn hoạt động.

## Preview

1. Tạo một Sheet mới và một Drafting View mới bằng dòng `CREATE`, đặt `APPLY=TRUE`.
2. Tạo thử số Sheet trùng, Title Block sai, Level sai, revision không tồn tại và chỉ nhập một tọa độ.
3. Preview phải đánh dấu các dòng đó là `Invalid/Conflict`; chỉ dữ liệu đúng là `Ready`.
4. Sửa workbook và Preview lại mà không cần khởi động lại Revit.
5. UPDATE Sheet đổi Title Block và bỏ một revision: Preview phải hiện giá trị cũ → mới, bao gồm revision bị bỏ.
6. UPDATE View đổi tên/template/tọa độ: Preview hiện đúng thuộc tính; dòng không đổi phải là NoChange và không Apply.
7. UPDATE View đổi VIEW_KIND hoặc LEVEL_NAME hợp lệ nhưng khác hiện trạng: Conflict, không báo Ready. Bỏ/chuyển Sheet của viewport hiện có cũng bị chặn.
8. Tạo REVISION key `ISSUE_A`, tham chiếu Sheet bằng `1;key:ISSUE_A`: cả hai Ready. Key không tồn tại, key trùng hoặc CREATE thiếu description đều chặn Sheet phụ thuộc.
9. UPDATE Revision giữ UNIQUE_ID, sửa description/date/by/to/visibility: Preview hiện cũ → mới. Sửa sequence hoặc sửa/unissue revision đã Issued phải Conflict.
10. Sau Preview, sửa model hoặc xóa/đổi revision rồi Apply: cảnh báo SHEETLINK_PREVIEW_STALE trước khi ghi; Preview lại mới tiếp tục được.
11. Preview workbook A rồi Browse/nhập đường dẫn B: grid cũ được xóa và Apply tắt. Preview B bị lỗi hoặc export B cũng không được khôi phục batch A; chỉ Preview thành công B mới bật Apply.
12. Trong lúc đọc/ghi workbook, đường dẫn và Browse bị khóa. Kết quả Preview cũ đến trễ không ghi đè Preview mới, kể cả khi chọn lại cùng đường dẫn.
13. CREATE Revision key NEW nhưng điền UNIQUE_ID hiện có: Revision Invalid và Sheet key:NEW Conflict, không gán revision cũ âm thầm. UPDATE revision có lỗi cũng không cung cấp alias; UPDATE hợp lệ chưa Apply hoặc SKIP đúng snapshot vẫn tham chiếu được.
14. Hai dòng Sheet hoặc View UPDATE có cùng UNIQUE_ID nhưng tên/số khác: cả hai Conflict, không có dòng Ready/NoChange bị ghi đè.
15. Chèn dòng trống trước dữ liệu: số dòng trong Preview và báo cáo Apply vẫn trùng dòng thật của Excel cho Sheet/View/Revision.

## Apply và Undo

1. Apply các dòng Ready; xác nhận Sheet, View, revision bổ sung và viewport đúng vị trí.
2. Cập nhật tên Sheet/View và tọa độ viewport bằng `UPDATE`; xác nhận không tạo bản sao.
3. Cho một dòng lỗi runtime và một dòng đúng; dòng đúng vẫn hoàn tất, dòng lỗi có thông báo riêng.
4. Bấm Undo một lần; toàn bộ lượt Apply gần nhất phải được hoàn tác.
5. Kiểm tra model chưa tự Save và chưa Synchronize with Central.
6. Batch tạo revision và Sheet tham chiếu key: Sheet nhận đúng Revision ID thật, không dựa trên sequence dự đoán. Export lại thấy revision mới và UNIQUE_ID.
7. CREATE revision có ISSUED=TRUE: metadata đầy đủ, sau tạo không thể sửa qua tool; visibility đúng trên cloud/tag.
8. Một revision CREATE lỗi runtime: Sheet phụ thuộc key lỗi và rollback, revision/Sheet/View độc lập vẫn hoàn tất. Một Undo đảo toàn lượt thành công.
9. Apply lại cùng CREATE description/date: Preview báo REVISION_ALREADY_EXISTS, không tạo revision trùng; UPDATE theo UNIQUE_ID hoạt động.

## Modeless và nhiều model

- Trong lúc cửa sổ mở có thể pan/zoom/chọn phần tử Revit.
- Chuyển sang model khác rồi Apply phải nhận cảnh báo quay lại đúng model, không được ghi nhầm document.
- Không thể đóng cửa sổ khi đang ghi workbook hoặc chạy ExternalEvent.
