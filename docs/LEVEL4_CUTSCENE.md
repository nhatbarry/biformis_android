# Cutscene cuối màn 4 và hồi tưởng cứu đuối

Đã nối vào `Level4_End` ở cả hai bản Ink Việt/Anh và biên dịch lại JSON. Runtime tự chuẩn bị assets khi cast actor; không cần chạy lệnh setup hoặc sửa scene bằng tay.

- Cánh cửa ban đầu đóng. Bốn nhịp đập tăng lực, ba nhịp đầu để lại độ cong và vết nứt lớn dần; nhịp cuối phá cánh gỗ thành 30 mảnh bay vào phòng. Khung đá giữ nguyên.
- A mặc áo choàng, có màu tóc và dấu máu theo `Assets/Animations/A_Bloody.aseprite`. Bộ frame mới gồm ngã úp, nằm úp, chống tay/gối và lồm cồm đứng dậy. Sau đó dùng lại sprite A_bloody gốc với nhịp chân chậm để đi tới vị trí đưa hộp cũ `(-440,-28)`.
- Hộp ẩn trong toàn bộ đoạn phá cửa/ngã/đứng dậy/đi tới. Chỉ sau khi A tới nơi mới cast `Box` và attach vào tay. B vẫn do người chơi điều khiển tới nhận hộp bằng Interact như trước.
- Hộp được giải mã trực tiếp từ `outputs/cube-pixel-left-up-64-transparent.webp`: 64 frame, 80 ms/frame, vòng lặp 5.12 giây, giữ alpha.
- Các mặt hộp được tô đen đặc bằng mesh UI theo từng frame; viền trắng giữ nguyên nét pixel của nguồn. Vệt chuyển động bên ngoài vẫn giữ, vệt nằm trong mặt hộp được lớp đen che lại. `OpaqueBoxGraphic` bám frame hiện tại nên hộp vẫn quay và chuyển tay như trước.
- Kích thước hộp giảm còn 1/4 theo cả chiều rộng và chiều cao: 0.3125 canvas unit/pixel, ô frame 64×64 hiển thị 20×20 canvas unit. Mặt đen và viền trắng thu theo cùng tỷ lệ.
- Cảnh cứu đuối dùng toàn bộ `red-rescues-blue-preview.gif`: 40 frame, 100 ms/frame, vòng lặp 4 giây. Hai câu thoại giữ nguyên. Runtime dùng khung 640×360, thu toàn bộ preview 1672×941 bằng Lanczos, không cắt nội dung; chỉ tải atlas khi cast `Drown`.

Assets runtime: `Assets/Resources/Story Cutscene/Level4/`. Texture Point, Clamp, không mipmap, RGBA32 không nén; không giữ bản pixel CPU. `ARecoveryAtlas.png` có 12 ô 32×32, sprite đứng cao 17 pixel, đường tiếp đất y=24 để khớp A_bloody gốc.

Frame A được tạo bằng công cụ imagegen tích hợp, sau khi sửa bản đầu thiếu áo choàng/sai tỷ lệ. Bản được dùng nằm ở `outputs/level4-A-bloody-recovery-source.png`. Prompt cuối trong [LEVEL4_A_BLOODY_PROMPT.md](LEVEL4_A_BLOODY_PROMPT.md). `Tools/level4_cutscene_assets.py` chỉ giải mã, tách layer có sẵn và đóng gói frame; không vẽ lại hoặc đổi màu nhân vật.

Xuất lại media: `python Tools/level4_cutscene_assets.py`. Chỉ đóng gói lại frame A: `python Tools/level4_cutscene_assets.py --recovery-only outputs/level4-A-bloody-recovery-source.png`.

Theo yêu cầu, chưa chạy game, test hay build. Người dùng tự kiểm tra đoạn cuối màn 4: bốn nhịp cửa, tư thế/tỷ lệ A, vị trí tiếp đất, hộp xuất hiện sau khi A tới chỗ cũ, nhận hộp bằng Interact và GIF lặp khi hai câu thoại cứu đuối hiện.
