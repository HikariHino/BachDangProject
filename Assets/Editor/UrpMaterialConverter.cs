using UnityEngine;
using UnityEditor;
using System.IO;

public static class UrpMaterialConverter
{
    [MenuItem("🤖 Trợ lý AI/🎨 Chuyển Toàn Bộ Material Props & Buildings Sang URP Lit")]
    public static void ConvertAllPropsMaterialsToURP()
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
        {
            Debug.LogError("❌ Không tìm thấy shader 'Universal Render Pipeline/Lit'!");
            return;
        }

        string[] searchFolders = new string[]
        {
            "Assets/Models/Props/Materials",
            "Assets/Models/Buildings/Materials",
            "Assets/Prefabs"
        };

        string[] guids = AssetDatabase.FindAssets("t:Material", searchFolders);
        int convertedCount = 0;

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;

            // Kiểm tra nếu shader hiện tại không phải là URP Lit hoặc bị lỗi ShaderGraph/Hidden
            if (mat.shader != urpLit)
            {
                Texture mainTex = mat.mainTexture;
                if (mainTex == null && mat.HasProperty("_BaseMap")) mainTex = mat.GetTexture("_BaseMap");
                if (mainTex == null && mat.HasProperty("_MainTex")) mainTex = mat.GetTexture("_MainTex");

                Texture normalMap = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
                Color baseColor = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : (mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white);

                mat.shader = urpLit;

                if (mainTex != null)
                {
                    mat.SetTexture("_BaseMap", mainTex);
                    mat.SetTexture("_MainTex", mainTex);
                }
                if (normalMap != null)
                {
                    mat.SetTexture("_BumpMap", normalMap);
                    mat.EnableKeyword("_NORMALMAP");
                }
                mat.SetColor("_BaseColor", baseColor);
                mat.SetColor("_Color", baseColor);

                EditorUtility.SetDirty(mat);
                convertedCount++;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"🎨 Đã chuyển đổi thành công {convertedCount} Material trong Assets/Models sang URP Lit chuẩn!");
    }
}
