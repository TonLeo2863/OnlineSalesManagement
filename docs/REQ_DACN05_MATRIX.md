# Ma trận CNTT_DACN_05

Cập nhật 16/10/2026. Trạng thái dựa trên mức chứng minh; có source không đồng nghĩa chạy đúng. Chưa xác minh toàn bộ source/database. Không có ảnh minh chứng giả.

| Tiêu chí | Trạng thái | Source/bằng chứng | Cách kiểm thử còn cần |
|---|---|---|---|
| MVC5, EF6, Identity2, SQL Server | Build ban đầu đạt sau restore; DB chưa xác minh | OnlineSalesManagement.sln; OnlineSalesManagement/OnlineSalesManagement.csproj; packages.config | Build kèm Razor, kết nối DB chỉ đọc, đối chiếu migration |
| Bảo toàn code, nhánh riêng | Đã kiểm tra working tree sạch; tạo nhánh từ master cục bộ 877c60b | codex/dacn05-completion | So sánh origin/master trước tích hợp |
| Phân quyền GET/POST | Đã sửa giới hạn controller; chưa test HTTP/role, chưa có quản trị role | Controllers/{Products,Customers,Employees,Suppliers,PurchaseReceipts,SalesOrders,InventoryReports,Reports}Controller.cs | Anonymous, khách, Kho, BanHang, Admin truy cập trực tiếp cả GET/POST |
| Đăng ký/đăng nhập/đăng xuất | Có Identity; chưa test; lockout chưa bật | Controllers/AccountController.cs; App_Start/IdentityConfig.cs | Đúng/sai, logout, cookie, điều hướng và tài khoản không role |
| Quản trị người dùng/hoạt động | Chưa hoàn thiện | Models/IdentityModels.cs | Quản trị role, tránh mất Admin cuối, audit |
| Sản phẩm | Đã sửa overposting tồn/ngày tạo, SKU, FK, RowVersion và xóa có liên kết; chưa test DB | Controllers/ProductsController.cs; Views/Products | POST giả tồn kho, SKU trùng, FK sai, 2 tab cùng sửa, giao dịch kho đồng thời |
| Danh mục | Có model; chưa có CRUD controller trong danh sách kiểm tra | Models/Category.cs | CRUD, mã trùng, xóa danh mục có sản phẩm |
| Khách hàng/nhân viên/nhà cung cấp | Có CRUD; mới thêm chặn role, chưa rà soát toàn bộ validation | Controllers/{Customers,Employees,Suppliers}Controller.cs | Bắt buộc, mã trùng, FK và xóa liên kết |
| Nhập kho | Có service, chưa kiểm chứng đầy đủ | Services/PurchaseReceiptService.cs | Nháp không tăng; xác nhận tăng một lần; hủy; transaction/RowVersion; nhiều dòng |
| Xuất kho/bán hàng | Có service, chưa kiểm chứng đầy đủ | Services/SalesOrderService.cs | Pending không trừ; vượt tồn; xác nhận trùng; hủy hoàn tồn; giá server |
| Hóa đơn nội bộ | Có action/view; chưa rà soát và in thực tế | Controllers/SalesOrdersController.cs; Views/SalesOrders/Invoice.cshtml | Chặn Pending/Cancelled; số, khách, chi tiết, giảm giá, thanh toán, người lập, A4 |
| Kênh tại quầy/online | Có enum và chọn kênh; chưa thấy controller storefront trong kiểm kê | Models/Enums.cs; Controllers/SalesOrdersController.cs | Khách đặt hàng, giá server, chỉ xem đơn mình, không tự xác nhận |
| Tồn kho | Có controller/view; chưa test đối chiếu | Controllers/InventoryReportsController.cs | SKU, giá trị ước tính, cảnh báo, lọc, A4, tổng lịch sử = tồn |
| Doanh thu | Source lọc Confirmed/Completed theo OrderDate, tách kênh; chưa test | Controllers/ReportsController.cs: Index | Loại Pending/Cancelled, biên ngày, số 0, không dữ liệu; phân biệt tiền thu |
| Nhập/xuất theo ngày kho | Có báo cáo nhưng đang dùng ReceiptDate/OrderDate, cần sửa | Controllers/ReportsController.cs: Purchases/Sales | Dùng TransactionDate và giá giao dịch; hủy; lọc ngày khác ngày tạo; in |
| Migrations/concurrency | Giữ nguyên migrations và phân hệ ca trực; chưa đối chiếu model DB | Migrations; Models/OnlineSalesModelConfiguration.cs | __MigrationHistory, pending migration, schema; không tự áp dụng |
| Phân tích/thiết kế/khảo sát | Chưa hoàn thiện | docs cần bổ sung | Use case, lớp/ERD theo model thật; mẫu khảo sát ghi rõ giả định |
| Kiểm thử/UI | Build baseline đạt; chưa có kiểm thử tích hợp | docs/TEST_RESULTS.md | Role, CRUD, kho, online, invoice, báo cáo, responsive |
| Báo cáo/10 tuần/hướng dẫn/thuyết trình | Đang chuẩn bị hướng dẫn; còn thiếu tài liệu học thuật và dàn ý | docs/SETUP_AND_RUN.md | Đối chiếu đề cương, ảnh chụp thực tế, demo 15 phút |

## Ba ưu tiên đầu tiên

1. Ổn định build và chặn truy cập trái quyền (mới xử lý ranh giới controller, chưa hoàn thành quản lý người dùng).
2. Bảo vệ tồn kho khỏi CRUD sản phẩm và kiểm tra concurrency thực tế.
3. Sửa định nghĩa thời gian báo cáo nhập/xuất theo giao dịch kho.

Không phát triển thêm ca trực/snapshot trong đợt này. Chưa chạy migration hay sửa dữ liệu thật.

## Cập nhật báo cáo

Nhập/xuất đã sửa source dùng TransactionDate/QuantityChange/UnitPrice; chứng từ còn hiệu lực, loại hủy. Build Razor đạt, smoke 35 đạt; vẫn chưa kiểm thử tích hợp/report HTTP/in A4. Các trạng thái cũ trong bảng là mốc kiểm kê ban đầu.
