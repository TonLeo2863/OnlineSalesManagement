# Kết quả và kịch bản kiểm thử

## Hoàn thiện bàn giao — lượt mới nhất 21/10/2026

Đối chiếu `git ls-remote origin`: nhánh `codex/dacn05-completion` vẫn ở 58b2d5183847583dae5f9b44f00be1727f2a1958. Tests/tài liệu bổ sung chưa commit, logic ứng dụng giữ nguyên. Database thật vẫn chỉ đọc.

| Kiểm tra | Kết quả | Bằng chứng / giới hạn |
|---|---|---|
| Rebuild Debug + Razor, smoke, integration cuối worktree | PASS: 52 smoke / 101 integration | artifacts/verification/verify-20261021-164422-863.json; SourceStable=true |
| HTTP xác thực/quyền/COD/owner/Confirm/Complete/hóa đơn/báo cáo | PASS 144 | artifacts/handover-browser-a4.log; toàn bộ chạy lại trong lượt mới |
| Chrome form Admin, tạo/đọc/sửa sản phẩm/khách/nhân viên/NCC | PASS | tests/Browser-Handover.mjs; 24 browser checks tổng cộng gồm phần in; kiểm danh sách sau redirect và sau sửa |
| Form sản phẩm không sửa tồn | PASS | Chrome kiểm tra không có input QuantityInStock |
| Xóa dữ liệu qua UI | CHƯA KIỂM THỬ | Không xóa fixture hoặc dữ liệu thật trong lượt này |
| PDF hóa đơn/doanh thu/tồn | PASS A4 dọc | PDF MediaBox; ảnh print media đã xem |
| PDF báo cáo nhập/xuất | PASS A4 ngang | CSS source thiết kế landscape; PDF MediaBox đúng |
| Hình ảnh chế độ in | PASS với dữ liệu thử | Đã xem 5 ảnh: bảng/tổng đọc được, không thấy chồng chữ hoặc cắt cột; enum có nơi còn tiếng Anh |
| Máy in vật lý, từng trang PDF qua renderer độc lập, chứng từ cực dài | CHƯA KIỂM THỬ | Không suy diễn từ xuất PDF thành đã in máy thật |
| Giỏ UI ba sản phẩm / thông báo | CHƯA KIỂM THỬ, chưa tìm thấy source | Không có stash/unreachable object hoặc file Cart/Notification/GioHang trong Git tìm được; không suy diễn từ service ba dòng thành giỏ UI |
| Admin database demo | CHƯA ĐẠT điều kiện demo | OnlineSalesDb có 0 user / 0 role; không cấp quyền khi chưa có xác nhận |
| Backup / VERIFYONLY / restore thử / PDF ký | CHƯA KIỂM THỬ hoặc chưa có | Người vận hành thực hiện theo DATABASE_OPERATOR_CHECKLIST.md |

Lượt browser thành công dùng DB riêng `Dacn05_Test_e7faacc84c3c47d38949ff704e83eac7`; artifacts/ui-verification/run-5fd81062d7df4aa8b5899734ec98934d/browser/ chứa browser-result.json, ảnh CRUD, invoice-a4.pdf, revenue-a4.pdf, purchases-a4.pdf, sales-a4.pdf, inventory-a4.pdf và ảnh print. Database được giữ lại; không drop/migrate DB thật.

Hai lượt browser trước FAIL do harness: chọn form POST đầu trang (logout) thay vì form đang điền; sau đó yêu cầu A4 dọc cho báo cáo đã thiết kế A4 ngang. Log được giữ ở artifacts/handover-browser-final.log và handover-browser-retry.log. Đã sửa harness và chạy lại toàn bộ đạt 24; không đổi logic app. Các FAIL này không bị ghi thành PASS của lượt đầu.

