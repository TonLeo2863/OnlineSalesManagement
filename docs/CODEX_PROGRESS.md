# Tiến độ CNTT_DACN_05

## 16/10/2026 — khởi động và bản sửa ưu tiên

- CWD C:\Projects\OnlineSalesManagement; origin đúng TonLeo2863/OnlineSalesManagement. Working tree ban đầu sạch; codex-fixes và master cùng 877c60b.
- Tạo codex/dacn05-completion từ trạng thái trên; chưa fetch nên chưa khẳng định trùng master mới nhất trên remote.
- Công cụ sandbox không khởi tạo (`setup refresh had errors`); các lệnh kiểm tra/build chạy qua yêu cầu quyền ngoài sandbox.
- Tìm thấy VS2022 Community/MSBuild, dotnet, sqlcmd và SQLEXPRESS đang chạy. Target thực tế net481.
- Restore NuGet thành công; build baseline exit 0. Sửa csproj: reference Facebook trước đó mang nhầm tên Cookies.
- Sửa ProductsController: không bind tồn/ngày tạo; cập nhật entity đã tải bằng danh sách trường rõ ràng; kiểm tra RowVersion; xử lý concurrency, duplicate/FK, chặn xóa sản phẩm có lịch sử/tồn. Bỏ các input tồn/ngày tạo và editor RowVersion trùng trong Create/Edit; hiển thị lỗi Delete.
- Thêm role attributes cho Products/Customers/Employees/Suppliers/PurchaseReceipts/SalesOrders/InventoryReports/Reports. Chưa triển khai quản trị role, chưa test HTTP; không coi phân hệ auth đã hoàn tất.
- Tạo REQ_DACN05_MATRIX.md, TEST_RESULTS.md, SETUP_AND_RUN.md và file này.
- Không sửa PurchaseReceiptService, SalesOrderService, migration hoặc database. Không phát triển ca trực.

## Vấn đề và bước tiếp theo

1. Build Debug + Razor sau sửa đã đạt; còn cần test HTTP/DB.
2. Kiểm tra remote branch/master và Git diff; chỉ commit file liên quan, không stage packages/bin/obj.
3. Rà soát Web.config theo cách không in secrets, schema/migration DB chỉ đọc, service nhập/xuất và invoice.
4. Hoàn thiện quản trị người dùng/role an toàn, category CRUD, validation CRUD còn lại, storefront dùng Identity Customer.UserId; không tự cấp Admin hoặc mật khẩu cố định.
5. Sửa báo cáo Purchases/Sales dùng InventoryTransactions.TransactionDate, lượng/giá snapshot; kiểm tra giao dịch hủy và định nghĩa chỉ tiêu.
6. Chạy integration/role/UI/in A4 trên DB kiểm thử được cho phép. Bổ sung phân tích/ERD theo model, khảo sát mẫu, báo cáo học thuật, kế hoạch 10 tuần, thuyết trình 15 phút và checklist ảnh thật.

## Git và bàn giao

Commit công việc: chưa tạo. Push: chưa thực hiện. Sau commit ghi SHA vào mục kế tiếp bằng commit tài liệu riêng (SHA của commit không thể tự nằm trong chính nội dung commit đó). Tiến độ được lưu trước khi push; sau push ghi nhận kết quả thật.

## Mốc commit đã kiểm chứng

- 289832e: bảo vệ CRUD sản phẩm, ranh giới phân quyền controller, sửa reference và tài liệu. Build Debug + Razor đạt; chưa test HTTP/DB ghi.
- Fetch origin thành công; origin/master cùng baseline 877c60b (0/0 trước commit).
- SQL chỉ đọc: SQL Server 2022 phiên bản 16.0.1000.6; có 2 migration InitialCreate/AddShiftManagementTables; 1 user, chưa có role; 20 giao dịch kho; 0 sản phẩm lệch tổng QuantityChange. Chưa khẳng định model khớp toàn bộ schema.
- Chuẩn bị push các commit này lên codex/dacn05-completion; chỉ ghi push thành công sau khi nhận kết quả.

## Báo cáo và smoke test — 16/10/2026

