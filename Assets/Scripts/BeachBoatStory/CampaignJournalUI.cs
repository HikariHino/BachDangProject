using System;
using System.Linq;
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
        readonly TMP_Text[] rows=new TMP_Text[48];readonly Image[] rowBackgrounds=new Image[48];readonly JournalCheck[] marks=new JournalCheck[48];
        RectTransform content,ruleContent;ScrollRect scroll,ruleScroll;int page,rowCount;float rowTop,previousScale=1,nextRefresh;CursorLockMode previousLock;bool previousVisible;
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
        public void ShowPage(int value){page=value;listView.SetActive(page!=2);ruleView.SetActive(page==2);side.SetActive(page!=2);Refresh();Canvas.ForceUpdateCanvases();scroll.verticalNormalizedPosition=ruleScroll.verticalNormalizedPosition=1;}
        public void SaveResult(){Close();BachDangFrontEnd.Instance.OpenSaveSlots("Tổng kết chiến dịch Bạch Đằng");}
        public void ReturnToTitle(){Close();BachDangSaveSystem.PendingLoad=null;Time.timeScale=1;SceneManager.LoadScene(gameObject.scene.name);}
        public void Refresh()
        {
            var p=CampaignProgress.Instance;if(p==null)return;var r=p.Record;
            heading.text=IsEnding?"ĐẠI THẮNG BẠCH ĐẰNG 938":"NHẬT KÝ CHIẾN DỊCH";
            subtitle.text=$"NHIỆM VỤ {p.CompletedMissions}/{CampaignProgress.StoryOrder.Length}  •  THỜI GIAN {CampaignProgress.Duration(r.seconds)}"+(r.historyIncomplete?"  •  SAVE CŨ: THỐNG KÊ CHƯA ĐẦY ĐỦ":"");
            closeButton.SetActive(!IsEnding);saveButton.SetActive(IsEnding);homeButton.SetActive(IsEnding);
            footer.text=IsEnding?"Lưu kết quả vào một ô trước khi về sảnh để giữ tiến trình và thành tích.":"TAB / ESC: đóng bảng  •  Cuộn chuột: xem toàn bộ danh sách  •  Bảng mở sẽ tạm dừng trò chơi.";
            if(page==2)
            {
                rules.text=p.RankingRules;
                Canvas.ForceUpdateCanvases();
                float width=Mathf.Max(300,rules.rectTransform.rect.width);
                ruleContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,Mathf.Max(700,rules.GetPreferredValues(rules.text,width,0).y+35));
            }
            rowCount=0;rowTop=4;
            if(page==0)
            {
                for(int i=0;i<CampaignProgress.StoryOrder.Length;i++)
                {
                    bool done=p.MissionDone(i),current=p.MissionCurrent(i);
                    string detail=CampaignProgress.StoryOrder[i]==13?$" ({VillagePreparationQuest.Instance.CompletedCount}/4 sạp)":"";
                    Row(CampaignProgress.MissionTitles[i]+detail,i==1?p.TimingResult(0)+"\n"+p.TimingBands(0):i==3?p.WoodResult+"\n"+p.WoodBands:"",done,current,i==1||i==3?82:46);
                    if(CampaignProgress.StoryOrder[i]!=13)continue;
                    for(int work=0;work<4;work++)
                    {
                        var stall=VillagePreparationQuest.Instance.stalls.FirstOrDefault(s=>(int)s.kind==work);
                        bool supplied=stall!=null&&stall.Completed;
                        Row("VẬT TƯ • "+CampaignProgress.WorkNames[work],"Thao tác trực tiếp tại sạp, giao hàng rồi nhận vật tư.",supplied,current&&!supplied,66);
                        int index=1+work;bool completed=supplied||p.MiniRecord(index)!=null;
                        Row("    Thành tích chuẩn bị",p.TimingResult(index)+"\n"+p.TimingBands(index),completed,current&&!completed,82);
                    }
                }
                Row("NHIỆM VỤ PHỤ • Tập bắn cung",$"Bãi tập trong doanh trại • 6 mũi, trúng ít nhất 4 bia • Kỷ lục {ArcheryTraining.Instance?.BestScore??0}/60 • Không tính vào hạng chiến dịch",ArcheryTraining.Instance?.Completed??false,false,82);
                int trained=BeachBoatQuestSession.Instance.player.GetComponent<PlayerVitals>().CompletedTrainingLevels;
                Row($"NHIỆM VỤ PHỤ • Luyện thể lực ({trained}/3)","TÙY CHỌN • Có thể bỏ qua • Không ảnh hưởng cốt truyện và hạng.",trained==3,false,68);
                for(int level=1;level<=3;level++)Row("    Luyện thể lực • "+CampaignProgress.DifficultyNames[level-1],$"Thắng cấp này: +20 thể lực tối đa • Tổng sau cấp {level}: +{level*20}.",trained>=level,false,64);
            }
            else if(page==1)
            {
                Row("Tìm đủ 5 khúc gỗ"+(p.WoodRank>0?" • HẠNG "+CampaignProgress.RankName(p.WoodRank):""),p.WoodResult+"\n"+p.WoodBands,p.WoodRank>0,false,82,true);
                for(int index=0;index<CampaignProgress.ScoredMinigames;index++)
                {
                    int tier=p.MiniRank(index);
                    Row(CampaignProgress.MiniName(index)+(tier>0?" • HẠNG "+CampaignProgress.RankName(tier):""),p.TimingResult(index)+"\n"+p.TimingBands(index),tier>0,false,82,true);
                }
                string[] titles={"Luyện quân: xong cả 3 cấp thể lực","Hậu cần: hoàn tất đủ 4 sạp","Trận địa: dựng đủ 5 cụm cọc","Dũng sĩ: tự hạ ít nhất 10 địch","Tiếp viện: leo lên thuyền đồng minh","Bất khuất: kết thúc không gục ngã","Giữ quân: kết thúc không có thương vong","Bậc thầy Bạch Đằng: kết thúc hạng S"};
                bool[] done={BeachBoatQuestSession.Instance.player.GetComponent<PlayerVitals>().CompletedTrainingLevels>=3,VillagePreparationQuest.Instance.CompletedCount==4,RiverStakeZone.Instance.IsComplete,r.kills.Length>=10,r.friendlyBoardings>0,r.finished&&!r.historyIncomplete&&r.defeats==0,r.finished&&!r.historyIncomplete&&r.casualties==0,r.finished&&!r.historyIncomplete&&r.finalRank=="S"};
                for(int i=0;i<titles.Length;i++)Row(titles[i],i==0?"TÙY CHỌN • +60 thể lực tối đa • Không tham gia xét hạng.":"",done[i],false,i==0?66:46,true);
                for(int tier=3;tier>=1;tier--)
                {
                    var conditions=p.ConditionsFor(tier);int met=conditions.Count(c=>c.met);
                    string missing=string.Join(", ",conditions.Where(c=>!c.met).Select(c=>c.name));
                    bool earned=r.finished&&(r.finalRank=="S"||r.finalRank==CampaignProgress.RankName(tier)||(tier==1&&r.finalRank=="A"))&&!r.historyIncomplete;
                    Row("TỔNG KẾT HẠNG "+CampaignProgress.RankName(tier),$"Đạt {met}/{conditions.Length} điều kiện. "+(met==conditions.Length?(r.finished?"Đủ tất cả mốc.":"Hoàn thành báo công để chốt hạng."):"Còn thiếu: "+missing),earned,false,100,true);
                }
            }
            for(int i=rowCount;i<rows.Length;i++)rows[i].transform.parent.gameObject.SetActive(false);
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,Mathf.Max(420,rowTop+4));
            rank.text=IsEnding?r.finalRank:"938";rank.color=IsEnding?Gold:Muted;
            score.text=IsEnding?(r.historyIncomplete?"TẠM TÍNH • THIẾU THỐNG KÊ":"HẠNG THEO THÀNH TÍCH"):"HÀNH TRÌNH GIỮ NƯỚC";
            metrics.text=$"Trống: "+(p.MiniRecord(0)==null?"Chưa ghi":CampaignProgress.Clock(p.MiniRecord(0).seconds)+" • "+CampaignProgress.RankName(p.MiniRank(0)))+
                $"\nMinigame vật tư: {Enumerable.Range(1,4).Count(i=>p.MiniRecord(i)!=null)}/4\nĐiều kiện S: {p.ConditionsFor(3).Count(c=>c.met)}/{p.ConditionsFor(3).Length}\nTìm gỗ: "+(r.woodStarted?CampaignProgress.Duration(r.woodSeconds):"Chưa ghi")+$"\nTự hạ địch: {r.kills.Length}/{p.KillTarget}\nĐóng cọc: "+(r.stakeStarted?CampaignProgress.Duration(r.stakeSeconds):r.stakeFinished?"Thiếu thời gian cũ":"Chưa bắt đầu")+$"\nGục ngã: {r.defeats}\nQuân ta tử trận: {r.casualties}\nThể lực phụ: {BeachBoatQuestSession.Instance.player.GetComponent<PlayerVitals>().CompletedTrainingLevels}/3";
        }
        void Row(string title,string detail,bool done,bool current=false,float height=46,bool achievement=false)
        {
            int i=rowCount++;var rt=(RectTransform)rows[i].transform.parent;rt.gameObject.SetActive(true);rt.anchoredPosition=new Vector2(0,-rowTop);rt.sizeDelta=new Vector2(0,height);rowTop+=height+4;
            marks[i].Set(done);rowBackgrounds[i].color=done?new Color(.075f,.19f,.14f,.9f):current?new Color(.20f,.21f,.13f,.95f):new Color(.07f,.10f,.105f,.8f);
            string status=done?(achievement?"ĐÃ ĐẠT":"ĐÃ XONG"):current?"ĐANG LÀM":achievement?"CHƯA ĐẠT":"CHƯA XONG";
            rows[i].text=title+$"  <color={(done?"#79E9AC":current?"#F0C26D":"#90A29D")}>{status}</color>"+(string.IsNullOrEmpty(detail)?"":"\n<size=14><color=#B5C7C0>"+detail+"</color></size>");
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
            var go=Box(name,parent,min,max,new Color(.16f,.25f,.23f));var b=go.AddComponent<Button>();b.onClick.AddListener(()=>{BachDangMenuAudio.Click();click();});b.navigation=new Navigation{mode=Navigation.Mode.None};var t=Text("Label",go.transform,Vector2.zero,Vector2.one,17,Cream,label);t.alignment=TextAlignmentOptions.Center;return go;
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
            for(int i=0;i<rows.Length;i++)
            {
                var row=Box("Mission_"+i,content,Vector2.zero,Vector2.one,Color.clear);var rt=(RectTransform)row.transform;rt.anchorMin=new Vector2(0,1);rt.anchorMax=Vector2.one;rt.pivot=new Vector2(.5f,1);rt.anchoredPosition=new Vector2(0,-i*48-4);rt.sizeDelta=new Vector2(0,44);rowBackgrounds[i]=row.GetComponent<Image>();
                var mark=new GameObject("CompletionMark",typeof(RectTransform),typeof(CanvasRenderer),typeof(JournalCheck));mark.transform.SetParent(row.transform,false);var mr=(RectTransform)mark.transform;mr.anchorMin=mr.anchorMax=new Vector2(0,.5f);mr.anchoredPosition=new Vector2(21,0);mr.sizeDelta=new Vector2(23,23);marks[i]=mark.GetComponent<JournalCheck>();marks[i].raycastTarget=false;
                rows[i]=Text("Task",row.transform,new Vector2(.075f,.03f),new Vector2(.98f,.97f),17,Cream);
            }
            ruleView=Box("RulesView",card.transform,new Vector2(.04f,.16f),new Vector2(.96f,.73f),Color.clear);ruleScroll=Scroll("RankingRules",ruleView.transform,Vector2.zero,Vector2.one,2400,out ruleContent);rules=Text("RulesText",ruleContent,new Vector2(.02f,0),new Vector2(.98f,1),18,Cream);rules.alignment=TextAlignmentOptions.TopLeft;
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
