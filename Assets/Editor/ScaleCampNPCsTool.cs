using UnityEngine;
using UnityEditor;

public class ScaleCampNPCsTool : EditorWindow
{
    [MenuItem("BachDang/Phóng To Lính Trong Trại (Scale 7x)")]
    public static void ScaleNPCs()
    {
        // Tìm cụm doanh trại mà bạn đã copy từ Khôi sang
        GameObject camp = GameObject.Find("--- CHIẾN TRƯỜNG: ĐẠI BẢN DOANH & XƯỞNG CỌC BẠCH ĐẰNG ---");
        if (camp == null)
        {
            EditorUtility.DisplayDialog("Lỗi", "Không tìm thấy Doanh trại gốc!", "OK");
            return;
        }

        int count = 0;
        // Tìm tất cả các nhân vật (có gắn Animator) bên trong doanh trại
        Animator[] animators = camp.GetComponentsInChildren<Animator>();
        
        Undo.RegisterFullObjectHierarchyUndo(camp, "Phóng to lính");

        foreach (Animator anim in animators)
        {
            Transform npcTransform = anim.transform;
            
            // Nếu nhân vật chưa được bọc
            if (npcTransform.parent != null && !npcTransform.parent.name.EndsWith("_Wrapper"))
            {
                // 1. Tạo một cái Vỏ bọc (Empty GameObject)
                GameObject wrapper = new GameObject(npcTransform.name + "_Wrapper");
                
                // 2. Đặt Vỏ bọc vào đúng vị trí của lính
                wrapper.transform.SetParent(npcTransform.parent);
                wrapper.transform.position = npcTransform.position;
                wrapper.transform.rotation = npcTransform.rotation;
                
                // 3. Nhét lính vào trong Vỏ bọc
                npcTransform.SetParent(wrapper.transform);
                
                // 4. Scale cái Vỏ bọc lên 7 lần
                wrapper.transform.localScale = new Vector3(7f, 7f, 7f);
                count++;
            }
            else if (npcTransform.parent != null && npcTransform.parent.name.EndsWith("_Wrapper"))
            {
                // Nếu đã bọc rồi thì chỉ cập nhật Scale
                npcTransform.parent.localScale = new Vector3(7f, 7f, 7f);
                count++;
            }
        }

        // Tự động Scale các vật thể props (vũ khí, thùng) rơi vãi xung quanh
        MeshRenderer[] meshes = camp.GetComponentsInChildren<MeshRenderer>();
        foreach (MeshRenderer mesh in meshes)
        {
            // Tránh scale Terrain hoặc mấy cái quá bự
            if (mesh.transform.parent != null && mesh.transform.localScale == Vector3.one)
            {
                if (!mesh.GetComponentInParent<Animator>()) // Tránh đụng vào lính đã scale ở trên
                {
                    mesh.transform.localScale = new Vector3(7f, 7f, 7f);
                }
            }
        }

        EditorUtility.DisplayDialog("Hoàn tất", $"Đã dùng kỹ thuật 'Vỏ Bọc' để phóng to {count} lính canh và các vật phẩm lên gấp 7 lần!", "OK");
    }
}
