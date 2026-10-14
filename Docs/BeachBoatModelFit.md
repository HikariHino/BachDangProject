# beachBoat: tăng kích thước model, giữ nguyên map — 14/10/2026

Theo lựa chọn của người dùng, nhân vật và công trình tăng khoảng 2 lần, còn địa hình giữ kích thước 42.000 × 2.100 × 42.000 đơn vị Unity (6 × 6 km tham chiếu).

## Thay đổi

- Nhân vật chính scale 7 → 14, chiều cao mesh khoảng 13,59 → 27,17. NPC có sẵn, nhà dân, lều, kho, sân nghề, đồ dùng, thuyền và cổng nhiệm vụ được tăng tương ứng quanh chân công trình/mặt nước. Không thêm model người.
- Hàng rào doanh trại chỉ tăng chiều cao, giữ đường bao đảo. Cầu gỗ dân sinh chỉ tăng bề rộng; cao độ mặt cầu và chiều dài giữ nguyên.
- Sắp xếp lại 174 vị trí trong 230 nhà/lều, di chuyển sân và hàng rào nhỏ đi cùng nhà. Kiểm tra hộp bao mái không còn giao nhau quá 5 đơn vị theo cả hai trục ngang; các góc nhà nằm trên mức triều cao.
- Điều chỉnh tốc độ di chuyển, độ cao nhảy, khoảng cách camera, khoảng tương tác, điểm đến dịch chuyển và nhãn nổi cho tỷ lệ mới. Độ nhạy xoay camera giữ nguyên.
- `BachDangWorldScale.unitsPerReferenceUnit` vẫn là 7; không nhân kích thước terrain, cây rừng hay trọng lực thêm lần nữa.

## Tài nguyên và công cụ

Tái sử dụng mesh/material/texture và LOD hiện có. Ngưỡng LOD được điều chỉnh theo kích thước mới để hạn chế tăng khoảng cách hiển thị LOD chi tiết. Model lớn hơn vẫn có thể tăng chi phí vẽ trên màn hình; chưa đo FPS hoặc RAM trên laptop 16 GB/RTX 3050.

`BeachBoatModelFit.Apply()` chỉ dùng cho beachBoat chưa chuyển đổi. Scene đã có dấu `BeachBoat_Models_Fitted_2x` để chặn nhân lặp. Không chạy lại các builder dựng map/doanh trại cũ vì chúng dùng tỷ lệ và vị trí cũ.

Menu `Bach Dang/Separate Fitted Houses` sắp xếp nhà đã phóng lớn; `Bach Dang/Clear Trees Inside Fitted Houses` dọn cây terrain trong hộp bao mái mở rộng. Cả hai chỉ chạy trong Edit Mode của beachBoat. Backup và dữ liệu kiểm tra ở `.utmp/ModelFit` (không đưa vào Git).

## Kiểm tra

- 43 kiểm tra Play Mode đã qua sau khi tăng model và sắp xếp nhà: mở đầu, hội thoại Ngô Quyền, cổng đến làng, nhặt đủ 5 gỗ, trở về, đến bãi cọc, đóng cọc, báo cáo, đổi chế độ F9, di chuyển và nhảy. Các kiểm tra nhiệm vụ gọi điểm vào tương tác của game; không thay thế việc chơi thử mọi đường đi bằng bàn phím.
- Đã xem ảnh camera người chơi tại doanh trại và ảnh làng sau chỉnh bố cục: `.utmp/ModelFit/camp-player.png`, `.utmp/ModelFit/village.png`.
- TestQuest không chỉnh sửa; SHA256 `6AF3268CBDB452A7FCA8AF35E3C04561C09D11767B9DB2F848DDA111C61A23B7`.
- Mở lại scene bằng Unity xác nhận: scale nhân vật 14, đủ 230 nhà/lều, 0 giao mái theo ngưỡng kiểm tra, 0 nhà có góc dưới triều cao, 0 Missing Script. Script Editor mới đã biên dịch và chạy thành công.
- Dọn 8 cây trong phạm vi mái mở rộng, còn 49.547 cây terrain. Đối chiếu toàn bộ heightmap và alphamap trước/sau cho cùng SHA256 `3BDD4B2EA68F1A4C7CD19DFC909F492631342EDFF9ADCCB02BCE433E976A74FD`. Backup terrain trước dọn tại `.utmp/ModelFit/terrain-before-tree-clear.asset`.

## Giới hạn xác minh khi mở lại Editor

Trong lần hoàn tất ngày 14/10, hai lần mở Editor có giao diện bị crash trước khi nhận lệnh. Windows ghi lỗi `0xc0000005`: lần đầu module unknown, lần thứ hai `nvgpucomp64.dll`; log Unity lần thứ hai có stack `nvwgf2umx`. Chưa xác định nguyên nhân gốc, không có bằng chứng để kết luận hai lần này do thiếu RAM hoặc do thay đổi kích thước model.

Phiên `-batchmode -nographics` mở scene và thực hiện kiểm tra dữ liệu/dọn cây thành công. Đây không phải xác nhận hiển thị hay FPS. Ảnh và 43 kiểm tra Play Mode ở trên thuộc lượt chạy trước đó; vẫn cần kiểm tra lại mở Editor có đồ họa và chạy trên laptop đích. Log phiên hoàn tất: `Logs/Editor-ModelFitResume.log`, `Logs/Editor.log`, `Logs/Editor-ModelFitHeadless.log`.
