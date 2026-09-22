using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class TreeEraserTool : EditorWindow
{
    private static bool isToolActive = false;
    private static float brushRadius = 3.0f; // Bán kính xóa mặc định (mét)
    private static Terrain targetTerrain = null;

    [MenuItem("🤖 Trợ lý AI/🪓 Công Cụ Xóa Cây Nhanh (Tree Eraser Tool)")]
    public static void ShowWindow()
    {
        var win = GetWindow<TreeEraserTool>("🪓 Xóa Cây Nhanh");
        win.minSize = new Vector2(340, 260);
        win.Show();
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.duringSceneGui += OnSceneGUI;
        FindActiveTerrain();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        isToolActive = false;
    }

    private static void FindActiveTerrain()
    {
        if (targetTerrain == null) targetTerrain = Terrain.activeTerrain;
        if (targetTerrain == null)
        {
            var all = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include);
            if (all.Length > 0) targetTerrain = all[0];
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("🪓 CÔNG CỤ XÓA CÂY NHANH", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Vì cây trên Sa Bàn là cây Terrain (không phải GameObject thông thường), không thể bấm Delete để xóa. Hãy dùng công cụ này để click xóa bất kỳ cây nào bạn muốn!", MessageType.Info);

        EditorGUILayout.Space(5);
        targetTerrain = (Terrain)EditorGUILayout.ObjectField("Terrain Sa Bàn:", targetTerrain, typeof(Terrain), true);
        if (targetTerrain == null)
        {
            if (GUILayout.Button("🔍 Tự Động Tìm Terrain")) FindActiveTerrain();
            return;
        }

        EditorGUILayout.Space(5);
        brushRadius = EditorGUILayout.Slider("Bán kính xóa (mét):", brushRadius, 1f, 25f);
        EditorGUILayout.HelpBox("• 1m - 3m: Xóa chính xác từng cây một lẻ loi.\n• 10m - 25m: Dọn sạch nhanh cả một vạt rừng.", MessageType.None);

        EditorGUILayout.Space(10);
        GUI.backgroundColor = isToolActive ? new Color(1f, 0.4f, 0.4f) : new Color(0.4f, 1f, 0.4f);
        string btnText = isToolActive ? "🛑 ĐANG BẬT XÓA CÂY (Bấm để TẮT)" : "▶️ BẬT CHẾ ĐỘ CLICK XÓA CÂY";
        if (GUILayout.Button(btnText, GUILayout.Height(38)))
        {
            isToolActive = !isToolActive;
            SceneView.RepaintAll();
        }
        GUI.backgroundColor = Color.white;

        if (isToolActive)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox("💡 HƯỚNG DẪN:\n1. Sang cửa sổ SCENE VIEW, bạn sẽ thấy vòng tròn màu đỏ theo chuột.\n2. BẤM CHUỘT TRÁI vào cây nào là cây đó BIẾN MẤT ngay lập tức!\n3. Bấm Ctrl + Z nếu lỡ tay xóa nhầm.", MessageType.Warning);
        }
    }

    private static void OnSceneGUI(SceneView sceneView)
    {
        if (!isToolActive) return;
        if (targetTerrain == null) FindActiveTerrain();
        if (targetTerrain == null || targetTerrain.terrainData == null) return;

        Event e = Event.current;
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

        RaycastHit hit;
        TerrainCollider tCol = targetTerrain.GetComponent<TerrainCollider>();
        bool hasHit = false;
        Vector3 hitPoint = Vector3.zero;

        if (tCol != null && tCol.Raycast(ray, out hit, 10000f))
        {
            hasHit = true;
            hitPoint = hit.point;
        }
        else if (Physics.Raycast(ray, out hit, 10000f))
        {
            hasHit = true;
            hitPoint = hit.point;
        }

        if (hasHit)
        {
            // Vẽ vòng tròn cọ xóa tại vị trí con trỏ chuột
            Handles.color = new Color(1f, 0.2f, 0.2f, 0.7f);
            Handles.DrawWireDisc(hitPoint, Vector3.up, brushRadius);
            Handles.color = new Color(1f, 0.2f, 0.2f, 0.15f);
            Handles.DrawSolidDisc(hitPoint, Vector3.up, brushRadius);

            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

            // Bấm chuột trái để xóa cây
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0)
            {
                EraseTreesAt(hitPoint, brushRadius);
                e.Use();
            }

            sceneView.Repaint();
        }
    }

    private static void EraseTreesAt(Vector3 centerPoint, float radius)
    {
        if (targetTerrain == null || targetTerrain.terrainData == null) return;
        TerrainData td = targetTerrain.terrainData;
        Vector3 tPos = targetTerrain.transform.position;
        Vector3 tSize = td.size;

        TreeInstance[] currentTrees = td.treeInstances;
        if (currentTrees == null || currentTrees.Length == 0) return;

        List<TreeInstance> remainingTrees = new List<TreeInstance>(currentTrees.Length);
        int deletedCount = 0;
        float radiusSqr = radius * radius;

        for (int i = 0; i < currentTrees.Length; i++)
        {
            TreeInstance ti = currentTrees[i];
            Vector3 worldPos = new Vector3(
                tPos.x + ti.position.x * tSize.x,
                tPos.y + ti.position.y * tSize.y,
                tPos.z + ti.position.z * tSize.z
            );

            float distSqr = (worldPos.x - centerPoint.x) * (worldPos.x - centerPoint.x) +
                            (worldPos.z - centerPoint.z) * (worldPos.z - centerPoint.z);

            if (distSqr <= radiusSqr)
            {
                deletedCount++;
            }
            else
            {
                remainingTrees.Add(ti);
            }
        }

        if (deletedCount > 0)
        {
            Undo.RegisterCompleteObjectUndo(td, "Erase Terrain Trees");
            td.SetTreeInstances(remainingTrees.ToArray(), true);
            Debug.Log($"🪓 Đã xóa {deletedCount} cây tại vị trí ({centerPoint.x:F1}, {centerPoint.z:F1}). Bấm Ctrl + Z nếu muốn hoàn tác!");
        }
    }
}
