# Tỷ lệ nhân vật và sinh hoạt làng — 05/10/2026

> Cập nhật 09/10/2026: xem [Làng tự nhiên và ngân sách laptop](BachDangLaptopAndNaturalVillage.md). Thiết lập cây, tầm nhìn và bộ nhớ trong tài liệu mới thay thế các ghi chú Nature Renderer/tầm nhìn trước đây ở trang này.

Scene làm việc: `Assets/Scenes/beachBoat.unity` (bản có núi, ngày–đêm, làng và doanh trại cải tiến).

## Tỷ lệ

Nhân vật tham chiếu `Main_Character` trong TestQuest có scale `(7,7,7)`, chiều cao mesh khoảng 13,59 đơn vị Unity. Cảnh beachBoat được đổi đồng bộ sang **7 đơn vị Unity trên một mét tham chiếu**: nhà, đồ đạc, nhân vật trang trí có sẵn, cây, thuyền, địa hình và vị trí camera cùng nhân 7. Bố cục và khoảng cách tương đối giữ nguyên. Terrain có kích thước nội bộ 42.000 × 2.100 × 42.000; tương ứng chiến trường 6 × 6 km trước khi đổi đơn vị.

Không thêm model người; các mesh, material và bộ animation của nhân vật có sẵn được giữ. Không chỉnh scene TestQuest hoặc ghép controller/nhiệm vụ vào beachBoat.

Thủy triều dùng cao độ nội bộ 70–108,5, tương ứng 10–15,5 m trên HUD. Tốc độ thuyền, biên độ nhấp nhô, tầm camera, sương và các khoảng cách của shader nước được đổi theo tỷ lệ. `BachDangWorldScale` dùng bản URP tạm trong Play Mode để tăng tầm bóng đổ và đổi trọng lực sang −68,67; trả lại thiết lập cũ khi dừng Play. Có xử lý chế độ Play không tải lại scene/domain của dự án.

Khi sau này ghép controller của bạn cùng nhóm vào map, các tốc độ/khoảng cách viết cứng theo mét phải dùng cùng hệ đơn vị. Ví dụ tốc độ 3 m/s tương ứng 21 đơn vị Unity/s. Không nhân scale 7 thêm lần nữa cho nhân vật đã có scale 7.

## Sinh hoạt đã thêm

Nhóm `BachDang_Living_Settlements/09_Sinh_Hoat` bổ sung cho ba khu dân cư:

- 9 vườn, 180 cụm cây trồng, hàng rào và dụng cụ làm vườn.
- 3 giếng chung có thành rỗng, khung kéo gầu và xô nước.
- 6 sân phơi vải, dây phơi và chậu giặt.
- 6 sân làm nghề/nấu ăn, mái che, củi, đồ dự trữ và khu phơi.
- 6 cụm hàng hóa cạnh chợ, cùng lối đất nối vào các sân.

Giữ 60 nhà dân và 18 lán quân đã có. Các chi tiết mới tái sử dụng tài nguyên hiện tại, gợi sinh hoạt làng; không phải phục dựng khảo cổ chính xác.

## Dữ liệu và cách xem

- Mở beachBoat rồi Play. **5**: làng chợ; **6**: xóm chài; **7**: trại tiếp vận; **8**: xóm vườn. **H** ẩn/hiện HUD; nút Sáng/Hoàng hôn/Đêm đổi thời gian; **T** đổi chiều triều.
- Dữ liệu mới ở `Assets/BachDangScaleAndLife`. Terrain và các lớp phủ dùng bản riêng; tài nguyên nguồn không bị sửa.
- Cây vườn dùng hai material URP Lit riêng để tránh ngưỡng ẩn 40–80 đơn vị của vật liệu cỏ cũ; chiều cao tham chiếu khoảng 0,41 m và 0,57 m. Giếng dùng texture đá lặp có sẵn thay vì atlas của đá tảng.
- Thư mục `Vegetation` có 16 prefab variant phục vụ riêng TerrainData mới. Chỉ ngưỡng LOD cuối được chia 7 để sửa giới hạn ẩn cây của Nature Renderer; không tăng chi tiết các LOD gần. `BachDang_ScaledVegetation_Defaults_Player7` giữ tầm nhìn cỏ theo cùng hệ đơn vị.
- Bản sao scene trước khi đổi tỷ lệ ở `.utmp/ScaleAndLife/beachBoat-before-scale-and-life.unity`; thư mục `.utmp` không đưa vào Git.
- `BachDangScaleAndLife.Apply()` có chặn áp dụng tỷ lệ lặp. Không chạy lại các builder đời trước để thay scene hiện tại.
- Menu **Bach Dang → Polish Village Gardens And Wells** chỉnh trực tiếp vườn/giếng hiện có; **Restore Scaled Forest Visibility** sửa khoảng cách ẩn cây. Hai thao tác giữ bố cục, có backup và chống nhân tỷ lệ lặp.

## Kiểm tra đã thực hiện

- So sánh toàn bộ heightmap và alphamap với TerrainData trước đổi tỷ lệ: sai khác bằng 0.
- TerrainCollider dùng đúng dữ liệu mới; còn 50.532 cây terrain sau khi dọn 10 cây trong các sân mới.
- Đủ 60 nhà, 18 lán, 20 SkinnedMeshRenderer có sẵn; không Missing Script trong scene.
- Play Mode: cả 8 góc camera đến đúng vị trí. Năm thuyền có TidalBoatFloat giữ đúng waterline ở cả triều thấp và cao, sai lệch trong biên độ sóng 0,28 đơn vị. Cao độ shader nước khớp mặt nước 70 và 108,5.
- Scene TestQuest, dữ liệu làng nguồn và ProjectSettings không có thay đổi trong Git.
- Mở lại Unity vẫn giữ đủ công trình và dữ liệu địa hình. Đã xem ảnh cận vườn, giếng, giếng ban đêm và HUD; rừng xa hiển thị trở lại sau sửa LOD.
- Dừng Play trả trọng lực về −9,81 và render pipeline về `PC_RPAsset`. Không ghi bản pipeline tạm vào ProjectSettings.
- Lượt Play lặp lại vẫn có gravity −68,67, shadow distance 2.450 và 8 góc camera; hệ số không nhân chồng. Kết thúc ở Edit Mode, scene đã lưu, 09:00, tự chạy ngày–đêm và góc toàn làng.

Ảnh và báo cáo kiểm tra lưu tại `.utmp/ScaleAndLife`: `garden-final.png`, `well-final.png`, `well-night-final.png`, `hud-final.png`, `verification.json`, `runtime-high.json`, `runtime-low.json`. Chưa xuất build hoặc đo hiệu năng trên máy đích. Gói TheTalesFactory vẫn có lỗi parser tài liệu `PublisherReadme.asset` đã tồn tại trước thay đổi này; CLI có báo timeout trong lúc Unity tạo/nạp terrain lớn, nhưng các thao tác lưu sau đó được kiểm chứng thành công.
