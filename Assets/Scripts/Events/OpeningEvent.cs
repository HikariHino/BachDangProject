using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.UI;

/// <summary>
/// Đoạn dẫn truyện mở đầu "Năm 938...".
/// Đã hỗ trợ BỎ QUA TỨC THÌ (bấm Space / E / Enter / Click) để người chơi không phải chờ đợi!
/// Tự động ẩn các UI nhiệm vụ cũ trong lúc đang chạy intro để không bị lỗi chữ "Chưa có nhiệm vụ nào".
/// </summary>
public class OpeningEvent : MonoBehaviour
{
    [Header("UI")]
    public GameObject openingPanel;
    public TMP_Text storyText;

    [Header("Player")]
    public GameObject player;

    public static bool IsOpeningActive { get; private set; } = true;

    private Canvas[] hiddenCanvases;
    private MonoBehaviour[] hiddenScripts;
    private GameObject[] hiddenQuestPanels;
    private Coroutine openingCoroutine;
    private bool isOpeningEnded = false;

    public bool IsOpeningEnded => isOpeningEnded;

    private string[] storyLines =
    {
        "Năm 938.",
        "Sau nhiều năm đất nước chìm trong thời kỳ Bắc thuộc, vùng đất Giao Châu vẫn đang đứng trước những biến động lớn.",
        "Nhà Nam Hán ở phương Bắc đang tìm cách đưa quân tiến xuống Giao Châu, với ý định giành lại quyền kiểm soát vùng đất này.",
        "Trước nguy cơ xâm lược, Ngô Quyền tập hợp lực lượng, chuẩn bị chống lại quân Nam Hán.",
        "Ngô Quyền lựa chọn sông Bạch Đằng làm nơi quyết chiến.",
        "Tại đây, quân ta chuẩn bị một kế sách dựa vào địa hình và thủy triều của dòng sông.",
        "Những cọc gỗ được đẽo nhọn và bố trí dưới lòng sông, chờ thời cơ thích hợp.",
        "Nhưng trước khi trận chiến bắt đầu..."
    };

    void Awake()
    {
        IsOpeningActive = true;
        if (player == null)
        {
            player = FindPlayerInScene();
        }
        // Tắt player ngay lập tức để không bị nhìn thấy trước khi opening kết thúc
        if (player != null)
            player.SetActive(false);
    }

    /// <summary>
    /// Tìm chính xác đối tượng nhân vật chính trong Scene, kể cả khi đối tượng đang bị tắt (Inactive)
    /// </summary>
    public static GameObject FindPlayerInScene()
    {
        // 1. Thử tìm thông thường nếu đang active
        var p = GameObject.Find("Player_Main_Animated") ?? GameObject.Find("Main_Character");
        if (p != null) return p;

        try
        {
            var tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null && !tagged.name.Contains("Camera")) return tagged;
        }
        catch { }

