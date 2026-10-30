# Admin xem trước cửa hàng và khách hủy Pending — 30/10/2026

## Source và phạm vi

Đã đọc source thực tế và git status trước sửa. Branch feature/management-upgrades-20261030, HEAD gốc 15b13a8649817781a66b790d4ab5bd5e5218a4a5; working tree đã có nhiều thay đổi chưa commit từ trước. Giữ các thay đổi đó, gồm Reorder, giỏ hàng, Identity và AccountGovernance. Lưu git status/HEAD và bản trước sửa của các file chính tại artifacts/store-preview-cancel/before-20261030-092033.

Không đổi model/namespace/database/Web.config, không thêm migration, không Commit/Push. Không truy vấn hoặc chạy test ghi trên OnlineSalesDb thật trong lượt này. Harness hiện có khởi tạo schema từ các migration hiện có và tạo fixture chỉ trên Dacn05_Test_* mới; không có migration mới cho hai tính năng.

## Hành vi mới

### Admin chỉ xem trước

Store/Index của Admin dùng _CustomerLayout để xem sản phẩm/danh mục/tìm kiếm/modal như khách, với nhãn “Chế độ xem trước — Quản trị viên” và “Quay lại quản trị”. Không còn navigation giỏ/đơn của tôi; nút mua trong card disabled, fieldset mua ở modal disabled, JSON chi tiết không cấp cart version hoặc khả năng mua. Không tạo cart/profile/order cho Admin khi browse.

StoreController.OnActionExecuting cho Index/ProductDetails tiếp tục đọc công khai; mọi action khách còn lại chỉ cho KhachHang và **không có Admin**. Admin mang đồng thời role KhachHang cũng bị loại bởi điều kiện !Admin. Chặn Cart/Checkout/Orders/Details, AddToCart/UpdateCart/ClearCart/Checkout/Reorder/CancelPending trước action, kể cả POST đã sửa HTML/token hợp lệ. AJAX bị chặn trả JSON403, GET AJAX cũng không vướng JsonRequestBehavior.DenyGet. Không dùng impersonation, không thay cookie/tài khoản để preview.

Cookie authentication được chia sẻ giữa các tab cùng hostname/browser profile. Khi logout khách rồi login Admin, các tab cũ dùng cookie Admin trên request tiếp theo và bị chặn checkout. Cart vẫn gắn UserId trong session; không chuyển giỏ sang Admin. Hai tài khoản đồng thời cần browser profile khác hoặc cửa sổ riêng tư; tab thường mới không tạo danh tính độc lập. HTTP đã kiểm bằng cùng CookieContainer đổi khách → Admin, không giả lập tài khoản khách cho Admin.

### Khách tự hủy Pending

POST Store/CancelPending có AntiForgeryToken và role guard, gọi SalesOrderService.CancelPendingByCustomer. Service mở Serializable transaction, khóa đúng hàng SalesOrders bằng UPDLOCK/HOLDLOCK như ConfirmOrder, kiểm CustomerId/UserId và trạng thái ngay trong transaction, kiểm không có ledger bất thường rồi chỉ cập nhật Status=Cancelled. Không gọi CancelOrder chung, không đổi tồn, không tạo ledger, không xóa đơn/dòng; giữ concurrency/RowVersion hiện có.

Confirm và Cancel trên cùng đơn phải chờ cùng khóa hàng: bên đến sau đọc trạng thái mới và bị từ chối. Khách không hủy Confirmed/Completed/Cancelled; form Pending cũ sau Admin Confirm hiển thị “Không thể hủy do đơn hàng đã được xử lý.” Không tìm thấy/không thuộc user trả404. Lặp hủy đã Cancelled hiển thị thông báo, không ghi lại. Admin CancelOrder Confirmed vẫn có luồng hoàn kho cũ.

Orders và Details chỉ hiện nút outline đỏ ở Pending. Bootstrap modal hiện mã đơn dạng text và “Bạn có chắc muốn hủy đơn hàng này?”, “Giữ đơn hàng” / “Xác nhận hủy”. Hủy/đóng modal không submit; xác nhận disable gửi lặp, server vẫn bảo vệ khi gửi nhiều HTTP. Sau thành công redirect Details, báo thành công/trạng thái Đã hủy, không còn nút hủy.

## File sửa/thêm trong lượt này

- Controllers/StoreController.cs — preview/role guard, JSON detail, POST CancelPending.
- Services/SalesOrderService.cs — CancelPendingByCustomer; không thay luồng Admin CancelOrder.
- Views/Store/Index.cshtml, Details.cshtml, Orders.cshtml, _ProductModal.cshtml.
- Views/Shared/_CustomerLayout.cshtml — banner/return và navigation preview.
- **Thêm** Views/Store/_CancelPending.cshtml, _CancelPendingDialog.cshtml.
- Scripts/customer.js — modal preview không mời login/mua.
- **Thêm** Scripts/cancel-order.js — xác nhận/giữ đơn/chặn double submit.
- OnlineSalesManagement.csproj — đăng ký ba Content mới.
- tests/IntegrationTests.cs, Run-UiTests.ps1, CustomerUi.Tests.mjs; **thêm** tests/CancelOrderUi.Tests.mjs.
- Báo cáo này. Các path ứng dụng phía trên tính từ OnlineSalesManagement/.

## Kết quả chạy mới cuối cùng

Rebuild cuối hoàn tất trước 09:30, HTTP cuối kết thúc khoảng 09:33 ngày 30/10 (+07). Source manifest 16 file liên quan trước/sau lượt cuối trùng hash, lưu final-source-before.json / final-source-after.json trong artifacts/store-preview-cancel. Đây là working tree chưa commit, không chứng nhận nguyên HEAD cũ.