Đóng gói: tests/Export-HandoverSource.ps1 tạo source draft và manifest SHA256 theo allowlist, kiểm từng entry ZIP, loại runtime/config fixture/profile browser. Web.config trong gói dùng template SQL Windows Authentication; dữ liệu/mật khẩu không được mang từ DB thử. Source draft chưa chứa PDF báo cáo ký hoặc backup. Kiểm chứng trên source đóng gói được ghi ở FINAL_SNAPSHOT_VALIDATION.md sau khi thực hiện; không tự coi restore NuGet/DB trên máy sạch đã đạt.

Các mục dưới là lịch sử các lượt trước trong ngày và phiên 16/10.

## Phiên PC MAY042 — 21/10/2026

Snapshot: worktree `.handover-worktree`, nhánh `codex/dacn05-completion`, HEAD nền 58b2d51; Web.config dùng SQL cục bộ. Thay đổi phiên này: mở rộng kiểm thử và tài liệu bàn giao; chưa sửa logic ứng dụng.

| Hạng mục | Kết quả thật |
|---|---|
| Master tại thư mục gốc, Build Debug + Razor | PASS, nhưng không dùng để chứng nhận nhánh phát triển |
| Nhánh phát triển, Rebuild Debug + Razor | PASS |
| Smoke | PASS 52 |
| Integration nền | PASS 92, DB riêng Dacn05_Test_c86e0f279912485b94e1eb012d3d0dd6 |
| HTTP nền | PASS 122 trên bản sao IIS Express và cùng DB thử; artifacts/ui-verification/run-a0c174768e7d43b7ac61bb9775c038f4 |
| HTTP mở rộng cuối phiên | PASS 144 tổng cộng, gồm checkout COD một sản phẩm → Pending → Admin xác nhận → trừ tồn một lần → khách xem trạng thái → hóa đơn/báo cáo → Complete không trừ thêm; chặn replay và owner khác |
| Integration bổ sung ba sản phẩm ở service | PASS 101 tổng cộng; DB riêng Dacn05_Test_218103954dde42a38ad1497a14c00c59 |
| Một Pending ba dòng → xác nhận lặp → trừ đúng một lần → báo cáo xuất → Complete → chặn hủy Completed | PASS tại service; trả ViewResult hóa đơn PASS; không phải kiểm thử giỏ hàng hay in A4 |
| Database gốc | Chỉ đọc: 5 đơn, 16 ledger, 0 user/role, 0 sản phẩm lệch tồn; không ghi/migrate/cấp Admin |
| CRUD ghi toàn bộ qua HTTP, giỏ hàng ba sản phẩm, thông báo, A4 trực quan, PDF ký/backup/zip | NOT TESTED / chưa có đủ artifact |

Record Rebuild/smoke: artifacts/verification/verify-20261021-161206-133.json (SourceStable=true). HTTP cuối phiên: artifacts/http-handover-retry-20261021.log; HTML artifacts/ui-verification/run-57c222516bce41958722c9b3efa2eafe; DB Dacn05_Test_327efecdc13e4d00915f62f8815a3c49 (giữ lại). Trong cùng lượt HTTP, integration 101 PASS trước khi chạy IIS. Các artifact bị Git ignore, giữ trên máy để đối chiếu. Không drop các DB thử.

Kiểm tra cuối sau khi mở rộng test: artifacts/verification/verify-20261021-161958-092.json — Status/Build/Smoke/Integration=Passed, SourceStable=true; 52 smoke và 101 integration. Log artifacts/final-verification-20261021.log. git diff --check PASS. Truy vấn DB gốc cuối phiên vẫn 0 user / 0 role / 5 đơn / 16 ledger. HTML lưu thực tế hiển thị trạng thái Pending rồi Confirmed; giao diện enum chưa bản địa hóa đầy đủ. Source ứng dụng không thay đổi sau lượt HTTP 144 PASS; lần Rebuild cuối kiểm tra cùng hash source hiện tại.

