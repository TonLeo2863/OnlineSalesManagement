# Khôi phục project ngày 30/10/2026

## Nguyên nhân và nguồn khôi phục

- Local master ở `877c60b`; remote master kiểm tra trực tiếp bằng `git ls-remote` ở `15b13a8649817781a66b790d4ab5bd5e5218a4a5`, cách 11 commit và có thể fast-forward. Các nhánh feature/management-upgrades, feature/management-upgrades-20261030 và codex/dacn05-completion cùng ở 15b13a8.
- Reflog ghi chuyển từ nhánh nâng cấp sang master lúc 09:39 ngày 30/10. Stash `1028d0e25a2a146b822758d64a89310427c33ccb` có parent 15b13a8 và thông điệp “Checking out master”. Marker “Updated upstream / Stashed changes” và index stage 1/2/3 cho thấy áp stash của nền mới lên master cũ gây xung đột. Không có MERGE_HEAD: không có merge cần abort.
- 23 unmerged paths gồm 5 both-modified và 18 deleted-by-us. Các file deleted-by-us có trong nền mới nhưng chưa tồn tại trên master cũ.
- Đối chiếu toàn bộ tree stash với working tree. Các khác biệt không có marker trong AccountController, CustomersController, SuppliersController, SalesOrderService và csproj là phần chức năng của nền mới bị thiếu khi áp lên master cũ. Khôi phục nền mới cộng nội dung nâng cấp trong stash, giữ các file untracked thực tế.

## Bảo toàn và trạng thái Git

- Bản sao trước sửa: `C:\Users\TonLeo\AppData\Local\Temp\OnlineSalesRecovery-20261030-115645`, gồm source, file mới, toàn bộ `.git` (index, objects, refs, logs), status và danh sách unmerged. Không bao gồm packages/artifacts/.vs có thể tái tạo.
- Không abort/reset-hard/clean, không xóa stash. Sau khi sao lưu, dùng `git read-tree 15b13a8` để đưa index về nền mới, giữ working tree đã phục hồi; chuyển sang `feature/management-upgrades`.
- Index không còn unmerged và không có thay đổi staged. Thay đổi ứng dụng vẫn chưa commit; các file mới vẫn untracked. Local master giữ nguyên 877c60b. Không commit, push, tạo merge commit hoặc thay đổi remote.
- Web.config gốc giữ nguyên từng byte (đối chiếu SHA256 trước/sau). Không chạy ứng dụng/test trực tiếp trên OnlineSalesDb, không chạy migration/restore/reset lên DB thật.
- Kiểm tra 49 file untracked ban đầu: 47 file trùng từng byte; 2 partial Razor hủy đơn chỉ thêm UTF-8 BOM, nội dung giữ nguyên. Không thiếu file nào từ snapshot.

## Source đã phục hồi

Sáu file gây CS2001, lấy từ nền source mới có trong stash/15b13a8:

- `OnlineSalesManagement/Services/DemoCatalog.cs`
- `OnlineSalesManagement/Services/DashboardService.cs`
- `OnlineSalesManagement/ViewModels/DashboardViewModel.cs`
- `OnlineSalesManagement/Services/NavigationAccess.cs`
- `OnlineSalesManagement/Controllers/CategoriesController.cs`
- `OnlineSalesManagement/Services/StorePresentation.cs`

Đồng thời khôi phục controller, view, CSS/JS, tài liệu và test của 11 commit mới; giải quyết đủ 23 đường dẫn xung đột. csproj khôi phục đúng tham chiếu Facebook và đăng ký ReorderCart, CatalogImport, AccountGovernance, migration, các view/JS mới. Audit xác nhận 161 file source, 229 Compile/Content entries và 51 POST actions; không có project item thiếu hoặc trùng.

Tổng cộng 55 file của tree nguồn mới không có trong working tree ban đầu đã được khôi phục; danh sách đầy đủ tại `artifacts/recovery/restored-missing-files.txt`.

