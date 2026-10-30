# Nâng cấp quản trị/khách hàng — 30/10/2026

Nhánh feature/management-upgrades-20261030 từ bản ổn định 15b13a8. Chưa commit/push. Web.config local đã modified trước phiên, không sửa trong lần nâng cấp này.

## Giai đoạn 1 — báo cáo trước khi chuyển giai đoạn

Đã làm: GET lọc vai trò/trạng thái/email/tên đăng nhập/SĐT tài khoản đã xác minh; 12 card/trang, tối đa 4 cột; pager giữ query. Trạng thái đình chỉ riêng với lockout tạm. POST đình chỉ/khôi phục có xác nhận, CSRF, kiểm Admin hiện tại, chặn tự đình chỉ/Admin hoạt động cuối; thay SecurityStamp và audit trong cùng transaction Serializable. Cookie kiểm lại trạng thái mỗi request; password/external/2FA từ chối đình chỉ. SetRole giữ nguyên luồng, bổ sung bảo vệ Admin hoạt động cuối.

Email mới chuẩn hóa Trim/FormKC/lowercase, đối chiếu cả dữ liệu cũ trong transaction; public registration trả thông báo chung. SĐT xác minh qua Manage tách khỏi Contact Customer/Order: chuẩn hóa VN/E.164, chặn trùng SĐT đã xác minh của tài khoản khác, giới hạn thời gian/thử OTP, không xác nhận nếu không có challenge đã gửi. Không thu SĐT ở Register khi chưa có nhà cung cấp SMS thật. SmsService scaffold hiện bị chặn fail-closed, không giả vờ đã gửi mã. Không thêm lịch sử định danh hoặc tự cấm theo IP/fingerprint.

Migration 202610292211500_AccountGovernance thêm AspNetUsers.IsAdministrativelySuspended NOT NULL DEFAULT 0 và bảng AccountAdministrationAudits. Audit chỉ ID tài khoản/Admin, UTC, hành động, lý do; không ghi mật khẩu/OTP/SĐT/email. Lý do được giới hạn và chỉ Admin truy cập. Không sửa/xóa bản ghi nghiệp vụ, không đổi FK. Down bị chặn để tránh xóa audit tự động. SQL Preview: migrations/AccountGovernance.sql; chỉ scaffold/apply trên DB thử. **CHƯA Apply OnlineSalesDb. Không chạy bản code mới trên DB thật trước khi Migration được duyệt và áp dụng đúng quy trình.**

Backup check chỉ đọc: SQL Server 16.0.1000.6, OnlineSalesDb có backup ngày 22/10/2026 và 15/10/2026, không có checksum, chưa VERIFYONLY/restore trong phiên này. Người vận hành phải tạo COPY_ONLY mới + checksum, VERIFYONLY/restore thử trước khi duyệt Apply. Chưa đề xuất Apply khi backup chưa đủ điều kiện.

Kiểm thử mới GĐ1: Rebuild Debug + Razor PASS (warning CodeDom Access denied trong sandbox, compile exit 0); 62 smoke PASS; 259 integration PASS; 210 HTTP PASS trên Dacn05_Test_e811e2082f0b48588e2a69e4d6c988d8; 17 Admin JS và 5 state-dialog JS PASS. HTTP đã kiểm cookie cũ mất quyền sau suspend, không đăng nhập được, restore đăng nhập lại, audit hai hành động, không xác nhận/thiếu CSRF không ghi, tự đình chỉ bị chặn, SMS chưa cấu hình và mã giả không thể xác minh. Log artifacts/management-upgrades/phase1-*. SMS gửi/nhận thật, provider external và Chrome UI thật: NOT TESTED. FAIL fixture username dấu gạch và assertion userSearch cũ đã sửa rồi chạy lại; không dùng lượt FAIL để chứng nhận PASS.

File GĐ1: App_Start/IdentityConfig.cs, Startup.Auth.cs; Controllers/AccountController.cs, AdminUsersController.cs, ManageController.cs; Models/IdentityModels.cs, AccountAdministrationAudit.cs; Services/AccountGovernance.cs, IdentityProvisioning.cs; Views/AdminUsers/Index.cshtml, AccountAudit.cshtml; csproj; 3 file Migration AccountGovernance; tools/Check-DatabaseSafety.ps1, Scaffold-AccountGovernance.cs, Invoke-AccountMigrationPreview.ps1; tests/IntegrationTests.cs, Run-UiTests.ps1, AdminUi.Tests.mjs, AccountStateUi.Tests.mjs; SQL Preview và tài liệu này.

