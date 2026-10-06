# BachDangProject

> Ghi chú: thanh máu địch có thể chỉnh scale trong EnemyHealthBar nếu quá to.

> **Redesign thanh máu (10/2026):**
> - `HealthBarUI.cs`: fill tự đổi màu theo tỉ lệ HP — > 0.5 xanh lá, 0.2–0.5 vàng, < 0.2 đỏ (dùng `Color.Lerp`).
> - Menu **BachDang → Redesign Player Health Bar**: tạo lại HUD máu player (container 400x40 ở top-left (30,-30), nền đen trong suốt, Border xám, Fill xanh pivot trái, Text "HP" góc trái), `HealthBarUI` nối `Health` của Main_Character, `overlayMode = true`.
> - Menu **BachDang → Redesign Enemy Health Bars**: tạo lại thanh máu giặc nhỏ gọn (300x50, localPosition (0,2.2,0), localScale 0.004, WorldSpace, fill xanh pivot trái, không text, `faceCamera = true`).