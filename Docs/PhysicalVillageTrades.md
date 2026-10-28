# Bốn việc chuẩn bị vật tư trong không gian 3D

Bốn sạp trong làng dùng cùng nhân vật, camera, địa hình và va chạm của phần đi bộ. Bấm E gần sạp bắt đầu ngay một lượt; không có bảng phép tính, bảng nối dây hoặc chọn độ khó.

| Sạp | Việc phải làm | Điều khiển | Giới hạn / S / A / B |
|---|---|---|---|
| Gỗ | Lấy gỗ bên trái, mang đặt lên bàn, cưa, vác sang giá phải; ba khúc | E cầm/đặt; giữ chuột trái kéo LÊN/XUỐNG dọc rãnh; rê ngang căn giữa | 75 / 45 / 60 / 71,25 giây |
| Dây | Lấy cuộn dây, mang tới bó cọc, tới và siết ba nút, giao bó đã buộc | E cầm/đặt; kéo dây quanh bốn điểm của mỗi nút | 60 / 36 / 48 / 57 giây |
| Gạo | Mang bao rỗng lên bàn, đong tới vạch xanh, nhấc và giao; hai bao | Bấm giữ gáo trong rổ, kéo qua bao rồi thả; E/Enter nhấc khi đủ; R bớt một gáo, C đổ lại | 75 / 45 / 60 / 71,25 giây |
| Lương khô | Lấy lần lượt phần gạo, cá khô và muối ở kệ; mang đặt lên khăn; gấp khăn và giao; hai gói | E cầm/đặt; kéo bốn mép khăn vào giữa | 90 / 54 / 72 / 85,5 giây |

WASD đi lại khi mang hàng; sau khi đặt lên bàn, camera cận mặt bàn chỉ hiện tay và dụng cụ, khóa đi bộ và xoay camera. Con trỏ hiện ra: bấm giữ dụng cụ/mép khăn rồi kéo trực tiếp theo vị trí trên bàn. Lệch rãnh cưa không tính nhịp; dây cần thả trong dải lực xanh; gáo gạo thả trên miệng bao mới đổ; khăn cần ép nếp 0,45 giây. Sai thao tác nhẹ chỉ thử lại bước đó. HUD ghi tiến độ, thời gian và điều khiển. H xem điều khiển, Enter tiếp tục. ESC/X bỏ lượt đang làm; ESC lần này không mở menu tạm dừng. Hết giờ hoặc quá ba lỗi thì R thử lại. Khi đủ hàng, Enter nhận vật tư. Có thể chơi lại sạp đã xong mà không mất vật tư đã nhận.

Không dùng công cụ hoặc lấy/giao vật từ xa: mỗi bước kiểm tra vị trí tiếp cận, độ cao và trạng thái hiện tại. Cưa, siết dây, đong và gói cần chuyển động dụng cụ đúng; giữ chuột đứng yên không tự hoàn thành; giao vật mới tăng số đơn. Bàn và thùng có collider, vật đang cầm bám theo tư thế nhân vật. Bao gạo có thanh thiếu vàng/đủ xanh/dư đỏ và ba nút bấm trực tiếp. Ba gáo 24% tạo mức đủ 72% (dải 65–85%). Xác nhận thiếu/dư chỉ giải thích thao tác, không tự xóa gạo; R bớt một gáo, C đổ lại, đồng hồ tiếp tục chạy. Thả ngoài bao giữ gạo trong gáo và nhắc vị trí cần thả.

`VillagePhysicalWork.PointerCoordinates` nghịch phép chiếu mặt phẳng thao tác 3D để lấy vị trí con trỏ thật trên bàn; không cộng dồn Mouse X/Y. Dùng cùng phép chiếu với hình ảnh, tránh lệch giữa tia chuột và camera có jitter tại tọa độ map lớn. Khoảng lệch lúc bấm được giữ lại để dụng cụ không giật tới con trỏ. Mặt phẳng gói khăn tách khỏi độ nâng bàn tay. Tư thế tay tiếp tục dùng đoạn xương dài cố định.

