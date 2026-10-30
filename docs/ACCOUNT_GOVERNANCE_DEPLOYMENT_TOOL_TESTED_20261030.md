# Công cụ triển khai đã sửa và kiểm thử — CHƯA Apply thật

30/10/2026, Asia/Ho_Chi_Minh. Kết quả cuối: tool tests 08:01:25–08:01:56, HTTP hoàn tất trước 08:26; đối chiếu DB thật/CHECKDB cuối 08:27. Branch feature/management-upgrades-20261030, base HEAD 15b13a8649817781a66b790d4ab5bd5e5218a4a5, working tree chưa commit. Không dùng kết quả cũ để chứng nhận lượt này.

## Nguyên nhân và sửa công cụ

Kết nối website có MultipleActiveResultSets=True. Công cụ cũ mở BEGIN TRAN ở một SqlCommand rồi giữ transaction qua các batch MARS, nên SQL rollback transaction ở cuối batch trước khi ReviewedApply được gọi.

tools/Apply-ApprovedAccountGovernance.ps1 hiện:

- Dùng SqlConnection triển khai riêng với **MultipleActiveResultSets=False**. Không sửa Web.config website.
- **Bỏ toàn bộ outer BEGIN/COMMIT và khóa bảng xuyên batch.** Không BeginTransaction() phía ADO.NET. ReviewedApply sở hữu duy nhất transaction; một SqlCommand chạy toàn script một batch. CommandTimeout/LOCK_TIMEOUT hữu hạn, SqlException dừng ngay; không tự retry.
- Không giả định có thể rollback sau commit. Nếu kiểm sau commit thấy sai khác, ghi Committed=true/FAIL_STOPPED, dừng để người dùng quyết định; không restore/reset. Catch chỉ cleanup transaction còn mở, không tạo transaction lồng nhau.
- Bắt buộc chọn đích rõ: `-Production` hoặc `-TestDatabase Dacn05_Restore_*`, cùng `-Approved`. Test mode từ chối OnlineSalesDb trước khi mở kết nối; false Production switch không ngầm chọn DB thật.
- Production dùng nguyên ReviewedApply đã duyệt, kiểm SHA256 cố định, server/DB thực tế, baseline History/schema, không có IIS/SQL sessions khác; đối chiếu dữ liệu/schema/identity counters với backup được kiểm chứng. Nếu thay đổi, gọi backup preflight để tạo Full CHECKSUM/VERIFY/restore mới trước khi tiếp tục; nếu lại thay đổi thì dừng.
- Test mode chỉ thay điều kiện DB_NAME trong bộ nhớ cho tên restore allowlist. Fault injection chỉ tồn tại ở parameter set Test, chèn THROW sau DDL và INSERT History, trước COMMIT. Không ghi sửa file SQL trên đĩa.

## Kiểm SQL đã duyệt

`docs/migrations/AccountGovernance.ReviewedApply.sql` **không đổi**, không có diff cần duyệt lại.

SHA256 vẫn `F1C448BB78AF3F6B7E5FE695EDF3A68F58EDDE146EB363B67793F6965E159907`.

Kiểm thực tế: **GO=0, BEGIN TRANSACTION=1, COMMIT TRANSACTION=1, ROLLBACK TRANSACTION=1**. Có XACT_ABORT, TRY/CATCH/THROW, guard server/database/history/partial-schema. Body và model metadata giữ nguyên Preview/EF resource. Guard SQL tự chống chạy lặp được thử thật trên DB đã nâng cấp; bị từ chối và không để transaction mở.

## DB và backup dùng kiểm thử

Backup trước migration, đã VERIFYONLY WITH CHECKSUM lại trong lượt này:

`C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\Backup\OnlineSalesDb_PreAccountGovernance_20261030_065601_a5ed34e0.bak`

Full COPY_ONLY/CHECKSUM, backup set 1, finish 06:56:05 ngày 30/10. Restore hai DB **mới**, MOVE file mới, không REPLACE:

- **Dacn05_Restore_20261030_080125_01**: lần Apply đầu thực sự, sau đó kiểm repeat và HTTP.
- **Dacn05_Restore_20261030_080125_02**: fault-injection rollback.

Cả hai được xác minh có đúng schema trước migration và hash/count trùng backup trước khi gọi công cụ. Không dùng DB đã nâng cấp để giả lập lần đầu. Các DB lỗi từ lượt thử sớm giữ lại, không drop/reset.

## PASS / FAIL / NOT TESTED