Bước trực tiếp: duyệt SQL Preview và phương án backup; nếu cần SMS thật, cung cấp provider/config qua kênh bí mật riêng, thay IIdentityMessageService cấu hình trong ApplicationUserManager.Create rồi kiểm thử OTP trên DB thử. Không gửi mã SMS trong phiên này.

## Giai đoạn 2 — nhập Excel

Đã bổ sung menu thêm một/nhập nhiều ở Products và Categories (chỉ Admin được nhập file). Upload .xlsx tối đa 2 MB/500 dòng, một worksheet; native .NET ZIP/XML scalar-only, không Office/ClosedXML/NuGet mới. Kiểm tra package/content type/quan hệ ngoài, giới hạn XML 4 MB/phần và tổng khai báo 16 MB/100 phần, không DTD, không macro/formula/hyperlink. Preview hiển thị dòng/cột lỗi, hợp lệ/trùng; token gắn user/session, hết hạn 20 phút; upload mới hủy intent cũ; Hủy không ghi; Confirm kiểm lại toàn bộ trong Serializable, chỉ tạo mới, rollback toàn batch nếu lỗi. Replay sau thành công trả 404, không tạo thêm. Không có cột QuantityInStock, sản phẩm mới tồn 0, không tạo ledger giả.

Headers sản phẩm: SKU, Tên sản phẩm, Danh mục, Mô tả, Đơn vị tính, Giá nhập, Giá bán, Mức tồn tối thiểu, Đường dẫn ảnh, Đang kinh doanh. Headers danh mục: Tên danh mục, Mô tả, Đang sử dụng. Giữ tên cột mẫu; TRUE/FALSE hoặc 1/0 (trống mặc định TRUE), giá dùng ô số hoặc dấu chấm, tối đa 2 số lẻ. Danh mục sản phẩm phải có sẵn và active; ảnh local ~/Content/... hoặc HTTPS. Dòng ví dụ phải được sửa/xóa trước khi nhập. Không hỗ trợ workbook nhiều sheet/công thức; đây là giới hạn chủ ý của template import.

Cách dùng: Admin → Products/Categories → Thêm… → Nhập nhiều từ Excel → Tải mẫu → Upload → đọc Preview/lỗi → Confirm hoặc Hủy. Không có route import cho Kho/khách.

GĐ2 Rebuild Debug + Razor PASS; 62 smoke PASS; integration có các ca Excel mới PASS (xem tổng trong phase2-integration-http.log); 218 HTTP PASS trên Dacn05_Test_22ede3ce1c7941a492d2a32c70d2fc2d. Đã test roundtrip template, thêm mới/duplicate, chỉ tồn 0, lỗi row/column, cấm QuantityInStock/formula, cap dòng, lỗi giả lập ở insert thứ hai rollback insert đầu; HTTP Upload/Preview/Cancel/Confirm/Replay và Admin-only. Không thêm migration. File: Controllers/CatalogImportController.cs; Services/CatalogWorkbook.cs, CatalogImportService.cs; ViewModels/CatalogImportViewModels.cs; Views/CatalogImport/Upload,Preview,Result; Views/Shared/_CatalogCreateMenu; Views/Products/Index, Categories/Index; Content/admin.css; csproj; tests/IntegrationTests.cs, Run-IntegrationTests.ps1, Run-UiTests.ps1.

## Giai đoạn 3 — chức vụ và bộ lọc hồ sơ

Dropdown chức vụ đã có danh sách yêu cầu, Khác nhập tự do hợp lệ tối đa 100 ký tự; giá trị cũ không có trong danh sách giữ trong ô Khác. Không sửa Identity Role. Employees/Customers Index mặc định lọc theo mã; nâng cao họ tên/email/SĐT liên hệ/trạng thái/ngày (thêm chức vụ cho nhân viên), 20 dòng/trang, GET giữ query và đặt lại. Ngày/điện thoại/độ dài sai trả validation, không query tham số ngày không hợp lệ.

GĐ3 Rebuild Debug/Razor PASS; 62 smoke, 278 integration, 224 HTTP PASS trên DB riêng Dacn05_Test_87b2a99b35404741884a9b952f9111cf. Kiểm dropdown UI bằng 3 test DOM PASS, Admin JS hiện còn 9 ca vì 8 ca lọc client Employees/Customers cũ được thay bằng kiểm EF/HTTP bộ lọc server. Không thêm migration. File: Services/PeopleDirectory.cs; EmployeesController/CustomersController; Views/Employees/Create,Edit,Index; Views/Customers/Index; partials _EmployeePosition, _PeopleFilters; csproj; tests IntegrationTests, Run-UiTests, AdminUi.Tests, EmployeePositionUi.Tests.

