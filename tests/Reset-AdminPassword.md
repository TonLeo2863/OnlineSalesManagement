# Đặt lại mật khẩu Admin bằng công cụ cục bộ

## Chẩn đoán Apply: ArrayIndex (2026-10-19)

Đã tái hiện trên database riêng: `NotSupportedException`, HRESULT `0x80131515`, thông báo cố định `The LINQ expression node type 'ArrayIndex' is not supported in LINQ to Entities.` Lỗi xuất hiện khi truy vấn `db.Users.Single(u => u.Id == args[0])` ở bước kiểm tra lại tài khoản trong transaction; `ValidateOpen` cũng có cùng lỗi. Preview không chạy các truy vấn này. Chưa tới GeneratePasswordResetToken hoặc ResetPassword nên lỗi này không phải mật khẩu yếu, token hay database từ chối cập nhật. Catch cuối cũ che exception, còn wrapper PowerShell chỉ báo exit code chung.

Đã sửa bằng cách lấy `args[0]` ra biến `id` trước khi dựng biểu thức LINQ. Giữ kiểm tra UserId/email/server/database/Admin và transaction Serializable. Test hiện chạy cấu hình Web.config như công cụ thật, chỉ thay kết nối bằng database mới `AdminReset_Test_<GUID>`; không dùng OnlineSalesDb. Test cũ chỉ gọi Reset trực tiếp nên bỏ sót bước kiểm tra lại tài khoản gây lỗi.

Mã lỗi an toàn hiện tại:

| Mã | Ý nghĩa |
| --- | --- |
| PASSWORD_MISMATCH | Hai lần nhập khác nhau; chưa reset |
| PASSWORD_POLICY | PasswordValidator từ chối; chỉ hiện quy tắc cố định |
| TOKEN_PROVIDER_MISSING / TOKEN_CRYPTO_ERROR / TOKEN_INVALID | Thiếu provider, lỗi bảo vệ token hoặc token không hợp lệ |
| RESET_REJECTED | ResetPassword trả IdentityResult thất bại sau precheck; không quy chụp là mật khẩu yếu |
| DATABASE_ERROR | Lỗi SQL; chỉ hiện stage, number, state, class |
| DATABASE_ENTITY_VALIDATION / DATABASE_CONCURRENCY | EF từ chối lưu hoặc xung đột dữ liệu |
| LINQ_ARRAY_INDEX | Truy vấn EF6 chứa truy cập mảng; lỗi lập trình đã sửa |
| DEPENDENCY_ERROR / CONFIGURATION_ERROR / OPERATION_ERROR | Lỗi còn lại; stage và HRESULT, không in exception thô |

Nếu thử lại, chạy Preview rồi Apply bằng đúng các tham số đã xác minh ở dưới. Không cần đổi mật khẩu để né lỗi ArrayIndex. Khi thành công công cụ báo `Password reset complete`; nếu còn lỗi, chỉ gửi mã lỗi và stage, không gửi mật khẩu/hash/token/connection string. Chưa Apply lên tài khoản thật trong quá trình chẩn đoán.

Các ghi nhận SSPI và 13 PASS ở cuối tài liệu là lịch sử của bộ test cũ, không phải nguyên nhân lỗi Apply này. Test mới không xuất `ex.ToString()`; log chỉ chứa mã lỗi an toàn.

Kết quả cuối: 19 PASS trên `AdminReset_Test_11b272b0d9834f94aa8176f51c575c49`. Bao gồm tái hiện lỗi ArrayIndex cũ, kiểm tra lại tài khoản trong transaction sau sửa, đọc credential đã commit từ context mới, giữ role Admin và đơn hàng/khách hàng mẫu cùng liên kết UserId. Database test được giữ lại, không tự xóa.

Chạy trong Windows Terminal/PowerShell trên máy vận hành, bằng tài khoản Windows được cấp quyền SQL phù hợp. Hạn chế quyền đọc/chạy thư mục công cụ bằng ACL cho người vận hành. Công cụ không cung cấp API hay URL; quyền truy cập SQL của Windows là ranh giới quyền vận hành, không yêu cầu tài khoản Windows phải thuộc nhóm Administrators.

Build trước khi chạy:

```powershell
msbuild .\OnlineSalesManagement.sln /t:Build /p:Configuration=Debug /v:minimal /nologo
```

