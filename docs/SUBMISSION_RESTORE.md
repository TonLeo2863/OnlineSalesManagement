# Khôi phục tối giản — bản nộp 22/10/2026

1. Cài Visual Studio 2022: workload ASP.NET and web development, .NET Framework **4.8.1 Developer/Targeting Pack**. Cài SQL Server 2022 và SSMS; Node.js chỉ cần khi chạy test JavaScript.
2. Giải nén source, mở OnlineSalesManagement.sln. Chọn Restore NuGet Packages hoặc dùng Developer PowerShell:

~~~powershell
MSBuild.exe OnlineSalesManagement.sln /t:Restore /p:RestorePackagesConfig=true
MSBuild.exe OnlineSalesManagement.sln /t:Build /p:Configuration=Debug /p:MvcBuildViews=true
~~~

3. Chỉ trên máy nhận: đặt DefaultConnection trong OnlineSalesManagement/Web.config tới SQL Server 2022 và **DB demo riêng**. Mẫu dùng Data Source=localhost;Initial Catalog=OnlineSalesDemo_HUIT;Integrated Security=True;MultipleActiveResultSets=True. Nếu dùng instance SQLEXPRESS thì thay localhost bằng localhost\SQLEXPRESS. Không đưa cấu hình cá nhân lên Git.
4. Gói source **không chứa DB/backup**, vì vậy không có dữ liệu và tài khoản demo sẵn. Nếu có backup được người sở hữu duyệt thì restore sang DB demo riêng. Nếu cần DB demo mới, người nhận tự rà soát migration rồi mới chạy Update-Database -Verbose trong Package Manager Console cho project OnlineSalesManagement, chỉ khi đã xác nhận DB đích là DB demo mới. Người đóng gói không chạy migration hay ghi DB thật trong lúc chốt bản.
5. Chọn OnlineSalesManagement làm Startup Project, chạy IIS Express. Tạo tài khoản khách qua Register. Không có mật khẩu Admin mặc định. Nếu DB mới chưa có Admin, dùng quy trình Preview/Apply đã xác nhận trong tests/Initialize-FirstAdmin.ps1 và tài khoản/email chính xác; không dùng công cụ này trên DB thật hoặc DB đã có Admin.
6. Dữ liệu thêm 16 nhóm/64 sản phẩm chỉ là DEMO: tools/Invoke-DemoCatalog.ps1 mặc định Preview, Apply bị giới hạn DB demo/test. Không ghi thẳng QuantityInStock; tồn demo phải qua phiếu nhập và Service hiện có.

Các bài test ghi dữ liệu tạo DB Dacn05_Test_* hoặc AdminReset_Test_* riêng. Cần Windows Authentication có quyền trên SQL Server thử. Không đổi connection sang DB thật để chạy test.

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests/Verify-Dacn05.ps1 -IncludeIntegration
powershell -NoProfile -ExecutionPolicy Bypass -File tests/Run-UiTests.ps1
node tests/AdminUi.Tests.mjs
node tests/MegaMenu.Tests.mjs
~~~

Bộ Browser-Handover.mjs được giữ từ commit tài liệu/test trước; không phải bằng chứng Chrome UI đã chạy cho giao diện mới. Không dùng kết quả cũ để chứng nhận snapshot hiện tại. Xem FINAL_TEST_SUMMARY_20261022.md.
