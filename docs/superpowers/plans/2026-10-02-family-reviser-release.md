# FamilyReviser and Unified Release Plan

**Goal:** Hoàn thiện FamilyReviser cho Revit 2024, sau đó phát hành bộ cài hợp nhất có backup/hash và checklist cuối.

## FamilyReviser

1. Xuất loadable Family và Family Type ra workbook; trong Family Editor các Family tìm thấy được đánh dấu là nested.
2. Hỗ trợ `RENAME_FAMILY` và `RENAME_TYPE`, chỉ chạy dòng `APPLY=TRUE`.
3. Preview kiểm tra stale identity/current name, ký tự cấm, tên rỗng/quá dài, regex naming rule tùy chọn, duplicate family và duplicate type trong cùng family.
4. Không xóa/duplicate family; in-place family rename bị chặn an toàn.
5. Backup workbook trước Apply; mỗi dòng một transaction, toàn lượt một TransactionGroup/Undo.

## Unified release

1. Chạy toàn bộ unit/UI/Revit contract/runtime dependency/schema tests.
2. Stage Revit 2024-only payload, portable install/uninstall và Inno installer.
3. Xuất SHA-256, release report và đường dẫn bộ cài RC.
4. Không cài đè máy người dùng cho đến khi có yêu cầu rõ; artifact luôn có thể rollback bằng các commit giai đoạn.
