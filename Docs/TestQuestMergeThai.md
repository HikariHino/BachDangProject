# TestQuest sau khi merge Thai vào Khoi

Merge commit: `3226bb7` (nhánh `origin/thai` tại `ebaa00c`).

## Cách chơi

- Mở `Assets/Scenes/TestQuest.unity`, bấm Play.
- Mở đầu: Space hoặc Enter để đọc câu tiếp theo; giữ Esc 0,8 giây để bỏ qua.
- WASD di chuyển theo hướng camera; Shift chạy nhanh khi tiến lên; Space nhảy.
- Chuột xoay camera; giữ chuột phải thu gần góc nhìn qua vai và xoay người theo camera.
- Giữ Alt để nhìn quanh; Esc thả/bắt chuột, bấm chuột trái trong vùng chơi để bắt lại.
- E tương tác nhiệm vụ. Tương tác bị khóa khi mở đầu đang hiển thị.

## Các sửa lỗi

- Giữ đúng Canvas của đoạn mở đầu, chuyển câu thủ công, khôi phục đúng trạng thái UI sau khi kết thúc. Không tự bật lại mọi script hoặc model phụ đã bị tắt.
- Giữ Animator và rig đã gán cho nhân vật; chuẩn hóa tốc độ đi chéo, thêm gia tốc/phanh, nhảy và camera tránh vật cản.
- Cổng cập nhật cả Transform và Rigidbody, xóa vận tốc cũ và đặt lại hướng camera khi dịch chuyển.
- Sửa 5 collider gỗ nhiệm vụ bị scale thành khối rắn rộng 200m: dùng trigger rộng 2m, giữ nguyên model và cự ly nhặt 8m, tránh đẩy nhân vật ra khỏi làng.
- TestQuest chỉ dùng một camera chính và một Terrain hoạt động. Terrain trùng vẫn được giữ ở trạng thái tắt. Tắt stream cây qua Nature Renderer; giới hạn khoảng cách cây hiển thị bằng Terrain gốc.
- Xóa 51 đối tượng rỗng mất prefab đá và tắt UI nhiệm vụ trùng trong Canvas mở đầu.
- Bootstrap cốt truyện chỉ chạy ở TestQuest, tránh áp tọa độ nhiệm vụ lên map beachBoat đã đổi tỷ lệ.
- Giữ nguyên file scene beachBoat, Terrain scale 7 của map đó và ProjectSettings.

## Kiểm tra và giới hạn

- Đã xem ảnh chụp Game có UI: đoạn mở đầu dễ đọc, không chồng bảng nhiệm vụ; góc nhìn sau vai có một bảng nhiệm vụ.
- Kiểm thử vật lý trong Play: đi thẳng/chéo tương đương tốc độ, chạy nhanh hơn, dừng không trượt, di chuyển theo hướng camera, nhảy cao khoảng 4,83 đơn vị với cấu hình 5, tiếp đất và camera tránh tường.
- Kiểm thử chuyển doanh trại → làng → doanh trại qua ScreenFader: đúng trạng thái nhiệm vụ, sai lệch vị trí XZ bằng 0 sau khi mô phỏng tiếp tục; 5 collider gỗ đều là trigger kích thước 2 × 2 × 2m.
- Unity biên dịch thành công; scene có một camera chính, một Terrain hoạt động, không có missing script. Console của lượt Play cuối có 0 lỗi; vẫn còn cảnh báo asset/shader cũ và API deprecated trong dự án.
- Các kiểm thử tự động gọi logic trong Play; vẫn cần người chơi thử cảm giác chuột/phím thực tế.
- TestQuest có 50.181 bản ghi cây/đá/bụi trên một Terrain đang hoạt động. Chưa đo RAM đỉnh hoặc thử trực tiếp trên laptop 16 GB/RTX 3050, nên chưa khẳng định hết nguy cơ thiếu RAM trên máy đó.
