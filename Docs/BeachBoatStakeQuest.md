# Chèo thuyền đóng cọc dưới sông — 16/10/2026

## Cách chơi

1. Nhận lệnh Ngô Quyền, qua cổng sang làng, bấm **E** nhặt đủ 5 cọc. Có 7 vị trí dùng đúng model CayGo của Thái.
2. Về báo cáo, nhận nhiệm vụ 2, qua cổng ra bến. Đi trên cầu gỗ tới cuối bến, bấm **E** lên thuyền.
3. **W/S** chèo tới/lùi, **A/D** lái, chuột xoay camera. Theo chỉ hướng tới các vòng vàng trong trận địa cọc dưới sông.
4. Thả W/S để dừng thuyền trong vòng, bấm **E** đóng cọc. Nếu quá sát cọc, lùi thuyền theo thông báo để chừa chỗ cho thân thuyền. Mỗi lượt mất 1,8 giây, khóa di chuyển/tương tác; có thể làm các điểm theo thứ tự bất kỳ.
5. Đủ 5/5, chèo về bến, dừng rồi bấm **E** lên bờ. Đi về cổng, bấm **E** về doanh trại và báo cáo Ngô Quyền.

F9 chuyển sa bàn/chơi, Esc thả/khóa chuột. Khi mở sa bàn, hội thoại hoặc đang đóng cọc, thuyền dừng. Không thể xuống thuyền giữa sông, đóng cọc khi đi bộ hoặc bỏ qua các vị trí để hoàn tất.

## Nguồn và cấu trúc

- Merge origin/thai tại **e648ec3** (Fix quest interaction and rowboat setup), merge commit **9f6265c**. Metadata tự sinh ở khoi chưa có tham chiếu trong beachBoat được thay bằng GUID nguồn của Thái để giữ liên kết TestQuest/controller/material mới. TestQuest giữ nguyên nội dung của commit Thái; không chép bản đồ cũ sang beachBoat.
- Dùng trực tiếp Assets/Animation/NPC_Rowing_Fix.controller và CheoThuyen.fbx đã được Thái cấu hình Humanoid, loop. Nhân vật hiện tại đổi sang động tác chèo khi lên thuyền, khôi phục controller đi bộ khi lên bờ. Root motion tắt khi chèo; tốc độ animation tỷ lệ với tốc độ thuyền. Mái chèo nhẹ căn theo hai bàn tay.
- Thuyen_NhiemVu_DongCoc dùng pf_build_boat_01_low.prefab, **12.191 đỉnh**, material có sẵn. Thêm ghế, mái chèo và cầu gỗ đơn giản. Không thêm nhân vật khác hoặc nạp thuyền AI 770.195 đỉnh vào beachBoat.
- QuestRowboat giữ sẵn tham chiếu, kiểm tra độ sâu/vật cản bằng truy vấn không cấp phát bộ nhớ mỗi tick. Khôi phục camera và vật lý đi bộ khi lên bờ. F9 không bật motor đi bộ khi còn trên thuyền.
- Dùng năm cọc có sẵn: CocGo_Hang1_4, CocGo_Hang2_4, CocGo_Hang3_4, CocGo_Hang2_5, CocGo_Hang1_5. Lúc Play chỉ năm cọc này ẩn để dành cho nhiệm vụ; 43 cọc khác giữ nguyên. Đóng xong, cọc về đúng vị trí gốc. Chỉ năm cọc chuyển động được bỏ static batching.
- Đã bỏ năm cọc nhiệm vụ trên bờ. RiverStakeWork.placements trỏ tới cọc dưới sông; vòng đánh dấu là vị trí dừng thuyền bên cạnh và đi theo mực nước. Không sửa đáy sông, terrain hoặc cây.
- TideSystem đọc hệ số đơn vị trong Start, khi scene đã tải xong, tránh hạ nhầm triều thấp từ 70 xuống 10 trong Awake. BeachBoat giữ triều thấp 70, cao 108,5.

## RAM và kiểm tra

- Không tạo texture mới, không nhân bản mesh theo mỗi cọc, không thêm đèn. Terrain vẫn **49.547 cây**. Cọc nhặt giữ LOD và không dùng MeshCollider. Không nạp TestQuest lúc chơi beachBoat.
- Cọc nhặt của Thái vẫn là asset chi tiết cao: 288.364 đỉnh, khoảng 38,75 MiB mesh và 3,33 MiB texture dùng chung. Culling giảm chi phí vẽ, không giải phóng asset đang tham chiếu.
- **103 kiểm tra luồng nhiệm vụ đạt**: mở đầu, nhận lệnh, qua cổng, nhặt cọc, ra bến, chèo thuyền, điều kiện chặn tương tác, đóng năm cọc, về bến và báo cáo. Bao gồm F9, khôi phục controller/vật lý và khoảng trống cho cọc mới.
- **5/5 đường chèo khứ hồi đạt** bằng điều khiển Step thực tế và truy vấn va chạm ở triều thấp, khi cả 48 cọc đã hiện. Đường thử vòng qua vật cản; đi thẳng xuyên hàng cọc bị chặn đúng thiết kế. Đây là kiểm tra tự động, không phải thao tác bàn phím thủ công.
- **3/3 chặng đi bộ đạt** bằng motor/Physics.Simulate: điểm đến → cuối bến → bờ → phạm vi E của cổng về.
- Animation Humanoid đã kiểm tra đổi tư thế ở nhiều pha và tỷ lệ tốc độ. Bến đủ sâu ở cả triều thấp/cao. Không Missing Script hoặc lỗi biên dịch. Lượt Play cuối không có lỗi game; còn hai cảnh báo BoxCollider scale âm trên model cảnh cũ.
- Editor sau thử trên máy hiện tại: khoảng **4,80 GiB working set, 5,25 GiB private memory**. Đây không phải mức đỉnh; chưa đo trên laptop 16 GB / RTX 3050.
- Backup và báo cáo trong .utmp/RowingQuest (Git bỏ qua): quest-result.json, row-paths-result.json, routes-result.json, pier-walk-result.json, boat-preview.png. Ảnh render dùng camera bố trí để kiểm tra.

Menu **Bach Dang/Install River Rowing Quest** chuyển scene đã có nhiệm vụ cọc cũ sang luồng chèo thuyền. Scene hiện tại đã cài; công cụ từ chối cài lặp. Chỉnh tham chiếu/điểm đặt trong Edit Mode, không chạy lại builder bản đồ.
