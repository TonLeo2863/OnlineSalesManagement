# Nội dung báo cáo và bàn giao

## Cấu trúc chuyển sang Word

Bìa: HUIT — Khoa Công nghệ thông tin; đồ án chuyên ngành CNTT_DACN_05; đề tài Xây dựng hệ thống bán hàng trực tuyến; sinh viên/MSSV/lớp/giảng viên/thời gian để trống cho người thực hiện điền. Tiếp theo: lời cảm ơn, cam đoan, nhận xét, mục lục, danh mục hình/bảng và từ viết tắt theo mẫu khoa. Không tự tạo chữ ký hoặc nhận xét giảng viên.

Chương 1 — Tổng quan: nêu nhu cầu quản lý bán hàng đa kênh, mục tiêu thống nhất danh mục/đơn/tồn và lưu lịch sử; xác định phạm vi một kho, kênh Counter/Online, vai trò Admin/Kho/BanHang/khách. Ghi rõ doanh nghiệp giả định và chưa khảo sát thực tế.

Chương 2 — Cơ sở lý thuyết: trình bày kiến trúc MVC, controller/action/filter, Razor và model binding; EF6 Code First, khóa ngoại, migration, transaction, RowVersion; Identity2/cookie/role và AntiForgeryToken. Dẫn tài liệu chính thức đã thực sự đọc khi hoàn thiện bản nộp; không tạo danh mục tài liệu tham khảo giả. Phân biệt .NET Framework MVC5 với ASP.NET Core.

Chương 3 — Khảo sát, phân tích và thiết kế: dùng ANALYSIS_AND_DESIGN.md, biểu mẫu khảo sát, quy trình nhập/xuất/hủy, đặc tả use case, lớp và ERD theo source. Giải thích tồn nằm trong Product và sổ InventoryTransaction; phân biệt ngày tạo với ngày giao dịch kho, doanh thu đơn với tiền thực thu.

Chương 4 — Cài đặt: giới thiệu solution, MVC5/.NET Framework 4.8.1 thực tế, EF6/SQL2022/Identity2/Bootstrap. Giải thích Input DTO và giao dịch trong hai service kho; server lấy giá; quyền phía server; bảo vệ owner online. Trình bày màn hình bằng ảnh chụp thật sau khi chạy, chú thích phiên bản commit/ngày/role và dữ liệu thử.

Chương 5 — Kiểm thử và đánh giá: đưa ma trận yêu cầu, môi trường, kịch bản và kết quả từ TEST_RESULTS.md. Tách build/smoke/integration/HTTP/UI; đánh dấu chưa chạy thay vì suy diễn thành công. Minh họa ca vượt tồn, xác nhận lặp, RowVersion, đơn chéo owner và mốc ngày khác ngày giao dịch.

Chương 6 — Kết luận và hướng phát triển: đánh giá theo từng tiêu chí đã có bằng chứng. Hạn chế: chưa khảo sát thực, thanh toán mô phỏng, chưa hóa đơn điện tử pháp lý, một kho, luồng online tối thiểu, kiểm thử tải/in thực tế còn tùy kết quả. Hướng phát triển: đối soát thanh toán, trả hàng, phân trang/tải lớn, báo cáo chốt kỳ, nhiều kho; ca trực chỉ triển khai ở đợt riêng.

Phụ lục: hướng dẫn cài đặt, sử dụng, test case, source sơ đồ Mermaid, danh sách ảnh và nhật ký tiến độ/commit. Có thể chuyển Markdown sang Word theo mẫu khoa; đây là nội dung có cấu trúc, chưa phải file DOCX đã dàn trang.

## Kế hoạch 10 tuần

Đây là kế hoạch đề xuất; không phải nhật ký khẳng định đã thực hiện 10 tuần.

| Tuần | Công việc | Sản phẩm / điều kiện nghiệm thu |
|---|---|---|
| 1 | Xác định đề tài/phạm vi, khảo sát | Biên bản hoặc ghi rõ giả định, danh sách biểu mẫu |
| 2 | Phân tích yêu cầu/use case | Ma trận tiêu chí, luồng/ngoại lệ, phân quyền |
| 3 | Thiết kế dữ liệu/kiến trúc | ERD/lớp đúng source, ràng buộc và migration |
| 4 | Identity và dữ liệu cơ bản | Role/CRUD/token/validation được kiểm thử |
| 5 | Nhập kho | Nháp/xác nhận/hủy, lịch sử, rollback và trùng |
| 6 | Bán/xuất và chứng từ | Giá snapshot, không âm tồn, hủy, in nội bộ |
| 7 | Online | Khách chọn/đặt/xem đơn riêng, chặn sửa giá/quyền |
| 8 | Báo cáo | Tồn/doanh thu/nhập/xuất, ngày đúng, A4 |
| 9 | Kiểm thử tích hợp/UI | Kết quả thật, xử lý lỗi, đối chiếu số dư |
| 10 | Báo cáo/demo/bàn giao | Word/PPT theo mẫu, ảnh thật, setup, hạn chế |

