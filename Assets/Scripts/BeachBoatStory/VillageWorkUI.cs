using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BeachBoatStory
{
    /// <summary>Small contextual HUD; the world and third-person controls remain visible.</summary>
    public sealed class VillageWorkUI : MonoBehaviour
    {
        static VillageWorkUI instance;
        GameObject root;TMP_Text header,step,record;
        static readonly Color Gold=new Color(1,.76f,.33f);
        public static void EnsureExists()
        {
            if(instance!=null)return;
            var go=new GameObject("VillageWorkCanvas",typeof(Canvas),typeof(CanvasScaler));
            go.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;go.GetComponent<Canvas>().sortingOrder=240;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
            instance=go.AddComponent<VillageWorkUI>();instance.Build();
        }
        public bool IsVisible=>root!=null&&root.activeSelf;
        TMP_Text Text(string name,Vector2 min,Vector2 max,int size,TextAlignmentOptions align)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(root.transform,false);
            var rt=go.GetComponent<RectTransform>();rt.anchorMin=min;rt.anchorMax=max;rt.offsetMin=rt.offsetMax=Vector2.zero;
            var bg=go.GetComponent<Image>();bg.color=new Color(.025f,.045f,.04f,.82f);bg.raycastTarget=false;
            var text=new GameObject("Text",typeof(RectTransform)).AddComponent<TextMeshProUGUI>();text.transform.SetParent(go.transform,false);StoryFont.Apply(text);
            var tr=text.rectTransform;tr.anchorMin=Vector2.zero;tr.anchorMax=Vector2.one;tr.offsetMin=new Vector2(12,6);tr.offsetMax=new Vector2(-12,-6);
            text.fontSize=size;text.color=Gold;text.alignment=align;text.raycastTarget=false;text.textWrappingMode=TextWrappingModes.Normal;if(size>=20){text.enableAutoSizing=true;text.fontSizeMin=16;text.fontSizeMax=size;}return text;
        }
        void Build()
        {
            root=new GameObject("PhysicalWorkHUD",typeof(RectTransform));root.transform.SetParent(transform,false);var rt=root.GetComponent<RectTransform>();rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;
            header=Text("Title",new Vector2(.025f,.83f),new Vector2(.35f,.96f),20,TextAlignmentOptions.TopLeft);
            step=Text("Action",new Vector2(.23f,.045f),new Vector2(.77f,.15f),20,TextAlignmentOptions.Center);
            record=Text("Rank",new Vector2(.025f,.75f),new Vector2(.35f,.825f),12,TextAlignmentOptions.Center);root.SetActive(false);
        }
        void Update()
        {
            var s=VillageWorkStall.Active;root.SetActive(s!=null&&!BachDangFrontEnd.IsBlocking&&!CampaignJournalUI.IsOpen);if(s==null)return;
            header.text=CampaignProgress.WorkNames[(int)s.kind].ToUpperInvariant()+"\n"+$"Đã giao {s.Progress}/{s.Goal} • Còn {s.TimeRemaining:0}s";
            int index=(int)s.kind+1;record.text=$"S ≤ {CampaignProgress.Clock(CampaignProgress.MiniTarget(index,3))} • A ≤ {CampaignProgress.Clock(CampaignProgress.MiniTarget(index,2))} • B ≤ {CampaignProgress.Clock(CampaignProgress.MiniTarget(index,1))}";
            if(s.Phase==VillageWorkStall.WorkPhase.Won)step.text="ĐỦ VẬT TƯ • ENTER nhận hàng • ESC rời đi";
            else if(s.Phase==VillageWorkStall.WorkPhase.Lost)step.text=s.Feedback+"\nR thử lại • ESC rời đi";
            else if(s.Phase==VillageWorkStall.WorkPhase.Instructions)step.text=s.Physical?.Hint+"\nWASD đi lại • Chuột xoay nhìn • ENTER tiếp tục";
            else
            {
                var work=s.Physical;
                step.text=(work?.Hint??"")+"\n"+(work!=null&&work.AtTable?
                    (work.Finishing&&s.kind==VillageWorkStall.WorkKind.Rope?$"Lực {work.ToolPressure*100:0}% • thả ở 55–85%":work.ToolFeedback)+" • H hướng dẫn • ESC rời sạp":
                    "WASD di chuyển • H xem điều khiển • ESC rời sạp");
            }
        }
        void OnDestroy(){if(instance==this)instance=null;}
    }
}
