// =====================================================================
// BIFORMIS - Kịch bản chính (chuyển từ "KỊCH BẢN GAME.docx")
// Bản tiếng Anh: Biformis_Story_EN.ink - phải giữ đúng tên knot và biến như file này.
//
// Mỗi cảnh là một knot. Trong chế độ cốt truyện, knot được gọi theo thứ tự trong "Story Data".
//
// Speaker (tag #speaker, tên hiển thị nằm ở Resources/Localization/Strings.txt, key "speaker.<id>"):
//   A        - người em (hình thái Xanh)
//   B        - người anh (hình thái Đỏ)
//   Villain  - Phản diện (nhân cách thứ hai của B) - khi không đứng trên sân khấu thì màn hình ửng đỏ khi nói
//   Teen     - Thiếu niên (chính là A, dùng hình Blue Character)
//   Mom      - Mẹ
//   Doctor   - Bác sĩ
//   System   - Thông báo hệ thống (không hiện tên)
//   Narrator - Dẫn truyện cho các chỉ dẫn sân khấu (không hiện tên)
//
// Tag dàn cảnh (CutsceneStage):
//   #bg:white | black | hospital | past | RRGGBB[:giây]   màu nền (tên hoặc mã hex, kèm thời gian chuyển màu)
//   #cast:A,B,Teen | none                    ai đứng trên sân khấu (Box = chiếc hộp đen, B_Bed = B nằm trên giường,
//                                           Villain = phản diện có hình, lúc đó màn hình không ửng đỏ)
//   #anim:Nhân vật:clip[:giây]               đổi animation (tên tag Aseprite), có thể chờ vài giây rồi mới đổi
//       B_Bed: sleep | wake | pant | sit_idle   (wake -> pant -> sit_idle tự nối nhau)
//       B_Floor (B nằm trên sàn Màn 3): sleep | wake | pant | idle      Level3 = ảnh nền sàn Màn 3
//       Teen: idle | vanish      Villain: appear | idle | walk | stab
//       Cảnh mở đầu: Room, Lever (idle | open), Trapdoor (idle | open), CageFront, Spotlight,
//         Captives (idle | struggle | merge | merged | merged_red),
//         Villain_Op (appear_remote | idle_remote | talk_remote | walk_remote | press_remote | pull_lever),
//         Level1 = ảnh sàn Màn 1, Shaft = luồng sáng, B_Fall (fall | land | lie | getup | idle)
//       Bệnh viện: Hospital = phòng bệnh (B nằm thở, máy đo nhịp tim), Mom (idle | talk | cry), Doctor (idle | talk)
//       Sau Màn 3: ký ức sân trường Track, Vignette, B_FB, A_FB; hầm ngục Dungeon (cả căn phòng, #move để trượt camera:
//         x = 320 - 4 * camX), DRoom, Door (idle | bang), Gap (glow | glow_fade | blood_seep | blood_still),
//         Bed_D, Bed_Empty, Vent (closed | open), TeenB (idle | idle_faded | fade | vanish),
//         Villain_D (appear | idle | talk | walk | touch_door), Caption = dòng "bíp... bíp... bíp..."
//       Cuối Màn 4: hầm ngục tối DarkDungeon (đứng yên ở nửa có cửa), DarkRoom, A4 = A bị thương (idle | run),
//         B4 = B (idle | run), Box = chiếc hộp ký ức
//       Quá khứ: Bedroom = phòng ngủ (idle | bang = cửa rung), WallShadow = bóng mỏ chim trên tường,
//         BSit = B ngồi (hug | rock | up | shiver | slam | down)
//       Hồi ức hai anh em (WhiteRoom_3): Track, Vignette, B_FB (idle | run | hesitate | reach | touch | hold),
//         A_FB (idle | run | reach | wait | hold), Drown = đuối nước (intro | loop), Montage = lớn lên (m1 | m2 | m3 | m4),
//         CloseUp = cận cảnh nắm tay
//         Ai có clip talk thì tự diễn talk trong lúc chữ chạy (trừ khi đang diễn clip khác, vd Mẹ đang khóc)
//   #move:Nhân vật:x[,y][:giây]              dời nhân vật tới toạ độ x (hoặc x,y) (đơn vị canvas, 0 = giữa màn hình)
//   #struggle:Nhân vật:số lần                người chơi chạm / bấm Space / E đủ số lần để vùng vẫy (có thanh tiến độ)
//   #hold                                    giống #wait nhưng chờ tới khi sân khấu xong việc (đi sau #struggle)
//   #flip:Nhân vật:on|off                    lật ngang nhân vật (quay mặt sang phía kia)
//   #fade:RRGGBB[:giây] | #fade:in[:giây]    phủ cả màn hình một màu rồi bỏ ra (vd màu giấy F6F0E4)
//   #banging:Door:on|off                     A đập cửa liên tục: cửa rung, màn hình giật, chữ RẦM! khi cửa ngoài khung
//   #knock:Door:độ đậm                       một tiếng cộc... yếu (độ đậm 0-1 của chữ)
//   #attach:Vật:Nhân vật:dx,dy | #attach:Vật:none   vật đi theo nhân vật, lệch dx,dy (vd chiếc hộp trên tay)
//   #reach:Nhân vật:Mục tiêu:x tối đa[:key] người chơi tự đi nhân vật tới sát mục tiêu (phím trái/phải, A/D, giữ ngón tay
//                                           về phía muốn đi) rồi bấm Space / E / chạm để làm; đặt #hold ngay sau.
//                                           key = chữ nhắc trong Strings.txt (key / key_touch), mặc định stage.take
//   #alpha:Nhân vật:độ đậm[:giây]            làm mờ / hiện một nhân vật (vd bóng trên tường)
//   #shake:độ mạnh[:giây]                    rung màn hình (đơn vị canvas, 4 = 1 pixel)
//   #wait:giây                               ẩn khung thoại, chờ cho sân khấu diễn; các tag sau nó chạy khi hết chờ
//   #fx:shake | flash | red | fade_black | fade_white | fade_in
//   #sfx:beep | stop | thud                  beep = máy đo nhịp tim (lặp), stop = tắt âm lặp
//   #pixel:cut[:giây]                       chuyển cảnh ô pixel 2x2: cảnh đang có vỡ dần thành ô, lộ cảnh mà các tag sau dựng
//   #pixel:RRGGBB[:giây] | #pixel:in[:giây]  phủ màn hình bằng ô màu, hoặc gỡ ô ra
//   #layout:top | bottom                     khung thoại ở trên / dưới màn hình, giữ tới khi đổi (mỗi cảnh bắt đầu ở dưới)
// =====================================================================

