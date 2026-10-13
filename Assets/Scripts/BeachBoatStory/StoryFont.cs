using TMPro;
using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Shared, pre-baked Vietnamese glyphs; independent of the global TMP dynamic fallback.</summary>
    public static class StoryFont
    {
        static TMP_FontAsset font;
        static StoryFontSettings settings;

        public static Font LegacyFont
        {
            get
            {
                if (settings == null) settings = Resources.Load<StoryFontSettings>("BeachBoatStory/FontSettings");
                return settings != null ? settings.legacyFont : null;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() { font = null; settings = null; }

        public static void Apply(TMP_Text text)
        {
            if (font == null) font = Resources.Load<TMP_FontAsset>("BeachBoatStory/VietnameseSDF");
            if (font == null)
            {
                Debug.LogError("Missing pre-baked beachBoat Vietnamese font.", text);
                return;
            }
            text.font = font;
            text.fontSharedMaterial = font.material;
            text.raycastTarget = false;
            text.extraPadding = true;
        }
    }
}
