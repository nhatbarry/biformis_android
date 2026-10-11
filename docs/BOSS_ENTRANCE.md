# Cảnh đường ống trước trận boss

Màn 5 → tải `Level Story 6` → A&B chồng cùng vị trí chạy từ cạnh trái sang phải, nhấp nháy đỏ/xanh → phản diện đeo mặt nạ xuất hiện → sáu câu thoại tạm → boss phẩy tay bằng clip Cast của phase 1 → sàn đấu thật lan từ vị trí boss ra toàn màn hình → bật trận đấu.

Cảnh được dựng trong màn 6 đã tải sẵn, nên đoạn triệu hồi không có scene transition hoặc loading overlay. Khi chết và chơi lại màn 6, bỏ qua cảnh này. Vào bằng cốt truyện, tiếp tục save hoặc chọn màn 6 sẽ xem cảnh; các test tải riêng arena ngoài StoryFlow giữ hành vi trực tiếp vào trận.

Thoại tạm nằm trong knot `BeforeBoss` của cả hai file `Assets/Ink Dialogue/Biformis_Story*.ink`. Thay sáu dòng `......`, giữ tag speaker và layout.

Nền dùng built-in ImageGen, lưu tại `Assets/Sprites/Story/Boss Entrance/Pipe Interior.png`. Giữ ảnh nguồn, import Point, không mipmap, không nén và giới hạn texture 256 để phù hợp mật độ pixel của game.

## Xem và sửa

Mở scene `Core`, bấm Play rồi chọn màn 6 trong menu chọn màn để xem toàn bộ cảnh. Bấm tiếp tục hội thoại như các đoạn thoại trước. Chết và chơi lại boss sẽ vào trận ngay.

Trong `Level Story 6`, object `Boss Entrance Sequence` chứa thời gian chạy, xuất hiện, nhấp nháy và triệu hồi. Sau khi thay nguồn animation hoặc biên dịch lại thoại, dùng menu `Biformis > Boss Entrance > Configure` để cập nhật các tham chiếu.

## Kiểm tra

33 kiểm tra PlayMode liên quan đã qua, gồm 7 kiểm tra cutscene mới, hội thoại VI/EN, trận boss và luồng cốt truyện đến menu sau đoạn kết. Các ảnh tại `Logs/Boss Entrance QA/` đã được xem ở tỷ lệ 16:9 và 20:9; kiểm tra cả việc khôi phục joystick, bỏ qua cảnh khi chơi lại và không tải scene trong đoạn triệu hồi. Chưa kiểm tra trên thiết bị Android thật.

Kết quả nằm tại `Logs/boss-entrance-regression.xml` và `Logs/boss-entrance-final-tests.xml`; lần chạy cuối xác nhận lại 23 trường hợp sau khi cập nhật các kiểm tra cũ và giữ nguyên cấu hình bộ đếm lượt đọc Ink.

## Prompt tạo nền

```text
Use case: stylized-concept
Asset type: finished background sprite for the Biformis 2D pixel-art game, 16:9 landscape, 1600x900 output with the intentional visual simplicity of a 160x90 logical pixel canvas enlarged by exactly 10. Create a NEW background; the attached images are style references, not edit targets.
Input images: Image 1, DungeonRoom, is a side-on staging and coarse pixel-density reference; Image 2, Level Story 5 start, is the game's industrial dark navy and muted teal palette reference. Do not reproduce its red pods or character.
Primary request: the INSIDE of a single very large horizontal industrial pipe, seen from the side, forming a tunnel through which small game characters will run from the left edge toward the right.
Composition: strictly side-on horizontal view, no central vanishing-point tunnel or view looking down the pipe. We are inside the cylinder, its dark curved metallic back wall extending horizontally edge to edge. Broad muted teal/steel pipe reinforcement ribs and joint rings at intervals, a few simple bolts and panel seams, dark navy recesses, restrained cyan edge highlights. Keep the centre open and quiet for separately animated characters. A flat walkable maintenance strip inside the lower pipe sits at logical y=57 pixels from the top, leaving 33 pixels of pipe foreground below it which may be covered by dialogue. The background covers the full frame, extends cleanly to both horizontal edges, and has NO outer black frame.
Style/medium: match the reference game: very coarse crisp square pixel art, large simple flat-colour shapes, limited palette roughly 12 colours, single logical-pixel edges, sparse block shadows. Main palette dark navy #121827, desaturated teal #315E68 and #43777C, pale restrained highlights #81B6AE. Dim industrial atmosphere. Keep background subdued so red/mint characters read clearly.
Constraints: scenery ONLY. No characters, no boss, no dialogue, no text, no UI, no logos, no watermark, no doorway, no dramatic explosion. No smooth gradients, no antialiasing, no painted texture or realistic reflections, no tiny high-resolution detail. One finished clean background image, not a sprite sheet or concept board.
```
