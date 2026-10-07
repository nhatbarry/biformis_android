# Màn boss — Level Story 6

Boss có 15 HP: phase 1 hiển thị 7/7, bất tử khi đóng băng và triệu hồi 5 loại quái, sau đó phase 2 hiển thị 8/8. Quái chết hết mới phá băng. Thắng boss tự chuyển sang Ending, không có cửa. Arena không có tường, camera cố định và giới hạn vị trí di chuyển trong khung chơi.

## Phase 1

Giữ asset trùm mũ và đạn tròn đỏ/xanh của enemy cũ (`R2.png`, `B2.png`). Ba pattern random không lặp ngay: 5 làn dài, vòng tròn 32 viên, 5 cụm mỗi cụm 5 viên. Nhịp volley lần lượt 0.20 / 0.48 / 0.42 giây, giảm khoảng 60% so với bản dày ban đầu. Boss đi tự do ở tốc độ 1.45 unit/giây trong lúc nghỉ và tung chiêu.

Levitate gọi sét đỏ/xanh từ trên xuống. Chỉ dùng Lightning Down 1 và 2; mỗi điểm có vòng cảnh báo trước khi đánh xuống. Đạn và sét theo luật màu, dash bảo vệ người chơi.

Tiếp xúc boss dùng trigger nhận đòn rộng và collider chân rắn như enemy cũ. Một lần ram gây 1 damage, đẩy người chơi, hit-stop, shockwave, rung và flash trắng. Sau hit-stop xóa đạn đang bay. Phần đóng băng không nhận sát thương.

## Chuyển phase

Gói `boss-bang-vo-bien-hinh-aura.zip`: 12 frame / 2440 ms, canvas 414×442, PPU 85.33, pivot chân (192,93). Giữ frame 3 khi có quái. Sau quái cuối, zoom vào 0.65s, phát tiếp phá băng và biến hình. Frame 7 hiện tóc đỏ; aura chỉ ở frame 8 trong 100ms. Giữ pose cuối cầm dao không aura 0.35s rồi zoom ra 0.65s. Sau zoom out mới đánh tiếp.

Ẩn toàn bộ Canvas và raycaster khi zoom/biến hình, giữ GameObject của control để không ngắt virtual gamepad. Pause dừng animation và camera. Nếu controller bị tắt, phục hồi UI và constraints của player.

## Phase 2

`BossPhaseTwoCombat` sử dụng 4 Aseprite đã chốt. Giữ thời lượng frame, pivot chân và PPU; chỉ bỏ viền tối ở mép nhân vật. Dao đổi đỏ/xanh theo màu chiêu bằng shader mask riêng từng frame.

| Chiêu | Hành vi |
|---|---|
| 01_VungDaoNgang | Báo `!` có màu trên đầu. Theo hướng player trong thời gian báo, khóa khi vung; sector 120°, bán kính 2.55. Boss vẫn di chuyển. |
| 02_TuLucTayTrong | Đứng tụ năng lượng có màu trên đầu; khoát tay phóng 3 viên ra 3 hướng, xòe khoảng 60°. Hướng chung về phía player, lệch random ±8° cả cụm và ±3° từng viên; không nhắm chính xác hay đuổi theo. Tốc độ 36 unit/s, trail màu 0.14s. |
| 03_DamLao | Trigger toàn arena. Báo `!`, theo mục tiêu đến lúc lao, đâm xuyên vị trí đó và vượt 1.6 unit nếu còn chỗ. Không chỉnh hướng trong lúc lao. |
| 04_ChemDocTayTrong | Đứng tụ lực và chém một lần, phóng 4 aura rộng 5.4 unit cách nhau 0.34s. Mỗi aura random đỏ/xanh riêng. |

Cận chiến và tầm xa cùng luật: cùng màu không đau, đối màu mất 1 HP, dash miễn sát thương. Mỗi đòn cận chiến gây tối đa một hit. Đạn nhanh và đâm lao dùng swept collision để không xuyên player giữa các frame.

Boss phase 2 roaming ở tốc độ 2.8 unit/s, đan xen đuổi và vòng sang bên. Đổi mục tiêu di chuyển sau 0.65–1.8s. Chỉ tụ lực bắn và chém dọc giữ vị trí; lao có chuyển động riêng. Khoảng nghỉ giữa các đòn random 0.45–1.45 × `restSeconds` (mặc định 1s). AI không lặp chiêu vừa dùng khi có lựa chọn khác.

## Player, UI và sửa gameplay

Player có 10 HP; thanh máu 10 điểm cạnh avatar, không dùng HP hover cũ. Control opacity 0.75: dash trên, đổi màu dưới bên trái cụm nút phải, `!` dưới bên phải và chỉ xuất hiện khi tương tác được. Avatar hội thoại thường và Ending đều ẩn, text dùng lại khoảng trống. Avatar gameplay vẫn giữ.

Nhân vật được bỏ viền tối ngoài cùng ở source sprite, giữ nét tối bên trong. `StageActor` cũng ngừng thêm UI Outline. Aseprite được dựng lại vùng cắt/UV, giữ ID sprite và pivot để sửa lỗi phản diện rời mảnh khi atlas thay đổi cách xếp.

Hành lang dùng Map Bound 40×53 để Cinemachine theo player trên điện thoại rộng. Bound cũ 22 có thể nhỏ hơn frustum lens của Cinemachine trước khi CameraFraming hiệu chỉnh và khiến camera đứng ở giữa màn.

Luneblade tìm đường vòng cục bộ qua tường (ô 0.6 unit, tối đa 384 node), tái dùng đường trong 0.8s và tách collider bị lún vào tường. Vùng nhận đòn 1.4×1.9, offset y=0.55, phủ xuống chân để player có thể ram từ dưới/bên hông trước khi collider chân chặn lại. Đổi màu/dash giữ layer Player Collider riêng cho box va tường.

## Editor và kiểm tra

- `PlagueDoctorBoss → Preview Phase Two (Play Mode)` xem nhanh phase 2; prefab vẫn bắt đầu ở phase 1.
- `Biformis → Configure Boss Phase Two Assets` import/bake frame và timing.
- `Biformis → Configure Boss Phase Two Combat` gắn mask dao, effects và roaming.
- `Biformis → Repair Character Sprite Packing` dựng lại vùng cắt/UV khi thay nguồn Aseprite; giữ ID và pivot.
- PlayMode kiểm tra collision/ram, màu/dash, quái cuối, chuyển phase/ẩn UI/zoom/pause, ba pattern, bốn moveset, roaming, hành lang chuyển từ màn 4 với máu còn lại và quái vòng góc tường.
