using UnityEngine;
using UnityEditor;

public class CleanupCampTool : EditorWindow
{
    [MenuItem("BachDang/Xóa Sạch Rào Cũ")]
    public static void CleanUp()
    {
        int count = 0;
        // Xóa tất cả các rào cũ có tên tiếng Việt hoặc tiếng Anh
        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (GameObject go in allObjects)
        {
            if (go.name == "--- HÀNG RÀO DOANH TRẠI ---" || go.name == "--- HANG RAO DOANH TRAI ---")
            {
                Undo.DestroyObjectImmediate(go);
                count++;
            }
        }
        
        EditorUtility.DisplayDialog("Dọn dẹp", $"Đã xóa thành công {count} khung rào cũ bị kẹt trong scene!", "OK");
    }
}
