# OnlineSalesDb — sẵn sàng chờ xác nhận cuối, chưa Apply

Lượt kiểm tra mới: 30/10/2026 06:56:01–06:56:15 (+07). Kết nối lấy từ Web.config và xác minh SERVERPROPERTY/DB_NAME(), không suy đoán instance. Không đổi source ứng dụng/Web.config, không Commit/Push.

## Database và backup vừa tạo

- Server thực: `DESKTOP-AUTD3A3\SQLEXPRESS`.
- DB thật: `OnlineSalesDb`, ONLINE / MULTI_USER / SIMPLE / không read-only.
- DB thử mới: `Dacn05_Preflight_20261030_065601_a5ed34e0`.
- Backup mới, Full COPY_ONLY, CHECKSUM, STOP_ON_ERROR, file mới GUID, không INIT/FORMAT/ghi đè bản cũ:

```text
C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\Backup\OnlineSalesDb_PreAccountGovernance_20261030_065601_a5ed34e0.bak
```

Backup finish theo SQL Server: 30/10/2026 06:56:05. Header xác nhận BackupType=1, Position=1, DatabaseName=OnlineSalesDb, HasBackupChecksums=true, IsCopyOnly=true, IsDamaged=false.

Đã thực thi thành công:

```sql
RESTORE VERIFYONLY FROM DISK=N'C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\Backup\OnlineSalesDb_PreAccountGovernance_20261030_065601_a5ed34e0.bak'
WITH FILE=1, CHECKSUM, STOP_ON_ERROR;
```

Restore chính backup này với FILE=1, CHECKSUM, STOP_ON_ERROR, RECOVERY, MOVE từng logical file sang file mới mang tên DB thử trong data/log directory của instance; **không WITH REPLACE**, không đè DB/file thật. DB thử giữ nguyên schema cũ và dữ liệu hiện tại; **không migration hoặc thêm fixture** trong DB thử này.

## Kết quả đối chiếu mới

| Kiểm tra | Kết quả |
|---|---|
| Full backup mới / header có CHECKSUM, COPY_ONLY | PASS |
| VERIFYONLY WITH CHECKSUM | PASS |
| Restore đúng file và backup set 1 sang DB riêng | PASS |
| 22 bảng: COUNT + SHA256 tất cả cột, theo PK | PASS, khớp live trước backup / DB restore / live sau kiểm tra |
| Schema/default/computed, index/PK/unique, FK, CHECK, trigger, identity counters | PASS, hash metadata trùng live và restore |
| FK/CHECK bị disabled hoặc untrusted | Không có |
| DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS trên DB restore | PASS, không có dòng vi phạm |
| DBCC CHECKDB trên DB restore | PASS, không REPAIR |
| QuantityInStock so với tổng InventoryTransactions.QuantityChange | PASS, 0 sản phẩm lệch |
| Migration SQL Preview trong ReviewedApply | PASS, nội dung giữ nguyên |
| SQL Model bytes so với Target trong EF migration .resx | PASS, byte-for-byte |
| Cập nhật schema DB thật | NOT RUN, đang chờ xác nhận |

Dữ liệu đã đối chiếu: 2 user, 2 role, 2 role membership; 9 sản phẩm/4 danh mục; 6 khách hàng, 3 nhân viên, 3 nhà cung cấp; 8 phiếu nhập/13 dòng; 13 đơn bán/18 dòng; 27 inventory transactions; bảng ca/kết sổ chưa có bản ghi. Toàn bộ 22 bảng gồm cả __MigrationHistory được kiểm, không chỉ các bảng có dữ liệu. Không ghi mật khẩu/token/PII vào evidence: chỉ tên bảng, count, hash. Không phát hiện thay đổi dữ liệu/schema trong khoảng chạy; đây không phải cơ chế ngăn người khác ghi DB sau thời điểm kiểm tra.

Evidence: `artifacts/account-preflight/20261030_065601_a5ed34e0/result.json`, production-before.json, restored.json, production-after.json, schema-before.json, schema-restored.json; log `artifacts/management-upgrades/fresh-backup-preflight.log`. Artifacts/backup không đưa vào Git.

## Trạng thái migration và thay đổi dự kiến

DB thật và DB thử mới đều chỉ có:

1. `202610131032209_InitialCreate` — EF 6.4.4.
2. `202610151148216_AddShiftManagementTables` — EF 6.5.1.

Cùng ContextKey `OnlineSalesManagement.Migrations.Configuration`. Chưa có cột đình chỉ hoặc bảng audit, không có schema dở dang. Không xóa/sửa các bản ghi History hiện tại.

AccountGovernance sẽ:

- Thêm `dbo.AspNetUsers.IsAdministrativelySuspended bit NOT NULL DEFAULT 0`. Mọi tài khoản cũ mặc định không đình chỉ.
- Tạo `dbo.AccountAdministrationAudits`: Id int IDENTITY PK; TargetUserId/ActorUserId nvarchar(128), Action nvarchar(20), Reason nvarchar(500), OccurredUtc datetime; tất cả NOT NULL. Không FK cascade, không cập nhật role/mật khẩu/tồn kho.
- Thêm đúng một dòng `202610292211500_AccountGovernance` vào __MigrationHistory với snapshot model từ migration EF6 và ProductVersion 6.5.1.

ReviewedApply đã có XACT_ABORT, BEGIN TRAN/TRY-CATCH/ROLLBACK và guard server/database, count/baseline history, cột/bảng đã tồn tại. Sau lần thành công history có 3 dòng, lần chạy lại bị guard từ chối trước DDL. Không thêm cơ chế tự bỏ qua lỗi hoặc tự sửa history. Script chưa được thực thi trong lượt preflight này; diễn tập migration ở lượt trước nằm trong báo cáo ACCOUNT_GOVERNANCE_RESTORE_20261030.md, không đánh đồng với DB thử mới nguyên trạng.

