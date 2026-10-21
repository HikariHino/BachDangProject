# Chèo thuyền đóng cọc dưới sông — 17/10/2026

## Cách chơi

1. Nhận lệnh Ngô Quyền, qua cổng sang làng, bấm **E** nhặt đủ 5 cọc. Có 7 vị trí dùng đúng model CayGo của Thái.
2. Về báo cáo, nhận nhiệm vụ 2, qua cổng ra bến. Đi trên cầu gỗ tới cuối bến, bấm **E** lên thuyền.
3. **W/S** chèo tới/lùi, **A/D** lái, chuột xoay camera. Theo chỉ hướng tới 5 vòng vàng dọc bãi sông; có thể làm theo thứ tự bất kỳ.
4. Thả W/S để dừng thuyền trong vòng, bấm **E để dựng riêng cụm đó** trong 3 giây. Bãi mở rộng có năm cụm **31 / 30 / 30 / 30 / 30 cọc**, tổng cộng 151 cọc, phủ hai luồng thuyền địch. Cụm đã làm mất dấu; HUD chỉ đến cụm chưa làm và hiển thị số cụm/cọc hoàn tất. Thuyền và tương tác tạm khóa khi đóng. Nếu quá sát cọc, lùi lại theo thông báo.
5. Làm đủ **5/5 cụm, 151/151 cọc**, chèo về bến, dừng rồi bấm **E** lên bờ. Đi về cổng, bấm **E** về doanh trại và báo cáo Ngô Quyền.

F9 chuyển sa bàn/chơi, Esc thả/khóa chuột. Khi mở sa bàn, hội thoại hoặc đang đóng cọc, thuyền dừng. Không thể xuống thuyền giữa sông, đóng cọc khi đi bộ hoặc bỏ qua các vị trí để hoàn tất.

## Nguồn và cấu trúc

- Merge origin/thai tại **e648ec3** (Fix quest interaction and rowboat setup), merge commit **9f6265c**. Metadata tự sinh ở khoi chưa có tham chiếu trong beachBoat được thay bằng GUID nguồn của Thái để giữ liên kết TestQuest/controller/material mới. TestQuest giữ nguyên nội dung của commit Thái; không chép bản đồ cũ sang beachBoat.
- Dùng trực tiếp Assets/Animation/NPC_Rowing_Fix.controller và CheoThuyen.fbx đã được Thái cấu hình Humanoid, loop. Nhân vật hiện tại đổi sang động tác chèo khi lên thuyền, khôi phục controller đi bộ khi lên bờ. Root motion tắt khi chèo; tốc độ animation tỷ lệ với tốc độ thuyền. Mái chèo chạy theo chu kỳ bắt nước, kéo và đưa về trước; tay dùng IK hai khớp để bám hai điểm trên cán, không lấy hướng cán từ trục tay trong clip.
- Thuyen_NhiemVu_DongCoc dùng mesh thuyền gỗ riêng: đáy kín, mạn thấp, ván sàn, nẹp và ghế ngang. Thân thuyền **3.576 đỉnh**, mái chèo **80 đỉnh** (thân cũ 12.191 đỉnh). Hai mesh thuyền/mái chèo được tạo trước trong Editor, dùng chung texture wood_tiled_02_a đã có. Không thêm nhân vật khác hoặc nạp thuyền AI 770.195 đỉnh vào beachBoat.
- QuestRowboat giữ sẵn tham chiếu, kiểm tra độ sâu/vật cản bằng truy vấn không cấp phát bộ nhớ mỗi tick. Thuyền và gốc nhân vật cập nhật một lần theo khung hình, tắt Rigidbody interpolation của nhân vật khi ngồi để tránh tranh chấp giữa nhịp vật lý và hình ảnh. Animator chạy Normal. Khôi phục controller, interpolation, culling, camera và vật lý đi bộ khi lên bờ. F9 không bật motor đi bộ khi còn trên thuyền.
- Toàn bộ 151 CocGo_Hang* được lưu inactive trong scene, chỉ dựng khi làm quest. fieldStakes giữ tham chiếu, tái sử dụng mesh/material cũ, không bật static batching để có thể hạ xuống khi đóng. Hủy thao tác giữa chừng chỉ thu hồi cụm đang làm; giữ nguyên các cụm đã hoàn tất và cho phép thử lại.
- RiverStakeWork.placements giữ năm điểm riêng cho năm cụm liên tiếp theo chiều sông; stakeIndices phủ đủ 151 cọc, mỗi cọc thuộc đúng một cụm. Vòng đánh dấu đi theo mặt nước, nằm trong luồng đủ sâu, cách các cọc ít nhất 50 đơn vị để thuyền tiếp cận. Bổ sung cọc chừa khoảng trống 65 đơn vị quanh điểm thao tác; cọc bị trôi cũng chọn chỗ vớt đủ sâu và không có vật cản. Cọc được thu ngắn và căn chân chôn 8 đơn vị dưới đáy. Đỉnh cọc ở Y 88,48–94,26, lộ 18,48–24,26 khi triều thấp 70; chìm ít nhất 14,24 dưới triều cao 108,5. Không sửa đáy sông, terrain hoặc cây.
- TideSystem đọc hệ số đơn vị trong Start, khi scene đã tải xong, tránh hạ nhầm triều thấp từ 70 xuống 10 trong Awake. BeachBoat giữ triều thấp 70, cao 108,5. Khi nhận lệnh đóng cọc (state 5), QuestRowboat giữ TideSystem ở triều thấp: nước rút êm và không tự dâng lại. Phím T/HUD không đảo triều được trong lúc làm và về báo cáo (state 5–6). Báo cáo xong (state 7) mới trả quyền điều khiển triều; các scene không có quest boat vẫn dùng chu kỳ cũ.

