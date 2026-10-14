# Đối chiếu TestQuest của Thái với beachBoat — 13–14/10/2026

Nguồn: nhánh `origin/thai` tại `03aad75`, các commit gameplay `ac85205` và `aee7027`, cùng các thành phần đã có trong scene nguồn. Đã fetch lại nhánh khi bắt đầu đối chiếu.

## Kiểm tra bổ sung cổng đi làng — 14/10/2026

Đã fetch và kiểm tra lại `origin/thai` tại `99fde7f`, bao gồm commit `1c1607b` (cổng quay về doanh trại). Mở bản scene nguồn trong preview bằng Unity, đóng và xóa bản tạm sau khi đọc; không thay scene TestQuest trong dự án.

- Scene nguồn có `GatePrompt` tại `InvisibleBarrier`, đi tới `VillageArrivalPoint`; `VillageGate_ToCamp` là `CampGateTransition` loại VillageGate, đích `SpawnPoint_ReturnToCamp`. BeachBoat đã có chức năng tương đương: `CampToVillage → VillageArrival` và `VillageToCamp → CampArrival`, fade và chuyển sang bước báo cáo Ngô Quyền.
- Nguồn có 7 khúc gỗ `CayGo` và một `WoodPickup` gắn lên terrain. BeachBoat giữ 7 lựa chọn gỗ, yêu cầu 5; không chép component nhặt gỗ trên terrain hoặc chạy thêm bộ đếm song song. Các model làng/hàng rào trong nhánh merge không đưa sang thay bản đồ.
- Điểm sang làng của beachBoat cách chỗ xuất hiện khoảng 1.160 đơn vị, trước đây không có model cổng. Thêm 3 khung cổng tại điểm đi làng, quay về từ làng và quay về từ bãi cọc; cổng đi làng/đi bãi cọc dùng chung vị trí nhưng mở theo bước nhiệm vụ.
- Thêm HUD chỉ hướng theo camera và khoảng cách tới mục tiêu hiện tại. Khi dịch chuyển ra bờ sông, chỉ dẫn đổi từ cổng sang bãi cọc; lúc quay về đổi sang Ngô Quyền. Chỉ dẫn ẩn trong mở đầu, hội thoại, fade và chế độ sa bàn.
- Cả phím E và kiểm tra tự động dùng `TryInteract`, kiểm tra cùng điều kiện nhiệm vụ, khoảng cách và khóa tương tác. Dời `Camp_Guard_Mid_Right` khỏi đường từ chỗ xuất hiện tới cổng.
- Ba cổng dùng chung prefab `pf_build_gate_01` (mesh 2.534 đỉnh), material có sẵn, collider hộp và LOD cull; không thêm đèn hay bóng động. HUD cache tham chiếu và cập nhật nội dung 5 lần/giây, không quét toàn scene mỗi frame.

Kiểm tra trước khi khởi động lại: 111 kiểm tra cổng/chỉ dẫn/đường đi đạt, 43 kiểm tra toàn luồng nhiệm vụ đạt. Đã xem ảnh Game có cổng, chỉ hướng và lời nhắc E. Sau khi bài kiểm tra hoàn tất, Editor có một crash native tại `ProcessCameraIndexListIsNodeVisibleStep1 / CullDynamicObjectsJob`; không có bằng chứng đủ để quy nguyên nhân cho RAM hoặc một script cụ thể.

Lượt tiếp tục cùng ngày đã fetch lại tới `cc7d5dd`. Commit scene `d7fb36b` thay cổng về bằng `Gate_ReturnToCamp ` có `GatePrompt`, tầm 30, lời nhắc “Bấm E để quay lại doanh trại”, đích `SpawnPoint_ReturnToCamp`. Không có thay đổi script gameplay so với `99fde7f`; danh sách component nguồn đã đối chiếu lại bằng preview. Chức năng về trại và chuyển sang báo cáo đã có ở `VillageToCamp` của beachBoat. Giữ điều kiện đủ 5 gỗ trước khi về để tránh mắc kẹt giữa các bước; không thêm `GatePrompt`/bộ đếm gỗ thứ hai. Các asset mô hình thuyền, hàng rào, cổng và clip chèo thuyền mới chưa được nối thành tính năng gameplay trong scene nguồn, không tự đưa sang map.

Sau khi mở mới Editor từ scene đã lưu: chạy lại 43 kiểm tra nhiệm vụ đạt; tiếp tục 40 mẫu đổi camera chơi/sa bàn và dịch chuyển qua 4 khu vực trong 78 giây, không tái hiện crash. Unity profiler báo allocated memory tối đa 1.868 MiB trong lượt thử; đo tiến trình sau lượt thử có working set 4.374 MiB và private memory 5.057 MiB. Số liệu Mono `Process.WorkingSet64` trong eval trả 0 nên không dùng; số liệu tiến trình được đo qua PowerShell. Đây là phép đo Editor trên máy hiện tại, không phải peak RAM toàn máy hoặc bảo đảm laptop 16 GB. Crash trước đó chưa xác định được nguyên nhân; lần chạy lại thành công không được coi là đã sửa dứt điểm.

