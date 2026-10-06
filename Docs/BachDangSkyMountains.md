# Bầu trời ngày đêm và núi đá vôi

Scene đã chỉnh: `Assets/Scenes/beachBoat.unity`, Unity 6000.6.0f1.

## Cách xem

Bấm Play. Bảng điều khiển có **Sáng**, **Hoàng hôn**, **Đêm** và **Tự chạy chu kỳ ngày / đêm**. Phím **H** ẩn/hiện bảng. Một ngày trong game mặc định dài **12 phút**, bắt đầu lúc **09:00**.

Chọn **Directional Light → Day Night Cycle** trong Inspector:

- `Time Of Day`: giờ 0–24, xem trước cả trong Edit Mode.
- `Day Length Minutes`: thời lượng một ngày đầy đủ.
- `Auto Advance`: tự chạy giờ trong Play Mode.
- `Cloud Degrees Per Second`: tốc độ trôi mây.
- `Moonlight Intensity`: độ sáng ánh trăng, hiện là 0.35.

## Thay đổi

- Sky shader hòa trộn hai panorama Sunless/Moonless sẵn có. Mặt trời và mặt trăng được vẽ riêng, di chuyển theo hướng nguồn sáng; mây trôi chậm. Ánh sáng, màu sương và ánh sáng môi trường đổi theo giờ. Phản xạ môi trường cập nhật tối đa mỗi 5 giây.
- Controller dùng bản sao vật liệu riêng khi chạy/xem trước. Scene lưu tham chiếu asset thật, đã xác nhận vẫn hoạt động sau khi mở lại Editor.
- Sáu vùng núi cũ có các đỉnh cao thấp, khe và vách đá rõ hơn. Đá được sơn theo độ dốc thực; cây/cỏ trên vách bị tỉa, cây còn lại được đặt lại theo mặt đất.
- TerrainData mới nằm tại `Assets/BachDangAtmosphere/BachDang_KarstTerrain.asset`; collider dùng cùng dữ liệu. NatureRenderer được khởi tạo lại để bỏ cache cây của địa hình cũ.
- Nhóm trang trí núi cũ được giữ ở trạng thái tắt. Các khối đá mới chỉ đặt trên phần đỉnh có độ dốc phù hợp, tránh tảng đá nhô lơ lửng ở vách.
- Menu `Polish Current Battlefield` giữ hệ thống ngày đêm khi nó đang được cấu hình và bật.

## Kiểm tra đã thực hiện

- C# biên dịch và shader trời không có lỗi; kiểm tra Play Mode, cảnh sáng/hoàng hôn/đêm, mặt trăng và HUD.
- Chạy tăng tốc qua mốc 24 giờ: thời gian thực tế khớp công thức chu kỳ, sai số khoảng 0.00002 giờ game.
- 432.861 mẫu độ cao được thay đổi trong vùng núi. Các mẫu gốc từ 28m trở xuống giữ nguyên hoàn toàn; lớp phủ vùng thấp cũng không thay đổi.
- Cây terrain: 51.054 → 50.645. Sáu nhóm núi và texture đá vẫn đúng sau khi khởi động lại Unity.
- Scene đã lưu ở Edit Mode, 09:00, chu kỳ 12 phút và tự chạy khi Play.
- Chưa kiểm tra bản build hoặc đo hiệu năng trên máy đích. Lỗi tài liệu nhà phát hành TheTalesFactory đã được ghi riêng trong `BachDangMapReview.md`.

Ảnh trước/sau và bản sao scene trước khi chỉnh núi ở `.utmp/SkyMountains/` (Git bỏ qua). Mốc trước khi chỉnh có tên `beachBoat-before-karst-*.unity`. Các thay đổi chưa commit/push.

Menu `Bach Dang → Refine Existing Karst Mountains` chỉ áp dụng một lần; gọi lại khi nhóm mới đã tồn tại sẽ giữ nguyên địa hình. Không chạy lại công cụ dựng toàn bộ terrain hoặc `SpawnMountainsOnly()` để tinh chỉnh lượt này, vì các công cụ cũ có phạm vi thay đổi rộng hơn.
