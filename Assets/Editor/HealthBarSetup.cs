using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using UnityEditor.SceneManagement;

public static class HealthBarSetup
{
    private const string NenSpritePath = "Assets/heath bar/thanh nen.png";
    private const string MauSpritePath = "Assets/heath bar/thanh mau.png";
    private const string PrefabPath = "Assets/heath bar/HealthBarPrefab.prefab";

    [MenuItem("BachDang/Tạo và gắn Thanh Máu cho tất cả nhân vật (CLI & Editor)")]
    public static void ApplyToAll()
    {
        Sprite nenSprite = AssetDatabase.LoadAssetAtPath<Sprite>(NenSpritePath);
        Sprite mauSprite = AssetDatabase.LoadAssetAtPath<Sprite>(MauSpritePath);

        if (nenSprite == null || mauSprite == null)
        {
            Debug.LogError($"[HealthBarSetup] Không tìm thấy sprite thanh máu tại {NenSpritePath} hoặc {MauSpritePath}");
            return;
        }

        GameObject prefab = CreateOrUpdatePrefab(nenSprite, mauSprite);
        if (prefab == null)
        {
            Debug.LogError("[HealthBarSetup] Không thể tạo Prefab thanh máu!");
            return;
        }

        var activeScene = EditorSceneManager.GetActiveScene();
        var characters = FindAllCharactersInScene(activeScene);

        int count = 0;
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Setup Health Bars For All Characters");

        foreach (var character in characters)
        {
            SetupHealthBarForCharacter(character, prefab, nenSprite, mauSprite);
            count++;
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(activeScene);
        AssetDatabase.SaveAssets();

        Debug.Log($"<color=green>[HealthBarSetup] HOÀN TẤT: Đã tạo và gắn thanh máu thành công cho {count} nhân vật trong scene '{activeScene.name}'!</color>");
    }

    public static GameObject CreateOrUpdatePrefab(Sprite nenSprite, Sprite mauSprite)
    {
        // Tạo root GameObject
        GameObject root = new GameObject("HealthBar");
        
        RectTransform rootRect = root.AddComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(240, 46);

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = null;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;
        scaler.referencePixelsPerUnit = 100f;

        CanvasGroup canvasGroup = root.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        root.transform.localScale = new Vector3(0.0045f, 0.0045f, 0.0045f);

        // 1. Background image (thanh nen: viền/nền đỏ sẫm)
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(root.transform, false);
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.sprite = nenSprite;
        bgImg.color = new Color(0.40f, 0.04f, 0.04f, 0.95f);
        bgImg.raycastTarget = false;

        // 2. Chip damage delay bar (vàng cam mượt mà khi nhận damage)
        GameObject delayObj = new GameObject("DelayFill");
        delayObj.transform.SetParent(root.transform, false);
        RectTransform delayRect = delayObj.AddComponent<RectTransform>();
        delayRect.anchorMin = Vector2.zero;
        delayRect.anchorMax = Vector2.one;
        delayRect.offsetMin = new Vector2(3, 3);
        delayRect.offsetMax = new Vector2(-3, -3);
        Image delayImg = delayObj.AddComponent<Image>();
        delayImg.sprite = mauSprite;
        delayImg.color = new Color(1.0f, 0.82f, 0.35f, 0.95f);
        delayImg.type = Image.Type.Filled;
        delayImg.fillMethod = Image.FillMethod.Horizontal;
        delayImg.fillOrigin = (int)Image.OriginHorizontal.Left;
        delayImg.fillAmount = 1f;
        delayImg.raycastTarget = false;

        // 3. Health bar fill chính (thanh mau: đỏ tươi rực rỡ)
        GameObject fillObj = new GameObject("HealthFill");
        fillObj.transform.SetParent(root.transform, false);
        RectTransform fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(3, 3);
        fillRect.offsetMax = new Vector2(-3, -3);
        Image fillImg = fillObj.AddComponent<Image>();
        fillImg.sprite = mauSprite;
        fillImg.color = new Color(0.95f, 0.12f, 0.12f, 1.0f);
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImg.fillAmount = 1f;
        fillImg.raycastTarget = false;

        // 4. Character Name Text (TextMeshPro)
        GameObject nameObj = new GameObject("NameText");
        nameObj.transform.SetParent(root.transform, false);
        RectTransform nameRect = nameObj.AddComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 1f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.pivot = new Vector2(0.5f, 0f);
        nameRect.anchoredPosition = new Vector2(0, 4);
        nameRect.sizeDelta = new Vector2(240, 26);
        TextMeshProUGUI nameTMP = nameObj.AddComponent<TextMeshProUGUI>();
        nameTMP.text = "Tướng Quân";
        nameTMP.fontSize = 20;
        nameTMP.fontStyle = FontStyles.Bold;
        nameTMP.alignment = TextAlignmentOptions.Center;
        nameTMP.color = Color.white;
        nameTMP.raycastTarget = false;
        nameObj.SetActive(false);

        // 5. Numeric Text (TextMeshPro)
        GameObject valObj = new GameObject("ValueText");
        valObj.transform.SetParent(root.transform, false);
        RectTransform valRect = valObj.AddComponent<RectTransform>();
        valRect.anchorMin = Vector2.zero;
        valRect.anchorMax = Vector2.one;
        valRect.offsetMin = Vector2.zero;
        valRect.offsetMax = Vector2.zero;
        TextMeshProUGUI valTMP = valObj.AddComponent<TextMeshProUGUI>();
        valTMP.text = "100 / 100";
        valTMP.fontSize = 18;
        valTMP.fontStyle = FontStyles.Bold;
        valTMP.alignment = TextAlignmentOptions.Center;
        valTMP.color = Color.white;
        valTMP.raycastTarget = false;
        valObj.SetActive(false);

        // 6. Gắn script FloatingHealthBar
        FloatingHealthBar floating = root.AddComponent<FloatingHealthBar>();
        
        SerializedObject so = new SerializedObject(floating);
        so.FindProperty("canvas").objectReferenceValue = canvas;
        so.FindProperty("backgroundImage").objectReferenceValue = bgImg;
        so.FindProperty("delayFillImage").objectReferenceValue = delayImg;
        so.FindProperty("fillImage").objectReferenceValue = fillImg;
        so.FindProperty("nameText").objectReferenceValue = nameTMP;
        so.FindProperty("valueText").objectReferenceValue = valTMP;
        so.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
        so.FindProperty("billboard").boolValue = true;
        so.FindProperty("showName").boolValue = false;
        so.FindProperty("showNumbers").boolValue = false;
        so.FindProperty("heightPadding").floatValue = 0.35f;
        so.FindProperty("baseWorldScale").vector3Value = new Vector3(0.0045f, 0.0045f, 0.0045f);
        so.ApplyModifiedPropertiesWithoutUndo();

        // Lưu Prefab
        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();

        return savedPrefab;
    }

    public static List<GameObject> FindAllCharactersInScene(UnityEngine.SceneManagement.Scene scene)
    {
        var result = new List<GameObject>();
        var rootGos = scene.GetRootGameObjects();

        foreach (var root in rootGos)
        {
            ScanGameObjectForCharacters(root, result);
        }

        return result;
    }

    private static void ScanGameObjectForCharacters(GameObject go, List<GameObject> list)
    {
        if (go == null) return;

        if (go.GetComponent<Camera>() != null || 
            go.GetComponent<Light>() != null || 
            go.GetComponent<Terrain>() != null ||
            go.name.Contains("Directional Light") || 
            go.name.Contains("Global Volume") ||
            go.name.Contains("Canvas_"))
        {
            return;
        }

        bool isCharacter = false;
        var animator = go.GetComponent<Animator>();
        var smr = go.GetComponentInChildren<SkinnedMeshRenderer>();

        if (animator != null || smr != null)
        {
            if (go.transform.Find("Armature") != null || smr != null || (animator != null && animator.avatar != null))
            {
                isCharacter = true;
            }
        }

        string lower = go.name.ToLower();
        if (lower.Contains("character") || lower.Contains("npc") || lower.Contains("peagent") || 
            lower.Contains("peasent") || lower.Contains("ngoquyen") || lower.Contains("danlang") ||
            lower.Contains("female_pea") || lower.Contains("disciple"))
        {
            if (go.transform.Find("Armature") != null || smr != null || animator != null)
            {
                isCharacter = true;
            }
        }

        if (isCharacter && !list.Contains(go))
        {
            list.Add(go);
            return;
        }

        for (int i = 0; i < go.transform.childCount; i++)
        {
            ScanGameObjectForCharacters(go.transform.GetChild(i).gameObject, list);
        }
    }

    public static void SetupHealthBarForCharacter(GameObject character, GameObject prefab, Sprite nenSprite, Sprite mauSprite)
    {
        Undo.RecordObject(character, "Setup Health Bar");

        // 1. Dọn dẹp capsule mockup thủ công cũ (ví dụ Capsule trên NgoQuyenTheGoat)
        Transform oldCapsule = character.transform.Find("Capsule");
        if (oldCapsule != null)
        {
            Undo.DestroyObjectImmediate(oldCapsule.gameObject);
        }

        // 2. Đảm bảo nhân vật có component Health
        Health health = character.GetComponent<Health>();
        if (health == null)
        {
            health = Undo.AddComponent<Health>(character);
        }

        // Đảm bảo nhân vật có Collider để nhận sát thương khi bị chém
        CapsuleCollider col = character.GetComponent<CapsuleCollider>();
        if (col == null)
        {
            col = Undo.AddComponent<CapsuleCollider>(character);
            col.height = 2.0f;
            col.radius = 0.45f;
            col.center = new Vector3(0, 1.0f, 0);
        }

        // Nếu là nhân vật người chơi điều khiển, gắn MeleeCombat để chém gây sát thương
        if (character.GetComponent<Character_Movement>() != null && character.GetComponent<MeleeCombat>() == null)
        {
            Undo.AddComponent<MeleeCombat>(character);
        }

        // 3. Xóa hoặc tìm thanh máu cũ đã tạo nếu có
        Transform existingBar = character.transform.Find("HealthBar");
        if (existingBar != null)
        {
            Undo.DestroyObjectImmediate(existingBar.gameObject);
        }

        // 4. Khởi tạo HealthBar từ Prefab
        GameObject barInstance = PrefabUtility.InstantiatePrefab(prefab, character.transform) as GameObject;
        if (barInstance == null)
        {
            barInstance = Object.Instantiate(prefab, character.transform);
        }

        barInstance.name = "HealthBar";
        Undo.RegisterCreatedObjectUndo(barInstance, "Create HealthBar Instance");

        // 5. Cấu hình FloatingHealthBar
        FloatingHealthBar floatingBar = barInstance.GetComponent<FloatingHealthBar>();
        if (floatingBar != null)
        {
            floatingBar.Setup(health, nenSprite, mauSprite);
            floatingBar.CalculateOffset();
            floatingBar.UpdateDisplayInstant();
            floatingBar.UpdatePositionAndRotation();

            // Đặt localPosition mặc định ngay lập tức
            barInstance.transform.position = character.transform.position + floatingBar.Offset;

            EditorUtility.SetDirty(floatingBar);
        }

        EditorUtility.SetDirty(health);
        EditorUtility.SetDirty(character);
    }
}
