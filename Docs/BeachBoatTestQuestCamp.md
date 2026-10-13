# Doanh trại TestQuest trong beachBoat

Ngày 13/10/2026, thay các công trình cũ bên trong `05_Trai_Du_Bi` bằng `TestQuest_Camp`: 25 lều, đài chỉ huy, hàng rào/chòi canh, cọc trang trí, đuốc và thuyền đóng dở từ bản TestQuest hiện có trên máy. Các doanh trại khác của beachBoat giữ nguyên.

## Liên kết gameplay

- Giữ nhân vật scale 7 và logic nhiệm vụ đã chuyển trước đó; không sao chép thêm nhân vật hoặc bootstrap từ TestQuest.
- Điểm bắt đầu/quay lại trại ở `(-6927.60, 123.82, -2894.14)`, quay về phía đài chỉ huy.
- Ngô Quyền ở `(-6719.91, 139.77, -3000.47)`, căn chân theo mặt sàn đài. Tầm tương tác 55 cho phép nói chuyện từ sân phía trước.
- Hai điểm đi làng/bờ sông đặt tại lối ra hàng rào `(-6847.80, 122.76, -3095.00)`; liên kết điểm đến cũ vẫn được giữ.
- Loại 22 cây trong/sát khu trại để tránh xuyên công trình; còn 50.342 cây. Không sửa độ cao hoặc lớp phủ terrain.

## Bộ nhớ và hiển thị

- Ba model AI nặng (lều, đài, cọc trang trí) dùng 9 mesh LOD riêng trong `Assets/BeachBoatStory/Camp`, tổng bộ nhớ mesh do Unity báo khoảng **7,66 MiB**, so với khoảng 255,9 MiB của ba mesh nguồn. Đây là số đo của nhóm mesh, không phải tổng RAM của scene/game.
- Các lều dùng chung mesh/material/texture. Không chép texture, không thêm đèn. Bật material instancing; tránh static batching nhân bản mesh theo từng lều.
- LOD giảm chi tiết theo khoảng cách; tắt bóng ở LOD xa nhất và cull khi rất xa. 553 renderer được lưu gồm cả các mức LOD, không phải 553 renderer đồng thời hiển thị.
- Collider lều/đài dùng mesh LOD1; hàng rào và thuyền dùng box. Toàn trại có 358 collider, so với riêng hàng rào nguồn có 1.267 collider.
- beachBoat không còn phụ thuộc ba FBX AI nặng này. FBX/material nguồn và scene TestQuest không bị chỉnh sửa. Không nạp TestQuest bổ sung lúc chơi.

## Kiểm chứng

- Chạy Unity 6000.6.0f1 Play Mode: 39 assertion qua mở đầu, nhận lệnh, 5 gỗ, chống nhặt trùng/ngoài tầm, bốn chuyển khu vực, đóng cọc, báo cáo cuối, camera/AudioListener, di chuyển và nhảy đều đạt.
- Xem ảnh toàn trại và cận cảnh trong Game. Sau đó căn lại chân Ngô Quyền theo sàn; kiểm tra khoảng cách tương tác tại sân là 51,53, trong tầm 55.
- Trại không có missing script; không có exception/lỗi C# trong lượt kiểm tra. Dự án còn cảnh báo asset/collider có sẵn.
- SHA256 TestQuest vẫn là `6AF3268CBDB452A7FCA8AF35E3C04561C09D11767B9DB2F848DDA111C61A23B7`.
- Chưa đo RAM/FPS trên laptop Nitro 16 GB / RTX3050; kết quả trên máy hiện tại không bảo đảm hiệu năng trên laptop.

`Assets/Editor/BeachBoatCampTransfer.cs` là công cụ chuyển một lần, không chạy trong game. Scene đã lưu sử dụng trực tiếp các mesh đi kèm. `ImportMeshes()` chỉ cần dữ liệu nhị phân tạm `.utmp/CampTransfer/*-lod*.bin` nếu dựng lại mesh từ đầu; các mesh đã có thì được bỏ qua. `ReplaceCamp()` từ chối chạy khi đã có `TestQuest_Camp`, tránh sao chép trùng.
