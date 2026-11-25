# 10. Gắn proxy cho toàn bộ máy

> Đọc xong tài liệu này bạn sẽ hiểu: chế độ "Gắn proxy cho toàn bộ máy" hoạt động thế nào, nó khác gì so với gắn proxy theo từng ứng dụng, và các giới hạn cần biết khi hỗ trợ khách hàng.

## 1. Nó làm gì?

Bình thường, ứng dụng nào **không** có rule ở tab "Ứng dụng" sẽ đi thẳng ra mạng (rule `Default` = `Direct`). Khi bật "Gắn proxy cho toàn bộ máy" ở tab **Thiết lập → Cài đặt chung**, rule `Default` đổi thành `Proxy <id>`: mọi ứng dụng chưa có rule riêng đều đi qua proxy đã chọn.

Các rule theo ứng dụng / giả lập / Chrome profile vẫn nằm **phía trên** `Default`, nên ứng dụng đã gắn proxy riêng vẫn giữ proxy của nó.

## 2. Cấu hình lưu ở đâu?

`AppSettings.SystemProxyId` (`Guid?`, lưu trong file settings): `null` = tắt, có giá trị = `Id` của `ProxyServer` dùng cho toàn bộ máy.

Proxy được chọn qua đúng hộp thoại `ChangeApplicationProxyForm` của nút "Gắn Proxy" ở tab "Ứng dụng" (`SystemProxySettingsControl.PickProxyAsync`, gọi `ChangeApplicationProxyForm.PickProxyAsync` dùng chung), nên liệt kê đủ đơn hàng đã mua (tĩnh, xoay, datacenter) lẫn proxy tự thêm. Đơn hàng đã mua chỉ được chép vào kho proxy trên máy (`UpsertBackendOrderAsync`) tại lúc được chọn.

## 3. Profile sinh ra trông thế nào?

`ProxifierProfileBuilder` nhận `ProxifierProfileModel.DefaultProxyId`. Thứ tự rule khi bật:

1. `Localhost` → Direct.
2. `ProxyManager` (chính tool) → Direct, để tool luôn gọi được API kể cả khi proxy chết.
3. Rule của từng ứng dụng → Proxy riêng.
4. `Chrome Profile leak guard` → Block (nếu có).
5. **`Local network`** → Direct cho `10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`, `169.254.0.0/16`, `*.local`. Chỉ sinh ra khi bật chế độ này: proxy ở xa không vào được mạng LAN của người dùng, thiếu rule này thì trang router, máy in, ổ mạng sẽ hỏng.
6. `Default` → **Proxy** (thay cho Direct/Block).

## 4. Khi proxy toàn máy không dùng được

`SystemProxyEligibility` (Core) áp cùng tiêu chuẩn với proxy gắn cho ứng dụng: phải tồn tại, endpoint hợp lệ, chưa hết hạn.

| Trạng thái | Rule `Default` |
|---|---|
| Tắt (`SystemProxyId = null`) | theo `DefaultRouteDirect` như cũ |
| Proxy dùng được | `Proxy` |
| Proxy bị xoá / hết hạn / sai endpoint | **`Block`** |

Chọn `Block` chứ không rơi về `Direct` là có chủ đích: người đã đặt cả máy sau proxy không được phép bị đưa ngầm về IP thật. `MainForm.WarnIfSystemProxyUnusable` báo cho người dùng một lần, và nhãn ở tab Thiết lập chuyển sang màu đỏ.

Ngoại lệ: **đăng xuất**. Proxy mua qua tài khoản không còn dùng được sau khi đăng xuất, nên `ClearBackendSystemProxyForLogoutAsync` tắt hẳn chế độ này (giống cách các rule ứng dụng bị gỡ proxy khi đăng xuất).

## 5. Nút "Bắt đầu"

Session khởi động được khi có ít nhất một rule ứng dụng hợp lệ **hoặc** proxy toàn máy dùng được. Bật chế độ này khi session đang dừng sẽ tự bắt đầu session; đổi/tắt khi đang chạy sẽ tự nạp lại profile.

## 6. Giới hạn cần biết

- **UDP**: profile bật `LeakPreventionMode`, traffic UDP không proxy được sẽ bị chặn. Web vẫn chạy (QUIC lùi về TCP), nhưng game, gọi thoại/video có thể hỏng.
- **Không phải VPN**: `ping` (ICMP) và traffic tầng kernel không đi qua proxy.
- **Windows service và tài khoản Windows khác không được xử lý**: `ProcessServices` và `ProcessOtherUsers` trong profile vẫn là `false`, nên ví dụ Windows Update vẫn đi IP thật. Bật hai cờ này sẽ đưa cả dịch vụ DNS của Windows vào Proxifier; chưa kiểm chứng trên máy thật nên chưa bật.
- **Chỉ có hiệu lực khi session đang chạy**: bấm "Dừng" hoặc thoát tool là máy về IP thật.
- Ứng dụng đang mở sẵn giữ các kết nối cũ; kết nối mới mới đi qua proxy.

## 7. Kiểm thử tay

1. Thiết lập → tick "Gắn proxy cho toàn bộ máy", chọn proxy → xác nhận → session tự chạy.
2. Mở trình duyệt bất kỳ **chưa** có rule, vào trang xem IP → ra IP của proxy.
3. Ứng dụng có rule riêng → vẫn ra IP của proxy riêng.
4. Vào `192.168.x.1` (router) → vẫn truy cập được.
5. Tool vẫn tải được danh sách proxy/đơn hàng (đi Direct).
6. Bỏ tick → trình duyệt ở bước 2 về IP thật (với kết nối mới).
7. Xoá proxy đang dùng cho toàn máy khi session chạy → có cảnh báo, ứng dụng chưa có rule mất mạng, không lộ IP thật.

## Đọc tiếp

- [05-proxifier-integration.md](05-proxifier-integration.md) — Proxifier và Proxification Rule.
