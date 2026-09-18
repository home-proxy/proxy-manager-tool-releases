# 08. Hướng dẫn thêm hỗ trợ 1 loại giả lập mới

> Tài liệu này là **checklist các bước cần làm**, không phải code có sẵn để copy-paste. Dùng MuMu Player làm ví dụ minh hoạ, nhưng **tên process/registry key thật của MuMu bạn phải tự xác minh trên máy có cài MuMu thật** trước khi code — số liệu MuMu trong bài này chỉ mang tính gợi ý, chưa được kiểm chứng.

Trước khi làm, đọc [04-application-proxy-assignment.md](04-application-proxy-assignment.md) để hiểu vì sao giả lập cần nhận diện qua "process host" + PID + định danh bền vững, thay vì path như app thường.

## Bước 0 — Xác minh thông tin thật của giả lập mới

Trên máy có cài giả lập đó (ví dụ MuMu Player), cần tự tìm ra:

1. **Tên process "host" thật** — process đứng ra tạo network traffic, không phải launcher UI. Cách tìm: mở Task Manager (tab Details) khi giả lập đang chạy, thử tắt/mở lại từng process để xem process nào biến mất/xuất hiện tương ứng, hoặc dùng công cụ như Process Explorer/Process Monitor để soi. Có thể có nhiều tên khác nhau theo phiên bản (giống LDPlayer có tới 3 biến thể tên).
2. **Cách xác định "instance nào"** khi mở nhiều cửa sổ cùng lúc — thử các hướng: tham số `--instance` trên command line, registry key riêng của phần mềm, tên thư mục chứa VM/data của từng instance, hoặc file config (`.ini`/`.json`/`.xml`) do giả lập tự sinh ra kèm tên hiển thị.
3. **Cổng TCP** (nếu có) mà giả lập forward ra — 1 số giả lập (như MEmu) có thể dùng cổng TCP làm cách phụ để khớp instance, tham khảo `GetTcpListeningPortsByProcessId` trong `EmulatorProcessScanner.cs`.

## Bước 1 — Thêm `EmulatorKind`

File: `src/NetAgent.ProxyManager.Core/Models/EmulatorKind.cs`.

```csharp
public enum EmulatorKind
{
    BlueStacks,
    LDPlayer,
    MEmu,
    Nox,
    MuMu   // thêm giá trị mới
}
```

## Bước 2 — Nhận diện process trong `EmulatorProcessScanner`

File: `src/NetAgent.ProxyManager.Core/Services/EmulatorProcessScanner.cs`.

1. Trong `TryClassifyRuntimeProcess`, thêm điều kiện nhận diện theo tên process host đã xác minh ở Bước 0 (theo mẫu các emulator có sẵn — so khớp `Name`/`ExecutablePath`, có thể kèm điều kiện phụ theo `CommandLine` nếu tên process không đủ đặc trưng, giống cách LDPlayer phải kiểm tra thêm "LDPlayer"/"leidian" trong path/cmdline vì dùng chung tên `VBoxHeadless.exe` với VirtualBox thường).
2. Trong `ResolveInstanceKey`, thêm nhánh đọc định danh bền vững của MuMu (registry/thư mục VM/tham số cmdline đã tìm ở Bước 0).
3. Trong `ResolveInstanceName`, thêm nhánh đọc tên hiển thị thân thiện (đọc từ file config nếu có) — nếu không có nguồn tên rõ ràng, có thể tạm để factory tự sinh tên kiểu `MuMu {index}` như đang làm cho các loại khác khi thiếu thông tin.

## Bước 3 — Viết unit test theo mẫu có sẵn

File: `src/NetAgent.ProxyManager.Tests/EmulatorProcessScannerTests.cs`.

Các class scanner hiện tại **không** unit-test được phần quét WMI trực tiếp (vì phụ thuộc process hệ thống thật) — thay vào đó, test tập trung vào các hàm suy luận thuần (`ResolveInstanceKey`, `ResolveInstanceName`, đọc file config giả). Viết test tương tự cho MuMu: chuẩn bị file config mẫu (fixture), gọi thẳng hàm resolve, assert ra đúng tên/định danh mong đợi. Chạy `dotnet test src/NetAgent.ProxyManager.Tests/NetAgent.ProxyManager.Tests.csproj` để xác nhận pass.

Cũng nên xem `RunningApplicationRuleFactoryTests.cs` và `ApplicationRuleServiceTests.cs` — 2 file test này khoá lại hành vi "không gán nhầm instance đang tắt sang PID của instance khác cùng loại đang chạy". Thêm case test tương tự cho MuMu nếu có nhánh logic riêng.

## Bước 4 — Debug thủ công (bắt buộc, không chỉ dựa vào unit test)

1. Cài MuMu Player thật trên máy dev, mở ít nhất **2 instance** cùng lúc để test được trường hợp phân biệt instance.
2. F5 chạy ProxyManager trong Visual Studio (xem [02-getting-started.md](02-getting-started.md)).
3. Vào tab "Ứng dụng" → "Thêm ứng dụng" → xác nhận cả 2 instance MuMu hiện ra **là 2 dòng riêng biệt**, đúng tên hiển thị nếu bạn có implement `ResolveInstanceName`.
4. Gán proxy cho từng instance, bấm sinh profile Proxifier — mở file `.ppx` sinh ra (mặc định tại `%AppData%\ProxyManager\ProxifierProfiles\active.ppx`), kiểm tra rule tương ứng có đúng `<Applications>pid=<pid-thật></Applications>` không (xem [05-proxifier-integration.md](05-proxifier-integration.md)).
5. **Test restart**: tắt 1 instance MuMu rồi mở lại → vào lại tab "Ứng dụng", refresh, kiểm tra rule cũ có **tự nhận PID mới** mà **không mất proxy đã gán** hay không (đây là hành vi `ApplicationRuleService.RefreshEmulatorRuntimeTargetsAsync` phải xử lý đúng — nếu bạn thêm logic `ResolveInstanceKey` không đủ ổn định, rule có thể bị "lạc" sau restart).
6. Thử mở 3 instance, tắt instance giữa, mở lại — đảm bảo không bị gán nhầm PID sang rule của instance khác (test case tương tự `RefreshEmulatorRuntimeTargetsAsync_ShouldNotMapStoppedRuleToTheOnlyRunningSameKindInstance` cho Nox).

## Bước 5 — Rà lại các chỗ khác có liệt kê cứng `EmulatorKind`

Tìm toàn bộ chỗ code có `switch`/`if` liệt kê từng `EmulatorKind` cụ thể (ví dụ icon hiển thị theo loại giả lập trong UI, text hiển thị tên loại) để đảm bảo MuMu cũng được xử lý đầy đủ, không rơi vào nhánh `default` không mong muốn.

## Tổng kết checklist

- [ ] Xác minh tên process host + cách phân biệt instance thật trên máy có cài giả lập
- [ ] Thêm `EmulatorKind.MuMu`
- [ ] Cập nhật `TryClassifyRuntimeProcess`, `ResolveInstanceKey`, `ResolveInstanceName`
- [ ] Viết/cập nhật unit test
- [ ] Debug tay: thêm rule, gán proxy, kiểm tra file `.ppx`
- [ ] Debug tay: tắt/mở lại instance, xác nhận không mất proxy đã gán, không gán nhầm
- [ ] Rà các chỗ UI/logic khác có liệt kê cứng theo `EmulatorKind`
