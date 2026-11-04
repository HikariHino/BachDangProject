using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BeachBoatStory
{
    /// <summary>Shared, resolution-independent visual language for the in-game HUD.</summary>
    public static class CampaignHUDTheme
    {
        public static readonly Color Ink=new Color(.025f,.054f,.058f,.84f),Gold=new Color(.87f,.72f,.43f),Paper=new Color(.94f,.94f,.86f),Muted=new Color(.63f,.73f,.71f);
        static Sprite plain,marked;
        static Sprite Surface(bool accent)
        {
            if(accent&&marked!=null)return marked;if(!accent&&plain!=null)return plain;
            const int size=32;var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){name="Campaign_HUD_corner",hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            var pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                bool cut=x+y<5||(size-1-x)+(size-1-y)<5;
                var tint=cut?Color.clear:Ink;
                if(!cut&&y==size-2&&x>6&&x<size-6)tint=new Color(.51f,.62f,.55f,.6f);
                if(accent&&!cut&&x<3&&y>6&&y<size-6)tint=Gold;
                pixels[y*size+x]=tint;
            }
            texture.SetPixels(pixels);texture.Apply();var sprite=Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(8,8,8,8));sprite.name="Campaign_HUD_"+(accent?"accent":"plain");sprite.hideFlags=HideFlags.HideAndDontSave;
            if(accent)marked=sprite;else plain=sprite;return sprite;
        }
        public static void Plate(GameObject target,bool accent=false)
        {
            var image=target.GetComponent<Image>()??target.AddComponent<Image>();image.enabled=true;image.sprite=Surface(accent);image.type=Image.Type.Sliced;image.color=Color.white;image.raycastTarget=false;
        }
        public static RectTransform Dock(GameObject go,Vector2 anchor,Vector2 pivot,Vector2 at,Vector2 size)
        {var r=(RectTransform)go.transform;r.anchorMin=r.anchorMax=anchor;r.pivot=pivot;r.anchoredPosition=at;r.sizeDelta=size;return r;}
        public static TMP_Text Label(string name,Transform parent,Vector2 at,Vector2 size,int fontSize,string value="")
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);Dock(go,Vector2.up,Vector2.up,at,size);
            var text=go.GetComponent<TextMeshProUGUI>();StoryFont.Apply(text);text.text=value;text.fontSize=fontSize;text.color=Paper;text.raycastTarget=false;text.textWrappingMode=TextWrappingModes.Normal;return text;
        }
    }
}
