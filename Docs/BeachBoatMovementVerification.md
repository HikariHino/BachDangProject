# Điều khiển và cầu thang beachBoat — 16/10/2026

Đối chiếu với `origin/thai` (`cc7d5dd`): camera nhân vật chính xoay theo chuột, không có lệnh chuột phải để ngắm/zoom. Script cung có thao tác chuột phải nhưng không gắn trên nhân vật chính trong TestQuest đã kiểm tra. beachBoat tắt `enableShoulderAim`, giữ độ nhạy 2 / 1,5 và cho phép Shift chạy theo mọi hướng.

Nhân vật beachBoat vẫn scale 14. Tốc độ đi/chạy 60/110, gia tốc/phanh 180/240 tương ứng hai lần giá trị gốc của Thái. Di chuyển trên dốc bám theo mặt đỡ; vận tốc hướng lên khi leo dốc không bị nhận nhầm là đang nhảy.

## Cầu thang bục tướng

- Dùng mesh va chạm riêng `stage_walkable_collision.asset`, bỏ các mặt thô trong hành lang cầu thang. Mesh hiển thị của bục giữ nguyên.
- `Stage_Stair_Walkway` là BoxCollider nghiêng; `Stage_Stair_Landing` nối đỉnh thang với sàn bục. Cả hai không có renderer, material hay texture mới.
- Dời `Camp_Guard_Front_Left` sang nền bên cạnh: collider lính gác scale 14 trước đó chắn mép trái lối lên. Giữ model và collider của lính.
- Scene TestQuest không chỉnh sửa. SHA256 sau merge của người dùng và sau lượt sửa này cùng là `F58F9D958F69B43257EBAA378C59565041FF981CE0210DAEDC25E345C954648A`.

## Lỗi biên dịch sau merge

Merge `4eda28e` để lại lời gọi `OpeningEvent.RefreshHiddenUI()` từ bootstrap nhưng thiếu hàm tương ứng. Bổ sung hàm tương thích vào OpeningEvent dùng chung, gọi logic ẩn UI có sẵn trong lúc mở đầu còn hoạt động. Đây là thay đổi script dùng chung, không sửa scene TestQuest.

## Kiểm tra

Unity 6000.6.0f1, phiên `-batchmode -force-d3d11`, log `Logs/Editor-StairsVerification2.log`:

- 5/5 kiểm tra physics qua: đi lên giữa, mép trái, mép phải; chạy lên; đi xuống. Chạy chính FixedUpdate của nhân vật và Physics.Simulate, không dịch chuyển nhân vật qua các bậc trong quá trình kiểm tra.
- 43/43 kiểm tra Play Mode qua: mở đầu, gặp tướng, bốn cổng, năm gỗ, đóng cọc, báo cáo, đổi chế độ tham quan/cốt truyện, di chuyển và nhảy. Các kiểm tra gọi hàm tương tác; không phải ghi lại thao tác bàn phím/chuột của người chơi.
- Không lỗi biên dịch hoặc lỗi runtime game trong phiên thử; CLI có một lần timeout 5 giây khi tải scene, scene sau đó mở thành công.
- Kiểm tra cuối không có Missing Script. Terrain vẫn 49.547 cây. Mesh collision 4.406 tam giác, bộ nhớ Unity báo khoảng 396 KiB (không phải tổng RAM scene).
- Chưa đo lại trên laptop 16 GB / RTX 3050; không dùng kết quả máy hiện tại để khẳng định mức RAM/FPS trên laptop.

Dữ liệu và script kiểm tra nằm trong `.utmp/MovementFix` (bị Git bỏ qua): `stairs-checks.json`, `quest-result.json`, `final-audit.json`. Backup trước sửa phần nối bục: `beachBoat-before-landing.unity`.
