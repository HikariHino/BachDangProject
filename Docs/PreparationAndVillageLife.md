# Chuẩn bị xuất quân, dựng cọc và sinh hoạt NPC

## Tay múc gạo

Tay trái đặt cố định lên thành rổ gạo bên trái. Chỉ tay phải kéo gáo; lòng bàn tay hướng xuống, không dùng hướng trục cán để lật ngửa bàn tay. Vẫn giữ thao tác kéo/thả và thanh thiếu/đủ/dư. Kiểm thử đo hướng lòng bàn tay của mesh hiển thị và khoảng cách tay trái tới rổ thật.

## Bài tập cung bắt buộc

Trước lệnh xuất quân phải hoàn thành một lượt sáu mũi, trúng ít nhất bốn bia. Nhật ký và nhiệm vụ góc phải chỉ đường tới bãi tập; đứng gần vòng vàng và bấm E. Bảng tại bãi và nhắc trên màn hình được làm lớn hơn. Khi bắt đầu, camera quay theo hướng bắn. Đạt thì gặp Ngô Quyền nhận lệnh; không đạt có thể tập lại.

Không so sánh trạng thái bằng `>= 9`: trạng thái vật tư mang số 13 và trống trận mang số 12 nhưng đều nằm trước trận đánh. Bãi tập mở theo ý nghĩa trạng thái. Save cũ ở bước chờ xuất quân vẫn tập được khi đã xuống thuyền, trở về bờ; chưa đạt thì chưa khởi chiến. Save đang thực sự chiến đấu hoặc đã thắng tiếp tục theo trạng thái đó. Cờ hoàn thành/kỷ lục cung dùng các trường save sẵn có, không ghi giả kết quả. Thời gian đang tập không phạt hạng chiến dịch.

## Ba cọc mẫu và chuyển thời gian

Người chơi tới một điểm đánh dấu, tự dựng ba cọc mẫu thay cho lặp thao tác trên cả năm cụm. Vác tới mạn, chọn vị trí không vướng, cuộn chuột chỉnh nghiêng theo góc hiển thị (12°, 18°, 9°, sai số cho phép ±3°). Mỗi cọc cần ba lần nện đúng.

Khi nện, chỉ tay và búa hiển thị, thân nhân vật không che đầu cọc. Giữ chuột trái, kéo ngang căn búa vào tâm; kéo lên nâng búa rồi thả khi lực 65–95%. Nện nhẹ/quá lực hoặc lệch tâm không tăng tiến độ, có phản hồi và có thể thử lại. Đầu búa chạm đúng vị trí 3D của đầu cọc, chiều dài búa/tay không bị kéo để bù tầm với. Hai tay nắm riêng trên/dưới cách 3,6 đơn vị, khuỷu mở hai bên và cổ tay thẳng. Hai thanh chỉ dẫn nằm dưới vùng thao tác.

Sau cọc thứ ba, chuyển cảnh đen “Vài ngày sau…” kể quân dân hoàn thiện theo mẫu rồi hiện toàn trận địa. Vẫn sử dụng năm cờ cụm cọc của save cũ; toàn bộ cờ được đánh dấu trong lúc màn hình đen. Lưu bị chặn trong thao tác/chuyển cảnh. Tải save hoàn thành không mở lại minigame hay để sót tay/công cụ. Save đang có một phần cụm vẫn giữ phần đó cho đến khi làm xong bài mẫu mới.

Lượt mới ghi mốc S đóng cọc 90 giây, A 135, B 180 trong `campaign.stakeTargetSeconds`. Mốc có dư cho ba lần mang/đặt, chín nhịp nện, căn chỉnh và đọc chỉ dẫn. Bản ghi cũ không có trường này tiếp tục dùng phép tính quãng đường/năm cụm cũ, giữ nguyên thời gian thực đã ghi.

## Làng và doanh trại

`NPCDailyActivity` sử dụng các vườn rau, sân phơi vải, giếng, sân nghề và khu hàng hóa có sẵn. Người làm ruộng cuốc/chăm rau; dân làng phơi/gấp vải, lấy nước; người chợ xếp hàng; ngư dân vá lưới; người chuyển hàng giữ giỏ; quân sĩ lau sửa dụng cụ giữa các lượt tuần tra. NPC nghỉ/ngủ, chủ tướng và thuyền chiến tiếp tục vai trò riêng.

NPC đi thật tới chỗ làm, giữ chỗ riêng rồi làm việc trong một khoảng thời gian, thay vì chỉ đổi dòng chữ hoạt động. Tay, dụng cụ, vải, lưới và giỏ chuyển động trong không gian 3D. Các lượt khác pha và chỗ làm không nhận hai chủ cùng lúc. Tạm dừng công việc khi trò chuyện hoặc bị tắt theo ngân sách hiển thị; props được dọn khi NPC hủy. Đây là sinh hoạt nền, không thay đổi điểm chiến dịch hay lưu vị trí tạm của dụng cụ vào save.

Ở cổng: không chọn điểm nghỉ trong phạm vi lối thông; tách được cả hai actor trùng tọa độ; đổi đường khi bị cản thay vì lặp một điểm vô hạn. Tấm ván/ngưỡng thấp có bề mặt được bước lên bằng collider thật, không vô hiệu hóa va chạm. Tường, hàng rào, nhà và NPC khác vẫn chặn đường.

