using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Bounded click editing; no tree-array scans on idle Scene repaints.</summary>
public class TreeEraserTool : EditorWindow
{
    internal const int InstanceLimit = 60000;
    [SerializeField] Terrain targetTerrain;
    [SerializeField] int mode, choice, amount = 1;
    [SerializeField] float radius = 5, spacing = 6, size = 1;
    [SerializeField] bool eraseStandalone = true;
    bool active;
    int[] indices = Array.Empty<int>();
    string[] labels = Array.Empty<string>();
    string status = "Chọn chế độ rồi bật cọ. Bấm chuột trái trong Scene để sửa cây.";

    [MenuItem("Bach Dang/Them - Xoa Cay")]
    [MenuItem("🤖 Trợ lý AI/🪓 Công Cụ Xóa Cây Nhanh (Tree Eraser Tool)")]
    public static void ShowWindow()
    {
        var window = GetWindow<TreeEraserTool>("Thêm / Xóa cây");
        window.minSize = new Vector2(380, 430);
        window.Show();
    }
    void OnEnable()
    {
        active = false;
        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.duringSceneGui += OnSceneGUI;
        Undo.undoRedoPerformed -= OnUndo;
        Undo.undoRedoPerformed += OnUndo;
        if (targetTerrain == null) targetTerrain = Terrain.activeTerrain;
        RefreshChoices();
    }
    void OnDisable()
    {
        active = false;
        SceneView.duringSceneGui -= OnSceneGUI;
        Undo.undoRedoPerformed -= OnUndo;
    }
    void OnUndo()
    {
        if (targetTerrain != null) targetTerrain.Flush();
        SceneView.RepaintAll(); Repaint();
    }
    void RefreshChoices()
    {
        var list = new List<int>(); var names = new List<string>();
        if (targetTerrain != null && targetTerrain.terrainData != null)
        {
            var prototypes = targetTerrain.terrainData.treePrototypes;
            for (int i = 0; i < prototypes.Length; i++)
            {
                string name = WoodyMeshName(prototypes[i].prefab);
                if (name == null) continue;
                list.Add(i);
                names.Add(name.StartsWith("Cypress", StringComparison.OrdinalIgnoreCase) ? "Bách / Cypress" :
                    name.StartsWith("Conifer", StringComparison.OrdinalIgnoreCase) ? "Thông / Conifer" : name);
            }
        }
        indices = list.ToArray(); labels = names.ToArray(); choice = Mathf.Clamp(choice, 0, Mathf.Max(0, indices.Length - 1));
    }
    internal static string WoodyMeshName(GameObject prefab)
    {
        if (prefab == null) return null;
        // Generated Tree_<guid> prefab names also describe rocks and grass: inspect the source mesh.
        foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            string n = filter.sharedMesh.name;
            if (new[] { "Cypress", "Conifer", "Tree_", "Oak", "Pine", "Birch", "Palm" }
                .Any(prefix => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))) return n;
        }
        return null;
    }
    void OnGUI()
    {
        EditorGUILayout.LabelField("THÊM / XÓA CÂY TRÊN SA BÀN", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        var terrain = (Terrain)EditorGUILayout.ObjectField("Terrain", targetTerrain, typeof(Terrain), true);
        if (EditorGUI.EndChangeCheck()) { active = false; targetTerrain = terrain; RefreshChoices(); }
        if (GUILayout.Button("Dùng Terrain đang mở / Cập nhật mẫu cây"))
        { active = false; targetTerrain = Terrain.activeTerrain; RefreshChoices(); }
        if (targetTerrain == null || targetTerrain.terrainData == null) return;
        float factor = BachDangWorldScale.ForScene(targetTerrain.gameObject.scene);
        EditorGUILayout.LabelField("Tổng mẫu cây, bụi, đá", targetTerrain.terrainData.treeInstanceCount.ToString("N0") + " / " + InstanceLimit.ToString("N0"));
        EditorGUILayout.LabelField("Tỉ lệ map", factor.ToString("0.##") + " Unity units / mét quy đổi");
        mode = GUILayout.Toolbar(mode, new[] { "Thêm cây", "Xóa cây" });
        radius = EditorGUILayout.Slider("Bán kính cọ (m)", radius, 1, 25);
        if (mode == 0)
        {
            if (indices.Length > 0) choice = EditorGUILayout.Popup("Loại cây", choice, labels);
            amount = EditorGUILayout.IntSlider("Cây mỗi lần bấm", amount, 1, 10);
            spacing = EditorGUILayout.Slider("Khoảng cách tối thiểu (m)", spacing, 2, 20);
            size = EditorGUILayout.Slider("Kích thước so với cây cũ", size, .5f, 1.5f);
            EditorGUILayout.HelpBox("1 cây: trồng ngay con trỏ. Nhiều cây: rải trong vòng cọ. Bỏ qua vùng nước, dốc quá 35° và chỗ quá sát cây cũ.", MessageType.None);
        }
        else
        {
            eraseStandalone = EditorGUILayout.Toggle("Xóa cả cây prefab đặt riêng", eraseStandalone);
            EditorGUILayout.HelpBox("Xóa cây có gốc trong vòng cọ. Giữ đá, bụi và cỏ. Chỉ xóa prefab cây, không xóa nhóm cảnh vật bao quanh.", MessageType.None);
        }
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || (mode == 0 && indices.Length == 0)))
        {
            if (GUILayout.Button(active ? "TẮT CỌ (Esc)" : "BẬT CỌ TRONG SCENE", GUILayout.Height(32)))
            { active = !active; SceneView.RepaintAll(); }
        }
        EditorGUILayout.HelpBox("Bấm từng lần bằng chuột trái • Ctrl+Z: hoàn tác • Ctrl+Y: làm lại\nAlt / chuột phải: di chuyển góc nhìn bình thường. Cọ tự tắt khi đóng cửa sổ.", MessageType.Info);
        EditorGUILayout.LabelField(status, EditorStyles.wordWrappedLabel);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        if (GUILayout.Button("Lưu cây và scene"))
        {
            AssetDatabase.SaveAssetIfDirty(targetTerrain.terrainData);
            status = EditorSceneManager.SaveScene(targetTerrain.gameObject.scene) ? "Đã lưu." : "Chưa lưu được scene.";
        }
    }
    void OnSceneGUI(SceneView view)
    {
        if (!active || targetTerrain == null || targetTerrain.terrainData == null) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode) { active = false; Repaint(); return; }
        var e = Event.current;
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { active = false; e.Use(); Repaint(); return; }
        if (e.alt || e.control || e.command || Tools.viewToolActive) return;
        var collider = targetTerrain.GetComponent<TerrainCollider>();
        if (collider == null || !collider.Raycast(HandleUtility.GUIPointToWorldRay(e.mousePosition), out var hit, 100000)) return;
        if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        if (e.type == EventType.Repaint)
        {
            Handles.color = mode == 0 ? Color.green : Color.red;
            Handles.DrawWireDisc(hit.point, Vector3.up, radius * BachDangWorldScale.ForScene(targetTerrain.gameObject.scene));
        }
        if (e.type == EventType.MouseMove) view.Repaint();
        if (e.type != EventType.MouseDown || e.button != 0) return;
        try
        {
            int delta = ApplyClick(targetTerrain, hit.point, mode == 0, indices.Length > choice ? indices[choice] : -1,
                radius, amount, spacing, size, eraseStandalone);
            status = delta == 0 ? "Không đổi: kiểm tra khoảng cách, mặt đất hoặc vùng cọ." :
                (delta > 0 ? "Đã thêm " : "Đã xóa ") + Mathf.Abs(delta) + " cây. Ctrl+Z để hoàn tác.";
        }
        catch (Exception ex) { status = ex.Message; Debug.LogException(ex); }
        e.Use(); Repaint(); SceneView.RepaintAll();
    }

    // One click = one Undo snapshot and one tree-array write, never every repaint or drag event.
    internal static int ApplyClick(Terrain terrain, Vector3 center, bool add, int prototype,
        float radiusMetres, int count, float spacingMetres, float sizeMultiplier, bool standalone)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || terrain == null || terrain.terrainData == null)
            throw new InvalidOperationException("Chỉ sửa cây trong Edit Mode.");
        if (terrain.transform.lossyScale != Vector3.one || terrain.transform.rotation != Quaternion.identity)
            throw new InvalidOperationException("Terrain cần giữ Scale 1 và Rotation 0.");
        var data = terrain.terrainData; var origin = terrain.transform.position; var dimensions = data.size;
        float factor = BachDangWorldScale.ForScene(terrain.gameObject.scene);
        float radiusWorld = Mathf.Clamp(radiusMetres, 1, 25) * factor;
        float spacingWorld = Mathf.Clamp(spacingMetres, 2, 20) * factor;
        var prototypes = data.treePrototypes;
        var woody = prototypes.Select(p => WoodyMeshName(p.prefab) != null).ToArray();
        var old = data.treeInstances; var next = new List<TreeInstance>(old.Length + 10);
        next.AddRange(old);
        var eraseObjects = new HashSet<GameObject>();
        Vector3 World(TreeInstance tree) => origin + Vector3.Scale(tree.position, dimensions);
        bool Near(Vector3 a, Vector3 b, float distance) => (new Vector2(a.x - b.x, a.z - b.z)).sqrMagnitude <= distance * distance;
        if (add)
        {
            if (prototype < 0 || prototype >= prototypes.Length || !woody[prototype]) throw new InvalidOperationException("Chọn một mẫu cây thân gỗ.");
            if (old.Length >= InstanceLimit) throw new InvalidOperationException("Đã chạm giới hạn 60.000 mẫu Terrain cho chế độ laptop; xóa bớt trước khi thêm.");
            float width = 0, height = 0; int samples = 0;
            foreach (var tree in old) if (tree.prototypeIndex == prototype && samples < 64)
            { width += tree.widthScale; height += tree.heightScale; samples++; }
            width = samples == 0 ? factor : width / samples; height = samples == 0 ? factor : height / samples;
            count = Mathf.Clamp(count, 1, Math.Min(10, InstanceLimit - old.Length));
            var random = new System.Random(); int added = 0;
            for (int attempt = 0; attempt < (count == 1 ? 1 : count * 12) && added < count; attempt++)
            {
                double angle = random.NextDouble() * Math.PI * 2;
                float r = count == 1 ? 0 : Mathf.Sqrt((float)random.NextDouble()) * radiusWorld;
                var point = center + new Vector3((float)Math.Cos(angle) * r, 0, (float)Math.Sin(angle) * r);
                float x = (point.x - origin.x) / dimensions.x, z = (point.z - origin.z) / dimensions.z;
                if (x < 0 || x > 1 || z < 0 || z > 1) continue;
                float ground = data.GetInterpolatedHeight(x, z) + origin.y;
                float dry = terrain.gameObject.scene.path == "Assets/Scenes/beachBoat.unity" ? 16.8f * factor : origin.y;
                if (ground < dry || data.GetSteepness(x, z) > 35) continue;
                if (next.Any(tree => tree.prototypeIndex >= 0 && tree.prototypeIndex < woody.Length && woody[tree.prototypeIndex] && Near(World(tree), point, spacingWorld))) continue;
                float variation = Mathf.Lerp(.9f, 1.1f, (float)random.NextDouble()) * Mathf.Clamp(sizeMultiplier, .5f, 1.5f);
                next.Add(new TreeInstance { position = new Vector3(x, (ground - origin.y) / dimensions.y, z), prototypeIndex = prototype,
                    widthScale = width * variation, heightScale = height * variation, rotation = (float)(random.NextDouble() * Math.PI * 2), color = Color.white, lightmapColor = Color.white });
                added++;
            }
        }
        else
        {
            next.RemoveAll(tree => tree.prototypeIndex >= 0 && tree.prototypeIndex < woody.Length && woody[tree.prototypeIndex] && Near(World(tree), center, radiusWorld));
            if (standalone) foreach (var root in terrain.gameObject.scene.GetRootGameObjects())
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (!Near(filter.transform.position, center, radiusWorld)) continue;
                    var instance = PrefabUtility.GetNearestPrefabInstanceRoot(filter.gameObject);
                    if (instance == null || instance == terrain.gameObject || !Near(instance.transform.position, center, radiusWorld)) continue;
                    // Inspect this prefab's source, never promote to the enclosing scenery/camp parent.
                    var source = PrefabUtility.GetCorrespondingObjectFromSource(instance);
                    if (WoodyMeshName(source) != null) eraseObjects.Add(instance);
                }
        }
        int delta = next.Count - old.Length - eraseObjects.Count;
        if (delta == 0) return 0;
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName(add ? "Thêm cây" : "Xóa cây");
        try
        {
            if (next.Count != old.Length)
            {
                Undo.RegisterCompleteObjectUndo(data, add ? "Thêm cây Terrain" : "Xóa cây Terrain");
                data.SetTreeInstances(next.ToArray(), false); EditorUtility.SetDirty(data); terrain.Flush();
            }
            foreach (var obj in eraseObjects) if (obj != null) Undo.DestroyObjectImmediate(obj);
            EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
            Undo.CollapseUndoOperations(group);
            return delta;
        }
        catch { Undo.RevertAllDownToGroup(group); terrain.Flush(); throw; }
    }
}
