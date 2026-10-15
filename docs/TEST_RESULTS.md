# Kết quả và kịch bản kiểm thử

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