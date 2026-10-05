# Bulk Visibility & Graphics — checklist trực tiếp trong Revit 2024

Status: **NOT RUN**. Dùng bản sao model có ít nhất hai View, một View Template, hai Sheet và nhiều View Filter.

## Catalog và chọn phạm vi

- [ ] Nút `Bulk V/G` xuất hiện trong panel Model Tools sau khi restart Revit.
- [ ] Nguồn liệt kê View và View Template có hỗ trợ Graphics Overrides; không liệt kê Sheet làm nguồn.
- [ ] Đích liệt kê View, View Template và Sheet; Sheet hiện đúng số Viewport.
- [ ] Chọn hai Sheet chứa cùng một View; Preview/Apply chỉ chạy View đó một lần.
- [ ] Chọn nguồn đồng thời làm đích; nguồn tự bị loại, không tự ghi đè chính nó.

## Preview và Apply

- [ ] Chọn một Category và một Filter; Preview báo đúng khác biệt Visibility, Projection, Cut, Fill và Transparency/Halftone.
- [ ] Bỏ từng nhóm thuộc tính trong cả ba chế độ; Preview không báo và Apply không thay nhóm đã bỏ của Category/Filter đã có.
- [ ] Filter visibility tắt nhưng enabled khác nhau giữa nguồn/đích; Preview vẫn báo Visibility khi tích chọn nhóm này, Apply sao chép cả hai cờ riêng biệt.
- [ ] `Merge`: Filter khác ở đích còn nguyên; chỉ Category/Filter tích chọn được cập nhật.
- [ ] `Replace`: chỉ Filter tích chọn (không phải mọi Filter nguồn) trở thành bộ đích; Preview báo từng Filter ngoài lựa chọn sẽ xóa, dù Filter tích chọn hiện giống hệt nguồn.
- [ ] `Replace`: chọn Filter đã có nhưng đảo thứ tự so với nguồn, bỏ Visibility/Fill/Transparency; Preview báo sắp xếp lại, Apply đổi thứ tự nguồn nhưng giữ visibility/enabled và mọi nhóm chưa bật của từng Filter.
- [ ] `Replace`: bỏ Projection/Cut/Fill/Transparency cho Category; Apply không xóa các override đang có.
- [ ] `Replace`: chỉ chọn Category, không chọn Filter; danh sách và thứ tự Filter đích không đổi.
- [ ] `Replace`: Apply hiện cảnh báo xóa Filter ngoài lựa chọn; chọn No không sửa model.
- [ ] `Update Existing`: Filter tích chọn đã có nhận nhóm đã bật; Filter chưa có hiện Skipped, không được thêm; Filter khác và thứ tự không đổi. Apply báo số Filter bị bỏ qua.
- [ ] Filter mới ở Merge/Replace nhận nhóm đã bật; nhóm chưa bật vẫn là mặc định Revit (không có thiết lập đích cũ).
- [ ] View đích bị template kiểm soát trả lỗi riêng; các View đích khác vẫn thành công.
- [ ] Chọn View Template làm đích; các View dùng template phản ánh thay đổi theo cơ chế Revit.

## Hoàn tác và an toàn

- [ ] Sau Apply, một Undo của Revit đảo toàn bộ lượt thành công trong TransactionGroup.
- [ ] Apply lại rồi bấm `Khôi phục lần Apply gần nhất`; Category đã tác động và toàn bộ Filter của từng đích trở về trạng thái trước Apply.
- [ ] Một đích lỗi không có backup giả và không bị Restore tác động nhầm.
- [ ] Đóng/mở lại cửa sổ: nút Restore bị reset; Undo Revit vẫn hoạt động.
- [ ] Chuyển active model trước khi action: document guard chặn thao tác, không sửa model khác.
- [ ] Xác nhận tool không Save và không Synchronize with Central.

## Giao diện và hồi quy

- [ ] Ở 1500×900 và 780×560, toolbar wrap; hai danh sách và Preview còn đủ chiều cao, không che Apply/Restore/Đóng.
- [ ] Checkbox chọn được bằng một lần bấm, không cần chọn hàng trước.
- [ ] Smoke TableGen, PDF/Word Link, DataLink, Filter và Parameter Loader sau Bulk V/G.
