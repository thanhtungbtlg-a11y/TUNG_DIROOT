# Parameter Tools Implementation Plan

**Goal:** Hoàn thiện ParaManager và ReOrdering cho Revit 2024, dùng chung workbook/preview/transaction core và không tự ghi đè binding khác loại.

## ReOrdering

1. Xuất toàn bộ `FamilyManager.GetParameters()` ra workbook, gồm cả parameter ẩn để đúng contract Revit API.
2. Người dùng chỉnh `TARGET_ORDER_IN_GROUP`; thứ tự group của Revit không bị thay đổi.
3. Preview kiểm tra đủ đúng tập parameter, key không trùng, group không đổi và thứ tự mỗi group là hoán vị `1..N`.
4. Trước Apply tự ghi workbook backup có timestamp; Apply gọi `ReorderParameters` trong một transaction/Undo.
5. Chỉ chạy trong Family Editor; không Save/Save As family.

## ParaManager

1. Xuất inventory Project/Shared/Family parameter ra Excel.
2. Preview thay đổi binding, category và parameter group; binding Instance/Type khác hiện trạng luôn là conflict, không tự chuyển.
3. Project: tạo shared binding mới và cập nhật category/group an toàn.
4. Family: tạo shared family parameter mới, đổi group/rename khi API cho phép; built-in/read-only báo rõ.
5. Mỗi dòng một transaction, một TransactionGroup/Undo; backup workbook trước Apply.

## Verification

- Core unit tests cho workbook/planner.
- Build và reflection contract với Revit API 2024.
- Responsive XAML regression, checklist trực tiếp và full installer regression.