// Số phút Màn 5 cho phép (khớp với LevelCountdown trong scene Màn 5).
VAR collapseMinutes = 2
VAR corridorBarkIndex = 0


// ---------------------------------------------------------------------
// MỞ ĐẦU - Cảnh 1: Bắt đầu
// ---------------------------------------------------------------------
=== Intro ===
Khốn kiếp! Lại quay lại rồi à? #speaker:B #bg:black #cast:Room,Lever,Trapdoor,Captives,CageFront #wait:0.8 #struggle:Captives:6 #hold #wait:0.5
Em nhớ là bọn mình đã chạy xa lắm rồi mà. #speaker:A
Xin chào! Chúng ta lại gặp nhau rồi! #speaker:Villain #fx:flash #cast:Room,Lever,Trapdoor,Captives,CageFront,Spotlight,Villain_Op #anim:Villain_Op:appear_remote #wait:0.8
Biến bọn tôi trở lại! Tôi không muốn làm nữa! #speaker:B
Bọn tôi sẽ trả tiền lại cho ông mà! Làm ơn thả bọn tôi ra! #speaker:A
Xin lỗi nhé! Ta e rằng hiện tại ta không thể thả hai người ra được. Với cả, ta làm gì có cách nào để tách hai người ra. #speaker:Villain
Hả? #speaker:B
Ta có ý này. Sao hai người không chấp nhận chuyện này đi nhỉ? Cứ đau khổ mãi về nó có được lợi gì đâu. #speaker:Villain
Đồ điên! Mấy chuyện này là do ông gây ra chứ ai. Đừng có rao giảng đạo lý nữa! #speaker:B
Đúng là nông cạn. Nói cho hai người biết, nghe lời ta thì được nhiều hơn là mất đấy. #speaker:Villain #anim:Villain_Op:press_remote #wait:0.7 #anim:Captives:merge #wait:1.2
Thôi được rồi, ta sẽ cho hai người thêm một cơ hội. Đừng coi đây là hình phạt mà hãy xem như là một bài học ta dành cho hai người đi.
#anim:Captives:merged_red #anim:Villain_Op:walk_remote #move:Villain_Op:-152:1 #wait:1 #anim:Villain_Op:pull_lever #anim:Lever:open:0.25 #anim:Trapdoor:open:0.34 #wait:0.6 #move:Captives:156,-260:0.6 #wait:0.9 #fx:fade_black #wait:1 #cast:Level1,Shaft,B_Fall #move:B_Fall:0,228 #anim:B_Fall:fall #fx:fade_in #move:B_Fall:0,-8:0.5 #wait:0.5 #anim:B_Fall:land #wait:1.6 #cast:Level1,B_Fall #wait:4.8
-> DONE