Lượt mở rộng đầu tiên FAIL ở assertion nhãn Pending tiếng Việt sau khi đặt COD; không coi là hoàn tất kiểm thử. Đã chỉnh assertion cho tên enum/DisplayName và lưu HTML Pending/Confirmed, chạy lại toàn bộ đạt 144; xem log lỗi artifacts/http-handover-20261021.log và HTML lượt thành công. Không sửa logic ứng dụng để làm test đạt. Chưa khẳng định giao diện đã bản địa hóa đầy đủ.

Lỗi thao tác đã xử lý: Git không nằm trên PATH (dùng Git kèm VS); switch bị chặn để bảo toàn Web.config (dùng worktree riêng); PowerShell chặn script (chạy tiến trình con với ExecutionPolicy Bypass, không thay policy máy). Không phải lỗi ứng dụng.

Các mục bên dưới là lịch sử phiên cũ, không thay thế kết quả hiện tại. Xem [HANDOVER_20261021.md](HANDOVER_20261021.md) cho phân loại và điều kiện bàn giao.

## Đã thực thi 16/10/2026

- `git status --short`: sạch trước khi làm; nhánh ban đầu codex-fixes.
- `git rev-list --left-right --count master...HEAD`: 0/0; HEAD 877c60b.
- MSBuild Debug trước restore: thất bại do thiếu CodeDom NuGet props.
- MSBuild Restore với RestorePackagesConfig=true: exit 0.
- MSBuild Debug sau restore/sửa tên reference Facebook: exit 0, tạo OnlineSalesManagement.dll.
- Build sau sửa controller với MvcBuildViews=true: exit 0. Git diff --check phát hiện khoảng trắng cuối dòng form; đã sửa và kiểm tra lại.
- Chưa chạy HTTP, kiểm thử tích hợp SQL, in A4 hoặc kiểm thử theo role. Chưa kết luận chức năng hoàn thành.

## Kịch bản bắt buộc trên database kiểm thử

| ID | Thao tác | Kỳ vọng | Kết quả |
|---|---|---|---|
| AUTH01 | Đăng nhập đúng/sai, logout rồi truy cập URL cũ | Đúng vào hệ thống; sai báo lỗi; logout không vào vùng bảo vệ | Chưa chạy |
| AUTH02 | Anonymous và khách gọi GET/POST từng controller quản trị, có/không token | Không được đọc/ghi; token không thay thế phân quyền | Chưa chạy |
| AUTH03 | Admin/Kho/BanHang gọi URL trực tiếp | Chỉ đúng vai trò được truy cập | Chưa chạy |
| PROD01 | Create POST gửi QuantityInStock=100, CreatedDate cũ | Tồn 0, ngày server; không có giao dịch giả | Chưa chạy |
| PROD02 | Edit POST gửi tồn/ngày tạo giả | Giá trị tồn/ngày tạo trong DB giữ nguyên | Chưa chạy |
| PROD03 | Hai tab tải cùng RowVersion, lưu lần lượt | Lần hai báo xung đột; không ghi đè | Chưa chạy |
| PROD04 | Sửa sản phẩm đồng thời với xác nhận nhập | Không làm mất tồn; stale form báo xung đột | Chưa chạy |
| PROD05 | SKU trùng, category không tồn tại, thiếu tên/đơn vị, giá âm | Validation thân thiện; không lỗi 500 | Chưa chạy |
| PROD06 | Xóa có tồn hoặc dòng chứng từ; xóa ID không tồn tại | Chặn xóa hoặc 404; lịch sử giữ nguyên | Chưa chạy |
| CRUD01 | Khách/nhân viên/NCC/danh mục: hợp lệ, mã trùng, thiếu dữ liệu, xóa liên kết | Đúng validation và FK | Chưa chạy |
| IN01 | Tạo nháp 2 dòng, xác nhận, xác nhận lại | Nháp không tăng; tăng đúng một lần, có lịch sử | Chưa chạy |
| IN02 | Hủy nháp/đã xác nhận; hàng đã xuất không đủ trả | Đúng trạng thái, rollback toàn bộ nếu không đủ | Chưa chạy |
| OUT01 | Pending, vượt tồn, xác nhận lặp, 2 đơn tranh tồn, hủy | Không âm/không trừ hai lần, hoàn tồn đúng quy tắc | Chưa chạy |
| ONLINE01 | Khách tạo đơn sửa giá/khách/role/status; xem đơn khách khác | Giá server, Pending, chặn truy cập chéo | Chưa chạy |
| INV01 | In Confirmed/Completed và thử Pending/Cancelled | Chứng từ nội bộ hợp lệ, chặn sai trạng thái, đủ thông tin A4 | Chưa chạy |
| REP01 | Khoảng ngày đảo, sai định dạng, ngày tối đa, không dữ liệu, số 0 | Không crash, có lỗi/empty state rõ ràng | Chưa chạy |
| REP02 | Tạo phiếu ngày A, xác nhận ngày B | Báo cáo kho thuộc B; doanh thu theo định nghĩa OrderDate | Chưa chạy |
| REP03 | Tổng QuantityChange so tồn; kiểm tra hủy; nhập/xuất giá lịch sử | Khớp từng sản phẩm, tổng và chi tiết | Chưa chạy |
| UI01 | Desktop/mobile, form lỗi, báo cáo/in A4, dữ liệu lớn | Đọc được, không mất dòng/đè chữ | Chưa chạy |

