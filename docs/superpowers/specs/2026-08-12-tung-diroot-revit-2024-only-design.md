# TUNG_DIROOT — Thiết kế phát hành chỉ cho Revit 2024

Ngày: 2026-08-12  
Trạng thái: Đã được người dùng duyệt về hướng triển khai

## Mục tiêu

Thu hẹp TUNG_DIROOT 1.0.0 thành add-in độc lập chỉ hỗ trợ Autodesk Revit 2024. Bản phát hành không chứa, không quảng bá và không yêu cầu Revit 2025/2026.

## Phạm vi thay đổi

- Target add-in duy nhất là Revit 2024 trên .NET Framework 4.8, tham chiếu đúng Revit API 2024 cài trên máy build.
- Build matrix, staging, metadata, manifest, portable installer, uninstaller và release gate mặc định chỉ xử lý năm 2024.
- Inno Setup chỉ đóng gói thư mục `2024`, chỉ tạo manifest trong `Addins\2024`, và tạo `TUNG_DIROOT-Setup-Revit2024.exe`.
- Portable package có tên `TUNG_DIROOT-Portable-Revit2024.zip`.
- `SHA256SUMS.txt` chỉ liệt kê hai artifact Revit 2024 nêu trên.
- Tài liệu người dùng, kiến trúc, release matrix và báo cáo phát hành được cập nhật thành 2024-only. Các kế hoạch lịch sử có thể giữ nguyên để bảo toàn lịch sử thiết kế, nhưng không được dùng làm tài liệu hỗ trợ hiện hành.
- Source code chức năng TableGen không thay đổi ngoài các điều chỉnh cần thiết để build/đóng gói 2024.

## Release gate

Release gate tự động phải xác minh:

1. 140 Core tests, 9 Rendering tests và 7 Presentation tests hiện có tiếp tục PASS.
2. Add-in Revit 2024 build Release với 0 warning và 0 error.
3. Payload không chứa `RevitAPI.dll` hoặc `RevitAPIUI.dll`.
4. Staging metadata bao phủ chính xác payload 2024 và tất cả size/SHA-256 hợp lệ.
5. Portable install, install-over và uninstall sandbox PASS; uninstaller từ chối thư mục không có ownership marker.
6. Inno Setup tạo đúng EXE 2024 và portable build tạo đúng ZIP 2024.
7. Không còn artifact hoặc manifest 2025/2026 trong `dist` của release hiện tại.

Checklist trực tiếp trong Revit 2024 vẫn là cổng xác nhận vận hành. Trước khi checklist được chạy, artifact được bàn giao dưới trạng thái release candidate; không tự điền PASS hoặc tạo tag chính thức thay cho người kiểm thử.

## Khả năng nâng cấp và gỡ

- AppId và đường dẫn cài `%LocalAppData%\TUNG_DIROOT\1.0.0` giữ nguyên để cài đè an toàn lên bản thử trước đó.
- Cài lại cùng phiên bản sẽ xóa payload 2024 cũ trước khi copy payload mới.
- Uninstaller chỉ xóa manifest trỏ vào thư mục cài thuộc quyền sở hữu của TUNG_DIROOT; không mở hoặc sửa RVT/Excel và không xóa settings/log mặc định.

## Artifact bàn giao

- `dist\TUNG_DIROOT-Setup-Revit2024.exe`
- `dist\TUNG_DIROOT-Portable-Revit2024.zip`
- `dist\SHA256SUMS.txt`
- Báo cáo kiểm thử tự động và checklist Revit 2024 ghi rõ trạng thái thực tế.

## Ngoài phạm vi

- Không build, kiểm thử hoặc phát hành cho Revit 2025/2026.
- Không dùng Revit API 2024 để tạo DLL mang tên 2025/2026.
- Không thay đổi định dạng RVT, workbook nguồn hoặc metadata link hiện có.
