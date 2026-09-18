# 02. Cài môi trường & Chạy thử lần đầu

> Đọc xong tài liệu này bạn sẽ: cài được Visual Studio đúng cấu hình, mở được solution, chạy (F5) và debug được ứng dụng trên một máy **chưa cài .NET gì cả**, và chạy được bộ unit test.

## 1. Yêu cầu máy

- Windows 10/11 64-bit (WinForms chỉ chạy trên Windows).
- Không cần cài .NET SDK/Runtime riêng trước — Visual Studio sẽ tự mang theo khi cài đúng workload (xem bước 2).
- (Tuỳ chọn) [Inno Setup](https://jrsoftware.org/isinfo.php) 6 hoặc 7, chỉ cần khi build file cài đặt (`.exe` installer) — xem [06-build-and-release.md](06-build-and-release.md).

## 2. Cài Visual Studio

Ứng dụng target `net8.0-windows` (WinForms). Vì máy intern có thể **chưa từng cài .NET runtime nào**, cách chắc ăn nhất là để Visual Studio tự mang theo SDK:

1. Tải **Visual Studio 2022** (bản Community là đủ) từ [visualstudio.microsoft.com](https://visualstudio.microsoft.com/).
2. Trong Visual Studio Installer, tick chọn workload **".NET desktop development"** (".NET Desktop Development").
   - Workload này tự động cài kèm **.NET 8 SDK** (bao gồm cả runtime) — không cần bạn tải `.NET 8 SDK` riêng từ trang dotnet.microsoft.com. Máy sau khi cài xong VS là build/run được ngay, không cần bước cài .NET nào khác.
3. Cài xong, mở PowerShell kiểm tra nhanh:
   ```powershell
   dotnet --version   # phải ra bản 8.x
   ```

> Nếu vì lý do nào đó bạn không dùng Visual Studio mà chỉ dùng VS Code/terminal, thì phải tự cài [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) riêng — VS Code không tự mang SDK như Visual Studio.

## 3. Clone & mở solution

```powershell
git clone <repo-url>
cd netagent-proxy-manager
```

Mở file solution bằng Visual Studio (double-click file `.sln`/`.slnx` ở gốc repo, hoặc `File > Open > Project/Solution`).

Solution có 4 project (xem chi tiết ở [01-architecture.md](01-architecture.md)):
- `NetAgent.ProxyManager.Core`
- `NetAgent.ProxyManager.Infrastructure`
- `NetAgent.ProxyManager.App` ← project khởi chạy (Startup Project)
- `NetAgent.ProxyManager.Tests`

Trong **Solution Explorer**, đảm bảo `NetAgent.ProxyManager.App` được đặt làm **Startup Project** (click phải → "Set as Startup Project") trước khi F5.

## 4. Build & chạy bằng CLI (không cần mở VS)

```powershell
# Build toàn bộ solution
dotnet build src/NetAgent.ProxyManager.App/NetAgent.ProxyManager.App.csproj

# Chạy app (mặc định dùng appsettings.Development.json vì ASPNETCORE/DOTNET_ENVIRONMENT=Development khi chạy bằng dotnet run)
dotnet run --project src/NetAgent.ProxyManager.App/NetAgent.ProxyManager.App.csproj
```

## 5. Debug bằng Visual Studio (F5)

1. Đặt breakpoint ở đâu cần (ví dụ trong `MainForm.cs` hoặc 1 service trong `Core`).
2. Nhấn **F5** (Start Debugging) hoặc **Ctrl+F5** (chạy không debug).
3. Visual Studio tự build rồi chạy `ProxyManager.App.exe`, breakpoint sẽ dừng lại như bình thường.

Ứng dụng có 2 file cấu hình môi trường:
- `appsettings.Development.json` — dùng backend **dev** (`dev-api.homeproxy.vn`), áp dụng khi chạy qua `dotnet run`/F5 trong Visual Studio.
- `appsettings.Production.json` — dùng backend **production** (`api.homeproxy.vn`), áp dụng cho bản build release gửi cho end-user.

Khi cần test với dữ liệu/tài khoản thật trên production, có thể tạm đổi environment (biến `Environment` trong `appsettings.json`) — nhưng nhớ đổi lại trước khi commit.

## 6. Chạy unit test

```powershell
dotnet test src/NetAgent.ProxyManager.Tests/NetAgent.ProxyManager.Tests.csproj
```

Hoặc trong Visual Studio: **Test > Test Explorer**, rồi "Run All". Nên chạy test này sau mỗi lần sửa code trong `Core`/`Infrastructure` — rất nhiều logic quan trọng (gán proxy, nhận diện emulator, tính hợp lệ rule...) được test khá kỹ ở đây, đọc test trước khi đọc code thật cũng là một cách hiểu nhanh flow.

## Đọc tiếp

- [03-sidebar-tabs-data-flow.md](03-sidebar-tabs-data-flow.md) — luồng dữ liệu từng tab trong sidebar.
- [04-application-proxy-assignment.md](04-application-proxy-assignment.md) — flow thêm app/giả lập và gán proxy.