| Thành phần của Thái | Trạng thái ở beachBoat |
| --- | --- |
| Mở đầu, nhân vật scale 7, camera góc nhìn thứ ba | Đã có; giữ điều khiển và font tiếng Việt đã sửa |
| Hội thoại Ngô Quyền, lệnh đi làng, gom 5 gỗ, báo cáo, đóng cọc, báo cáo cuối | Đã có trong namespace `BeachBoatStory` |
| Cổng và fade chuyển khu vực | Đã có 4 cổng, dùng các vị trí của map beachBoat |
| 3 dân làng có hội thoại | Đã có |
| Lều, đài chỉ huy, hàng rào, đồ trang trí doanh trại | Đã chuyển và tối ưu ở các đợt trước |
| 7 lính đứng, 1 lính ngồi, 1 lính nằm | Commit `04b839b` đã chuyển đủ 9 model; đợt này hoàn thiện bố trí, animation và hội thoại |
| `GatePrompt`, `WoodPickup`, `WoodQuestManager` | File đã có nhưng không gắn vào beachBoat: chức năng tương ứng đã do hệ thống `BeachBoatStory` đảm nhiệm; không chạy hai bộ đếm gỗ/cổng cùng lúc |
| Kiếm/khiên của nhân vật | Hai script trong scene Thái bị tắt và không có object được gán; không xem đây là tính năng hoàn chỉnh để tự bật |
| Terrain, nước, cây và bản đồ cũ | Không đưa sang |

## Hoàn thiện nhóm lính

- Giữ đúng 9 model có sẵn, không tạo thêm bản sao. Bố trí 4 lính trên đài, 2 lính trước sân, 1 lính gần cổng và 2 lính nghỉ ở khu trại.
- Bốn lính trên đài được căn theo raycast xuống collider mặt sàn; công cụ từ chối đặt lính nếu không có mặt sàn đỡ bên dưới.
- Cả 9 có hội thoại bằng E. Hai câu thoại của lính nghỉ lấy từ `AttachSeatedCampNpcDialogues` trong bootstrap của Thái. Lính đứng dùng câu thoại doanh trại của `AttachOrdinaryNpcDialogues`.
- Bộ điều khiển `ThaiCampPoses` riêng cho beachBoat có Idle/Sitting/Laying và đủ parameter cho `NPCCampPose`; animation ngồi dùng một clip loop riêng. Không sửa clip hoặc controller nguồn của TestQuest.
- NPC gần nhất nhận tương tác; Ngô Quyền được ưu tiên khi ở trong tầm nhận lệnh/báo cáo. Hội thoại thường không tăng tiến độ nhiệm vụ.
- Cả 9 dùng chung một mesh và texture đã có. Mesh nguồn được Unity báo khoảng 19,32 MiB, texture 2048 khoảng 3,33 MiB, không nhân bản theo số lính. Có LOD cull từ xa, Animator CullCompletely, collider box đơn giản và không thêm bóng động cho nhóm này. Đây không phải tổng RAM của game và chưa phải phép đo trên laptop.

Scene hiện có 7 vị trí gỗ từ commit `04b839b`; nhiệm vụ vẫn yêu cầu 5. Hai vị trí dự phòng được giữ, không bật bộ đếm thứ hai.

Công cụ Editor `Bach Dang/Complete Thai Camp Actors` chỉ chỉnh nhóm `Camp_Soldiers` đã tồn tại; không cần nạp scene TestQuest trong game.

## Kiểm chứng

- 39 kiểm tra luồng nhiệm vụ/điều khiển trong Play đều đạt sau lần sửa cuối.
- 62 kiểm tra bổ sung đạt: đủ 9 lính, rig và culling, hội thoại từng lính, giữ nguyên tiến độ, ưu tiên Ngô Quyền/cổng/gỗ, khóa tương tác khi ở sa bàn và ngoài tầm, đủ glyph tiếng Việt, không có hệ nhiệm vụ cũ chạy trùng.
- Xem ảnh trong Game của lính ngồi/nằm, hội thoại và đội gác. Console lượt cuối không có lỗi C# hoặc exception; còn cảnh báo asset có sẵn.
- TestQuest và TerrainData giữ nguyên SHA256 lần lượt `6AF3268CBDB452A7FCA8AF35E3C04561C09D11767B9DB2F848DDA111C61A23B7` và `327E4125D24732268FF5511CD4CCC1CBA59EFB9EB5E88EFFD2EDAE52C5E93BB2`. Terrain vẫn có 49.555 cây.
