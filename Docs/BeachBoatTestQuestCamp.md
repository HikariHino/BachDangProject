# Doanh trại TestQuest trong beachBoat

> Cập nhật mật độ sau khi chuyển ra đảo: [BeachBoatCampLife.md](BeachBoatCampLife.md). Tổng số lều hiện tại là 164; các số 25 lều/358 collider dưới đây mô tả bản chuyển trại ban đầu.

Ngày 13/10/2026, thay các công trình cũ bên trong `05_Trai_Du_Bi` bằng `TestQuest_Camp`: 25 lều, đài chỉ huy, hàng rào/chòi canh, cọc trang trí, đuốc và thuyền đóng dở từ bản TestQuest hiện có trên máy. Các doanh trại khác của beachBoat giữ nguyên.

**Cập nhật cùng ngày: đã chuyển toàn bộ trại này ra đảo tương ứng trong ảnh TestQuest**, không còn ở vị trí đất liền. Tọa độ bố cục X/Z được quy đổi ×7 theo terrain beachBoat; kích thước lều/chòi/nhân vật giữ nguyên để không thành công trình khổng lồ. Vì vậy khoảng cách giữa các lều rộng hơn khi nhìn toàn đảo. Căn đáy công trình theo mặt đất, giữ đồ trang trí gần lều/đài.

## Liên kết gameplay

- Giữ nhân vật scale 7 và logic nhiệm vụ đã chuyển trước đó; không sao chép thêm nhân vật hoặc bootstrap từ TestQuest.
- Điểm bắt đầu/quay lại trại trên đảo ở `(-5508.01, 123.50, 1221.07)`, quay về phía đài chỉ huy.
- Ngô Quyền ở `(-5508.01, 139.81, 1126.07)`, căn chân theo mặt sàn đài. Tầm tương tác 55 cho phép nói chuyện từ sân phía trước.
- Hai điểm đi làng/bờ sông đặt tại lối ra hàng rào `(-6382.60, 122.31, 455.00)`; liên kết điểm đến cũ vẫn được giữ.
- Lần chuyển ra đảo loại thêm 787 cây trong/sát khu trại, còn 49.555 cây. Không sửa độ cao hoặc lớp phủ terrain. Lần đặt trên đất liền trước đó đã loại 22 cây.

## Bộ nhớ và hiển thị

- Ba model AI nặng (lều, đài, cọc trang trí) dùng 9 mesh LOD riêng trong `Assets/BeachBoatStory/Camp`, tổng bộ nhớ mesh do Unity báo khoảng **7,66 MiB**, so với khoảng 255,9 MiB của ba mesh nguồn. Đây là số đo của nhóm mesh, không phải tổng RAM của scene/game.
- Các lều dùng chung mesh/material/texture. Không chép texture, không thêm đèn. Bật material instancing; tránh static batching nhân bản mesh theo từng lều.
- LOD giảm chi tiết theo khoảng cách; tắt bóng ở LOD xa nhất và cull khi rất xa. Để nối chu vi đảo lớn hơn, thêm 1.998 đoạn hàng rào dùng chung mesh/material có sẵn, không tạo mesh/texture mới; các đoạn bổ sung không đổ bóng. Toàn trại có 2.551 renderer tính cả các LOD, được cull theo khoảng cách.
- Collider lều/đài dùng mesh LOD1; tháp/thuyền dùng box. Hàng rào dùng 245 box dọc từng đoạn, chừa lối đi nhiệm vụ, thay vì một collider trên mỗi panel. Toàn trại vẫn có 358 collider sau khi chuyển ra đảo.
- Toàn trại dùng chung 27 mesh (kể cả thuyền/tháp), khoảng 24,11 MiB bộ nhớ mesh theo Unity. Đây không phải tổng RAM; các GameObject hàng rào thêm vẫn có chi phí quản lý. Scene trên đĩa tăng từ khoảng 11,6 MB lên 21,7 MB.
- beachBoat không còn phụ thuộc ba FBX AI nặng này. FBX/material nguồn và scene TestQuest không bị chỉnh sửa. Không nạp TestQuest bổ sung lúc chơi.

## Kiểm chứng

- Chạy Unity 6000.6.0f1 Play Mode: 39 assertion qua mở đầu, nhận lệnh, 5 gỗ, chống nhặt trùng/ngoài tầm, bốn chuyển khu vực, đóng cọc, báo cáo cuối, camera/AudioListener, di chuyển và nhảy đều đạt.
- Sau khi chuyển ra đảo, chạy lại đủ 39 assertion trong Play Mode đều đạt; điểm xuất hiện và hai điểm đi làng/sông không chạm collider công trình. Xem ảnh toàn đảo và cận cảnh đài chỉ huy trong Game; khoảng cách nói chuyện từ sân là 51,64, trong tầm 55.
- Trại không có missing script; không có exception/lỗi C# trong lượt kiểm tra. Dự án còn cảnh báo asset/collider có sẵn.
- SHA256 TestQuest vẫn là `6AF3268CBDB452A7FCA8AF35E3C04561C09D11767B9DB2F848DDA111C61A23B7`.
- Chưa đo RAM/FPS trên laptop Nitro 16 GB / RTX3050; kết quả trên máy hiện tại không bảo đảm hiệu năng trên laptop.

`Assets/Editor/BeachBoatCampTransfer.cs` là công cụ chuyển một lần, không chạy trong game. Scene đã lưu sử dụng trực tiếp các mesh đi kèm. `ImportMeshes()` chỉ cần dữ liệu nhị phân tạm `.utmp/CampTransfer/*-lod*.bin` nếu dựng lại mesh từ đầu; các mesh đã có thì được bỏ qua. `ReplaceCamp()` từ chối chạy khi đã có `TestQuest_Camp`, tránh sao chép trùng.

`Assets/Editor/BeachBoatCampIsland.cs` chuyển trại đã nhập ra đảo, chặn chạy lặp qua `Island_Fence_Infill`. Menu `Bach Dang → Move TestQuest Camp To Island`; công cụ chỉ chạy trong Editor. Bản sao trước khi chuyển, ảnh và kết quả kiểm tra tại `.utmp/CampIsland`.
