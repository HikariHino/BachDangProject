using System;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

public static class BeachBoatVietnameseFont
{
    public const string AssetPath = "Assets/BeachBoatStory/Resources/BeachBoatStory/VietnameseSDF.asset";
    public const string Vietnamese = "aàáảãạăằắẳẵặâầấẩẫậeèéẻẽẹêềếểễệiìíỉĩịoòóỏõọôồốổỗộơờớởỡợuùúủũụưừứửữựyỳýỷỹỵđ";

    public static string RequiredCharacters()
    {
        string text = Vietnamese + Vietnamese.ToUpperInvariant() + " • — – … ‘ ’ “ ” → ← ↑ ↓";
        text += text.Normalize(NormalizationForm.FormD);
        text += new string(Enumerable.Range(32, 95).Select(i => (char)i).ToArray());
        foreach (string path in Directory.GetFiles("Assets/Scripts/BeachBoatStory", "*.cs")) text += File.ReadAllText(path, Encoding.UTF8);
        return new string(text.Where(c => !char.IsControl(c) && !char.IsSurrogate(c) && c != '\uFEFF').Distinct().OrderBy(c => c).ToArray());
    }

    [MenuItem("Bach Dang/Create Vietnamese Story Font")]
    public static void Build()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Build the font in Edit Mode.");
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetPath) != null) throw new InvalidOperationException("Font already exists. Validate it before replacing.");
        string root = "Assets/BeachBoatStory";
        if (!AssetDatabase.IsValidFolder(root + "/Resources")) AssetDatabase.CreateFolder(root, "Resources");
        if (!AssetDatabase.IsValidFolder(root + "/Resources/BeachBoatStory")) AssetDatabase.CreateFolder(root + "/Resources", "BeachBoatStory");
        var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
        var font = TMP_FontAsset.CreateFontAsset(source, 48, 6, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
        font.name = "BeachBoat Vietnamese SDF";
        string missing;
        if (!font.TryAddCharacters(RequiredCharacters(), out missing, true))
            throw new InvalidOperationException("Could not bake glyphs: " + string.Join(", ", missing.Select(c => "U+" + ((int)c).ToString("X4"))));
        font.atlasPopulationMode = AtlasPopulationMode.Static;
        font.isMultiAtlasTexturesEnabled = false;
        font.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
        AssetDatabase.CreateAsset(font, AssetPath);
        font.material.name = "BeachBoat Vietnamese SDF Material";
        AssetDatabase.AddObjectToAsset(font.material, font);
        for (int i = 0; i < font.atlasTextures.Length; i++)
        {
            var atlas = font.atlasTextures[i]; atlas.name = "Vietnamese Atlas " + i; atlas.filterMode = FilterMode.Bilinear;
            AssetDatabase.AddObjectToAsset(atlas, font); EditorUtility.SetDirty(atlas);
        }
        EditorUtility.SetDirty(font.material); EditorUtility.SetDirty(font); AssetDatabase.SaveAssets();
        var settings = ScriptableObject.CreateInstance<BeachBoatStory.StoryFontSettings>();
        settings.legacyFont = source;
        AssetDatabase.CreateAsset(settings, "Assets/BeachBoatStory/Resources/BeachBoatStory/FontSettings.asset");
        AssetDatabase.SaveAssets();
        Debug.Log($"Vietnamese font: {font.characterTable.Count} characters, {font.atlasTextures.Length} atlas(es), no runtime glyph generation.");
    }
}
