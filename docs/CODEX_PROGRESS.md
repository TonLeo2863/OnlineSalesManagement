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