Chỉ dùng dữ liệu kiểm thử được cho phép. Không xóa hoặc reset database thật để chạy các ca này.

## Kiểm tra SQL chỉ đọc

SQL Server 16.0.1000.6; __MigrationHistory có 2 migration tương ứng file source. 1 tài khoản, 0 role. 20 giao dịch; truy vấn đối chiếu Product.QuantityInStock với tổng QuantityChange: 0 sản phẩm lệch. Không chạy migration hoặc thay đổi dữ liệu. Fetch origin thành công; baseline trùng origin/master.

## Smoke test đã thực thi

Build Debug + Razor sau sửa báo cáo: exit 0. Run-SmokeTests.ps1: TOTAL PASS 35, exit 0. Chỉ kiểm tra logic model, metadata phân quyền/anti-forgery/bind và chặn ngày lỗi; chưa test HTTP hoặc ghi SQL.


## Checkpoint theo yêu cầu người dùng — dừng yêu cầu quyền lặp lại

- Không tiếp tục shell escalation/retry; không reset/xóa code hoặc thay đổi cấu hình Codex. Đã xác minh file đã lưu bằng Node filesystem, không chạy shell.
- HEAD local và remote-tracking: 4866f637ca66db7357459b0423f87406b8869a68, nhánh codex/dacn05-completion. Code mới sau checkpoint còn chưa commit/push.
- File mới đã lưu: Controllers/AdminUsersController.cs, CategoriesController.cs, StoreController.cs; Views/AdminUsers, Categories, Store; docs/ANALYSIS_AND_DESIGN.md, REPORT_AND_HANDOVER.md; tests/IntegrationTests.cs, Run-IntegrationTests.ps1. Các sửa CRUD/Identity/menu/csproj và bản sửa cuối SalesOrders/InventoryReports/Layout vẫn giữ nguyên.
- Build Debug + MvcBuildViews=true thực sự exit 0 trước bản sửa cuối. DLL timestamp UTC 2026-10-15T21:09:14.341Z. Bản sửa cuối lúc 21:20 UTC (Complete action, đối chiếu tồn, CSS in) CHƯA build/test. Không coi toàn bộ working tree đã kiểm thử.
- Smoke cũ: 35 PASS thực chạy. SmokeTests.cs đã bổ sung ca Admin/Categories/Store sau lần chạy đó, CHƯA chạy lại bản mở rộng.
- Integration: lần đầu harness lỗi PowerShell InitialCatalog (không phải lỗi app/sandbox); đã sửa sang key Initial Catalog. Lần sau thực sự exit 0, TOTAL INTEGRATION PASS 28. DB tách biệt Dacn05_Test_194b22964f3f4d0989f62917ea62e521, giữ lại, không drop. Bao gồm migration/model compatibility, nhập nhiều dòng, trùng xác nhận, pending, giá snapshot, ngày kho, vượt tồn/rollback, hủy/hoàn, đối chiếu, RowVersion. Chưa HTTP/role/checkout/in A4/tải lớn.
- Sandbox: lần gọi mặc định lỗi trước tạo process: helper_unknown_error: setup refresh had errors. Các build/test thành công nói trên chạy ngoài sandbox theo tool approvals. Không phải lỗi biên dịch C#. Config người dùng có [windows] sandbox = elevated; repo trusted; không có .codex/config.toml trong repo. Không tìm thấy C:/Users/TonLeo/.codex/.sandbox/sandbox.log. Chưa xác định nguyên nhân sâu hơn và chưa xác nhận sandbox đã phục hồi.
- Tiếp tục sau khi môi trường được sửa: một lệnh chỉ đọc ngắn trong sandbox, không escalation; nếu đạt mới build snapshot hiện tại, chạy smoke mở rộng, rà soát admin/storefront qua HTTP/DB thử, cập nhật ma trận rồi commit/push đúng phạm vi. Không tự nâng Admin; câu hỏi chỉ định tài khoản Admin đầu tiên còn chờ.


