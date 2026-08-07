# Sửa AutoCAD đơ/lag khi MEPDB + đăng nhập

## Nguyên nhân

AutoCAD chỉ có **1 luồng UI chính**. Plugin v0.13 khi chạy `MEPDB`:

1. Mở cửa sổ login (WPF/WinForms)
2. Gọi HTTP tới License Server: OTP → activate device → check license
3. Mỗi lần `MEPDB` còn **hỏi server lại** quyền feature

Nếu code dùng:

```csharp
await api.CallAsync().GetAwaiter().GetResult();  // trên luồng UI
// hoặc
api.CallAsync().Result;
```

→ luồng AutoCAD **bị block** → cảm giác **đơ/lag** (5–30 giây).

Repo `du-cursor` đã sửa từ **v0.2.3** bằng `AsyncRunner` (chạy HTTP trên thread pool).  
**DLL v0.13 trong bundle chưa có fix này** (build từ MepPanelMvp cũ hơn).

---

## Cách xử lý tạm (không sửa code)

1. **Giữ License Server chạy (F5)** — server tắt = chờ timeout = lag lâu hơn
2. Sau khi bấm **Đăng nhập**, **đợi 10–20 giây** — đang gọi 3 API liên tiếp
3. Dùng `http://192.168.1.7:5268` (cùng máy/LAN), tránh WiFi chậm
4. Lần sau đã login vẫn có thể hơi chậm vì mỗi `MEPDB` vẫn check server

---

## Cách sửa dứt điểm (build lại plugin)

Trong source **MepPanelMvp** (`src/MepPanel.AutoCAD.Licensing/` hoặc tương đương):

### 1. Thêm file `AsyncRunner.cs`

Copy từ repo:

`src/MepPanel.AutoCAD.Licensing/AsyncRunner.cs`

```csharp
internal static class AsyncRunner
{
    public static T Run<T>(Func<Task<T>> work)
        => Task.Run(work).GetAwaiter().GetResult();

    public static void Run(Func<Task> work)
        => Task.Run(work).GetAwaiter().GetResult();
}
```

### 2. Sửa `LoginWindow` — nút Gửi OTP / Đăng nhập

Thay mọi chỗ gọi API trực tiếp trên event click bằng:

```csharp
AsyncRunner.Run(() => _apiClient.RequestOtpAsync(phone));
AsyncRunner.Run(() => CompleteLoginAsync(phone, otp));
```

Tham khảo: `src/MepPanel.AutoCAD.Licensing/LoginWindow.cs` trong repo `du-cursor`.

### 3. Sửa `LicenseGuard.EnsureAuthorized`

```csharp
CheckLicenseResponse response = AsyncRunner.Run(
    () => client.CheckLicenseAsync(phone, pluginVersion));
```

Tham khảo: `src/MepPanel.AutoCAD.Licensing/LicenseGuard.cs`.

### 4. Build lại và cài

```powershell
.\scripts\build-plugin-release.ps1
```

Khởi động lại AutoCAD → test `MEPDB` — UI không còn đơ lâu.

---

## Lag do load panel WPF

Sau khi hết lag login, lần đầu mở panel MEPDB (WPF) vẫn có thể chật **1–2 giây** — bình thường do load UI, không phải license.
