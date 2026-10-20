# Hướng dẫn Combat (Health, MeleeAttack, HitEffect, HealthBarUI)

Các script combat nằm trong `Assets/Scripts/Combat/`:

- `Health.cs` — máu của quái/nhân vật (`maxHP`, `currentHP`, `TakeDamage`).
- `MeleeAttack.cs` — đánh cận chiến bằng chuột trái, cần `Animator` và `enemyLayer`.
- `EnemyAttack.cs` — giặc tự chém nhân vật trong tầm khi hết cooldown.
- `HitEffect.cs` — hiệu ứng khi trúng đòn.
- `HealthBarUI.cs` — thanh máu HUD trên màn hình (overlay).

## (a) Gắn thanh máu HUD cho nhân vật

1. Chọn nhân vật/object có `Health` trong Hierarchy.
2. Chạy menu **BachDang → Create Health Bar**.
   - Script tạo Canvas mới tên `PlayerHealthBarCanvas` (Render Mode = **Screen Space - Overlay**, CanvasScaler Scale With Screen Size, reference resolution 1920x1080), không làm con của nhân vật.
   - Trên Canvas có Slider `HealthBar` neo góc trên-trái (anchorMin = anchorMax = (0,1), pivot (0,1), anchoredPosition (30,-30), sizeDelta (300,40)), nền tối + fill xanh.
   - Script `HealthBarUI` được gắn trên Canvas, tự nối `Target` = `Health` của object đang chọn (nếu selection không có `Health` thì tự tìm `Health` đầu tiên trong scene và log), `Slider`/`Fill Image` nối sẵn, `Overlay Mode` = true nên không quay về camera.
3. Thanh máu hiển thị trên màn hình; chỉnh `fillImage.color` để đổi màu.

> **Lưu ý:** Nếu trong scene còn `HealthBar` cũ dùng Canvas **World Space** gắn trên đầu object (vd. object `AutoQuestScene` hay con của nhân vật), hãy **xóa thủ công** trong Hierarchy để tránh thanh máu cũ chồng lên HUD mới.

## (b) Tự động setup Animator attack

1. Chọn GameObject có `Animator` của quái/nhân vật trong Hierarchy.
2. Chạy menu **BachDang → Setup Attack Animator**.
   - Script sẽ thêm parameter `Attack` (Trigger), tạo state `Attack` (tự tìm clip tên chứa "attack" trong controller; nếu không thấy sẽ log cảnh báo và để trống motion — kéo clip vào thủ công), nối transition Any State → Attack (condition `Attack`), và transition exit-time từ Attack về state cũ.

## (c) Gắn reference cho MeleeAttack

Trên GameObject có `MeleeAttack`:

- `Animator` = Animator của nhân vật.
- `Enemy Layer` = layer của quái (chọn đúng layer, vd. `Enemy`).
- `Damage`, `Attack Range` chỉnh theo ý muốn.
- Trên quái cần có `Collider` và component `Health`; `HitEffect` (nếu dùng) cũng gắn trên quái để nhận sự kiện trúng đòn.

## (d) Test

1. Nhấn **Play**.
2. Click chuột trái để tấn công — Animator chạy state Attack, quái trong tầm bị trừ máu, thanh máu HUD trên màn hình giảm theo.
3. Kiểm tra Console để xem log HP (`còn X/Y HP`) và cảnh báo (nếu state Attack thiếu motion, clip attack chưa được gán).

## (d2) Thanh máu world-space cho từng giặc

1. Chạy menu **BachDang → Create Enemy Health Bar**.
   - Với mỗi giặc trong scene (tên chứa `Meshy_AI`/`LinhDich`/`Enemy`/`Giac`, trừ `Main_Character`) có `Health`:
     - Xóa `EnemyHealthBar` cũ (nếu có) rồi tạo mới con GameObject `EnemyHealthBar` tại local position (0, 2, 0), localScale (0.01, 0.01, 0.01).
     - Gắn `Canvas` RenderMode **World Space**, sizeDelta (200, 40), `worldCamera` = `Camera.main` (nếu có).
     - Con `Slider` 200x40: nền đen + fill xanh.
     - Gắn `HealthBarUI`: `target` = `Health` của giặc, `slider`/`fillImage` nối sẵn, `Overlay Mode` = false, `Face Camera` = true (thanh máu luôn quay về camera).
   - Log: số lượng thanh máu đã tạo.

## (e) Enemy tự chém nhân vật

1. Chạy menu **BachDang → Setup Enemy Attack**:
   - Tạo layer `Player` và `Enemy` nếu thiếu.
   - Set `Main_Character` + children sang layer `Player`.
   - Với mỗi enemy (tên chứa `Meshy_AI`/`LinhDich`/`Enemy`/`Giac`, khác `Main_Character`): thêm `Health` + `EnemyAttack` nếu thiếu, gán `animator` = Animator con, `playerLayer` = layer `Player`, enemy giữ layer `Enemy`.
2. Test: **Play**, cho nhân vật đến gần enemy trong tầm ~1.8f phía trước — giặc tự chém (trigger `Attack`, trừ 15 HP, log Console). Khi `Health` của nhân vật/enemy về 0 — object gục ngã: tắt Animator/script di chuyển, `Rigidbody` → `isKinematic = true`, `useGravity = false`, vận tốc = 0 (không rơi tự do), Slerp quay `Quaternion.Euler(0, 0, 90f)` quanh trục forward (local Z) để ngã sang một bên trong ~0.8s, chờ 1.5s, rồi chìm xuống (`position -= Vector3.up * 0.5 * dt` trong 1s) và Destroy. Log: `gục ngã và biến mất`.

## (f) Troubleshooting

- **Thanh máu dính vào kiếm/khiên/vũ khí:** chạy menu **BachDang → Clean Combat Setup** — script sẽ xóa `Health`/`EnemyAttack`/`MeleeAttack`/`HitEffect` khỏi mọi object tên chứa `Sword`/`Shield`/`Bow`/`Weapon`/`Sword_`/`Shield_` hoặc nằm trong Main_Character, đồng thời xóa child `EnemyHealthBar`/`PlayerHealthBarCanvas` của chúng, và log tiếng Việt tên object bị dọn.
- **Đi xuyên qua giặc:** chạy menu **BachDang → Fix Combat Colliders** để thêm `CapsuleCollider`/`SphereCollider` + `Rigidbody` (freezeRotation) cho nhân vật/giặc và `MeshCollider`/`BoxCollider` cho mặt đất. Giờ menu này: kích thước `CapsuleCollider` tự tính theo bounds của các `Renderer` con (height = bounds.size.y, radius = max(size.x, size.z)/4, center theo bounds); nếu nhân vật đã có `CapsuleCollider` thì resize lại thay vì bỏ qua; `Rigidbody` chỉ gắn vào object root khớp pattern, không gắn lên bone; object tên chứa `Sword`/`Shield`/`Weapon`/`Bow`/`WeaponHolder` bị loại trừ, không được gắn collider/Rigidbody.
- **Giặc không chém được:** kiểm tra `playerLayer` của `EnemyAttack` chỉ gồm layer **Player** (không gộp Enemy).
- **Gục ngã xuyên plane:** đã sửa `DieRoutine` — không tắt collider nữa, thân giữ va chạm với mặt đất.
