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