Các chức năng được giữ: AccountGovernance và audit/migration; nhập Excel với preview; tìm kiếm và lọc nâng cao; lịch sử giao dịch; mua lại; giỏ hàng; Admin xem trước; khách hủy Pending; giao diện, role guard, chống CSRF, bảo vệ tồn kho và các test liên quan.

## Sửa encoding không đổi Web.config

HTTP test phát hiện Web.config máy thiếu globalization/fileEncoding UTF-8 có trong bản nâng cấp. IIS đọc các Razor view UTF-8 không BOM bằng encoding máy, gây chữ Việt sai. Thêm UTF-8 BOM vào 23 view thiếu BOM, không đổi nội dung. Danh sách tại `artifacts/recovery/razor-utf8-files.txt`. Hai file untracked chịu thay đổi encoding này là `_CancelPending.cshtml` và `_CancelPendingDialog.cshtml`.

Lần HTTP tiếp theo xác nhận UTF-8 đúng, rồi phát hiện định dạng tiền theo vùng Windows thay vì vi-VN. Bổ sung `Application_BeginRequest` trong `Global.asax.cs` đặt CurrentCulture/CurrentUICulture thành vi-VN, tương đương culture của cấu hình nâng cấp, không chỉnh Web.config máy. Không đổi phép tính tổng giỏ hàng.

## Kiểm tra

- NuGet Restore bằng MSBuild `/t:Restore /p:RestorePackagesConfig=true`: exit 0 sau khi cho phép truy cập NuGet.
- Rebuild Debug + Razor: `/t:Rebuild /p:Configuration=Debug /p:MvcBuildViews=true`.
- Smoke: 64 PASS.
- Node UI tests: 17 tests PASS (gồm các file test dạng script có assertion riêng).
- Offline deployment target guards: 2 PASS.
- Integration lần đầu: 316 PASS trên DB `Dacn05_Test_<GUID>` riêng. HTTP lần đầu trả 200 nhưng thất bại assertion encoding, đã xác định và sửa bằng BOM như trên.
- Log build và HTTP tại `artifacts/recovery/`. Kết quả chạy cuối bổ sung bên dưới.
- `git diff --check` chỉ báo trailing whitespace vốn có trong Web.config máy; giữ nguyên theo yêu cầu.

## Chạy IIS Express

1. Mở `OnlineSalesManagement.sln` ngay tại thư mục repository này, trên nhánh `feature/management-upgrades`.
2. Chọn OnlineSalesManagement làm Startup Project; Configuration Debug, Any CPU; chọn IIS Express.
3. Build rồi Ctrl+F5/F5. Giữ Web.config hiện tại và dùng tài khoản thực có sẵn; không chạy Update-Database, seed hoặc công cụ Apply để khởi động.
4. Để chạy HTTP test an toàn: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/Run-UiTests.ps1`. Runner tạo DB Dacn05_Test_GUID và bản sao site riêng, chạy IIS Express ở http://localhost:64265, rồi dừng IIS; DB test được giữ lại.

Không dùng checkout master cũ với các thay đổi này. Chờ chủ repository xác nhận trước commit/push hoặc tích hợp nhánh.

## Xác minh cuối sau sửa encoding/culture

- Rebuild Debug + Razor cuối: exit 0, 0 warnings, 0 errors (`artifacts/recovery/rebuild-final.log`).
- Audit source cuối: 161 source files, 229 project entries, 51 POST actions; UTF-8 hợp lệ (`artifacts/recovery/source-audit.txt`).
- Integration cuối: 316 PASS; HTTP dùng cùng runner ở `artifacts/recovery/ui-tests-verified.log`.
- HTTP cuối: 265 PASS, runner exit 0; xác nhận Excel, lọc nâng cao, account governance, preview, giỏ hàng, mua lại và hủy Pending qua IIS Express. DB riêng được giữ lại: `Dacn05_Test_4c19134bfc374a46b9fe4d4b620fa20a`. Evidence: `artifacts/ui-verification/run-44b0add16bb84ed6b5f29a7f8ed3dcd0`.
