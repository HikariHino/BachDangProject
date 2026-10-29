using UnityEngine;
namespace BeachBoatStory
{
    /// <summary>Three separate bottom HUD zones, measured in the same scaled canvas units.</summary>
    public sealed class GameplayHUDLayout : MonoBehaviour
    {
        RectTransform canvas,vitals,controls,prompt,hull,impact;Vector2 previous;
        public void Initialize(RectTransform health,RectTransform help,RectTransform interaction,RectTransform durability,RectTransform notice)
        {canvas=(RectTransform)transform;vitals=health;controls=help;prompt=interaction;hull=durability;impact=notice;Apply();}
        static void Dock(RectTransform rect,Vector2 anchor,Vector2 pivot,Vector2 at,Vector2 size){rect.anchorMin=rect.anchorMax=anchor;rect.pivot=pivot;rect.anchoredPosition=at;rect.sizeDelta=size;}
        void LateUpdate(){if(canvas!=null&&canvas.rect.size!=previous)Apply();}
        void Apply()
        {
            if(canvas==null)return;previous=canvas.rect.size;float width=Mathf.Max(1,previous.x);
            Dock(vitals,Vector2.zero,Vector2.zero,new Vector2(16,20),new Vector2(Mathf.Min(282,width*.33f),96));
            Dock(controls,Vector2.right,Vector2.right,new Vector2(-16,20),new Vector2(Mathf.Min(430,width*.38f),86));
            Dock(hull,Vector2.zero,Vector2.zero,new Vector2(16,126),new Vector2(Mathf.Min(282,width*.33f),32));
            Dock(impact,Vector2.zero,Vector2.zero,new Vector2(16,164),new Vector2(Mathf.Min(330,width*.36f),32));
            Dock(prompt,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,208),new Vector2(Mathf.Min(520,width*.7f),40));
        }
    }
}
