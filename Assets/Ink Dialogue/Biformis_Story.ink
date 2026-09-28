// =====================================================================
// BIFORMIS - Kịch bản chính (chuyển từ "KỊCH BẢN GAME.docx")
// Bản tiếng Anh: Biformis_Story_EN.ink - phải giữ đúng tên knot và biến như file này.
//
// Mỗi cảnh là một knot. Trong chế độ cốt truyện, knot được gọi theo thứ tự trong "Story Data".
//
// Speaker (tag #speaker, tên hiển thị nằm ở Resources/Localization/Strings.txt, key "speaker.<id>"):
//   A        - người em (hình thái Xanh)
//   B        - người anh (hình thái Đỏ)
//   Villain  - Phản diện (nhân cách thứ hai của B) - không có hình, màn hình ửng đỏ khi nói
//   Teen     - Thiếu niên (B lúc còn học trung học)
//   Mom      - Mẹ
//   Doctor   - Bác sĩ
//   System   - Thông báo hệ thống (không hiện tên)
//   Narrator - Dẫn truyện cho các chỉ dẫn sân khấu (không hiện tên)
//
// Tag dàn cảnh (CutsceneStage):
//   #bg:white | black | hospital | past     màu nền
//   #cast:A,B,Teen | none                    ai đứng trên sân khấu (Box = chiếc hộp đen)
//   #fx:shake | flash | red | fade_black | fade_white | fade_in
//   #sfx:beep | stop | thud                  beep = máy đo nhịp tim (lặp), stop = tắt âm lặp
// Không dùng tag #layout: layoutAnimator trong DialogueManager đang null.
// =====================================================================

// Số phút Màn 5 cho phép (khớp với LevelCountdown trong scene Màn 5).
VAR collapseMinutes = 2
VAR corridorBarkIndex = 0


// ---------------------------------------------------------------------
// MỞ ĐẦU - Cảnh 1: Bắt đầu
// ---------------------------------------------------------------------
=== Intro ===
Khốn kiếp! Lại quay lại rồi à? #speaker:B #bg:black #cast:A,B
Em nhớ là bọn mình đã chạy xa lắm rồi mà. #speaker:A
Xin chào! Chúng ta lại gặp nhau rồi! #speaker:Villain #fx:red
Biến bọn tôi trở lại! Tôi không muốn làm nữa! #speaker:B
Bọn tôi sẽ trả tiền lại cho ông mà! Làm ơn thả bọn tôi ra! #speaker:A
Xin lỗi nhé! Ta e rằng hiện tại ta không thể thả hai người ra được. #speaker:Villain
Với cả, ta làm gì có cách nào để tách hai người ra.
Hả? #speaker:B
Ta có ý này. Sao hai người không chấp nhận chuyện này đi nhỉ? #speaker:Villain
Cứ đau khổ mãi về nó có được lợi gì đâu.
Đồ điên! Mấy chuyện này là do ông gây ra chứ ai. Đừng có rao giảng đạo lý nữa! #speaker:B
Đúng là nông cạn. Nói cho hai người biết, nghe lời ta thì được nhiều hơn là mất đấy. #speaker:Villain
Thôi được rồi, ta sẽ cho hai người thêm một cơ hội.
Đừng coi đây là hình phạt mà hãy xem như là một bài học ta dành cho hai người đi.
Hệ thống khởi động. #speaker:System #cast:none #fx:flash
BẮT ĐẦU CHƠI.
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
<i>(B tỉnh dậy. Trước mặt B là một thiếu niên đang đứng trong một căn phòng màu trắng.)</i> #speaker:Narrator #bg:white #cast:B,Teen
Anh có sao không? #speaker:Teen
Em là ai? Em làm gì ở đây? #speaker:B
Em đang luyện tập cho đội điền kinh của trường thì bị chấn thương. #speaker:Teen
Bác sĩ nói em không thể chạy được nữa, nhưng em vẫn cố chạy.
Em đã chạy và ngã không biết bao nhiêu lần, đau đớn lắm, và rồi em ở đây.
Em không biết tại sao em lại ở đây sao? #speaker:B
Vâng. Nhưng ở đây ấm áp và dễ chịu quá. #speaker:Teen
Em cảm thấy mình có thể chạy nhanh như bay ở đây.
Giá như ở ngoài kia cũng được như vậy thì hay biết mấy anh nhỉ?
Bị đạn bắn vào chắc đau lắm nhỉ? #speaker:Villain #fx:shake
Rốt cuộc ông muốn gì? #speaker:B
Chịu thôi. Ta chỉ mong ngươi nhanh chóng ngộ ra sự thật. #speaker:Villain
Đi theo ta là không có thiệt đâu.
Ý ông là sao? #speaker:B
Mọi đau khổ của con người đều đến từ sự bất lực khi không thể làm những việc ngoài phạm vi thể xác. #speaker:Villain
Sau khi thực hiện một vài nghiên cứu, ta đã nhận ra một điều.
Ở hình thái tiến hoá cuối cùng, nhân loại sẽ hợp thành một, sử dụng chung một tư duy và không còn cơ thể vật lý.
Khi đó, chúng ta không thể bị thương, có thể bay nhanh như gió, du hành giữa các vì sao và mọi khả năng là vô tận.
Như vậy sẽ không còn sự bất lực, không còn sự nuối tiếc, không còn phân biệt đối xử nữa.
Lúc đó kể cả có bị đạn bắn chắc cũng chẳng sợ nhỉ?
Nói tóm lại, sứ mệnh của ta là đưa ngày đó đến gần hơn với loài người.
Ngươi có tin là chỉ cần một công tắc, ta có thể ban cho ngươi cuộc sống mơ ước đó không?
Ông ta đang nói gì vậy chứ... #speaker:B
// -> Tỉnh dậy, BẮT ĐẦU MÀN 3.
-> DONE


