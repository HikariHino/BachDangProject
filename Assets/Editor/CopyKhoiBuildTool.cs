using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public class CopyKhoiBuildTool : EditorWindow
{
    [MenuItem("BachDang/Đem Doanh Trại của Khôi sang TestQuest")]
    public static void CopyKhoiObjects()
    {
        // 1. Lưu scene hiện tại (TestQuest) để an toàn tuyệt đối
        Scene currentScene = EditorSceneManager.GetActiveScene();
        if (currentScene.name != "TestQuest")
        {
            bool proceed = EditorUtility.DisplayDialog("Cảnh báo", 
                "Bạn đang không ở scene TestQuest! Bạn có chắc muốn chép Doanh trại vào scene hiện tại (" + currentScene.name + ") không?", 
                "Có, chép vào đây luôn", "Thôi, để tôi mở TestQuest");
            if (!proceed) return;
        }
        
        // Save scene hiện tại trước khi làm để có biến gì còn quay lại được
        EditorSceneManager.SaveScene(currentScene);

        // 2. Mở Scene beachBoat dạng Additive (Mở song song, không làm mất TestQuest)
        string beachBoatPath = "Assets/Scenes/beachBoat.unity";
        Scene beachBoatScene = EditorSceneManager.OpenScene(beachBoatPath, OpenSceneMode.Additive);

        if (!beachBoatScene.IsValid())
        {
            EditorUtility.DisplayDialog("Lỗi", "Không tìm thấy file scene beachBoat!", "OK");
            return;
        }

        // 3. Tìm các cụm Object Doanh trại & Hạm đội của Khôi trong beachBoat
        GameObject campObj = null;
        GameObject fleetObj = null;

        GameObject[] rootObjs = beachBoatScene.GetRootGameObjects();
        foreach (GameObject go in rootObjs)
        {
            if (go.name.Contains("CHIẾN TRƯỜNG: ĐẠI BẢN DOANH"))
                campObj = go;
            if (go.name.Contains("HẠM ĐỘI THUYỀN CHIẾN"))
                fleetObj = go;
        }

        // 4. Nhân bản (Copy) sang scene TestQuest
        bool copiedAnything = false;

        if (campObj != null)
        {
            GameObject newCamp = Instantiate(campObj);
            newCamp.name = campObj.name; // Xóa chữ (Clone) đi cho đẹp
            SceneManager.MoveGameObjectToScene(newCamp, currentScene);
            copiedAnything = true;
        }

        if (fleetObj != null)
        {
            GameObject newFleet = Instantiate(fleetObj);
            newFleet.name = fleetObj.name;
            SceneManager.MoveGameObjectToScene(newFleet, currentScene);
            copiedAnything = true;
        }

        // 5. Đóng beachBoat lại, KHÔNG lưu beachBoat để giữ nguyên 100% bản gốc của Khôi
        EditorSceneManager.CloseScene(beachBoatScene, true);

        // 6. Hoàn tất
        if (copiedAnything)
        {
            EditorSceneManager.MarkSceneDirty(currentScene);
            EditorUtility.DisplayDialog("Hoàn hảo!", 
                "Tôi đã copy nguyên xi cụm Doanh Trại (và Thuyền chiến) của Khôi sang TestQuest cho bạn!\n\n" +
                "- Bản gốc beachBoat vẫn nguyên vẹn 100%.\n" +
                "- Scene TestQuest của bạn không bị mất bất cứ thứ gì.\n\n" +
                "Doanh trại thường nằm ở tọa độ X: -820, Z: 320. Bạn hãy zoom Camera ra đó xem nhé!", 
                "Tuyệt vời");
        }
        else
        {
            EditorUtility.DisplayDialog("Báo cáo", "Không tìm thấy Cụm Doanh trại trong beachBoat. Có thể nó nằm trong một Folder khác.", "OK");
        }
    }
}
