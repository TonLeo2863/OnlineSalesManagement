# Giao diện khách hàng — 19/10/2026

Nhánh `codex/dacn05-completion`. Tiếp tục từ các thay đổi giỏ/phân quyền chưa commit của đợt trước; không reset hoặc ghi đè chúng. Không migration, không thay connection string OnlineSalesDb, không commit/push/merge.

## Rà soát ban đầu

- Bootstrap 5.2.3 đã có tại project; dùng bundle kèm Popper có sẵn cho layout khách hàng, không thêm thư viện CDN.
- `_ViewStart` trước đây chọn `_Layout` quản trị cho mọi tài khoản/anonymous. CSS Site cũ giới hạn input 280px và layout có sidebar.
- StoreController/ShoppingCart đã có thêm/cập nhật/xóa/clear, version chống replay và Checkout nhiều dòng Pending/Unpaid bằng SalesOrderService. Giỏ là Session theo UserId, chưa lưu SQL; giữ nguyên giới hạn đó, không thay bằng giỏ client.
- Product đã có ImageUrl/Description/Category/Unit. Không thay model Product hoặc schema.
- Source hiện chưa có entity/service/controller thông báo đơn hàng; không thể tuyên bố giữ một hệ thống thông báo chưa được triển khai. Đợt này hiển thị trạng thái đơn bằng tiếng Việt; thông báo bền vững/polling là phần còn thiếu của phạm vi trước.

## Các giai đoạn

1. Layout khách hàng riêng, header ngang/badge giỏ theo tài khoản, banner và bốn sản phẩm nổi bật từ DB; Admin/Kho/BanHang vẫn dùng layout sidebar. Build/Razor, smoke và HTTP trước khi chuyển tiếp.
2. Grid ảnh vuông, modal lấy dữ liệu server và AJAX thêm giỏ bằng anti-forgery; JSON có success/message/count/version; toast và chống gửi lặp; kiểm chứng backend và JavaScript.
3. Giỏ có ảnh/tăng giảm và giao diện đơn tiếng Việt; kiểm chứng hồi quy Checkout/kho/quyền.

Các lỗi trong quá trình làm được sửa rồi chạy lại, không coi lượt thất bại là đạt. Lượt đầu build layout đạt nhưng HTTP Home lỗi khi render badge qua child action; sau khi đổi sang đọc ShoppingCart theo UserId trong layout và render partial, Home/HTTP đạt. Không thêm endpoint badge trùng lặp; không dùng child action chịu OutputCache của Store. Lượt lỗi đầu chưa lưu response body nên không coi đó là chẩn đoán đầy đủ exception.

## Giới hạn kiểm thử giao diện

Công cụ browser inventory hiện trả `apps: []`, `browsers: []`. Chưa thể xác nhận bằng ảnh/click Chrome hoặc Edge thật. Dùng build Razor, HTTP trên IIS Express bản sao/DB thử riêng và kiểm thử logic JavaScript; ghi rõ các phần chỉ kiểm chứng cấu trúc/logic, không đánh đồng với nghiệm thu trực quan responsive/focus/ESC.

## Kết quả