// ---------------------------------------------------------------------
// CẢNH - SAU MÀN 3 -> CĂN PHÒNG TRẮNG (lần 2) -> BỆNH VIỆN
// ---------------------------------------------------------------------
=== AfterLevel3 ===
Hồi nãy anh mơ thấy một giấc mơ lạ lắm. #speaker:B #bg:black #cast:A,B
Anh lại mơ thấy những chuyện đó nữa à? #speaker:A
Sao em biết anh mơ thấy gì? #speaker:B
Hồi còn học trung học, anh bị chấn thương nên phải rút khỏi đội điền kinh của trường còn gì? #speaker:A
Anh quên chuyện đó à?
[speed=0.4]... #speaker:B
Có vẻ ông ta muốn anh quên đi những chuyện đó...
Vậy anh có muốn quên đi không? #speaker:A
[speed=0.4]... #speaker:B
-> WhiteRoom_2

=== WhiteRoom_2 ===
<i>(B lại mơ. Thiếu niên xuất hiện.)</i> #speaker:Narrator #bg:white #cast:B,Teen
Anh nhớ ra em rồi sao? #speaker:Teen
Ờ. #speaker:B
Vậy anh thấy sao? #speaker:Teen
Anh không biết mình nên có cảm xúc gì. Anh chỉ thấy mệt thôi. #speaker:B
Anh sẽ quên em sao? #speaker:Teen
Anh không biết. #speaker:B
Đừng lăn tăn nữa. Cậu muốn trở lại làm kẻ cô đơn và thất bại đó sao? #speaker:Villain #fx:red
Đừng nói nữa. #speaker:B
Chỉ cần một nút ấn thôi mà. Mau đi theo ta đi rồi. #speaker:Villain
Anh ơi. Anh có ở đó không? #speaker:A
A? #speaker:B
Anh ơi. Đừng có nghe hắn. Xin anh đấy. #speaker:A
Cút đi! Ngươi muốn nhìn hắn đau khổ sao? #speaker:Villain #fx:shake
Mở cửa ra cho ta. #speaker:A
Vẫn còn cố chấp à? Vậy thì chết đi. #speaker:Villain #fx:shake
-> Hospital_1