- Push giai đoạn đầu thành công: 289832e và 286d1ce trên origin/codex/dacn05-completion.
- ReportsController.Purchases/Sales: dùng TransactionDate, QuantityChange và UnitPrice của InventoryTransactions; chỉ chứng từ hiện còn hiệu lực; mô tả rõ loại chứng từ hủy, không phải sổ ròng. Chặn ngày sai định dạng trước truy vấn. Bổ sung CSS A4 ngang/lặp tiêu đề bảng.
- tests/SmokeTests.cs và Run-SmokeTests.ps1: 35 kiểm tra đạt (quy tắc stock, overflow, role attributes, anti-forgery, bind và ngày sai). Không phải test HTTP/integration. Script mặc định bị execution policy chặn; chạy trong tiến trình riêng với -ExecutionPolicy Bypass, không đổi policy máy.
- Build Debug + MvcBuildViews=true exit 0; chưa kiểm chứng in A4 hoặc dữ liệu báo cáo qua HTTP.
- Đang chờ người dùng chọn tài khoản Admin đầu tiên, không tự nâng quyền. Tiếp tục CRUD/danh mục, storefront, phân tích và test độc lập.

- Commit báo cáo/test 3f803ac: build Razor đạt, 35 smoke test đạt; chuẩn bị push. Chưa kiểm thử HTTP/báo cáo bằng DB.


## Checkpoint theo yêu cầu người dùng — dừng yêu cầu quyền lặp lại

- Không tiếp tục shell escalation/retry; không reset/xóa code hoặc thay đổi cấu hình Codex. Đã xác minh file đã lưu bằng Node filesystem, không chạy shell.
- HEAD local và remote-tracking: 4866f637ca66db7357459b0423f87406b8869a68, nhánh codex/dacn05-completion. Code mới sau checkpoint còn chưa commit/push.
- File mới đã lưu: Controllers/AdminUsersController.cs, CategoriesController.cs, StoreController.cs; Views/AdminUsers, Categories, Store; docs/ANALYSIS_AND_DESIGN.md, REPORT_AND_HANDOVER.md; tests/IntegrationTests.cs, Run-IntegrationTests.ps1. Các sửa CRUD/Identity/menu/csproj và bản sửa cuối SalesOrders/InventoryReports/Layout vẫn giữ nguyên.
- Build Debug + MvcBuildViews=true thực sự exit 0 trước bản sửa cuối. DLL timestamp UTC 2026-10-15T21:09:14.341Z. Bản sửa cuối lúc 21:20 UTC (Complete action, đối chiếu tồn, CSS in) CHƯA build/test. Không coi toàn bộ working tree đã kiểm thử.
- Smoke cũ: 35 PASS thực chạy. SmokeTests.cs đã bổ sung ca Admin/Categories/Store sau lần chạy đó, CHƯA chạy lại bản mở rộng.
- Integration: lần đầu harness lỗi PowerShell InitialCatalog (không phải lỗi app/sandbox); đã sửa sang key Initial Catalog. Lần sau thực sự exit 0, TOTAL INTEGRATION PASS 28. DB tách biệt Dacn05_Test_194b22964f3f4d0989f62917ea62e521, giữ lại, không drop. Bao gồm migration/model compatibility, nhập nhiều dòng, trùng xác nhận, pending, giá snapshot, ngày kho, vượt tồn/rollback, hủy/hoàn, đối chiếu, RowVersion. Chưa HTTP/role/checkout/in A4/tải lớn.
- Sandbox: lần gọi mặc định lỗi trước tạo process: helper_unknown_error: setup refresh had errors. Các build/test thành công nói trên chạy ngoài sandbox theo tool approvals. Không phải lỗi biên dịch C#. Config người dùng có [windows] sandbox = elevated; repo trusted; không có .codex/config.toml trong repo. Không tìm thấy C:/Users/TonLeo/.codex/.sandbox/sandbox.log. Chưa xác định nguyên nhân sâu hơn và chưa xác nhận sandbox đã phục hồi.
- Tiếp tục sau khi môi trường được sửa: một lệnh chỉ đọc ngắn trong sandbox, không escalation; nếu đạt mới build snapshot hiện tại, chạy smoke mở rộng, rà soát admin/storefront qua HTTP/DB thử, cập nhật ma trận rồi commit/push đúng phạm vi. Không tự nâng Admin; câu hỏi chỉ định tài khoản Admin đầu tiên còn chờ.


## Tiếp tục an toàn — sandbox vẫn lỗi (16/10/2026)

