# Ma trận CNTT_DACN_05 — trạng thái hiện tại

Mốc xác minh mới: 16/10/2026, phiên CLI 0.163.0: sandbox ghi/đọc/xóa probe đạt; Rebuild Debug + Razor đạt; 52 smoke và 28 integration đạt trên source hiện tại. Integration chạy riêng ngoài sandbox sau lỗi SSPI trong sandbox, chỉ trên DB thử GUID. Các ghi chú snapshot cũ bên dưới là lịch sử; HTTP/UI/in và các điều kiện nghiệm thu chưa chạy vẫn còn mở. Xem TEST_RESULTS.md, mục xác minh bàn giao mới nhất.

| Tiêu chí | Source chính | Trạng thái và bằng chứng | Cần làm để nghiệm thu |
|---|---|---|---|
| Build MVC5/.NET Framework 4.8.1 | .sln; .csproj; packages.config | Snapshot hiện tại Rebuild Debug + Razor đạt; 52 smoke PASS | Duy trì kiểm tra khi source thay đổi |
| Bảo toàn Git/dữ liệu | CODEX_PROGRESS.md | Nhánh codex/dacn05-completion; không reset/merge/drop | Git status/diff chính thức; stage có chọn lọc; chỉ push phần đạt |
| Đăng ký/đăng nhập/đăng xuất | AccountController; IdentityConfig; Startup.Auth | Có Identity2; code bật lockout và kiểm tra SecurityStamp mỗi request; chưa test HTTP | Login đúng/sai, logout, cookie/lockout, điều hướng, role GET/POST |
| Admin/quản trị người dùng | AdminUsersController; Views/AdminUsers | Có xem user/cấp một role chính, chặn gỡ chính mình/last Admin; chưa cấp Admin đầu tiên và chưa test HTTP | Chỉ định Admin qua quy trình được phép, kiểm thử đồng thời/cookie; nhật ký role toàn diện còn thiếu |
| Quyền Kho/BanHang/khách | Controller Authorize; StoreController | Có role server; khách xem/đặt riêng; 42 POST có token theo audit cấu trúc | Kiểm thử HTTP từng role và truy cập URL trực tiếp; metadata không thay HTTP |
| Sản phẩm | ProductsController; Views/Products | Chặn bind tồn/ngày tạo; SKU/FK/RowVersion; xóa có liên kết; source đã push mốc đầu | CRUD integration, xung đột form với giao dịch kho, lỗi đầu vào |
| Danh mục | CategoriesController; Views/Categories | Có CRUD, tên trùng và chặn xóa có sản phẩm; chưa nghiệm thu | Build/Razor và CRUD/role/FK thực tế |
| Khách/nhân viên/NCC | Customers/Employees/SuppliersController | Có validation mã trùng, chặn xóa liên kết; Customer giữ UserId/ngày tạo phía server | CRUD hợp lệ/sai; race duplicate/FK; dữ liệu không tồn tại |
| Nhập kho | PurchaseReceiptService; PurchaseReceiptsController | Snapshot cũ integration đạt nháp/nhiều dòng/xác nhận trùng/hủy/rollback | Chạy lại snapshot mới; HTTP và cạnh tranh đồng thời |
| Xuất kho/bán hàng | SalesOrderService; SalesOrdersController | Snapshot cũ integration đạt Pending/giá server/vượt tồn/xác nhận trùng/hủy hoàn; action Complete mới chưa build | Complete không động tồn, trạng thái sai, đồng thời, HTTP và chứng từ |
| Hóa đơn nội bộ | SalesOrdersController.Invoice; Invoice.cshtml | Có chặn trạng thái và xem/in; chưa in A4 thực tế | Đủ khách/người lập/giá/giảm giá/thanh toán; không gọi hóa đơn điện tử pháp lý |
| Online | StoreController; Views/Store | Có catalog/đặt một sản phẩm mỗi đơn/đơn riêng; guard UserId và token owner/product mới; 6 ca mới chưa chạy | Build; HTTP/CSRF/owner; giá/status không nhận client; replay/đổi tài khoản |
| Tồn kho | InventoryReportsController; Views/InventoryReports | Có tìm kiếm/cảnh báo/giá trị ước tính; đối chiếu ledger mới chưa build | Đối chiếu/in A4/empty/filter/data lớn |
| Doanh thu | ReportsController.Index | Theo OrderDate, Confirmed/Completed, tách Counter/Online; không khẳng định tiền thực thu | Kiểm thử tổng, ngày/tháng/khoảng, hủy/Pending/0/empty/data lớn |
| Báo cáo nhập/xuất | ReportsController.Purchases/Sales | TransactionDate/QuantityChange/UnitPrice; chỉ chứng từ còn hiệu lực. Snapshot cũ integration đạt mốc ngày/giá | Chạy lại; NCC filter còn thiếu; HTTP/in/large data; chứng từ hủy |
| Schema/migration/concurrency | Migrations; OnlineSalesModelConfiguration | DB cũ có 2 migration, SQL2022; DB thử snapshot cũ CompatibleWithModel đạt; giữ ca trực chuẩn bị | Không chạy migration DB thật; kiểm tra lại model khi build/test được |
| Phân tích/thiết kế/khảo sát | ANALYSIS_AND_DESIGN.md; MODEL_CLASSES.mmd | Có quy trình/use case/ERD/mẫu khảo sát; 16 model/199 property/21 navigation từ source | Render/duyệt Mermaid, khảo sát thực hoặc ghi rõ giả định; không bịa bằng chứng |
| Báo cáo/kế hoạch/hướng dẫn/demo | REPORT_AND_HANDOVER.md; SETUP_AND_RUN.md | Có nội dung Word Markdown, kế hoạch 10 tuần, hướng dẫn role, dàn ý 15 phút/checklist ảnh | Dàn trang theo mẫu khoa, chụp hình thật; chưa có DOCX/PPTX render |
| Kiểm thử và UI | tests; TEST_RESULTS.md | Source audit Node đạt 121 source/184 entries/42 POST; snapshot cũ 35 smoke/28 integration | Chạy runner mới; HTTP/UI/responsive/in/tải và các ca chưa chạy |

## Ưu tiên tiếp tục

1. Sandbox đã xác minh đọc/ghi/xóa probe thành công; không cần đổi cấu hình bảo mật.
2. Build toàn bộ working tree + Razor và 52 smoke đã đạt; integration riêng 28 PASS. Tiếp tục các điều kiện nghiệm thu HTTP/UI còn mở.
3. Integration trên DB thử riêng; HTTP role/owner/checkout/Complete và CRUD. Không tự nâng quyền Admin.
4. Hoàn thiện lọc NCC, báo cáo/tải/in và ảnh minh chứng theo kết quả thật; chỉ commit/push phần đạt.

Không phát triển thêm ca trực, không đổi framework/namespace, không xóa migration hoặc database.
