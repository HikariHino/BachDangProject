# Bổ sung sinh hoạt doanh trại trên đảo

Ngày 13/10/2026, bổ sung trực tiếp vào `Assets/Scenes/beachBoat.unity`, trong `05_Trai_Du_Bi/TestQuest_Camp/Island_Camp_Life`.

- Giữ 25 lều gốc, thêm 139 lều: tổng **164 lều**, chia thành các cụm quân và hai dãy quanh khu chỉ huy.
- Thêm 3 kho tiếp tế, 3 sân tập, 71 cụm thùng lương thực/củi/ván gỗ và mạng đường đất nối các khu.
- Giữ kích thước lều tương ứng nhân vật scale 7; không phóng lớn nhân vật hoặc đảo. Không thêm người, animation, đèn hoặc particle.
- Chừa lối từ điểm xuất hiện tới cổng nhiệm vụ, khoảng trống trước đài chỉ huy và khoảng cách giữa các lều. Kho/lều bổ sung là công trình trang trí với box collider, không có nội thất đi vào.
- Dọn đúng 2 cây Conifer dạng GameObject nằm trên đường/sân mới. Giữ nguyên TerrainData và 49.555 cây terrain, TestQuest, 15 SkinnedMeshRenderer và transform nhân vật hiện có.

## Ngân sách tài nguyên

- Lều mới dùng chung 3 mesh LOD đã tối ưu, material và texture của lều cũ. Kho/đồ dùng tái sử dụng tài nguyên đã có trong beachBoat; đối chiếu dependencies không thêm texture.
- Mesh mới duy nhất là đường/sân: 6.980 đỉnh, khoảng **0,475 MiB** theo Unity Profiler. Đây chỉ là bộ nhớ mesh mới, không phải tổng mức tăng RAM của scene; vật thể/collider/material vẫn có chi phí quản lý.
- Thêm 504 renderer tính cả các LOD và 167 collider đơn giản; toàn trại có 525 collider. Các vật nhỏ được cull theo khoảng cách, không đổ bóng; không có script mới chạy mỗi frame.
- Không bật static batching cho các lều lặp lại để tránh nhân bản mesh. Các material bổ sung bật instancing và tham chiếu texture cũ.
- Chưa đo trực tiếp RAM/FPS trên laptop Nitro 16 GB / RTX3050.

## Kiểm chứng bản đã lưu

- Mở lại Unity và chạy 39 assertion trong Play Mode: mở đầu, đối thoại, nhận lệnh, 5 vật nhặt, chống nhặt trùng/ngoài tầm, bốn điểm chuyển khu vực, đóng cọc, báo cáo cuối, camera/AudioListener, di chuyển và nhảy đều đạt.
- Không missing script trong doanh trại. Capsule kiểm tra điểm xuất hiện/hai cổng không chạm công trình; capsule cast trên hành lang từ điểm xuất hiện tới cổng không chạm phần bổ sung.
- Đã xem ảnh toàn đảo, khu chỉ huy và cận cảnh nhân vật trong Game. Không lỗi C# hoặc exception trong log lượt kiểm tra cuối; còn cảnh báo asset/collider có sẵn của dự án.
- SHA256 TerrainData sau thay đổi khớp bản trước; TestQuest vẫn có SHA256 `6AF3268CBDB452A7FCA8AF35E3C04561C09D11767B9DB2F848DDA111C61A23B7`.

## Công cụ

`Assets/Editor/BeachBoatCampDensity.cs`, menu **Bach Dang → Dress Island Military Camp**, chỉ dùng trong Editor và từ chối chạy lặp khi đã có `Island_Camp_Life` hoặc thư mục tài nguyên `CampLife`. Scene đã lưu sẵn, người chơi không cần chạy công cụ.

Bản sao scene trước chỉnh sửa, ảnh và báo cáo tại `.utmp/CampDensity` (không đưa vào Git).