| Kiểm tra cuối | Kết quả |
|---|---|
| Công cụ trên hai DB restore mới | **21 PASS** |
| Hai guard chọn đích offline | **2 PASS**, không mở SQL connection |
| Apply lần đầu | PASS, commit đúng một AccountGovernance |
| Cột đình chỉ | PASS bit NOT NULL/default0; tài khoản cũ false |
| Audit table | PASS đủ 6 cột, độ dài/type/nullability/identity/PK đúng, ban đầu rỗng |
| History | PASS đúng một dòng mới; metadata model EF hợp lệ |
| Dữ liệu/quyền/credentials cũ | PASS hash/count toàn bộ cột cũ, gồm password hash/security stamp/roles và nghiệp vụ |
| Lần chạy công cụ thứ hai | PASS bị chặn trước khi thực thi SQL migration; dữ liệu/schema không đổi |
| Guard chạy lặp của SQL | PASS từ chối đã nâng cấp, transaction đóng |
| Fault injection giữa chừng | PASS nhận đúng THROW sau DDL/INSERT; rollback cột, bảng, History; hash data/schema trở về baseline |
| EF6 model compatibility trên DB mới nâng cấp | PASS, không tắt kiểm model |
| Rebuild Debug + Razor | PASS exit 0 |
| Smoke | **63 PASS** |
| Nghiệp vụ trên DB vừa nâng cấp | **26 PASS** |
| HTTP IIS Express trên DB thử | **59 PASS** |
| Tồn kho và ledger | PASS toàn bộ sản phẩm cũ/mới |
| CHECKDB + CHECKCONSTRAINTS | PASS hai DB thử; kiểm lại sau HTTP/fixtures cũng PASS |
| Production Application/SQL migration | **NOT RUN**, OnlineSalesDb chỉ đọc |
| Chrome UI thủ công, đăng nhập Admin cũ bằng mật khẩu thực | NOT TESTED; login dùng fixture riêng, không đổi mật khẩu Admin cũ |

HTTP mới bao gồm Admin/Kho/BanHang/KhachHang login/logout, role matrix 9 controllers, báo cáo, chưa xác nhận đình chỉ không ghi, đình chỉ đổi SecurityStamp, cookie cũ mất truy cập, bị đình chỉ không đăng nhập lại, khôi phục đăng nhập được, hai audit event chính xác. Tất cả dữ liệu ghi chỉ ở DB thử.

Các FAIL trung gian đã sửa và chạy lại trên restore mới: fault injection thiếu dấu `;` trước THROW (lỗi compile test, chưa chứng minh rollback), so sánh thừa sysdiagrams rỗng chỉ ở một phía (migration test đã commit, công cụ dừng đúng; dữ liệu cũ thực tế không sai). Phạm vi trước/sau nay thống nhất 22 bảng nghiệp vụ/Identity/History, không tính sysdiagrams rỗng. Lượt cuối không còn FAIL chưa xử lý. Lệnh chạy PS trực tiếp bị ExecutionPolicy chặn một lần; harness dùng `powershell -NoProfile -ExecutionPolicy Bypass` theo process, không đổi policy máy.

Evidence: artifacts/deployment-tool-tests/20261030_080125/result.json, source-hashes.json, production-final-readonly.json, first-apply.log, injected-failure.log, second-apply-blocked.log; các result riêng trong artifacts/account-apply. Logs tổng: artifacts/management-upgrades/deployment-tool-tests.log, deployment-tool-build.log, deployment-tool-smoke.log, deployment-target-guards.log, deployment-tool-services-http.log. Không commit artifacts/backup.

## OnlineSalesDb thật và backup cuối phiên

Đối chiếu chỉ đọc lúc 08:27: server DESKTOP-AUTD3A3\SQLEXPRESS / OnlineSalesDb; 22 bảng COUNT/SHA256 và schema/default/index/FK/CHECK/trigger/identity counters vẫn trùng backup 06:56; History chỉ hai migration cũ, chưa cột/bảng AccountGovernance. Latest Full trong msdb vẫn backup 06:56:05 có CHECKSUM/COPY_ONLY. Vì không thay đổi, chưa cần tạo thêm backup. Trước Apply thật, công cụ vẫn kiểm lại; thay đổi phát sinh sẽ buộc backup CHECKSUM/VERIFY/restore mới. Không lấy xác nhận lúc 08:27 làm chứng nhận vô thời hạn.

Web.config SHA256 giữ nguyên `8C2441F0BC7C0A617CEFB4F65744180A1E44E09E36C7E8CFEE63963BB854A9C2`.

## Lệnh cuối để triển khai — CHƯA chạy, cần xác nhận mới

Đóng IIS Express và mọi kết nối/tác vụ ghi vào OnlineSalesDb theo cửa sổ triển khai; công cụ sẽ dừng nếu còn session khác, không tự kill. Từ root workspace, **chỉ sau xác nhận Apply mới**:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Apply-ApprovedAccountGovernance.ps1 -Approved -Production
```

Lệnh này dùng đúng file ReviewedApply có hash trên, thực thi một lần, không Update-Database/Seed/Automatic Migration. Nếu lỗi thì dừng và xem result.json; không bấm chạy lại hoặc restore đè. Script SQL được duyệt tự rollback khi lỗi trước commit; nếu kết nối mất phải kiểm History/schema trước mọi quyết định. Không tự áp dụng Down hoặc xóa history.

Chạy lại test (mỗi lượt tạo cặp DB restore mới, có dữ liệu nhạy cảm của backup nên chỉ lưu local):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests\Test-AccountDeploymentTool.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tests\Test-DeploymentTargetGuards.ps1
```

File sửa/thêm trong lượt này: tools/Apply-ApprovedAccountGovernance.ps1; tests/Test-AccountDeploymentTool.ps1; tests/Test-DeploymentTargetGuards.ps1; tests/Run-RestoredDatabaseTests.ps1; báo cáo này. Không đổi SQL đã duyệt, Web.config, schema/model ứng dụng; không Commit/Push. **DỪNG CHỜ XÁC NHẬN APPLY THẬT.**
