# Chẩn đoán và diễn tập AccountGovernance — 30/10/2026

## Nguyên nhân và kết nối đã xác minh

Đọc Web.config thực tế và truy vấn @@SERVERNAME/DB_NAME(): **DESKTOP-AUTD3A3\SQLEXPRESS / OnlineSalesDb**, Windows Authentication. Không suy đoán instance. ApplicationDbContext kế thừa IdentityDbContext<ApplicationUser>, dùng DefaultConnection; không đổi namespace/context key. AutomaticMigrationsEnabled=false.

MigrationHistory thật chỉ có:

- 202610131032209_InitialCreate
- 202610151148216_AddShiftManagementTables

ContextKey: OnlineSalesManagement.Migrations.Configuration. Model mới thêm bool ApplicationUser.IsAdministrativelySuspended và DbSet AccountAdministrationAudits; migration 202610292211500_AccountGovernance chưa được áp dụng vào DB thật. manager.FindByIdAsync là truy vấn EF đầu tiên khi xác minh cookie cũ, nên EF phát hiện model thay đổi tại đó. Rebuild chỉ biên dịch code, không cập nhật DB. Không sửa cookie validator, không SetInitializer(null), không bỏ model check, không xóa MigrationHistory.

Thiếu trong DB thật:

| Schema mới | Định nghĩa |
|---|---|
| AspNetUsers.IsAdministrativelySuspended | bit NOT NULL DEFAULT 0 |
| AccountAdministrationAudits.Id | int IDENTITY NOT NULL, primary key |
| TargetUserId, ActorUserId | nvarchar(128) NOT NULL |
| Action | nvarchar(20) NOT NULL |
| Reason | nvarchar(500) NOT NULL |
| OccurredUtc | datetime NOT NULL |

Đối chiếu sys.columns giữa DB thật và DB restore sau migration, loại hai bổ sung nêu trên và MigrationHistory: **0 khác biệt** về kiểu, max_length, precision, scale, nullable, identity. EF CompatibleWithModel(false) trên DB thử sau migration PASS, không vô hiệu initializer.

## Rủi ro và tính tương thích

Migration Up chỉ CREATE TABLE, ADD COLUMN và cập nhật snapshot __MigrationHistory do EF sinh. Không DROP/TRUNCATE/DELETE/UPDATE nghiệp vụ, không thay FK, không sửa mật khẩu/roles/security stamp/tồn kho. DEFAULT 0 giữ mọi user cũ không bị đình chỉ. Bảng audit bắt đầu rỗng. Không có FK từ audit để không cascade mất lịch sử khi tài khoản bị xử lý ở nghiệp vụ khác. Down chủ động ném NotSupportedException: không dùng downgrade tự động để xóa audit.

DDL vẫn có thể cần schema lock và dung lượng log; phải dừng site/giao dịch ghi trong cửa sổ cập nhật. SQL Preview thô **không idempotent và không bọc transaction sẵn**; không bấm chạy lặp, không chạy thêm EF Update-Database sau SQL. Bản ReviewedApply bổ sung transaction/XACT_ABORT và guard đúng server/database/history/schema; chỉ chuẩn bị, chưa chạy trên DB thật. Configuration.Seed gọi SampleDataSeeder (bỏ qua nếu có Products/Categories); diễn tập chọn đường SQL nên hoàn toàn không gọi Seed.

## Backup và restore đã làm

File từ metadata msdb và RESTORE HEADERONLY:

`C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\Backup\OnlineSalesDb.bak`

File có nhiều backup set. Set 1: 15/10/2026 18:46:32; **set 2: 22/10/2026 10:31:06**, full OnlineSalesDb. Không có checksum. Chọn FILE=2, không mặc định set 1. VERIFYONLY PASS; restore thực tế thành công. Backup này là ảnh ngày 22/10, **không phải dữ liệu mới nhất của DB thật ngày 30/10**.

DB thử đã restore: **Dacn05_Restore_20261030_063318** trên đúng instance đã xác minh. Hai logical files OnlineSalesDb / OnlineSalesDb_log được MOVE sang hai physical files mang tên DB thử trong default data/log directory của chính instance. Không WITH REPLACE, không ghi đè database/file thật. Script từ chối target tồn tại và chỉ nhận tên Dacn05_Restore_*.