// ---------------------------------------------------------------------
// MÀN 2 và MÀN 3 - câu thoại nổi trên đầu nhân vật trong lúc chơi (PlayerBarks)
// ---------------------------------------------------------------------
// Khi bị trúng đòn
=== Bark_Hurt ===
Đau quá. #speaker:B
-> DONE

// Thỉnh thoảng, ngẫu nhiên
=== Bark_Pain ===
{~Mệt quá.|Tôi không muốn chạy nữa.|Làm ơn.} #speaker:B
-> DONE


// ---------------------------------------------------------------------
// KẾT THÚC MÀN 2 - mọi thứ nhoè đi, B ngất (hiệu ứng làm trong scene Màn 2)
// -> CẢNH - CĂN PHÒNG TRẮNG (lần 1)
// ---------------------------------------------------------------------
=== Level2_End ===
-> WhiteRoom_1

=== WhiteRoom_1 ===
Anh có sao không? #speaker:Teen #bg:white #cast:B_Bed,Teen #move:Teen:-84 #wait:1.8 #anim:B_Bed:wake #wait:2.6
Em là ai? Em làm gì ở đây? #speaker:B
Em đang luyện tập cho đội điền kinh của trường thì bị chấn thương. #speaker:Teen
Bác sĩ nói em không thể chạy được nữa, nhưng em vẫn cố chạy.
Em đã chạy và ngã không biết bao nhiêu lần, đau đớn lắm, và rồi em ở đây.
Em không biết tại sao em lại ở đây sao? #speaker:B
Vâng. Nhưng ở đây ấm áp và dễ chịu quá. #speaker:Teen
Em cảm thấy mình có thể chạy nhanh như bay ở đây.
Giá như ở ngoài kia cũng được như vậy thì hay biết mấy anh nhỉ?
Bị đạn bắn vào chắc đau lắm nhỉ? #speaker:Villain #anim:Teen:vanish #wait:0.7 #cast:B_Bed,Villain #bg:F4D6D6:1 #wait:0.6
Rốt cuộc ông muốn gì? #speaker:B
Chịu thôi. Ta chỉ mong ngươi nhanh chóng ngộ ra sự thật. #speaker:Villain
Đi theo ta là không có thiệt đâu.
Ý ông là sao? #speaker:B
Mọi đau khổ của con người đều đến từ sự bất lực khi không thể làm những việc ngoài phạm vi thể xác. #speaker:Villain #bg:EBB5B5:1.5
Sau khi thực hiện một vài nghiên cứu, ta đã nhận ra một điều. #bg:D68284:1.5
Ở hình thái tiến hoá cuối cùng, nhân loại sẽ hợp thành một, sử dụng chung một tư duy và không còn cơ thể vật lý. #bg:C8666B:1.5
Khi đó, chúng ta không thể bị thương, có thể bay nhanh như gió, du hành giữa các vì sao và mọi khả năng là vô tận. #bg:A8434B:1.5
Như vậy sẽ không còn sự bất lực, không còn sự nuối tiếc, không còn phân biệt đối xử nữa. #bg:86262F:1.5
Lúc đó kể cả có bị đạn bắn chắc cũng chẳng sợ nhỉ? #bg:641621:1.5
Nói tóm lại, sứ mệnh của ta là đưa ngày đó đến gần hơn với loài người.
Ngươi có tin là chỉ cần một công tắc, ta có thể ban cho ngươi cuộc sống mơ ước đó không?
Ông ta đang nói gì vậy chứ... #speaker:B #anim:Villain:walk #move:Villain:60:2 #wait:2 #anim:Villain:stab #wait:1.1 #fx:fade_black #wait:1 #cast:Level3,B_Floor #bg:131823:0 #fx:fade_in #wait:1.8 #anim:B_Floor:wake #wait:2.6
// -> Tỉnh dậy, BẮT ĐẦU MÀN 3.
-> DONE


