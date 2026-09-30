# Bàn giao lượt chỉnh map — 30/09/2026

Scene: `Assets/Scenes/beachBoat.unity`, Unity 6000.6.0f1.

## Đã thực hiện

- Kết nối Editor bằng Unity CLI trên máy; tạo cấu hình Unity MCP riêng cho project tại `.codex/config.toml`.
- Cân lại ánh sáng, sương mù và màu cảnh; tạo skybox, vật liệu nước và Volume Profile riêng trong `Assets/BachDangAtmosphere`.
- Nước nhận sương mù URP, phản chiếu môi trường khi không có camera phản chiếu phẳng. Bỏ nhánh shader trả trắng sai ở mép nước; giảm bọt giữa sông và giữ hiệu ứng bọt ven bờ.
- Đồng bộ chiều cao shader với mặt nước bằng `TidalWaterSurface`, không thay mesh nước hoặc sửa vật liệu gốc của gói OptiWater.
- Bổ sung 280 cụm cây thấp, dương xỉ, cỏ và đá ven bờ từ tài nguyên hiện có. Giữ nguyên terrain 6 km và các dãy núi.
- Dựng bến gỗ có trụ chống tại mép nước. Ẩn sáu bản đường ván cũ bị lệch mesh khỏi pivot; vẫn giữ các object này để có thể khôi phục.
- Đưa thuyền tuần tiễu bị chôn trong đất ra vùng nước. Căn đáy thân năm thuyền đồng minh chìm khoảng 0,4 m, căn thủy binh theo boong; bỏ cờ Static và thêm chuyển động theo triều với độ nhấp nhô nhỏ.
- Gán animation Idle Humanoid có sẵn cho 19 NPC, không bật root motion và không thay controller đã được gán trước đó.
- Khôi phục tham chiếu script bị thiếu trên 10 đuốc bằng `TorchLightFlicker`; giữ trạng thái bật/tắt của prefab. Không còn Missing Script trong scene.
- Nút HUD và phím T dùng chung lệnh thủy triều; bỏ bảng HUD chồng nhau. Camera đồng bộ góc chuột sau khi đổi preset; góc doanh trại được đưa vào trong tường để thấy nhân vật.

## Đã kiểm tra trong Editor

- Biên dịch C# thành công; shader nước không có lỗi biên dịch.
- Play Mode ở triều thấp 10 m và triều cao 15,5 m: cả năm thuyền theo mực nước, sai lệch nằm trong biên độ nhấp nhô 4 cm; đáy thân thuyền nằm dưới mặt nước; giá trị shader khớp chiều cao nước.
- 19 Animator chạy state Idle, root motion tắt.
- Góc yaw/pitch của điều khiển camera khớp với camera sau khi đổi góc.
- Chụp và xem cảnh tổng quan, doanh trại, bến thuyền, triều cao/thấp. Ảnh nằm tại `.utmp/MapPolish/`.
- Scene đã lưu ở Edit Mode. Đây là kiểm tra trong Editor, chưa phải kiểm thử bản build hoặc đo hiệu năng trên máy đích.

## Còn cần làm

1. **Kịch bản trận đánh:** hoàn thiện chuỗi nhử giặc → rút lui → triều rút → phục kích, AI và điều kiện thắng/thua. Hiện hệ thống sa bàn, thủy triều và va chạm thuyền chưa tạo thành vòng chơi đầy đủ.
2. **NPC và vũ khí:** có Idle cơ bản nhưng chưa có hành vi gác, tuần tra, đẽo cọc, rèn hoặc chiến đấu. Kiếm/khiên/giáo còn gắn theo root ở một số NPC, cần căn vào xương bàn tay theo từng model.
3. **Bãi cọc:** ở triều cao vẫn còn đầu một số cọc lộ trên mặt nước. Cần chốt tương quan chiều cao cọc và mức triều để đúng thiết kế “ngập hết đầu cọc”; tăng mức nước tùy tiện sẽ làm ngập đất bờ thấp.
4. **Âm thanh và cảnh quan:** chưa thấy hệ thống âm thanh sông nước/trận đánh trong các script đã rà. Cây thấp hiện dùng cỏ/bụi/dương xỉ sẵn có; chưa phải bộ lau sậy/ngập mặn chuyên biệt. Khu đất trống và sự đa dạng cây/núi còn có thể làm tiếp.
5. **Build:** danh sách scene build hiện chỉ có `SampleScene`. Chọn scene mở đầu và thêm `beachBoat` theo luồng game của nhóm trước khi xuất build; lượt này không đổi scene khởi động.
6. **Lỗi tài nguyên có sẵn:** gói núi TheTalesFactory có `PublisherReadme.asset` bị lỗi parser và `Icons.meta` không hợp lệ. Lỗi phát sinh từ cửa sổ giới thiệu của nhà phát hành khi reload; cần sửa riêng gói tài liệu này. Không nhầm lỗi đó với Missing Script trong scene hoặc lỗi shader nước.

## Cách xem và chỉnh tiếp

Mở `beachBoat`, bấm Play. Phím **1–4** đổi góc, **T** đổi triều, **H** ẩn/hiện bảng điều khiển, **C/Tab** đổi camera, giữ chuột phải để xoay.

Menu **Bach Dang → Polish Current Battlefield** áp dụng lại bộ thông số và căn thuyền cho scene này. Lệnh tạo bản sao scene trước khi sửa tại `.utmp/MapPolish/`; không tạo thêm cây hoặc bến nếu nhóm đã tồn tại. Lệnh có thể đặt lại các thông số ánh sáng/nước/camera và căn thuyền theo preset, nên không chạy lại nếu muốn giữ những tinh chỉnh thủ công mới hơn ở các phần đó.

Các bản sao scene và ảnh trong `.utmp` bị Git bỏ qua. Các file thay đổi trong project chưa được commit hoặc push.
