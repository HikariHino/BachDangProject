# Bản sửa phản hồi hình và clip 29/10/2026

## Làng và hướng dẫn

Sạp gạo tự chuyển tới vị trí đất khô thoáng, ngoài diện tích phơi vải, cột và lối thao tác của người dân. Kiểm tra cả khoảng cách tới khu phơi lẫn bounds vật thể, thay vì chỉ xét collider. Tất cả đồ và người bán đi cùng sạp; save không giữ một bản vị trí sạp cũ riêng.

Bốn minigame dùng bốn góc đánh dấu nhỏ quanh vật cần lấy/đặt, bỏ các vòng vàng nổi và vòng đo gạo quanh bao. Thanh lượng gạo trên UI tiếp tục thể hiện thiếu/đủ/dư. Dây buộc thật vẫn là vật thể của công việc.

Lời Ngô Quyền chỉ yêu cầu tới bãi tập và bắn đủ bia; không đọc phím điều khiển. Chỉ đường ưu tiên đúng vị trí tập cung khi còn thiếu điều kiện này. Hội thoại luyện cung đang mở trong save cũ được đổi sang lời mới; phím E nằm ở UI. Clip `ngo_archery_natural` được tạo bằng Edge tiếng Việt theo quyền tạo giọng đã có.

## Thể lực, cung và bơi

Sân thể lực là bài tiếp tế riêng, không dựng khung nhảy ở cửa lều. Sân dùng địa hình tạm độc lập, hàng rào và đèn, cùng vùng sáng riêng để tránh cây/đá terrain xuất hiện sau khi dịch chuyển. Người chơi được đưa tới sân, vác bao tới bệ có cờ vàng, đặt xuống và quay về lấy bao tiếp. Giao đủ 3/4/5 bao tương ứng ba cấp. Mọi đường thoát đều khôi phục vị trí vào lều; load dọn đạo cụ, địa hình và vùng sáng trước khi áp dụng vị trí trong save.

Cung người chơi dùng góc đứng ổn định, độ duỗi tay có giới hạn và điểm kéo sát má. Chuỗi cung và tên theo vị trí hai bàn tay đã giải IK, kể cả khi ngắm dốc. Bộ điều khiển đi bộ không xoay ngược người trong khi giương cung.

Cung thủ trên bờ kéo dây 0,73 giây trước khi tên rời cung, thả dây, hạ tay rồi mới kéo lần tiếp. Có dây cung và tên trên tay. Chân bơi dùng đạp luân phiên, gần duỗi, mặt phẳng gối hướng xuống thay cho gập IK ngang ra sau.

## Nhử địch và âm thanh

Khi bị phát hiện, thuyền nhỏ tiếp tục dùng điều khiển chèo bình thường. Năm loạt tên xuất hiện theo tiến độ trở về trận địa. Vùng chữ nhật đỏ trên nước báo trước 1,2 giây; tên bay thêm 0,7 giây tới điểm đã khóa theo hướng và tốc độ lúc bắn. Đổi hướng hoặc thay tốc độ để né; tên không đuổi theo mục tiêu sau cảnh báo. Một loạt trúng trừ 16 độ bền, không trừ nhiều lần cho từng mũi tên trong loạt.

Phải qua năm loạt và thực sự vào bãi cọc mới chuyển đội hình địch vào trận địa. Thủy triều vẫn dùng cơ chế rút trong lúc đuôi đội hình tiến vào. Save ghi tiến độ, vùng tên chờ, thời gian còn lại và lượt trúng; save cũ đã vào trận địa không bị bắt quay lại nhử.

Tiếng nện dùng clip gỗ riêng, cân mức âm lượng và giới hạn đỉnh, phát đúng lúc búa chạm. Khi phục kích, tiếng Ngô Quyền, tiếng hoảng và tiếng la cùng một hàng phát; không cắt câu đang nói, tối đa hai tiếng la và một câu hoảng trong một trận. Mặt nạ thoại và số lần la được lưu để hạn chế lặp khi load.

Các ảnh và báo cáo kiểm thử qua Unity CLI nằm trong `Logs/Review29*`, `Logs/Recovery3DPlaytest.json` và các suite hồi quy cung, vật tư, đóng cọc, toàn trận. File save thật được đối chiếu SHA-256; kiểm thử dùng `.utmp`.

## Kết quả kiểm thử

742 kiểm tra đạt: Review29 (39), Recovery3D (41), ConcurrentBattle (62), Archery (80), CasualWork (59), PhysicalVillage (461). Kiểm tra cả né/trúng tên, lưu giữa loạt tên chờ, chặn bỏ qua thử thách, save cũ, câu thoại, động tác cung thủ, góc gập chân bơi, âm nện, chuyển sân và trở về lều. Trận đầy đủ đi qua nhử địch, đội hình vào bãi, rút nước, mắc cọc và giải phóng phản công. 13 file save người chơi giữ nguyên SHA-256.
