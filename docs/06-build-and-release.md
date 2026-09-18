# 06. Build & Release cho end-user

> Đọc xong tài liệu này bạn sẽ biết: cách build ra 2 loại bản cài (self-contained và framework-dependent), cách đóng gói installer, cách phát hành lên GitHub Releases, và vì sao website đại lý lấy đúng được bản mới nhất.

## 1. Hai kiểu publish: Self-contained vs Framework-dependent

| | Self-contained | Framework-dependent |
|---|---|---|
| Máy end-user cần cài .NET runtime trước? | **Không** — file .exe tự mang theo .NET 8 runtime | **Cần** — installer sẽ tự âm thầm cài ".NET 8 Desktop Runtime" nếu máy chưa có |
| Kích thước installer | Lớn hơn | Nhỏ hơn |
| Dùng khi nào | Đây là bản **đang dùng để phát hành cho khách** (upload GitHub Releases, tải qua website đại lý) | Dùng khi muốn installer nhẹ và chấp nhận cài thêm runtime |

Cả 2 đều build ra 1 file `.exe` duy nhất (`PublishSingleFile=true`) chạy trên `win-x64`.

> **Quan trọng (từ bản này):** file `appsettings.json`, `appsettings.Production.json`, toàn bộ `Assets/**` và (nếu có) `Installers/ProxifierSetup.exe` được **nhúng thẳng vào assembly** (`<EmbeddedResource>` trong `NetAgent.ProxyManager.App.csproj`). Nhờ vậy chỉ cần **1 file `ProxyManager.App.exe`** là chạy được — người dùng tải về **không phải copy thêm file cấu hình/icon nào cạnh exe**. Nếu trên đĩa vẫn có `appsettings*.json` (bản installer/dev) thì các file đó vẫn được **override** đè lên bản nhúng. Xem `src/NetAgent.ProxyManager.App/Theme/EmbeddedAssets.cs`.
>
> - **Branding theo đại lý** vẫn giữ nguyên: `assets/merchant_id.txt` + logo trong zip đại lý được `AppBranding` áp vào lúc khởi động; merchant id được ghi ra `appsettings.Production.json` trên đĩa (seed từ bản nhúng nếu chưa có file) nên **mỗi đại lý một merchant id riêng**.
> - **Proxifier tự cài ngầm:** nếu bạn commit `src/NetAgent.ProxyManager.App/Installers/ProxifierSetup.exe` (hash khớp `Proxifier:InstallerSha256`), file này được nhúng vào exe; lần đầu mở app sẽ tự giải nén ra `%AppData%\ProxyManager\Installers\` và **cài Proxifier ngầm** (`MainForm.TryAutoInstallProxifierSilentlyAsync`). Không có file thì bước này bỏ qua im lặng, CI vẫn build được.

## 2. Build bằng script `scripts/build-installer.ps1`

Không cần set gì trong file `.csproj` — mọi lựa chọn RID/self-contained/single-file đều truyền qua **tham số dòng lệnh** của `dotnet publish`, do script này lo:

```powershell
# Bản self-contained (mặc định, không cần .NET trên máy khách)
powershell scripts/build-installer.ps1 -Version 1.2.0 -DeploymentMode SelfContained

