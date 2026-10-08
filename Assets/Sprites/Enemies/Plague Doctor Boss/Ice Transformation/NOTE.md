# NOTE — Băng vỡ, biến hình và aura

12 frame, tổng 2440 ms. Băng mọc bao kín boss, chuyển đỏ, vỡ tung; lộ dạng tóc đỏ cầm dao. Aura đỏ–trắng chỉ bùng một lần trong 100 ms rồi tắt hoàn toàn.

- Frame 1–3: hình thành băng quanh toàn thân.
- Frame 4–5: chuyển đỏ, nứt và lấy đà.
- Frame 6: băng vỡ (1190 ms).
- Frame 7: lộ dạng mới; có thể đổi phase boss ở 1240 ms.
- Frame 8: aura bùng mạnh (1310 ms), kéo dài 100 ms.
- Frame 9: aura tắt hoàn toàn (1410 ms).
- Frame 9–12: boss ổn định rồi giữ dáng cuối cầm dao chéo xuống, không aura.

Mũ vẫn gắn ở cổ áo và gập về sau. Mặt dạng mới để thoáng. Các hiệu ứng có sẵn nằm chung một layer Artwork cùng boss; không phải layer aura riêng.

PNG là một hàng 12 ô, mỗi ô 414 × 442 px. Aseprite có timeline, thời gian frame và slice Pivot. Pivot từ góc trên trái: (192, 349). Pivot Unity: (0.463768, 0.210407). PPU đề xuất: 85.33 để giữ chiều cao dạng cuối tương ứng boss 2.5 lần trước đó.

Unity: Multiple, Slice Grid By Cell Size, Filter Point, Compression None, Mip Maps Off. Tắt Loop Time cho cảnh biến hình, kết thúc thì chuyển sang các animation boss cầm dao đã chốt. Event ChangeBossPhase ở frame 7 dùng đổi logic/chỉ số phase; tiếp tục phát clip đến cuối để không cắt mất nhịp aura ở frame 8. Không trim canvas từng frame khi xuất từ Aseprite.

GIF xem trước dùng cùng các frame nhưng có bảng màu 256 màu. PNG/Aseprite/APNG giữ RGBA của ảnh tạo. source-grid.png lưu bản gốc. Tạo ảnh bằng built-in ImageGen; prompt.json lưu prompt đã dùng.
