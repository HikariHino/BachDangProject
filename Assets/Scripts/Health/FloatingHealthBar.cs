using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Thanh máu lơ lửng trên đầu nhân vật (World-Space Billboard UI).
/// Tự động xoay mặt về camera, hiển thị lượng máu mượt mà với hiệu ứng trễ chip-damage.
/// </summary>
[ExecuteAlways]
public class FloatingHealthBar : MonoBehaviour
{
    [Header("Mục tiêu")]
    [SerializeField] private Health targetHealth;
    [SerializeField] private Vector3 offset = new Vector3(0, 2.2f, 0);
    [SerializeField] private bool autoCalculateOffset = true;
    [SerializeField] private float heightPadding = 0.35f;

    [Header("Thành phần UI")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image delayFillImage;  // Thanh tụt máu từ từ (chip damage vàng/cam)
    [SerializeField] private Image fillImage;       // Thanh máu chính (thanh mau.png đỏ)
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI valueText;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Tùy chọn hiển thị")]
    [SerializeField] private bool billboard = true;
    [SerializeField] private bool showName = false;
    [SerializeField] private bool showNumbers = false;
    [SerializeField] private bool hideWhenFull = false;
    [SerializeField] private bool hideWhenDead = false;
    [SerializeField] private float chipSpeed = 2.5f;
    [SerializeField] private Vector3 baseWorldScale = new Vector3(0.0045f, 0.0045f, 0.0045f);

    private Camera cachedCamera;

    public Health TargetHealth
    {
        get => targetHealth;
        set
        {
            if (targetHealth != null)
            {
                targetHealth.OnHealthChanged -= HandleHealthChanged;
            }
            targetHealth = value;
            if (targetHealth != null)
            {
                targetHealth.OnHealthChanged += HandleHealthChanged;
                if (autoCalculateOffset) CalculateOffset();
                UpdateDisplayInstant();
            }
        }
    }

    public Vector3 Offset
    {
        get => offset;
        set => offset = value;
    }

    private void Awake()
    {
        if (canvas == null) canvas = GetComponent<Canvas>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (targetHealth == null) targetHealth = GetComponentInParent<Health>();
    }

    private void OnEnable()
    {
        if (targetHealth == null)
        {
            targetHealth = GetComponentInParent<Health>();
        }

        if (targetHealth != null)
        {
            targetHealth.OnHealthChanged += HandleHealthChanged;
            if (autoCalculateOffset) CalculateOffset();
            UpdateDisplayInstant();
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorApplication.update -= EditorTick;
            UnityEditor.EditorApplication.update += EditorTick;
        }
#endif
    }

    private void OnDisable()
    {
        if (targetHealth != null)
        {
            targetHealth.OnHealthChanged -= HandleHealthChanged;
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.update -= EditorTick;
#endif
    }

#if UNITY_EDITOR
    private void EditorTick()
    {
        if (this == null || gameObject == null) return;
        if (!Application.isPlaying)
        {
            UpdatePositionAndRotation();
        }
    }
#endif

    public void Setup(Health health, Sprite bgSprite, Sprite fillSprite, float customOffset = -1f)
    {
        targetHealth = health;
        if (targetHealth != null)
        {
            targetHealth.OnHealthChanged -= HandleHealthChanged;
            targetHealth.OnHealthChanged += HandleHealthChanged;
        }

        if (backgroundImage != null && bgSprite != null)
        {
            backgroundImage.sprite = bgSprite;
        }

        if (fillImage != null && fillSprite != null)
        {
            fillImage.sprite = fillSprite;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        }

        if (delayFillImage != null && fillSprite != null)
        {
            delayFillImage.sprite = fillSprite;
            delayFillImage.type = Image.Type.Filled;
            delayFillImage.fillMethod = Image.FillMethod.Horizontal;
            delayFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        }

        if (customOffset > 0f)
        {
            autoCalculateOffset = false;
            offset = new Vector3(0, customOffset, 0);
        }
        else if (autoCalculateOffset)
        {
            CalculateOffset();
        }

        UpdateDisplayInstant();
        UpdatePositionAndRotation();
    }

    public void CalculateOffset()
    {
        if (targetHealth == null) return;

        // Tính đỉnh cao nhất của mesh/model
        float maxY = 0f;
        bool found = false;
        foreach (var r in targetHealth.GetComponentsInChildren<Renderer>())
        {
            if (r is SpriteRenderer || r is CanvasRenderer) continue;
            if (!found) { maxY = r.bounds.max.y; found = true; }
            else { maxY = Mathf.Max(maxY, r.bounds.max.y); }
        }

        if (found)
        {
            float localH = maxY - targetHealth.transform.position.y;
            offset = new Vector3(0, localH + heightPadding, 0);
        }
        else
        {
            offset = new Vector3(0, 2.2f, 0);
        }
    }

    public void UpdatePositionAndRotation()
    {
        if (targetHealth == null)
        {
            targetHealth = GetComponentInParent<Health>();
            if (targetHealth == null) return;
        }

        // Định vị trí world
        transform.position = targetHealth.transform.position + offset;

        // Chuẩn hóa scale theo tỉ lệ cha để kích thước vật lý luôn chuẩn ~1.1m
        if (transform.parent != null)
        {
            Vector3 ps = transform.parent.lossyScale;
            transform.localScale = new Vector3(
                ps.x != 0 ? baseWorldScale.x / ps.x : baseWorldScale.x,
                ps.y != 0 ? baseWorldScale.y / ps.y : baseWorldScale.y,
                ps.z != 0 ? baseWorldScale.z / ps.z : baseWorldScale.z
            );
        }
        else
        {
            transform.localScale = baseWorldScale;
        }

        // Billboard quay mặt về camera
        if (billboard)
        {
            Camera cam = GetActiveCamera();
            if (cam != null)
            {
                transform.rotation = cam.transform.rotation;
            }
        }
    }

    private void LateUpdate()
    {
        UpdatePositionAndRotation();

        // Cập nhật thanh delay chip damage mượt mà
        if (delayFillImage != null && fillImage != null)
        {
            if (delayFillImage.fillAmount > fillImage.fillAmount)
            {
                float delta = Application.isPlaying ? Time.deltaTime : 0.05f;
                delayFillImage.fillAmount = Mathf.Lerp(delayFillImage.fillAmount, fillImage.fillAmount, delta * chipSpeed);
            }
            else
            {
                delayFillImage.fillAmount = fillImage.fillAmount;
            }
        }

        // Ẩn/hiện theo trạng thái máu
        if (canvasGroup != null)
        {
            bool visible = true;
            if (hideWhenDead && targetHealth.IsDead) visible = false;
            if (hideWhenFull && targetHealth.HealthPercent >= 0.999f) visible = false;

            float targetAlpha = visible ? 1f : 0f;
            if (Application.isPlaying)
            {
                canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, Time.deltaTime * 5f);
            }
            else
            {
                canvasGroup.alpha = targetAlpha;
            }
        }
    }

    private Camera GetActiveCamera()
    {
        if (cachedCamera != null && cachedCamera.isActiveAndEnabled) return cachedCamera;

#if UNITY_EDITOR
        if (!Application.isPlaying && UnityEditor.SceneView.lastActiveSceneView != null)
        {
            var svCam = UnityEditor.SceneView.lastActiveSceneView.camera;
            if (svCam != null) return svCam;
        }
#endif

        cachedCamera = Camera.main;
        if (cachedCamera == null) cachedCamera = Camera.current;
        return cachedCamera;
    }

    private void HandleHealthChanged(float current, float max)
    {
        float pct = max > 0 ? Mathf.Clamp01(current / max) : 0f;
        if (fillImage != null) fillImage.fillAmount = pct;
        if (valueText != null) valueText.text = $"{Mathf.CeilToInt(current)}/{Mathf.CeilToInt(max)}";
        if (nameText != null && targetHealth != null) nameText.text = targetHealth.CharacterName;
    }

    public void UpdateDisplayInstant()
    {
        if (targetHealth == null) return;
        float pct = targetHealth.HealthPercent;
        if (fillImage != null) fillImage.fillAmount = pct;
        if (delayFillImage != null) delayFillImage.fillAmount = pct;

        if (nameText != null)
        {
            nameText.gameObject.SetActive(showName);
            nameText.text = targetHealth.CharacterName;
        }

        if (valueText != null)
        {
            valueText.gameObject.SetActive(showNumbers);
            valueText.text = $"{Mathf.CeilToInt(targetHealth.CurrentHealth)}/{Mathf.CeilToInt(targetHealth.MaxHealth)}";
        }
    }

    public void SetShowLabels(bool name, bool numbers)
    {
        showName = name;
        showNumbers = numbers;
        if (nameText != null) nameText.gameObject.SetActive(showName);
        if (valueText != null) valueText.gameObject.SetActive(showNumbers);
    }
}
