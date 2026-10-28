using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BeachBoatStory
{
    /// <summary>Small contextual HUD; the world and third-person controls remain visible.</summary>
    public sealed class VillageWorkUI : MonoBehaviour
    {
        static VillageWorkUI instance;
        GameObject root,pressurePanel,ricePanel;TMP_Text header,step,record,pressureLabel,riceLabel;RectTransform pressureCursor,riceLevel;
        Button liftRice,trimRice,emptyRice;
        public bool TensionGaugeVisible=>pressurePanel!=null&&pressurePanel.activeInHierarchy;
        public bool RiceGaugeVisible=>ricePanel!=null&&ricePanel.activeInHierarchy;
        public string RiceStatus=>riceLabel?.text;
        static readonly Color Gold=new Color(1,.76f,.33f);
        public static void EnsureExists()
        {
            if(instance!=null)return;
            var go=new GameObject("VillageWorkCanvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
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
            pressureLabel=Text("TensionLabel",new Vector2(.29f,.265f),new Vector2(.71f,.31f),16,TextAlignmentOptions.Center);
            pressurePanel=new GameObject("TensionGauge",typeof(RectTransform),typeof(Image));pressurePanel.transform.SetParent(root.transform,false);
            var rail=pressurePanel.GetComponent<RectTransform>();rail.anchorMin=new Vector2(.3f,.205f);rail.anchorMax=new Vector2(.7f,.26f);rail.offsetMin=rail.offsetMax=Vector2.zero;
            pressurePanel.GetComponent<Image>().color=new Color(.03f,.04f,.035f,.95f);pressurePanel.GetComponent<Image>().raycastTarget=false;
            Image Band(string name,float a,float b,Color color)
            {
                var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(rail,false);var rect=go.GetComponent<RectTransform>();rect.anchorMin=new Vector2(a,.15f);rect.anchorMax=new Vector2(b,.85f);rect.offsetMin=rect.offsetMax=Vector2.zero;
                var image=go.GetComponent<Image>();image.color=color;image.raycastTarget=false;return image;
            }
            Band("TooLoose",0,.55f,new Color(.5f,.35f,.08f));Band("ReleaseInGreen",.55f,.85f,new Color(.13f,.82f,.38f));Band("TooTight",.85f,1,new Color(.65f,.15f,.08f));
            pressureCursor=Band("ForceCursor",0,0,Color.white).rectTransform;pressureCursor.sizeDelta=new Vector2(7,8);
            riceLabel=Text("RiceStatus",new Vector2(.28f,.285f),new Vector2(.72f,.34f),19,TextAlignmentOptions.Center);
            ricePanel=new GameObject("RiceControls",typeof(RectTransform));ricePanel.transform.SetParent(root.transform,false);
            var panel=ricePanel.GetComponent<RectTransform>();panel.anchorMin=new Vector2(.28f,.16f);panel.anchorMax=new Vector2(.72f,.28f);panel.offsetMin=panel.offsetMax=Vector2.zero;
            var gauge=new GameObject("FillGauge",typeof(RectTransform),typeof(Image));gauge.transform.SetParent(panel,false);
            var gaugeRect=gauge.GetComponent<RectTransform>();gaugeRect.anchorMin=new Vector2(0,.74f);gaugeRect.anchorMax=Vector2.one;gaugeRect.offsetMin=gaugeRect.offsetMax=Vector2.zero;gauge.GetComponent<Image>().color=new Color(.06f,.09f,.08f,.95f);gauge.GetComponent<Image>().raycastTarget=false;
            Image Fill(string name,float a,float b,Color color)
            {
                var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(gaugeRect,false);var rect=go.GetComponent<RectTransform>();rect.anchorMin=new Vector2(a,.1f);rect.anchorMax=new Vector2(b,.9f);rect.offsetMin=rect.offsetMax=Vector2.zero;var image=go.GetComponent<Image>();image.color=color;image.raycastTarget=false;return image;
            }
            Fill("TargetRange",.65f/1.2f,.85f/1.2f,new Color(.13f,.65f,.3f));
            riceLevel=Fill("CurrentFill",0,0,Gold).rectTransform;
            foreach(float x in new[]{.65f/1.2f,.85f/1.2f}){var mark=Fill("EnoughBoundary",x,x,Color.white).rectTransform;mark.sizeDelta=new Vector2(3,5);}
            Button RiceButton(string label,float a,float b,System.Action<VillagePhysicalWork> action)
            {
                var text=Text(label,new Vector2(a,0),new Vector2(b,.6f),16,TextAlignmentOptions.Center);var go=text.transform.parent.gameObject;go.transform.SetParent(panel,false);
                var image=go.GetComponent<Image>();image.raycastTarget=true;image.color=new Color(.12f,.25f,.21f,.98f);var button=go.AddComponent<Button>();button.targetGraphic=image;button.navigation=new Navigation{mode=Navigation.Mode.None};text.text=label;
                button.onClick.AddListener(()=>{var work=VillagePhysicalWork.Active;if(work!=null)action(work);UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);});return button;
            }
            liftRice=RiceButton("NHẤC BAO [E / ENTER]",0,.48f,w=>w.Confirm());trimRice=RiceButton("BỚT GẠO [R]",.5f,.75f,w=>w.TrimRice());emptyRice=RiceButton("ĐỔ LẠI [C]",.77f,1,w=>w.EmptyRice());
        }
        void Update()
        {
            var s=VillageWorkStall.Active;root.SetActive(s!=null&&!BachDangFrontEnd.IsBlocking&&!CampaignJournalUI.IsOpen);if(s==null)return;
            bool show=s.Physical!=null&&s.Physical.AtTable&&s.kind==VillageWorkStall.WorkKind.Rope;
            pressurePanel.SetActive(show);pressureLabel.transform.parent.gameObject.SetActive(show);
            var rice=s.Physical;bool showRice=rice!=null&&rice.AtTable&&s.kind==VillageWorkStall.WorkKind.Rice;
            ricePanel.SetActive(showRice);riceLabel.transform.parent.gameObject.SetActive(showRice);
            if(showRice)
            {
                Color state=rice.RiceReady?new Color(.35f,1,.5f):rice.RiceExcess?new Color(1,.3f,.2f):Gold;
                riceLabel.text=rice.RiceReady?"ĐỦ GẠO • NHẤC BAO ĐỂ GIAO":rice.RiceExcess?"DƯ GẠO • BỚT MỘT GÁO [R]":"THIẾU GẠO • XÚC THÊM TỪ RỔ";riceLabel.color=state;
                riceLevel.anchorMax=new Vector2(Mathf.Clamp01(rice.RiceFill/1.2f),.9f);riceLevel.GetComponent<Image>().color=state;
                liftRice.interactable=rice.RiceReady;trimRice.interactable=emptyRice.interactable=rice.RiceFill>0;
            }
            if(show)
            {
                pressureLabel.text=s.Physical.Finishing?"LỰC SIẾT • THẢ CHUỘT TRONG DẢI XANH":"QUẤN ĐỦ VÒNG DÂY RỒI SIẾT";
                float x=s.Physical.ToolPressure;pressureCursor.anchorMin=new Vector2(x,.05f);pressureCursor.anchorMax=new Vector2(x,.95f);
                pressureCursor.GetComponent<Image>().color=x>=.55f&&x<=.85f?new Color(.7f,1,.75f):Color.white;
            }
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
