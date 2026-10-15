# Cài đặt và chạy

## Môi trường đã phát hiện

Windows; Visual Studio 2022 Community; MSBuild tại `C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe`; có dotnet và sqlcmd. SQL Server instance SQLEXPRESS đang Running (chưa xác minh phiên bản và database dự án).

Project hiện target **.NET Framework 4.8.1**, MVC 5.2.9, EF 6.5.1, Identity 2.2.4. Không đổi framework. Cần workload ASP.NET và targeting pack 4.8.1.

## Khôi phục và build

Trong Developer PowerShell for VS 2022 tại thư mục solution:

```powershell
MSBuild.exe OnlineSalesManagement.sln /t:Restore /p:RestorePackagesConfig=true
MSBuild.exe OnlineSalesManagement.sln /t:Build /p:Configuration=Debug
MSBuild.exe OnlineSalesManagement.sln /t:Build /p:Configuration=Debug /p:MvcBuildViews=true
```

Mở solution trong VS, chọn web project làm startup, chạy IIS Express. Kiểm tra kết quả thật theo TEST_RESULTS.md; build không chứng minh database hoặc nghiệp vụ hoạt động.

## Database và migrations

Kiểm tra DefaultConnection trong Web.config tại máy, tránh sao chép secrets vào tài liệu/Git. Chưa áp dụng migration trong phiên này. Trước Update-Database: xác định đúng server/database, backup theo quy trình, xem Get-Migrations và bảng __MigrationHistory bằng truy vấn chỉ đọc; so model với migration; xuất script và rà soát Up/Down. Không bật automatic migrations, drop database, xóa migration hoặc lịch sử. Phân hệ ca trực/snapshot giữ nguyên trạng thái chuẩn bị.

Seed hiện gọi SampleDataSeeder trong Migrations/Configuration.cs; cần rà soát trước khi chạy Update-Database, không tự nạp dữ liệu vào DB thật.

## Vai trò hiện áp dụng

Tên Identity role chuẩn trong code: `Admin`, `Kho`, `BanHang`; vai trò khách dự kiến `KhachHang` (chưa có luồng mua hàng riêng).

| Controller | Role được phép |
|---|---|
| Products, Suppliers, PurchaseReceipts, InventoryReports | Admin, Kho |
| Customers, SalesOrders | Admin, BanHang |
| Employees, Reports | Admin |

Hiện chưa có UI cấp role/bootstrap Admin; không tự tạo tài khoản/mật khẩu cố định. Dùng tài khoản quản trị đã được người sở hữu xác minh và ASP.NET Identity UserManager/RoleManager để cấp role trong môi trường phù hợp. Tài khoản cũ chưa có role sẽ bị từ chối; đây là phần cần hoàn thiện trước bàn giao demo. Không sửa DB trực tiếp hoặc tự nâng quyền tài khoản hiện có trong phiên này.

Kho tạo phiếu nháp rồi xác nhận; bán hàng tạo đơn Pending rồi xác nhận; chỉ thao tác qua dịch vụ/chứng từ để thay đổi tồn. Sản phẩm mới có tồn bằng 0. Admin kiểm tra báo cáo; báo cáo nhập/xuất hiện còn sai mốc ngày kho, chưa dùng làm số liệu bàn giao.