File được kiểm: `docs/migrations/AccountGovernance.ReviewedApply.sql`.

SHA256: `F1C448BB78AF3F6B7E5FE695EDF3A68F58EDDE146EB363B67793F6965E159907`.

## Thực thi sau khi được xác nhận cuối — CHƯA chạy

Trước khi chạy: dừng site/IIS Express và mọi tác vụ ghi theo cửa sổ triển khai được duyệt; kiểm lại DB/history, SHA256 script và count/hash so với evidence 06:56. Nếu DB đã thay đổi thì backup/verify/restore và đối chiếu lại; không dùng bản 06:56 để bỏ qua giao dịch mới. Script DDL có thể giữ schema lock; nếu timeout không tự bấm chạy lại.

Từ PowerShell, lệnh chính xác (sqlcmd đã xác minh có trên máy; Windows Authentication):

```powershell
sqlcmd -S 'DESKTOP-AUTD3A3\SQLEXPRESS' -d 'OnlineSalesDb' -E -b -r 1 -f 65001 -i 'C:\Users\TonLeo\source\repos\OnlineSalesManagement\docs\migrations\AccountGovernance.ReviewedApply.sql'
if ($LASTEXITCODE -ne 0) { throw 'Migration failed: stop, inspect transaction/schema/history; do not retry blindly.' }
```

Chỉ chạy ReviewedApply **một lần**. Không chạy SQL Preview thô hoặc EF Update-Database tiếp theo, không chạy Seed. Sau thành công kiểm đúng 1 AccountGovernance, cột có default0, audit rỗng ngay sau migration, hash/count toàn bộ cột cũ/roles/credentials/nghiệp vụ không đổi, tồn khớp ledger; rồi mở site và kiểm Admin/quyền/báo cáo. Không tạo giao dịch fixture trên DB thật để test.

## Nếu thất bại — kế hoạch bảo toàn dữ liệu

1. Giữ site dừng, ghi lại lỗi SQLCMD và thời điểm; không xóa History, không chạy Down, không sửa tay cột/bảng, không reset hoặc tự chạy lại. Script rollback toàn transaction khi lỗi được catch. Nếu mất kết nối/timeout, xác minh session và transaction đã kết thúc bằng kết nối quản trị trước khi kết luận.
2. Đọc lại schema và History. Nếu cả hai vẫn baseline cũ và dữ liệu khớp, DB chưa được nâng cấp; có thể quay về source ổn định trước nâng cấp sau kiểm tra. Nếu schema đã đủ và History mới có đúng 1 dòng, coi migration đã commit dù client mất kết nối; không chạy lại. Nếu trạng thái dở dang/bất thường, dừng và điều tra.
3. Khi cần phục hồi từ backup, **ưu tiên restore sang DB recovery mới**, không overwrite OnlineSalesDb, không xóa bất kỳ DB/file cũ. Lệnh mẫu chính xác với tên recovery mới chưa tồn tại, thực thi riêng chỉ khi phương án phục hồi được duyệt:

```sql
USE master;
IF DB_ID(N'Dacn05_Recovery_20261030_065601_a5ed34e0') IS NOT NULL
    THROW 51010, 'Recovery database already exists; stop.', 1;
RESTORE VERIFYONLY
 FROM DISK=N'C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\Backup\OnlineSalesDb_PreAccountGovernance_20261030_065601_a5ed34e0.bak'
 WITH FILE=1,CHECKSUM,STOP_ON_ERROR;
RESTORE DATABASE [Dacn05_Recovery_20261030_065601_a5ed34e0]
 FROM DISK=N'C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\Backup\OnlineSalesDb_PreAccountGovernance_20261030_065601_a5ed34e0.bak'
 WITH FILE=1,CHECKSUM,STOP_ON_ERROR,RECOVERY,
 MOVE N'OnlineSalesDb' TO N'C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\DATA\Dacn05_Recovery_20261030_065601_a5ed34e0.mdf',
 MOVE N'OnlineSalesDb_log' TO N'C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\DATA\Dacn05_Recovery_20261030_065601_a5ed34e0_log.ldf';
DBCC CHECKDB([Dacn05_Recovery_20261030_065601_a5ed34e0]) WITH NO_INFOMSGS;
```

Logical files và instance data directory lấy từ chính FILELISTONLY/metadata, không suy đoán. Trước restore kiểm các physical paths recovery không tồn tại; nếu có, chọn tên mới, không xóa/ghi đè. Đối chiếu recovery với manifest 06:56, History cũ và tồn kho. Backup phục hồi model cũ nên cần source trước nâng cấp; chạy source mới ngay trên recovery schema cũ sẽ lại gặp model mismatch.

4. Chỉ chuyển dịch vụ sang recovery/source phù hợp sau xác nhận, lưu OnlineSalesDb hiện tại để điều tra. Không âm thầm đổi Web.config. Nếu có giao dịch sau thời điểm backup, phục hồi về 06:56 sẽ thiếu chúng: phải giữ thêm backup hiện trạng và lên phương án bảo toàn/đối chiếu giao dịch trước khi chuyển. Không restore đè OnlineSalesDb thật khi chưa có kế hoạch và xác nhận riêng; hướng phục hồi mới không phá dữ liệu cũ.

**Kết luận:** các kiểm tra backup/restore/data/schema/script hiện tại PASS; sẵn sàng xin xác nhận cuối. Chưa chạy SQL cập nhật DB thật, chưa chạy Update-Database, không xóa DB hoặc dữ liệu cũ.
