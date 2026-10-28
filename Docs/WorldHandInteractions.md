# Thao tác dụng cụ 3D và bãi tập cung

## Bốn sạp vật tư

Sau khi đặt vật lên bàn, camera chuyển tới góc nhìn sát mặt bàn. `WorkbenchHands`
lọc các tam giác chịu ảnh hưởng của xương tay từ mesh nhân vật và tạo rig hiển thị
riêng từ bộ xương đó, dùng lại vật liệu và dáng nắm. Cánh tay đi vào từ dưới khung
hình, bàn tay bám điểm thao tác; đầu/thân không che công việc.
Tay hiển thị dùng chuỗi vai–khuỷu–cổ tay có chiều dài cố định, chỉ phóng đồng đều
toàn tay cho góc nhìn gần. Không kéo dài riêng một trục xương để với bàn. Bài thử
đo tỉ lệ từng đoạn suốt chuyển động, sai số tối đa quan sát dưới 0,01%.
Không thay FBX gốc. Chuột điều khiển dụng cụ; đi bộ khóa trong bước làm tại bàn.
Rời bàn để mang hàng thì khôi phục nhân vật đầy đủ và camera đi bộ gần vai.
`VillageWorkstation` dựng bàn, mái, hai thùng và nguồn vật tư một lần lúc vào
scene. Cả bốn sạp dùng cùng các vật thể đó trước E, trong lúc chơi và sau khi
rời sạp; mái không còn bị gỡ, bàn không bị thay bằng một bàn tạm khác. Chỉ hàng
đang cầm/làm/giao và dụng cụ là trạng thái của lượt chơi. Mesh tay được hủy khi
đóng hoạt động hoặc load, rồi khôi phục camera và renderer của nhân vật.

| Sạp | Thao tác sau khi mang vật tới bàn |
|---|---|
| Gỗ | Bấm giữ cán cưa rồi kéo LÊN/XUỐNG dọc rãnh xanh; rê ngang để căn giữa (lệch ngang tối đa 28%); mỗi nửa nhịp ít nhất 0,22 giây và quãng kéo 1,2 đơn vị thao tác, tám nửa nhịp mỗi khúc |
| Dây | Kéo quanh bốn điểm, giữ để tăng lực siết; THẢ trong dải xanh 55–85%, tương đương cửa sổ khoảng 0,46 giây. Thả sai chỉ làm lại nút này; ba nút cho bó cọc |
| Gạo | Bấm giữ gáo trong rổ trái, kéo qua miệng bao phải rồi THẢ để đổ. Thả sai chỗ vẫn giữ gạo trong gáo. Ba gáo 24% tạo mức đủ 72%; E/Enter hoặc nút Nhấc bao khi xanh; R bớt một gáo, C đổ lại khi dư |
| Lương khô | Đặt đủ ba nguyên liệu, kéo mép khăn vào giữa rồi giữ yên 0,45 giây để ép kín; nhả sớm chưa tính nếp, có thể ép lại. Bốn mép tạo thành gói |

Giữ chuột đứng yên từ đầu không tự làm xong; chỉ bước siết/ép sau đường kéo đúng
mới dùng thời gian giữ. Thanh lực lớn nằm giữa phía dưới màn hình, ngoài vùng
làm việc và phía trên dòng điều khiển; dải xanh chỉ vùng nhả tốt. Thanh này hiện
suốt bước buộc dây, không bị mái, tay hay camera 3D che khuất;
HUD hiển thị phản hồi lệch rãnh, thiếu/đủ/dư gạo hoặc nếp chưa kín. E/Enter dùng để cầm, đặt,
nhấc và giao vật. ESC/X rời lượt; H xem thao tác.
Các giới hạn và mốc hạng của bốn sạp giữ nguyên. Kết quả cũ vẫn giữ trong save.

Vạch xanh chỉ hiện sau khi đặt gỗ lên bàn, trong đúng bước cưa và góc nhìn bàn;
không hiện khi vừa mở sạp, đang lấy/vác gỗ hoặc đã giao hàng. Khi làm việc,
con trỏ được mở khóa và hiện rõ; bấm giữ dụng cụ rồi kéo theo vị trí thật.
`PointerCoordinates` nghịch phép chiếu mặt phẳng thao tác 3D, tránh cộng dồn
Mouse X/Y gây lệch hướng và phụ thuộc độ nhạy. Con trỏ gói lương luôn nằm trên mặt bàn;
độ nâng tay/khăn là chuyển động 3D riêng, không làm đảo hướng kéo.

Sửa theo clip 15:00 ngày 28/10: khoảng lệch lúc bắt dụng cụ được giới hạn, tránh
con trỏ ở giữa nhưng vật vẫn nằm ngoài vùng đích. Cưa bám rãnh ở chế độ chuột;
dây có vùng bắt điểm rộng hơn và giữ vị trí khi siết. Khăn kéo theo trục của mép
đang chọn; khi qua vùng giữa thì chốt ở giữa trong lúc giữ để ép, không bật lại
vì rê quá tay. Cần nhả và bấm giữ mép tiếp theo. Tay đang kẹp mép bám chính điểm
trên cánh khăn quay, tay còn lại giữ phía đối diện, tránh bắt chéo hai tay.

`WorkbenchHandGrips` có sáu dáng mesh riêng: trái/phải × nắm cán/kẹp/giữ.
Model gốc không có rig từng ngón, nên `BeachBoatWorkbenchGripAuthoring` tạo các
vùng gập đốt gốc/giữa/đầu và khép ngón cái trực tiếp trên mesh tay, giữ nguyên
FBX và tư thế cung. Chỉ giữ mesh hai cánh tay để giảm dung lượng. Chuyển dáng
được blend, cán cưa/gáo quyết định trục nắm; lúc mang vật, tay nhân vật khép theo
loại vật tư rồi khôi phục khi rời lượt hoặc load. Âm thanh đã đổi sang bản thu
thật và cân mức nhỏ hơn; xem [TradeAudioSources.md](TradeAudioSources.md).

