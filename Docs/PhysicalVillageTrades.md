# Bốn việc chuẩn bị vật tư trong không gian 3D

Bốn sạp trong làng dùng cùng nhân vật, camera, địa hình và va chạm của phần đi bộ. Bấm E gần sạp bắt đầu ngay một lượt; không có bảng phép tính, bảng nối dây hoặc chọn độ khó.

| Sạp | Việc phải làm | Điều khiển | Giới hạn / S / A / B |
|---|---|---|---|
| Gỗ | Lấy gỗ bên trái, mang đặt lên bàn, cưa, vác sang giá phải; ba khúc | E cầm/đặt; giữ chuột trái cưa | 75 / 45 / 60 / 71,25 giây |
| Dây | Lấy cuộn dây, mang tới bó cọc, tới và siết ba nút, giao bó đã buộc | E cầm/đặt; giữ chuột trái tại mỗi nút | 60 / 36 / 48 / 57 giây |
| Gạo | Mang bao rỗng lên bàn, đong tới vạch xanh, nhấc và giao; hai bao | Giữ chuột trái đong; nhả chuột khi đủ, E nhấc; dư thì E đổ lại | 75 / 45 / 60 / 71,25 giây |
| Lương khô | Lấy lần lượt phần gạo, cá khô và muối ở kệ; mang đặt lên khăn; gấp khăn và giao; hai gói | E cầm/đặt; giữ chuột trái gói | 90 / 54 / 72 / 85,5 giây |

WASD đi lại theo camera; chuột xoay nhìn. Vòng sáng và chữ ngắn nằm cạnh vật cần thao tác. HUD nhỏ ghi tiến độ, thời gian và điều khiển. H xem điều khiển, Enter tiếp tục. ESC/X bỏ lượt đang làm; ESC lần này không mở menu tạm dừng. Hết giờ hoặc quá ba lỗi thì R thử lại. Khi đủ hàng, Enter nhận vật tư. Có thể chơi lại sạp đã xong mà không mất vật tư đã nhận.

Không dùng công cụ hoặc lấy/giao vật từ xa: mỗi bước kiểm tra vị trí tiếp cận, độ cao và trạng thái hiện tại. Cưa, siết dây, đong và gói cần thời gian thao tác thực; giao vật mới tăng số đơn. Bàn và thùng có collider, vật đang cầm bám theo tư thế nhân vật. Bao gạo thiếu/đủ/dư có màu và chữ; đổ lại bao dư tính một lỗi và hai giây phạt, không cộng đơn.

Giữ renderer nhân vật và bộ điều khiển đi bộ sẵn có. Cánh tay được đặt tư thế mang/làm việc sau animation; công cụ, dây quấn, dòng gạo và các mép khăn được dựng/biến đổi trong thế giới 3D. Tạm ẩn vải phơi khi nó chắn camera trong lượt làm việc và khôi phục khi rời sạp.

## Lưu game và thành tích

Giữ định dạng save phiên bản 1 và bốn cờ vật tư hiện tại. Save cũ đã nhận hàng vẫn giữ hàng. Lượt đang mang công cụ/vật tư là trạng thái tạm; lưu tay bị chặn và tự lưu đợi tới khi rời sạp. Khi tải checkpoint, dọn vật cầm, bàn tạm, tư thế và camera trước khi phục hồi dữ liệu.

Mã kỷ lục mới là `village3d:0` đến `village3d:3`, tách khỏi `village:0:1` đến `village:3:1` của bản cũ. Không viết lại số giây cũ thành thời gian mới. Kỷ lục cũ được gắn nhãn **bản cũ** và xét theo giới hạn đã ghi trong chính kỷ lục đó. Nếu có cả hai loại kỷ lục, so sánh tỷ lệ thời gian/giới hạn để giữ kết quả tốt hơn, không làm mất thành tích vì một lượt chơi lại chậm hơn. Các việc 3D tính cả thời gian đi mang vật và phạt lỗi; mốc S/A/B lần lượt là 60%/80%/95% giới hạn. Thời gian thao tác tối thiểu cộng quãng đường trong sạp thấp hơn các mốc này, có dư để người chơi quan sát và di chuyển.

## Kiểm thử CLI

`BeachBoatPhysicalVillagePlaytest.RunBatch()` / `RunInEditor()` kiểm tra bốn luồng thao tác, chặn tương tác xa, ESC, tiếp tục sau xem điều khiển, tạm dừng đồng hồ, bao gạo dư, phần thưởng, save/load và thành tích cũ/mới. Báo cáo `Logs/PhysicalVillagePlaytest.json`, ảnh `Logs/Village3D_*.png`. Sử dụng thư mục save riêng và kiểm SHA-256 các save thật.

`BeachBoatCasualWorkPlaytest` chạy lại chuẩn bị đủ vật tư và đóng cả năm cụm cọc; `BeachBoatConcurrentBattlePlaytest` kiểm tra không ảnh hưởng Quest 3.

Các ca chạy nền gọi cùng hàm tương tác mà input sử dụng và render ảnh bằng camera Unity. Unity batch không có cửa sổ lấy focus, vì vậy không xác nhận khóa con trỏ của hệ điều hành; phần đó chỉ kiểm khi chạy ca ESC trong Editor tương tác. Kiểm tra CLI vẫn xác nhận đóng đúng hoạt động, trả về gameplay, không mở Pause bằng cùng lần ESC và khôi phục thời gian/thiết lập camera.

Kết quả kiểm tra ngày 27/10/2026: **282 kiểm tra qua** — hoạt động 3D 84, ESC/tạm dừng 92, vật tư/đóng cọc 46, Quest 3 60. Đã kiểm tra ảnh góc chơi và góc trực diện khi mang vật, cùng xác nhận bàn tay tới đúng vật đang cầm. Bộ kiểm thử kiểm SHA-256 các save thật và không sửa các file đó.