- Thử đúng một lệnh git status --short trong sandbox: lỗi setup refresh had errors trước tạo process. Không retry/escalation, không chạy shell qua công cụ khác hoặc đổi bảo mật Windows.
- Đã đọc checkpoint trước khi sửa. Kiểm kê file bằng đọc Git index và so hash raw/LF, không sửa .git; không thay thế git status chính thức. Không fetch/diff/commit/push trong lượt này.
- StoreController.cs: Checkout GET/POST, Orders và Details trả 401 trước truy vấn khi Identity UserId trống, tránh lọc UserId == null làm lộ đơn khách tại quầy.
- Token checkout gắn thêm owner UserId và ProductId trong session; sai owner/sản phẩm trả 400 trước truy vấn; xóa cả ba khóa khi thành công.
- SmokeTests.cs thêm fake HttpContext/Session và 6 ca cho thiếu UserId và token sai owner/product. CHƯA compile/chạy các ca mới.
- Check-Source.mjs chạy thực qua Node tool, không shell: 121 file C#/Razor đăng ký đúng trong 184 Compile/Content entries; không entry trùng/thiếu file; UTF-8 strict hợp lệ; 42 POST được nhận diện có AntiForgeryToken. Đây là kiểm tra cấu trúc, KHÔNG thay thế build/HTTP/SQL.
- MODEL_CLASSES.mmd tạo từ model: 16 lớp, 199 thuộc tính, 21 navigation đơn thực tế. Giữ các lớp ca trực chỉ chuẩn bị. Header/ngoặc kiểm tra đạt, CHƯA render Mermaid.
- Content/admin.css đã được khai báo csproj; không sửa project thêm trong lượt này.
- Không thể chạy hiện tại: git status/diff chính thức, MSBuild/Razor, smoke/integration mới, IIS/HTTP, commit/push. Kết quả cũ 35 smoke và 28 integration chỉ áp dụng snapshot cũ.
- Không ghi database, migration, cấu hình Windows hoặc merge master. Chưa chỉ định/cấp Admin đầu tiên.
- File lượt này: StoreController.cs, SmokeTests.cs, Check-Source.mjs, MODEL_CLASSES.mmd, bốn tài liệu tiến độ và liên kết trong ANALYSIS_AND_DESIGN.md.
- Tiếp tục khi sandbox phục hồi: một git status chỉ đọc; review diff; build Debug + MvcBuildViews=true; smoke mở rộng; HTTP owner/token/role trên DB thử; integration; chỉ commit/push phần đạt lên codex/dacn05-completion.


## Lượt tiếp tục sau thông báo CLI 0.163.0

- Người dùng báo codex.cmd exec --sandbox read-only chạy git status thành công. Công cụ exec_command của phiên này vẫn báo helper_unknown_error: setup refresh had errors trước tạo process ngay lần đầu; không retry/escalation hoặc chạy tiến trình qua công cụ khác.
- Nhóm kiểm tra đầu tiên chưa có lệnh nào thực thi: git status/branch/diff, đọc docs qua shell và probe ghi đều chưa chạy. Không suy ra quyền ghi sandbox từ kết quả CLI read-only hoặc từ Node filesystem.
- Đã đọc CODEX_PROGRESS.md, REQ_DACN05_MATRIX.md và TEST_RESULTS.md bằng Node filesystem. Đọc Git index v2 và blob chỉ đọc để rà thay đổi; 21 tracked file khác index khớp thông tin người dùng. Đây không thay thế native git status/diff hay xác minh staged/remote. Không sửa .git.
- Rà diff CRUD Customers/Employees/Suppliers (mã trùng/FK/giữ owner), action Complete và đối chiếu ledger; giữ nguyên code đang làm dở. Không sửa hai service kho hoặc migration.
- Thêm tests/Verify-Dacn05.ps1: tìm MSBuild từ PATH/vswhere; Rebuild Debug + MvcBuildViews=true; dừng khi native build lỗi; smoke; integration tùy switch trên DB thử riêng; so SHA256 source trước/sau. Ghi JSON vào artifacts/verification (đã nằm trong ignore artifacts/). Chưa thực thi hoặc xác nhận cú pháp PowerShell bằng runtime.
- Runner chỉ đánh dấu test đã qua sau khi script con chạy thành công; lỗi ghi rõ FailureStage. Không tự stage/commit/push hoặc cho kết quả thành bằng chứng HTTP/UI.
- Hợp nhất REQ_DACN05_MATRIX.md thành trạng thái hiện tại; không còn dựa trên bảng kiểm kê ban đầu đã cũ. Lịch sử 35 smoke/28 integration vẫn giữ trong tài liệu kiểm thử.
- Node Check-Source.mjs thực chạy đạt: 121 file C#/Razor, 184 project entries, UTF-8 strict và 42 POST có token theo kiểm tra source. Không phải build hoặc test C#/HTTP.
- Ghi file và đọc lại qua Node filesystem thành công; khả năng ghi bằng process trong sandbox hiện tại CHƯA kiểm chứng. Không đổi bảo mật Windows hay cấu hình Codex.
- Không thể thực hiện trong lượt này: native Git status/diff, MSBuild/Razor, test C# mới, IIS/HTTP, commit/push. Không đánh dấu working tree hoàn thành và không dùng kết quả cũ thay thế.
- Cần tiếp tục: khôi phục công cụ exec của phiên; kiểm tra read-only/ghi riêng; native git status/diff; chạy Verify-Dacn05.ps1 và sau đó -IncludeIntegration; xử lý lỗi nếu có; HTTP role/owner/checkout/Complete; review chọn file rồi commit/push nhánh codex/dacn05-completion. Không merge master/drop DB. Admin đầu tiên chưa được chọn.