## Kiểm thử CLI

`BeachBoatPhysicalVillagePlaytest`, `BeachBoatArcheryPlaytest`, `BeachBoatCasualWorkPlaytest` và `BeachBoatDailyLifePlaytest` dùng Play Mode của Unity qua CLI. Các báo cáo nằm trong Logs; dùng thư mục save QA riêng và kiểm SHA-256 file save thật. Ca NPC cố ý đặt hai người trùng tọa độ ở cổng rồi quan sát tách và các công việc trong làng; ca cọc thử lực yếu, lệch tâm, sai góc và lưu/tải sau chuyển thời gian.

Kết quả ngày 28/10/2026:

- Bốn sạp và tay múc: 411 kiểm tra đạt.
- Cung bắt buộc, trạng thái vật tư 13, save chờ xuất quân cũ, sáu mũi tên và lưu/tải: 56 kiểm tra đạt.
- Ba cọc mẫu, lực/tâm/góc, tay nắm búa và chuyển thời gian: 53 kiểm tra đạt; 533 mẫu tư thế, sai số tiếp xúc tối đa 0,000124 đơn vị, cổ tay lệch dưới 0,028°.
- NPC ở cổng và sinh hoạt: 22 kiểm tra đạt. Quan sát được bảo dưỡng, lấy nước, phơi vải, chuyển hàng, vá lưới và làm vườn trong lượt cuối; buôn bán đã quan sát trong lượt trước. Kiểm tra làm vườn yêu cầu tới đúng vườn thật, không nhận một điểm dừng bị chặn là chỗ làm.

Cả bốn bộ kiểm thử xác nhận 13 file save thật không đổi. Không sửa scene hoặc ProjectSettings. Ảnh kiểm tra tay búa: `Logs/StakeConstruction_Hammer.png` và `Logs/StakeConstruction_Hammer_GripClose.png`; sinh hoạt: `Logs/DailyLife_*.png`. Đây là chuyển động sinh hoạt bằng IK và vật dụng của dự án, không phải bộ motion capture mới.

## Bãi tập cung có điểm và làn bắn riêng

Làn bắn được dành riêng cả trước và trong lượt chơi. NPC tránh toàn bộ đoạn đường cắt ngang làn, không chỉ tránh tọa độ của bia. Khi tạo bãi tập trong lúc khởi tạo scene, người đứng trong làn được đặt lại vào vị trí khô, trống bên ngoài bằng kiểm tra collider. Việc này diễn ra trước khi chơi; sau đó NPC đi bằng đường tuần tra thật. Dây và cọc thấp đánh dấu hai bên làn, không chặn người chơi.

Điểm dừng của mỗi NPC được giữ khoảng cách với điểm dừng của người khác. Nếu các waypoint cũ đều bận hoặc thuộc làn bắn, NPC tìm một điểm trống ở vòng ngoài. Nhóm trùng tọa độ tách theo các hướng đối xứng khác nhau; khi sát tường, thử cả hướng chéo thay vì tiếp tục ép một đường.

`ArcheryTrainingHUD` hiện điểm /60, số bia trúng và mục tiêu 4/6, bia cần ngắm và sáu ô kết quả từng mũi. Mũi đang bay có trạng thái riêng; trượt hiện đỏ, trúng hiện điểm xanh. Tâm 10, vòng giữa 7, vòng ngoài 4; bắn nhầm bia vòng vàng không có điểm. Mọi mũi đều phải tới đích hoặc trượt xong mới hiện bảng kết quả. Kết quả trùng của cùng một mũi không được cộng hai lần.

Bia trúng tên đổ về sau quanh chân trụ, mũi tên cắm theo bia. Khi tới lượt dùng lại, bia dựng lên, xong mới được bắn. Bảng kết quả có tổng điểm, số trúng, thời gian lượt, kỷ lục và nút tập lại/rời bãi. Hạng **luyện tập** là S ≥50, A ≥40, B ≥25, còn lại C; đây là hạng riêng của lượt bắn, không thay phép xếp hạng chiến dịch. Điều kiện bắt buộc trước xuất quân vẫn là trúng ít nhất bốn trong sáu mũi.

Đợt kiểm thử bãi tập: `ArcheryPlaytest.json` đạt 80 kiểm tra; `RangeCrowdPlaytest.json` đặt tám lính thật chồng tọa độ rồi quan sát 30 giây, tất cả đi tiếp, độ chồng collider sau ổn định bằng 0; 1.003 mẫu quan sát NPC gần bãi đều ở ngoài làn. Ảnh giao diện: `Logs/ArcheryTraining_Scoreboard.png`, `Logs/ArcheryTraining_Results.png`. Báo cáo và ảnh QA không đưa vào Git.

Kiểm tra hồi quy sinh hoạt sau khi thay đường đi: `DailyLifePlaytest.json` đạt 22 kiểm tra, quan sát bảo dưỡng, lấy nước, buôn bán, chuyển hàng, phơi vải và làm vườn. Cả ba bộ mới (80 + 21 + 22 = 123 kiểm tra) giữ nguyên SHA-256 của 13 file save thật. Lượt bắn cuối xác nhận bia có góc đổ thực tế trên 80°, dựng lại mới nhận mũi kế tiếp và kết quả được giữ qua lưu/tải.
