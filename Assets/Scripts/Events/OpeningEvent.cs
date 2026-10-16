using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.UI;

/// <summary>
/// Đoạn dẫn truyện mở đầu "Năm 938...".
/// Có thể bỏ qua bằng Space, Enter hoặc Esc; E được dành riêng cho tương tác trong game.
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
        // The scene serializes the first line as a preview/default value, and
        // PlayOpening writes the same line again. Clear the preview immediately
        // so the year is only rendered by the timed intro sequence.
        if (storyText != null)
            storyText.text = string.Empty;
        player = FindPlayerInScene();
        DisableDuplicatePlayers(player);
        // Tắt player ngay lập tức để không bị nhìn thấy trước khi opening kết thúc
        if (player != null)
            player.SetActive(false);
    }

    /// <summary>
    /// Tìm chính xác đối tượng nhân vật chính trong Scene, kể cả khi đối tượng đang bị tắt (Inactive)
    /// </summary>
    public static GameObject FindPlayerInScene()
    {
        // Chọn object thật sự nhận input di chuyển. Tìm cả object inactive vì intro
        // chủ động ẩn người chơi cho tới khi kết thúc.
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.isLoaded)
        {
            GameObject best = null;
            int bestScore = int.MinValue;
            foreach (var root in scene.GetRootGameObjects())
            {
                var allTransforms = root.GetComponentsInChildren<Transform>(true);
                foreach (var t in allTransforms)
                {
                    var movement = t.GetComponent<Character_Movement>();
                    if (movement != null)
                    {
                        int score = 100 + (t.gameObject.activeInHierarchy ? 10 : 0);
                        if (t.name == "Player_Main_Animated") score += 5;
                        if (score > bestScore)
                        {
                            best = t.gameObject;
                            bestScore = score;
                        }
                    }
                }
            }
            if (best != null) return best;

            // Fallback cho scene chưa gắn Character_Movement.
            foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == "Player_Main_Animated" || t.name == "Main_Character")
                    return t.gameObject;
        }
        return null;
    }

    private static void DisableDuplicatePlayers(GameObject chosenPlayer)
    {
        if (chosenPlayer == null) return;
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        foreach (var root in scene.GetRootGameObjects())
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.gameObject == chosenPlayer) continue;
            bool isPlayerAncestor = chosenPlayer.transform.IsChildOf(t);
            if (!isPlayerAncestor && (t.name == "Player_Main_Animated" || t.name == "Main_Character"))
                t.gameObject.SetActive(false);
            var otherMovement = t.GetComponent<Character_Movement>();
            if (otherMovement != null) otherMovement.enabled = false;
        }
    }

    private float _openingStartTime;

    void Start()
    {
        _openingStartTime = Time.time;

        // Tìm opening canvas từ openingPanel
        if (openingPanel != null)
        {
            _openingCanvas = openingPanel.GetComponentInParent<Canvas>();
        }
        
        // Fallback nếu không có
        if (_openingCanvas == null) _openingCanvas = GetComponentInParent<Canvas>();
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
            // Bỏ qua opening canvas
            if (c != null && _openingCanvas != null && c.gameObject == _openingCanvas.gameObject)
                continue;
                
            if (c != null)
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
        Canvas myCanvas = _openingCanvas;
        if (myCanvas == null && openingPanel != null)
            myCanvas = openingPanel.GetComponentInParent<Canvas>();
        if (myCanvas == null) myCanvas = GetComponentInParent<Canvas>();
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
        foreach (Canvas c in hiddenCanvases)
        {
            if (c != null && c != myCanvas)
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

            // E được để dành cho tương tác trong game, không dùng để bỏ qua intro.
            // Bỏ GetMouseButtonDown(0) để tránh click chuột vào màn hình Game gây skip luôn
            // Thêm điều kiện Time.time > _openingStartTime + 1f để tránh bấm nhầm lúc vừa load scene
            if (Time.time > _openingStartTime + 1f)
            {
                if (Input.GetKeyDown(KeyCode.Space) ||
                    Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
                    Input.GetKeyDown(KeyCode.Escape))
                {
                    SkipOpening();
                }
            }
        }
    }

    private Canvas _openingCanvas;

    // Bootstrap can create quest UI after this component's Start has run.
    public void RefreshHiddenUI()
    {
        if (!IsOpeningActive || isOpeningEnded) return;
        if (_openingCanvas == null && openingPanel != null)
            _openingCanvas = openingPanel.GetComponentInParent<Canvas>();
        SuppressQuestUI();
    }

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
                storyText.text = storyLines[i] + "\n\n<size=15><color=#888888>[Nhấn Space / Enter để bỏ qua]</color></size>";
                
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
            
        player = FindPlayerInScene();

        if (player != null)
        {
            DisableDuplicatePlayers(player);
            player.SetActive(true);
            foreach (var movement in player.GetComponentsInChildren<Character_Movement>(true))
                movement.enabled = true;

            // Đảm bảo scale nhân vật đúng 7,7,7
            player.transform.localScale = new Vector3(7f, 7f, 7f);

            // Dùng animation controller có idle thường, không giữ tư thế cầm vũ khí.
            var animators = player.GetComponentsInChildren<Animator>(true);
            foreach (var anim in animators)
            {
                anim.enabled = true;
                anim.applyRootMotion = false;
                
                var ctrl = Resources.Load<RuntimeAnimatorController>("Player_Neutral");
                if (ctrl != null)
                {
                    anim.runtimeAnimatorController = ctrl;
                }
                anim.SetBool("isWalking", false);
                anim.SetBool("isRunning", false);
            }

            // Xác định chính xác độ cao mặt đất tại toạ độ mới
            Vector3 targetPos = new Vector3(-992.8307f, 17.42352f, 265.8618f);
            var playerColliders = player.GetComponentsInChildren<Collider>(true);
            var colliderStates = new bool[playerColliders.Length];
            for (int i = 0; i < playerColliders.Length; i++)
            {
                colliderStates[i] = playerColliders[i].enabled;
                playerColliders[i].enabled = false;
            }
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(targetPos.x, targetPos.y + 60f, targetPos.z), Vector3.down, out hit, 150f))
            {
                targetPos.y = hit.point.y + 0.1f;
            }
            else if (Terrain.activeTerrain != null)
            {
                targetPos.y = Terrain.activeTerrain.SampleHeight(targetPos) + Terrain.activeTerrain.transform.position.y + 0.1f;
            }
            for (int i = 0; i < playerColliders.Length; i++)
                playerColliders[i].enabled = colliderStates[i];

            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            var rb = player.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.constraints = RigidbodyConstraints.FreezeRotationX |
                                 RigidbodyConstraints.FreezeRotationY |
                                 RigidbodyConstraints.FreezeRotationZ;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.position = targetPos;
            }

            player.transform.position = targetPos;
            player.transform.rotation = Quaternion.Euler(0f, 110f, 0f);

            if (cc != null) cc.enabled = true;

            SetupPlayerCamera(player.transform);
        }
            
        // Bật lại các UI Canvas đã giấu
        if (hiddenCanvases != null)
        {
            foreach (Canvas c in hiddenCanvases)
            {
                if (c != null) c.enabled = true;
            }
        }

        // HideExistingQuestPanels disables the whole story canvas during the
        // intro. Re-enable its GameObject after the intro so the objective HUD
        // and interaction prompt can appear again.
        var storyCanvas = GameObject.Find("StoryUI_Canvas");
        if (storyCanvas != null)
            storyCanvas.SetActive(true);

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

    public static void SetupPlayerCamera(Transform target)
    {
        if (target == null) return;
        var cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
        Camera mainCamera = null;
        foreach (var cam in cameras)
            if (cam != null && cam.gameObject.name == "Main Camera" && cam.gameObject.activeInHierarchy)
            { mainCamera = cam; break; }
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null)
            foreach (var cam in cameras)
                if (cam != null && cam.enabled && cam.gameObject.activeInHierarchy && cam.CompareTag("MainCamera"))
                { mainCamera = cam; break; }
        if (mainCamera == null)
            foreach (var cam in cameras)
                if (cam != null && cam.enabled && cam.gameObject.activeInHierarchy) { mainCamera = cam; break; }
        if (mainCamera == null) return;

        foreach (var cam in cameras)
            if (cam != null && cam != mainCamera) cam.enabled = false;
        mainCamera.enabled = true;

        // Chỉ để một script điều khiển transform camera. Camera_Script có góc
        // over-the-shoulder phù hợp với điều khiển WASD kiểu PUBG.
        foreach (var driver in Object.FindObjectsByType<Camera_Script>(FindObjectsInactive.Include))
            if (driver != null) driver.enabled = driver.gameObject == mainCamera.gameObject;
        foreach (var driver in Object.FindObjectsByType<ThirdPersonCamera>(FindObjectsInactive.Include))
            if (driver != null) driver.enabled = false;
        foreach (var driver in Object.FindObjectsByType<SimpleCameraFollow>(FindObjectsInactive.Include))
            if (driver != null) driver.enabled = false;

        var mainDriver = mainCamera.GetComponent<Camera_Script>();
        if (mainDriver == null) mainDriver = mainCamera.gameObject.AddComponent<Camera_Script>();
        mainDriver.target = target;
    }
}