## Lượt tiếp tục — kết quả riêng của working tree mới

- git status: không chạy được vì lỗi sandbox trước tạo process; một lần thử.
- Node Check-Source.mjs: audit cấu trúc thực chạy đạt, 121 source / 184 entries / 42 POST có token / UTF-8 hợp lệ.
- Store: đã rà source 4 guard thiếu UserId và token owner/product; chưa chạy MVC.
- Smoke C#: thêm 6 ca mới; chưa compile/chạy. Build, integration và HTTP/UI/in: chưa chạy lại.
- MODEL_CLASSES.mmd: 16 lớp/199 thuộc tính/21 navigation từ source; chưa render.
- Không commit/push. Không lấy 35 smoke/28 integration cũ làm bằng chứng cho code mới.
- Khi chạy được: xác minh 401 thiếu NameIdentifier và 400 khi đổi khách/sản phẩm sau mở checkout; không phát sinh Customer/SalesOrder/InventoryTransaction khi bị chặn.


## Kết quả sau thông báo CLI 0.163.0

| Hạng mục | Kết quả |
|---|---|
| exec_command workspace hiện tại | Lỗi setup refresh trước tạo process, một lần thử |
| Probe ghi trong sandbox | Chưa chạy do lỗi khởi tạo; chưa xác nhận write-mode |
| Ghi/đọc file qua Node filesystem | Thực hiện thành công; là tool khác, không xác nhận process sandbox |
| Đối chiếu Git index/blob chỉ đọc | 21 tracked file khác index; đã rà các diff nghiệp vụ; native status/diff chưa chạy |
| Check-Source.mjs qua Node | Đạt: 121 source, 184 entries, UTF-8 strict, 42 POST có token |
| Verify-Dacn05.ps1 | Đã viết/rà source; chưa chạy PowerShell |
| Build/Razor/smoke mở rộng/integration mới/HTTP | Chưa chạy trong lượt này |
| Commit/push | Không thực hiện; bản mới chưa nghiệm thu |

Runner mới tạo bản ghi JSON có hash source, stage lỗi và trạng thái NotRun/Passed/Failed; chỉ bản ghi của một lần chạy thực tế mới là bằng chứng. Thư mục artifacts/verification được ignore; không đưa config runtime/bin/database lên Git.

## Xác minh bàn giao trong phiên mới — 16/10/2026