# Bản framework-dependent
powershell scripts/build-installer.ps1 -Version 1.2.0 -DeploymentMode FrameworkDependent
```

Script sẽ:
1. `dotnet publish` project `App` với `-r win-x64`, `--self-contained true|false`, `-p:PublishSingleFile=true`, gán version từ `-Version` → output vào `artifacts/publish/win-x64-self-contained/` (hoặc `win-x64-framework-dependent/`).
2. Copy `Installers/ProxifierSetup.exe` vào thư mục publish (đây là bản Proxifier gốc, xem [05-proxifier-integration.md](05-proxifier-integration.md)).
3. Gọi `ISCC.exe` (Inno Setup Compiler) build file `installer/HomeProxy.ProxyManager.iss` → ra installer cuối cùng tại `artifacts/installer/ProxyManager.Setup-<suffix>-<version>.exe`.

> Cần cài **Inno Setup 6/7** trước, và copy `ProxifierSetup.exe` thật + cập nhật `InstallerSha256` trong `appsettings.json` — xem `src/NetAgent.ProxyManager.App/Installers/README.md`.

Installer (`.iss`) khi chạy trên máy khách sẽ: cài .NET Desktop Runtime nếu cần (chỉ bản framework-dependent), cài Proxifier ngầm nếu máy chưa có, copy toàn bộ file app vào thư mục cài, rồi chạy `ProxyManager.App.exe --prepare-branding` (xem [07-branding-assets.md](07-branding-assets.md)) để gắn logo/merchant id trước khi mở app lần đầu.

## 3. Phát hành lên GitHub Releases

File `.exe` (bản **self-contained**, để khách không cần cài gì thêm) được đăng lên **GitHub Releases** của repo này. Có 2 cách:

### 3a. Tự động bằng GitHub Actions (khuyến nghị)

Repo có workflow `.github/workflows/release.yml`. Nó chạy trên `windows-latest`: test → `dotnet publish` single-file self-contained → **tự tạo Release và đính kèm đúng 1 file `ProxyManager.App.exe`**.

Cách kích hoạt:

```bash
# tạo tag version rồi push -> workflow tự build & release
git tag v1.2.0
git push origin v1.2.0
```

Hoặc chạy tay từ tab **Actions → Release ProxyManager → Run workflow** (nhập version) để build thử mà không tạo release.

### 3b. Thủ công (không cần CI)

1. Build bản self-contained như mục 2, lấy file `.exe`.
2. Trên GitHub: **Releases → Draft a new release**, đặt tag (ví dụ `v1.2.0`), upload file `ProxyManager.App.exe` làm release asset, publish.
3. Đảm bảo asset là file **duy nhất** có đuôi `.exe` trong release đó.

> Đây là action **publish artifact công khai** — kiểm tra đúng version/asset trước khi publish, vì website đại lý sẽ tự động lấy release "latest" ngay khi có.

## 2b. Các bước đẩy code lên repo lần đầu

Nếu thư mục chưa phải git repo (chưa có `.git`), khởi tạo và trỏ về remote đã có:

```bash
git init
git add .
git commit -m "Embed config + assets so the single-file exe runs standalone"
git branch -M main
git remote add origin <link-github-repo-remote>   # bỏ qua nếu đã add
git push -u origin main
```

`bin/`, `obj/`, `artifacts/` đã nằm trong `.gitignore` nên không bị đẩy lên. Sau đó phát hành theo mục 3a bằng cách push tag.

## 4. Vì sao website đại lý luôn lấy đúng bản mới nhất

Website bán proxy của đại lý (repo `homeproxy-resell`, không nằm trong repo này) có 1 trang **"Tool Proxy"** cho khách tải app về:

- `src/features/tool-proxy/tool-proxy-page.tsx` — trang UI, có nút "Tải về" gọi tới `/api/tool-proxy/download`.
- `src/app/api/tool-proxy/download/route.ts` — API route xử lý: gọi **GitHub Releases API** lấy release `latest` của repo này (`resolveLatestGitHubReleaseExe`, dùng biến môi trường `TOOL_PROXY_GITHUB_OWNER`/`TOOL_PROXY_GITHUB_REPO` để biết repo nào, có cache ~5 phút), tìm asset `.exe` trong release đó, tải về, rồi **nén lại thành 1 file `ProxyManager.zip`** (có mật khẩu) kèm theo logo/merchant id của đại lý (xem [07-branding-assets.md](07-branding-assets.md)) trước khi trả cho khách.

Nói cách khác: **chỉ cần publish đúng 1 GitHub Release mới với đúng 1 file `.exe` asset, website đại lý sẽ tự lấy được ngay** (sau tối đa vài phút cache) — không cần sửa gì ở phía `homeproxy-resell`. Đây là lý do phải cẩn thận khi publish release: publish sai/thiếu asset sẽ ảnh hưởng trực tiếp tới trải nghiệm tải app của toàn bộ khách hàng đang online.

## Đọc tiếp

- [07-branding-assets.md](07-branding-assets.md) — logo & merchant id được gắn vào app như thế nào.