| Bộ kiểm tra | Kết quả |
|---|---|
| Rebuild Debug + MvcBuildViews=true | PASS exit0; warning CodeDom Access denied của môi trường |
| Smoke | **64 PASS** |
| Integration DB riêng | **316 PASS** |
| HTTP IIS Express DB riêng | **265 PASS** |
| JavaScript | **44 PASS**: Customer12, Cancel5, MegaMenu10, Admin9, State5, Position3 |
| Structural/Razor registration/UTF8 | PASS 161 C#/Razor, 229 entries, 51 POST checks |
| Source trước/sau lượt cuối / diff whitespace | PASS; chỉ warning Git LF→CRLF |
| Chrome desktop/mobile, hình thức modal bằng trình duyệt thật | NOT TESTED |
| Test ghi trên DB thật | NOT RUN, không được thực hiện |

DB cuối: **Dacn05_Test_9a96506f2118437a8a4ccfec9d83638e**. Evidence HTTP: artifacts/ui-verification/run-a299ab369c994910a1d43dc683aaa064. Không xóa DB thử. Logs: artifacts/store-preview-cancel/build.log, smoke.log, js.log, final-integration-http.log. Log integration-http.log là lượt trước 263 HTTP trước bổ sung hai ca JSON preview/AJAX GET; không dùng lượt đó thay cho tổng cuối. Không có FAIL chưa xử lý trong lượt cuối; không dùng kết quả phiên AccountGovernance cũ để chứng nhận.

Các ca trọng tâm PASS: Admin manual POST cả 6 action, GET4 route khách, AJAX403, JSON product thật không cấp quyền/cart, không tạo profile/order; multi-line customer checkout như cũ; owner cancel Pending giữ3 dòng và tổng tồn/ledger, foreign owner404, thiếu CSRF không ghi, replay thông báo; Admin confirm rồi form Pending cũ cancel bị chặn; service từ chối Confirmed/Completed/Cancelled; 12 cặp task Confirm/Cancel khởi động đồng thời, mỗi cặp đúng1 thành công với ledger/tồn tương ứng; Admin hủy Confirmed vẫn hoàn kho; toàn DB thử khớp ledger cuối integration. Node DOM tests kiểm Giữ đơn/đóng không gửi, mã đơn text an toàn, xác nhận chỉ gửi1 và khóa nút. Không đánh đồng DOM/HTTP với Chrome UI thật.

Web.config SHA256 giữ nguyên 8C2441F0BC7C0A617CEFB4F65744180A1E44E09E36C7E8CFEE63963BB854A9C2.

## Test thủ công

1. **Admin preview:** Rebuild, chạy IIS Express, login Admin → sidebar Cửa hàng trực tuyến. Kiểm nhãn/Quay lại quản trị, menu hover/click/ESC, tìm tên/SKU/danh mục, ảnh/giá/tồn và modal chi tiết. Thu viewport xuống mobile. Không có link giỏ/đơn của tôi; nút mua disabled. Mở trực tiếp /Store/Cart, /Store/Checkout, /Store/Orders phải403. Quay lại quản trị vẫn xem SalesOrders/PurchaseReceipts/Reports theo quyền cũ.
2. **Khách mua như cũ:** dùng browser profile khác/riêng tư login KhachHang. Thêm nhiều món vào giỏ, sửa lượng và checkout, nhận đơn Pending. Thử nghiệp vụ ghi trên DB thử để không tạo giao dịch demo trong DB thật.
3. **Giữ đơn:** ở Orders hoặc Details của đơn Pending → Hủy đơn, kiểm mã/đúng thông báo → Giữ đơn hàng hoặc đóng modal. Reload vẫn Pending, không mất dòng.
4. **Hủy thành công:** mở lại modal → Xác nhận hủy; thấy “Đã hủy đơn hàng.” và trạng thái Đã hủy, đơn/dòng vẫn có trong lịch sử, nút hủy biến mất. POST lặp phải báo đã hủy và tồn/ledger không đổi.
5. **Quyền/trạng thái:** trên DB thử, thử ID của khách khác (404), token thiếu/sai (bị chặn), mở Pending rồi Admin Confirm từ trình duyệt khác trước khi khách xác nhận hủy (báo đã xử lý). Confirmed/Completed không có nút; gọi POST thủ công cũng không hủy được. Không dùng CancelOrder quản trị để giả lập luồng khách.
6. **Cookie:** trong cùng profile browser, logout khách rồi login Admin. Tab checkout cũ cùng hostname phải bị từ chối ở request kế tiếp; refresh Store thấy Preview. Muốn giữ hai danh tính đồng thời phải dùng profile riêng. Không có session impersonation mới.

Chạy lại tự động từ root (chỉ runner DB test, không dùng các tool deployment production):

```powershell
msbuild OnlineSalesManagement.sln /t:Rebuild /p:Configuration=Debug /p:MvcBuildViews=true
powershell -NoProfile -ExecutionPolicy Bypass -File tests/Run-SmokeTests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tests/Run-UiTests.ps1 -Port 64347
node --test tests/CustomerUi.Tests.mjs tests/CancelOrderUi.Tests.mjs tests/MegaMenu.Tests.mjs tests/AdminUi.Tests.mjs tests/AccountStateUi.Tests.mjs tests/EmployeePositionUi.Tests.mjs
```

Không chạy thêm migration trên OnlineSalesDb; hai chức năng này không đổi schema. Chưa Commit/Push, chờ người dùng xem source/giao diện.
