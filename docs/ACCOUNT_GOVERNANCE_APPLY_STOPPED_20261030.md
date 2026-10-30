# Apply đã dừng trước migration — 30/10/2026 07:07 (+07)

Người dùng đã cho phép Apply một lần, kèm điều kiện dừng khi lỗi. Đã dừng, không tự thử lại.

## Kết quả

| Bước | Kết quả |
|---|---|
| Xác minh DESKTOP-AUTD3A3\SQLEXPRESS / OnlineSalesDb | PASS |
| Không có IIS Express hoặc session/transaction khác trên DB khi bắt đầu | PASS |
| Dữ liệu/schema/identity counters so với backup 06:56 | PASS, không thay đổi |
| VERIFYONLY WITH CHECKSUM lại backup đã duyệt | PASS |
| Chưa có schema/History AccountGovernance | PASS |
| Mở transaction bảo vệ trước gọi ReviewedApply | FAIL: MARS batch-scoped transaction |
| Số lần gọi ReviewedApply | **0** |
| Migration commit | **Chưa có** |
| Đối chiếu đọc sau lỗi | PASS, 22 bảng và metadata trùng backup |
| Build/Razor và kiểm EF theo schema mới | NOT RUN, dừng sau lỗi theo yêu cầu |
| Đăng nhập Admin trên source mới sau migration | NOT TESTED, chưa migration |

## Nguyên nhân

tools/Apply-ApprovedAccountGovernance.ps1 nhập cấu hình kết nối có MultipleActiveResultSets=True từ Web.config. Công cụ bổ sung transaction bảo vệ qua nhiều SqlCommand để giữ khóa bảng khi kiểm dữ liệu. `BEGIN TRANSACTION` ở batch riêng trên kết nối MARS bị SQL Server từ chối khi batch kết thúc: “A transaction that was started in a MARS batch is still active at the end of the batch. The transaction is rolled back.”

Đây là lỗi trong công cụ thực thi mới do assistant chuẩn bị, không phải lỗi của ReviewedApply hoặc EF migration. Lỗi xảy ra trước dòng gọi script; ApplyInvocations=0, Committed=false, trạng thái FAIL_STOPPED. ReviewedApply và Web.config không sửa. Không gọi Update-Database, Seed, reset, restore đè, xóa History hoặc dữ liệu.

## Trạng thái DB sau khi dừng, đã kiểm chỉ đọc

- 22 bảng COUNT/SHA256 và schema/default/index/FK/CHECK/trigger/identity vẫn trùng baseline đã backup.
- MigrationHistory vẫn 2 dòng: InitialCreate và AddShiftManagementTables.
- Cột IsAdministrativelySuspended chưa có; bảng AccountAdministrationAudits chưa có.
- Không còn user transaction khác trên OnlineSalesDb; connection kiểm tra có @@TRANCOUNT=0.
- User/password hash/security stamp/role, khách hàng, đơn, phiếu nhập và InventoryTransactions giữ nguyên.
- Web.config SHA256 vẫn 8C2441F0BC7C0A617CEFB4F65744180A1E44E09E36C7E8CFEE63963BB854A9C2.

Backup còn nguyên: C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\Backup\OnlineSalesDb_PreAccountGovernance_20261030_065601_a5ed34e0.bak. Không có dấu hiệu cần phục hồi DB trong sự cố này.

Evidence: artifacts/account-apply/20261030_070704/result.json, after-stop-readonly.json; artifacts/management-upgrades/production-account-apply.log. SQL được duyệt vẫn SHA256 F1C448BB78AF3F6B7E5FE695EDF3A68F58EDDE146EB363B67793F6965E159907.

## Bước tiếp theo, chưa thực hiện

Không chạy lại công cụ hiện tại. Cần sửa cách kết nối/transaction của công cụ (tắt MARS riêng trên connection triển khai, không sửa Web.config; hoặc dùng một batch/sqlcmd phù hợp), thử trên DB riêng trước, rồi xác minh quiet/backup/schema và xin xác nhận chạy lại theo điều kiện dừng của người dùng. Không cần thay nội dung ReviewedApply.

IIS Express vẫn đang dừng. Chạy source nâng cấp với schema thật hiện tại sẽ tiếp tục gặp model mismatch, nên chưa mở lại source này để đăng nhập Admin. Có thể dùng source ổn định trước nâng cấp trong thư mục riêng nếu cần vận hành DB schema cũ. Không reset working tree để quay về bản cũ.

Không Commit/Push. Không tự phục hồi hoặc thử Apply lần nữa trong phiên này.