## RAM và kiểm tra

- Không tạo texture mới, không nhân bản mesh theo mỗi cọc, không thêm đèn. Terrain vẫn **49.547 cây**. Cọc nhặt giữ LOD và không dùng MeshCollider. Không nạp TestQuest lúc chơi beachBoat.
- Cọc nhặt của Thái vẫn là asset chi tiết cao: 288.364 đỉnh, khoảng 38,75 MiB mesh và 3,33 MiB texture dùng chung. Culling giảm chi phí vẽ, không giải phóng asset đang tham chiếu.
- **510 xác nhận tự động đạt** cho luồng năm cụm sau khi sửa thuyền và khóa triều, được ghi ở .utmp/RowingPolish/quest-result.json: bãi trống trước quest, vật liệu, dựng theo thứ tự bất kỳ, chống tương tác lặp, hủy một cụm vẫn giữ cụm đã xong, chỉ đủ 5/5 mới chuyển sang báo cáo, cọc lộ trong quest; lệnh đảo triều bị chặn ở state 5–6 và hoạt động lại sau báo cáo, khi đó cọc ngập đúng mức triều cao.
- **5/5 đường chèo khứ hồi đạt** bằng điều khiển Step thực tế và truy vấn va chạm ở triều thấp, khi cả 48 cọc đã hiện. Đường thử vòng qua vật cản; đi thẳng xuyên hàng cọc bị chặn đúng thiết kế. Đây là kiểm tra tự động, không phải thao tác bàn phím thủ công.
- **3/3 chặng đi bộ đạt** bằng motor/Physics.Simulate: điểm đến → cuối bến → bờ → phạm vi E của cổng về.
- Animation Humanoid đã kiểm tra đổi tư thế ở nhiều pha và tỷ lệ tốc độ. Bến đủ sâu ở cả triều thấp/cao. Không Missing Script hoặc lỗi biên dịch. Lượt Play cuối không có lỗi game; còn hai cảnh báo BoxCollider scale âm trên model cảnh cũ.
- Chưa đo mức RAM đỉnh hoặc chơi trực tiếp trên laptop 16 GB / RTX 3050. Mesh thuyền mới nhẹ hơn; texture gỗ dùng lại asset có sẵn (~3,33 MiB trong bộ nhớ), không tạo texture mới.
- Báo cáo năm cụm trong .utmp/ClusterStakeField: quest-result.json, routes-result.json, row-paths-result.json. Ảnh kiểm tra thủy triều và backup nằm trong .utmp/FullStakeField. Các thư mục này được Git bỏ qua. Ảnh render dùng camera bố trí để kiểm tra.

Menu **Bach Dang/Install River Rowing Quest** chuyển scene đã có nhiệm vụ cọc cũ sang luồng chèo thuyền. Scene hiện tại đã cài; công cụ từ chối cài lặp. Chỉnh tham chiếu/điểm đặt trong Edit Mode, không chạy lại builder bản đồ.

Công cụ **Bach Dang/Configure Complete Quest Stake Field** căn toàn bộ cọc theo hai mức triều, gắn fieldStakes, chia năm cụm cân bằng, đặt điểm tương tác và lưu bãi trống trước quest. `BeachBoatFullStakeFieldSetup.ExtendAndExit` bổ sung cọc dọc tuyến cuối của cả mười tàu; báo cáo `Logs/ExpandedStakeCoverage.json` kiểm tra cả mười tuyến và độ ngập khi triều cao. Scene hiện tại đã áp dụng; không cần chạy lại khi chơi. Các báo cáo 48 cọc ở trên thuộc phiên bản trước khi mở rộng. Kiểm tra hiện tại nằm trong `Logs/BattleLaunchRegression.json` và `Logs/BeachBoatQuest3Playtest.json`.

Công cụ **Bach Dang/Polish Quest Rowboat** thay riêng phần hình của thuyền nhiệm vụ, tạo mesh/material dùng chung và căn chỗ ngồi sát bên chèo. Không chạy lại installer nhiệm vụ hoặc builder bản đồ.

Kiểm tra chuyển động mới: .utmp/RowingPolish/motion-result.json (30/60/120 FPS, gốc nhân vật bám ghế, cả chu kỳ IK, nhịp lá chèo xuống nước/nhấc về). Ảnh rowing-*.png là render trực tiếp trong scene; không phải ảnh thiết kế.
