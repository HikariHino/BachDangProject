# Dân cư và sân sinh hoạt — beachBoat

Scene `Assets/Scenes/beachBoat.unity` có 492 NPC sinh hoạt: 214 trong 11 khu nhiệm vụ dưới đây, 276 tại 46 cụm xóm/trại mới trải khắp terrain, và 2 lính gác bến. Nhân vật nhiệm vụ, người chơi và thủy thủ chiến đấu được giữ riêng. Chi tiết phần mở rộng nằm trong `Docs/BeachBoatWholeMap.md`.

| Khu | NPC sinh hoạt |
|---|---:|
| Chợ làng ven sông | 40 |
| Xóm chài | 30 |
| Xóm ruộng vườn | 30 |
| Doanh trại chính | 26 |
| Bến tiếp vận | 18 |
| Bốn ngõ quanh sạp nhiệm vụ | 10 mỗi ngõ |
| Trại bờ đông | 16 |
| Trại quân bờ sông | 14 |

Tổng cộng 107 sân làm việc có mái che, thùng hàng, gỗ xếp và đồ dùng. Vị trí bổ sung kiểm tra nền đất trên triều cao, công trình, cây terrain, điểm tương tác và tuyến đi của NPC. Đồ dùng chắn đường được dời hoặc bỏ. Các nhóm mới nằm trong `BeachBoat_Busy_Settlements`, được lưu trực tiếp vào scene, không tạo ngẫu nhiên lại lúc chơi.

Người dân mang hàng, chuyển giỏ, đi qua các điểm làm việc rồi dừng nghỉ với nhịp khác nhau. Bộ quản lý chọn tối đa 44 NPC sinh hoạt gần camera trong phạm vi 700 đơn vị; người ngoài ngân sách được ẩn để không đứng cứng giữa tư thế đi bộ. Khu ở xa được `MapZoneStreamer` tắt theo khoảng cách. NPC gần nhau đổi hướng nhẹ để tránh xuyên người; xử lý tránh vật cản không tạo mảng tạm mỗi khung hình. Nhân vật dùng lại mesh, avatar, controller và vật liệu đã có.

Có 240 con gà ở sân nhà và xóm dân cư, dùng chung ba mesh cùng bốn vật liệu, không có Animator, Rigidbody hay NavMeshAgent. Gà bước chân, đi quanh sân, dừng mổ đất và chỉ cập nhật chuyển động khi camera ở gần. Điểm xuất phát chừa khoảng trống cho cả phạm vi đi lại; các con trong đàn tránh nhau. Tổng asset riêng cho hình/vật liệu gà dưới 1 MB; không thêm texture.

`BeachBoatBusyWorld.DensifyAndExit` áp dụng bố trí một lần và từ chối dựng trùng. Không chạy lại builder làng gốc. `BeachBoatBusyWorldPlaytest.RunAndExit` chạy qua cả 11 khu để kiểm tra chuyển động, giới hạn cập nhật, trò chuyện, va chạm, đồ dùng và vật nuôi. Các kiểm tra quest 2 và quest 3 được chạy riêng để xác nhận các điểm tương tác và đường thuyền vẫn hoạt động.

Ảnh render và báo cáo nằm trong `Logs/Dense_BusyWorld_*.png`, `Logs/DenseWorldBuild.json` và `Logs/BusyWorldPlaytest.json`. Đây là kiểm tra Unity Editor trên máy hiện tại; không thay cho benchmark bản build trên máy đích.
