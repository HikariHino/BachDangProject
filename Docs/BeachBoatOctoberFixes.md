# BeachBoat — sửa các tool ngày 18/10/2026

## Các lỗi đã sửa

- `NPCWander`: đọc đúng tham số `isWalking`, kiểm tra tham số trước khi dùng; điều khiển `Pose` cho ngồi/nằm rồi trở lại đi bộ. Đi theo cao độ đất, tránh nước và vật cản, không sinh GameObject waypoint trong lúc chơi.
- `BeachBoatNPCSetup`: chỉ gắn vào humanoid trên bờ có tham số đi bộ; dùng lại controller có clip Walking. Không gắn vào nhóm Camp_Soldiers, người chơi, chủ tướng, lính nghỉ/chòi canh hoặc thủy binh.
- `BeachBoatGateFix`: giữ vị trí cổng và điểm dịch chuyển đã đặt; không dời cổng vào đoạn hàng rào gần nhất. Chỉ dọn prefab sạp/bàn xác định trong lối đi; có thể Undo, không xóa bằng chuỗi tên ngắn như `tu`, `ke`.
- `BeachBoatSceneryPolish`: material URP dùng chung và được lưu thành asset; hiệu ứng chạy trong assembly runtime, không còn MonoBehaviour nằm trong Editor. Không tạo thêm lớp mây che skybox ngày/đêm. Chim và gỗ trôi quanh vị trí đặt, không nhảy về gốc thế giới.
- Quest 3: dùng lại mesh thuyền nhẹ, điểm xuất phát trên sông được kiểm tra ở triều thấp; lên thuyền thật mới bắt đầu. Địch va vào cọc đã dựng, chìm, đếm chiến thắng một lần. Cổng ra/về và hướng dẫn hoạt động ở hồi 3. Mảng thuyền trống, phần tử null/trùng không làm vòng sinh thuyền treo.
- `EnvironmentOptimizer`: đo FPS qua nhiều khoảng thời gian sau khởi động, trả chất lượng khi tắt. Script này không phải cơ chế giải phóng RAM và không được tự thêm vào scene.

## Cách chạy tool

Mở `Assets/Scenes/beachBoat.unity` ở **Edit Mode**. Các menu vẫn nằm trong **BeachBoat**. Tool từ chối chạy trong Play Mode hoặc scene khác, tạo bản sao scene trước sửa trong `.utmp/October18Repair`, có Undo và lưu scene. Chạy lại không nhân đôi NPC, thuyền hay trang trí.

Đã áp dụng và lưu trong beachBoat: 9 NPC đi lại, kiểm tra 4 cổng, nhóm trang trí nhẹ, 5 thuyền địch ẩn trước trận chiến. TestQuest không bị sửa. Cọc vẫn ẩn khi mở đầu, chỉ dựng theo 5 cụm trong quest; triều vẫn giữ thấp khi đóng cọc.

## Kiểm tra

- Unity 6000.6.0f1 biên dịch không có lỗi; project còn cảnh báo API cũ không liên quan.
- Chạy các menu hai lần: số Transform giữ nguyên 13.946; vị trí cổng giữ nguyên; không có material Standard/thiếu shader/không được hỗ trợ trong scene.
- Chạy Play Mode chuỗi nhiệm vụ 1–2: **510 kiểm tra đạt**.
- Tiếp tục qua nhận lệnh hồi 3, cổng, lên thuyền, 5 lần va cọc thực tế, chìm, chiến thắng và quay về: **20 kiểm tra đạt**.
- Terrain giữ nguyên **49.547 cây**; không thêm texture hoặc model người. 5 thuyền dùng chung mesh/material đã có; ánh lửa không đổ bóng động.

Các kết quả này là trên máy hiện tại, không thay cho đo bộ nhớ đỉnh khi mở scene trên laptop 16 GB. Chưa push các thay đổi.

Kiểm tra bổ sung: 9 kiểm tra đạt cho clip đi bộ, chuyển ngồi/nằm rồi đi lại, mảng thuyền null/trùng và wave size bằng 0.
