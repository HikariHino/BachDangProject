using UnityEngine;
using UnityEngine.UI;

// Hiển thị thanh máu cho một Health target (overlay HUD hoặc world-space).
public class HealthBarUI : MonoBehaviour
{
    public Health target;
    public Slider slider;
    public Image fillImage;
    public bool faceCamera = true;
    public bool overlayMode = true;
    [Tooltip("Tốc độ thanh máu tụt mượt (đơn vị/s)")]
    public float smoothSpeed = 4f;

    private float displayRatio = 1f;

    void Update()
    {
        if (target != null)
        {
            float ratio = target.maxHP > 0 ? (float)target.currentHP / target.maxHP : 0f;
            // Thanh máu tụt dần mượt thay vì giật ngay lập tức
            displayRatio = Mathf.MoveTowards(displayRatio, ratio, smoothSpeed * Time.deltaTime);

            if (slider != null)
            {
                slider.value = displayRatio;
            }

            if (fillImage != null)
            {
                fillImage.fillAmount = displayRatio;

                // Tô màu thanh máu theo tỉ lệ HP: >0.5 xanh lá, 0.2-0.5 vàng, <0.2 đỏ.
                Color fullColor = Color.green;
                Color midColor = Color.yellow;
                Color lowColor = Color.red;
                if (displayRatio > 0.5f)
                {
                    fillImage.color = Color.Lerp(midColor, fullColor, (displayRatio - 0.5f) / 0.5f);
                }
                else if (displayRatio >= 0.2f)
                {
                    fillImage.color = Color.Lerp(lowColor, midColor, (displayRatio - 0.2f) / 0.3f);
                }
                else
                {
                    fillImage.color = lowColor;
                }
            }
        }

        if (!overlayMode && faceCamera && Camera.main != null)
        {
            transform.LookAt(Camera.main.transform);
        }
    }
}
