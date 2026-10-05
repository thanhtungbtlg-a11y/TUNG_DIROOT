# Hướng dẫn cài đặt TUNG_DIROOT

## Yêu cầu

Windows 64-bit, Autodesk Revit 2024 và .NET Framework 4.8. Đóng Revit trước khi cài hoặc nâng cấp. Không cần quyền administrator vì add-in được cài theo user. Bộ cài này không hỗ trợ hoặc tạo manifest cho Revit 2025/2026.

## Setup EXE

Chạy `TUNG_DIROOT-Setup-Revit2024.exe`. Binary được đặt tại `%LocalAppData%\TUNG_DIROOT\1.3.0\2024`; workbook mẫu và tài liệu nằm trong `Samples`/`Docs` cạnh thư mục runtime. Manifest được tạo tại `%AppData%\Autodesk\Revit\Addins\2024\TUNG_DIROOT.addin` với đường dẫn assembly tuyệt đối. Cài đè cùng phiên bản hoặc bản mới hơn không cần gỡ trước; AppId cố định cho phép Inno Setup nhận diện lần cài cũ.

## Portable ZIP

Giải nén toàn bộ `TUNG_DIROOT-Portable-Revit2024.zip` vào một thư mục local, không chạy trực tiếp trong ZIP. Chạy `Install-TUNG_DIROOT.cmd`; script copy binary Revit 2024 sang LocalAppData và tạo manifest. Có thể chạy PowerShell trực tiếp nếu cần log chi tiết. Portable và Setup dùng cùng manifest contract: application class `TungDiroot.Addin.Application`, AddInId `8A93D06F-E03B-4A7C-9A96-DF5A3D5B0E24`, VendorId `TUNG`.

## Nâng cấp và gỡ

Đóng Revit, chạy setup mới hoặc portable installer mới. Không cần xóa phiên bản trước bằng tay. Để gỡ portable, chạy `Uninstall-TUNG_DIROOT.cmd`; Setup có mục uninstall của Windows. Gỡ mặc định chỉ xóa binary và manifest do installer quản lý, giữ `%LocalAppData%\TUNG_DIROOT\settings.json` và `Logs`. Tùy chọn PowerShell `-RemoveUserSettingsAndLogs` mới xóa hai mục này.

Trước khi cài đè, Setup và portable tự sao lưu runtime đang được manifest sử dụng và manifest gốc vào `%LocalAppData%\TUNG_DIROOT\Backups\<timestamp>`. Mỗi backup có `backup.json` và SHA-256 cho các file. Nếu backup thất bại, installer dừng trước khi thay thế file.

Khi Revit đang mở, cài/restore vào thư mục add-in thật sẽ bị chặn trước khi ghi file; hãy đóng Revit trước. Restore kiểm tra đúng toàn bộ file đã kê khai, không nhận file thêm ngoài danh sách hoặc backup đã đổi. Thư mục cài mới phải trống hoặc có marker sở hữu của TUNG_DIROOT; thư mục backup không được nằm trong thư mục cài.

Để quay lại bản cũ, đóng Revit và chạy script `Restore-TUNG_DIROOT.ps1` nằm trong thư mục `Installer` của Setup hoặc cạnh `.cmd` trong bản portable:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "Restore-TUNG_DIROOT.ps1" -BackupPath "C:\duong-dan\TUNG_DIROOT\Backups\<timestamp>"
```

Restore kiểm tra hash rồi tạo một runtime riêng dưới `%LocalAppData%\TUNG_DIROOT\Restored`, chuyển manifest sang runtime đó và giữ bản hiện tại để có thể quay lại. Khởi động lại Revit sau restore.

Uninstaller không mở/chỉnh RVT, không xóa metadata link trong RVT và không xóa Excel nguồn. Nếu cài lại, registry trong model vẫn có thể được đọc. RevitAPI.dll/RevitAPIUI.dll không nằm trong payload; add-in dùng API từ Revit 2024 đã cài.

Artifact hiện được tạo dưới trạng thái release candidate cho tới khi checklist trực tiếp trong Revit 2024 hoàn tất. Xem [release matrix](testing/release-matrix.md) để biết trạng thái thực tế.