- Giai đoạn 1: Debug/Razor + 61 smoke đạt, 110 integration + **164 HTTP PASS**, gồm khách không có sidebar và đúng sidebar cho Admin/Kho/BanHang. Record `artifacts/verification/verify-20261019-095716-707.json`; evidence `artifacts/ui-verification/run-1075030a7f6f4d99a8bf51b7ab675e7b`; DB thử `Dacn05_Test_51256c5ed7394760a5e582542dd6860d`.
- Giai đoạn 2: Debug/Razor + 61 smoke đạt (`artifacts/verification/verify-20261019-100631-608.json`), 110 integration + **179 HTTP PASS**. Evidence `artifacts/ui-verification/run-85ee5e27f6b3493eb4b4ddaca11b047f`, DB `Dacn05_Test_7b543762484a4de3bc08881941f0db2e`. HTTP kiểm chứng dữ liệu modal 200/404, AJAX 200 không Location, replay/thiếu tồn/ngừng bán 409, badge sau reload, cart AJAX Checkout Pending/Unpaid và anonymous/hết phiên JSON 401 không chuyển Login.
- JavaScript hiện có **11 behavioral tests PASS** qua `node --test tests/CustomerUi.Tests.mjs`: CSRF/fetch, chặn request lặp đang chạy, toast/badge chờ server, error/401/CSRF HTML/network, modal hết hàng/anonymous/ngừng bán, abort và loại phản hồi cũ, ảnh hỏng dùng placeholder; bổ sung trường hợp modal về chậm không ghi đè version của POST mới hơn. Đây là DOM adapter/VM test, không phải browser thực.
- Giai đoạn 3: giỏ có thumbnail, form POST tăng/giảm/nhập số lượng/xóa, bảng tổng; Checkout bố cục hai cột, ảnh và thông tin nhận hàng; Orders/Details có template trạng thái tiếng Việt, nút chi tiết và thông điệp trạng thái hiện tại. Đăng nhập/đăng ký Việt hóa; partial external login chỉ hiện provider thực sự cấu hình. Đã kiểm chứng bằng lượt cuối bên dưới.
- Lượt cuối tuần tự `Verify-Dacn05.ps1 -IncludeIntegration`: **Passed**, Debug/Razor + **61 smoke + 11 JavaScript + 110 integration PASS**, `SourceStable=true`, bao gồm hash CSS/JS/SVG. Record `artifacts/verification/verify-20261019-102131-459.json`; DB riêng `Dacn05_Test_7e55dce52c7b41c3a81145a3c13abad6`. Không warning CodeDom ở lượt build ngoài sandbox này.
- Source audit cuối: **136 file C#/Razor, 202 project entries, 45 POST action có anti-forgery**, UTF-8 hợp lệ. Đối chiếu source với snapshot build cuối: không thay đổi.
- HTTP cuối sau khi build kết thúc: **186 PASS**, exit 0, kèm 110 integration PASS khi dựng fixture riêng. Evidence `artifacts/ui-verification/run-d8e5b29be5124c8a8b5058d883d60314`; DB riêng `Dacn05_Test_5b21031b71b04b9f96a332b0051139af` (giữ lại), IIS test đã dừng bằng finally. Bổ sung xác minh ảnh/nút tăng giảm/tổng giỏ, nhãn trạng thái đơn/thanh toán tiếng Việt, nút chi tiết; Store của Admin/Kho/BanHang giữ sidebar và chỉ nạp customer.js một lần. Đăng ký/đăng nhập/đăng xuất và các quyền truy cập vẫn đạt.

Các lượt lỗi giai đoạn 3 không được dùng để chứng nhận: dùng ViewBag trong một Store/_ViewStart gây CS0103 (đã xóa file không cần thiết, điều kiện CSS/JS đặt trong layout theo route); Rebuild chạy trùng integration từng làm thiếu DLL (đã chạy lại tuần tự); snapshot guard từng báo SourceChanged khi sửa view trong lượt verify (đã chạy lại với source ổn định). Không có lỗi kho được sửa vòng qua hoặc bỏ kiểm thử.

## File thay đổi trong đợt giao diện

