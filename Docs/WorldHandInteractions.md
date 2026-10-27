# Thao tác dụng cụ 3D và bãi tập cung

## Bốn sạp vật tư

Camera tiến tới bàn khi người chơi tới vị trí làm việc; chuột điều khiển dụng cụ
trong thế giới thay vì cùng lúc xoay camera. Rời bàn để mang hàng thì trở lại
camera đi bộ gần vai. Mái che của sạp tạm ẩn trong lượt làm việc và khôi phục
khi rời sạp, cùng các thiết lập camera ban đầu.

| Sạp | Thao tác sau khi mang vật tới bàn |
|---|---|
| Gỗ | Giữ chuột trái, kéo cưa qua lại tới điểm sáng; tám nửa nhịp cưa cho mỗi khúc |
| Dây | Giữ chuột trái, kéo đầu dây lần lượt quanh bốn vị trí của vòng buộc; ba vòng trên bó cọc |
| Gạo | Kéo gáo sang trái để xúc, sang phải rồi lên trên để nghiêng đổ; mỗi gáo tăng 24%, ba gáo được 72% trong dải xanh 65–85% |
| Lương khô | Đặt đủ ba nguyên liệu, kéo từng mép khăn sáng vào giữa; bốn mép tạo thành gói |

Giữ chuột đứng yên không tăng tiến độ; chuyển động sai hướng không hoàn thành
bước. E vẫn dùng để cầm, đặt, nhấc và giao vật. ESC/X rời lượt; H xem thao tác.
Các giới hạn và mốc hạng của bốn sạp giữ nguyên. Kết quả cũ vẫn giữ trong save.

## Đóng cọc trên thuyền

Lấy bó cọc, W vác ra mạn. Rê chuột chọn vị trí đặt trong vùng với tay; cuộn
chuột chỉnh độ nghiêng. Mẫu cọc xanh là chỗ đặt được; đỏ là vướng vật thể hoặc
đất khô. Phải ngắm vị trí trước khi E dựng cọc. Giữ chuột trái và kéo lên để
nâng búa, thả để nện; nâng chưa đủ không tính nhát. Ba nhát cho mỗi bó, ba bó
cho một cụm. Tiến độ này tiếp tục dựng dần các cọc thật của cụm ngoài sông.

Hai tay đỡ bó cọc nằm ngang trước ngực bằng IK, thay cho xoay cánh tay bằng
góc cố định. Lúc nện búa, tay bám cán búa, cọc lún dần, có tiếng đóng gỗ và
nước bắn. Hủy/load kết thúc công việc và khôi phục camera, nhân vật, chèo thuyền.

## Luyện cung trước trận

Bãi tập được tạo trên đất khô cạnh doanh trại, có ba bia rơm ở khoảng cách khác
nhau. Đến bảng luyện cung và E bắt đầu một lượt sáu mũi. Giữ chuột phải ngắm;
giữ chuột trái kéo dây và thả để bắn. Bia cần bắn sáng màu vàng. Trúng tâm
được 10 điểm, vòng giữa 7, vòng ngoài 4; bắn nhầm bia không được điểm. Trúng
ít nhất bốn bia là đạt. R tập lại sau kết quả; Enter hoặc ESC rời bãi tập.

Cung, tư thế tay, mũi tên bay và thao tác kéo/thả dùng chung với chiến đấu.
Tay trái giữ cán cung, tay phải kéo dây về má; mũi tên hiện trên dây khi kéo,
thân cung biến dạng theo lực kéo và có chuyển động nhả dây. Giữ chuột phải
mà không kéo chuột trái không tự tích lực bắn.

Luyện cung là mục phụ trong nhật ký, có kỷ lục riêng 0–60 và cờ hoàn thành
trong save. Không cộng vào số lần bắn, độ chính xác hay số hạ địch của trận
thật; kết thúc luyện tập hoàn lại lượng tên trước đó. Autosave đợi kết thúc
lượt. Save cũ không có trường luyện cung được hiểu là chưa luyện.

## Dáng tay của model nhân vật

Rig nhân vật gốc có `LeftHand/RightHand` và xương kết thúc, không có chuỗi xương
ngón tay. `BeachBoatBowGripAuthoring.Build` tạo hai blend shape từ các đỉnh mesh
chịu ảnh hưởng của bàn tay trong bind pose: nắm cán bằng tay trái và móc dây
bằng tay phải. Không thay model, skin weights hoặc file FBX gốc.

`BowGripProfile` giữ trục dọc/ngang bàn tay và tâm tiếp xúc với cán/dây. IK đặt
cổ tay thẳng theo cẳng tay, khuỷu kéo dây ở ngoài thân, điểm kéo ở phía trước
má. Bàn tay không còn lấy hướng từ thuyền hay giữ nguyên dáng mở của model.
Mesh có dáng nắm chỉ được dùng khi ngắm, rồi trả về mesh gốc khi hạ cung.
LOD giữ nguyên mesh nắm trong lúc ngắm để tránh đổi mesh liên tục.

Khi đóng cọc, cùng hai dáng nắm được áp dụng lên cán búa. Nhân vật quay về
phía cọc; tay phải nắm điểm trên, tay trái nắm thấp hơn 1,8 đơn vị thế giới.
Trục bàn tay và tâm lòng nắm khớp với hướng cán; vị trí đầu búa vẫn hướng
tới đầu cọc. Rời thao tác búa sẽ trả bàn tay về mesh gốc như khi hạ cung.

Tài nguyên được tạo ở `Assets/Resources/BachDang938/Player`, khoảng 13,3 MiB.
Ảnh kiểm chứng trước/sau và báo cáo nằm trong `Logs/ArcheryTraining_Front.png`,
`Logs/ArcheryTraining_Draw.png` và `Logs/ArcheryPlaytest.json` (không đưa vào Git).

## Kiểm tra bằng Unity CLI

- Bốn sạp, camera, tiếp xúc dụng cụ, thành tích và save/load: 98 kiểm tra.
- Tập cung, ba mức kéo dây, góc cổ tay, nắm tay, thoát và save/load: 52 kiểm tra.
- Năm cụm cọc, cầm/nện búa, hủy, khôi phục và save/load: 67 kiểm tra. Bài đo
  lấy hơn 2.200 mẫu sau animation/IK, kiểm tra tay bám cán và cổ tay theo cẳng tay
  suốt nhịp nâng/nện, thay vì chỉ kiểm tra một ảnh giơ búa.
- Trận Quest 3, nhử địch, thủy triều, đắm tàu, giao chiến, rút quân và save/load:
  60 kiểm tra.

Các bài thử dùng thư mục lưu tạm riêng, xác minh SHA-256 của các save thật không
đổi. Scene game và ProjectSettings không bị sửa trong đợt thay thao tác này.
