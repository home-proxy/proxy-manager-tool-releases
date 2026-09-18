# ProxyManager (NetAgent.ProxyManager)

Ứng dụng Windows Forms (.NET 8) giúp người dùng mua, quản lý và gán proxy cho từng ứng dụng/giả lập chạy trên máy, thông qua backend **HomeProxy** và tích hợp với **Proxifier** để định tuyến traffic theo từng ứng dụng.

## Tài liệu

Tài liệu dành cho intern/dev mới, đọc theo thứ tự số:

| File | Nội dung |
| --- | --- |
| [docs/01-architecture.md](docs/01-architecture.md) | Giới thiệu ứng dụng, kiến trúc solution, các tầng, luồng dữ liệu, dependency injection |
| [docs/02-getting-started.md](docs/02-getting-started.md) | Cài Visual Studio/môi trường, build, chạy, debug, chạy test lần đầu |
| [docs/03-sidebar-tabs-data-flow.md](docs/03-sidebar-tabs-data-flow.md) | Luồng dữ liệu từng tab trong sidebar (Ứng dụng, Proxy, Mua hàng, Thiết lập...) |
| [docs/04-application-proxy-assignment.md](docs/04-application-proxy-assignment.md) | Flow thêm ứng dụng/giả lập và gán proxy — app thường theo path, giả lập theo PID |
| [docs/05-proxifier-integration.md](docs/05-proxifier-integration.md) | Cách app cài đặt/điều khiển Proxifier, sinh profile, giải thích Proxification Rule |
| [docs/06-build-and-release.md](docs/06-build-and-release.md) | Build bản publish (self-contained/framework-dependent), đóng gói installer, phát hành GitHub Release |
| [docs/07-branding-assets.md](docs/07-branding-assets.md) | Luồng gán logo & merchant id theo từng đại lý |
| [docs/08-adding-new-emulator.md](docs/08-adding-new-emulator.md) | Checklist thêm hỗ trợ một loại giả lập mới |

## Cấu trúc solution

```
src/
  NetAgent.ProxyManager.Core/            Domain models, interfaces, business logic (không phụ thuộc UI/HTTP/registry cụ thể)
  NetAgent.ProxyManager.Infrastructure/  Implementation: gọi API backend, lưu file JSON, DPAPI, registry Windows
  NetAgent.ProxyManager.App/             WinForms UI + composition root (Program.cs)
  NetAgent.ProxyManager.Tests/           Unit test (xUnit + FluentAssertions)
installer/       Script Inno Setup (.iss) để đóng gói file cài đặt .exe
scripts/         PowerShell script build & đóng gói release (build-installer.ps1)
docs/            Tài liệu kỹ thuật
```

Xem chi tiết từng tầng tại [docs/01-architecture.md](docs/01-architecture.md).

## Yêu cầu môi trường

- Windows 10/11 (WinForms chỉ chạy trên Windows)
- .NET 8 SDK trở lên
- (Tuỳ chọn, để build installer) Inno Setup 6 hoặc 7

## Chạy nhanh

```powershell
# Build toàn bộ solution
dotnet build src/NetAgent.ProxyManager.App/NetAgent.ProxyManager.App.csproj

# Chạy app (mặc định môi trường Development theo appsettings.Development.json)
dotnet run --project src/NetAgent.ProxyManager.App/NetAgent.ProxyManager.App.csproj

# Chạy toàn bộ unit test
dotnet test src/NetAgent.ProxyManager.Tests/NetAgent.ProxyManager.Tests.csproj
```

Chi tiết setup lần đầu, cấu hình môi trường, cách debug: xem [docs/02-getting-started.md](docs/02-getting-started.md).

## Ghi chú

- Trước khi build release/installer, phải copy `ProxifierSetup.exe` thật vào `src/NetAgent.ProxyManager.App/Installers/` và cập nhật hash SHA-256 tương ứng — xem [docs/05-proxifier-integration.md](docs/05-proxifier-integration.md) và [docs/06-build-and-release.md](docs/06-build-and-release.md).
