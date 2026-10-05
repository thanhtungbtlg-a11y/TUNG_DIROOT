# TUNG_DIROOT TableGen Implementation Roadmap

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Xây dựng và phát hành add-in độc lập `TUNG_DIROOT` nhập, hiển thị và đồng bộ bảng Excel trong Revit 2024–2026.

**Architecture:** Công việc được chia thành bốn kế hoạch nối tiếp. Core Excel không phụ thuộc Revit; lớp add-in chuyển mô hình trung gian thành view Revit; registry/UI quản lý vòng đời liên kết; pipeline phát hành tạo bộ cài chung và portable ZIP.

**Tech Stack:** C# 12/7.3 theo target, .NET Standard 2.0, .NET Framework 4.8, .NET 8 Windows, WPF, Autodesk Revit API 2024–2026, Open XML SDK, xUnit, PowerShell và Inno Setup 6.

## Global Constraints

- Tên sản phẩm, Ribbon tab và AddIn identity: `TUNG_DIROOT`; lệnh chính: `TableGen`.
- Revit 2024 dùng `.NET Framework 4.8`; Revit 2025–2026 dùng `.NET 8 Windows`.
- Bản đầu hỗ trợ `.xlsx` và `.xlsm`, không chạy macro/VBA, không hỗ trợ `.xls`.
- Microsoft Excel là tùy chọn; mọi luồng bắt buộc phải hoạt động khi máy không cài Excel.
- Chế độ `Bảng Revit`: Drafting, Legend và Schedule header; chế độ `Ảnh`: Drafting và Legend.
- Legend phải duplicate từ Legend mẫu có sẵn; không giả định Revit API tạo Legend rỗng.
- Add-in không tham chiếu source, DLL, resource hoặc binary của TungMEP2, TungPrintSheets hay DiRoots.
- Không license/kích hoạt, không yêu cầu Internet, không gửi dữ liệu dự án ra ngoài.
- Không phân phối `RevitAPI.dll` hoặc `RevitAPIUI.dll`; build phải resolve từ bản Revit/SDK do người dùng cung cấp.
- Mỗi task tuân thủ TDD, chạy test/build trước commit và chỉ commit các file thuộc task.

---

## Cấu trúc repository được khóa bởi kế hoạch

```text
TUNG_DIROOT/
├─ src/
│  ├─ TungDiroot.Core/          # Excel/domain/layout/update logic, không Revit/WPF
│  ├─ TungDiroot.Rendering/     # Built-in WPF và optional Excel COM image engines
│  ├─ TungDiroot.Presentation/  # View-model/commands không phụ thuộc Revit API
│  └─ TungDiroot.Addin/         # Revit adapters, writers, Extensible Storage và WPF views
├─ tests/
│  ├─ TungDiroot.Core.Tests/
│  ├─ TungDiroot.Rendering.Tests/
│  └─ TungDiroot.Presentation.Tests/
├─ build/                       # Resolve API, build matrix, staging và release
├─ installer/                   # Inno Setup, manifest và portable scripts
├─ tools/                       # Contract/integration test entrypoints
├─ samples/                     # Workbook mẫu deterministic
├─ docs/                        # Design, plans, guides và test reports
├─ artifacts/                   # Build/staging tạm, gitignored
└─ dist/                        # EXE/ZIP/hash phát hành, gitignored
```

`Core` không được tham chiếu ngược lên project khác. `Rendering` và `Presentation` chỉ tham chiếu `Core`. `Addin` được phép tham chiếu cả ba project còn lại cùng Autodesk Revit API. Test project chỉ tham chiếu unit đang kiểm thử; Revit integration được kiểm chứng bằng build contract và checklist trong Revit thật.

## Kiểm tra độ phủ đặc tả

| Phần đặc tả | Task triển khai |
|---|---|
| `.xlsx/.xlsm`, range, style, merge, hidden, formula cache | Plan 1 Tasks 2–4 |
| Fingerprint, absolute/relative path | Plan 1 Task 5 |
| Revit 2024–2026 shell và Ribbon | Plan 2 Task 1; Plan 4 Task 1 |
| Drafting/Legend/Schedule và giới hạn Legend/Ảnh | Plan 2 Tasks 2–4 |
| Built-in/Excel High Fidelity, DPI, font/drawing warning | Plan 2 Tasks 5–6 |
| Multi-range import, preview và cảnh báo 5.000 ô | Plan 2 Task 7 |
| Extensible Storage, recover và safe element scope | Plan 3 Tasks 1–2 |
| Manager UI, batch, pause/stop | Plan 3 Tasks 3–4 |
| Auto Sync, Relink, settings, logging | Plan 3 Tasks 5–6 |
| Worksharing và failure isolation | Plan 3 Task 7 |
| Setup/portable, upgrade/uninstall, docs và release matrix | Plan 4 Tasks 1–5 |

---

## Kế hoạch thành phần và thứ tự thực hiện

1. [Plan 1 — Foundation and Excel Core](./2026-08-12-tung-diroot-01-foundation-excel-core.md)
   - Tạo solution, domain model, Open XML reader, style/range parser, fingerprint và path resolver.
   - Exit: core đọc được workbook mẫu mà không cần Revit hoặc Excel.

2. [Plan 2 — Revit Writers and Hybrid Image](./2026-08-12-tung-diroot-02-revit-writers-image.md)
   - Tạo add-in shell đa phiên bản, view factory, table writers, built-in renderer, Excel COM renderer và luồng import thủ công.
   - Exit: người dùng nhập một hoặc nhiều vùng Excel vào view Revit bằng Bảng/Ảnh.

3. [Plan 3 — Link Manager and Synchronization](./2026-08-12-tung-diroot-03-link-manager-sync.md)
   - Thêm Extensible Storage, manager UI, update data-only/recreate, batch pause/stop, Auto Sync, Relink và logging.
   - Exit: liên kết tồn tại qua lần mở model và cập nhật an toàn theo từng view.

4. [Plan 4 — Packaging and Release](./2026-08-12-tung-diroot-04-packaging-release.md)
   - Build matrix 2024–2026, installer/portable, fixture, kiểm thử, tài liệu và release artifacts.
   - Exit: có EXE setup, portable ZIP, báo cáo kiểm thử và tài liệu bàn giao.

## Quy tắc chuyển giai đoạn

- Không bắt đầu plan tiếp theo khi exit criteria của plan trước chưa đạt.
- Mỗi plan kết thúc bằng một commit checkpoint và `git status --short` phải sạch.
- Lỗi kiểm thử tích hợp Revit không được đánh dấu “pass” bằng unit test thay thế; phải ghi đúng version Revit chưa được kiểm chứng.
- Không tạo artifact phát hành nếu thiếu build của bất kỳ version nào trong 2024–2026.