## Âm thanh thao tác

`VillageWorkFoley` dùng một kênh ma sát (cưa/dây/vải) và cặp kênh thao tác ngắn có fade (xúc, đổ gạo, ép nếp, siết nút, đặt hàng). Ma sát chỉ phát khi kéo hoặc siết/ép và giảm nhanh khi ngừng; số nguồn phát được giới hạn. Help/tạm dừng ngừng cả ba nguồn; ESC/load hủy nguồn phát. Tuân theo master volume, F10 và âm lượng người chơi.

Tám WAV mono 32 kHz hiện lấy từ bản thu thật tìm trên YouTube, thay bộ nhiễu tổng hợp cũ. Có fade đầu/cuối, lọc bớt dải chói và đỉnh không quá 0,22. File nằm trong `Assets/Resources/BachDang938/WorkAudio`; nguồn, quyền dùng do tác giả nêu và quy trình cắt nằm trong [TradeAudioSources.md](TradeAudioSources.md).

`VillageWorkstation` dựng bố trí cố định cho cả bốn sạp khi scene bắt đầu: cùng bàn, mái, thùng lấy/giao hàng và nguồn vật tư trước E, trong và sau lượt chơi. Không ẩn mái hay thay bàn lúc mở minigame. Cánh tay được đặt tư thế mang/làm việc sau animation; dụng cụ, dây quấn, dòng gạo và mép khăn biến đổi trong thế giới 3D. Thanh lực buộc dây nằm ở giữa phía dưới màn hình; tay trái giữ bó gỗ, tay phải kéo dây, cổ tay dùng trục bind pose trung tính. Vải phơi chỉ tạm ẩn khi chắn camera và khôi phục khi rời sạp.

## Lưu game và thành tích

Giữ định dạng save phiên bản 1 và bốn cờ vật tư hiện tại. Save cũ đã nhận hàng vẫn giữ hàng. Lượt đang mang công cụ/vật tư là trạng thái tạm; lưu tay bị chặn và tự lưu đợi tới khi rời sạp. Khi tải checkpoint, dọn vật cầm, dụng cụ, tư thế và camera trước khi phục hồi dữ liệu. Bàn/mái cố định của sạp vẫn giữ nguyên.

Mã kỷ lục mới là `village3d:0` đến `village3d:3`, tách khỏi `village:0:1` đến `village:3:1` của bản cũ. Không viết lại số giây cũ thành thời gian mới. Kỷ lục cũ được gắn nhãn **bản cũ** và xét theo giới hạn đã ghi trong chính kỷ lục đó. Nếu có cả hai loại kỷ lục, so sánh tỷ lệ thời gian/giới hạn để giữ kết quả tốt hơn, không làm mất thành tích vì một lượt chơi lại chậm hơn. Các việc 3D tính cả thời gian đi mang vật và phạt lỗi; mốc S/A/B lần lượt là 60%/80%/95% giới hạn. Thời gian thao tác tối thiểu cộng quãng đường trong sạp thấp hơn các mốc này, có dư để người chơi quan sát và di chuyển.

## Kiểm thử CLI

`BeachBoatPhysicalVillagePlaytest.RunBatch()` / `RunInEditor()` kiểm tra bốn luồng thao tác, chặn tương tác xa, ESC, tiếp tục sau xem điều khiển, tạm dừng đồng hồ, bao gạo dư, phần thưởng, save/load và thành tích cũ/mới. Báo cáo `Logs/PhysicalVillagePlaytest.json`, ảnh `Logs/Village3D_*.png`. Sử dụng thư mục save riêng và kiểm SHA-256 các save thật.

`BeachBoatCasualWorkPlaytest` chạy lại chuẩn bị đủ vật tư và đóng cả năm cụm cọc; `BeachBoatConcurrentBattlePlaytest` kiểm tra không ảnh hưởng Quest 3.

