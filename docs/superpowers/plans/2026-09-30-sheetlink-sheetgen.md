# SheetLink / SheetGen Implementation Plan

**Goal:** Thêm một công cụ Excel hai chiều để xuất, xem trước, tạo và cập nhật Sheet, View, Viewport và revision trong Revit 2024.

## Workbook contract

- Tab `SHEETS`: `APPLY`, `ACTION`, `UNIQUE_ID`, `SHEET_NUMBER`, `SHEET_NAME`, `TITLE_BLOCK_TYPE`, `REVISION_SEQUENCES`.
- Tab `VIEWS`: `APPLY`, `ACTION`, `UNIQUE_ID`, `VIEW_NAME`, `VIEW_KIND`, `LEVEL_NAME`, `VIEW_TEMPLATE`, `SHEET_NUMBER`, `CENTER_X_MM`, `CENTER_Y_MM`.
- Dòng hiện có xuất dưới dạng `UPDATE`; dòng mẫu mới là `CREATE` và mặc định chưa Apply.
- Định danh luôn là text; tọa độ là số thật; header có style, filter và freeze.
- Reader nhận cả inline string lẫn shared string do Excel có thể chuyển đổi khi lưu.

## Tasks

1. Viết test và lõi workbook OpenXML, parser và validator có mã lỗi ổn định.
2. Đọc catalog Revit và xuất snapshot Sheet/View sang workbook ngoài Revit API thread.
3. Tạo preview theo từng dòng; kiểm tra trùng số Sheet, thiếu Title Block/Level/Template và dữ liệu không hợp lệ.
4. Apply bằng `ExternalEvent`: mỗi dòng một transaction, toàn batch một `TransactionGroup`, lỗi một dòng không dừng phần còn lại.
5. Hỗ trợ tạo/cập nhật Sheet, gán revision có sẵn, tạo Drafting/Floor/Ceiling View và đặt/move Viewport theo mm.
6. Thêm cửa sổ responsive, nút Ribbon riêng, checklist Revit 2024 và contract tests.
7. Chạy toàn bộ regression, commit riêng và chỉ sau đó chuyển sang ParaManager/ReOrdering.

## Safety

- Không xóa Sheet/View/Revision.
- Không tự Save, Save As hoặc Synchronize with Central.
- Không gọi Revit API từ background thread.
- Preview không mở transaction; Apply chỉ chạy các dòng `APPLY=TRUE` hợp lệ.
