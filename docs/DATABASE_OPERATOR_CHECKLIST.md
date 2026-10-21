# Database demo và backup do người vận hành thực hiện

Đích đã xác minh trên PC: `MAY042\SQLEXPRESS`, `OnlineSalesDb`; Windows Authentication. Database đã có đơn và lịch sử, không phải database mới. Phiên kiểm tra chỉ đọc; không cấp Admin, không seed/migrate trên DB này.

## Kiểm tra trước demo

```sql
SELECT @@SERVERNAME AS InstanceName, DB_NAME() AS DatabaseName;
SELECT MigrationId FROM dbo.__MigrationHistory ORDER BY MigrationId;
SELECT COUNT(*) AS UsersCount FROM dbo.AspNetUsers;
SELECT COUNT(*) AS AdminCount
FROM dbo.AspNetUsers u
JOIN dbo.AspNetUserRoles ur ON u.Id=ur.UserId
JOIN dbo.AspNetRoles r ON r.Id=ur.RoleId
WHERE r.Name=N'Admin';
```

Migrations mong đợi: `202610131032209_InitialCreate`, `202610151148216_AddShiftManagementTables`. Không chạy Update-Database chỉ để tạo tài khoản. SampleDataSeeder không tạo Identity user; bỏ qua nếu có Category hoặc Product, không dùng để sửa database đang có dữ liệu.

Hiện có 0 user/role: chưa có Admin hợp lệ cho demo. Người vận hành đăng ký tài khoản mong muốn bằng ứng dụng, kiểm tra ID/email và xác nhận rõ trước khi dùng `tests/Initialize-FirstAdmin.ps1`. Chạy preview trước; Apply yêu cầu xác nhận đúng target và câu `GRANT ADMIN <ID> <email>`. Không đưa mật khẩu vào báo cáo/source. Sau cấp quyền, đăng xuất/đăng nhập lại và kiểm tra menu Admin. Tài khoản Admin trong database thử chỉ là fixture, không phải Admin của database demo.

## Tạo và xác minh backup — chỉ người vận hành chạy

Thay đường dẫn bằng file mới, không trùng file đã có; thư mục phải tồn tại và tài khoản dịch vụ SQL Server phải có quyền ghi. Lệnh dưới là mẫu, chưa thực thi trong phiên bàn giao:

```sql
BACKUP DATABASE [OnlineSalesDb]
TO DISK = N'D:\Handover\OnlineSalesDb_YYYYMMDD_HHMM_NEW.bak'
WITH COPY_ONLY, CHECKSUM, STATS=10;

RESTORE VERIFYONLY
FROM DISK = N'D:\Handover\OnlineSalesDb_YYYYMMDD_HHMM_NEW.bak'
WITH CHECKSUM;

RESTORE FILELISTONLY
FROM DISK = N'D:\Handover\OnlineSalesDb_YYYYMMDD_HHMM_NEW.bak';
```

Không dùng INIT/FORMAT để ghi đè backup. VERIFYONLY không thay thế thử khôi phục. Ghi vào biên bản: người thao tác, thời gian, instance, database, tên file, SHA256, kết quả VERIFYONLY và restore thử; chưa làm thì ghi CHƯA KIỂM THỬ.

## Khôi phục trên máy nhận

1. Cài SQL Server phiên bản bằng hoặc mới hơn bản tạo backup; xem phiên bản thực tế trong HANDOVER_20261021.md. Dùng Windows Authentication hoặc cấu hình người vận hành cung cấp riêng.
2. Trong SSMS chọn Restore Database → Device → file .bak; xem FILELISTONLY để biết đúng logical name và số file.
3. Restore thử vào tên **mới** `OnlineSalesDb_RestoreCheck_<ngaygio>`; chọn vị trí .mdf/.ldf mới cho từng file. Không chọn Overwrite existing database (WITH REPLACE), không đè `OnlineSalesDb` đang có dữ liệu.
4. Kiểm tra migration, số user/role/đơn/ledger và đối chiếu tồn. Chạy ứng dụng trỏ database restore thử, kiểm tra đăng nhập/đọc chứng từ trước khi quyết định dùng làm database demo.
5. Sửa DefaultConnection trong Web.config ở máy nhận: đúng instance và catalog vừa phục hồi. Không chạy seed/migration nếu schema đã khớp; không sửa tồn bằng SQL.
6. Trước thay dữ liệu demo hoặc tài khoản quan trọng, cần xác nhận chủ dự án. Không phân phối công khai backup có dữ liệu cá nhân hoặc thông tin tài khoản.

Mẫu kiểm tra checksum file trong PowerShell:

```powershell
Get-FileHash -Algorithm SHA256 -LiteralPath 'D:\Handover\OnlineSalesDb_YYYYMMDD_HHMM_NEW.bak'
```

## Biên bản còn chờ

| Mục | Trạng thái |
|---|---|
| Tài khoản Admin database demo | CHƯA CÓ; chờ người vận hành đăng ký/xác nhận cấp quyền |
| File .bak | CHƯA TẠO trong phiên bàn giao |
| VERIFYONLY / restore thử | CHƯA KIỂM THỬ |
| PDF đã ký / gói nộp đầy đủ | CHƯA CÓ |