Các ca chạy nền gọi cùng hàm tương tác mà input sử dụng và render ảnh bằng camera Unity. Unity batch không có cửa sổ lấy focus, vì vậy không xác nhận khóa con trỏ của hệ điều hành; phần đó chỉ kiểm khi chạy ca ESC trong Editor tương tác. Kiểm tra CLI vẫn xác nhận đóng đúng hoạt động, trả về gameplay, không mở Pause bằng cùng lần ESC và khôi phục thời gian/thiết lập camera.

Đợt sạp cố định và cải thiện độ sáng: **238 kiểm tra qua** — bốn sạp/ánh sáng/camera/thanh lực/cổ tay/ESC/load/thành tích 171, vật tư và năm cụm đóng cọc 67. Đã so sánh chính các vật thể và ảnh sạp trước/sau E, xác nhận cột mái không xuyên thùng, chụp thanh lực và ảnh doanh trại trước/sau tăng sáng. Các đoạn tay không kéo giãn, cổ tay dùng trục bind pose; nhân vật và camera khôi phục khi giao hàng, thoát hoặc load. Bộ kiểm thử kiểm SHA-256 các save thật và không sửa các file đó.

Thao tác kéo dụng cụ, camera cận bàn, đóng cọc và tập cung được mô tả thêm trong [WorldHandInteractions.md](WorldHandInteractions.md).

Đợt sửa hướng chuột và chân Ngô Quyền: 202 kiểm tra qua. Xác nhận bốn hướng
trên ảnh chiếu thật của cả bốn minigame, bao gồm lúc đang nâng khăn; vạch xanh
ẩn ở bước lấy hàng và chỉ hiện khi cưa; đế giày Ngô Quyền chạm sàn trước/sau
load. Chụp ảnh sau khi camera đã chuyển góc ổn định, dùng save tạm và giữ nguyên
SHA-256 của các save thật.

Đợt sửa theo clip 28/10 và thêm foley: **358 kiểm tra qua** trong
`PhysicalVillagePlaytest.json`. Luồng chơi dùng `PressPointer`/`DragPointer`,
xác nhận nghịch chiếu dưới một pixel, hoàn tất cả bốn sạp, thử nhả sai chỗ,
thiếu/dư gạo, bớt/đổ lại, nhấc và giao bao, siết/ép, tay hiển thị chạm vật,
âm thanh đúng công việc, pause/ESC/load và thành tích. Chụp thêm Rice_Entry,
Rice_Excess, Rice_Ready. SHA-256 các save thật giữ nguyên.

`BeachBoatCasualWorkPlaytest.RunBatch` qua thêm **67 kiểm tra** chuẩn bị vật tư
và dựng năm cụm cọc. Tổng hai bộ: **425**; 13 file save thật được kiểm hash
trong mỗi bộ và không thay đổi. Hai bộ chạy bằng Unity 6000.6.0f1 / DX11;
không phát sinh lỗi C# hoặc exception trong log của lượt chạy đạt.

Đợt clip 15:00, dáng ngón và âm thanh thu thật ngày 28/10: **470 kiểm tra qua**
(403 minigame, 67 vật tư/đóng cọc). Có ca bắt mép lệch 12/7 pixel, kéo ngang
với lệch dọc, rê quá tâm theo ba hướng và giữ nếp không bật lại. Kiểm mesh có
sáu dáng ngón, đốt ngón thực sự biến dạng, tư thế hiển thị đúng ở cả bốn sạp,
tay giữ nguyên chiều dài/cổ tay trung tính, đủ tám clip thu thật có dữ liệu
âm khác im lặng và mức RMS/peak nằm trong giới hạn. Đã render xem lại ảnh tay
ở bàn. Save tạm riêng; 13 file save thật giữ nguyên SHA-256 trong mỗi bộ.
