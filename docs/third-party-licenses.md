# Third-party licenses

TUNG_DIROOT sử dụng `DocumentFormat.OpenXml` 2.20.0 của Microsoft để đọc/ghi Open XML. Package được phân phối theo MIT License. Nguồn và license chính thức: [Open XML SDK](https://github.com/dotnet/Open-XML-SDK).

`Newtonsoft.Json` 13.0.1 được dùng để serialize metadata/settings và khớp với assembly JSON do Revit 2024 cung cấp. Package được phân phối theo MIT License; xem [Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json).

`System.Memory` và một số thư viện hỗ trợ .NET của Microsoft/.NET Foundation được dùng ở runtime với đúng assembly version do Revit 2024 nạp sẵn. Chúng được phân phối theo MIT License; xem [.NET Runtime](https://github.com/dotnet/runtime).

xUnit, Microsoft.NET.Test.Sdk và xunit.runner.visualstudio chỉ là dependency phát triển/kiểm thử, không phải runtime payload add-in. Mỗi dự án tuân theo license của package tương ứng.

Autodesk Revit API assemblies là tài sản của Autodesk. `RevitAPI.dll` và `RevitAPIUI.dll` chỉ được tham chiếu từ Revit cài trên máy build/khách và tuyệt đối không đóng gói trong Setup/ZIP. TUNG_DIROOT không phân phối Autodesk Revit.

Microsoft Excel là tùy chọn cho renderer High Fidelity và không được đóng gói. Built-in renderer không yêu cầu Excel. TUNG_DIROOT không dùng hoặc phân phối binary/source của DiRoots, One/SheetLink/TableGen, Naviate, TungMEP hay TUNGMEP2.

File `installer/THIRD-PARTY-NOTICES.txt` đi kèm artifact là bản tóm tắt notices. Khi nâng phiên bản package, cần cập nhật tài liệu này, lock/central package version và kiểm tra lại license trước release.
