# Mega menu, dữ liệu demo và thẻ vai trò

## Source đã đọc và phạm vi giữ nguyên

Đọc trực tiếp controller Store/AdminUsers, view cửa hàng/layout, ShoppingCart, StorePresentation, model Product/Category, PurchaseReceiptService và các test hiện có trong workspace. Các thay đổi chưa push được giữ lại. Không sửa handler giỏ hàng/đặt hàng/đơn hàng, customer.js, toast cửa hàng, _CustomerLayout, _Layout, Web.config gốc, schema, migrations hoặc logic SetRole.

Không có ảnh MemoryZone đính kèm trong ngữ cảnh hiện tại; thiết kế dựa trên mô tả tương tác. Không sao chép logo, CSS hay ảnh quảng cáo bên ngoài.

## Chức năng

- Menu danh mục bên trái lấy từ Categories đang hoạt động; desktop mở panel bên phải bằng hover/click, cùng vùng DOM để di chuyển sang panel không bị đóng; đóng khi rời vùng, focus ra ngoài, click ngoài, Escape. ArrowUp/Down chuyển danh mục; ArrowRight vào liên kết, ArrowLeft/Escape trả focus về nút. Enter/Space dùng hành vi button chuẩn. Mobile dưới 992px dùng accordion.
- Schema không có cây danh mục nên không tạo danh mục con giả. Panel chia cột khám phá danh mục, gợi ý sản phẩm và sản phẩm khác. Các liên kết sản phẩm dùng categoryId + SKU; Store.Index lọc categoryId trước khi tìm kiếm q. Giữ giới hạn 200 sản phẩm, lọc sản phẩm/danh mục active, giỏ hàng và modal sản phẩm cũ.
- AdminUsers: 4 cột từ 1200px, 2 cột tablet, 1 cột dưới 576px; card cao bằng nhau, avatar tròn, email, badge, chọn quyền và nút trong card. Tìm theo email/quyền hiện tại (không tìm text trong các option). Modal nêu tài khoản, quyền cũ/mới; Hủy/Escape không submit; toast Bootstrap có nút đóng. Vẫn dùng anti-forgery và SetRole cũ.
- Thứ tự hiển thị: Admin, Kho, BanHang, KhachHang, vai trò lạ, không có vai trò; nhiều vai trò lấy ưu tiên hiển thị cao nhất; trong nhóm email A–Z. Đây không phải cơ chế kế thừa quyền.

## Preview trên OnlineSalesDb — chỉ đọc

Ngày 22/10/2026: hiện có **4 danh mục, 9 sản phẩm**. Bộ demo có **16 tên danh mục, 64 sản phẩm OSDEMO-xx-xx**. Dự kiến **thêm 13 danh mục + 64 sản phẩm**, tái sử dụng 3 danh mục trùng tên, **0 SKU trùng**. Sau Apply giả định sẽ là **17 danh mục, 73 sản phẩm**; giữ danh mục cũ ngoài bộ demo để không mất dữ liệu. Số lượng có thể thay đổi nếu dữ liệu thực tế được sửa sau Preview; phải chạy lại ngay trước quyết định Apply.

Mỗi sản phẩm có tên tiếng Việt, SKU, mô tả ghi rõ minh họa, đơn vị, giá nhập/bán giả định, mức cảnh báo 3, trạng thái active, ảnh `~/Content/product-placeholder.svg` có sẵn trong source (placeholder đồ án, không dùng ảnh thương mại từ bên ngoài). Giá không được trình bày là giá thị trường. Tồn ban đầu bằng 0 mặc định; không gán QuantityInStock hay tạo ledger trong seed.

Preview tắt EF initializer, không tạo DB, không chạy migration và không ghi bất kỳ bản ghi nào. Apply chỉ cho phép DB hiện hữu có tên OnlineSalesDemo_* hoặc Dacn05_Test_* và yêu cầu switch xác nhận. Database thật bị chặn cả ở PowerShell lẫn C#. Không cập nhật sản phẩm/danh mục đã có theo SKU/tên; không tác động Identity, đơn hàng, khách hàng hay tài khoản Admin.

## Lệnh sử dụng

Build trước:

```powershell
msbuild OnlineSalesManagement.sln /p:Configuration=Debug /p:MvcBuildViews=true /verbosity:minimal /nologo
```

