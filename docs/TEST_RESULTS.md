# Kết quả và kịch bản kiểm thử

## Đã thực thi 16/10/2026

- `git status --short`: sạch trước khi làm; nhánh ban đầu codex-fixes.
- `git rev-list --left-right --count master...HEAD`: 0/0; HEAD 877c60b.
- MSBuild Debug trước restore: thất bại do thiếu CodeDom NuGet props.
- MSBuild Restore với RestorePackagesConfig=true: exit 0.
- MSBuild Debug sau restore/sửa tên reference Facebook: exit 0, tạo OnlineSalesManagement.dll.
- Build sau sửa controller với MvcBuildViews=true: exit 0. Git diff --check phát hiện khoảng trắng cuối dòng form; đã sửa và kiểm tra lại.
- Chưa chạy HTTP, kiểm thử tích hợp SQL, in A4 hoặc kiểm thử theo role. Chưa kết luận chức năng hoàn thành.

## Kịch bản bắt buộc trên database kiểm thử

| ID | Thao tác | Kỳ vọng | Kết quả |
|---|---|---|---|
| AUTH01 | Đăng nhập đúng/sai, logout rồi truy cập URL cũ | Đúng vào hệ thống; sai báo lỗi; logout không vào vùng bảo vệ | Chưa chạy |
| AUTH02 | Anonymous và khách gọi GET/POST từng controller quản trị, có/không token | Không được đọc/ghi; token không thay thế phân quyền | Chưa chạy |
| AUTH03 | Admin/Kho/BanHang gọi URL trực tiếp | Chỉ đúng vai trò được truy cập | Chưa chạy |
| PROD01 | Create POST gửi QuantityInStock=100, CreatedDate cũ | Tồn 0, ngày server; không có giao dịch giả | Chưa chạy |
| PROD02 | Edit POST gửi tồn/ngày tạo giả | Giá trị tồn/ngày tạo trong DB giữ nguyên | Chưa chạy |
| PROD03 | Hai tab tải cùng RowVersion, lưu lần lượt | Lần hai báo xung đột; không ghi đè | Chưa chạy |
| PROD04 | Sửa sản phẩm đồng thời với xác nhận nhập | Không làm mất tồn; stale form báo xung đột | Chưa chạy |
| PROD05 | SKU trùng, category không tồn tại, thiếu tên/đơn vị, giá âm | Validation thân thiện; không lỗi 500 | Chưa chạy |
| PROD06 | Xóa có tồn hoặc dòng chứng từ; xóa ID không tồn tại | Chặn xóa hoặc 404; lịch sử giữ nguyên | Chưa chạy |
| CRUD01 | Khách/nhân viên/NCC/danh mục: hợp lệ, mã trùng, thiếu dữ liệu, xóa liên kết | Đúng validation và FK | Chưa chạy |
| IN01 | Tạo nháp 2 dòng, xác nhận, xác nhận lại | Nháp không tăng; tăng đúng một lần, có lịch sử | Chưa chạy |
| IN02 | Hủy nháp/đã xác nhận; hàng đã xuất không đủ trả | Đúng trạng thái, rollback toàn bộ nếu không đủ | Chưa chạy |
| OUT01 | Pending, vượt tồn, xác nhận lặp, 2 đơn tranh tồn, hủy | Không âm/không trừ hai lần, hoàn tồn đúng quy tắc | Chưa chạy |
| ONLINE01 | Khách tạo đơn sửa giá/khách/role/status; xem đơn khách khác | Giá server, Pending, chặn truy cập chéo | Chưa chạy |
| INV01 | In Confirmed/Completed và thử Pending/Cancelled | Chứng từ nội bộ hợp lệ, chặn sai trạng thái, đủ thông tin A4 | Chưa chạy |
| REP01 | Khoảng ngày đảo, sai định dạng, ngày tối đa, không dữ liệu, số 0 | Không crash, có lỗi/empty state rõ ràng | Chưa chạy |
| REP02 | Tạo phiếu ngày A, xác nhận ngày B | Báo cáo kho thuộc B; doanh thu theo định nghĩa OrderDate | Chưa chạy |
| REP03 | Tổng QuantityChange so tồn; kiểm tra hủy; nhập/xuất giá lịch sử | Khớp từng sản phẩm, tổng và chi tiết | Chưa chạy |
| UI01 | Desktop/mobile, form lỗi, báo cáo/in A4, dữ liệu lớn | Đọc được, không mất dòng/đè chữ | Chưa chạy |

Chỉ dùng dữ liệu kiểm thử được cho phép. Không xóa hoặc reset database thật để chạy các ca này.

## Kiểm tra SQL chỉ đọc

SQL Server 16.0.1000.6; __MigrationHistory có 2 migration tương ứng file source. 1 tài khoản, 0 role. 20 giao dịch; truy vấn đối chiếu Product.QuantityInStock với tổng QuantityChange: 0 sản phẩm lệch. Không chạy migration hoặc thay đổi dữ liệu. Fetch origin thành công; baseline trùng origin/master.
