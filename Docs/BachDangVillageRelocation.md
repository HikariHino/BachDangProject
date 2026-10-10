# Dành đảo cho doanh trại — 09/10/2026

Đã lưu trực tiếp trong `Assets/Scenes/beachBoat.unity`.

- Chuyển 24 nhà của `01_Lang_Cho_Ben_Song` sang bờ nam, gần trại dự bị: tọa độ quy đổi theo scale 7 là (-950, 0, -730).
- Chuyển 16 nhà của `03_Xom_Vuon_Bo_Tay` sang bãi đất phía bắc: (-915, 0, 1095).
- Di chuyển đồng bộ các nhóm nhà/chợ, `09_Sinh_Hoat`, `10_Lang_Tu_Nhien`, `11_San_Nha_Va_Cho` và hai điểm camera của từng làng. Giữ tên nhóm để HUD tiếp tục tìm được làng.
- Giữ xóm chài, bến cá phía đông, ba khu doanh trại và toàn bộ 20 renderer nhân vật ở vị trí cũ. Không xây thêm doanh trại trong lần này.
- Chỉnh nền sân, đường và cỏ trang trí theo mặt đất mới; thêm hai lối tiếp cận ngắn. Tắt hai đường nối làng cũ trên đảo, giữ các công trình quân sự.

## Tối ưu và kiểm tra

- Tổng số nhà vẫn là 60; chuyển 40 nhà, không nhân đôi model hay texture. Không thêm người, đèn, collider hoặc script chạy mỗi frame.
- Dọn 166 cây nằm trong khuôn viên và đường vào làng mới; cây địa hình còn 50.364. Không mở rộng hay sao chép TerrainData dùng trong scene.
- Mesh sân/đường/cỏ phải đổi độ cao được lưu riêng trong `Assets/BachDangVillageRelocation`, giữ nguyên tài nguyên nguồn. Bộ đếm mesh ngay sau tạo: 7.340.784 byte (khoảng 7 MiB); đây không phải mức tăng tổng RAM của Unity.
- Nature Renderer tree streaming vẫn tắt; camera vẫn giới hạn xa 3.500. Góc chụp toàn đảo tạm tăng khoảng nhìn rồi được bỏ khi mở lại bản đã lưu.
- Đã kiểm tra hình ảnh hai làng và đảo. Kiểm tra bản scene lưu trên đĩa qua PreviewScene: 60 nhà, 20 renderer nhân vật, không Missing Script; 199 mesh nền ở cao hơn địa hình khoảng 0,048–0,086 m quy đổi. Điểm nền thấp nhất đã kiểm tra 17,387 m, trên mực triều cao 15,5 m.
- Trong lúc kiểm tra, Editor chuyển sang `SampleScene`; lượt Play đó không được tính là kiểm thử runtime của `beachBoat`. Chưa kiểm thử trực tiếp laptop 16 GB/RTX 3050. PreviewScene kiểm tra đã đóng, không đổi scene đang mở của người dùng.

Công cụ: `Bach Dang → Reserve Island And Relocate Villages`, chỉ áp dụng cho beachBoat ở Edit Mode; có bản sao trước thay đổi và một nhóm Undo. Nhóm `12_Di_Doi_Dan_Cu` ngăn chạy lặp. Bản sao, ảnh và báo cáo nằm trong `.utmp/Relocate`. Các số cây trong báo cáo các bước trước là số tại thời điểm thực hiện bước đó.
