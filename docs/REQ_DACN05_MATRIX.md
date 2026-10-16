# Ma trận CNTT_DACN_05 — trạng thái hiện tại

Mốc xác minh mới: sửa menu/Dashboard/UTF-8 ngày 16/10/2026. Build Debug + Razor, 52 smoke, 92 integration và 122 HTTP đạt trên DB thử riêng. DB ứng dụng chỉ đọc/preview, chưa cấp Admin khi chưa nhận xác nhận. Có source và test theo phạm vi không đồng nghĩa nghiệm thu toàn bộ đồ án; xem mục mới nhất trong TEST_RESULTS.md.

| Tiêu chí | Source chính | Trạng thái và bằng chứng | Cần làm để nghiệm thu |
|---|---|---|---|
| Build MVC5/.NET Framework 4.8.1 | .sln; .csproj; packages.config | Snapshot hiện tại Rebuild Debug + Razor đạt; 52 smoke PASS | Duy trì kiểm tra khi source thay đổi |
| Bảo toàn Git/dữ liệu | CODEX_PROGRESS.md | Nhánh codex/dacn05-completion; không reset/merge/drop | Git status/diff chính thức; stage có chọn lọc; chỉ push phần đạt |
| Đăng ký/đăng nhập/đăng xuất | AccountController; IdentityConfig; Startup.Auth | HTTP đăng ký chỉ KhachHang, login/logout bốn quyền đạt; registration atomic, có SecurityStamp | Sai mật khẩu/lockout, external OAuth, cookie sau thay role và đồng thời |
| Admin/quản trị người dùng | AdminUsersController; Views/AdminUsers | Bootstrap cục bộ có xác nhận ID/email, chỉ khi chưa có Admin; service guards và tool preview đạt; HTTP GET/POST validation theo quyền đạt | Chờ xác nhận Admin thật; trọn luồng đổi role/cookie/đồng thời và nhật ký quản trị |
| Quyền Kho/BanHang/khách | Controller Authorize; StoreController | Menu và GET trực tiếp 10 controller kiểm thử bốn quyền; POST role có/không token; giữ Authorize | POST nghiệp vụ khác, checkout/owner/CSRF qua HTTP |
| Sản phẩm | ProductsController; Views/Products | Chặn bind tồn/ngày tạo; SKU/FK/RowVersion; xóa có liên kết; source đã push mốc đầu | CRUD integration, xung đột form với giao dịch kho, lỗi đầu vào |
| Danh mục | CategoriesController; Views/Categories | Có CRUD, tên trùng và chặn xóa có sản phẩm; chưa nghiệm thu | Build/Razor và CRUD/role/FK thực tế |
| Khách/nhân viên/NCC | Customers/Employees/SuppliersController | Có validation mã trùng, chặn xóa liên kết; Customer giữ UserId/ngày tạo phía server | CRUD hợp lệ/sai; race duplicate/FK; dữ liệu không tồn tại |
| Nhập kho | PurchaseReceiptService; PurchaseReceiptsController | Integration hiện tại đạt nháp/nhiều dòng/xác nhận trùng/hủy/rollback | Chạy lại snapshot mới; HTTP và cạnh tranh đồng thời |
| Xuất kho/bán hàng | SalesOrderService; SalesOrdersController | Integration hiện tại đạt Pending/giá server/vượt tồn/xác nhận trùng/hủy hoàn; action Complete đã build, chưa test hành vi | Complete không động tồn, trạng thái sai, đồng thời, HTTP và chứng từ |
| Hóa đơn nội bộ | SalesOrdersController.Invoice; Invoice.cshtml | Có chặn trạng thái và xem/in; chưa in A4 thực tế | Đủ khách/người lập/giá/giảm giá/thanh toán; không gọi hóa đơn điện tử pháp lý |
| Online | StoreController; Views/Store | Có catalog/đặt một sản phẩm mỗi đơn/đơn riêng; guard UserId/token owner/product có 6 smoke đã đạt | Build; HTTP/CSRF/owner; giá/status không nhận client; replay/đổi tài khoản |
| Tồn kho | InventoryReportsController; Views/InventoryReports | Có tìm kiếm/cảnh báo/giá trị ước tính; đối chiếu ledger đã build và GET theo quyền đạt | Đối chiếu/in A4/empty/filter/data lớn |
| Doanh thu | ReportsController.Index | Theo OrderDate, Confirmed/Completed, tách Counter/Online; không khẳng định tiền thực thu | Kiểm thử tổng, ngày/tháng/khoảng, hủy/Pending/0/empty/data lớn |
| Báo cáo nhập/xuất | ReportsController.Purchases/Sales | TransactionDate/QuantityChange/UnitPrice; chỉ chứng từ còn hiệu lực. Integration hiện tại đạt mốc ngày/giá | Chạy lại; NCC filter còn thiếu; HTTP/in/large data; chứng từ hủy |
| Schema/migration/concurrency | Migrations; OnlineSalesModelConfiguration | DB cũ có 2 migration, SQL2022; DB thử hiện tại CompatibleWithModel/RowVersion đạt; giữ ca trực chuẩn bị | Không chạy migration DB thật; kiểm tra lại model khi build/test được |
| Phân tích/thiết kế/khảo sát | ANALYSIS_AND_DESIGN.md; MODEL_CLASSES.mmd | Có quy trình/use case/ERD/mẫu khảo sát; 16 model/199 property/21 navigation từ source | Render/duyệt Mermaid, khảo sát thực hoặc ghi rõ giả định; không bịa bằng chứng |
| Báo cáo/kế hoạch/hướng dẫn/demo | REPORT_AND_HANDOVER.md; SETUP_AND_RUN.md | Có nội dung Word Markdown, kế hoạch 10 tuần, hướng dẫn role, dàn ý 15 phút/checklist ảnh | Dàn trang theo mẫu khoa, chụp hình thật; chưa có DOCX/PPTX render |
| Dashboard và UTF-8 | HomeController; DashboardService; Welcome/Index; Web.config | Số liệu thật theo quyền; khách không nhận dữ liệu quản trị; HTTP UTF-8 trước/sau và bốn role đạt | Visual desktop/mobile, empty data/tải lớn |
| Kiểm thử và UI | tests; TEST_RESULTS.md | Audit Node 126 source/189 entries/42 POST; 52 smoke, 92 integration, 122 HTTP PASS | UI bằng ảnh/responsive/in/tải và các ca chưa chạy |

## Ưu tiên tiếp tục

1. Sandbox đã xác minh đọc/ghi/xóa probe thành công; không cần đổi cấu hình bảo mật.
2. Build toàn bộ working tree + Razor và 52 smoke đã đạt; 92 integration và 122 HTTP PASS. Tiếp tục các điều kiện nghiệm thu HTTP/UI còn mở.
3. HTTP role GET/menu và POST role đã đạt; còn owner/checkout/Complete và CRUD ghi. Chỉ cấp Admin đầu tiên sau xác nhận cụ thể.
4. Hoàn thiện lọc NCC, báo cáo/tải/in và ảnh minh chứng theo kết quả thật; chỉ commit/push phần đạt.

Không phát triển thêm ca trực, không đổi framework/namespace, không xóa migration hoặc database.
