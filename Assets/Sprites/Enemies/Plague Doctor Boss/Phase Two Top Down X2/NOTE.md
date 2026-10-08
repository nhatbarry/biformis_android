# NOTE — Boss phase 2 top-down, dao dài ×2

20 animation: 4 hướng Down/Right/Up/Left × (Đi chuyển, Vung dao ngang, Tụ lực tay trống, Đâm lao, Chém dọc). Mỗi animation 9 frame, PNG một hàng, Aseprite, GIF và APNG; source-grid lưu artwork gốc.

Camera nhìn từ trên xuống 3/4: thấy đỉnh tóc và vai, thân/chân rút ngắn theo phối cảnh. Hướng Up là nhìn lưng, mặt không hiện xuyên tóc. Giữ tóc đỏ, mặt mint, mắt xanh nhỏ, cổ ngắn, áo than/xám, mũ gập sau cổ và kiểu dao hiện tại. Dao dài gấp đôi, không tăng bề rộng hoặc thêm bộ phận mới.

Hai asset bại trận đứng yên/thở nhẹ và tan biến giữ nguyên trong gói trước, không nằm trong bộ mới này. Các animation phase 1 và chuyển phase không được thay thế bởi gói combat phase 2 này.

## Import và pivot

Tất cả file dùng chung canvas 749 × 564 px và pivot (336, 426) tính từ góc trên trái. Pivot Unity: (0.448598, 0.244681). PNG là 9 ô ngang. Multiple, Grid By Cell Size, không Trim/Crop từng frame; Filter Point, Compression None, Mip Maps Off. Đặt Max Texture Size đủ lớn để giữ nguyên kích thước sheet; giá trị có trong settings.json.

PPU đề xuất trong settings từng clip giữ chiều cao tư thế chuẩn tương ứng boss 2.5 lần trước đó. Các file giữ độ phân giải artwork tạo; không upscale thêm 2.5 lần nữa.

## Chiêu và thời gian

- Đi chuyển: loop 800 ms, bước tại chỗ; Unity di chuyển root theo hướng. Frame đầu và cuối giống nhau.
- Vung ngang: SlashHit tại frame 5 (480 ms), tổng 1060 ms. Tàn ảnh trắng có sẵn.
- Tụ lực tay trống: SpawnRangedSkill tại frame 7 (980 ms), tổng 1400 ms. Dao hạ xuống, tay còn lại phóng chiêu; projectile do Unity tạo riêng.
- Đâm lao: BeginDash frame 4 (470 ms), StabHit frame 5 (510 ms), EndDash frame 7 (620 ms), tổng 1150 ms. Sprite giữ pivot; Unity điều khiển quãng lao. GIF di-chuyen-preview minh họa chuyển vị trí riêng.
- Chém dọc: SpawnSwordAura frame 6 (880 ms), tổng 1400 ms. Tay cầm dao giơ cạnh đầu rồi chém xuống; tay trống đặt ở bụng. Aura kiếm do Unity thêm riêng.

Frame đếm từ 1. Bật Loop Time cho Walk, tắt cho 4 attack. Animation Event gắn với frame tương ứng khi chỉnh tốc độ phát. Hitbox và quãng lao theo game; dao mới dài hơn nên cân chỉnh tầm đánh trong Unity.

Aseprite gồm một layer Artwork với timeline, tag và slice Pivot. Các bộ phận chưa tách layer riêng. PNG/Aseprite/APNG giữ RGBA; GIF giới hạn 256 màu. Bộ frame xuất dùng chính artwork mới, không dựng lại boss bằng các bộ pixel cũ. Mở preview.html để xem 20 GIF cạnh nhau, hoặc thư mục Previews để xem từng động tác đủ 4 hướng.

Tạo bằng built-in ImageGen; prompt.json lưu spec/master và các prompt animation.
