# Giai đoạn 1–2: phân quyền và giỏ nhiều sản phẩm (19/10/2026)

Nhánh: `codex/dacn05-completion`. Working tree sạch trước khi bắt đầu. Không sửa connection string ứng dụng, không cập nhật OnlineSalesDb, không sửa bảng Identity thủ công, không commit/push/merge.

## Rà soát và phạm vi sửa

Đã kiểm kê source MVC5/EF6/Identity2, controllers và các ràng buộc quyền; kiểm tra Account, Startup/OWIN, IdentityConfig, FilterConfig, routes, Global.asax, Web.config, cấu hình IIS Express, models/Fluent API/migrations, services tạo/xác nhận/hủy đơn, nhập kho, dashboard/menu, view cửa hàng/layout và các harness build/smoke/integration/HTTP hiện có.

Các điểm trước sửa:

- MVC Authorize trả 401 cho người đã đăng nhập nhưng thiếu role; cookie middleware mặc định biến thành 302 về Login. Account/Login GET gốc trả view, không có nhánh tự động chuyển lại ReturnUrl. Không đủ bằng chứng để kết luận source này tự tạo vòng lặp vô hạn.
- HTTP source gốc: `Kho GET /AdminUsers -> 302 Location=http://localhost:64265/Account/Login?ReturnUrl=%2FAdminUsers`. Lượt trace ban đầu dừng khi harness nối nhầm URL tuyệt đối vào base URL; đã sửa helper nhận URL localhost tuyệt đối. Không dùng lượt lỗi làm kết quả kiểm thử đạt.
- Checkout cũ chỉ nhận một ProductId/Quantity và lưu token dùng chung trong Session. Note của khách chưa được đưa vào đơn.
- CreatePending đã nhận danh sách Details, lấy giá server, tạo Pending/Unpaid. Confirm/Cancel hiện có transaction Serializable, UPDLOCK/HOLDLOCK, kiểm tra ledger/status, RowVersion của Product; không thay quy trình nhập kho hoặc trừ/hoàn kho.

## Giai đoạn 1

- `App_Start/Startup.Auth.cs`: OnApplyRedirect phân biệt request đã đăng nhập, trả 403 khi thiếu quyền; anonymous vẫn được chuyển Login. Giữ SecurityStampValidator TimeSpan.Zero để quyền/stamp tiếp tục được kiểm tra từng request; không đổi bảo vệ cookie.
- `Controllers/AccountController.cs`: tài khoản đã đăng nhập mở Login được đưa về Home; ReturnUrl chỉ nhận URL nội bộ, tránh chuyển trở lại các action Account sau đăng nhập; giữ ReturnUrl hợp lệ khi form sai.
- `tests/Run-UiTests.ps1`: ghi status/Location và yêu cầu chính xác 403 cho GET thiếu quyền.
- Kiểm chứng riêng giai đoạn 1: Debug + Razor đạt; 52 smoke đạt; 92 integration và 122 HTTP đạt trên DB riêng `Dacn05_Test_28f0aa4e245f429698c3061d9e6cb8c3`. Evidence: `artifacts/ui-verification/run-a7e87c66650544678079cafa32671058`. HTTP sau sửa: `KhachHang GET /AdminUsers -> 403 Location=`; các nhóm thiếu quyền còn lại cũng không về Login.
- Chưa tái hiện được ERR_TOO_MANY_REDIRECTS vô hạn của phiên người dùng. Cần HAR của đúng phiên bị lỗi nếu lỗi còn xuất hiện; không quy kết lỗi do SecurityStampValidator hoặc xóa cookie thay cho sửa phân quyền.

## Giai đoạn 2

- `Services/ShoppingCart.cs`: giỏ server theo OwnerId, phiên bản ngẫu nhiên cho mỗi thay đổi; giới hạn 100 dòng và 1000 đơn vị/dòng; lưu receipt 20 lần đặt gần nhất để replay chuyển lại đúng đơn.
- `Controllers/StoreController.cs`: key Session theo Identity UserId; thêm/cập nhật/xóa/làm trống bằng POST + anti-forgery; request dùng version cũ không thay giỏ. GET không sửa số lượng. Checkout dựng Details từ giỏ server, kiểm tra token theo tài khoản, phục hồi đơn đã commit theo OrderNumber và chủ đơn, PRG sau thành công. Giá/tổng/trạng thái/chủ đơn do client gửi không được bind. Giữ trang đơn chỉ truy vấn theo Customer.UserId. Tắt cache cho cửa hàng có nội dung theo tài khoản.
- `Services/SalesOrderService.cs`: riêng CreatePending Online kiểm tra khách active, danh mục active và đủ tồn ngay trong transaction. Giữ cách tạo Pending/Unpaid và snapshot giá server; chưa trừ/giữ hàng. Pending tại quầy vẫn giữ nghiệp vụ cũ.
- `Views/Store/Index.cshtml`, `Cart.cshtml`, `Checkout.cshtml`: thêm vào giỏ; bảng số lượng/giá/thành tiền/tổng; cập nhật/xóa/làm trống; checkout nhiều dòng, người nhận/điện thoại/địa chỉ/ghi chú.
- `.csproj`: đăng ký service/view mới. Smoke/Integration/HTTP mở rộng để kiểm tra giỏ, replay, tài khoản và đơn nhiều dòng.

