using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace BeachBoatStory
{
    public sealed class HealingItemHUD : MonoBehaviour
    {
        GameObject root;Image tile;TMP_Text count,notice;PlayerVitals vitals;
        void Start()
        {
            vitals=BeachBoatQuestSession.Instance.player.GetComponent<PlayerVitals>();root=new GameObject("HealingItemHUD",typeof(Canvas),typeof(CanvasScaler));root.transform.SetParent(transform,false);root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;root.GetComponent<Canvas>().sortingOrder=112;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
            var slot=Box("Healing_item_slot",root.transform,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(-36,16),new Vector2(36,88),new Color(.035f,.1f,.09f,.95f));tile=slot.GetComponent<Image>();
            Box("Border",slot.transform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,new Color(.8f,.64f,.32f));Box("Inset",slot.transform,Vector2.zero,Vector2.one,new Vector2(2,2),new Vector2(-2,-2),new Color(.045f,.11f,.095f));
            var roll=Box("Medicine_roll",slot.transform,new Vector2(.18f,.28f),new Vector2(.82f,.76f),Vector2.zero,Vector2.zero,new Color(.84f,.81f,.67f));roll.transform.localRotation=Quaternion.Euler(0,0,-18);
            for(int i=0;i<4;i++)Box("Bandage_fold",roll.transform,new Vector2(.15f+i*.2f,0),new Vector2(.18f+i*.2f,1),Vector2.zero,Vector2.zero,new Color(.62f,.6f,.49f));
            Box("Herbal_mark_vertical",roll.transform,new Vector2(.44f,.1f),new Vector2(.56f,.9f),Vector2.zero,Vector2.zero,new Color(.27f,.4f,.26f));Box("Herbal_mark_horizontal",roll.transform,new Vector2(.25f,.36f),new Vector2(.75f,.64f),Vector2.zero,Vector2.zero,new Color(.27f,.4f,.26f));
            count=Label("Item_count",slot.transform,new Vector2(.52f,.7f),new Vector2(.97f,.98f),17);var key=Label("Key_H",slot.transform,new Vector2(.05f,.02f),new Vector2(.95f,.26f),16);key.text="[H] THUỐC";
            notice=Label("Healing_notice",root.transform,new Vector2(.29f,.135f),new Vector2(.71f,.185f),20);
        }
        static GameObject Box(string name,Transform parent,Vector2 min,Vector2 max,Vector2 offsetMin,Vector2 offsetMax,Color color)
        {var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);var r=(RectTransform)go.transform;r.anchorMin=min;r.anchorMax=max;r.offsetMin=offsetMin;r.offsetMax=offsetMax;go.GetComponent<Image>().color=color;go.GetComponent<Image>().raycastTarget=false;return go;}
        static TMP_Text Label(string name,Transform parent,Vector2 min,Vector2 max,int size)
        {var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);var t=go.GetComponent<TextMeshProUGUI>();StoryFont.Apply(t);t.fontSize=size;t.alignment=TextAlignmentOptions.Center;t.color=new Color(1,.86f,.6f);t.raycastTarget=false;t.rectTransform.anchorMin=min;t.rectTransform.anchorMax=max;t.rectTransform.offsetMin=t.rectTransform.offsetMax=Vector2.zero;return t;}
        void Update(){if(root==null)return;bool show=BeachBoatQuestSession.Instance.storyMode&&!BeachBoatQuestSession.InteractionBlocked&&ArcheryTraining.Active==null;root.SetActive(show);if(!show)return;count.text="×"+vitals.HealingItems;notice.text=Time.unscaledTime<vitals.HealingFeedbackUntil?vitals.HealingFeedback:"";tile.color=vitals.HealingItems>0?Color.white:Color.gray;}
    }
}
