# Tổng kết kiểm thử cuối — HUIT CNTT_DACN_05

Chạy mới ngày 22/10/2026, 09:44–10:03 (UTC+07), trên working tree nhánh codex/dacn05-completion, commit nền 58b2d5183847583dae5f9b44f00be1727f2a1958.

Fingerprint source 235 file trước/sau đều là **78a4b4eb549631008103d19096f954a169a871c3a90b4718b936f903d6f08450**. DLL bản sao IIS Express trùng DLL vừa build. Không dùng kết quả kiểm thử cũ để chứng nhận.

| Hạng mục | Kết quả |
|---|---|
| Rebuild Debug + Razor | PASS |
| Smoke | 61 PASS |
| Integration EF6/SQL | 230 PASS |
| HTTP hiện có và bổ sung CRUD/nghiệp vụ | 292 PASS |
| JavaScript customer/admin/menu | 38 PASS (11 + 17 + 10) |
| Đối chiếu tổng báo cáo/SQL | 14 PASS |
| Identity reset trên DB riêng | 19 PASS |
| Lỗi chức năng chưa xử lý trong ca đã chạy | Không phát hiện |
| Chrome UI thật, hover/mobile/card responsive trực quan | NOT TESTED |
| Print preview A4, phân trang trực quan, máy in | NOT TESTED |

Môi trường: Windows, VS 2022/MSBuild 17.14.60, .NET Framework 4.8.1, SQL Server 2022 (16.0.1000.6), IIS Express. Test chỉ ghi DB Dacn05_Test_* / AdminReset_Test_* riêng. Không ghi DB thật hoặc thay tài khoản Admin thật. Lỗi xác thực SQL trong sandbox được lưu riêng; chạy lại ngoài sandbox đã PASS, không coi lần lỗi môi trường là PASS.

Phạm vi đã chạy gồm login/logout, bốn vai trò, CRUD sản phẩm/khách/nhân viên/NCC, nhập/xác nhận, đơn nhiều dòng, xác nhận lặp, thiếu hàng, Complete, Cancel và hoàn tồn, doanh thu/nhập/xuất/tồn khớp SQL, CSRF/token/quyền sai, lọc đúng/sai/rỗng, menu URL không lẫn danh mục, card/sắp xếp quyền, Hủy/Escape không submit trong DOM mô phỏng. Không đánh đồng test DOM với Chrome UI thật.

Chốt Git: code ứng dụng được đối chiếu theo nội dung chuẩn hóa xuống dòng với manifest đã kiểm thử. Thay đổi đóng gói chỉ gồm tài liệu, .gitignore và DefaultConnection mẫu không chứa tên máy/SQL cá nhân. Web.config trên máy chủ đồ án giữ nguyên. Do cấu hình SQL được làm portable, bản Git không có cùng fingerprint thô của toàn bộ môi trường test; không tuyên bố đã chạy lại 230/292 ca trên cấu hình portable hoặc máy mới. Build/Razor trên source xuất được ghi bổ sung sau khi kiểm tra.

Commit remote ae139f1bc7e71e70fd313c75eb0df8bb9c83f3c8 chỉ bổ sung test/tài liệu cũ. Lịch sử và các file riêng của commit này được giữ; IntegrationTests.cs và Run-UiTests.ps1 dùng phiên bản workspace mới đã chạy 230/292 ca, thay thế kỳ vọng checkout một sản phẩm cũ. Các ghi chép Chrome/PDF ngày 21/10 trong tài liệu cũ không chứng nhận UI mới.

Log/manifest đầy đủ và bản sao site/token/config thử được giữ cục bộ trong artifacts, không đưa lên GitHub hay gói source. Không chứa database, mật khẩu, bin/obj, packages hoặc browser profile.

## Commit chốt và kiểm tra bản xuất sạch

- Commit code đã chốt: **5126e1f62bc90c5f86a63861668148b62e506fe0**.
- 212 file ứng dụng (bao gồm Razor config) được đối chiếu với source đã kiểm thử, chỉ chuẩn hóa xuống dòng. Root Web.config dùng DefaultConnection portable; .csproj.user và serviceDependencies.local.json không được đóng gói.
- Cây source xuất từ index của commit code được Rebuild Debug + MvcBuildViews=true: **PASS**. 61 smoke trên chính bản xuất: **PASS**. Build dùng NuGet packages đã có qua junction tạm ở thư mục kiểm chứng; không khẳng định đã Restore NuGet trên máy sạch.
- Không chạy lại integration/HTTP trên SQL template portable hoặc máy nhận; các kết quả 230/292 nêu trên thuộc cùng code ứng dụng với cấu hình SQL thử đã được ghi trong manifest. Không tuyên bố hai môi trường có cùng fingerprint thô.
- Commit tài liệu bàn giao tiếp theo chỉ thay docs, giữ nguyên tree của OnlineSalesManagement và tests so với commit code này. Dùng git rev-parse HEAD ở bản GitHub để lấy commit đầu nhánh cuối cùng.
- Gói nộp là git archive của HEAD cuối, không chứa .git, packages, bin/obj, database/backup, artifacts hoặc cấu hình SQL cá nhân. Thư mục workspace vẫn giữ Web.config local nên không dùng nguyên workspace để nén.