## Giai đoạn 4 — lịch sử giao dịch

Customers Details có lịch sử mua hàng theo CustomerId; Employees có đơn đã xử lý theo EmployeeId và phiếu nhập riêng; Suppliers có lịch sử phiếu nhập theo SupplierId. Mỗi phần 10 chứng từ/trang, projection/AsNoTracking, COUNT/SUM/page cố định, không load collection từng dòng. Tổng số đếm mọi trạng thái; tổng giá trị sales chỉ Confirmed/Completed, receipt chỉ Confirmed. Đơn online EmployeeId NULL không bị nhận là do nhân viên bán. Link dùng PK thật; không tạo dữ liệu hoặc đổi FK trong chức năng đọc lịch sử.

GĐ4 Rebuild Debug/Razor PASS; 62 smoke, 284 integration, 229 HTTP PASS trên Dacn05_Test_c0e7a77938bb44d89987dd8bbf4adcd1. Test chứng từ qua Service, nhiều trang, số tổng hợp, FK riêng, online chưa phân công, empty state và quyền truy cập. Không thêm migration. File: Services/TransactionHistory.cs; ViewModels/TransactionHistoryViewModel.cs; CustomersController/EmployeesController/SuppliersController (Details); ba Details views; Views/Shared/_TransactionHistory; csproj; IntegrationTests/Run-UiTests.

## Giai đoạn 5 — Mua lại

Nút Mua lại nằm dưới Tiếp tục mua sắm trong Store/Details. POST có AntiForgeryToken, query owner theo Customer.UserId và kiểm lại ở service. Lấy dòng cũ nhưng giá/active/category/tồn hiện tại; ghép vào giỏ hiện có, tối đa 100 mặt hàng/1000 đơn vị mỗi mặt hàng, lượng cộng bị chặn bởi tồn sau khi tính lượng đã có trong giỏ. Món không mua được có cảnh báo; không xóa món đang có, không sao chép giá/giảm giá cũ, không tạo Pending. Một transition cart version và vô hiệu checkout intent cũ; bấm lặp version cũ không cộng trùng.

GĐ5 Rebuild Debug/Razor PASS; 63 smoke, 290 integration, 237 HTTP PASS trên Dacn05_Test_df9cd2c315a14f718ca00ca8c569ee09. Đã kiểm đơn nhiều dòng, giỏ có sẵn, clamp tồn, ngừng bán/danh mục ngừng, giá mới, không tạo đơn, replay, đơn người khác, CSRF; test giá/active chỉ đổi fixture test và khôi phục trong finally, không sửa QuantityInStock trực tiếp. Pure-model tests dùng tồn trong bộ nhớ, không ghi DB. Không thêm migration. File: Services/ShoppingCart.cs, ReorderCart.cs; StoreController; Views/Store/Details, Cart; csproj; IntegrationTests/Run-UiTests.

## Giai đoạn 6 — kết quả cuối trên source mới

Ngày chạy 30/10/2026, Asia/Ho_Chi_Minh, khoảng 06:16–06:21. Windows, MSBuild/Visual Studio hiện có, .NET Framework 4.8.1, EF6/Identity2 theo packages.config, SQL Server 16.0.1000.6, IIS Express localhost:64341, Node.js. Nhánh feature/management-upgrades-20261030; HEAD gốc 15b13a8649817781a66b790d4ab5bd5e5218a4a5. Đây là **working tree chưa commit**, không phải kiểm thử lại nguyên commit gốc.

Manifest SHA256 từng file app/tests/tools (loại bin/obj/packages/.vs) trước/sau chạy trùng nhau: artifacts/management-upgrades/final-source-before.json và final-source-after.json. SHA256 manifest trước: C8EA4B88DC894442100EDD4E6AB07404DD2163675C526B72104E6EBE4AAA901D. Manifest chỉ lưu hash/path, không chép cấu hình hay secret. Báo cáo docs được hoàn thiện sau test và không nằm trong manifest này.

| Bộ kiểm tra trên lượt cuối | Kết quả |
|---|---|
| Rebuild Debug + MvcBuildViews=true | PASS, exit 0; còn warning CodeDom Access denied trong sandbox |
| Smoke | PASS 63 |
| Integration trên DB mới | PASS 298 |
| HTTP/IIS Express | PASS 241 |
| JavaScript | PASS 38: Customer 11, MegaMenu 10, Admin 9, trạng thái 5, chức vụ 3 |
| Identity reset hồi quy DB riêng | PASS 19; có warning assembly OWIN của harness |
| Structural/UTF-8/project registration | PASS 159 C#/Razor, 226 project entries, 50 POST actions |
| git diff --check, loại Web.config có thay đổi từ trước | PASS |
| Chrome UI thật, ảnh chụp responsive thực tế | NOT TESTED |
| SMS/SMTP/OAuth provider thật | NOT TESTED; chưa cấu hình, không gửi SMS/email |
| Backup VERIFYONLY/restore và migration DB thật | NOT TESTED, chưa Apply |