Không có migration trong đợt này. Giỏ dùng ASP.NET Session hiện tại (InProc), không phải giỏ lưu bền vững trong SQL. Giỏ mất khi Session hết hạn/app recycle; cùng tài khoản ở hai session độc lập có hai giỏ. Đăng xuất/đăng nhập lại trong cùng Session có thể thấy lại giỏ của chính tài khoản. Các tài khoản trong cùng Session vẫn có key/owner riêng. Đơn đã đặt được lưu bền vững trong SQL. Nếu cần giỏ dùng chung nhiều thiết bị và qua app recycle, cần đợt migration bổ sung được review SQL, thử DB riêng và xác nhận trước khi áp dụng DB thật.

Giỏ chưa giữ hàng. Tồn có thể giảm sau khi thêm; trang giỏ hiển thị lỗi và Checkout chặn; CreatePending kiểm tra lại trong transaction, Confirm vẫn kiểm tra tồn lần cuối theo nghiệp vụ hiện có. Form cũ ở tab khác yêu cầu tải lại giỏ để tránh ghi đè. Link Checkout cũ có id không còn tự đặt một món; người dùng thêm vào giỏ rồi Checkout.

## Kiểm thử thủ công hai profile độc lập

1. Mở Chrome profile Admin và Edge profile khách (hoặc Chrome profile thường và Chrome ẩn danh). Hai tab trong cùng profile chia sẻ cookie đăng nhập nên không dùng để giả lập hai tài khoản độc lập.
2. Dùng cùng URL gốc/port. Đăng nhập Admin ở profile thứ nhất, khách A ở profile thứ hai. Dùng thêm một profile độc lập cho khách B.
3. Khách A thêm ít nhất ba SKU, tăng/giảm số lượng, xóa/re-add, kiểm tra tổng; refresh không thêm lặp. Khách B mở Giỏ/Đơn của tôi phải không thấy dữ liệu A.
4. Khách truy cập trực tiếp `/AdminUsers`, `/SalesOrders`, `/Products` phải nhận 403 không có Location về Login. Anonymous truy cập `/Store/Cart` phải đến Login, đăng nhập xong về đúng trang; ReturnUrl ngoài website không được điều hướng ra ngoài.
5. Mở hai tab giỏ trong profile khách, cập nhật tab đầu rồi gửi form tab cũ: không thay số lượng lần nữa. Gửi Checkout hai lần: cùng đơn, ba dòng, Pending/Unpaid, không đổi kho.
6. Admin xác nhận một đơn; xác nhận lại không trừ kho thêm. Hủy đơn Confirmed hoàn kho theo từng dòng. Khách xem đúng đơn của mình; URL đơn khách khác trả 404.
7. Modal, chuông/thông báo bền vững và polling thuộc giai đoạn 3–5, chưa triển khai trong đợt chỉ làm 1–2 này. Chưa chứng nhận UI desktop/mobile bằng ảnh hoặc kiểm thử tải/cạnh tranh nhiều IIS worker.

## Kết quả cuối giai đoạn 2

- Rebuild Debug + `MvcBuildViews=true`: đạt; smoke **61 PASS**. Record cuối: `artifacts/verification/verify-20261019-092026-615.json` (Status/Build/Smoke=Passed, SourceStable=true).
- Integration: **110 PASS**. Đơn ba sản phẩm Pending/Unpaid, snapshot giá server, rollback khi thiếu tồn, sản phẩm/danh mục ngừng bán, confirm nhiều dòng không trừ lặp, cancel hoàn kho; giữ các ca RowVersion, nhập kho và phân quyền trước đó.
- HTTP: **159 PASS**, exit 0. Ba sản phẩm trong một giỏ/đơn; tăng/giảm/xóa/tổng; sản phẩm không tồn tại/vượt tồn/số lượng âm; anti-forgery; replay thêm hàng và Checkout; không tin Price/TotalAmount/Status/CustomerId/ProductId/Quantity từ form Checkout; owner đọc đúng đơn/ghi chú; khách khác nhận 404; cách ly giỏ ngay cả khi cố ý chia sẻ SessionId giữa hai cookie Identity độc lập; clear giỏ; anonymous challenge/Login kết thúc; authenticated Login về Home; thiếu role trả đúng 403 không có Location.
- DB riêng: `Dacn05_Test_60907fbbd1c44bee99784bf50227232c` (giữ lại). Bản sao IIS và HTML evidence: `artifacts/ui-verification/run-c68fa8c3774348dc92a22932253b08ad` (ignored). IIS test đã được dừng bằng finally.
- Source audit: 128 file C#/Razor, 191 project entries, 45 POST action có anti-forgery, UTF-8 hợp lệ. `git diff --check` đạt.
- MSBuild có warning `Access denied` từ CodeDom props trong sandbox, nhưng build C# và Razor hoàn tất thành công. Chưa có kiểm thử trực quan bằng browser profile thật hoặc tải/cạnh tranh đa worker; không dùng HTTP CookieContainer để tuyên bố đã kiểm thử Chrome/Edge UI.

Các lượt lỗi trước đó không được coi là kết quả đạt: SSPI trong sandbox (đã chuyển runner DB thử ra Windows Authentication ngoài sandbox), cú pháp Razor view Cart (đã sửa), tên biến PowerShell PID chỉ đọc trong harness (đã đổi tên và chạy lại toàn bộ), gọi nhầm tên export source audit (đã gọi đúng `checkSource` và đạt).