- Sandbox mặc định: git status chạy được; tạo file probe tên GUID trong project, đọc đúng nội dung rồi xóa chính file đó thành công. Không sửa dữ liệu khác trong phép thử.
- Rà native Git diff, các Controller/View mới và cũ, Identity, model liên quan và csproj; không có file Model thay đổi. git diff --check đạt. Không cần sửa code trong phiên xác minh này.
- Đã đọc Verify-Dacn05.ps1 và cả hai script con trước khi chạy. Runner mặc định chỉ rebuild/test và ghi artifacts/bin/obj; không thao tác Git hoặc ghi DB. Integration tùy chọn có migration Up/seed/ghi dữ liệu trên DB mới tên GUID, kiểm tra prefix Dacn05_Test_ và Windows Authentication trước khi chạy; không drop DB hoặc chạy Down.
- powershell -NoProfile -ExecutionPolicy Bypass -File tests/Verify-Dacn05.ps1: exit 0; Rebuild Debug + MvcBuildViews=true đạt, 52 smoke PASS, SourceStable=true. Record cục bộ: artifacts/verification/verify-20261015-222944-182.json (ignored; Integration=NotRun trong record này vì được chạy riêng).
- Audit Node thực chạy checkSource: 121 source / 184 project entries / 42 POST có token / UTF-8 hợp lệ. Đây là audit cấu trúc, không thay thế HTTP.
- Run-IntegrationTests.ps1: lần trong sandbox thất bại kết nối SQL do SSPI (The target principal name is incorrect); không coi là lỗi logic ứng dụng. Lần chạy lại ngoài sandbox được phê duyệt: exit 0, TOTAL INTEGRATION PASS 28. DB thử Dacn05_Test_3cbfd79b114b43f2badf1f2c6f2e361a được giữ lại; không sửa DB ứng dụng. 133 hash source tiếp tục khớp bản build.
- Chưa kiểm chứng: HTTP login/logout/cookie/role, CRUD qua request thật, checkout thành công/truy cập đơn chéo owner qua HTTP, Complete qua HTTP/DB, cạnh tranh thực tế, UI/mobile/in A4/tải lớn; chưa chỉ định Admin đầu tiên, render Mermaid hoặc dàn trang DOCX/PPTX. 52 smoke/28 integration không chứng nhận các phần này.
- Chỉ bàn giao thay đổi đã có; không phát triển tính năng mới, merge master, reset, xóa thay đổi cũ hoặc migration DB thật. Kết quả mới này thay thế trạng thái chưa build/test của các checkpoint lịch sử bên trên, không phải nghiệm thu toàn bộ đồ án.
## Sửa menu, Dashboard và tiếng Việt — 16/10/2026

Nguyên nhân xác minh trước sửa:
- DB ứng dụng có 1 user, 0 AspNetRoles, 0 AspNetUserRoles. Register chỉ tạo user rồi sign-in, không có AddToRole; không có Admin đầu tiên. CanAccess ẩn các controller có role là đúng, không phải thiếu Controller/View.
- Home/Index là template ASP.NET mặc định, HomeController không truy vấn Dashboard.
- Source UTF-8 strict hợp lệ, Layout chứa đúng Quản lý bán hàng và meta charset=utf-8. GET IIS trước sửa trả text/html; charset=utf-8 nhưng body UTF-8 chứa Quáº£n; Encoding.Default của host là Windows-1252, Web.config chưa có fileEncoding. Sau cấu hình globalization UTF-8, cùng chuỗi Layout trả đúng qua HTTP. Không thay font hoặc chuyển mã hàng loạt.

