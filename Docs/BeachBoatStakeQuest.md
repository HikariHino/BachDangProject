# Nhiệm vụ đóng cọc tại vị trí chỉ định — 16/10/2026

## Chơi trong beachBoat

1. Nhận lệnh Ngô Quyền, qua cổng sang làng và nhặt đủ 5 cọc bằng E. Có 7 vị trí để lựa chọn.
2. Về báo cáo, nhận **Nhiệm vụ 2: Đóng cọc**, qua cổng ra bãi sông.
3. Đến từng vòng vàng đánh số 1–5, bấm E để đóng một cọc. Có thể làm theo thứ tự bất kỳ; HUD chỉ tới điểm chưa hoàn thành gần nhất.
4. Mỗi lượt hạ cọc mất 1,4 giây và khóa di chuyển/tương tác trong lúc thực hiện. Cọc đã đóng giữ lại trong cảnh, vòng đánh dấu biến mất, bộ đếm tăng đúng một lần.
5. Đủ 5/5 mới mở cổng về và cho phép báo cáo hoàn thành với Ngô Quyền.

Không còn bấm E một lần ở bãi để hoàn thành toàn bộ bằng chuyển cảnh. Không thể đóng tùy chỗ, đóng lặp một điểm, đóng ngoài tầm hoặc bỏ qua năm điểm để hoàn thành.

## Model và vị trí

- Thay 7 chồng gỗ cũ bằng mesh `Assets/Models/ModelAI/CayGo/CayGo/CayGo.fbx` đang dùng trên các `CayGo` trong TestQuest của Thái. Giữ texture của `CayGo.mat`, dùng một material riêng bật GPU instancing để không sửa material nguồn.
- Cọc nhiệm vụ đặt theo tỷ lệ nhân vật scale 14, chiều dài khoảng 32 đơn vị. Các cọc đã đóng dùng chung nguồn model.
- `RiverStakeWork` có năm phần tử `placements`: `approach` là vị trí đứng làm việc trên bờ, `marker` là vòng/chữ đánh dấu, `stake` là cọc được hạ xuống phía mép nước. Có thể di chuyển các điểm này trong Edit Mode để đổi vị trí chỉ định.
- Điểm đến `RiverArrival` chuyển ra phía bãi sông, ngoài hàng rào đảo. `RiverToCamp` và khung `Visible_Story_Gate` được căn cùng vị trí, để đường về không xuyên qua hàng rào hoặc bị cột cổng chặn.

## Giữ nhẹ

Toàn bộ 12 visual dùng chung mesh và texture nguồn, không sao chép texture theo từng cọc. Có LOD cull từ xa, không thêm đèn hay bóng động, không dùng MeshCollider cho cọc nhặt. Năm vòng đánh dấu dùng LineRenderer và font tiếng Việt đã có; không nạp TestQuest khi chơi. Danh sách năm điểm được giữ sẵn, không tìm toàn scene mỗi frame.

Model Thái có 288.364 đỉnh, Unity báo khoảng 38,75 MiB cho mesh và 3,33 MiB cho texture dùng chung. Đây vẫn là model chi tiết cao; culling chỉ giảm chi phí vẽ, không làm mất phần bộ nhớ asset đang được tham chiếu. Chưa đo trực tiếp trên laptop 16 GB / RTX 3050.

## Bảo toàn và kiểm tra

- 88/88 kiểm tra luồng nhiệm vụ đạt: nhận lệnh, cổng, nhặt đúng model, đủ 5 cọc, khóa mở đầu/hội thoại/sa bàn, từ chối sai bước/ngoài tầm/điểm trùng/thiếu cọc, hủy thao tác đang làm, làm năm điểm theo thứ tự 3–1–5–2–4, khóa cổng về khi chưa đủ, báo cáo và di chuyển/nhảy sau hoàn thành.
- 6/6 chặng đi bộ bằng FixedUpdate + Physics.Simulate đạt trên scene cuối: từ RiverArrival qua năm vị trí và quay về RiverToCamp. Khởi động lượt Play mới xác nhận 7 cọc nhặt có lại, tiến độ 0/5 và cọc chưa đóng được ẩn.
- 0 Missing Script; đủ 7 vật nhặt, 5 vị trí, chỉ 1 mesh dùng chung; terrain vẫn 49.547 cây. Không có lỗi biên dịch/runtime game trong lượt Play cuối. Log có lỗi prefab đá thiếu từ lúc đọc preview TestQuest nguồn; không chép các prefab lỗi đó sang beachBoat.
- Đã xem ảnh render `markers-preview.png` và `planted-result.png`: chữ tiếng Việt đầy đủ, vòng vàng đánh số, bảng tiến độ và cọc đã đóng hiển thị. Đây là ảnh camera bố trí để kiểm tra, không phải ảnh ghi lại thao tác bàn phím.
- Tiến trình Editor ở cuối lượt thử: working set 3,43 GiB, private memory 5,25 GiB trên máy hiện tại. Đây là số đo tại một thời điểm, không phải RAM đỉnh hay kết quả laptop.
- Scene TestQuest không đổi: SHA256 `F58F9D958F69B43257EBAA378C59565041FF981CE0210DAEDC25E345C954648A`.
- Các sửa điều khiển/cầu thang trước đó giữ nguyên. Không sửa địa hình, cây rừng hoặc đưa map cũ sang.
- Dữ liệu kiểm tra trong `.utmp/StakeQuest` (Git bỏ qua), gồm `quest-result.json`, `walk-result.json`, ảnh render và backup `beachBoat-before-stakes.unity`.
- Công cụ `Bach Dang/Install Designated Stake Quest` chỉ cài một lần, từ chối nếu đã có các điểm đóng cọc; không chạy lại builder map/doanh trại.

Kiểm tra Play dùng các hàm tương tác thật của game và mô phỏng physics; không thay thế trải nghiệm bàn phím/chuột thực tế trên laptop.