// ---------------------------------------------------------------------
// CẢNH - SAU MÀN 3 -> CĂN PHÒNG TRẮNG (lần 2) -> BỆNH VIỆN
// ---------------------------------------------------------------------
=== AfterLevel3 ===
Hồi nãy anh mơ thấy một giấc mơ lạ lắm. #speaker:B #bg:F6F0E4:0 #fade:F6F0E4:0 #cast:Track,B_FB,A_FB,Vignette #fade:in:0.9 #wait:0.9
Anh lại mơ thấy những chuyện đó nữa à? #speaker:A
Sao em biết anh mơ thấy gì? #speaker:B
Hồi còn học trung học, anh bị chấn thương nên phải rút khỏi đội điền kinh của trường còn gì? Anh quên chuyện đó à? #speaker:A
[speed=0.4]... #speaker:B #flip:B_FB:on
Có vẻ ông ta muốn anh quên đi những chuyện đó... #flip:B_FB:off
Vậy anh có muốn quên đi không? #speaker:A
[speed=0.4]... #speaker:B #flip:B_FB:on
-> WhiteRoom_2

=== WhiteRoom_2 ===
Anh nhớ ra em rồi sao? #speaker:TeenB #fade:F6F0E4:0.9 #wait:0.9 #cast:Dungeon,DRoom,Door,Bed_D,Vent,TeenB #move:Dungeon:-320 #fade:in:0.7 #wait:0.7
Ờ. #speaker:B
Vậy anh thấy sao? #speaker:TeenB
Anh không biết mình nên có cảm xúc gì. Anh chỉ thấy mệt thôi. #speaker:B
Anh sẽ quên em sao? #speaker:TeenB
Anh không biết. #speaker:B
Đừng lăn tăn nữa. Cậu muốn trở lại làm kẻ cô đơn và thất bại đó sao? #speaker:Villain #cast:Dungeon,DRoom,Door,Bed_D,Vent,TeenB,Villain_D #anim:Villain_D:appear #wait:0.36 #anim:TeenB:idle_faded
Đừng nói nữa. #speaker:B
Chỉ cần một nút ấn thôi mà. Mau đi theo ta đi rồi. #speaker:Villain
Anh ơi. Anh có ở đó không? #speaker:A #banging:Door:on #cast:Dungeon,DRoom,Door,Bed_D,Vent,TeenB,Villain_D,Gap #anim:Gap:glow
A? #speaker:B
Anh ơi. Đừng có nghe hắn. Xin anh đấy. #speaker:A
Cút đi! Ngươi muốn nhìn hắn đau khổ sao? #speaker:Villain #flip:Villain_D:on #anim:Villain_D:walk #move:Villain_D:-408:2.6 #move:Dungeon:320:2.6 #wait:2.6 #anim:Villain_D:idle #cast:Dungeon,DRoom,Door,Gap,Bed_Empty,Vent,Villain_D #anim:Vent:open
Mở cửa ra cho ta. #speaker:A
Vẫn còn cố chấp à? Vậy thì chết đi. #speaker:Villain #anim:Villain_D:walk #move:Villain_D:-480:0.7 #wait:0.7 #banging:Door:off #anim:Villain_D:touch_door
#knock:Door:0.9 #wait:0.9 #knock:Door:0.6 #wait:1.3 #knock:Door:0.3 #wait:1.8 #anim:Gap:glow_fade #wait:1.8 #anim:Gap:blood_seep #wait:3.85 #anim:Villain_D:idle #wait:0.5 #flip:Villain_D:off #anim:Villain_D:walk #move:Villain_D:128:2.4 #wait:0.3 #move:Dungeon:-320:2.1 #wait:2.1 #anim:Villain_D:idle #wait:1.5 #fade:060103:1.2 #wait:1.2 #cast:Caption #sfx:beep #wait:2.2
-> Hospital_1

