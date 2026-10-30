# PASS — AccountGovernance đã nâng cấp OnlineSalesDb thật

## Định danh và kết quả thực thi

- Server đã xác minh: **DESKTOP-AUTD3A3\SQLEXPRESS**.
- Database thật: **OnlineSalesDb**.
- Thời điểm chạy: **30/10/2026 08:36:39–08:36:46 (+07)**.
- Công cụ: `powershell -NoProfile -ExecutionPolicy Bypass -File tools/Apply-ApprovedAccountGovernance.ps1 -Approved -Production`.
- **ApplyInvocations=1, Committed=true, Status=PASS_COMMITTED**. Không chạy lại ReviewedApply.
- ReviewedApply SHA256 giữ nguyên: `F1C448BB78AF3F6B7E5FE695EDF3A68F58EDDE146EB363B67793F6965E159907`.
- Công cụ thực tế SHA256: `8960632A332852B8CE3D9A818E70B2AA17F942344CB375FD32F577CEAFF3583C`.
- Source: branch feature/management-upgrades-20261030, base HEAD 15b13a8649817781a66b790d4ab5bd5e5218a4a5, working tree chưa commit. Không Commit/Push.

## Kiểm tra trước triển khai và điều chỉnh guard

Không có IIS Express. Không có user session/transaction khác trên OnlineSalesDb trước thực thi. Session 47 là tác vụ hệ thống Service Broker `BRKR TASK`, is_user_process=0, không có process ứng dụng. Guard cũ tính cả session hệ thống; đã sửa **chỉ điều kiện s.is_user_process=1** ở kiểm tra session. Vẫn chặn cả user session đang ngủ và user transaction. Không kill session, không tắt Service Broker hoặc sửa Web.config/ReviewedApply.

Trước khi chạy production, công cụ sau sửa được kiểm lại từ backup trên cặp DB restore mới Dacn05_Restore_20261030_083553_01 / _02: **22 PASS**. Có test giữ kết nối người dùng để chứng minh vẫn bị chặn trước migration, fault injection rollback, Apply lần đầu và repeat guard. Chỉ sau khi test PASS mới gọi công cụ production một lần. Không có lỗi trong lượt production này.

Dữ liệu, schema và identity counters vẫn trùng bản preflight 06:56 nên không cần tạo thêm backup. Backup mới nhất theo msdb là Full COPY_ONLY/CHECKSUM lúc 06:56:05, đã VERIFYONLY WITH CHECKSUM lại ngay trước Apply; restore/CHECKDB đã kiểm thành công:

```text
C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\Backup\OnlineSalesDb_PreAccountGovernance_20261030_065601_a5ed34e0.bak
```

MARS=False chỉ trên SqlConnection triển khai. ReviewedApply chạy một batch, tự quản lý duy nhất transaction, không outer transaction và không EF migrator.

## Thay đổi schema thực tế

1. Thêm `dbo.AspNetUsers.IsAdministrativelySuspended bit NOT NULL DEFAULT 0`; mọi tài khoản hiện có vẫn false.
2. Tạo `dbo.AccountAdministrationAudits`: Id int IDENTITY PK; TargetUserId/ActorUserId nvarchar(128); Action nvarchar(20); Reason nvarchar(500); OccurredUtc datetime; tất cả NOT NULL. Bảng mới rỗng ngay sau nâng cấp.
3. Thêm **đúng một** History `202610292211500_AccountGovernance`, ContextKey OnlineSalesManagement.Migrations.Configuration, ProductVersion 6.5.1. Snapshot model trong DB khớp byte-for-byte Target EF resource.

Hai migration cũ giữ nguyên. Không sửa dữ liệu nghiệp vụ, tài khoản/role/password/security stamp cũ, tồn kho, FK hoặc namespace. Không chạy Update-Database, Seed, Automatic Migration, Drop-Database, Down, restore đè hoặc xóa MigrationHistory.

## Kiểm thử mới sau Apply

| Kiểm tra | Kết quả |
|---|---|
| Thực thi ReviewedApply trên DB thật | PASS, 1 lần, commit |
| SQL chỉ đọc: server/schema/history/model/default/PK/constraints/ledger | **11 PASS** |
| Đối chiếu hash/count toàn bộ cột cũ của 22 bảng | PASS, trước/sau giống nhau |
| User/password hash/SecurityStamp/role membership | PASS, byte/hash dữ liệu cũ giữ nguyên |
| EF6/Identity chỉ đọc trên DB thật | **9 PASS** |
| EF CompatibleWithModel(false) | PASS, không tắt initializer/model check |
| FindByIdAsync như cookie validator | PASS, không còn model mismatch |
| Rebuild Debug + Razor (MvcBuildViews=true) | PASS, exit 0 |
| Smoke sau build | **63 PASS** |
| Đăng nhập Admin thật bằng mật khẩu người dùng / Chrome UI | NOT TESTED, cần người dùng thao tác |