Thay đổi:
- NavigationAccess kiểm tra Authorize của controller và Index, gồm Roles/Users/AllowAnonymous; Layout dùng helper chung. Giữ toàn bộ Authorize server.
- Đăng ký thường và external dùng IdentityProvisioning: tạo user + role KhachHang + membership (và external login nếu có) trong transaction; chỉ sign-in sau commit. Không tự gán Admin, không thay role tài khoản cũ.
- Initialize-FirstAdmin.ps1 mặc định PREVIEW; Apply cần câu xác nhận chính xác ID/email, kiểm tra lại target và chưa có Admin trong transaction Serializable, cập nhật SecurityStamp. Không có route web bootstrap, mật khẩu mặc định hoặc migration. Preview thực tế đạt, chưa Apply DB ứng dụng vì chưa có xác nhận chọn tài khoản.
- Dashboard truy vấn số liệu thật: đơn/chờ xác nhận/doanh thu Confirmed+Completed toàn thời gian; tồn và sắp hết. Admin thấy cả hai; Kho chỉ kho; BanHang chỉ bán hàng. KhachHang/anonymous nhận Welcome, không truy vấn số liệu quản trị. Có empty state và liên kết nghiệp vụ đúng quyền.
- globalization file/request/response/header UTF-8; giữ nguyên các chuỗi tiếng Việt đã đúng. Script PowerShell có tiếng Việt được lưu UTF-8 BOM để Windows PowerShell 5.1 đọc đúng.

Đã kiểm thử thực tế:
- Rebuild Debug + MvcBuildViews=true và 52 smoke đã đạt. Snapshot cuối được kiểm tra lại bằng Verify-Dacn05.ps1; xem record mới nhất trong artifacts/verification.
- Integration mở rộng: TOTAL INTEGRATION PASS 92, gồm các ca kho trước đó, đăng ký role KhachHang, bootstrap thiếu xác nhận/sai email/đã có Admin, menu bốn quyền và số liệu Dashboard đối chiếu DB.
- Run-UiTests.ps1: TOTAL HTTP PASS 122, exit 0 trên IIS Express bản sao và DB Dacn05_Test_8b5508f4cac74fec92b0fb0472178170 (giữ lại). Kiểm tra anonymous home; login/logout của bốn quyền; Dashboard/UTF-8/sidebar; GET trực tiếp 10 controller theo từng quyền; POST AdminUsers/SetRole thiếu token và có token nhưng role INVALID (không cấp quyền qua các POST này); đăng ký HTTP và truy vấn membership chỉ KhachHang.
- Bằng chứng HTML: artifacts/ui-verification/run-b3f233650d5d473bb65485013f363297 (ignored). Mật khẩu fixture sinh ngẫu nhiên, không commit/log. Harness tự dừng đúng IIS do nó tạo; không drop DB thử hoặc sửa DB ứng dụng.
- Các lượt HTTP ban đầu dừng vì lỗi harness (HTML entities, tham số string null làm GET thành POST, tên biến HOME reserved); đã sửa và chạy lại toàn bộ 122 ca đạt. Tool preview từng gặp LINQ ArrayIndex, đã sửa dùng biến scalar rồi chạy lại đạt. Không dùng kết quả lỗi làm chứng nhận.

Chưa kiểm chứng trong phạm vi này: external OAuth thực tế (chưa cấu hình provider), lockout/sai mật khẩu, cạnh tranh nhiều request đồng thời/cookie sau thay role, CRUD ghi qua HTTP toàn bộ phân hệ, checkout thành công/owner qua HTTP, Complete qua HTTP/DB, UI desktop/mobile bằng ảnh và in A4/tải lớn. Browser automation không có browser khả dụng. Các kiểm thử POST role ở trên xác minh authorization/anti-forgery/validation, không chứng nhận trọn luồng thay role quản trị. Không merge master, không migration DB thật; Admin đầu tiên trên DB ứng dụng vẫn chờ xác nhận.
- Snapshot cuối: artifacts/verification/verify-20261016-111304-673.json, Status/Build/Smoke=Passed, SourceStable=true, 52 smoke PASS. Truy vấn cuối DB ứng dụng vẫn 1 user / 0 role / 0 membership, xác nhận không cấp Admin hoặc tạo fixture ở DB thật.
