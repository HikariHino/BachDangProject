# Gameplay của Thái trong beachBoat

Nguồn đối chiếu: `origin/thai` tại `03aad75`, gồm các commit `ac85205`, `aee7027`, `03aad75`. Chỉ chuyển gameplay; không merge toàn bộ scene TestQuest hoặc map cũ.

## Chơi thử

Mở `Assets/Scenes/beachBoat.unity`, bấm Play.

- Space/Enter đọc tiếp mở đầu; giữ Esc 0,8 giây để bỏ qua.
- WASD di chuyển, Shift chạy, Space nhảy; chuột xoay camera, chuột phải nhìn qua vai, Alt nhìn quanh, Esc thả chuột.
- E tương tác với Ngô Quyền, dân làng, gỗ và các điểm chuyển khu vực.
- F9 đổi giữa camera sa bàn hiện có và điều khiển nhân vật, sau khi kết thúc mở đầu/hội thoại/chuyển cảnh.
- Độ nhạy: chọn `BeachBoat_Thai_Gameplay/Camera_Thai_ThirdPerson`, chỉnh `Mouse X Speed` và `Mouse Y Speed` trong `Camera_Script` (mặc định 2 / 1,5).

Luồng nhiệm vụ: gặp Ngô Quyền trong sân `05_Trai_Du_Bi` → ra cổng sang `01_Lang_Cho_Ben_Song` → góp đủ 5 lượt gỗ dọc đường làng → về trại báo cáo → qua cổng ra bờ sông → thực hiện lệnh đóng cọc → quay về báo cáo hoàn thành hồi 2.

## Phạm vi thay đổi

- Một root mới `BeachBoat_Thai_Gameplay`: nhân vật scale 7, Ngô Quyền, 3 dân làng, 5 vật nhặt, điểm chuyển khu vực, camera và logic nhiệm vụ.
- Sử dụng trại/làng/bãi cọc sẵn có ở beachBoat. Không đưa nhà, hàng rào, terrain, núi, nước hay cây từ TestQuest sang. Bước đóng cọc dùng dẫn truyện tại trận địa hiện có, không ẩn/hiện hoặc dựng lại các cọc của map.
- Các lớp đặt trong namespace `BeachBoatStory`; không sửa script/scene của TestQuest, không chạy bootstrap tọa độ map cũ trên beachBoat.
- Mở đầu chờ người chơi đọc; hội thoại và chuyển cảnh khóa điều khiển. Một lần E chỉ thực hiện một hành động. Dịch chuyển cập nhật cả Rigidbody, Transform và camera.
- Lời nhắc E có chủ sở hữu và phải được gia hạn khi còn ở trong tầm tương tác: tự ẩn khi đi xa, nhặt xong, mở hội thoại hoặc chuyển sang sa bàn. Một vật thể không thể xóa lời nhắc của vật thể khác. Vận tốc ngang và lệnh nhảy đang chờ được hủy khi hội thoại/chuyển cảnh khóa điều khiển.
- Camera sa bàn và HUD cũ được chuyển chế độ tại runtime, khôi phục khi ngừng chơi. Chỉ một camera chính và một AudioListener hoạt động.

## Giữ nhẹ cho laptop

- Giữ nguyên 50.364 cây, TerrainData và cấu hình tối ưu map hiện có; không nạp TestQuest bổ sung khi chơi.
- Tái sử dụng mesh/material/texture của nhân vật hiện có. Không nhân bản texture hoặc thêm đèn/bóng động.
- Model gỗ AI nguồn có 288.364 đỉnh. Năm vật nhặt dùng chung chồng gỗ hiện có với 4.532 đỉnh (giảm khoảng 98,4% đỉnh mỗi vật nhặt); không có collider khối lớn.
- Danh sách 5 vật nhặt được cache, không quét toàn scene mỗi frame. Animator nhân vật được culling khi ngoài tầm nhìn.
- Chỉ thêm collider đơn giản cho nhà trong khu chơi nếu nhà đó chưa có collider.

## Kiểm chứng ngày 12/10/2026

- Unity 6000.6.0f1 biên dịch thành công; root gameplay không có missing script.
- Hai lượt Play; lượt cuối chạy 39 assertion qua các hàm gameplay trong Play Mode: reset nhiệm vụ, mở đầu, hội thoại, bốn cổng, đủ 5 gỗ, chống nhặt trùng/ngoài tầm, đóng cọc, báo cáo cuối, đổi camera, gia tốc và nhảy.
- Rà soát bổ sung: 12 assertion cho lời nhắc khi vào/ra tầm, vật nhặt bị hủy, hội thoại và F9, chặn nhặt trong hội thoại, hủy vận tốc/lệnh nhảy còn dư. Sau đó chạy lại 39 assertion của toàn bộ nhiệm vụ.
- Xem ảnh Game của mở đầu, hội thoại, sân trại, làng và bờ sông. Lượt cuối Console 0 lỗi; còn cảnh báo collider/asset có sẵn.
- Đối chiếu toàn bộ block có ID của scene gốc: không xóa hoặc sửa block cũ, ngoài danh sách SceneRoots thêm root gameplay. 5.628 transform gốc giữ nguyên. TestQuest và TerrainData có SHA256 giống trước khi chuyển.
- Chưa đo trên laptop Nitro RAM 16 GB / RTX3050; kiểm tra trên máy hiện tại không thay thế phép đo RAM/FPS trên laptop.

`Assets/Editor/BeachBoatQuestInstaller.cs` là công cụ chuyển một lần, không chạy trong game. Nếu cần dựng lại ở một bản scene chưa có gameplay, công cụ yêu cầu bản tạm đúng blob `origin/thai:Assets/Scenes/TestQuest.unity` tại `Assets/Editor/BeachQuestSource/ThaiTestQuest.unity`. Bản tạm và các controller/material không dùng đã được xóa khỏi dự án sau khi chuyển; scene đã lưu không phụ thuộc thư mục này.
