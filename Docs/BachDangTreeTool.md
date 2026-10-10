# Thêm và xóa cây trong Scene

Mở menu **Bach Dang → Them - Xoa Cay**. Menu Xóa Cây Nhanh cũ cũng mở công cụ này.

1. Chọn Terrain của map, chọn **Thêm cây** hoặc **Xóa cây**.
2. Chỉnh bán kính cọ. Khi thêm, chọn Bách/Cypress hoặc Thông/Conifer, số cây mỗi lần bấm và kích thước.
3. Bấm **BẬT CỌ TRONG SCENE**, rồi bấm chuột trái từng lần trên mặt đất trong Scene. Một cây được đặt đúng con trỏ; nhiều cây được rải trong vòng cọ.
4. **Ctrl+Z** hoàn tác, **Ctrl+Y** làm lại, **Esc** tắt cọ. Alt và chuột phải vẫn dùng để điều khiển góc nhìn.
5. Bấm **Lưu cây và scene** để lưu cả TerrainData và scene. TerrainData là tài nguyên dùng chung: nếu scene khác tham chiếu cùng tài nguyên thì cũng thấy thay đổi.

## Phạm vi và tối ưu

- Cọ xóa nhận các mẫu cây thân gỗ theo mesh, tránh nhận nhầm prefab `Tree_<guid>` chứa đá, bụi hoặc cỏ. Hai mẫu cây hiện tại đã được xác minh; nếu thêm bộ cây có quy tắc tên khác, cần bổ sung bộ nhận dạng.
- Tùy chọn **Xóa cả cây prefab đặt riêng** xử lý cả các cây đặt quanh trại cũ; giữ nhóm cha và các vật thể khác.
- Bán kính hiển thị theo mét quy đổi của map scale 7. Kích thước cây mới dựa trên tối đa 64 cây cùng mẫu đang có; không nhân thêm scale 7 lần thứ hai.
- Không thêm model hoặc texture mới, không bật lại Nature Renderer tree streaming.
- Mỗi lần bấm tối đa 10 cây; khoảng cách tối thiểu 2–20 m; bỏ qua dốc trên 35°, ngoài terrain, và vùng dưới 16,8 m trên beachBoat.
- Giới hạn 60.000 bản ghi Terrain (gồm cây, đá, bụi trong cùng hệ thống). Đây là giới hạn của công cụ, không phải cam kết mọi laptop đều đủ RAM.
- Chỉ đọc/ghi danh sách cây khi bấm, không xử lý mỗi frame hoặc trong lúc rê cọ. Mỗi lần sửa là một nhóm Undo; Unity vẫn cần bộ nhớ cho bản Undo TerrainData. Công cụ chỉ chạy trong Editor.

## Kiểm tra ngày 09/10/2026

Đã kiểm tra trên địa hình tạm: thêm cây, tỉ lệ 7, bám đất, khoảng cách chống chồng, xóa giữ nguyên bản ghi đá, Undo/Redo Terrain, giới hạn mỗi lượt và tổng số cây, xóa prefab giữ nhóm cha/vật thể bên cạnh, Undo prefab. Toàn bộ cây của map thật giữ nguyên 50.364 bản ghi sau thử nghiệm. Biên dịch thành công; chưa đo RAM trên laptop 16 GB.

Đã xóa cụm `2_XuongRen_Va_BaiDeoCoc` trong beachBoat theo yêu cầu: nhà rèn, cần cẩu và vật dụng của xưởng. Bến thuyền và các khu khác giữ nguyên. Bản sao trước khi xóa và kết quả thử nằm trong `.utmp/IslandCamps`.
