# Làng và doanh trại — bàn giao 02/10/2026

Scene: `Assets/Scenes/beachBoat.unity`, Unity 6000.6.0f1.

Bản cập nhật tỷ lệ nhân vật và sinh hoạt ngày 05/10/2026 nằm trong [BachDangScaleAndLife.md](BachDangScaleAndLife.md). Các tọa độ dưới đây ghi theo hệ đơn vị trước khi chuyển tỷ lệ ×7.

## Nội dung đã lưu

Nhóm `BachDang_Living_Settlements` chứa ba khu dân cư và ba doanh trại bổ sung:

| Khu | Tâm X/Z | Công trình chính |
| --- | --- | --- |
| Làng chợ bên sông | -690 / 110 | 24 nhà dân, quầy chợ, kho, nhà sinh hoạt chung |
| Xóm chài bờ đông | 180 / 75 | 20 nhà dân, quầy cá, giàn phơi, kho và lối xuống bến |
| Xóm vườn bờ tây | -1090 / 255 | 16 nhà dân, sân chứa củi, kho và mái che |
| Trại tiếp vận | -935 / 220 | 6 lán quân, kho tiếp tế, sân tập, cổng và chòi canh |
| Trại dự bị | -1020 / -410 | 6 lán quân, kho tiếp tế, sân tập, cổng và chòi canh |
| Trại bờ đông | 180 / 320 | 6 lán quân, kho tiếp tế, sân tập, cổng và chòi canh |

Tổng cộng 60 nhà dân, 18 lán quân; cùng quầy, kho, nhà chung, cổng và chòi là 108 công trình chính. Bổ sung hàng rào, thùng, xô, củi, mái che sân nhà và 12 đuốc. Đường đất có mép mờ, bám mặt terrain và nhận ánh sáng/sương mù. Bến cá có cầu gỗ dài 58 m, trụ xuống nền sông, thùng cá và giàn phơi; mặt cầu cao 17,65 m, trên mức triều cao 15,5 m.

Tái sử dụng prefab và vật liệu hiện có. Đây là bố trí cảnh sinh hoạt theo phong cách của bộ tài nguyên, không phải phục dựng kiến trúc khảo cổ năm 938. Không thêm model người, Animator hoặc hành vi dân cư; giữ nguyên nhân vật hiện có để thành viên phụ trách tiếp tục làm.

## Cách xem

Mở scene và bấm Play. Phím **5** xem làng chợ, **6** xem xóm chài, **7** xem trại tiếp vận, **8** xem xóm vườn. Nút tương ứng nằm ở hàng thứ hai trên HUD. **H** ẩn bảng; **1–4** giữ các góc chiến trường trước đó. Các nút Sáng / Hoàng hôn / Đêm và tùy chọn tự chạy ngày đêm vẫn hoạt động.

Scene lưu ở 09:00, bật tự chạy chu kỳ 12 phút và camera hướng về làng chợ. Các object mới nằm trong một nhóm riêng, có thể chọn từng nhà hoặc từng khu để chỉnh.

## Dữ liệu và công cụ

- Terrain dùng bản riêng `Assets/BachDangSettlements/BachDang_SettlementTerrain.asset`; chiều cao và lớp phủ giữ nguyên so với `BachDang_KarstTerrain.asset`. Chỉ dọn cây/cỏ dưới công trình và đường đi; 50.542 cây terrain còn lại.
- `BachDangSettlementBuilder` tạo bố trí chính; `BachDangSettlementAssets` căn prefab theo mesh bounds và giữ vật liệu. Builder ghi các thiết lập Light thành prefab override để lưu bền vững.
- Menu **Bach Dang → Add Villages And Supply Camps** và **Add Civilian Fishing Landing** chỉ thêm khi nhóm tương ứng chưa tồn tại; không dựng trùng hoặc cập nhật đè các nhóm đã chỉnh tay.
- Shader đường: `Assets/Shaders/BachDangVillagePath.shader`. Vật liệu và mesh đường lưu trong `Assets/BachDangSettlements`.
- Bản sao scene trước khi thêm làng và trước khi thêm bến ở `.utmp/Settlements`; thư mục này bị Git bỏ qua. Không dùng các script rebuild tạm trong thư mục đó để chỉnh map đang làm.

## Đã kiểm tra

- Mở lại Unity: đủ 8 nhóm con, 60 nhà, 18 lán, 12 đèn đuốc bật đúng thông số; bến cá đã lưu.
- So sánh toàn bộ heightmap và alphamap với terrain núi gốc: không có thay đổi. TerrainCollider trỏ đúng dữ liệu mới.
- Không có SkinnedMeshRenderer mới; toàn scene vẫn có 20 như trước. Builder đã kiểm tra ma trận vị trí/xoay/tỉ lệ của các renderer nhân vật khi thêm công trình.
- Không có Missing Script trong nhóm mới; shader đường không có lỗi biên dịch.
- Play Mode: HUD có 8 preset, chuyển đúng bốn góc mới; đã chụp và xem HUD, làng ban ngày/đêm, doanh trại, xóm chài và bến cá.
- Không thấy lỗi runtime mới trong lần kiểm tra. Lỗi parser `PublisherReadme.asset` của gói TheTalesFactory vẫn là lỗi tài liệu có sẵn, ghi trong `BachDangMapReview.md`.

Ảnh kiểm tra ở `.utmp/Settlements`: `village-final.png`, `village-hud-play.png`, `village-night.png`, `camp-final.png`, `fishing-village.png`, `dock-final.png`. Mới kiểm tra trong Editor; chưa xuất build hoặc đo hiệu năng trên máy đích.