Preview database hiện tại (read-only, dùng Windows authentication theo cấu hình máy, không sửa Web.config):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Invoke-DemoCatalog.ps1 -Mode Preview
```

Preview/Apply trên DB thử đã có schema:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Invoke-DemoCatalog.ps1 -Mode Preview -Database Dacn05_Test_TEN_DB_THU
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Invoke-DemoCatalog.ps1 -Mode Apply -Database Dacn05_Test_TEN_DB_THU -ConfirmDemoApply
```

Không truyền OnlineSalesDb vào Apply; command chủ động từ chối. Việc Apply thật sẽ cần bạn duyệt Preview mới, đồng ý 13 danh mục/64 sản phẩm, tên danh mục và giá demo; sau đó mới chuẩn bị lệnh riêng có đích DB và phạm vi đã duyệt. Hiện không có đường Apply thật hoặc seed tự động lúc khởi động ứng dụng.

Tạo lại môi trường test an toàn:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests/Run-IntegrationTests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tests/Run-UiTests.ps1 -Port 64303
node tests/MegaMenu.Tests.mjs
node tests/AdminUi.Tests.mjs
node tests/CustomerUi.Tests.mjs
```

Runner integration dùng DB Dacn05_Test_GUID mới, không dùng DB thật. Schema chỉ được dựng trên DB thử; không xóa/reset DB. Test tạo tồn demo bằng 8 phiếu nhập, mỗi phiếu 8 dòng, xác nhận bằng PurchaseReceiptService; mỗi SKU demo có tồn 5. Seed chạy lại không nhập thêm hàng. Các tài khoản thử được tạo riêng, không phải tài khoản thật.

## File sửa/thêm trong lần này

- OnlineSalesManagement/Controllers/StoreController.cs
- OnlineSalesManagement/Controllers/AdminUsersController.cs
- OnlineSalesManagement/Services/StorePresentation.cs
- OnlineSalesManagement/Services/DemoCatalog.cs (mới)
- OnlineSalesManagement/Views/Store/Index.cshtml
- OnlineSalesManagement/Views/Store/_CategoryMenu.cshtml (mới)
- OnlineSalesManagement/Views/AdminUsers/Index.cshtml
- OnlineSalesManagement/Scripts/store-menu.js (mới)
- OnlineSalesManagement/Content/customer.css (chỉ bổ sung CSS có scope)
- OnlineSalesManagement/Content/admin.css (bổ sung CSS card/toast)
- OnlineSalesManagement/OnlineSalesManagement.csproj (đăng ký file mới)
- tools/DemoCatalogCommand.cs (mới)
- tools/Invoke-DemoCatalog.ps1 (mới)
- tests/IntegrationTests.cs (bổ sung test)
- tests/Run-UiTests.ps1 (bổ sung kiểm tra HTTP và lưu HTML)
- tests/AdminUi.Tests.mjs (kiểm tra modal có cả quyền cũ/mới)
- tests/MegaMenu.Tests.mjs (mới)
- docs/STORE_MEGA_MENU_REVIEW.md (mới)

## Kết quả và giới hạn

- Build Debug + Razor compile: PASS.
- Integration: **230 PASS** trên DB thử, gồm seed hai lần không trùng, bảo toàn sản phẩm cũ, tồn/ledger, 16 danh mục, giao giữa q và categoryId, ít nhất 6 tài khoản, vai trò lạ/multiple/không có vai trò, đổi quyền và chặn mất Admin.
- HTTP: **196 PASS** trên bản sao IIS Express + DB Dacn05_Test_676de4f6ace54bf68e8b6e9994cd47d9; có 6 card, menu và metadata ARIA, danh mục SSD không lẫn nhóm, tìm SKU giữ categoryId, phân quyền 4 vai trò và hồi quy toàn bộ giỏ hàng/đặt hàng/đơn của tôi/AJAX.
- Bằng chứng HTML thực tế: `artifacts/ui-verification/run-eda3fe9a22e244e3bed33e195c97999a/Store.html` và `AdminUsers.html`. Đây là HTML phản hồi server, không phải screenshot; không chia sẻ công khai vì có anti-forgery token fixture test.
- JavaScript: 10 mega menu + 17 Admin UI + 11 customer UI PASS. Đây là test sự kiện DOM mô phỏng, không thay thế trình duyệt thật.
- Chưa có browser kết nối: inventory trình duyệt trống; thử mở IAB trả Browser is not available. Vì vậy chưa chụp screenshot hoặc xác nhận trực quan hover, breakpoint 4/2/1 và mobile bằng trình duyệt thật. Bố cục được kiểm tra từ CSS, HTML thực tế và test sự kiện; cần demo trực quan thêm.
- Không commit/push; không Apply OnlineSalesDb; không sửa Web.config máy.