DB integration/HTTP cuối: Dacn05_Test_91c5b868659f4b71b27e0406056424aa. DB Identity reset: AdminReset_Test_368686a2382b473488092e62a11643c0. Giữ lại, không drop/reset. Evidence HTTP: artifacts/ui-verification/run-3584a79073064cf6989ba4d73cf72514. Logs cuối: artifacts/management-upgrades/final-build.log, final-smoke.log, final-js.log, final-integration-http.log, final-identity-reset.log, final-database-safety.log. Không đưa artifacts vào Git.

Ca mới vòng cuối: OTP session gắn đúng owner, mã sai không xác minh, mã hợp lệ xác minh trong test (không gửi SMS); collision số đã xác minh; đăng ký song song đúng một tài khoản; toàn bộ tồn cuối integration bằng tổng QuantityChange; ngày Employee Edit giữ yyyy-MM-dd; response recovery không tiết lộ email có tồn tại/xác minh; reset mã sai chung thông báo. ForgotPassword không giả vờ gửi email khi EmailService vẫn scaffold. Đã xử lý cookie thiếu NameIdentifier và giới hạn tổng XML giải nén thực tế ngoài giới hạn ZIP khai báo.

Lượt đầu vòng cuối FAIL ở ca đăng ký đồng thời: EF6 bọc deadlock trong EntityException, ngoài hai catch cũ. Đã bổ sung catch trả lỗi chung, Rebuild và chạy lại toàn bộ integration/HTTP trên DB mới: PASS. Lưu lượt lỗi ở final-attempt1-integration-http-FAIL.log; không dùng lượt đó để chứng nhận. Các kết quả GĐ1–5 phía trên là lịch sử từng bước, không cộng vào tổng cuối. Không còn FAIL chưa xử lý trong bộ tự động đã chạy; điều này không chứng nhận các mục NOT TESTED.

### Chạy lại và duyệt migration

Từ root repository, mở Developer PowerShell Visual Studio; restore NuGet nếu máy mới. Không chạy site mới trực tiếp với OnlineSalesDb trước khi migration được duyệt.

```powershell
msbuild OnlineSalesManagement.sln /t:Rebuild /p:Configuration=Debug /p:MvcBuildViews=true
powershell -NoProfile -ExecutionPolicy Bypass -File tests/Run-SmokeTests.ps1
node --test tests/CustomerUi.Tests.mjs tests/MegaMenu.Tests.mjs tests/AdminUi.Tests.mjs tests/AccountStateUi.Tests.mjs tests/EmployeePositionUi.Tests.mjs
powershell -NoProfile -ExecutionPolicy Bypass -File tests/Run-UiTests.ps1 -Port 64341
powershell -NoProfile -ExecutionPolicy Bypass -File tests/Test-ResetAdminPassword.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Invoke-AccountMigrationPreview.ps1 -Mode Preview
```

Run-UiTests tạo DB Dacn05_Test_* riêng, chạy integration rồi host site copy trên IIS Express; không sửa Web.config gốc. Test-ResetAdminPassword chỉ nhắm AdminReset_Test_* local. Preview tạo docs/migrations/AccountGovernance.sql, không Apply; không dùng Mode Scaffold để vận hành. Code mới cần bool/bảng audit; chưa tương thích schema thật cũ đến khi migration được duyệt. Metadata read-only cuối phiên xác nhận cột/bảng mới **chưa có trong OnlineSalesDb**. Không chạy Update-Database trên kết nối thật khi chưa duyệt.

Các bước người dùng cần làm: (1) xem diff và SQL Preview; (2) tạo backup COPY_ONLY mới với CHECKSUM, VERIFYONLY và restore DB riêng; (3) kiểm bằng Chrome desktop/tablet/mobile, modal Hủy/Xác nhận trên DB thử; (4) cấu hình provider SMS thật qua secret ngoài source nếu cần dùng xác minh số điện thoại, kiểm delivery/expiry/resend; SMTP recovery tự động vẫn chưa được triển khai; (5) chỉ sau đó duyệt lịch Apply migration thật và deployment. Không có Apply, Commit hay Push trong phiên này. Không sửa Web.config máy cá nhân, không chỉnh tồn/ledger trực tiếp, không thay tài khoản Admin thật.

