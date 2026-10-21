using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BeachBoatStory
{
    /// <summary>
    /// Lightweight whole-map overview. The terrain is sampled into one 256px texture once;
    /// no second camera, extra culling pass or per-frame texture allocation is used.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BeachBoatMinimap : MonoBehaviour
    {
        const int TextureSize = 256;
        Transform player, mainCamp;
        Terrain terrain;
        RectTransform panel, mapRect, playerMarker, campMarker;
        Texture2D mapTexture;
        bool visible;

        public void Initialize(Transform playerTransform)
        {
            player = playerTransform;
            terrain = Terrain.activeTerrain;
            var camp = GameObject.Find("05_Trai_Du_Bi");
            mainCamp = camp != null ? camp.transform : null;
            if (player == null || terrain == null)
            {
                Debug.LogWarning("Minimap could not find the player or active terrain.", this);
                enabled = false;
                return;
            }
            BuildUI();
            BuildTerrainTexture();
            UpdateMarkers();
            panel.gameObject.SetActive(false);
        }

        void Update()
        {
            if (BeachBoatQuestSession.InteractionBlocked || BeachBoatMinigameStation.Active != null)
            { panel.gameObject.SetActive(false); return; }
            panel.gameObject.SetActive(visible);
            if (Input.GetKeyDown(KeyCode.M))
            {
                visible = !visible;
                panel.gameObject.SetActive(visible);
            }
            if (visible) UpdateMarkers();
        }

        void BuildUI()
        {
            var shell = new GameObject("Minimap", typeof(RectTransform), typeof(Image));
            shell.transform.SetParent(transform, false);
            panel = (RectTransform)shell.transform;
            panel.anchorMin = new Vector2(.79f, .085f);
            panel.anchorMax = new Vector2(.985f, .43f);
            panel.offsetMin = panel.offsetMax = Vector2.zero;
            shell.GetComponent<Image>().color = new Color(.025f, .035f, .032f, .92f);

            var title = CreateText("Title", panel, new Vector2(.04f, .89f), new Vector2(.96f, .99f), 16);
            title.text = "BẢN ĐỒ  •  [M] ẨN";
            title.alignment = TextAlignmentOptions.Center;
            title.color = new Color(1f, .84f, .42f);

            var map = new GameObject("TerrainMap", typeof(RectTransform), typeof(RawImage), typeof(RectMask2D));
            map.transform.SetParent(panel, false);
            mapRect = (RectTransform)map.transform;
            mapRect.anchorMin = new Vector2(.045f, .08f);
            mapRect.anchorMax = new Vector2(.955f, .88f);
            mapRect.offsetMin = mapRect.offsetMax = Vector2.zero;
            map.GetComponent<RawImage>().color = Color.white;

            var north = CreateText("North", mapRect, new Vector2(.43f, .89f), new Vector2(.57f, .99f), 17);
            north.text = "BẮC ▲";
            north.alignment = TextAlignmentOptions.Center;
            north.color = new Color(1f, .88f, .5f);

            playerMarker = Marker("Player", mapRect, "▲", new Color(1f, .76f, .16f), 24);
            campMarker = Marker("MainCamp", mapRect, "●", new Color(.95f, .34f, .23f), 20);
            var legend = CreateText("Legend", panel, new Vector2(.04f, .005f), new Vector2(.96f, .075f), 13);
            legend.text = "▲ Bạn     ● Đại bản doanh";
            legend.alignment = TextAlignmentOptions.Center;
            legend.color = new Color(.9f, .92f, .86f);
        }

        static TextMeshProUGUI CreateText(string name, Transform parent, Vector2 min, Vector2 max, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            StoryFont.Apply(text); text.fontSize = size; text.raycastTarget = false;
            var rect = text.rectTransform; rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return text;
        }

        static RectTransform Marker(string name, Transform parent, string glyph, Color color, float size)
        {
            var text = CreateText(name, parent, Vector2.zero, Vector2.zero, size);
            text.text = glyph; text.color = color; text.alignment = TextAlignmentOptions.Center;
            text.fontStyle = FontStyles.Bold;
            text.rectTransform.sizeDelta = new Vector2(34, 34);
            return text.rectTransform;
        }

        void BuildTerrainTexture()
        {
            var data = terrain.terrainData;
            mapTexture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGB24, false, true)
            { name = "BeachBoat_Minimap_256", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[TextureSize * TextureSize];
            float water = TideSystem.Instance != null ? TideSystem.CurrentWaterHeight : 108.5f;
            for (int y = 0; y < TextureSize; y++)
            for (int x = 0; x < TextureSize; x++)
            {
                float nx = x / (TextureSize - 1f), nz = y / (TextureSize - 1f);
                float height = data.GetInterpolatedHeight(nx, nz) + terrain.transform.position.y;
                Color color;
                if (height <= water + 1.5f)
                {
                    float depth = Mathf.Clamp01((water + 20f - height) / 55f);
                    color = Color.Lerp(new Color(.22f,.55f,.53f), new Color(.055f,.25f,.32f), depth);
                }
                else
                {
                    float altitude = Mathf.Clamp01((height - water) / 440f);
                    color = Color.Lerp(new Color(.48f,.50f,.27f), new Color(.22f,.30f,.18f), altitude);
                    if (height < water + 12f) color = Color.Lerp(new Color(.68f,.61f,.39f), color, (height-water)/12f);
                }
                pixels[y * TextureSize + x] = color;
            }
            mapTexture.SetPixels32(pixels); mapTexture.Apply(false, true);
            mapRect.GetComponent<RawImage>().texture = mapTexture;
        }

        void UpdateMarkers()
        {
            if (player == null || terrain == null || mapRect == null) return;
            Place(playerMarker, player.position);
            playerMarker.localRotation = Quaternion.Euler(0, 0, -player.eulerAngles.y);

            if (mainCamp != null) Place(campMarker, mainCamp.position);
            else campMarker.gameObject.SetActive(false);
        }

        void Place(RectTransform marker, Vector3 world)
        {
            var origin = terrain.transform.position; var size = terrain.terrainData.size;
            float nx = Mathf.Clamp01((world.x - origin.x) / size.x);
            float nz = Mathf.Clamp01((world.z - origin.z) / size.z);
            marker.anchorMin = marker.anchorMax = new Vector2(nx, nz);
            marker.anchoredPosition = Vector2.zero;
        }

        void OnDestroy()
        {
            if (mapTexture != null) Destroy(mapTexture);
        }
    }
}
