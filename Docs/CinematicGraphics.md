# Bạch Đằng — nâng cấp hình ảnh

Bản này đưa hình ảnh sang hướng vật liệu tự nhiên, bề mặt có chi tiết và ánh sáng điện ảnh. Đây là một lượt nâng cấp trong Unity URP, chưa phải việc thay toàn bộ game bằng chất lượng sản xuất của Resident Evil Village.

## Nội dung đã dựng

- Chín bộ vật liệu nhân vật sử dụng albedo, normal và metallic/roughness của chính model gốc. Da, vải và giáp có phản ứng ánh sáng riêng; giới hạn độ bóng để tránh bề mặt như sáp. Giữ rig, chiều cao, xương, animation và collider hiện có.
- Hai mesh LOD cho từng mẫu nhân vật dùng cho đám đông. Nhân vật gần camera dùng mesh gốc; người chơi và Ngô Quyền luôn giữ mesh gốc. Nhân vật xa giảm chi tiết và bóng, không thay trạng thái AI hay va chạm.
- Sáu lớp nền terrain được thay bằng bề mặt quét 2K: cỏ/đá, đất rừng, bùn, cát sỏi và đá. Không sửa heightmap, cây đã trồng, đường đi hoặc phân bố lớp sơn.
- Đá phủ rêu, giỏ đan và gốc cây quét 3D thay/bổ sung các vật dụng tại doanh trại. Gốc cây mới đặt ở rìa, kiểm tra khoảng trống trước khi đặt; các vật thay thế giữ collider gốc.
- Gỗ sàn/thân thuyền và hàng rào, nền sân/đường làng có vật liệu PBR thay màu phẳng. Giữ hình học, kích thước và logic thuyền.
- Màu nước sông, gợn normal, màu phim ACES, cân bằng màu và bloom nhẹ. Không bật motion blur hay depth of field khi chơi; không sửa biên độ sóng hay mốc thủy triều.
- Vẫn giữ 26 cột lửa mới và giới hạn tám đèn lửa gần camera.

## Chất lượng

Trong **Cài đặt → Đồ họa**, bấm để chuyển **Tự động / Cân bằng / Cao**. Áp dụng ngay và nhớ lựa chọn.

Tự động chọn Cao khi GPU báo ít nhất 6 GB VRAM và máy có ít nhất 16 GB RAM; điều kiện này là lựa chọn ngân sách, không phải cam kết FPS. Cân bằng giữ renderer và khoảng chi tiết nhẹ trước đây. Cao dùng renderer riêng với bốn cascade bóng, shadow map 4096, SMAA, SSAO, cỏ gần camera và cây chi tiết xa hơn. Texture streaming có ngân sách 2 GB ở Cao, 384 MB ở Cân bằng. LOD nhân vật hoạt động ở cả hai chế độ.

Không thay định dạng campaign save. Save cũ tải vào scene và asset mới nên nhận được đồ họa mới. Phần test sử dụng thư mục lưu riêng, không ghi vào save của người chơi.

## Nguồn và tái tạo

Powered by [Poly Haven](https://polyhaven.com). Các nguồn tải mới là [CC0](https://polyhaven.com/license):

- [Weathered brown planks](https://polyhaven.com/a/weathered_brown_planks)
- [Brown mud 02](https://polyhaven.com/a/brown_mud_02)
- [Forest floor](https://polyhaven.com/a/forest_floor)
- [Rock boulder dry](https://polyhaven.com/a/rock_boulder_dry)
- [Sandy gravel 02](https://polyhaven.com/a/sandy_gravel_02)
- [Aerial grass rock](https://polyhaven.com/a/aerial_grass_rock)
- [Tree stump 01](https://polyhaven.com/a/tree_stump_01)
- [Rock moss set 01](https://polyhaven.com/a/rock_moss_set_01)
- [Wicker basket 01](https://polyhaven.com/a/wicker_basket_01)

`Assets/BachDangCinematic/sources.json` ghi URL gốc, MD5 và kích thước của 39 file nguồn. Tài sản nhân vật và vật liệu cũ của dự án giữ nguồn/quyền sử dụng trước đó; không được coi là tài sản CC0 của Poly Haven. Game không gọi API hoặc tải asset khi chơi.

`Tools/prepare_cinematic_assets.py` tải/kiểm MD5 và đóng gói mask offline (Python, Pillow, NumPy). Mask URP dùng R=metallic, G=AO, A=smoothness. `BeachBoatCinematicArt.Apply()` là công cụ dựng dành cho scene gốc trước nâng cấp, không chạy lặp trên scene đã dựng. Các asset kết quả đã được lưu trong project.

LOD được tạo offline bằng [UnityMeshSimplifier](https://github.com/Whinarn/UnityMeshSimplifier), commit `53fdb3122645bcd3ad2c258235200dff3dfbaa9b`, giấy phép MIT của Mattias Edlund/Sven Forstmann. Mã nguồn và giấy phép nằm trong `Assets/Editor/ThirdParty/UnityMeshSimplifier`; công cụ chỉ chạy trong Editor, không được đưa vào mã runtime của game.

## Kiểm tra

`BeachBoatCinematicPlaytest.RunInEditor()` kiểm tra vật liệu, skin bindings/LOD, renderer hỗ trợ, chuyển Cao/Cân bằng, quy mô vật quét và load campaign. Ảnh thật trong Play Mode nằm ở `Logs/Cinematic_Play_*.png`, báo cáo ở `Logs/CinematicPlaytest.json`. Thời gian khung hình trong báo cáo là số đo Unity Editor chạy nền trên máy kiểm thử, không tương đương FPS của bản build.

Chạy lại `BeachBoatCasualWorkPlaytest` và `BeachBoatConcurrentBattlePlaytest` để kiểm tra chuẩn bị vật tư, đóng cọc, load game và toàn bộ luồng nhử địch/thủy triều/mắc cọc sau thay đổi renderer, vật liệu và LOD.

Kết quả ngày 27/10/2026: **150 kiểm tra qua** — đồ họa 44, vật tư/đóng cọc 46, thủy chiến 60. Đã xem ảnh nhân vật ở khoảng gần/trung bình/xa, doanh trại ban mai/ban ngày và gốc cây mới. Các file campaign save thật được kiểm SHA-256 và giữ nguyên. Bộ asset đã dựng gồm 1.135 renderer nhân vật trong scene, 28 giỏ, 16 đá thay thế và 11 gốc cây mới. Mesh đám đông giảm còn khoảng 35% số tam giác ở mức giữa; mức xa giữ thêm các mép mở cần thiết để bảo toàn nón và vạt áo.
