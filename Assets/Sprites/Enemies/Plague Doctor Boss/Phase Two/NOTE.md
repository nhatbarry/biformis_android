# NOTE — 4 động tác boss với dao đã chốt

## Nội dung

1. **01_VungDaoNgang.aseprite — 9 frame:** lấy đà, vung dao ngang nhanh, tàn ảnh trắng, thu tay. Bám hình vung dao đã chốt.
2. **02_TuLucTayTrong.aseprite — 8 frame:** tay không cầm dao giơ lên tụ lực rồi phóng tay ra phía trước; tay cầm dao giữ mũi dao chéo xuống. Dùng cho chiêu đánh xa. Đã giữ tám pose đầy đủ của bộ frame, bỏ pose chuyển tiếp bị cắt bàn tay ở mép ảnh nguồn.
3. **03_DamLao.aseprite — 9 frame:** thu mình lấy đà rồi đâm lao về phía trước. Di chuyển vị trí boss bằng Unity; sprite được căn theo pivot chân.
4. **04_ChemDocTayTrong.aseprite — 9 frame:** tay phía trong/xa camera cầm dao, giơ dao cạnh đầu rồi chém xuống. Mặt luôn nhìn thấy; tay phía ngoài/gần camera đặt trước bụng tụ lực. Aura kiếm sẽ thêm riêng trong Unity.

Mỗi file có timeline, thời gian từng frame, một tag bao toàn bộ animation và slice tên **Pivot**. Ảnh RGBA có nền trong suốt. Một layer **Artwork** chứa sprite hoàn chỉnh, gồm cả dao và tàn ảnh có trong hình đã chốt. Các bộ phận chưa tách thành layer riêng.

## Kích thước và pivot

| File | Frame | Canvas (px) | Pivot từ góc trên trái (px) | Pivot Unity | PPU đề xuất | Tổng thời gian |
|---|---:|---|---|---|---:|---:|
| 01_VungDaoNgang.aseprite | 9 | 595 × 496 | (226, 427) | (0.379832, 0.139113) | 144.21 | 1060 ms |
| 02_TuLucTayTrong.aseprite | 8 | 481 × 474 | (216, 409) | (0.449064, 0.137131) | 148.05 | 1340 ms |
| 03_DamLao.aseprite | 9 | 529 × 497 | (251, 415) | (0.474480, 0.164990) | 124.16 | 1150 ms |
| 04_ChemDocTayTrong.aseprite | 9 | 499 × 514 | (218, 426) | (0.436874, 0.171206) | 119.04 | 1400 ms |

Canvas mỗi animation có kích thước riêng để chứa hết dao và tàn ảnh. Dùng đúng pivot từng file khi chuyển animation. PPU đề xuất giữ chiều cao tư thế chuẩn tương ứng boss lớn 2.5 lần trong bộ trước; không nhân tỷ lệ thêm 2.5 lần nữa.

## Nhịp frame và sự kiện chiêu

Frame trong note đếm từ **1**. Thời gian sự kiện là thời điểm bắt đầu frame, tính từ đầu animation.

- **01_VungDaoNgang.aseprite**: thời gian từng frame (ms): 160, 110, 160, 50, 60, 90, 100, 110, 220. `SlashHit` ở frame 5, 480 ms.
- **02_TuLucTayTrong.aseprite**: thời gian từng frame (ms): 180, 120, 140, 220, 260, 60, 140, 220. `SpawnRangedSkill` ở frame 6, 920 ms.
- **03_DamLao.aseprite**: thời gian từng frame (ms): 180, 110, 180, 40, 50, 60, 140, 130, 260. `StabHit` ở frame 5, 510 ms.
- **04_ChemDocTayTrong.aseprite**: thời gian từng frame (ms): 180, 120, 160, 320, 100, 40, 100, 140, 240. `SpawnSwordAura` ở frame 6, 880 ms.

Riêng đâm lao: **BeginDash** ở frame 4 (470 ms), **StabHit** ở frame 5 (510 ms), **EndDash** ở frame 7 (620 ms). Hướng, khoảng cách, tốc độ lao và hitbox do Unity điều khiển. Đặt Animation Event ở đúng mốc chiêu; khi đổi tốc độ phát, sự kiện tiếp tục gắn với frame tương ứng.

Các animation là hành động một lần: trong Unity tắt **Loop Time**, phát xong quay về idle/di chuyển. Việc giữ tư thế tụ lực lâu hơn có thể thực hiện ở frame 4–5 của animation 02, hoặc frame 4 của animation 04.

## Xuất PNG để dùng trong Unity

Xuất sprite sheet từ từng Aseprite thành **một hàng ngang**, giữ nguyên kích thước canvas của từng frame; không Trim/Crop từng frame. Slice theo Grid By Cell Size với kích thước trong bảng. Chọn Sprite Mode Multiple, Filter Mode Point, Compression None, tắt Mip Maps. Dùng pivot và PPU tương ứng từng file.

PNG/Aseprite giữ pixel của bộ hình chốt; không thay bằng các bản boss dựng lại trước đây. Gói này chỉ chứa bốn Aseprite cuối cùng và note.