Tay hiển thị lấy trục cẳng tay/cổ tay từ bind pose của mesh, không kế thừa góc
cổ tay đang bị uốn trong animation đi bộ. Khi buộc, tay trái giữ bó gỗ bên trái
nút, tay phải nắm và kéo dây; hai bàn tay không còn chồng trên cùng một điểm.

## Độ sáng doanh trại

`CampReadability` tạo volume cục bộ quanh các khu nhà/lều doanh trại, với khoảng
chuyển tiếp 55 đơn vị. Exposure 0,7 EV, contrast 4 và gamma nhẹ làm rõ giáp,
mặt người và lối đi; không áp dụng cho làng hoặc sông. Tăng cường độ đuốc trong
vùng 20%, tầm sáng 15%, vẫn giữ ngân sách tối đa tám đèn và không thêm đèn realtime.
Volume và profile chỉ tồn tại trong lượt chơi, không viết vào scene hay save.

## Chân Ngô Quyền trên sàn chỉ huy

Đã đo các đỉnh đế giày trên mesh sau skinning và raycast xuống MeshCollider sàn.
Vị trí Y của riêng `NgoQuyen_QuestGiver` được chỉnh từ 157,48978 xuống 155,04704;
đế giày cách điểm sàn cao nhất khoảng 0,05 đơn vị. Không sửa tỉ lệ NPC hay sàn.
Kiểm tra lại trong Play Mode và sau load. Save không lưu vị trí Ngô Quyền nên
slot cũ nhận vị trí scene đã sửa. Công cụ đo: `BeachBoatCommanderFloorAudit`.

## Đóng cọc trên thuyền

Lấy cọc, W vác ra mạn. Rê chọn vị trí không vướng; cuộn chuột chỉnh nghiêng
theo góc được chỉ dẫn rồi E dựng. Giữ chuột trái, kéo ngang căn tâm búa và kéo
lên nâng; thả khi lực trong dải xanh 65–95%. Lực sai hoặc lệch tâm không tăng
tiến độ. Tự dựng ba cọc mẫu, mỗi cọc ba nhát đạt. Sau đó chuyển cảnh “Vài ngày
sau…” và quân dân hoàn thiện cả năm cụm ngoài sông. Xem thêm
[PreparationAndVillageLife.md](PreparationAndVillageLife.md).

Hai tay đỡ bó cọc nằm ngang trước ngực bằng IK, thay cho xoay cánh tay bằng
góc cố định. Lúc nện búa chuyển sang góc nhìn chỉ có tay để không che đầu cọc;
búa chạm đúng vị trí 3D, cọc lún dần, có tiếng gỗ và nước bắn. Hủy/load kết thúc
công việc và khôi phục camera, nhân vật, chèo thuyền.

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

Luyện cung là nhiệm vụ bắt buộc trước xuất quân trong nhật ký, có kỷ lục riêng
0–60 và cờ hoàn thành trong save. Không cộng vào số lần bắn, độ chính xác hay số hạ địch của trận
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

Khi đóng cọc, tay hiển thị dùng dáng nắm cán riêng của `WorkbenchHands`.
Tay phải nắm điểm trên, tay trái nắm thấp hơn 3,6 đơn vị thế giới; khuỷu tay
mở sang hai bên. Cổ tay thẳng theo cẳng tay, các ngón ôm cán dài 11 đơn vị.
Đầu búa tới đúng đầu cọc. Rời thao tác búa sẽ trả nhân vật và camera bình thường.

Tài nguyên được tạo ở `Assets/Resources/BachDang938/Player`, khoảng 13,3 MiB.
Ảnh kiểm chứng trước/sau và báo cáo nằm trong `Logs/ArcheryTraining_Front.png`,
`Logs/ArcheryTraining_Draw.png` và `Logs/ArcheryPlaytest.json` (không đưa vào Git).

## Kiểm tra bằng Unity CLI

Các số dưới đây là kết quả của đợt thao tác 3D trước. Kết quả cho bài cọc mẫu,
bắt buộc tập cung, tay múc và sinh hoạt NPC mới nằm trong
[PreparationAndVillageLife.md](PreparationAndVillageLife.md).

- Bốn sạp, bố trí trước/trong/sau E, độ sáng doanh trại, thanh lực phía dưới,
  tỉ lệ tay/cổ tay, bốn hướng kéo chuột (kể cả lúc nâng khăn), vạch cưa chỉ hiện
  đúng bước, chân Ngô Quyền, thành tích và save/load: 202 kiểm tra.
  ESC và load được thử ngay trong góc nhìn bàn để xác
  nhận xóa tay hiển thị, trả nhân vật đầy đủ và camera bình thường.
- Tập cung, ba mức kéo dây, góc cổ tay, nắm tay, thoát và save/load: 52 kiểm tra.
- Năm cụm cọc, cầm/nện búa, hủy, khôi phục và save/load: 67 kiểm tra. Bài đo
  lấy hơn 2.200 mẫu sau animation/IK, kiểm tra tay bám cán và cổ tay theo cẳng tay
  suốt nhịp nâng/nện, thay vì chỉ kiểm tra một ảnh giơ búa.
- Trận Quest 3, nhử địch, thủy triều, đắm tàu, giao chiến, rút quân và save/load:
  60 kiểm tra.

Các bài thử dùng thư mục lưu tạm riêng, xác minh SHA-256 của các save thật không
đổi. Scene game và ProjectSettings không bị sửa trong đợt thay thao tác này.