=== Hospital_1 ===
<i>(Màn hình đen. Tiếng máy móc trong bệnh viện vang lên.)</i> #speaker:Narrator #bg:black #cast:none #sfx:beep
Thằng bé có thể tỉnh lại không bác sĩ? #speaker:Mom #bg:hospital #cast:Mom,Doctor
Tâm trí cậu ấy đang chống cự rất mạnh. #speaker:Doctor
Với tình hình này, tôi không thể nói trước rằng cậu A có xâm nhập vào được hay không.
// -> BẮT ĐẦU MÀN 4.
-> DONE


// ---------------------------------------------------------------------
// KẾT THÚC MÀN 4 -> QUÁ KHỨ -> BỆNH VIỆN -> PHÒNG TRẮNG
// ---------------------------------------------------------------------
=== Level4_End ===
<i>(A xuất hiện trong căn phòng trắng và nhìn thấy B.)</i> #speaker:Narrator #bg:white #cast:A,B
<i>(A đưa cho B một chiếc hộp hình vuông màu đen.)</i> #cast:A,B,Box
-> Past

=== Past ===
<i>(Quá khứ.)</i> #speaker:Narrator #bg:past #cast:B #fx:fade_in
Anh ơi! Ra ăn cơm đi! #speaker:A
Anh...
Cậu nghĩ tại sao chúng ta từ khi sinh ra đã như vậy rồi nhỉ? #speaker:B
Kiểu có thể bị thương bất cứ lúc nào ấy.
Anh đang nói chuyện với ai thế? #speaker:A
Nếu như không có cơ thể này thì tốt quá ha. #speaker:B
Anh bị sao thế? Anh đừng doạ em! #speaker:A
Bọn ở trường lại nói mình nữa rồi. #speaker:B
Ôi mình muốn bổ đầu ra để chứng minh mình đã khổ sở chừng nào với bọn nó quá.
Sao thế giới này lại đông người vậy chứ? Sao tất cả mọi người đều khác nhau?
Thật không công bằng! Tại sao ai cũng nhắm vào tôi.
Cút đi! Cút đi! #fx:shake
Anh! Anh ơi! #speaker:A #sfx:thud #fx:shake #cast:none
-> Hospital_2

=== Hospital_2 ===
<i>(Tiếng máy bệnh viện.)</i> #speaker:Narrator #bg:black #sfx:beep
Có vẻ con trai bà đã tự tạo ra một nhân cách thứ hai. #speaker:Doctor #bg:hospital #cast:Mom,Doctor
L... là sao ạ bác sĩ? #speaker:Mom
Nhân cách thứ hai này có thể khiến mong muốn của cậu ấy trở thành sự thật. #speaker:Doctor
Vì vậy, thật tiếc khi phải nói rằng nó đã chiếm quyền kiểm soát.
Con trai bà không còn thiết tha gì đời sống thật nữa.
-> WhiteRoom_3

=== WhiteRoom_3 ===
<i>(Quay trở lại căn phòng trắng. A và B đối diện nhau.)</i> #speaker:Narrator #sfx:stop #bg:white #cast:A,B
Từ đó cũng được vài năm rồi. #speaker:A
Bỗng nhiên một ngày bác sĩ nói anh có chuyển biến tích cực.
Họ đã chớp lấy cơ hội này để giúp em xâm nhập vào não anh và đưa anh trở lại.
Anh có nhớ không? Hồi còn bé, em từng suýt chết đuối khi đi biển nên sinh ra chứng sợ hãi đến tận bây giờ.
[speed=0.4]... #speaker:B
Nhưng mà em không hối hận chút nào đâu, vì từ lúc đó anh đã luôn đi sát bên cạnh em mỗi lần tắm biển. #speaker:A
Thấy anh như vậy, em vui lắm. Em không muốn người anh trai ân cần luôn lo em bị đuối nước đó sẽ biến mất.
Dù có thế nào, những ký ức đó cũng đã tạo nên bản thân em của hiện tại mà.
Thứ khiến chúng ta trở nên khác biệt hoá ra lại đẹp đẽ như vậy nhỉ?
Em chỉ muốn là chính em thôi và em cũng mong anh là chính anh nữa, có đau khổ một chút cũng được.
Quay về thôi. Em sẽ giúp anh vượt qua như anh đã từng giúp em trước đây. Em tin anh sẽ làm được.
<i>(Hai người nắm tay nhau.)</i> #speaker:Narrator #fx:flash
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