Thay `USER-ID-THAT` và `admin@example.com` bằng định danh chính xác. Email được so sánh chính xác, kể cả chữ hoa/thường. Server được đối chiếu với `SERVERPROPERTY('ServerName')`, database với `DB_NAME()` sau khi kết nối bằng DefaultConnection trong Web.config. Hai tham số Server/Database là giá trị kỳ vọng để bảo vệ thao tác, không chuyển đích kết nối.

Preview chỉ đọc:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\Reset-AdminPassword.ps1 -UserId 'USER-ID-THAT' -Email 'admin@example.com' -Server 'DESKTOP-AUTD3A3\SQLEXPRESS' -Database 'OnlineSalesDb' -Preview
```

Chỉ khi Preview xác nhận đúng tài khoản Admin và database, người vận hành mới chạy Apply:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\Reset-AdminPassword.ps1 -UserId 'USER-ID-THAT' -Email 'admin@example.com' -Server 'DESKTOP-AUTD3A3\SQLEXPRESS' -Database 'OnlineSalesDb' -Apply
```

Nhập nguyên câu xác nhận `RESET <UserId> <Email> ON <Server>/<Database>` mà công cụ hiển thị. Sau đó nhập mật khẩu mới hai lần; ký tự không hiển thị. Không truyền mật khẩu qua tham số, pipe, biến môi trường hoặc file. Chính sách hiện tại: ít nhất 6 ký tự, có chữ hoa, chữ thường, số và ký tự đặc biệt. Identity cần chuỗi mật khẩu tạm trong bộ nhớ; vùng BSTR trung gian được xóa sau khi chuyển từ SecureString.

Công cụ dùng `GeneratePasswordResetToken` và `ResetPassword`, kiểm tra stamp đã đổi và commit trong transaction. Cookie ứng dụng cũ bị từ chối ở request tiếp theo vì ứng dụng kiểm tra SecurityStamp với interval bằng 0. Không xóa tài khoản, không đổi role, không sửa PasswordHash bằng SQL, không chạy migration. Công cụ không tự mở khóa tài khoản đang bị lockout; nếu có, chờ hết thời gian khóa theo cấu hình.

## Kiểm thử và chẩn đoán

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\Test-ResetAdminPassword.ps1
```

Test biên dịch hai file C# bằng .NET Framework csc, chọn entry point ResetAdminPasswordTests, tham chiếu DLL trong OnlineSalesManagement/bin. Exe.config chỉ giữ các mục EF/runtime cần thiết, runtime .NET Framework 4.8.1 và một DefaultConnection mới. Test kết nối shared memory tới SQL Express trên máy này bằng Windows authentication, tạo database mới `AdminReset_Test_<GUID>`, giữ lại để chẩn đoán, không xóa database. C# chặn tên database khác mẫu trước khi kết nối. Schema fixture được tạo bằng ObjectContext.CreateDatabase, không gọi DbMigrator, không sao chép dữ liệu thật.

Nếu lỗi, Main bắt exception và xuất `ex.ToString()` sau khi che mật khẩu test, connection string, trường nhạy cảm và chuỗi token dài. Log: `OnlineSalesManagement/bin/ResetAdminPasswordTests.error.log`; bin và *.log đã bị Git ignore. Không đưa log thô hay bí mật vào Git.

Chẩn đoán ngày 2026-10-19: lỗi Application Error 0xe0434352 do SqlException không được bắt: `The target principal name is incorrect. Cannot generate SSPI context.` trong bước tạo database. Cùng test qua shared memory vẫn lỗi SSPI trong sandbox, nhưng Windows authentication ngoài sandbox thành công. Không thay SPN, dịch vụ SQL hay cấu hình database thật. .NET Framework 4.8.1 có trên máy; EF package 6.5.1 (assembly version 6.0.0.0), Identity package 2.2.4 (assembly version 2.0.0.0), OWIN 4.2.2. Cảnh báo CS1701 do Identity tham chiếu OWIN 3.0.1 được xử lý bằng binding redirect OWIN 4.2.2 trong config; không phải lỗi gây crash đã ghi nhận.

Build solution thành công. Test thành công với 13 PASS trên `AdminReset_Test_603076731edd40b994cf4a3003b5a98e`: từ chối sai ID/email/server/database, từ chối non-Admin và mật khẩu yếu, Preview giữ hash/stamp, mật khẩu mới hoạt động và mật khẩu cũ thất bại, stamp đổi, tài khoản và role được giữ nguyên. Chưa Apply tài khoản Admin thật.
