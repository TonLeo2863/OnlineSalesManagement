# Cải thiện giao diện quản trị và báo cáo

## Phạm vi

- Employees / Customers / Suppliers: tiêu đề tiếng Việt, khung bảng responsive, tìm nhanh không phân biệt dấu, lọc trạng thái, số kết quả, thông báo rỗng, badge và nút CRUD. Giữ tất cả trường thông tin cũ và route CRUD. Tìm nhanh ở ba trang này chạy phía trình duyệt trên danh sách hiện tại.
- AdminUsers: card tài khoản, avatar chữ cái, badge từng vai trò, combobox và nút lưu bên phải. Dialog xác nhận gồm “Xác nhận lưu” và “Hủy”; Escape cũng hủy. Nội dung dialog dùng textContent. Giữ POST, anti-forgery, TempData và toàn bộ kiểm tra an toàn SetRole hiện có.
- PurchaseReceipts: lọc server-side từ khóa (mã phiếu, nhà cung cấp, nhân viên, ghi chú), trạng thái, nhà cung cấp, nhân viên, ngày; vùng nâng cao gồm min/max tiền, mã phiếu, chỉ Draft hoặc Confirmed.
- SalesOrders: lọc server-side từ khóa (mã đơn, khách hàng, ghi chú), trạng thái, khách hàng, kênh bán, ngày, min/max tiền. Dùng chung partial với nhập kho.
- Bộ lọc dùng GET, giữ query string và giá trị trên form khi refresh; có Xóa lọc; tự mở vùng nâng cao khi có điều kiện. Ngày cuối tính hết ngày; tiền min/max bao gồm hai đầu. Điều kiện mâu thuẫn hoặc không hợp lệ hiển thị lỗi cùng danh sách rỗng. Hai checkbox Draft/Confirmed là hai điều kiện “chỉ”; chọn cả hai báo lỗi rõ ràng.
- Reports/Index: đầu báo cáo, kỳ báo cáo, thời điểm in/người lập, chỉ số tổng hợp, bảng có tổng cộng và khu vực ba chữ ký. Print CSS A4 12mm, ẩn menu/sidebar/nút/form, lặp tiêu đề bảng, tránh cắt hàng. Vẫn giữ công thức và điều kiện tính doanh thu cũ.

## File sửa trong lần này

1. `OnlineSalesManagement/Views/Employees/Index.cshtml`
2. `OnlineSalesManagement/Views/Customers/Index.cshtml`
3. `OnlineSalesManagement/Views/Suppliers/Index.cshtml`
4. `OnlineSalesManagement/Views/AdminUsers/Index.cshtml`
5. `OnlineSalesManagement/Controllers/PurchaseReceiptsController.cs`
6. `OnlineSalesManagement/Views/PurchaseReceipts/Index.cshtml`
7. `OnlineSalesManagement/Controllers/SalesOrdersController.cs`
8. `OnlineSalesManagement/Views/SalesOrders/index.cshtml`
9. `OnlineSalesManagement/Views/Shared/_DocumentFilters.cshtml` (mới)
10. `OnlineSalesManagement/Content/admin.css`
11. `OnlineSalesManagement/Views/Reports/Index.cshtml`
12. `OnlineSalesManagement/OnlineSalesManagement.csproj` (chỉ bổ sung partial vào Content; giữ các thay đổi đã có trước)
13. `tests/IntegrationTests.cs` (thêm bảy ca lọc vào bộ test đã có; giữ thay đổi trước đó)
14. `tests/AdminUi.Tests.mjs` (mới)
15. `docs/ADMIN_UI_REVIEW.md` (mới)

Các thay đổi khác đã có sẵn trong working tree không thuộc lần sửa này. Không sửa model, namespace, schema, migration, Web.config hoặc logic ghi tồn kho/phân quyền.

## Kiểm tra

- `msbuild OnlineSalesManagement.sln /p:Configuration=Debug /p:MvcBuildViews=true /verbosity:minimal /nologo`: PASS (build và biên dịch Razor, exit 0).
- `powershell -NoProfile -ExecutionPolicy Bypass -File tests/Run-SmokeTests.ps1`: 61 PASS.
- `powershell -NoProfile -ExecutionPolicy Bypass -File tests/Run-IntegrationTests.ps1`: 117 PASS, gồm 7 ca mới về điều kiện lọc và các ca hồi quy tồn kho, doanh thu, phân quyền, cửa hàng.
- `node tests/AdminUi.Tests.mjs`: 17 PASS (xác nhận/hủy/Escape và tìm kiếm/lọc tiếng Việt).
- `powershell -NoProfile -ExecutionPolicy Bypass -File tests/Run-UiTests.ps1 -Port 64271`: 186 HTTP PASS; gồm GET thực tế của cả bảy trang sửa, phân quyền bốn vai trò và hồi quy cửa hàng. Lần đầu ở cổng 64265 bị HTTP 503 tại trang chủ; chạy lại cổng 64271 thành công.
- Evidence HTTP: `artifacts/ui-verification/run-314effcd54044b2d9890dfdddfc93af2`.
- `git diff --check`: PASS.
- Integration tests chạy ngoài sandbox sau lỗi SSPI trong sandbox. Runner chỉ tạo/cập nhật database riêng có tiền tố `Dacn05_Test_`, giữ lại database test theo thiết kế; không migration database ứng dụng.

## Điểm cần kiểm tra demo

- Chưa kiểm tra trực quan bằng trình duyệt và máy in thật. Số trang phụ thuộc số đơn; bảng dài sẽ in tiếp trang, không cắt bỏ dữ liệu để ép 1–2 trang.
- Dialog dùng API HTML dialog của trình duyệt hiện đại.
