using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BeachBoatStory
{
    [DefaultExecutionOrder(-800)]
    public sealed class CampaignJournalUI : MonoBehaviour
    {
        public static CampaignJournalUI Instance {get;private set;}
        public static bool IsOpen=>Instance!=null&&Instance.overlay!=null&&Instance.overlay.activeSelf;
        public bool IsEnding {get;private set;}
        GameObject overlay,listView,ruleView,side,saveButton,homeButton,closeButton;
        TMP_Text heading,subtitle,metrics,rules,footer,rank,score;
        readonly TMP_Text[] rows=new TMP_Text[15];readonly Image[] rowBackgrounds=new Image[15];readonly JournalCheck[] marks=new JournalCheck[15];
        RectTransform content;ScrollRect scroll;int page;float previousScale=1,nextRefresh;CursorLockMode previousLock;bool previousVisible;
        static readonly Color Gold=new Color(.94f,.76f,.43f),Cream=new Color(.92f,.95f,.91f),Green=new Color(.43f,.94f,.66f),Muted=new Color(.56f,.65f,.64f);
        public void Awake(){Instance=this;Build();}
        void Update()
        {
            var progress=CampaignProgress.Instance;var front=BachDangFrontEnd.Instance;
            if(IsOpen)
            {
                if(!IsEnding&&(Input.GetKeyDown(KeyCode.Tab)||Input.GetKeyDown(KeyCode.Escape))){Close();return;}
                if(Time.unscaledTime>=nextRefresh){nextRefresh=Time.unscaledTime+.25f;Refresh();}return;
            }
            if(front==null||!front.GameStarted||front.Page!=BachDangFrontEnd.MenuPage.None)return;
            if(progress.Record.finished&&!StoryDialogueUI.Instance.IsOpen){Open(true);return;}
            if(Input.GetKeyDown(KeyCode.Tab)&&!OpeningEvent.IsOpeningActive&&!StoryDialogueUI.Instance.IsOpen&&!(QuestCheckpoint.Instance?.IsDefeated??false)&&!(ScreenFader.Instance?.IsTransitioning??false))Open(false);
        }
        public void Open(bool ending=false)
        {
            if(IsOpen)return;IsEnding=ending;previousScale=Time.timeScale;previousLock=Cursor.lockState;previousVisible=Cursor.visible;
            Time.timeScale=0;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;overlay.SetActive(true);ShowPage(0);StoryDialogueUI.TryConsumeInput();
        }
        public void OpenRules(){Open();ShowPage(2);}
        public void Close()
        {
            if(!IsOpen)return;overlay.SetActive(false);Time.timeScale=previousScale;Cursor.lockState=previousLock;Cursor.visible=previousVisible;StoryDialogueUI.TryConsumeInput();
        }
        public void ShowPage(int value){page=value;listView.SetActive(page!=2);ruleView.SetActive(page==2);side.SetActive(page!=2);scroll.verticalNormalizedPosition=1;Refresh();}
        public void SaveResult(){Close();BachDangFrontEnd.Instance.OpenSaveSlots("Tổng kết chiến dịch Bạch Đằng");}
        public void ReturnToTitle(){Close();BachDangSaveSystem.PendingLoad=null;Time.timeScale=1;SceneManager.LoadScene(gameObject.scene.name);}
        public void Refresh()
        {
            var p=CampaignProgress.Instance;if(p==null)return;var r=p.Record;
            heading.text=IsEnding?"ĐẠI THẮNG BẠCH ĐẰNG 938":"NHẬT KÝ CHIẾN DỊCH";
            subtitle.text=$"NHIỆM VỤ {p.CompletedMissions}/{CampaignProgress.StoryOrder.Length}  •  THỜI GIAN {CampaignProgress.Duration(r.seconds)}"+(r.historyIncomplete?"  •  SAVE CŨ: THỐNG KÊ CHƯA ĐẦY ĐỦ":"");
            closeButton.SetActive(!IsEnding);saveButton.SetActive(IsEnding);homeButton.SetActive(IsEnding);
            footer.text=IsEnding?"Lưu kết quả vào một ô trước khi về sảnh để giữ tiến trình và thành tích.":"TAB / ESC: đóng bảng  •  Cuộn chuột: xem toàn bộ danh sách  •  Bảng mở sẽ tạm dừng trò chơi.";
            rules.text=p.RankingRules;
            if(page==0)
            {
                for(int i=0;i<rows.Length;i++)
                {
                    bool done=p.MissionDone(i),current=p.MissionCurrent(i);rows[i].transform.parent.gameObject.SetActive(true);marks[i].Set(done);
                    rowBackgrounds[i].color=done?new Color(.075f,.19f,.14f,.9f):current?new Color(.20f,.21f,.13f,.95f):new Color(.07f,.10f,.105f,.8f);
                    string detail=CampaignProgress.StoryOrder[i]==13?$" ({VillagePreparationQuest.Instance.CompletedCount}/4 sạp)":"";
                    rows[i].text=CampaignProgress.MissionTitles[i]+detail+(done?"  <color=#79E9AC>ĐÃ XONG</color>":current?"  <color=#F0C26D>ĐANG LÀM</color>":"  <color=#90A29D>CHƯA XONG</color>");
                }
            }
            else if(page==1)
            {
                string[] titles={"Luyện quân: xong cả 3 cấp thể lực","Hậu cần: hoàn tất đủ 4 sạp","Trận địa: dựng đủ 5 cụm cọc","Dũng sĩ: tự hạ ít nhất 10 địch","Tiếp viện: leo lên thuyền đồng minh","Bất khuất: kết thúc không gục ngã","Giữ quân: kết thúc không có thương vong","Bậc thầy Bạch Đằng: kết thúc hạng S"};
                bool[] done={BeachBoatQuestSession.Instance.player.GetComponent<PlayerVitals>().CompletedTrainingLevels>=3,VillagePreparationQuest.Instance.CompletedCount==4,RiverStakeZone.Instance.IsComplete,r.kills.Length>=10,r.friendlyBoardings>0,r.finished&&!r.historyIncomplete&&r.defeats==0,r.finished&&!r.historyIncomplete&&r.casualties==0,r.finished&&!r.historyIncomplete&&r.finalRank=="S"};
                for(int i=0;i<rows.Length;i++){bool active=i<titles.Length;rows[i].transform.parent.gameObject.SetActive(active);if(!active)continue;marks[i].Set(done[i]);rowBackgrounds[i].color=done[i]?new Color(.075f,.19f,.14f,.9f):new Color(.07f,.10f,.105f,.8f);rows[i].text=titles[i]+(done[i]?"  <color=#79E9AC>ĐÃ ĐẠT</color>":"  <color=#90A29D>CHƯA ĐẠT</color>");}
            }
            rank.text=IsEnding?r.finalRank:"938";rank.color=IsEnding?Gold:Muted;
            score.text=IsEnding?$"{r.finalScore}/100 ĐIỂM"+(r.historyIncomplete?" • TẠM TÍNH":""):"HÀNH TRÌNH GIỮ NƯỚC";
            metrics.text=(IsEnding?$"MINIGAME     {p.MiniPoints:0.0}/35\nĐÓNG CỌC     {p.StakePoints:0}/25\nHẠ ĐỊCH      {p.KillPoints:0.0}/25\nTHỜI GIAN    {p.TimePoints:0}/15\n\n":"")+
                $"Tự hạ địch: {r.kills.Length}/{p.KillTarget}\nKỷ lục minigame: {Mathf.Min(13,r.minigames.Length)}/13\nĐóng cọc: "+(r.stakeStarted?CampaignProgress.Duration(r.stakeSeconds):r.stakeFinished?"Save cũ thiếu thời gian":"Chưa bắt đầu")+$"\nGục ngã: {r.defeats}\nQuân ta tử trận: {r.casualties}";
        }
        static GameObject Box(string name,Transform parent,Vector2 min,Vector2 max,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);var rt=(RectTransform)go.transform;rt.anchorMin=min;rt.anchorMax=max;rt.offsetMin=rt.offsetMax=Vector2.zero;go.GetComponent<Image>().color=color;return go;
        }
        static TMP_Text Text(string name,Transform parent,Vector2 min,Vector2 max,float size,Color color,string value="")
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);var rt=(RectTransform)go.transform;rt.anchorMin=min;rt.anchorMax=max;rt.offsetMin=rt.offsetMax=Vector2.zero;var t=go.GetComponent<TMP_Text>();StoryFont.Apply(t);t.text=value;t.fontSize=size;t.color=color;t.alignment=TextAlignmentOptions.MidlineLeft;t.raycastTarget=false;return t;
        }
        static GameObject Button(string name,Transform parent,Vector2 min,Vector2 max,string label,UnityEngine.Events.UnityAction click)
        {
            var go=Box(name,parent,min,max,new Color(.16f,.25f,.23f));var b=go.AddComponent<Button>();b.onClick.AddListener(click);b.navigation=new Navigation{mode=Navigation.Mode.None};var t=Text("Label",go.transform,Vector2.zero,Vector2.one,17,Cream,label);t.alignment=TextAlignmentOptions.Center;return go;
        }
        ScrollRect Scroll(string name,Transform parent,Vector2 min,Vector2 max,float height,out RectTransform target)
        {
            var go=Box(name,parent,min,max,new Color(.02f,.04f,.045f,.7f));var scroll=go.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.scrollSensitivity=35;scroll.movementType=ScrollRect.MovementType.Clamped;
            var viewport=Box("Viewport",go.transform,Vector2.zero,Vector2.one,Color.clear);viewport.AddComponent<RectMask2D>();scroll.viewport=(RectTransform)viewport.transform;
            var content=new GameObject("Content",typeof(RectTransform));content.transform.SetParent(viewport.transform,false);target=(RectTransform)content.transform;target.anchorMin=new Vector2(0,1);target.anchorMax=Vector2.one;target.pivot=new Vector2(.5f,1);target.sizeDelta=new Vector2(-14,height);target.anchoredPosition=new Vector2(-4,0);scroll.content=target;
            var track=Box("Scrollbar",go.transform,new Vector2(.985f,0),Vector2.one,new Color(.12f,.17f,.17f));var handle=Box("Handle",track.transform,Vector2.zero,Vector2.one,Gold);var bar=track.AddComponent<Scrollbar>();bar.handleRect=(RectTransform)handle.transform;bar.direction=Scrollbar.Direction.BottomToTop;scroll.verticalScrollbar=bar;return scroll;
        }
        void Build()
        {
            var canvas=gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=13000;gameObject.AddComponent<GraphicRaycaster>();var scaler=gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
            overlay=Box("JournalOverlay",transform,Vector2.zero,Vector2.one,new Color(.008f,.018f,.022f,.94f));var card=Box("Card",overlay.transform,new Vector2(.055f,.055f),new Vector2(.945f,.945f),new Color(.035f,.06f,.065f,.99f));
            Box("GoldRule",card.transform,new Vector2(.035f,.84f),new Vector2(.965f,.843f),Gold);
            heading=Text("Heading",card.transform,new Vector2(.04f,.90f),new Vector2(.87f,.98f),31,Gold);
            subtitle=Text("Subtitle",card.transform,new Vector2(.04f,.85f),new Vector2(.95f,.90f),14,Muted);
            Button("Missions",card.transform,new Vector2(.04f,.76f),new Vector2(.23f,.82f),"NHIỆM VỤ",()=>ShowPage(0));Button("Achievements",card.transform,new Vector2(.25f,.76f),new Vector2(.44f,.82f),"THÀNH TÍCH",()=>ShowPage(1));Button("Rules",card.transform,new Vector2(.46f,.76f),new Vector2(.65f,.82f),"CÁCH XẾP HẠNG",()=>ShowPage(2));
            listView=Box("MissionList",card.transform,new Vector2(.04f,.16f),new Vector2(.65f,.73f),Color.clear);scroll=Scroll("Tasks",listView.transform,Vector2.zero,Vector2.one,15*48+8,out content);
            for(int i=0;i<15;i++)
            {
                var row=Box("Mission_"+i,content,Vector2.zero,Vector2.one,Color.clear);var rt=(RectTransform)row.transform;rt.anchorMin=new Vector2(0,1);rt.anchorMax=Vector2.one;rt.pivot=new Vector2(.5f,1);rt.anchoredPosition=new Vector2(0,-i*48-4);rt.sizeDelta=new Vector2(0,44);rowBackgrounds[i]=row.GetComponent<Image>();
                var mark=new GameObject("CompletionMark",typeof(RectTransform),typeof(CanvasRenderer),typeof(JournalCheck));mark.transform.SetParent(row.transform,false);var mr=(RectTransform)mark.transform;mr.anchorMin=mr.anchorMax=new Vector2(0,.5f);mr.anchoredPosition=new Vector2(21,0);mr.sizeDelta=new Vector2(23,23);marks[i]=mark.GetComponent<JournalCheck>();marks[i].raycastTarget=false;
                rows[i]=Text("Task",row.transform,new Vector2(.075f,.03f),new Vector2(.98f,.97f),17,Cream);
            }
            ruleView=Box("RulesView",card.transform,new Vector2(.04f,.16f),new Vector2(.96f,.73f),Color.clear);var rs=Scroll("RankingRules",ruleView.transform,Vector2.zero,Vector2.one,1190,out var ruleContent);rules=Text("RulesText",ruleContent,new Vector2(.02f,0),new Vector2(.98f,1),18,Cream);rules.alignment=TextAlignmentOptions.TopLeft;
            side=Box("Statistics",card.transform,new Vector2(.68f,.16f),new Vector2(.96f,.73f),new Color(.06f,.11f,.12f));rank=Text("Rank",side.transform,new Vector2(.05f,.70f),new Vector2(.95f,.98f),82,Gold);rank.alignment=TextAlignmentOptions.Center;
            score=Text("Score",side.transform,new Vector2(.04f,.61f),new Vector2(.96f,.71f),17,Gold);score.alignment=TextAlignmentOptions.Center;
            metrics=Text("StatisticsText",side.transform,new Vector2(.08f,.035f),new Vector2(.95f,.61f),17,Cream);metrics.enableAutoSizing=true;metrics.fontSizeMin=13;metrics.fontSizeMax=17;metrics.alignment=TextAlignmentOptions.TopLeft;
            footer=Text("Footer",card.transform,new Vector2(.04f,.02f),new Vector2(.96f,.08f),13,Muted);
            closeButton=Button("CloseJournal",card.transform,new Vector2(.76f,.085f),new Vector2(.96f,.14f),"QUAY LẠI [TAB]",Close);
            saveButton=Button("SaveResult",card.transform,new Vector2(.53f,.085f),new Vector2(.74f,.14f),"LƯU KẾT QUẢ",SaveResult);homeButton=Button("ReturnToTitle",card.transform,new Vector2(.76f,.085f),new Vector2(.96f,.14f),"VỀ SẢNH",ReturnToTitle);overlay.SetActive(false);
        }
        void OnDestroy(){if(IsOpen)Time.timeScale=previousScale;if(Instance==this)Instance=null;}
    }
    public sealed class JournalCheck : MaskableGraphic
    {
        bool done;public void Set(bool value){if(done==value)return;done=value;SetVerticesDirty();}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=GetPixelAdjustedRect();var c=done?new Color(.43f,.94f,.66f):new Color(.38f,.48f,.46f);
            void Line(Vector2 a,Vector2 b,float width){var n=new Vector2(-(b-a).y,(b-a).x).normalized*width/2;int start=vh.currentVertCount;foreach(var p in new[]{a+n,b+n,b-n,a-n})vh.AddVert(new Vector3(r.xMin+p.x*r.width,r.yMin+p.y*r.height),c,Vector2.zero);vh.AddTriangle(start,start+1,start+2);vh.AddTriangle(start,start+2,start+3);}
            if(done){Line(new Vector2(.12f,.5f),new Vector2(.4f,.22f),.13f);Line(new Vector2(.4f,.22f),new Vector2(.91f,.84f),.13f);}
            else{Line(new Vector2(.18f,.18f),new Vector2(.82f,.18f),.06f);Line(new Vector2(.82f,.18f),new Vector2(.82f,.82f),.06f);Line(new Vector2(.82f,.82f),new Vector2(.18f,.82f),.06f);Line(new Vector2(.18f,.82f),new Vector2(.18f,.18f),.06f);}
        }
    }
}
