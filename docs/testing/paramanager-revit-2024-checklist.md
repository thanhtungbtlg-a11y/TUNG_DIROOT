# Checklist ParaManager — Revit 2024

## Project mode

1. Mở project test có shared/project parameter Instance và Type, nhiều category.
2. Xuất inventory; xác nhận tab `PARAMETER_MANAGER` có các binding hiện tại và 10 dòng `CREATE_SHARED` mẫu.
3. Chọn Shared Parameter TXT. Tạo một dòng `CREATE_SHARED` đúng group/name, binding, `PARAMETER_GROUP_ID` và category; Preview phải Ready.
4. Thử GUID sai, category sai, group sai và tên đã tồn tại; Preview phải chặn.
5. Với UPDATE, đổi INSTANCE thành TYPE; phải báo `BINDING_KIND_CONFLICT` và không Apply.
6. Nhập thêm category rồi Apply; category mới được thêm, category cũ vẫn còn. Đổi parameter group hợp lệ và kiểm tra Properties.

## Family mode

1. Mở family test và xuất inventory FAMILY.
2. Tạo shared family parameter mới từ TXT; kiểm tra Instance/Type và group.
3. Đổi tên parameter family nội bộ; shared hoặc built-in rename phải bị chặn trước Apply.
4. Đổi group hợp lệ; kiểm tra formula/value vẫn còn. Nếu Revit từ chối, dòng đó rollback riêng và báo lỗi.

## An toàn

- Trước Apply phải có `.TUNG_BACKUP_<timestamp>.xlsx` cạnh workbook.
- Một dòng lỗi không dừng dòng đúng; một Undo đảo toàn bộ lượt thành công.
- Chuyển document khi cửa sổ mở phải bị chặn ghi nhầm model.
- Không Save, Save As, Synchronize with Central hoặc kết nối mạng.