=== Hospital_1 ===
Thằng bé có thể tỉnh lại không bác sĩ? #speaker:Mom #bg:black:0 #cast:Hospital,Mom,Doctor #fade:in:1 #wait:1
Tâm trí cậu ấy đang chống cự rất mạnh. #speaker:Doctor
Với tình hình này, tôi không thể nói trước rằng cậu A có xâm nhập vào được hay không.
// -> BẮT ĐẦU MÀN 4.
-> DONE


// ---------------------------------------------------------------------
// KẾT THÚC MÀN 4 -> QUÁ KHỨ -> BỆNH VIỆN -> HỒI ỨC HAI ANH EM
// ---------------------------------------------------------------------
=== Level4_End ===
#bg:black:0 #fade:08060A:0 #flip:B4:off #move:A4:-576 #cast:DarkDungeon,DarkRoom,B4 #fade:in:0.7 #wait:1.2 #cast:DarkDungeon,DarkRoom,B4,A4,Box #attach:Box:A4:34,8 #anim:A4:run #move:A4:-440:1.3 #wait:1.3 #anim:A4:idle #wait:0.3 #flip:B4:on #wait:0.7 #reach:B4:A4:-56 #hold #attach:Box:B4:-18,4 #wait:1.2 #fade:08060A:0.9 #wait:0.9
-> Past

=== Past ===
Anh ơi! Ra ăn cơm đi! #speaker:A #cast:Bedroom,WallShadow,BSit #alpha:WallShadow:0 #fade:in:0.9 #wait:1.5 #anim:Bedroom:bang #wait:0.45 #anim:Bedroom:bang #wait:0.45
Anh... #speaker:A
Cậu nghĩ tại sao chúng ta từ khi sinh ra đã như vậy rồi nhỉ? Kiểu có thể bị thương bất cứ lúc nào ấy. #speaker:B #anim:BSit:up #alpha:WallShadow:1:0.5 #wait:0.5
Anh đang nói chuyện với ai thế? #speaker:A #alpha:WallShadow:0.5
Nếu như không có cơ thể này thì tốt quá ha. #speaker:B #alpha:WallShadow:1
Anh bị sao thế? Anh đừng doạ em! #speaker:A #banging:Bedroom:on
Bọn ở trường lại nói mình nữa rồi. Ôi mình muốn bổ đầu ra để chứng minh mình đã khổ sở chừng nào với bọn nó quá. #speaker:B #anim:BSit:shiver
Sao thế giới này lại đông người vậy chứ? Sao tất cả mọi người đều khác nhau? Thật không công bằng! Tại sao ai cũng nhắm vào tôi. Cút đi! Cút đi! #speaker:B #anim:BSit:rock
Anh! Anh ơi! #speaker:A #alpha:WallShadow:0 #anim:BSit:up #wait:0.28 #anim:BSit:slam #shake:4:0.22 #fade:FFFFFF:0 #wait:0.05 #fade:in:0 #wait:0.26 #anim:BSit:up #wait:0.24 #anim:BSit:slam #shake:6:0.28 #fade:FFFFFF:0 #wait:0.05 #fade:in:0 #wait:0.26 #anim:BSit:up #wait:0.2 #anim:BSit:slam #shake:8:0.34 #fade:FFFFFF:0 #wait:0.05 #fade:in:0 #wait:0.26 #anim:BSit:down #shake:6:0.2 #wait:0.9
#fade:08060A:1 #wait:1 #banging:Bedroom:off #cast:Caption #sfx:beep #wait:1.8
-> Hospital_2

=== Hospital_2 ===
Có vẻ con trai bà đã tự tạo ra một nhân cách thứ hai. #speaker:Doctor #bg:black:0 #cast:Hospital,Mom,Doctor #fade:in:1 #wait:1
L... là sao ạ bác sĩ? #speaker:Mom
Nhân cách thứ hai này có thể khiến mong muốn của cậu ấy trở thành sự thật. Vì vậy, thật tiếc khi phải nói rằng nó đã chiếm quyền kiểm soát. Con trai bà không còn thiết tha gì đời sống thật nữa. #speaker:Doctor
#anim:Mom:cry #wait:2.2 #fade:08060A:1 #wait:1
-> WhiteRoom_3