        // 2. Quét sâu toàn bộ Scene kể cả INACTIVE GameObject
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.isLoaded)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var allTransforms = root.GetComponentsInChildren<Transform>(true);
                foreach (var t in allTransforms)
                {
                    if (t.gameObject.name == "Player_Main_Animated" || 
                        t.gameObject.name == "Main_Character" || 
                        (t.gameObject.CompareTag("Player") && !t.gameObject.name.Contains("Camera")))
                    {
                        return t.gameObject;
                    }
                }
            }
        }

        // 3. Fallback theo component Character_Movement
        var cm = Object.FindAnyObjectByType<Character_Movement>(FindObjectsInactive.Include);
        if (cm != null) return cm.gameObject;

        return null;
    }

    void Start()
    {
        // Tìm opening canvas ngay
        _openingCanvas = GetComponentInParent<Canvas>();
        if (_openingCanvas == null) _openingCanvas = GetComponent<Canvas>();
        if (_openingCanvas != null)
        {
            _openingCanvas.sortingOrder = 9999;
            _openingCanvas.enabled = true;
        }

        // Tắt TẤT CẢ canvas ngay lập tức (trước cả coroutine delay)
        var allCanvasNow = FindObjectsOfType<Canvas>(true);
        foreach (Canvas c in allCanvasNow)
        {
            if (c != null && c != _openingCanvas)
                c.enabled = false;
        }

        StartCoroutine(InitOpening());
    }

    IEnumerator InitOpening()
    {
        // Delay 3 frame để Bootstrap kịp tạo StoryUI_Canvas và tất cả UI khác
        yield return null;
        yield return null;
        yield return null;

        if (player == null)
        {
            player = FindPlayerInScene();
        }

        // Đảm bảo Canvas của đoạn mở đầu luôn ở tầng cao nhất (đè hoàn toàn tất cả UI khác)
        Canvas myCanvas = GetComponentInParent<Canvas>();
        if (myCanvas == null) myCanvas = GetComponent<Canvas>();
        if (myCanvas != null)
        {
            myCanvas.sortingOrder = 9999; // Đảm bảo luôn đè lên mọi UI
            myCanvas.enabled = true;
        }
        _openingCanvas = myCanvas;

        // Tạm ẩn các bảng nhiệm vụ cũ để màn hình đen hoàn toàn sạch sẽ
        HideExistingQuestPanels();

        // Ẩn tất cả Canvas UI không liên quan (kể cả StoryUI_Canvas do Bootstrap tạo sau frame 1)
        hiddenCanvases = FindObjectsOfType<Canvas>(true);
        string openingCanvasName = myCanvas != null ? myCanvas.gameObject.name : "Canvas_Opening";
        foreach (Canvas c in hiddenCanvases)
        {
            if (c != null && c.gameObject.name != openingCanvasName)
            {
                c.enabled = false;
            }
        }

        // Ẩn luôn các UI OnGUI cứng đầu (như Thủy triều, SaBanHUD)
        hiddenScripts = FindObjectsOfType<MonoBehaviour>();
        foreach (MonoBehaviour mb in hiddenScripts)
        {
            if (mb != null)
            {
                string typeName = mb.GetType().Name;
                if (typeName == "SaBanHUD" || typeName == "TideSystem")
                {
                    mb.enabled = false;
                }
            }
        }

        if (openingPanel != null)
            openingPanel.SetActive(true);
            
        if (storyText != null)
        {
            storyText.fontStyle = FontStyles.Italic;
            storyText.fontSize = 28;
            storyText.color = new Color(storyText.color.r, storyText.color.g, storyText.color.b, 0);
        }

        if (player != null)
            player.SetActive(false);

        openingCoroutine = StartCoroutine(PlayOpening());
    }

    void Update()
    {
        if (!isOpeningEnded)
        {
            // Đảm bảo trong suốt đoạn mở đầu, bảng nhiệm vụ không bao giờ bị kích hoạt lại
            SuppressQuestUI();

            // Cho phép người chơi BẤM PHÍM BẤT KỲ ĐỂ BỎ QUA ĐOẠN DẪN TRUYỆN MỞ ĐẦU
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E) || 
                Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
                Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(0))
            {
                SkipOpening();
            }
        }
    }

    private Canvas _openingCanvas;

    private void SuppressQuestUI()
    {
        // Tìm opening canvas nếu chưa có
        if (_openingCanvas == null)
        {
            _openingCanvas = GetComponentInParent<Canvas>();
            if (_openingCanvas == null) _openingCanvas = GetComponent<Canvas>();
        }

        // Scan TẤT CẢ canvas trong scene mỗi frame, tắt hết trừ opening canvas
        // (bắt cả canvas được tạo dynamically bởi Bootstrap sau Start)
        var allCanvases = FindObjectsOfType<Canvas>(true);
        foreach (Canvas c in allCanvases)
        {
            if (c != null && c != _openingCanvas && c.enabled)
            {
                c.enabled = false;
            }
        }

        // Tắt thêm objectiveUIPanel nếu có
        if (BachDangStoryManager.Instance != null && BachDangStoryManager.Instance.objectiveUIPanel != null)
        {
            if (BachDangStoryManager.Instance.objectiveUIPanel.activeSelf)
                BachDangStoryManager.Instance.objectiveUIPanel.SetActive(false);
        }
    }

    private void HideExistingQuestPanels()
    {
        string[] panelNames = { "QuestPanel", "QuestUIPanel", "QuestDesc", "QuestTitle", "NhiemVuPanel", "StoryUI_Canvas", "ObjectivePanel" };
        var found = new System.Collections.Generic.List<GameObject>();
        foreach (string name in panelNames)
        {
            GameObject obj = GameObject.Find(name);
            if (obj != null)
            {
                obj.SetActive(false);
                found.Add(obj);
            }
        }
        hiddenQuestPanels = found.ToArray();
    }

    public void SkipOpening()
    {
        if (isOpeningEnded) return;

        if (openingCoroutine != null)
        {
            StopCoroutine(openingCoroutine);
        }

        EndOpening();
    }

    IEnumerator PlayOpening()
    {
        yield return new WaitForSeconds(0.3f);

        for (int i = 0; i < storyLines.Length; i++)
        {
            if (storyText != null)
            {
                storyText.text = storyLines[i] + "\n\n<size=15><color=#888888>[Nhấn Space hoặc E để vào game ngay]</color></size>";
                
                // Fade in (0.5s)
                yield return StartCoroutine(FadeText(0f, 1f, 0.5f));
                
                // Thời gian đọc vừa phải (từ 1.8s đến 3.2s tùy độ dài câu)
                float waitTime = Mathf.Clamp(storyLines[i].Length * 0.04f, 1.8f, 3.2f);
                yield return new WaitForSeconds(waitTime);
                
                // Fade out (0.4s)
                yield return StartCoroutine(FadeText(1f, 0f, 0.4f));
                
                yield return new WaitForSeconds(0.15f);
            }
        }

        // Fade out màn hình đen nhanh gọn (0.5s) ra cảnh game
        if (openingPanel != null)
        {
            Image bgImage = openingPanel.GetComponent<Image>();
            if (bgImage != null)
            {
                float t = 0;
                Color startColor = bgImage.color;
                while (t < 0.5f)
                {
                    t += Time.deltaTime;
                    bgImage.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(1f, 0f, t / 0.5f));
                    yield return null;
                }
            }
        }

        EndOpening();
    }

    IEnumerator FadeText(float startAlpha, float endAlpha, float duration)
    {
        if (storyText == null) yield break;

        float t = 0;
        Color c = storyText.color;
        while (t < duration)
        {
            t += Time.deltaTime;
            storyText.color = new Color(c.r, c.g, c.b, Mathf.Lerp(startAlpha, endAlpha, t / duration));
            yield return null;
        }
        storyText.color = new Color(c.r, c.g, c.b, endAlpha);
    }

    void EndOpening()
    {
        if (isOpeningEnded) return;
        isOpeningEnded = true;
        IsOpeningActive = false;

        if (openingPanel != null)
            openingPanel.SetActive(false);
            
        if (player == null)
        {
            player = FindPlayerInScene();
        }

        if (player != null)
        {
            player.SetActive(true);

            // Đảm bảo scale nhân vật đúng 7,7,7
            player.transform.localScale = new Vector3(7f, 7f, 7f);

            // === FIX T-POSE: đảm bảo Animator được enable và có đúng controller ===
            var animators = player.GetComponentsInChildren<Animator>(true);
            foreach (var anim in animators)
            {
                anim.enabled = true;
                if (anim.runtimeAnimatorController == null)
                {
                    // Tìm Sword_Anima controller trong Resources hoặc project
                    var ctrl = Resources.Load<RuntimeAnimatorController>("Sword_Anima");
                    if (ctrl == null)
                        ctrl = Resources.Load<RuntimeAnimatorController>("Main_Character/Animation/Sword_Anima");
                    if (ctrl != null)
                    {
                        anim.runtimeAnimatorController = ctrl;
                        Debug.Log("[OpeningEvent] Đã gán Sword_Anima controller cho " + anim.gameObject.name);
                    }
                    else
                    {
                        Debug.LogWarning("[OpeningEvent] Không tìm thấy Sword_Anima trong Resources. Vui lòng gán thủ công trong Inspector.");
                    }
                }
                anim.applyRootMotion = false;
            }

            // Xác định chính xác độ cao mặt đất tại toạ độ x: -1200, z: 340 (tránh bị lún đất hoặc rơi khỏi map)
            Vector3 targetPos = new Vector3(-1200f, 40f, 340f);
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(targetPos.x, targetPos.y + 60f, targetPos.z), Vector3.down, out hit, 150f))
            {
                targetPos.y = hit.point.y + 0.1f;
            }
            else if (Terrain.activeTerrain != null)
            {
                targetPos.y = Terrain.activeTerrain.SampleHeight(targetPos) + Terrain.activeTerrain.transform.position.y + 0.1f;
            }

            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            var rb = player.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.position = targetPos;
            }

            player.transform.position = targetPos;
            player.transform.rotation = Quaternion.Euler(0f, 110f, 0f);

            if (cc != null) cc.enabled = true;

            // Đảm bảo tất cả Camera_Script đều bám theo nhân vật chính
            var cameras = Object.FindObjectsByType<Camera_Script>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var cam in cameras)
            {
                if (cam != null)
                {
                    cam.target = player.transform;
                }
            }
        }
            
        // Bật lại các UI Canvas đã giấu
        if (hiddenCanvases != null)
        {
            foreach (Canvas c in hiddenCanvases)
            {
                if (c != null) c.enabled = true;
            }
        }

        // Bật lại các Script OnGUI
        if (hiddenScripts != null)
        {
            foreach (MonoBehaviour mb in hiddenScripts)
            {
                if (mb != null) mb.enabled = true;
            }
        }

        // Bật lại UI nhiệm vụ Bạch Đằng
        if (BachDangStoryManager.Instance != null)
        {
            BachDangStoryManager.Instance.UpdateUI();
        }
    }
}