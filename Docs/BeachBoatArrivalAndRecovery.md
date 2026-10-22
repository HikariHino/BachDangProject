# Điểm đến, trò chuyện và hồi phục trong beachBoat

`RiverArrival` từng nằm đúng trên hàng rào sau khi cụm cổng bến được xoay 90 độ. Điểm này được dời theo hướng thực tế của cổng, sang hành lang đi bộ. `BeachBoatSpawnAudit.FixAndExit` sửa scene và kiểm tra capsule cùng lối bước ra trước khi lưu.

`PlayerArrival` dùng capsule thật của người chơi để kiểm tra chồng lấn và một hướng đi ra. Khi điểm đến bị chặn, nó tìm nền khô gần đó thay vì giữ nhân vật bên trong trụ/công trình. Logic này áp dụng cho spawn đầu game, qua cổng và chơi lại checkpoint. Không dịch chuyển nhân vật đang đi bình thường mỗi frame.

Trò chuyện dân thường dùng `NearbyConversation`: chỉ chọn một NPC gần nhất ở phía trước, trong tầm không vượt 42% chiều cao capsule nhân vật và có đường nhìn không bị tường che. Cả NPC gốc và NPC sinh hoạt dùng cùng bộ chọn; giá trị khoảng cách cũ quá lớn trong scene không còn làm hiện lời nhắc từ xa. Nhân vật nhiệm vụ vẫn có luồng tương tác riêng.

Giữ nguyên các lều và điểm dịch vụ. Nhấn E mở hướng dẫn minigame, chưa cộng chỉ số:

| Dịch vụ | Hoạt động | Giới hạn | Phần thưởng sau khi thắng và nhận |
|---|---|---|---|
| Quân y | Làm sạch → đặt băng → cố định, vị trí nút đổi sau mỗi bước | 22 giây, tối đa 2 lỗi | Đầy máu |
| Bếp trại | Nước → cơm → cá khô, chọn đúng món theo thứ tự | 22 giây, tối đa 2 lỗi | 25 máu và 35 thể lực |
| Lều nghỉ | Nhấn Space/thở ra khi con trỏ trong vùng xanh | 4 nhịp, 20 giây, tối đa 2 lỗi | 35 máu và đầy thể lực |
| Kho luyện tập | Bước A/D luân phiên theo vùng nhịp xanh | 6 nhịp, 24 giây, tối đa 2 lỗi | Thể lực tối đa ít nhất 120 và hồi đầy |

Thua khi hết giờ hoặc sai lần thứ ba. Có thể nhấn R thử lại; Esc hủy và trả điều khiển/cursor. Không cấp thưởng khi mở, hủy, thua hoặc chỉ đến màn thắng. Hồi thể lực thụ động tạm dừng trong minigame. Cooldown dùng chung theo loại dịch vụ chỉ bắt đầu khi nhận thưởng, không thể chuyển lều cùng loại để nhận liên tục. Rèn giáp giữ chức năng cũ.

`BeachBoatArrivalRecoveryPlaytest` tái hiện vị trí cũ trên cọc, kiểm tra tự tìm chỗ trống và điều khiển thật bước khỏi điểm mới; thử NPC gần/xa/sau lưng/sau tường, hủy/thua/hết giờ và đóng minigame khi chết. `BeachBoatCampFacilityPlaytest` kiểm tra các dịch vụ từ hướng dẫn đến thắng và nhận thưởng, kèm ảnh giao diện. Checkpoint và minigame nhiệm vụ được chạy riêng để kiểm tra hồi quy. Báo cáo/ảnh nằm trong `Logs` và không đưa vào Git.
