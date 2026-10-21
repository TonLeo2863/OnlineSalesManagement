# Xác minh snapshot source đóng gói

Nguồn: worktree `codex/dacn05-completion`, commit nền 58b2d51 và thay đổi chưa commit được liệt kê trong manifest. Không dùng master.

`tests/Export-HandoverSource.ps1` tạo ZIP source draft, thư mục source riêng và SOURCE_MANIFEST.json: hash workspace và hash từng file trong gói. Khác biệt Web.config được khai báo: thay instance cục bộ thành `.\SQLEXPRESS` với Windows Authentication; người nhận chỉnh instance/catalog theo hướng dẫn. Không có mật khẩu mặc định.

Quy trình kiểm chứng source draft:

1. Kiểm hash từng file trong ZIP với manifest, không có .git hoặc runtime artifacts.
2. Build/Razor và smoke/integration trên thư mục source draft, dùng cache packages hiện có qua junction cục bộ. Cache, junction, bin/obj/artifacts phát sinh không nằm trong ZIP.
3. Đối chiếu hash file source draft sau kiểm thử với manifest của ZIP. Lưu record SourceStable và kết quả vào artifact cạnh ZIP.
4. Chạy HTTP/browser trên source draft với DB `Dacn05_Test_<GUID>` riêng và IIS Express bản sao; không dùng database demo.

Kết quả thật được ghi trong file `VALIDATION_RESULT.json` cạnh ZIP sau khi chạy. Tài liệu này mô tả quy trình, không tự chứng nhận các bước đã PASS trước khi có record. Không kiểm thử restore NuGet qua Internet trên máy sạch; không thử restore .bak vì người vận hành chưa cung cấp backup.

Gói mang tên SOURCE_DRAFT để tránh nhầm với `CNTT_DACN_05_BuiCongDanh.zip` đầy đủ. Sau khi tìm/tích hợp bản giỏ và có Admin/backup/PDF ký, phải tạo snapshot và chạy kiểm thử lại; manifest hiện tại không chứng nhận source thay đổi về sau.