=== WhiteRoom_3 ===
// THOẠI GIỮ CHỖ: các câu (không bắt đầu bằng #) chép nguyên từ preview của bộ Scene_HoiUc_AnhEm, chờ thoại chính thức.
// Thay chữ của từng câu là đủ; mỗi dòng chỉ có tag phía trên một câu là dàn cảnh của nó, không cần sửa.
// Sân tập (Track, B_FB, A_FB) -> đuối nước (Drown, khung thoại ở trên) -> sân tập -> lớn lên (Montage m1-m4) -> sân tập
// -> A bước lại, chìa tay; B cúi nhìn, rụt tay hai lần -> người chơi đưa B tới và bấm Space / E / nút ! -> cận cảnh nắm tay.
#sfx:stop #pixel:08060A:0 #fade:in:0 #bg:F6F0E4:0 #flip:B_FB:off #flip:A_FB:on #move:B_FB:-76 #move:A_FB:68 #cast:Track,B_FB,A_FB,Vignette #pixel:in:0.7 #wait:1.3
Anh còn nhớ mùa hè năm đó không? Cái lần em suýt chết đuối ấy. #speaker:A
#layout:top #pixel:cut:0.7 #cast:Drown #anim:Drown:intro #wait:1.2
Em chìm dần, nước tràn vào mũi, chẳng thấy gì nữa. #speaker:A
Rồi anh lao xuống. Anh kéo em lên. #speaker:A
#layout:bottom #pixel:cut:0.56 #cast:Track,B_FB,A_FB,Vignette #wait:1.06
[speed=0.4]... #speaker:B
#pixel:cut:0.56 #cast:Montage #anim:Montage:m1 #wait:0.56
Từ hôm đó, anh đi đâu là em theo đó. #speaker:A
#pixel:cut:0.48 #anim:Montage:m2 #wait:2.4 #pixel:cut:0.48 #anim:Montage:m3 #wait:2.5 #pixel:cut:0.48 #anim:Montage:m4 #wait:2.9 #pixel:cut:0.7 #cast:Track,B_FB,A_FB,Vignette #wait:1.2
Lớn rồi mà em vẫn thế. Vẫn cứ bám theo anh. #speaker:A
Anh chẳng còn là người anh hồi đó nữa đâu. #speaker:B
Không sao. Hồi đó anh kéo em lên rồi. #speaker:A
Giờ đến lượt em. #speaker:A
#anim:A_FB:run #move:A_FB:40:0.9 #wait:0.9 #anim:A_FB:idle #wait:0.4 #anim:A_FB:reach #wait:1 #anim:B_FB:hesitate #wait:2.2 #reach:B_FB:A_FB:-120:stage.hold #hold #flip:B_FB:off #anim:B_FB:run #move:B_FB:-8:0.2 #wait:0.2 #anim:B_FB:reach #wait:0.51 #anim:B_FB:touch #wait:0.3 #cast:CloseUp #wait:3.2 #pixel:08060A:1.4 #wait:1.6
-> DONE


// ---------------------------------------------------------------------
// CẢNH CHUYỂN TIẾP - Hành lang dẫn đến màn cuối
// Mỗi vùng BarkZone dọc hành lang gọi Corridor_Bark một lần, lần lượt ra câu tiếp theo.
// Đếm bằng biến chứ không dùng {stopping:}: DialogueManager.ExitDialogue() gọi
// story.ResetState() nên số lần ghé knot bị xoá, còn biến global thì được giữ lại.
// ---------------------------------------------------------------------
=== Corridor_Bark ===
{corridorBarkIndex:
- 0: Vẫn còn một cửa nữa ư? #speaker:A
- 1: Không ổn rồi. {collapseMinutes} phút nữa toàn bộ chỗ này sẽ đổ sập. #speaker:B
- else: Bọn mình phải nhanh lên. #speaker:A
}
~ corridorBarkIndex = corridorBarkIndex + 1
-> DONE


// ---------------------------------------------------------------------
// MÀN 5 - MÀN CUỐI: đếm ngược, không có thoại.
// KẾT THÚC: không có thoại - làm bằng hiệu ứng trong scene "Story Ending"
// (các màn nổ thành pixel, nhân cách thứ hai vẫy tay rồi tan biến, A và B cùng bước ra khỏi màn hình).
// ---------------------------------------------------------------------
