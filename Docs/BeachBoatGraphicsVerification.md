# Kiểm tra mở beachBoat có đồ họa — 15/10/2026

Lần kiểm tra này mở Unity 6000.6.0f1 với `-force-d3d11`, tải beachBoat và vào Play thành công trên RTX 4060 Ti/RAM 32 GB. API thực tế xác nhận là Direct3D11. Không thay đổi map, ProjectSettings hoặc driver.

## Kết quả

- 43 kiểm tra nhiệm vụ qua trong lượt Play mới: hội thoại, đi cổng đến làng, nhặt gỗ, trở về, đến bãi cọc, hoàn tất và báo cáo; đổi F9, di chuyển/nhảy.
- Ảnh Game View tại doanh trại hiển thị model và chữ tiếng Việt bình thường: `.utmp/GraphicsCheck/camp-dx11.png`.
- 6 mẫu RAM tiến trình cách nhau 5 giây trong Play: working set 4.286–4.289 GiB; private bytes 4.956–4.993 GiB. Đây là khoảng lấy mẫu, không phải đỉnh RAM lúc khởi động hoặc cam kết chạy được trên máy 16 GB.
- Bộ đếm Unity tại một thời điểm Play: allocated 1.887,96 MiB, reserved 3.885,76 MiB; mesh đã nạp 677,71 MiB, texture đã nạp 676,22 MiB. Không cộng các bộ đếm này vào RAM tiến trình.
- Terrain 49.547 cây; Nature Renderer tree streaming vẫn tắt, treeDistance 1.800.
- Dừng Play thành công, scene không dirty, không lỗi biên dịch; Console tại cuối lượt có 0 error và 2 warning BoxCollider có scale âm ở các model đã có. Không sửa/saved lại scene trong lượt này.

Kết quả xác nhận có thể chạy lại bản scene đã chỉnh trên máy hiện tại. Chưa tái hiện crash để xác định nguyên nhân gốc; Direct3D11 cũng đã xuất hiện trong một lần lỗi trước đó, nên không khẳng định tùy chọn này chữa dứt điểm lỗi NVIDIA. Chưa kiểm tra laptop Nitro 16 GB/RTX 3050 hoặc xuất build.

## Mở lại bằng cùng tùy chọn

Từ PowerShell tại thư mục dự án:

```powershell
.\Tools\OpenUnityDX11.ps1
```

Script tìm đúng phiên bản qua Unity CLI nếu đã cài, sau đó thử vị trí mặc định Unity Hub. Nếu Unity nằm chỗ khác, truyền đường dẫn thật trên máy đó:

```powershell
.\Tools\OpenUnityDX11.ps1 -EditorPath 'D:\Unity\6000.6.0f1\Editor\Unity.exe'
```

Dùng `-CheckOnly` để chỉ xem lệnh, không mở Editor. Lưu và đóng Editor hiện tại trước khi chạy. Script không cài Unity, không thay đổi chính sách PowerShell, không sửa cấu hình đồ họa và không nhân thêm tỷ lệ model. Log mỗi lần mở nằm trong `Logs/Editor-DX11-*.log`.

Đã kiểm tra `-CheckOnly` tìm đúng Editor tại máy hiện tại và nhánh chặn mở trùng khi Editor đang chạy. Phiên GUI kiểm tra được khởi động trực tiếp bằng cùng các tham số; chưa kiểm tra tự tìm Editor trên laptop.

Unity mô tả tùy chọn ép Editor dùng Direct3D11 tại [Editor command-line arguments](https://docs.unity3d.com/6000.0/Documentation/Manual/EditorCommandLineArguments.html#gfx-api-arguments).

Log kiểm tra: `Logs/Editor-DX11-Verification.log`. Dữ liệu nhiệm vụ/RAM: `.utmp/GraphicsCheck` và `.utmp/MapMemory/20261015-153355-DX11-play.json` (thư mục tạm không đưa vào Git).
