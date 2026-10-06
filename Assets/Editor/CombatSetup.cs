using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Tiện ích Editor: tự thêm parameter "Attack", state Attack và transition
// vào AnimatorController của Animator đang được chọn.
public static class CombatSetup
{
    [MenuItem("BachDang/Setup Attack Animator")]
    public static void SetupAttackAnimator()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            Debug.LogWarning("BachDang/Setup Attack Animator: chưa chọn GameObject nào trong Hierarchy.");
            return;
        }

        Animator animator = selected.GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogWarning($"BachDang/Setup Attack Animator: GameObject '{selected.name}' không có Animator.");
            return;
        }

        AnimatorController controller = animator.runtimeAnimatorController as AnimatorController;
        if (controller == null)
        {
            Debug.LogWarning("BachDang/Setup Attack Animator: Animator không dùng AnimatorController " +
                             "(có thể là AnimatorOverrideController). Vui lòng dùng controller gốc.");
            return;
        }

        // 1) Thêm parameter "Attack" dạng Trigger nếu chưa có.
        bool hasAttackParam = false;
        foreach (AnimatorControllerParameter p in controller.parameters)
        {
            if (p.name == "Attack" && p.type == AnimatorControllerParameterType.Trigger)
            {
                hasAttackParam = true;
                break;
            }
        }
        if (!hasAttackParam)
        {
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            Debug.Log("Đã thêm parameter 'Attack' (Trigger).");
        }

        AnimatorStateMachine machine = controller.layers[0].stateMachine;

        // State trước Attack sẽ quay về (mặc định là default state).
        AnimatorState previousState = machine.defaultState;

        // 2) Tìm state Attack đã có chưa; nếu chưa thì tạo mới.
        AnimatorState attackState = null;
        foreach (ChildAnimatorState child in machine.states)
        {
            if (child.state != null && child.state.name == "Attack")
            {
                attackState = child.state;
                break;
            }
        }

        if (attackState == null)
        {
            attackState = machine.AddState("Attack");

            // Tìm animation clip đầu tiên có tên chứa "attack" trong cùng controller.
            AnimationClip attackClip = FindAttackClip(machine);
            if (attackClip != null)
            {
                attackState.motion = attackClip;
                Debug.Log($"Đã gán clip '{attackClip.name}' cho state Attack.");
            }
            else
            {
                Debug.LogWarning("Không tìm thấy animation clip nào có tên chứa 'attack' trong controller. " +
                                 "State Attack để trống motion — hãy kéo clip vào thủ công.");
            }
        }

        // 3) Transition từ Any State sang Attack với condition Attack (nếu chưa có).
        bool hasAnyToAttack = false;
        foreach (AnimatorStateTransition t in machine.anyStateTransitions)
        {
            if (t.destinationState == attackState)
            {
                hasAnyToAttack = true;
                break;
            }
        }
        if (!hasAnyToAttack)
        {
            AnimatorStateTransition toAttack = machine.AddAnyStateTransition(attackState);
            toAttack.hasExitTime = false;
            toAttack.duration = 0.1f;
            toAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");
        }

        // 4) Transition exit-time từ Attack về state trước (nếu chưa có).
        if (previousState != null && previousState != attackState)
        {
            bool hasBack = false;
            foreach (AnimatorStateTransition t in attackState.transitions)
            {
                if (t.destinationState == previousState && t.hasExitTime)
                {
                    hasBack = true;
                    break;
                }
            }
            if (!hasBack)
            {
                AnimatorStateTransition back = attackState.AddTransition(previousState);
                back.hasExitTime = true;
                back.exitTime = 0.9f;
                back.duration = 0.1f;
                back.canTransitionToSelf = false;
            }
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log($"BachDang/Setup Attack Animator: hoàn tất cho controller '{controller.name}'.");
    }

    [MenuItem("BachDang/Create Health Bar")]
    public static void CreateHealthBar()
    {
        GameObject selected = Selection.activeGameObject;
        Health health = null;
        if (selected != null)
        {
            health = selected.GetComponent<Health>();
        }
        if (health == null)
        {
            Debug.Log("BachDang/Create Health Bar: object đang chọn không có Health, tự tìm Health đầu tiên trong scene.");
            health = Object.FindFirstObjectByType<Health>();
            if (health == null)
            {
                Debug.LogError("BachDang/Create Health Bar: không tìm thấy component Health nào trong scene. Hủy bỏ.");
                return;
            }
            Debug.Log($"BachDang/Create Health Bar: dùng Health trên '{health.gameObject.name}'.");
        }

        // Tạo Canvas HUD (Screen Space - Overlay) mới trong scene, KHÔNG làm con của nhân vật.
        GameObject canvasObj = new GameObject("PlayerHealthBarCanvas", typeof(RectTransform));

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        // Slider "HealthBar" anchored top-left.
        GameObject sliderObj = new GameObject("HealthBar", typeof(RectTransform));
        sliderObj.transform.SetParent(canvasObj.transform, false);
        RectTransform sliderRect = sliderObj.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0f, 1f);
        sliderRect.anchorMax = new Vector2(0f, 1f);
        sliderRect.pivot = new Vector2(0f, 1f);
        sliderRect.anchoredPosition = new Vector2(30f, -30f);
        sliderRect.sizeDelta = new Vector2(300f, 40f);

        Image backgroundImage = sliderObj.AddComponent<Image>();
        backgroundImage.color = new Color(0f, 0f, 0f, 0.5f); // nền tối

        Slider slider = sliderObj.AddComponent<Slider>();
        slider.targetGraphic = backgroundImage;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;

        GameObject fillAreaObj = new GameObject("Fill Area", typeof(RectTransform));
        fillAreaObj.transform.SetParent(sliderObj.transform, false);
        RectTransform fillAreaRect = fillAreaObj.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = Vector2.zero;
        fillAreaRect.offsetMax = Vector2.zero;

        GameObject fillObj = new GameObject("Fill", typeof(RectTransform));
        fillObj.transform.SetParent(fillAreaObj.transform, false);
        RectTransform fillRect = fillObj.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        Image fillImage = fillObj.AddComponent<Image>();
        fillImage.color = Color.green; // thanh máu màu xanh
        slider.fillRect = fillRect;

        // HealthBarUI gắn trên Canvas: target và slider/fillImage nối sẵn, overlayMode = true.
        HealthBarUI healthBarUI = canvasObj.AddComponent<HealthBarUI>();
        healthBarUI.target = health;
        healthBarUI.slider = slider;
        healthBarUI.fillImage = fillImage;
        healthBarUI.overlayMode = true;

        // Đảm bảo có EventSystem trong scene.
        EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<EventSystem>();
            eventSystemObj.AddComponent<StandaloneInputModule>();
            Debug.Log("Đã tạo mới EventSystem trong scene.");
        }

        Undo.RegisterCreatedObjectUndo(canvasObj, "Create Health Bar");
        Selection.activeGameObject = canvasObj;
        Debug.Log($"BachDang/Create Health Bar: hoàn tất! Đã tạo 'PlayerHealthBarCanvas' (Screen Space - Overlay) với Slider 'HealthBar' + HealthBarUI, target = Health của '{health.gameObject.name}'. " +
                  "Thanh máu hiển thị trên màn hình (HUD), không còn world-space.");
    }

    [MenuItem("BachDang/Redesign Player Health Bar")]
    public static void RedesignPlayerHealthBar()
    {
        // Tìm Health của Main_Character.
        Health playerHealth = null;
        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go == null || EditorUtility.IsPersistent(go) || !go.scene.IsValid())
            {
                continue;
            }
            if (go.name.IndexOf("Main_Character", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                playerHealth = go.GetComponent<Health>();
                if (playerHealth != null)
                {
                    break;
                }
            }
        }
        if (playerHealth == null)
        {
            Debug.LogError("BachDang/Redesign Player Health Bar: không tìm thấy Health trên Main_Character.");
            return;
        }

        // Tìm object tên "PlayerHealthBarCanvas" ở root.
        GameObject canvasObj = null;
        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go == null || EditorUtility.IsPersistent(go) || !go.scene.IsValid())
            {
                continue;
            }
            if (go.name == "PlayerHealthBarCanvas" && go.transform.parent == null)
            {
                canvasObj = go;
                break;
            }
        }

        if (canvasObj == null)
        {
            canvasObj = new GameObject("PlayerHealthBarCanvas", typeof(RectTransform));
            Canvas newCanvas = canvasObj.AddComponent<Canvas>();
            newCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler newScaler = canvasObj.AddComponent<CanvasScaler>();
            newScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            newScaler.referenceResolution = new Vector2(1920f, 1080f);
            newScaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();
            Undo.RegisterCreatedObjectUndo(canvasObj, "Redesign Player Health Bar");
            Debug.Log("Đã tạo mới 'PlayerHealthBarCanvas' (ScreenSpaceOverlay, CanvasScaler 1920x1080).");
        }
        else
        {
            Undo.RegisterCompleteObjectUndo(canvasObj, "Redesign Player Health Bar");
        }

        // Xóa con HealthBar cũ (toàn bộ children của canvas).
        for (int i = canvasObj.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = canvasObj.transform.GetChild(i);
            Undo.DestroyObjectImmediate(child.gameObject);
        }

        // Container: anchored top-left (30,-30), size (400,40), nền đen trong suốt.
        GameObject container = new GameObject("HealthBar", typeof(RectTransform));
        container.transform.SetParent(canvasObj.transform, false);
        RectTransform containerRect = container.GetComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0f, 1f);
        containerRect.anchorMax = new Vector2(0f, 1f);
        containerRect.pivot = new Vector2(0f, 1f);
        containerRect.anchoredPosition = new Vector2(30f, -30f);
        containerRect.sizeDelta = new Vector2(400f, 40f);

        Image bgImage = container.AddComponent<Image>();
        bgImage.color = new Color(0f, 0f, 0f, 0.6f);

        // Border: Image màu xám đậm.
        GameObject border = new GameObject("Border", typeof(RectTransform));
        border.transform.SetParent(container.transform, false);
        RectTransform borderRect = border.GetComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.offsetMin = Vector2.zero;
        borderRect.offsetMax = Vector2.zero;
        Image borderImage = border.AddComponent<Image>();
        borderImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);

        // Fill: anchor stretch, pivot (0,0.5) để fill từ trái.
        GameObject fillObj = new GameObject("Fill", typeof(RectTransform));
        fillObj.transform.SetParent(container.transform, false);
        RectTransform fillRect = fillObj.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        fillRect.pivot = new Vector2(0f, 0.5f);
        Image fillImage = fillObj.AddComponent<Image>();
        fillImage.color = Color.green;
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;

        // Text "HP" nhỏ góc trái (Legacy Text của uGUI, Arial, size 20, trắng).
        GameObject textObj = new GameObject("HPLabel", typeof(RectTransform));
        textObj.transform.SetParent(container.transform, false);
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0.5f);
        textRect.anchorMax = new Vector2(0f, 0.5f);
        textRect.pivot = new Vector2(0f, 0.5f);
        textRect.anchoredPosition = new Vector2(10f, 0f);
        textRect.sizeDelta = new Vector2(80f, 30f);
        Text hpText = textObj.AddComponent<Text>();
        hpText.text = "HP";
        hpText.fontSize = 20;
        hpText.color = Color.white;
        hpText.alignment = TextAnchor.MiddleLeft;
        try
        {
            hpText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        catch
        {
            hpText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
        if (hpText.font == null)
        {
            hpText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        // Slider component trên container: nền Image, fillRect = Fill, handle null.
        Slider slider = container.AddComponent<Slider>();
        slider.targetGraphic = bgImage;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        slider.fillRect = fillRect;
        slider.handleRect = null;

        // HealthBarUI: gắn/cập nhật lên canvas object.
        HealthBarUI healthBarUI = canvasObj.GetComponent<HealthBarUI>();
        if (healthBarUI == null)
        {
            healthBarUI = canvasObj.AddComponent<HealthBarUI>();
        }
        healthBarUI.target = playerHealth;
        healthBarUI.slider = slider;
        healthBarUI.fillImage = fillImage;
        healthBarUI.overlayMode = true;
        healthBarUI.faceCamera = false;

        Selection.activeGameObject = canvasObj;
        Debug.Log("BachDang/Redesign Player Health Bar: hoàn tất! " +
                  "Container (400x40, top-left 30,-30) + nền đen trong suốt + Border xám + Fill xanh pivot trái + Text 'HP'. " +
                  "HealthBarUI.target = Health của Main_Character, slider/fillImage đã nối, overlayMode = true.");
    }

    [MenuItem("BachDang/Redesign Enemy Health Bars")]
    public static void RedesignEnemyHealthBars()
    {
        int redesignedCount = 0;
        var candidates = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (GameObject go in candidates)
        {
            if (go == null || EditorUtility.IsPersistent(go) || !go.scene.IsValid())
            {
                continue;
            }
            if (!IsRealEnemy(go))
            {
                continue;
            }

            Health health = go.GetComponent<Health>();
            if (health == null)
            {
                continue;
            }

            Undo.RegisterCompleteObjectUndo(go, "Redesign Enemy Health Bars");

            // Xóa EnemyHealthBar cũ.
            Transform old = go.transform.Find("EnemyHealthBar");
            if (old != null)
            {
                Undo.DestroyObjectImmediate(old.gameObject);
            }

            // Tạo lại thanh máu nhỏ gọn, world-space.
            GameObject barObj = new GameObject("EnemyHealthBar", typeof(RectTransform));
            barObj.transform.SetParent(go.transform, false);
            barObj.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            barObj.transform.localRotation = Quaternion.identity;
            barObj.transform.localScale = new Vector3(0.004f, 0.004f, 0.004f);

            Canvas canvas = barObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            if (Camera.main != null)
            {
                canvas.worldCamera = Camera.main;
            }
            RectTransform canvasRect = barObj.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(300f, 50f);

            // Slider làm chính bar: nền đen trong suốt + fill xanh pivot trái, không text.
            GameObject sliderObj = new GameObject("Slider", typeof(RectTransform));
            sliderObj.transform.SetParent(barObj.transform, false);
            RectTransform sliderRect = sliderObj.GetComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0.5f, 0.5f);
            sliderRect.anchorMax = new Vector2(0.5f, 0.5f);
            sliderRect.pivot = new Vector2(0.5f, 0.5f);
            sliderRect.anchoredPosition = Vector2.zero;
            sliderRect.sizeDelta = new Vector2(300f, 50f);

            Image backgroundImage = sliderObj.AddComponent<Image>();
            backgroundImage.color = new Color(0f, 0f, 0f, 0.6f);

            Slider slider = sliderObj.AddComponent<Slider>();
            slider.targetGraphic = backgroundImage;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;

            GameObject fillObj = new GameObject("Fill", typeof(RectTransform));
            fillObj.transform.SetParent(sliderObj.transform, false);
            RectTransform fillRect = fillObj.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fillRect.pivot = new Vector2(0f, 0.5f);
            Image fillImage = fillObj.AddComponent<Image>();
            fillImage.color = Color.green;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            slider.fillRect = fillRect;

            HealthBarUI healthBarUI = barObj.AddComponent<HealthBarUI>();
            healthBarUI.target = health;
            healthBarUI.slider = slider;
            healthBarUI.fillImage = fillImage;
            healthBarUI.overlayMode = false;
            healthBarUI.faceCamera = true;

            Undo.RegisterCreatedObjectUndo(barObj, "Redesign Enemy Health Bars");
            redesignedCount++;
        }

        Debug.Log($"BachDang/Redesign Enemy Health Bars: đã redesign {redesignedCount} thanh máu giặc (300x50, localPos (0,2.2,0), scale 0.004).");
    }

    // Kiểm tra go có phải enemy "thật" không:
    // - Tên không chứa Main_Character, mọi ancestor cũng không chứa Main_Character.
    // - Có component Health.
    // - Tên chứa "Enemy"/"LinhDich"/"Giac" HOẶC path prefab/asset nằm trong thư mục Enemy.
    private static bool IsRealEnemy(GameObject go)
    {
        if (go == null)
        {
            return false;
        }

        if (go.name.IndexOf("Main_Character", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return false;
        }

        Transform parent = go.transform.parent;
        while (parent != null)
        {
            if (parent.name.IndexOf("Main_Character", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }
            parent = parent.parent;
        }

        if (go.GetComponent<Health>() == null)
        {
            return false;
        }

        bool nameMatch =
            go.name.IndexOf("Enemy", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            go.name.IndexOf("LinhDich", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            go.name.IndexOf("Giac", System.StringComparison.OrdinalIgnoreCase) >= 0;

        bool pathInEnemyFolder = false;
        string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
        if (!string.IsNullOrEmpty(assetPath))
        {
            string normalized = assetPath.Replace('\\', '/');
            pathInEnemyFolder = normalized.IndexOf("/Enemy/", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                normalized.IndexOf("/Enemies/", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        return nameMatch || pathInEnemyFolder;
    }

    [MenuItem("BachDang/Create Enemy Health Bar")]
    public static void CreateEnemyHealthBar()
    {
        int createdCount = 0;
        var candidates = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (GameObject go in candidates)
        {
            if (go == null || EditorUtility.IsPersistent(go) || !go.scene.IsValid())
            {
                continue;
            }

            // Chỉ enemy thật (có Health, không nằm dưới Main_Character) — tránh gắn lên Sword/Shield tên chứa "Meshy_AI".
            if (!IsRealEnemy(go))
            {
                continue;
            }

            Health health = go.GetComponent<Health>();
            if (health == null)
            {
                continue;
            }

            Undo.RegisterCompleteObjectUndo(go, "Create Enemy Health Bar");

            // Xóa EnemyHealthBar cũ nếu đã có.
            Transform old = go.transform.Find("EnemyHealthBar");
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            // Canvas World Space làm con của giặc.
            GameObject barObj = new GameObject("EnemyHealthBar", typeof(RectTransform));
            barObj.transform.SetParent(go.transform, false);
            barObj.transform.localPosition = new Vector3(0f, 2f, 0f);
            barObj.transform.localRotation = Quaternion.identity;
            barObj.transform.localScale = new Vector3(0.005f, 0.005f, 0.005f);

            Canvas canvas = barObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            if (Camera.main != null)
            {
                canvas.worldCamera = Camera.main;
            }

            RectTransform canvasRect = barObj.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(200f, 40f);

            CanvasScaler enemyScaler = barObj.AddComponent<CanvasScaler>();
            enemyScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            enemyScaler.scaleFactor = 1f;
            barObj.AddComponent<GraphicRaycaster>();

            // Slider 200x40 nền đen + fill xanh.
            GameObject sliderObj = new GameObject("Slider", typeof(RectTransform));
            sliderObj.transform.SetParent(barObj.transform, false);
            RectTransform sliderRect = sliderObj.GetComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0.5f, 0.5f);
            sliderRect.anchorMax = new Vector2(0.5f, 0.5f);
            sliderRect.pivot = new Vector2(0.5f, 0.5f);
            sliderRect.anchoredPosition = Vector2.zero;
            sliderRect.sizeDelta = new Vector2(200f, 40f);

            Image backgroundImage = sliderObj.AddComponent<Image>();
            backgroundImage.color = new Color(0f, 0f, 0f, 0.5f); // nền đen bán trong suốt

            Slider slider = sliderObj.AddComponent<Slider>();
            slider.targetGraphic = backgroundImage;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;

            GameObject fillAreaObj = new GameObject("Fill Area", typeof(RectTransform));
            fillAreaObj.transform.SetParent(sliderObj.transform, false);
            RectTransform fillAreaRect = fillAreaObj.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = Vector2.zero;
            fillAreaRect.offsetMax = Vector2.zero;

            GameObject fillObj = new GameObject("Fill", typeof(RectTransform));
            fillObj.transform.SetParent(fillAreaObj.transform, false);
            RectTransform fillRect = fillObj.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            Image fillImage = fillObj.AddComponent<Image>();
            fillImage.color = Color.green; // fill xanh
            slider.fillRect = fillRect;

            // HealthBarUI: target = Health của giặc, overlayMode = false, faceCamera = true.
            HealthBarUI healthBarUI = barObj.AddComponent<HealthBarUI>();
            healthBarUI.target = health;
            healthBarUI.slider = slider;
            healthBarUI.fillImage = fillImage;
            healthBarUI.overlayMode = false;
            healthBarUI.faceCamera = true;

            Undo.RegisterCreatedObjectUndo(barObj, "Create Enemy Health Bar");
            createdCount++;
        }

        Debug.Log($"BachDang/Create Enemy Health Bar: đã tạo {createdCount} thanh máu cho giặc.");
    }

    [MenuItem("BachDang/Setup Combat Scene")]
    public static void SetupCombatScene()
    {
        // 1) Tìm nhân vật chính (tên chứa "Main_Character", active) trong scene.
        GameObject mainCharacter = null;
        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go == null || EditorUtility.IsPersistent(go))
            {
                continue;
            }
            if (!go.scene.IsValid())
            {
                continue;
            }
            if (go.activeInHierarchy && go.name.Contains("Main_Character"))
            {
                mainCharacter = go;
                break;
            }
        }

        if (mainCharacter == null)
        {
            Debug.LogError("BachDang/Setup Combat Scene: không tìm thấy GameObject active tên chứa 'Main_Character' trong scene.");
            return;
        }

        Undo.RegisterCompleteObjectUndo(mainCharacter, "Setup Combat Scene");

        Health mainHealth = mainCharacter.GetComponent<Health>();
        if (mainHealth == null)
        {
            mainHealth = Undo.AddComponent<Health>(mainCharacter);
            Debug.Log($"Đã thêm Health cho '{mainCharacter.name}'.");
        }
        mainHealth.maxHP = 100;

        MeleeAttack melee = mainCharacter.GetComponent<MeleeAttack>();
        if (melee == null)
        {
            melee = Undo.AddComponent<MeleeAttack>(mainCharacter);
            Debug.Log($"Đã thêm MeleeAttack cho '{mainCharacter.name}'.");
        }
        melee.animator = mainCharacter.GetComponent<Animator>();
        melee.damage = 20;
        melee.attackRange = 1.8f;

        HitEffect hitEffect = mainCharacter.GetComponent<HitEffect>();
        if (hitEffect == null)
        {
            hitEffect = Undo.AddComponent<HitEffect>(mainCharacter);
            Debug.Log($"Đã thêm HitEffect cho '{mainCharacter.name}'.");
        }

        // Tầng Enemy: thử lấy, nếu thiếu thử tạo qua SerializedObject TagManager.
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer < 0)
        {
            enemyLayer = TryCreateLayer("Enemy");
        }
        if (enemyLayer < 0)
        {
            Debug.LogWarning("BachDang/Setup Combat Scene: chưa có layer 'Enemy'. " +
                            "Vào Edit > Project Settings > Tags and Layers và thêm layer 'Enemy', " +
                            "sau đó chạy lại menu.");
        }
        else
        {
            melee.enemyLayer = (LayerMask)(1 << enemyLayer);
        }

        // 2) Tìm tất cả enemy trong scene.
        string[] enemyKeywords = { "Meshy_AI", "LinhDich", "Enemy", "Giac" };
        int healthCount = 0;
        int layerCount = 0;
        var candidates = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (GameObject go in candidates)
        {
            if (go == null || EditorUtility.IsPersistent(go) || !go.scene.IsValid())
            {
                continue;
            }

            bool isMain = go.name.Contains("Main_Character");
            bool isEnemy = false;
            foreach (string kw in enemyKeywords)
            {
                if (go.name.IndexOf(kw, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    isEnemy = true;
                    break;
                }
            }

            if (isMain || !isEnemy)
            {
                continue;
            }

            Undo.RegisterCompleteObjectUndo(go, "Setup Combat Scene");

            Health h = go.GetComponent<Health>();
            if (h == null)
            {
                h = Undo.AddComponent<Health>(go);
                Debug.Log($"Đã gắn Health cho enemy '{go.name}'.");
            }
            h.maxHP = 50;
            healthCount++;

            if (enemyLayer >= 0)
            {
                go.layer = enemyLayer;
                foreach (Transform child in go.GetComponentsInChildren<Transform>(true))
                {
                    child.gameObject.layer = enemyLayer;
                }
                layerCount++;
            }
        }

        if (enemyLayer < 0)
        {
            Debug.LogWarning("BachDang/Setup Combat Scene: không set layer cho enemy vì layer 'Enemy' chưa tồn tại.");
        }

        // 3) Thêm state Attack vào AnimatorController của Main_Character.
        bool hasAttackState = false;
        Animator mainAnimator = mainCharacter.GetComponent<Animator>();
        AnimatorController controller = mainAnimator != null
            ? mainAnimator.runtimeAnimatorController as AnimatorController
            : null;

        if (controller != null)
        {
            bool hasAttackParam = false;
            foreach (AnimatorControllerParameter p in controller.parameters)
            {
                if (p.name == "Attack" && p.type == AnimatorControllerParameterType.Trigger)
                {
                    hasAttackParam = true;
                    break;
                }
            }
            if (!hasAttackParam)
            {
                controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
                Debug.Log("Đã thêm parameter 'Attack' (Trigger) vào controller.");
            }

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState attackState = null;
            foreach (ChildAnimatorState child in machine.states)
            {
                if (child.state != null && child.state.name == "Attack")
                {
                    attackState = child.state;
                    break;
                }
            }

            if (attackState == null)
            {
                attackState = machine.AddState("Attack");
                AnimationClip clip = FindCombatClip();
                if (clip != null)
                {
                    attackState.motion = clip;
                    Debug.Log($"Đã gán clip '{clip.name}' cho state Attack.");
                }
                else
                {
                    Debug.LogWarning("Không tìm thấy clip chứa 'Slash'/'Attack' trong Assets/Main_Character/Animation.");
                }
            }

            hasAttackState = attackState != null;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }
        else
        {
            Debug.LogWarning("BachDang/Setup Combat Scene: Main_Character không dùng AnimatorController gốc, không thêm state Attack.");
        }

        // 4) Log tóm tắt tiếng Việt.
        Debug.Log($"BachDang/Setup Combat Scene: hoàn tất. Enemy gắn/set Health: {healthCount}, " +
                  $"enemy chuyển sang layer Enemy: {layerCount}, state Attack: {(hasAttackState ? "có" : "không")}.");
    }

    [MenuItem("BachDang/Setup Enemy Attack")]
    public static void SetupEnemyAttack()
    {
        // 1) Đảm bảo có layer "Player" và "Enemy".
        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer < 0)
        {
            playerLayer = TryCreateLayer("Player");
        }
        if (playerLayer < 0)
        {
            Debug.LogWarning("BachDang/Setup Enemy Attack: chưa có layer 'Player'. " +
                             "Vào Edit > Project Settings > Tags and Layers và thêm layer 'Player', sau đó chạy lại menu.");
        }

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer < 0)
        {
            enemyLayer = TryCreateLayer("Enemy");
        }
        if (enemyLayer < 0)
        {
            Debug.LogWarning("BachDang/Setup Enemy Attack: chưa có layer 'Enemy'. " +
                             "Vào Edit > Project Settings > Tags and Layers và thêm layer 'Enemy', sau đó chạy lại menu.");
        }

        // 2) Tìm Main_Character và set layer Player cho nó cùng children.
        GameObject mainCharacter = null;
        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go == null || EditorUtility.IsPersistent(go) || !go.scene.IsValid())
            {
                continue;
            }
            if (go.activeInHierarchy && go.name.Contains("Main_Character"))
            {
                mainCharacter = go;
                break;
            }
        }

        int playerLayerCount = 0;
        if (mainCharacter == null)
        {
            Debug.LogError("BachDang/Setup Enemy Attack: không tìm thấy GameObject active tên chứa 'Main_Character'.");
        }
        else if (playerLayer >= 0)
        {
            Undo.RegisterCompleteObjectUndo(mainCharacter, "Setup Enemy Attack");
            mainCharacter.layer = playerLayer;
            foreach (Transform child in mainCharacter.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = playerLayer;
            }
            playerLayerCount = 1;
        }

        // 3) Với mỗi object tên chứa Meshy_AI/LinhDich/Enemy/Giac (trừ Main_Character):
        //    thêm Health + EnemyAttack nếu thiếu, set animator/playerLayer, giữ layer Enemy.
        int healthCount = 0;
        int enemyAttackCount = 0;
        int enemyLayerCount = 0;
        var candidates = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (GameObject go in candidates)
        {
            if (go == null || EditorUtility.IsPersistent(go) || !go.scene.IsValid())
            {
                continue;
            }

            // Chỉ enemy thật (có Health, không nằm dưới Main_Character).
            if (!IsRealEnemy(go))
            {
                continue;
            }

            Undo.RegisterCompleteObjectUndo(go, "Setup Enemy Attack");

            Health h = go.GetComponent<Health>();
            if (h == null)
            {
                h = Undo.AddComponent<Health>(go);
                Debug.Log($"Đã gắn Health cho enemy '{go.name}'.");
            }
            healthCount++;

            EnemyAttack enemyAttack = go.GetComponent<EnemyAttack>();
            if (enemyAttack == null)
            {
                enemyAttack = Undo.AddComponent<EnemyAttack>(go);
                Debug.Log($"Đã gắn EnemyAttack cho enemy '{go.name}'.");
            }
            enemyAttack.animator = go.GetComponentInChildren<Animator>();
            if (playerLayer >= 0)
            {
                // Chỉ layer Player, không gộp Enemy.
                int onlyPlayerLayer = 1 << LayerMask.NameToLayer("Player");
                enemyAttack.playerLayer = (LayerMask)onlyPlayerLayer;
            }
            enemyAttackCount++;

            if (enemyLayer >= 0)
            {
                go.layer = enemyLayer;
                foreach (Transform child in go.GetComponentsInChildren<Transform>(true))
                {
                    child.gameObject.layer = enemyLayer;
                }
                enemyLayerCount++;
            }
        }

        Debug.Log($"BachDang/Setup Enemy Attack: hoàn tất. Main_Character set layer Player: {playerLayerCount}, " +
                  $"enemy có Health: {healthCount}, enemy có EnemyAttack: {enemyAttackCount}, enemy giữ layer Enemy: {enemyLayerCount}.");
    }

    [MenuItem("BachDang/Fix Combat Colliders")]
    public static void FixCombatColliders()
    {
        int characterFixed = 0;
        int planeFixed = 0;

        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go == null || EditorUtility.IsPersistent(go) || !go.scene.IsValid())
            {
                continue;
            }

            bool isMain = go.name.IndexOf("Main_Character", System.StringComparison.OrdinalIgnoreCase) >= 0;
            bool isEnemy = IsRealEnemy(go);

            // Nhân vật: Main_Character và enemy thật (có Health, không lẫn vũ khí/child của Main_Character).
            if (isMain || isEnemy)
            {
                // Bỏ qua vũ khí/phụ kiện: không gắn CapsuleCollider/Rigidbody lên Sword/Shield/Weapon/Bow/WeaponHolder.
                bool isWeaponLike = IsExcludedCombatName(go.name);
                // Chỉ xử lý object khớp pattern ở tầng root của nhân vật — không gắn lên bone/child trùng keyword.
                bool isChildOfCharacterRoot = IsChildOfCharacter(go);

                if (!isWeaponLike && !isChildOfCharacterRoot)
                {
                    Undo.RegisterCompleteObjectUndo(go, "Fix Combat Colliders");
                    bool changed = false;

                    CapsuleCollider capsule = go.GetComponent<CapsuleCollider>();
                    if (capsule == null)
                    {
                        capsule = go.GetComponentInChildren<CapsuleCollider>();
                    }

                    bool hasSphere = go.GetComponent<SphereCollider>() != null || go.GetComponentInChildren<SphereCollider>() != null;
                    if (capsule != null)
                    {
                        // Đã có CapsuleCollider: resize lại theo bounds mới thay vì bỏ qua.
                        ApplyCapsuleSizeFromRenderers(go, capsule);
                        changed = true;
                        Debug.Log($"Đã resize CapsuleCollider cho '{go.name}' theo bounds mới.");
                    }
                    else if (!hasSphere)
                    {
                        capsule = Undo.AddComponent<CapsuleCollider>(go);
                        ApplyCapsuleSizeFromRenderers(go, capsule);
                        capsule.isTrigger = false;
                        changed = true;
                        Debug.Log($"Đã thêm CapsuleCollider cho '{go.name}'.");
                    }

                    // Rigidbody chỉ gắn vào chính object khớp pattern (root nhân vật), không gắn vào bone/children.
                    Rigidbody rb = go.GetComponent<Rigidbody>();
                    if (rb == null)
                    {
                        rb = Undo.AddComponent<Rigidbody>(go);
                        changed = true;
                        Debug.Log($"Đã thêm Rigidbody cho '{go.name}'.");
                    }
                    if (!rb.freezeRotation)
                    {
                        rb.freezeRotation = true; // không bị lật khi di chuyển
                        changed = true;
                    }

                    if (changed)
                    {
                        characterFixed++;
                    }
                }
            }

            // Plane: tên chứa "Plane" hoặc MeshRenderer nằm ngang.
            bool isPlaneByName = go.name.IndexOf("Plane", System.StringComparison.OrdinalIgnoreCase) >= 0;
            bool isHorizontalMesh = false;
            MeshRenderer meshRenderer = go.GetComponent<MeshRenderer>();
            if (!isPlaneByName && meshRenderer != null)
            {
                // MeshRenderer nằm ngang: mặt phẳng (normal chủ yếu theo trục Y).
                MeshFilter mf = go.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    Bounds b = mf.sharedMesh.bounds;
                    if (b.size.y < Mathf.Min(b.size.x, b.size.z) * 0.2f)
                    {
                        isHorizontalMesh = true;
                    }
                }
            }

            if ((isPlaneByName || isHorizontalMesh) && !IsExcludedCombatName(go.name))
            {
                bool hasCollider = go.GetComponent<Collider>() != null;
                if (!hasCollider)
                {
                    Undo.RegisterCompleteObjectUndo(go, "Fix Combat Colliders");
                    MeshFilter mf = go.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        MeshCollider meshCol = Undo.AddComponent<MeshCollider>(go);
                        meshCol.sharedMesh = mf.sharedMesh;
                        meshCol.convex = false;
                        Debug.Log($"Đã thêm MeshCollider (convex=false) cho '{go.name}'.");
                    }
                    else
                    {
                        BoxCollider box = Undo.AddComponent<BoxCollider>(go);
                        Vector3 size = box.size;
                        size.y = 0.5f; // dày 0.5
                        box.size = size;
                        Debug.Log($"Đã thêm BoxCollider (dày 0.5) cho '{go.name}'.");
                    }
                    planeFixed++;
                }
            }
        }

        Debug.Log($"BachDang/Fix Combat Colliders: đã sửa {characterFixed} nhân vật (Capsule/SphereCollider + Rigidbody freezeRotation), {planeFixed} mặt phẳng (MeshCollider/BoxCollider).");
    }

    [MenuItem("BachDang/Clean Combat Setup")]
    public static void CleanCombatSetup()
    {
        string[] weaponKeywords = { "Sword", "Shield", "Bow", "Weapon", "Sword_", "Shield_" };
        int cleanedCount = 0;

        var candidates = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (GameObject go in candidates)
        {
            if (go == null || EditorUtility.IsPersistent(go) || !go.scene.IsValid())
            {
                continue;
            }

            bool nameIsWeapon = false;
            foreach (string kw in weaponKeywords)
            {
                if (go.name.IndexOf(kw, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    nameIsWeapon = true;
                    break;
                }
            }

            bool underMainCharacter = false;
            Transform parent = go.transform.parent;
            while (parent != null)
            {
                if (parent.name.IndexOf("Main_Character", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    underMainCharacter = true;
                    break;
                }
                parent = parent.parent;
            }

            if (!nameIsWeapon && !underMainCharacter)
            {
                continue;
            }

            Undo.RegisterCompleteObjectUndo(go, "Clean Combat Setup");

            bool changed = false;

            // Xóa component combat (Health, EnemyAttack, MeleeAttack, HitEffect) nếu có.
            Health[] healths = go.GetComponents<Health>();
            foreach (Health h in healths)
            {
                Undo.DestroyObjectImmediate(h);
                changed = true;
            }

            EnemyAttack[] enemyAttacks = go.GetComponents<EnemyAttack>();
            foreach (EnemyAttack ea in enemyAttacks)
            {
                Undo.DestroyObjectImmediate(ea);
                changed = true;
            }

            MeleeAttack[] meleeAttacks = go.GetComponents<MeleeAttack>();
            foreach (MeleeAttack ma in meleeAttacks)
            {
                Undo.DestroyObjectImmediate(ma);
                changed = true;
            }

            HitEffect[] hitEffects = go.GetComponents<HitEffect>();
            foreach (HitEffect he in hitEffects)
            {
                Undo.DestroyObjectImmediate(he);
                changed = true;
            }

            // Xóa child EnemyHealthBar / PlayerHealthBarCanvas (duyệt sâu, thu thập trước rồi xóa).
            List<GameObject> barsToRemove = new List<GameObject>();
            foreach (Transform child in go.GetComponentsInChildren<Transform>(true))
            {
                if (child == null || child.gameObject == go)
                {
                    continue;
                }
                if (child.name == "EnemyHealthBar" || child.name == "PlayerHealthBarCanvas")
                {
                    barsToRemove.Add(child.gameObject);
                }
            }
            foreach (GameObject bar in barsToRemove)
            {
                Undo.DestroyObjectImmediate(bar);
                changed = true;
            }

            if (changed)
            {
                cleanedCount++;
                Debug.Log($"BachDang/Clean Combat Setup: đã dọn '{go.name}' (xóa Health/EnemyAttack/MeleeAttack/HitEffect và child EnemyHealthBar/PlayerHealthBarCanvas).");
            }
        }

        Debug.Log($"BachDang/Clean Combat Setup: hoàn tất. Đã dọn {cleanedCount} object vũ khí/phụ kiện (dính Health/EnemyAttack/thanh máu).");
    }

    // Tên chứa Sword/Shield/Weapon/Bow/WeaponHolder (không phân biệt hoa thường) => không gắn collider/Rigidbody.
    private static bool IsExcludedCombatName(string name)
    {
        return name.IndexOf("Sword", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Shield", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Weapon", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Bow", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("WeaponHolder", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    // true nếu go nằm bên trong một object nhân vật (Main_Character/enemy) khác => go là bone hoặc child, bỏ qua.
    private static bool IsChildOfCharacter(GameObject go)
    {
        Transform parent = go.transform.parent;
        while (parent != null)
        {
            string n = parent.name;
            bool isMain = n.IndexOf("Main_Character", System.StringComparison.OrdinalIgnoreCase) >= 0;
            // Coi như child của nhân vật nếu cha có Health (Main_Character/enemy) — không phụ thuộc từ khóa tên như "Meshy_AI".
            bool isCharacterWithHealth = parent.GetComponent<Health>() != null;
            if (isMain || isCharacterWithHealth)
            {
                return true;
            }
            parent = parent.parent;
        }
        return false;
    }

    // Tính kích thước CapsuleCollider từ bounds gộp của các Renderer trong object.
    private static void ApplyCapsuleSizeFromRenderers(GameObject go, CapsuleCollider capsule)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            // Không có renderer thì giữ mặc định.
            capsule.center = new Vector3(0f, 1f, 0f);
            capsule.height = 2f;
            capsule.radius = 0.4f;
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }

        float height = bounds.size.y;
        float radius = Mathf.Max(bounds.size.x, bounds.size.z) / 4f;
        // CapsuleCollider yêu cầu height >= 2 * radius.
        height = Mathf.Max(height, radius * 2f);
        Vector3 localCenter = go.transform.InverseTransformPoint(bounds.center);

        capsule.center = localCenter;
        capsule.height = height;
        capsule.radius = radius;
    }

    // Tìm clip có tên chứa "Slash" hoặc "Attack" trong Assets/Main_Character/Animation.
    private static AnimationClip FindCombatClip()
    {
        string folder = "Assets/Main_Character/Animation";
        string[] fbxPaths =
        {
            folder + "/SlashSword.fbx",
            folder + "/Attack2.fbx",
        };

        foreach (string path in fbxPaths)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (Object asset in assets)
            {
                if (asset is AnimationClip clip &&
                    (clip.name.IndexOf("Slash", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                     clip.name.IndexOf("Attack", System.StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return clip;
                }
            }
        }
        return null;
    }

    // Thử tạo layer mới qua SerializedObject trên TagManager. Trả về index layer hoặc -1.
    private static int TryCreateLayer(string layerName)
    {
        try
        {
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layersProp = tagManager.FindProperty("layers");
            if (layersProp == null)
            {
                return -1;
            }

            for (int i = 8; i < layersProp.arraySize; i++)
            {
                SerializedProperty layerProp = layersProp.GetArrayElementAtIndex(i);
                if (layerProp.stringValue == layerName)
                {
                    return i;
                }
            }

            for (int i = 8; i < layersProp.arraySize; i++)
            {
                SerializedProperty layerProp = layersProp.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(layerProp.stringValue))
                {
                    layerProp.stringValue = layerName;
                    tagManager.ApplyModifiedProperties();
                    Debug.Log($"Đã tạo layer '{layerName}' (index {i}).");
                    return i;
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"Không thể tạo layer '{layerName}' tự động: {ex.Message}");
        }
        return -1;
    }

    // Duyệt cây state (kể cả sub-state machine) để tìm clip tên chứa "attack".
    private static AnimationClip FindAttackClip(AnimatorStateMachine machine)
    {
        var visited = new HashSet<AnimatorStateMachine>();
        return FindAttackClipRecursive(machine, visited);
    }

    private static AnimationClip FindAttackClipRecursive(AnimatorStateMachine machine, HashSet<AnimatorStateMachine> visited)
    {
        if (machine == null || !visited.Add(machine))
        {
            return null;
        }

        foreach (ChildAnimatorState child in machine.states)
        {
            if (child.state != null && child.state.motion is AnimationClip clip &&
                clip.name.ToLowerInvariant().Contains("attack"))
            {
                return clip;
            }
        }

        foreach (ChildAnimatorStateMachine sub in machine.stateMachines)
        {
            AnimationClip found = FindAttackClipRecursive(sub.stateMachine, visited);
            if (found != null)
            {
                return found;
            }
        }

        // Kiểm tra cả blend tree con (lấy clip đầu tiên bên trong blend tree).
        foreach (ChildAnimatorState child in machine.states)
        {
            if (child.state != null && child.state.motion is BlendTree tree)
            {
                AnimationClip found = FindClipInBlendTree(tree);
                if (found != null && found.name.ToLowerInvariant().Contains("attack"))
                {
                    return found;
                }
            }
        }

        return null;
    }

    private static AnimationClip FindClipInBlendTree(BlendTree tree)
    {
        if (tree == null)
        {
            return null;
        }

        foreach (ChildMotion child in tree.children)
        {
            if (child.motion is AnimationClip clip)
            {
                return clip;
            }
            if (child.motion is BlendTree subTree)
            {
                AnimationClip found = FindClipInBlendTree(subTree);
                if (found != null)
                {
                    return found;
                }
            }
        }

        return null;
    }
}
