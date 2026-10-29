# Thao tác vật tư, công binh, hồi máu và lệnh nhử địch

## Cưa và gói lương

Cưa chuyển động phát các mảnh gỗ nhỏ bằng particle mesh có trọng lực. Kết thúc tám nhịp tạo hai nửa riêng, một nửa ở bên trái đường cắt trên bàn, nửa còn lại được nhấc lên. Không chỉ thu ngắn khúc gỗ còn đặt giữa bàn.

Nguyên liệu gói được thu nhỏ và gom vào giữa khăn, chuyển phần thao tác vào trong mặt bàn. Các mép khăn gấp sát, nâng lên theo lớp và che nguyên liệu sau hai mép. Gói sau cùng sử dụng nguyên hierarchy khăn đã gấp và dây buộc, giữ scale 1 khi mang/đặt; không thay bằng khối cầu lớn.

## Công trường đóng cọc

Bốn thuyền công binh, tối đa tám người, chia thành các cặp giữ/nện chung một cọc. Người giữ đứng trên ván thấp; người nện đứng trên sàn gỗ cao hơn, có collider đỡ chân thật. Búa lấy điểm nắm thật của bàn tay, tránh búa treo rời người. Các tổ có nhịp khác nhau, tiếng nện nhỏ, nước bắn và hai câu hô ngắn. Tài nguyên cọc vẫn chính là mesh của trận địa sau chuyển thời gian.

Người chơi giữ vị trí đã kiểm tra trên sàn thuyền. Camera khi nện lấy điểm nhìn tại đầu nhân vật trong lòng thuyền, thay cho bay ra trên mặt nước cạnh cọc. Đầu cọc đặt ở tầm ngực và hạ dần theo từng nhát; camera nhìn xuống vị trí làm việc. Ngoài lực và tâm, nhả búa khi vạch nhịp vào vùng xanh. Sau ba cọc vẫn chuyển cảnh “Vài ngày sau…” và dựng toàn trận địa.

## Thuốc hồi máu và thể lực 3D

Ô vuông có biểu tượng băng thuốc nằm giữa sát dưới màn hình, hiện số lượng và phím H. Bắt đầu với ba item, mỗi item hồi tối đa 35 máu; đầy máu hoặc hết item không tiêu hao. Quân y tiếp thêm tối thiểu ba item sau khi hoàn thành băng bó. Số lượng có trường version riêng trong save; save cũ nhận bộ ban đầu, save mới có số lượng 0 giữ nguyên 0.

Luyện thể lực là vác bao cát qua các cổng gỗ có collider, nhảy qua thanh thấp và đem bao về điểm xuất phát. Không còn tăng tiến độ bằng nút nhịp Space cũ. Chỉ hoàn thành đường tập và trả bao mới nhận +20 thể lực tối đa của cấp kế tiếp; cấp đã nhận không cộng lần nữa. Có 65 giây, ba cấp vẫn được lưu, ESC dọn course và giữ các phần thưởng đã nhận. Đây là bài phụ và không thêm điều kiện xếp hạng chiến dịch.

## Lệnh xuất phát và tàu đắm

Ngô Quyền dặn rõ chèo ra nhử địch rồi dẫn vào cọc. Người chưa đạt cung được nhắc riêng và chưa thể xuất phát. Cờ `lureBriefed` giữ trạng thái đã nghe lệnh; save cũ trước trận nhận lời dặn trước khi bắt đầu, save đang chiến đấu tiếp tục bình thường. Callback của lời dặn được khai báo rõ để lưu/tải giữa hội thoại.

Âm la thất thanh dùng các clip người thật CC0 trong `WreckAudio`, phát ngay ở sự kiện đắm trên một kênh riêng. Một clip phải phát xong mới có clip kế tiếp, tránh nhiều tàu cùng đắm làm chồng/cắt tiếng. Các lời chuẩn bị mới trong `Voices/Preparation` được tạo bằng Microsoft Edge TTS tiếng Việt theo quyền tạo giọng đã có, chỉ gửi nội dung các câu thoại. Không thay các clip giọng nhân vật khác.

## Kiểm chứng ngày 29/10/2026

Chạy trong Play Mode qua Unity CLI, không điều khiển giao diện máy tính. Các suite ghi kết quả trong `Logs/`, chỉ dùng thư mục save tạm `.utmp/` và đối chiếu SHA-256 của 13 file save người chơi:

- `BeachBoatCasualWorkPlaytest`: 59 kiểm tra về ba cọc mẫu, nhịp/lực/tâm, bàn tay, camera nằm trên sàn thuyền, tổ công binh và chuyển thời gian.
- `BeachBoatRecovery3DPlaytest`: 39 kiểm tra về thuốc, di trú save, đường thể lực, chặn lưu giữa bài tập, dọn course khi load, âm la và hội thoại xuất quân có clip thật.
- `BeachBoatArcheryPlaytest`: 80 kiểm tra về cung bắt buộc, bia đổ, điểm số, tư thế và save.
- `BeachBoatPhysicalVillagePlaytest`: 461 kiểm tra về bốn sạp, ray chuột, âm công việc, mảnh gỗ/hai nửa cưa, gói khăn cùng scale và dọn thao tác khi thoát/load.

Ảnh từ camera của game: `StakeConstruction_Team.png`, `StakeConstruction_Hammer.png`, `Village3D_Timber.png` và `Village3D_Provisions.png`. Chuyển động công binh dùng tư thế IK theo nhịp trên rig hiện có.