Để làm lại với DB thử mới (không dùng lại tên đã có):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Test-RestoredAccountMigration.ps1 -TestDatabase Dacn05_Restore_20261030_100000
```

Script kiểm backup/header → VERIFYONLY → FILELISTONLY → RESTORE WITH MOVE/FILE đúng set → snapshot dữ liệu → xác nhận đúng baseline hai migration → áp dụng **SQL AccountGovernance đúng một lần trong transaction** → đối chiếu snapshot. Không chạy EF Update-Database, không Seed. Script lấy server từ Web.config và backup từ metadata server; không chỉnh Web.config gốc. Nếu gặp lỗi/partial schema/history lạ, dừng, không tự sửa MigrationHistory hoặc reset DB.

Nếu dùng SSMS thủ công: connect đúng instance → Databases → Restore Database → Device chọn .bak → chọn full set ngày 22/10 (Position 2) → Destination đặt tên DB thử mới → Files bật Relocate và sửa mọi tên file sang tên DB thử → Options KHÔNG bật Overwrite existing database → xem Script trước Execute. Chỉ restore, rồi chọn **một** đường SQL migration trên DB thử; không chạy script tự động ở trên sau khi đã restore thủ công vì nó từ chối DB tồn tại.

## Bảo toàn dữ liệu và thử website

Đã so sánh 21 bảng dữ liệu cũ bằng COUNT + SHA256 nội dung XML sắp theo primary key, giữ nguyên tất cả cột cũ kể cả Identity/password hash/security stamp/rowversion; không xuất dữ liệu tài khoản ra báo cáo. Snapshot trước/sau migration trùng nhau, trước khi tạo fixture. Bảng __MigrationHistory được kiểm riêng; bảng audit mới rỗng, số user suspended=0, migration mới đúng 1 dòng.

Backup có 2 user, 9 sản phẩm, 4 danh mục, 6 khách hàng, 3 nhân viên, 3 nhà cung cấp, 8 phiếu nhập/13 dòng, 13 đơn bán/18 dòng, 27 InventoryTransactions. Không dùng các con số này để mô tả DB thật hiện tại.

Sau snapshot, chỉ trên DB thử: tạo tài khoản fixture mới cho Admin/Kho/BanHang/KhachHang, mật khẩu ngẫu nhiên trong biến môi trường và xóa sau test; không thay mật khẩu/role tài khoản đã restore. Tạo sản phẩm/hồ sơ/chứng từ fixture mới để kiểm Service; không gán QuantityInStock trực tiếp. Giữ lại DB và fixtures để chẩn đoán, không drop/reset.

Chạy kiểm thử trên DB đã restore/migrate:

```powershell
msbuild OnlineSalesManagement.sln /t:Rebuild /p:Configuration=Debug /p:MvcBuildViews=true
powershell -NoProfile -ExecutionPolicy Bypass -File tests/Run-RestoredDatabaseTests.ps1 -TestDatabase Dacn05_Restore_20261030_063318 -Port 64345
```

Harness không chạy migration, chỉ dùng DB có evidence restore/migration thành công; mỗi lượt thêm fixture mã riêng. Site copy riêng tại artifacts/restore-verification/Dacn05_Restore_20261030_063318/site, chỉ Web.config **bản copy** trỏ DB thử. IIS Express test dừng khi harness kết thúc. Muốn mở bằng trình duyệt, chạy IIS Express với /path trỏ thư mục site copy này, /port:64345 rồi truy cập http://localhost:64345; dùng tài khoản gốc của backup mà bạn biết mật khẩu. Chưa kiểm mật khẩu thực của Admin cũ; HTTP test dùng Admin fixture mới. Không trỏ site source gốc sang DB thử hoặc thật một cách âm thầm.

## Chuẩn bị cập nhật OnlineSalesDb thật — CHƯA thực hiện

Chỉ tiến hành sau khi người dùng duyệt rõ ràng:

1. Lưu bản code ổn định trước nâng cấp; xác nhận đúng source mới và SQL Preview. Dừng IIS Express/site và các tác vụ ghi. Trong SSMS xác nhận `SELECT @@SERVERNAME, DB_NAME()` là DESKTOP-AUTD3A3\SQLEXPRESS / OnlineSalesDb.
2. Tạo **backup mới riêng tên có timestamp** của OnlineSalesDb hiện tại bằng COPY_ONLY, CHECKSUM. Không ghi đè OnlineSalesDb.bak ngày 22/10. VERIFYONLY WITH CHECKSUM, restore thử backup mới sang tên DB riêng và kiểm dữ liệu. Bản 22/10 đã thử không thay cho bước này.
3. Kiểm History thật vẫn đúng baseline hai migration; cột/bảng mới chưa có. Nếu đã có một phần, history khác hoặc dữ liệu/schema khác, dừng để rà lại, không tự xóa/sửa history.
4. Mở `docs/migrations/AccountGovernance.ReviewedApply.sql` trong SSMS, chọn đúng OnlineSalesDb. Đọc guard và transaction. Thực thi **một lần** khi được duyệt. Không chạy AccountGovernance.sql thô nữa và không chạy EF Update-Database. Nếu lỗi, transaction rollback; giữ thông báo lỗi để phân tích.
5. Kiểm History có đúng một AccountGovernance; cột bit NOT NULL/default0; bảng audit rỗng ngay sau migration; counts/hash dữ liệu cũ giữ nguyên. Không viết fixture test vào DB thật.
6. Khởi động site mới, đăng nhập Admin của bạn, kiểm danh sách/quyền, trang nghiệp vụ và báo cáo bằng thao tác đọc. Cookie cũ cũng phải qua kiểm model bình thường. Nếu có lỗi, dừng site; không chạy Down (bị chặn). Ưu tiên quay lại binary trước nếu tương thích; phục hồi backup chỉ theo kế hoạch đã duyệt, tránh mất giao dịch sau backup.

Không chạy bản source nâng cấp với OnlineSalesDb chưa migration. Muốn tiếp tục dùng DB thật ngay lúc này, chạy bản source ổn định trước nâng cấp bằng checkout/thư mục riêng; không reset hoặc bỏ các thay đổi hiện tại.

File phiên này: tools/Test-RestoredAccountMigration.ps1; tests/RestoredDatabaseTests.cs; tests/Run-RestoredDatabaseTests.ps1; docs/migrations/AccountGovernance.ReviewedApply.sql; báo cáo này. Không thay source ứng dụng, Migration gốc, Web.config gốc; không Commit/Push.

## Kết quả thực tế của phiên này

Thời gian 30/10/2026 khoảng 06:28–06:45 (+07). Branch feature/management-upgrades-20261030; base HEAD 15b13a8649817781a66b790d4ab5bd5e5218a4a5, working tree chưa commit. Kết quả dưới đây được chạy mới trong phiên chẩn đoán, không lấy tổng 298/241 của phiên trước để chứng nhận DB restore.

| Kiểm tra | Kết quả |
|---|---|
| Server/DB/history/schema thật, chỉ đọc | PASS, kết nối như trên, vẫn chỉ hai migration cũ |
| Backup set 2 / VERIFYONLY / restore DB riêng | PASS |
| SQL migration trên DB restore | PASS, áp dụng đúng 1 lần, không EF Update-Database |
| 21 bảng dữ liệu cũ trước/sau migration | PASS, hash và count trùng nhau |
| EF CompatibleWithModel(false) sau SQL migration | PASS |
| Rebuild Debug + Razor | PASS exit 0; có warning CodeDom/MSB3061 không xóa được một số file Roslyn trong sandbox |
| Smoke | PASS 63 |
| Nghiệp vụ trên DB restore | PASS 24 |
| HTTP/IIS Express trên DB restore | PASS 51 |
| DBCC CHECKDB DB restore, không REPAIR | PASS |
| Kiểm parser hai script PowerShell mới / diff whitespace mới | PASS |
| Đăng nhập bằng mật khẩu Admin cũ của người dùng | NOT TESTED, không biết/không đổi mật khẩu; đã kiểm Admin fixture |
| Chrome UI thủ công/print thực tế và SQL ReviewedApply trên DB thật | NOT TESTED |

Nghiệp vụ PASS: create/read/update fixture Product/Customer/Employee/Supplier; xóa category fixture không tham chiếu; phiếu Draft/Confirm/replay; đơn Pending/Confirm/Complete/Cancel; thiếu hàng rollback; tồn của toàn bộ sản phẩm cũ và mới khớp ledger. HTTP PASS: Admin/Kho/BanHang/KhachHang đăng nhập, cookie truy cập request tiếp theo không lỗi model, role matrix 9 controller, logout mất quyền; Admin mở Revenue Index, Purchases, Sales, InventoryReports, Store và bộ lọc AdminUsers. Đây là kiểm server HTTP, không chứng nhận giao diện trình duyệt hoặc toàn bộ nhánh CRUD Delete của từng hồ sơ.

Các lượt FAIL ban đầu là lỗi harness đã sửa: PowerShell SqlConnectionStringBuilder indexer/parser; compiler thiếu tham chiếu do dùng ApplicationUserManager (đổi harness sang UserManager Identity chuẩn); fixture thiếu SĐT bắt buộc; helper gửi POST thay GET khi chuỗi null thành rỗng; biến $HOME read-only đổi thành $dashboardPage. Không sửa source ứng dụng để bỏ validation. Các lượt lỗi có thể đã thêm một số tài khoản/chứng từ fixture vào DB thử; giữ lại, không xóa/reset. Migration chỉ chạy ở bước restore đầu tiên, không chạy lại trong các lượt sửa harness. Lượt cuối exit 0, 24 + 51 PASS; không dùng lượt lỗi để chứng nhận.

Evidence: artifacts/management-upgrades/restore-account-migration.log, restore-build.log, restore-smoke.log, restore-services-http.log; snapshot/count/hash và site copy trong artifacts/restore-verification/Dacn05_Restore_20261030_063318. Artifacts không commit. SHA256 Web.config gốc cuối phiên vẫn 8C2441F0BC7C0A617CEFB4F65744180A1E44E09E36C7E8CFEE63963BB854A9C2, trùng trước phiên.

Để mở site thử đã chuẩn bị (lệnh chạy thủ công từ PowerShell, không chỉnh Web.config gốc):

```powershell
& "$env:ProgramFiles\IIS Express\iisexpress.exe" /path:"C:\Users\TonLeo\source\repos\OnlineSalesManagement\artifacts\restore-verification\Dacn05_Restore_20261030_063318\site" /port:64345
```

Vào http://localhost:64345 bằng cửa sổ riêng tư, đăng nhập Admin bằng mật khẩu tại thời điểm backup 22/10 mà bạn biết. Không dùng tài khoản fixture vì mật khẩu ngẫu nhiên không được giữ/ghi log. Dừng IIS Express này sau kiểm. Để chạy site mới trên DB thật, cần xác nhận triển khai theo 6 bước bên trên; hiện **chưa Apply**.
