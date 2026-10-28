# Sửa mở đầu, bơi tự do và thao tác vật tư / đóng cọc

Đợt ngày 29/10/2026 theo ảnh phản hồi.

- Clip “Năm 938.” giữ phần đọc đầu, cắt cụm âm dư xuất hiện sau một khoảng lặng. Không phát tiếng chim, gió và nền sông tổng hợp trong phần dẫn truyện. File nguồn cũ để so sánh nằm trong `.utmp/AudioQA`, không đưa vào Git.
- `RiverBoardingCombat` nhận biết nước sâu cả khi chưa có phiên lên thuyền. Bơi tạm giữ điều khiển vật lý, lên bờ trả lại motor/collider/controller; ngưỡng vào/ra khác nhau để tránh đổi dáng liên tục ở mép nước. Trạng thái bơi dùng trường boarding có sẵn, suy ra phiên bơi tự do từ `aboard=false`, không đổi cấu trúc save cũ. Khi lên thuyền sau bơi, kết thúc tư thế bơi trước khi lưu cấu hình chèo.
- Bỏ mesh hình cầu đánh dấu bàn tay, ẩn vòng trỏ nổi trong bước dùng dụng cụ. Rổ gạo và bao được đặt sâu hơn trên mặt bàn; rổ có thành hở, vành đan, đống gạo và hạt nhỏ ghép thành một mesh thay cho vòng hạt to. Tay trái và miền kéo chuột cùng dịch với đồ vật.
- Cọc dùng chính mesh và chất liệu cọc nhọn của trận địa, đưa trục dài về hướng dựng cọc và giữ chiều dài hiển thị cố định. Bước vác chọn điểm có sàn gỗ dưới cả hai chân, bước chân ngắn hơn và đứng lùi vào trong. Khớp chân xoay theo trục ngang thật của nhân vật, thay cho trục X cục bộ của xương nhập từ model. Ba vị trí cọc ở bên ngoài mạn, tách nhau; góc ban đầu được căn theo từng cọc. Nếu E chưa thực hiện được, hiện lý do, có A/D chỉnh góc và R đặt lại vị trí.
- Có thuyền công binh và bốn lính thi công cùng lúc. Mẫu thuyền/nhân vật/cọc dùng tài nguyên dự án; thuyền và người có collider riêng. Tất cả đạo cụ thi công thuộc phiên làm việc và được dọn khi hoàn thành/hủy/load. Sau ba cọc mẫu vẫn chuyển cảnh “Vài ngày sau…” rồi khôi phục toàn trận địa bằng năm cờ cụm cọc đã có trong save.

Các ca CLI dùng thư mục save riêng và so sánh SHA-256 file lưu thật. Báo cáo và ảnh ở `Logs/` không đưa vào Git.

Kết quả cuối ngày 29/10/2026: biên dịch không lỗi; `GlobalWaterPlaytest.json` đạt 34 kiểm tra, `PhysicalVillagePlaytest.json` đạt 451, `CasualWorkPlaytest.json` đạt 57 và hồi quy `ShorelinePlaytest.json` đạt 63 (tổng 605). Ca bơi dùng ba trạng thái trước trận, lưu/tải khi đang bơi và lên lại thuyền. Ca vật tư đo cả bốn góc footprint của hai đồ đựng gạo so với mặt bàn; ca cọc xác nhận sàn dưới chân, mesh cọc của trận địa, bốn công binh và hoàn thành đủ ba cọc rồi lưu/tải sau chuyển thời gian. 13 file save thật không đổi. Không thay scene hoặc ProjectSettings.
