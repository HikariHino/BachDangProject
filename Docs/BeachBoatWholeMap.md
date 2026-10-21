# Xóm dân, trạm gác và thác suối trên toàn map beachBoat

Terrain rộng 42.000 × 42.000 đơn vị. Các khu dân cư cũ tập trung quanh nhiệm vụ, để lại nhiều vùng đất trống nhìn thấy khi chèo thuyền hoặc quan sát từ xa. Scene hiện có thêm 46 cụm gồm 39 xóm dân và 7 trại tiền tiêu, rải từ khoảng -19.500 đến 19.500 theo cả hai trục.

Phần mới nằm dưới `BeachBoat_Whole_Map_Life`, có 164 ngôi nhà, 276 người dân/lính, 184 con gà, 164 đường đi giữa nhà và sân chung, 331 cụm đồ dùng/hàng rào, ruộng trồng và 415 cụm cây/đá từ prefab địa hình. Nhà được chọn theo nền đất khô, độ dốc và cây có sẵn, không thay đổi heightmap, mực thủy triều hoặc xóa rừng terrain. Các nhà trong trại mới dùng prefab doanh trại có cửa mở và nội thất, cao 47 đơn vị; nhà dân cao 42 đơn vị.

Các mái `Market_Canopy` hình tấm vách dựng sai đã được thay bằng sạp mở: bốn cột tiếp đất, mái hai dốc và bàn hàng. Cổng `RiverToCamp` được thu về tỉ lệ người, đặt trên đường từ cầu tàu vào trạm gác, nối hàng rào, chòi, đồ tiếp vận và hai lính gác. Điểm lên/xuống thuyền vẫn nằm ở cầu tàu; điểm đến khi chuyển từ doanh trại được đặt trên lối đi trong bờ.

Bốn thác suối đặt trên các sườn đồi có độ chênh cao. Mỗi cụm có đá dùng texture sẵn, dòng nước từ khe đá xuống hồ chân thác, suối tiếp nối theo địa hình, hơi nước và âm thanh 3D chỉ nghe ở gần. Shader `BachDang/Cascade` tạo chuyển động dòng chảy mà không cần thêm texture hoặc script Update cho mỗi thác.

Tất cả dân cư thêm vào dùng chung `AmbientPopulationBudget`, tối đa 44 NPC được cập nhật hoạt ảnh gần camera. NPC vùng xa dùng prefab chung và được ẩn. Các cụm cảnh quan được `MapZoneStreamer` quản lý; nhà ở tầm xa còn hiện để không làm bờ sông trống khi quan sát, chi tiết nhỏ dùng LOD riêng. Vật nuôi không dùng Animator và chỉ di chuyển khi ở gần người chơi. Các con số này là giới hạn hoạt động, không phải số FPS đã benchmark.

## Tránh kẹt thuyền

`AmbientRiverBoat.Step` ưu tiên nhường một hành lang quanh thuyền nhiệm vụ, kể cả lúc đang đợi ở cầu tàu hoặc giữ vị trí chiến đấu. Thuyền chọn hướng tách ngang/ra xa, kiểm tra nước đủ sâu, bờ và vật cản trước khi dịch chuyển; không đẩy thuyền người chơi.

`QuestRowboat` cho phép chèo thoát một va chạm đã chồng lấn nếu khoảng cách thân thuyền được cải thiện. Độ sâu chồng lấn được đo trên cả đoạn sống thuyền; dùng ba điểm rời rạc có thể chặn đuôi thuyền khi người chơi đang chèo ra xa. Khi tâm thuyền đối diện còn nằm trên đoạn sống, khoảng cách giữa hai tâm tăng và độ chồng lấn không tăng cũng được tính là thoát. Va chạm mới với thuyền, bờ, đá và công trình vẫn chặn và tính sát thương.

## Kiểm tra

- `BeachBoatWholeMapPlaytest.RunAndExit`: kiểm tra tất cả nhà mới về kích thước, nền đất và chồng lấn; ghé tám khu đại diện để kiểm tra NPC di chuyển và ngân sách hoạt ảnh; kiểm tra thác/âm thanh cục bộ; thử chèo thoát, tuần tra nhường đường và va chạm mới.
- `BeachBoatBattleLaunchPlaytest.RunAndExit`: các tư thế đậu thuyền lỗi, lối ra bến ở hai mực triều, đường tới các cụm cọc và thoát kẹt cầu tàu.
- `BeachBoatVillageWorkPlaytest.RunAndExit`: sạp nhiệm vụ, câu đố, thắng/thua và chơi lại.
- `BeachBoatQuest3Playtest.RunAndExit`: thuyền địch theo tuyến, triều rút, toàn bộ tàu chìm, chiến đấu và kết thúc nhiệm vụ.
- `BeachBoatCheckpointPlaytest.RunAndExit` và `BeachBoatAudioPlaytest.RunAndExit`: phục hồi checkpoint và âm thanh tương tác/gần-xa.

Báo cáo và ảnh chạy thử nằm trong `Logs/WholeMap*.json`, `Logs/WholeMap_*.png` và các báo cáo nhiệm vụ tương ứng; thư mục Logs không đưa vào Git. Builder `BeachBoatWholeMapExpansion` áp dụng trực tiếp vào scene bằng Unity Editor, từ chối dựng trùng, không tạo thêm khu ngẫu nhiên mỗi lần chơi.