- `Views/_ViewStart.cshtml`, `Views/Shared/_CustomerLayout.cshtml`: chọn layout theo DashboardService.IsStaff; header/logo/navbar/tài khoản/badge, footer, Bootstrap bundle local. `Views/Shared/_Layout.cshtml` chỉ nạp CSS/JS/toast khách hàng khi nhân viên vào route Store; sidebar và các route quản trị giữ nguyên.
- `Controllers/HomeController.cs`, `Views/Home/Welcome.cshtml`: banner và bốn sản phẩm mới nhất ưu tiên còn hàng từ DB.
- `Controllers/StoreController.cs`: DTO chi tiết sản phẩm active, JSON cho AJAX thêm/cập nhật giỏ; giữ POST thường/CSRF/version/server price và Checkout nhiều dòng. `App_Start/Startup.Auth.cs` thêm JSON 401 cho XHR chưa đăng nhập; giữ 403 và cookie/stamp validation.
- `Services/StorePresentation.cs`: helper ảnh local/HTTP(S), placeholder cho URL trống/không phù hợp. `Services/ShoppingCart.cs` chỉ bổ sung ImageUrl trong DTO CartLine; không thay entity/schema.
- `Views/Store/Index.cshtml`, `_ProductModal.cshtml`, `_CartBadge.cshtml`, `Cart.cshtml`, `Checkout.cshtml`, `Orders.cshtml`, `Details.cshtml`: toàn bộ giao diện mua sắm và form hiện có.
- `Views/Shared/_ShopToast.cshtml`, `DisplayTemplates/OrderStatus.cshtml`, `PaymentStatus.cshtml`, `PaymentMethod.cshtml`: toast và nhãn trạng thái tiếng Việt. Khối thông điệp trong Details phản ánh trạng thái đơn hiện tại; không giả lập một notification entity hoặc lịch sử sự kiện.
- `Content/customer.css`, `Content/product-placeholder.svg`, `Scripts/customer.js`: grid 4/3/2/1–2 cột, ảnh 1:1, modal centered/scrollable, fetch/toast/badge, fallback ảnh và loại phản hồi cũ; không thêm dependency bên ngoài.
- `Views/Account/Login.cshtml`, `Register.cshtml`, `_ExternalLoginsListPartial.cshtml`, thông báo sai mật khẩu trong `AccountController.cs`: chỉnh nội dung/bố cục, giữ form/action/anti-forgery và các quy tắc Identity hiện có.
- `.csproj` đăng ký file mới; `tests/Run-UiTests.ps1`, `CustomerUi.Tests.mjs`, `Verify-Dacn05.ps1`: mở rộng kiểm chứng. Verification cuối bao gồm hash CSS/JS/SVG để phát hiện thay đổi tài nguyên trong lúc test.

Các thay đổi SalesOrderService và các ca kho trong IntegrationTests/SmokeTests từ đợt giỏ trước được giữ nguyên, không thay nghiệp vụ nhập/xuất kho ở đợt giao diện.

## Kiểm thử trực quan còn cần chạy

1. Chạy website bằng Visual Studio/IIS Express với cấu hình phù hợp. Dùng Chrome profile khách và Edge/profile độc lập cho Admin; không dùng hai tab cùng profile để giả lập hai tài khoản.
2. Kiểm tra viewport 1440px/1100px/800px/390px/320px: grid 4/3/2/2/1 cột, header thu gọn, không tràn ngang.
3. Mở Xem chi tiết bằng bàn phím, kiểm tra tab/focus nằm trong modal, ESC/nút Đóng/backdrop đóng và focus trở lại nút mở. Màn hình nhỏ cuộn được nội dung modal.
4. Thêm nhiều SKU ở card/modal: giữ URL Store, toast khoảng 3 giây và đóng được bằng X; badge tăng đúng tổng đơn vị. Bấm lặp khi request chậm không gửi thêm request. Thử mạng lỗi, hết phiên và sản phẩm vừa bị ngừng bán để kiểm tra thông báo.
5. Giỏ: +/−, nhập số lượng rồi Cập nhật, xóa và clear; các thao tác giỏ dùng POST rồi tải lại trang. Checkout tạo một đơn nhiều dòng Pending; Admin xác nhận mới trừ kho theo logic hiện có.
6. So sánh sidebar Admin/nhân viên trước/sau, kiểm tra khách truy cập trực tiếp URL quản trị nhận 403; đơn người khác nhận 404.

## Phần còn thiếu/giới hạn

- Giỏ vẫn dùng Session theo UserId; reload hoặc đăng nhập lại trong cùng Session không tự xóa giỏ, nhưng hết Session/app recycle vẫn mất. Chưa có giỏ SQL dùng chung nhiều thiết bị; không chạy migration.
- Chưa có hệ thống thông báo đơn bền vững/polling trong source hiện tại. Đợt này chỉ cải thiện nhãn/thông điệp trạng thái trong đơn, không thêm chuông giả hoặc dữ liệu giả.
- Các màn hình quản lý Identity phụ (đổi mật khẩu, 2FA, external callback...) vẫn theo scaffold hiện có; không thay cấu hình bảo mật hoặc quy tắc mật khẩu. Thông báo validation do Identity sinh có thể còn tiếng Anh.
- Chưa có kiểm chứng trực quan browser thực vì inventory không có browser/app; CSS breakpoint/Bootstrap keyboard/focus cần nghiệm thu bằng các bước trên.