### File bổ sung trong vòng rà soát cuối

Ngoài danh sách GĐ1–5: Views/Account/ForgotPasswordConfirmation.cshtml; cập nhật thêm IdentityProvisioning, AccountController, ManageController, Startup.Auth, CatalogWorkbook, Employees/Create/Edit và các test tương ứng như mô tả phía trên. Danh sách đầy đủ bên dưới loại Web.config có thay đổi từ trước.

## Danh sách file sửa/thêm đầy đủ

- docs/MANAGEMENT_UPGRADES_20261030.md
- docs/migrations/AccountGovernance.sql
- OnlineSalesManagement/App_Start/IdentityConfig.cs
- OnlineSalesManagement/App_Start/Startup.Auth.cs
- OnlineSalesManagement/Content/admin.css
- OnlineSalesManagement/Controllers/AccountController.cs
- OnlineSalesManagement/Controllers/AdminUsersController.cs
- OnlineSalesManagement/Controllers/CatalogImportController.cs
- OnlineSalesManagement/Controllers/CustomersController.cs
- OnlineSalesManagement/Controllers/EmployeesController.cs
- OnlineSalesManagement/Controllers/ManageController.cs
- OnlineSalesManagement/Controllers/StoreController.cs
- OnlineSalesManagement/Controllers/SuppliersController.cs
- OnlineSalesManagement/Migrations/202610292211500_AccountGovernance.cs
- OnlineSalesManagement/Migrations/202610292211500_AccountGovernance.Designer.cs
- OnlineSalesManagement/Migrations/202610292211500_AccountGovernance.resx
- OnlineSalesManagement/Models/AccountAdministrationAudit.cs
- OnlineSalesManagement/Models/IdentityModels.cs
- OnlineSalesManagement/OnlineSalesManagement.csproj
- OnlineSalesManagement/Services/AccountGovernance.cs
- OnlineSalesManagement/Services/CatalogImportService.cs
- OnlineSalesManagement/Services/CatalogWorkbook.cs
- OnlineSalesManagement/Services/IdentityProvisioning.cs
- OnlineSalesManagement/Services/PeopleDirectory.cs
- OnlineSalesManagement/Services/ReorderCart.cs
- OnlineSalesManagement/Services/ShoppingCart.cs
- OnlineSalesManagement/Services/TransactionHistory.cs
- OnlineSalesManagement/ViewModels/CatalogImportViewModels.cs
- OnlineSalesManagement/ViewModels/TransactionHistoryViewModel.cs
- OnlineSalesManagement/Views/Account/ForgotPasswordConfirmation.cshtml
- OnlineSalesManagement/Views/AdminUsers/AccountAudit.cshtml
- OnlineSalesManagement/Views/AdminUsers/Index.cshtml
- OnlineSalesManagement/Views/CatalogImport/Preview.cshtml
- OnlineSalesManagement/Views/CatalogImport/Result.cshtml
- OnlineSalesManagement/Views/CatalogImport/Upload.cshtml
- OnlineSalesManagement/Views/Categories/Index.cshtml
- OnlineSalesManagement/Views/Customers/Details.cshtml
- OnlineSalesManagement/Views/Customers/Index.cshtml
- OnlineSalesManagement/Views/Employees/Create.cshtml
- OnlineSalesManagement/Views/Employees/Details.cshtml
- OnlineSalesManagement/Views/Employees/Edit.cshtml
- OnlineSalesManagement/Views/Employees/Index.cshtml
- OnlineSalesManagement/Views/Products/Index.cshtml
- OnlineSalesManagement/Views/Shared/_CatalogCreateMenu.cshtml
- OnlineSalesManagement/Views/Shared/_EmployeePosition.cshtml
- OnlineSalesManagement/Views/Shared/_PeopleFilters.cshtml
- OnlineSalesManagement/Views/Shared/_TransactionHistory.cshtml
- OnlineSalesManagement/Views/Store/Cart.cshtml
- OnlineSalesManagement/Views/Store/Details.cshtml
- OnlineSalesManagement/Views/Suppliers/Details.cshtml
- tests/AccountStateUi.Tests.mjs
- tests/AdminUi.Tests.mjs
- tests/EmployeePositionUi.Tests.mjs
- tests/IntegrationTests.cs
- tests/Run-IntegrationTests.ps1
- tests/Run-UiTests.ps1
- tools/Check-DatabaseSafety.ps1
- tools/Invoke-AccountMigrationPreview.ps1
- tools/Scaffold-AccountGovernance.cs
