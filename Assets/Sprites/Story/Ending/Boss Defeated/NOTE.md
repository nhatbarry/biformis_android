# NOTE — Boss bại trận, thở nhẹ và tan thành bụi

- **boss-thua-tran-tho-nhe**: hai tay trống, đứng hơi rũ vai, chỉ thở nhẹ. 6 frame, loop êm; frame đầu và cuối khớp nhau. Không dao, aura, máu hay hiệu ứng hạt.
- **boss-dung-thang-tan-bui**: hai tay trống, đứng thẳng. Thân tan từ chân lên đầu thành hạt pixel trong bảng màu nhân vật. Phần còn lại giữ vị trí gốc, không rơi xuống hoặc thu nhỏ. 12 frame; frame cuối hoàn toàn trong suốt. Phát một lần.

Mỗi nhóm gồm Aseprite timeline, PNG một hàng, GIF, APNG, ảnh nguồn và settings.json. Aseprite có một layer Artwork chứa hình hoàn chỉnh, một tag cho animation và slice Pivot. Hạt bụi chưa tách thành layer riêng. PNG/Aseprite giữ RGBA từ bộ frame tạo; GIF dùng cùng pose và bảng màu 256 màu.

| Animation | Frame | Canvas mỗi frame | Pivot Unity | PPU đề xuất | Thời gian |
|---|---:|---|---|---:|---:|
| Defeated_Empty_Hands_Breathe | 6 | 604 × 593 px | (0.442053, 0.190556) | 156.16 | 1800 ms |
| Upright_Dust_Dissolve | 12 | 426 × 439 px | (0.450704, 0.132118) | 131.84 | 1830 ms |

## Unity

PNG: Sprite Mode Multiple, Slice Grid By Cell Size, không Trim/Crop canvas từng frame. Dùng pivot và PPU trong bảng/settings.json; PPU đề xuất giữ chiều cao tương ứng boss đã tăng 2.5 lần trước đó. Filter Mode Point, Compression None, Mip Maps Off.

Đặt giới hạn kích thước texture khi import đủ lớn để giữ nguyên PNG, tránh tự giảm độ phân giải của sheet. Kích thước ngang sheet bằng số frame × chiều rộng canvas trong bảng.

Bật Loop Time cho bản thở, tắt Loop Time cho bản tan bụi. GIF tan bụi lặp để xem trước; animation trong game phát một lần. Giữ boss đứng yên khi bắt đầu tan, không tiếp tục animation đi/đánh và không di chuyển root theo các fragment.

StartDissolve: frame 2, 280 ms. BossGone: frame 12, 1380 ms; lúc này hình đã biến mất hoàn toàn. Có thể tắt renderer/collider hoặc hủy đối tượng ở mốc này theo logic game.

Thời gian từng frame (ms):
- Thở nhẹ: 240, 280, 360, 280, 300, 340.
- Tan bụi: 280, 100, 100, 110, 110, 120, 110, 110, 120, 100, 120, 450.

Tạo bằng built-in ImageGen, prompt.json lưu các prompt đã dùng. Frame xuất chỉ căn pivot từ artwork hiện tại; không dùng lại các bản boss dựng pixel khác ở những gói trước.
