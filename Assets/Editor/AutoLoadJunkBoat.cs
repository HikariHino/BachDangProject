using UnityEngine;
using UnityEditor;

public class AutoLoadJunkBoat : MonoBehaviour
{
    [MenuItem("🤖 Trợ lý AI/🛶 Gọi Thuyền Cổ Ra!")]
    public static void LoadBoat()
    {
        // Đường dẫn chính xác tới file con thuyền
        string assetPath = "Assets/junk/source/Sketchfab_2013_12_04_23_35_26.fbx";
        
        // Tải mô hình 3D từ thư mục
        GameObject boatPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        
        if (boatPrefab != null)
        {
            // Ném nó ra giữa Scene
            GameObject boatInstance = (GameObject)PrefabUtility.InstantiatePrefab(boatPrefab);
            
            if (boatInstance != null)
            {
                // Cho phép người dùng bấm Ctrl+Z để hoàn tác nếu muốn
                Undo.RegisterCreatedObjectUndo(boatInstance, "Gọi Thuyền AI");
                
                // Tự động click chọn con thuyền
                Selection.activeGameObject = boatInstance;
                
                // Tự động lia Camera tới góc nhìn con thuyền
                if (SceneView.lastActiveSceneView != null)
                {
                    SceneView.lastActiveSceneView.FrameSelected();
                }
                
                Debug.Log("🤖 Trợ lý AI: Đã triệu hồi thuyền thành công!");
            }
        }
        else
        {
            Debug.LogError("🤖 Trợ lý AI: Không tìm thấy thuyền tại: " + assetPath);
        }
    }
}
