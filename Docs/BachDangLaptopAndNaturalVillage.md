# Làng tự nhiên và ngân sách laptop — 09/10/2026

Scene: `Assets/Scenes/beachBoat.unity`, bản đang có các sửa chữa RAM của người dùng ở commit `0246687`.

Đợt tiếp theo đã hoàn thành [sân nhà và chợ](BachDangCourtyardsAndMarket.md), sử dụng 5 mesh chung và không thêm texture/đèn.

## Thay đổi đã lưu

- Sắp xếp lại 60 hộ trong ba làng: lệch vị trí và hướng, giữ model nhà cùng đồ dùng của từng hộ; kiểm tra không tăng phần giao nhau giữa các công trình.
- 81 đoạn đường/ngõ và lối vào nhà; đường tránh công trình, có thể thu hẹp thành ngõ nhỏ khi thiếu chỗ. Giữ điểm nối với đường ngoài làng.
- 60 sân đất trước nhà, nền đất mòn quanh khu làm nghề, đất ẩm/sỏi quanh giếng, 36 luống rau, các mảng cỏ loang và 29 hàng rào bổ sung.
- Giữ 18 lán quân, 20 SkinnedMeshRenderer và toàn bộ vị trí/mesh nhân vật hiện có. Không sửa model người.
- Còn 50.530 cây terrain: dọn 2 cây trong phạm vi sân/đường mới. Không dựng thêm rừng hoặc tạo thêm TerrainData.

Nhóm cảnh mới: `BachDang_Living_Settlements/10_Lang_Tu_Nhien`; tài nguyên riêng: `Assets/BachDangVillageNatural`.

## Giới hạn RAM

- Giữ `_renderTreesWithNatureRenderer = false`, tầm cây 1.800 đơn vị, thiết lập đuốc không đổ bóng, các sửa chữa camera và render pipeline của người dùng.
- Nature Renderer tự bật chế độ cây native của Terrain và đặt khoảng cách detail native về 0 khi nạp. Đây là lý do hai trường này được Unity ghi lại khi lưu scene; không bật lại bộ nạp cây Nature Renderer.
- Texture của phần đất/cỏ dùng chung tài nguyên có sẵn. Không thêm ảnh lớn, đèn, collider hoặc script Update cho cảnh mới.
- Cỏ trang trí mới còn **171 cụm**, ghép thành 6 mesh: 7.872 hoặc 7.551 đỉnh mỗi mesh. So với lần dựng thử đầu tiên, giảm khoảng 82% số đỉnh cỏ. Mỗi mesh giới hạn 8.000 đỉnh cho các lần dựng tiếp theo.
- Tổng geometry tự tạo cho đất/cỏ: **69.062 đỉnh, khoảng 8,70 MiB** theo `Profiler.GetRuntimeMemorySizeLong`. Con số này không gồm mesh nguồn hàng rào được dùng lại và không phải tổng RAM của Unity.
- Builder tự từ chối lưu nếu geometry mới vượt 12 MiB; không static-batch thêm các mesh đất/cỏ vốn đã riêng biệt để tránh bản sao vertex buffer.
- Folder tài nguyên mới khoảng 9,5 MiB trên đĩa. Dung lượng đĩa không đồng nghĩa với RAM khi mở scene.

## Kiểm tra trên máy hiện tại

Máy kiểm tra: RAM 32 GB, RTX 4060 Ti. Chưa kiểm tra trực tiếp Nitro RAM 16 GB, RTX 3050.

- Mở scene trước cải tiến: tiến trình Unity khoảng 4,52 GiB private bytes, 4,08 GiB working set ở mẫu đo sau chụp hình.
- Trong lượt Play sau cải tiến: khoảng 5,72 GiB private bytes, 4,65 GiB working set ở thời điểm đo. Hai mẫu ở hai chế độ khác nhau, **không dùng phần chênh làm RAM phát sinh của cảnh mới**.
- Unity allocated trong Play khoảng 2.007 MiB; reserved khoảng 3.870 MiB. Các bộ đếm Unity không bao gồm mọi allocation của driver/plugin và không thay thế số liệu của tiến trình.
- Play thành công, Console 0 error tại lần kiểm tra; dừng Play trả camera về vị trí mặc định, ngày 09:00 tự chạy, gravity −9,81 và `PC_RPAsset`.
- Mở lại scene đã lưu thành công; đủ 60 nhà, 18 lán, 20 actor, không Missing Script; TerrainCollider đúng dữ liệu; sai số đỉnh mặt đường so với cao độ thiết kế dưới 0,000023 đơn vị.
- Sửa `DayNightCycle` giữ callback tới đối tượng đã hủy khi đổi/mở lại scene: đăng ký callback không lặp, gỡ cả lúc Disable/Destroy, bỏ qua đối tượng Unity đã bị hủy. Kiểm tra bằng cách mở lại scene rồi trực tiếp gọi callback của đối tượng cũ: không exception; bầu trời mới vẫn hoạt động lúc 09:00.
- Còn các warning của shader cỏ/terrain nguồn. Chưa đo FPS trên laptop, chưa xuất build, và phép mở lại scene không phải phép đo đỉnh RAM của một lần khởi động Unity hoàn toàn mới.

## Kiểm tra trên laptop

Mở đúng `beachBoat`, chờ nạp xong rồi dùng **Bach Dang → Report Current Map Memory** trước Play và sau Play. Báo cáo JSON ở `.utmp/MapMemory`; công cụ chỉ đọc các đối tượng đã nạp, không nạp thêm dependencies hoặc khởi động lại bộ cây. Đồng thời xem RAM tiến trình Unity và bộ nhớ hệ thống lúc đang mở scene trong Task Manager.

Giữ tắt Nature Renderer tree streaming khi tiếp tục chỉnh map. Việc giảm tầm vẽ không tự đảm bảo giảm allocation lúc plugin nạp cây. Khi đánh giá thay đổi mới, so sánh ở cùng scene, camera và chế độ Editor/Play trên laptop đích.

Ảnh và báo cáo kiểm tra ở `.utmp/NaturalVillage` và `.utmp/MapMemory` (không đưa vào Git). Sao lưu trước thay đổi nằm trong `.utmp/NaturalVillage/beachBoat-before-*.unity` và `terrain-before-*.asset`.
