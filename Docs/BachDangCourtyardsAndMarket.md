# Sân nhà và chợ — 09/10/2026

Đã lưu trong `Assets/Scenes/beachBoat.unity`, nối tiếp phần làng tự nhiên và các sửa chữa RAM của người dùng.

## Cảnh vật

- Trang trí đủ 60 sân nhà bằng cụm chum/giỏ cạnh đồ dùng đã có; giữ khoảng trống giữa cửa và đường. Có biến thể kích thước, cách đặt và vật dụng giữa các hộ.
- Thêm 2 mái vải nhỏ tại những sân còn đủ chỗ, mỗi mái có bàn làm việc và giỏ bên dưới.
- Tận dụng 12 mái che chợ có sẵn ở ba làng. Bổ sung bàn bày hàng quay vào sân chung, sắp các thùng hàng cũ sang hai bên quầy.
- Chuyển 6 cụm hàng hóa từ ngoài rìa về gần các sạp. Thêm 6 ghế nghỉ bên sân chung; giữ giếng và lối qua chợ.
- Không thay nhà, lán quân, model người; giữ nguyên 50.530 cây địa hình.

Nhóm mới: `BachDang_Living_Settlements/11_San_Nha_Va_Cho`. Tài nguyên riêng: `Assets/BachDangCourtyards`.

## Ngân sách bộ nhớ

Chum, giỏ, bàn, khung và mái vải dùng **5 mesh chung**. Sau Play và mở lại scene, Unity báo tổng bộ nhớ của 5 mesh là **76.936 byte (khoảng 75 KiB)**; mẫu ngay sau tạo là 42.448 byte. Số này là bộ đếm mesh, không phải toàn bộ chi phí RAM của GameObject/renderer/material.

- 241 MeshRenderer mới, 3 material mới không có ảnh riêng; vải dùng material sẵn có.
- Không thêm texture, Light, ParticleSystem, Collider, Animator hoặc script chạy mỗi frame.
- Các chi tiết mới không đổ bóng, không static batching để tránh nhân bản hình học; material chum/giỏ/gỗ bật hỗ trợ instancing. Hiệu quả batching thực tế còn phụ thuộc render pipeline.
- Builder chặn vượt 1 MiB mesh chung hoặc 260 renderer. Giữ các cài đặt tối ưu trước đó, bao gồm Nature Renderer tree streaming tắt.
- Toàn bộ folder asset mới và metadata khoảng 98 KiB trên đĩa. Không sao chép TerrainData, không thay ProjectSettings hay texture nguồn.

## Kiểm tra

- Đã xem cận sân nhà và mặt trước quầy hàng trong Play; chỉnh lại hướng quầy theo ảnh thực tế.
- Không có lỗi Console mới trong hai lượt Play; lỗi DayNightCycle cũ từ trước lần sửa ở lượt trước vẫn có trong lịch sử CLI nhưng không tái xuất hiện.
- Mở lại scene đã lưu: 60 sân, 12 quầy, 2 bàn làm việc dưới mái hiên, 6 ghế, không Missing Script; 0 light/collider mới; đủ 50.530 cây.
- Bản sao trước thay đổi, ảnh và báo cáo: `.utmp/Courtyards`. Báo cáo bộ nhớ trước/sau nằm trong `.utmp/MapMemory`.
- Kiểm tra trên máy RAM 32 GB/RTX 4060 Ti; chưa chứng nhận FPS hay đỉnh RAM lúc khởi động trên laptop 16 GB/RTX 3050.

Menu `Bach Dang → Dress Courtyards And Market` không tạo lại chi tiết khi nhóm mới đã tồn tại. Không chạy các builder terrain đời trước để áp dụng phần trang trí này.