## Xác minh bàn giao trong phiên mới — 16/10/2026

- Sandbox mặc định: git status chạy được; tạo file probe tên GUID trong project, đọc đúng nội dung rồi xóa chính file đó thành công. Không sửa dữ liệu khác trong phép thử.
- Rà native Git diff, các Controller/View mới và cũ, Identity, model liên quan và csproj; không có file Model thay đổi. git diff --check đạt. Không cần sửa code trong phiên xác minh này.
- Đã đọc Verify-Dacn05.ps1 và cả hai script con trước khi chạy. Runner mặc định chỉ rebuild/test và ghi artifacts/bin/obj; không thao tác Git hoặc ghi DB. Integration tùy chọn có migration Up/seed/ghi dữ liệu trên DB mới tên GUID, kiểm tra prefix Dacn05_Test_ và Windows Authentication trước khi chạy; không drop DB hoặc chạy Down.
- powershell -NoProfile -ExecutionPolicy Bypass -File tests/Verify-Dacn05.ps1: exit 0; Rebuild Debug + MvcBuildViews=true đạt, 52 smoke PASS, SourceStable=true. Record cục bộ: artifacts/verification/verify-20261015-222944-182.json (ignored; Integration=NotRun trong record này vì được chạy riêng).
- Audit Node thực chạy checkSource: 121 source / 184 project entries / 42 POST có token / UTF-8 hợp lệ. Đây là audit cấu trúc, không thay thế HTTP.
- Run-IntegrationTests.ps1: lần trong sandbox thất bại kết nối SQL do SSPI (The target principal name is incorrect); không coi là lỗi logic ứng dụng. Lần chạy lại ngoài sandbox được phê duyệt: exit 0, TOTAL INTEGRATION PASS 28. DB thử Dacn05_Test_3cbfd79b114b43f2badf1f2c6f2e361a được giữ lại; không sửa DB ứng dụng. 133 hash source tiếp tục khớp bản build.
- Chưa kiểm chứng: HTTP login/logout/cookie/role, CRUD qua request thật, checkout thành công/truy cập đơn chéo owner qua HTTP, Complete qua HTTP/DB, cạnh tranh thực tế, UI/mobile/in A4/tải lớn; chưa chỉ định Admin đầu tiên, render Mermaid hoặc dàn trang DOCX/PPTX. 52 smoke/28 integration không chứng nhận các phần này.
- Chỉ bàn giao thay đổi đã có; không phát triển tính năng mới, merge master, reset, xóa thay đổi cũ hoặc migration DB thật. Kết quả mới này thay thế trạng thái chưa build/test của các checkpoint lịch sử bên trên, không phải nghiệm thu toàn bộ đồ án.

## Mốc bàn giao đã push — 16/10/2026

- Commit source/tài liệu đã kiểm chứng: dd753f65e81c75e7ee110e7f7a76eec95a9ca8f4.
- git push origin HEAD:refs/heads/codex/dacn05-completion exit 0; remote cập nhật 4866f63..dd753f6. Không force, không merge master.
- Build/Razor đạt, 52 smoke PASS, 28 integration PASS (ngoài sandbox, DB thử riêng); giới hạn nghiệm thu giữ nguyên như mục xác minh ở trên.