Tiến độ thực tế xem CODEX_PROGRESS.md; từng tuần chỉ đánh dấu hoàn thành khi có kết quả/bằng chứng.

## Hướng dẫn sử dụng theo vai trò

- Khách: đăng ký/đăng nhập → Cửa hàng trực tuyến → tìm sản phẩm → Chọn mua → số lượng/người nhận/địa chỉ → đặt COD → Đơn của tôi. Đơn Pending chưa giữ hàng; nhân viên xác nhận sau. Luồng tối thiểu đặt một sản phẩm mỗi đơn, không giỏ nhiều sản phẩm.
- Kho: sản phẩm/danh mục/NCC → tạo phiếu nhập với nhân viên và dòng hàng → kiểm tra bản nháp → xác nhận → kiểm tra tồn/lịch sử. Không dùng CRUD sản phẩm để nhập số dư. Hủy chỉ khi service chấp nhận.
- Bán hàng: khách hàng → tạo đơn Counter/Online theo nghiệp vụ → chọn sản phẩm/số lượng → lưu Pending → xác nhận khi đủ hàng → xem/in chứng từ. Không xác nhận lại hoặc xóa lịch sử; dùng hủy đúng trạng thái.
- Admin: quản lý nhân viên, cấp role trong Người dùng, xem 200 hoạt động kho gần nhất, báo cáo. Màn hình role chọn một vai trò chính; không tự gỡ Admin của mình/last Admin. Nhật ký kho chưa bao phủ mọi thao tác tài khoản.

## Dàn ý thuyết trình 15 phút

| Slide | Thời lượng | Nội dung / lời dẫn |
|---|---|---|
| 1 | 30 giây | Tên đề tài, tác giả, giảng viên; mục tiêu demo |
| 2 | 60 giây | Bài toán, mô hình doanh nghiệp giả định và phạm vi |
| 3 | 60 giây | Vai trò/use case; server kiểm soát quyền |
| 4 | 60 giây | MVC5/EF6/Identity2, phân lớp controller/service/data |
| 5 | 90 giây | ERD và tồn = tổng giao dịch; giá snapshot |
| 6 | 120 giây | Demo nhập nháp → xác nhận → tồn tăng; chặn xác nhận lặp |
| 7 | 120 giây | Demo đặt online → nhân viên xác nhận → đơn riêng/in nội bộ |
| 8 | 90 giây | Demo vượt tồn và hủy hoàn tồn; rollback/RowVersion |
| 9 | 90 giây | Bốn báo cáo, định nghĩa ngày và doanh thu/tiền thu |
| 10 | 90 giây | Kết quả build/test thật; đối chiếu ledger; giới hạn kiểm thử |
| 11 | 60 giây | Hạn chế/hướng phát triển và bàn giao |
| 12 | 30 giây | Kết thúc, chuyển sang câu hỏi |

Tổng 15 phút. Chuẩn bị dữ liệu thử và cửa sổ đăng nhập từng role trước demo. Đây là dàn ý chuyển sang PowerPoint, chưa phải file PPTX được dựng/render.

## Danh sách ảnh cần chụp thật

1. Login đúng/sai và chặn URL trái quyền (che email cá nhân nếu cần).
2. Người dùng/role và thông báo chặn gỡ last Admin.
3. CRUD sản phẩm, mã trùng, form không sửa tồn, xung đột RowVersion.
4. Danh mục, khách, nhân viên, NCC và thông báo xóa có liên kết.
5. Phiếu nhập nháp, xác nhận, tồn/lịch sử trước-sau, xác nhận lặp.
6. Pending, xác nhận bán, vượt tồn, hủy/hoàn và ledger.
7. Cửa hàng, đặt online, đơn của tôi, chặn xem đơn khách khác.
8. Chứng từ nội bộ xem/in A4, trạng thái hợp lệ.
9. Tồn và cảnh báo, doanh thu tách kênh, nhập/xuất theo TransactionDate, khoảng ngày lỗi/không dữ liệu.
10. Build, smoke/integration console với kết quả thật và Git commit; không chụp secret/connection string hoặc database thật để nộp Git.

Ghi dưới mỗi hình: mã test, bước thực hiện, kỳ vọng, kết quả, ngày, commit và dữ liệu thử. Chưa có ảnh thì để ô chờ, không dựng ảnh giả.