Số dữ liệu cũ sau Apply: 2 user, 2 role, 2 role membership; 9 sản phẩm, 4 danh mục; 6 khách hàng, 3 nhân viên, 3 nhà cung cấp; 8 phiếu nhập/13 dòng; 13 đơn bán/18 dòng; 27 InventoryTransactions. Tồn của mọi sản phẩm khớp tổng QuantityChange. Không lưu mật khẩu/token/PII vào báo cáo, chỉ count/hash. Kiểm EF không thử mật khẩu, không sign-in, không SaveChanges/ghi dữ liệu thật; các fixture ghi dữ liệu chỉ dùng DB thử.

## Evidence và file trong lượt này

- `artifacts/account-apply/20261030_083639_057/result.json`, before.json, after-old-columns.json, post-verify-old-data.json.
- `artifacts/management-upgrades/production-account-final-apply.log`, production-post-sql.log, production-post-ef.log, production-post-build.log, production-post-smoke.log.
- Guard regression: artifacts/deployment-tool-tests/20261030_083553 và production-final-tool-regression.log.
- File sửa: tools/Apply-ApprovedAccountGovernance.ps1 (lọc session hệ thống); tests/Test-AccountDeploymentTool.ps1 (thử user session đang ngủ).
- File thêm: tools/Verify-AccountGovernanceProduction.ps1; tests/ProductionAccountReadOnlyTests.cs; tests/Run-ProductionAccountReadOnlyTests.ps1; báo cáo này.

Không sửa ứng dụng/Web.config/SQL đã duyệt. Web.config SHA256 vẫn `8C2441F0BC7C0A617CEFB4F65744180A1E44E09E36C7E8CFEE63963BB854A9C2`. Artifacts, backup, bin/obj không đưa vào Git.

## Mở lại IIS Express và kiểm tra bằng Admin cũ

1. Mở OnlineSalesManagement.sln trong Visual Studio 2022. Đặt project OnlineSalesManagement làm Startup Project, chọn IIS Express trên thanh chạy. Không chạy migration hoặc seed nữa.
2. Chạy Ctrl+F5 hoặc F5. Web.config gốc đã giữ đúng DefaultConnection tới DESKTOP-AUTD3A3\SQLEXPRESS / OnlineSalesDb. Không sửa connection sang DB test/restore.
3. Vào /Account/Login, dùng **email và mật khẩu Admin cũ**. Không cần reset mật khẩu. Nếu đã có cookie hợp lệ, trang có thể chuyển về Home; cookie validator nay đọc user với schema mới thành công.
4. Mở /AdminUsers: kiểm card, quyền và trạng thái hoạt động; thử GET tìm email/vai trò/trạng thái, mở modal rồi Hủy để kiểm không ghi.
5. Mở Products, Customers, Employees, Suppliers, PurchaseReceipts, SalesOrders, Reports, InventoryReports và đối chiếu dữ liệu hiển thị. Các bộ lọc/lịch sử/giỏ hàng giữ luồng hiện có.
6. Nếu muốn demo đình chỉ/khôi phục, đổi role hoặc nhập Excel, ưu tiên DB thử và tài khoản fixture; không dùng Admin đang vận hành/đơn thật để thử thao tác phá trạng thái. Bảo vệ tự đình chỉ/Admin cuối vẫn giữ nguyên.

Không tự chạy lại công cụ Apply sau thành công. Nếu trình duyệt vẫn lỗi, ghi URL/lỗi và kiểm server/database thực tế của site đang mở; không xóa History hoặc thêm migration để che lỗi. Các harness giả định schema trước migration chỉ dùng cho diễn tập trước nâng cấp, không chạy lại trực tiếp với DB thật đã nâng cấp. Bộ read-only chốt số liệu trong phiên này sẽ không còn khớp nếu sau đó người dùng tạo dữ liệu mới hợp lệ; đây không tự động đồng nghĩa mất dữ liệu.

**Xác nhận cuối: OnlineSalesDb thật đã được nâng cấp AccountGovernance thành công. IIS Express vẫn dừng ở cuối phiên để người dùng chủ động mở lại.**
