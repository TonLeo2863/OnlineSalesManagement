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

## Chạy smoke test

Sau build: powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests\Run-SmokeTests.ps1 (chỉ tiến trình này, không sửa policy máy). Không truy vấn/ghi database. Nhập/xuất hiện đã sửa source dùng ngày giao dịch kho và giá snapshot; vẫn cần kiểm chứng HTTP/SQL trước dùng làm số liệu bàn giao.


## Khôi phục thực thi Codex Windows

Cấu hình người dùng hiện dùng [windows] sandbox = "elevated". Lỗi setup refresh had errors xảy ra trước process, không phải lỗi build. Dừng retry/escalation lặp lại. Theo tài liệu https://learn.chatgpt.com/docs/windows/windows-sandbox: đóng/mở lại Codex; nếu cần, dùng PowerShell Administrator để provision lại elevated sandbox với `codex sandbox setup --elevated --user "DOMAIN\user" --codex-home "C:\Users\user\.codex"`, thay đúng tài khoản thực tế. Nếu chính sách máy cho phép và setup vẫn thất bại, có thể chọn [windows] sandbox = "unelevated" làm fallback tạm, với cô lập mạng yếu hơn. Không xóa .codex hoặc thay ACL hàng loạt. Sau khởi động lại, kiểm tra duy nhất một lệnh chỉ đọc trong sandbox trước build/test; không coi lệnh ngoài sandbox là bằng chứng sandbox đã sửa. Giữ nguyên code và database.


## Audit cấu trúc độc lập MSBuild

Nếu Node.js có sẵn, tại repository:

~~~powershell
node --input-type=module -e "import { checkSource } from './tests/Check-Source.mjs'; console.log(await checkSource(process.cwd()));"
~~~

Checker chỉ đọc source/csproj, kiểm tra UTF-8 và POST nhận diện được; không build C#, chạy IIS hoặc kết nối SQL. Lượt này đã chạy checker bằng Node tool không gọi shell. Không tự thay quyền Windows hoặc chạy tiến trình qua công cụ khác để vượt sandbox.

Code mới phải build/test lại sau khi shell phục hồi, đặc biệt guard thiếu UserId và token owner/product. Chưa commit/push bản chưa kiểm chứng. Database thử cũ giữ nguyên.


## Kiểm tra cùng một snapshot source

Sau khi môi trường thực thi và ghi workspace hoạt động, trong Developer PowerShell tại repository:

~~~powershell
& .	estsVerify-Dacn05.ps1
& .	estsVerify-Dacn05.ps1 -IncludeIntegration
~~~

Không cần chạy cả hai nếu đã chọn chạy trực tiếp bản có integration. Runner không thay đổi ExecutionPolicy; dùng môi trường được phép chạy script theo chính sách máy hiện có. Nếu bị chặn, ghi lỗi và xử lý theo chính sách quản trị, không tự thay bảo mật Windows.

Runner Rebuild Debug/Razor trước test; dừng ngay khi build thất bại để tránh chạy DLL cũ. Integration tạo DB mới Dacn05_Test_<GUID>, chỉ chạy migration Up trên DB thử tách biệt và giữ DB lại. Không sửa/drop DB thật. Kết quả/hash source lưu artifacts/verification/*.json, thư mục bị ignore; giữ log locally làm bằng chứng. Runner chưa chạy trong lượt công cụ exec bị lỗi; không coi file script là kết quả kiểm thử.

## Menu, Dashboard và UTF-8 — bản sửa 16/10/2026

Menu đọc cùng các điều kiện Authorize của Controller và action Index qua NavigationAccess; không thay thế phân quyền server. Admin có toàn bộ menu; Kho có sản phẩm/danh mục/NCC/nhập/tồn; BanHang có khách hàng/đơn hàng; KhachHang và tài khoản cũ chưa có role chỉ có mua hàng/tổng quan. Đăng ký thường và đăng ký ngoài gán KhachHang trong cùng transaction tạo user; không cấp Admin từ form đăng ký.

Dashboard Admin hiển thị bán hàng và kho; Kho chỉ có kho; BanHang chỉ có bán hàng. Khách/anonymous nhận Welcome trước khi truy vấn dữ liệu quản trị. Doanh thu là tổng TotalAmount của đơn Confirmed/Completed toàn thời gian theo trạng thái hiện tại, không phải tiền đã thu. Tổng tồn cộng số lượng mọi sản phẩm (có thể khác đơn vị); sắp hết là sản phẩm active có tồn <= ReorderLevel. Không hardcode số liệu.

Khởi tạo Admin đầu tiên chỉ dành cho người vận hành có quyền chạy công cụ cục bộ và kết nối DB. Không có URL bootstrap, mật khẩu mặc định hay tự chọn tài khoản đầu tiên. Chạy preview trước, thay đúng ID/email đã được người quản trị xác nhận:

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests/Initialize-FirstAdmin.ps1 -UserId '<ID-da-xac-nhan>' -Email '<email-da-xac-nhan>'
# Chỉ sau khi kiểm tra target/database và nhận xác nhận của người quản trị:
powershell -NoProfile -ExecutionPolicy Bypass -File tests/Initialize-FirstAdmin.ps1 -UserId '<ID-da-xac-nhan>' -Email '<email-da-xac-nhan>' -Apply
~~~

- Preview không ghi DB. Apply còn yêu cầu nhập đúng câu GRANT ADMIN <ID> <email>; nhập khác sẽ dừng.
- Kiểm tra lại ID/email và không có membership Admin trong transaction Serializable; nếu đã có Admin, dùng AdminUsers thay vì chạy bootstrap lần nữa.
- Chỉ thêm Admin cho đúng tài khoản, cập nhật SecurityStamp, không tạo user/mật khẩu, không sửa migration. Đăng xuất/đăng nhập lại sau khi cấp quyền.
- Tài khoản có trước bản sửa không được tự động nâng quyền hoặc gán role hàng loạt. Chưa thực hiện Apply trên DB ứng dụng khi chưa được xác nhận cụ thể.

Web.config khai báo globalization fileEncoding/requestEncoding/responseEncoding/responseHeaderEncoding = utf-8. Giữ meta charset=utf-8. File nguồn đã đúng UTF-8, không chuyển lại từ chuỗi mojibake hay đổi font. Host hiện có Encoding.Default Windows-1252; view UTF-8 không BOM từng bị đọc theo encoding host rồi xuất ra response UTF-8. HTTP trước sửa đã có charset=utf-8 nhưng bytes chứa Quáº£n; sau khai báo fileEncoding đúng, cùng chữ trong Layout hiển thị đúng.

Kiểm thử riêng website/DB thử (cần MSBuild, SQL Windows Authentication và IIS Express):

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests/Verify-Dacn05.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tests/Run-UiTests.ps1
~~~

Run-UiTests tạo DB Dacn05_Test_<GUID> mới qua runner integration, tạo tài khoản thử cho bốn quyền, sao chép website vào artifacts/ui-verification/run-<GUID>/site, thay connection string chỉ trong bản sao rồi chạy IIS Express localhost:64265. Mật khẩu thử ngẫu nhiên chỉ tồn tại trong tiến trình, không đưa vào source/log; DB thử được giữ lại, IIS do script tạo được dừng trong finally. Không chạy đồng thời nhiều harness cùng port hoặc rebuild khi harness đang sao chép/chạy. HTML bằng chứng nằm trong artifacts (ignored); không dùng các tài khoản thử trên DB thật.