using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BeachBoatStory
{
    /// <summary>Modal workbench: physical goods, timer, difficulty and explicit results.</summary>
    public sealed class VillageWorkUI : MonoBehaviour
    {
        static VillageWorkUI instance;
        GameObject shade,instructions,work,result,woodChoices,foodChoices,ropeGame,riceGame;
        TMP_Text title,rules,stats,task,feedback,resultTitle,resultBody,startLabel;
        TMP_Text[] difficultyLabels,woodLabels,foodLabels,riceLabels;
        Button[] difficultyButtons;
        GameObject[] woodButtons;
        TMP_Text ropeMoves;
        RectTransform clockFill,progressFill,riceFill;
        VillageWorkIcon[] woodIcons,ropeIcons;
        VillageWorkStall shown;
        int selectedDifficulty=1;
        Button undoButton,restoreButton;
        static readonly Color Gold=new Color(1,.75f,.30f),Green=new Color(.25f,.70f,.48f),Ink=new Color(.045f,.06f,.07f,.98f);
        public static void EnsureExists()
        {
            if(instance!=null)return;
            var go=new GameObject("VillageWorkCanvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            go.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;go.GetComponent<Canvas>().sortingOrder=240;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
            instance=go.AddComponent<VillageWorkUI>();instance.Build();
        }
        public bool IsVisible=>shade!=null&&shade.activeSelf;
        void Build()
        {
            shade=Box("WorkOverlay",transform,Vector2.zero,Vector2.one,new Color(0,0,0,.65f));
            var card=Box("Workbench",shade.transform,new Vector2(.16f,.10f),new Vector2(.84f,.90f),Ink).transform;
            Box("HeaderLine",card,new Vector2(.04f,.82f),new Vector2(.96f,.824f),Gold);
            title=Label("Title",card,new Vector2(.05f,.86f),new Vector2(.90f,.96f),30,Gold);
            Click("Close",card,new Vector2(.91f,.87f),new Vector2(.98f,.96f),"X",()=>VillageWorkStall.Active?.Close());
            instructions=Box("Instructions",card,new Vector2(.05f,.06f),new Vector2(.95f,.80f),Color.clear);
            rules=Label("Rules",instructions.transform,new Vector2(.04f,.53f),new Vector2(.96f,.96f),23,Color.white);
            Label("DifficultyCaption",instructions.transform,new Vector2(.04f,.40f),new Vector2(.96f,.50f),18,Gold).text="CHỌN ĐỘ KHÓ";
            difficultyLabels=new TMP_Text[3];difficultyButtons=new Button[3];
            for(int i=0;i<3;i++)
            {
                int level=i;var b=Click("Difficulty"+i,instructions.transform,new Vector2(.04f+i*.31f,.24f),new Vector2(.32f+i*.31f,.39f),new[]{"DỄ","VỪA","KHÓ"}[i],()=>{selectedDifficulty=level;VillageWorkStall.Active?.SelectDifficulty(level);});
                difficultyLabels[i]=b.GetComponentInChildren<TMP_Text>();difficultyButtons[i]=b.GetComponent<Button>();
            }
            var start=Click("StartWork",instructions.transform,new Vector2(.25f,.02f),new Vector2(.75f,.18f),"BẮT ĐẦU",()=>VillageWorkStall.Active?.StartAttempt(selectedDifficulty));startLabel=start.GetComponentInChildren<TMP_Text>();
            work=Box("Work",card,new Vector2(.05f,.05f),new Vector2(.95f,.80f),Color.clear);
            stats=Label("Stats",work.transform,new Vector2(0,.90f),new Vector2(1,1),19,Color.white);
            var clock=Box("Clock",work.transform,new Vector2(0,.85f),new Vector2(1,.875f),new Color(.2f,.2f,.2f));clockFill=Box("Fill",clock.transform,Vector2.zero,Vector2.one,Gold).GetComponent<RectTransform>();
            task=Label("Task",work.transform,new Vector2(0,.67f),new Vector2(1,.81f),24,Gold);
            feedback=Label("Feedback",work.transform,new Vector2(0,.08f),new Vector2(1,.16f),17,Color.white);
            undoButton=Click("Undo",work.transform,new Vector2(0,.17f),new Vector2(.48f,.25f),"HOÀN TÁC  [Z]",()=>VillageWorkStall.Active?.Undo()).GetComponent<Button>();
            restoreButton=Click("RestoreTask",work.transform,new Vector2(.52f,.17f),new Vector2(1,.25f),"LÀM LẠI PHẦN NÀY  [C]",()=>VillageWorkStall.Active?.RestoreCurrentTask()).GetComponent<Button>();
            var progress=Box("Progress",work.transform,new Vector2(0,.045f),new Vector2(1,.075f),new Color(.2f,.2f,.2f));progressFill=Box("Fill",progress.transform,Vector2.zero,Vector2.one,Green).GetComponent<RectTransform>();
            woodChoices=Box("TimberChoices",work.transform,new Vector2(0,.27f),new Vector2(1,.65f),Color.clear);woodIcons=new VillageWorkIcon[8];woodLabels=new TMP_Text[8];woodButtons=new GameObject[8];
            for(int i=0;i<8;i++)
            {
                int option=i;float x=(i%4)*.25f,y=i<4?.59f:.20f;
                var b=Click("Timber"+i,woodChoices.transform,new Vector2(x+.006f,y),new Vector2(x+.244f,y+.38f),"",()=>VillageWorkStall.Active?.SubmitOption(option));woodButtons[i]=b;
                Label("Number",b.transform,new Vector2(.02f,.70f),new Vector2(.16f,.99f),14,Gold).text=(i+1).ToString();
                woodIcons[i]=Icon(b.transform,0,new Vector2(.20f,.67f),new Vector2(.80f,.98f));
                woodLabels[i]=Label("Length",b.transform,new Vector2(.02f,.01f),new Vector2(.98f,.70f),14,Color.white);
            }
            Click("DeliverTimber",woodChoices.transform,new Vector2(.23f,0),new Vector2(.77f,.17f),"GIAO GỖ [ENTER]",()=>VillageWorkStall.Active?.SubmitOption(8));
            foodChoices=Box("Ingredients",work.transform,new Vector2(0,.27f),new Vector2(1,.65f),Color.clear);foodLabels=new TMP_Text[3];
            for(int i=0;i<3;i++)
            {
                int option=i;var b=Click("Ingredient"+i,foodChoices.transform,new Vector2(i*.335f+.005f,.27f),new Vector2(i*.335f+.325f,1),"",()=>VillageWorkStall.Active?.SubmitOption(option));
                Icon(b.transform,i+1,new Vector2(.10f,.30f),new Vector2(.90f,.95f));
                foodLabels[i]=Label("Name",b.transform,new Vector2(.04f,.02f),new Vector2(.96f,.30f),17,Color.white);
            }
            Click("PackRations",foodChoices.transform,new Vector2(.20f,0),new Vector2(.80f,.23f),"KIỂM TRA VÀ GÓI  [4]",()=>VillageWorkStall.Active?.SubmitOption(3));
            ropeGame=Box("RopeWorkbench",work.transform,new Vector2(0,.27f),new Vector2(1,.65f),Color.clear);
            ropeIcons=new VillageWorkIcon[9];
            for(int i=0;i<9;i++)
            {
                int tile=i;float x=.39f+(i%3)*.075f,y=.94f-(i/3)*.30f;
                var b=Click("RopeTile"+i,ropeGame.transform,new Vector2(x,y-.28f),new Vector2(x+.070f,y),"",()=>VillageWorkStall.Active?.SubmitOption(tile));
                ropeIcons[i]=Icon(b.transform,4,Vector2.zero,Vector2.one);
            }
            Label("Entry",ropeGame.transform,new Vector2(.17f,.37f),new Vector2(.38f,.64f),21,Green).text="VÀO  >";
            Label("Exit",ropeGame.transform,new Vector2(.63f,.37f),new Vector2(.82f,.64f),21,Gold).text=">  RA";
            ropeMoves=Label("Moves",ropeGame.transform,new Vector2(0,.05f),new Vector2(.32f,.30f),17,Color.white);
            Click("CheckRope",ropeGame.transform,new Vector2(.65f,.02f),new Vector2(1,.28f),"KIỂM TRA  [SPACE]",()=>VillageWorkStall.Active?.SubmitOption(9));
            riceGame=Box("RiceWorkbench",work.transform,new Vector2(0,.27f),new Vector2(1,.65f),Color.clear);
            Icon(riceGame.transform,1,new Vector2(.35f,.38f),new Vector2(.65f,1));
            var bag=Box("Measure",riceGame.transform,new Vector2(.14f,.40f),new Vector2(.24f,.95f),new Color(.3f,.22f,.12f));riceFill=Box("RiceLevel",bag.transform,Vector2.zero,Vector2.zero,Gold).GetComponent<RectTransform>();
            riceLabels=new TMP_Text[4];
            for(int i=0;i<4;i++){int option=i;var b=Click("Scoop"+i,riceGame.transform,new Vector2(i*.25f+.005f,0),new Vector2(i*.25f+.24f,.32f),"",()=>VillageWorkStall.Active?.SubmitOption(option));riceLabels[i]=b.GetComponentInChildren<TMP_Text>();}
            result=Box("Result",card,new Vector2(.05f,.06f),new Vector2(.95f,.80f),Color.clear);
            resultTitle=Label("Heading",result.transform,new Vector2(.04f,.70f),new Vector2(.96f,.93f),35,Gold);
            resultBody=Label("Details",result.transform,new Vector2(.04f,.36f),new Vector2(.96f,.67f),22,Color.white);
            Click("ConfirmResult",result.transform,new Vector2(.20f,.06f),new Vector2(.80f,.26f),"",()=>{var s=VillageWorkStall.Active;if(s==null)return;if(s.Phase==VillageWorkStall.WorkPhase.Won)s.ClaimReward();else s.StartAttempt(s.Difficulty);});
            shade.SetActive(false);
        }
        void Update()
        {
            var s=VillageWorkStall.Active;shade.SetActive(s!=null);if(s==null){shown=null;return;}
            if(s!=shown){shown=s;selectedDifficulty=s.Difficulty;}
            title.text=s.stallName;
            bool intro=s.Phase==VillageWorkStall.WorkPhase.Instructions,playing=s.Phase==VillageWorkStall.WorkPhase.Playing;
            instructions.SetActive(intro);work.SetActive(playing);result.SetActive(!intro&&!playing);
            if(intro)
            {
                selectedDifficulty=s.Difficulty;
                rules.text=s.Instructions+"\n\nHoàn thành lần lượt Dễ → Vừa → Khó để nhận vật tư. Z: hoàn tác; C: làm lại phần này. Không hoàn thời gian/lỗi. Mỗi lỗi mất 2 giây. ESC: rời sạp.";
                for(int i=0;i<3;i++){difficultyLabels[i].color=i==selectedDifficulty?Gold:Color.white;difficultyButtons[i].interactable=i<=s.UnlockedDifficulty;difficultyLabels[i].text=new[]{"DỄ","VỪA","KHÓ"}[i]+(i<s.CompletedLevels?" • XONG":i>s.UnlockedDifficulty?" • KHÓA":"");}
                startLabel.text="BẮT ĐẦU  •  "+new[]{"DỄ","VỪA","KHÓ"}[selectedDifficulty];return;
            }
            if(!playing)
            {
                bool won=s.Phase==VillageWorkStall.WorkPhase.Won;
                resultTitle.text=won?"LÀM TỐT LẮM!":"CHƯA HOÀN THÀNH";resultTitle.color=won?Green:new Color(1,.4f,.3f);
                resultBody.text=won?$"Xong mức {new[]{"DỄ","VỪA","KHÓ"}[s.Difficulty]}: {s.Progress}/{s.Goal} phần việc trong {s.Elapsed:0.0}s.\n"+(s.Difficulty==2?"Nhận vật tư để tính tiến độ nhiệm vụ.":"Xác nhận để mở mức tiếp theo. Chưa nhận vật tư."):s.Feedback+"\nMức đã hoàn thành và các sạp khác vẫn được giữ.";
                result.transform.Find("ConfirmResult/Label").GetComponent<TMP_Text>().text=won?(s.Difficulty==2?"NHẬN VẬT TƯ  [ENTER]":"MỞ MỨC TIẾP THEO  [ENTER]"):"CHƠI LẠI  [R]";return;
            }
            stats.text=$"{new[]{"DỄ","VỪA","KHÓ"}[s.Difficulty]}  •  CÒN {s.TimeRemaining:0.0}s  •  XONG {s.Progress}/{s.Goal}  •  SAI {s.Mistakes}/{s.MistakeLimit}";
            undoButton.interactable=s.CanUndo;restoreButton.interactable=s.CanRestore;
            stats.color=s.TimeRemaining<7?new Color(1,.4f,.3f):Color.white;
            clockFill.anchorMax=new Vector2(s.TimeRemaining/s.TimeLimit,1);progressFill.anchorMax=new Vector2(s.Progress/(float)s.Goal,1);
            woodChoices.SetActive(s.kind==VillageWorkStall.WorkKind.Timber);foodChoices.SetActive(s.kind==VillageWorkStall.WorkKind.Provisions);
            ropeGame.SetActive(s.kind==VillageWorkStall.WorkKind.Rope);riceGame.SetActive(s.kind==VillageWorkStall.WorkKind.Rice);
            task.fontSize=s.kind==VillageWorkStall.WorkKind.Timber?19:24;
            task.text=s.kind==VillageWorkStall.WorkKind.Timber?$"CHỌN {s.TimberRequiredPieces} THANH • DÀI {s.TimberTarget} THƯỚC"+(s.Difficulty>0?$" • LỰC ≥ {s.TimberStrengthTarget}":"")+(s.Difficulty>1?$" • GIÁ ≤ {s.TimberBudget}":"")+$"\nĐã chọn {s.TimberSelectedPieces}/{s.TimberRequiredPieces} • dài {s.TimberTotal}"+(s.Difficulty>0?$" • lực {s.TimberStrengthTotal}":"")+(s.Difficulty>1?$" • giá {s.TimberCostTotal}":""):
                s.kind==VillageWorkStall.WorkKind.Rope?"XOAY Ô ĐỂ NỐI DÂY LIỀN TỪ VÀO ĐẾN RA":
                s.kind==VillageWorkStall.WorkKind.Rice?$"CẦN {s.RiceTarget} ĐƠN VỊ  •  CÓ {s.RiceLoad}  •  LƯỢT {s.Moves}/{s.MoveLimit}":$"{s.People} NGƯỜI: MỖI NGƯỜI {s.RicePerPerson} GẠO + {s.FishPerPerson} CÁ; CẢ GÓI {s.SaltPerBundle} MUỐI";
            feedback.text=s.Feedback;feedback.color=s.FeedbackVersion==0?Color.white:s.LastCorrect?Green:new Color(1,.5f,.35f);
            for(int i=0;i<8;i++)
            {
                woodButtons[i].SetActive(i<s.TimberOptions);woodButtons[i].GetComponent<Image>().color=s.TimberUsed(i)?new Color(.15f,.40f,.30f):new Color(.17f,.20f,.20f);
                woodIcons[i].SetDamaged(!s.IsSoundTimber(i));woodLabels[i].text=$"{s.TimberLength(i)} thước"+(s.Difficulty>0?$" • lực {s.TimberStrength(i)}":"")+(s.Difficulty>1?$"\nGiá {s.TimberCost(i)}":"")+(!s.IsSoundTimber(i)?" • NỨT":s.TimberUsed(i)?" • CHỌN":"");
            }
            for(int i=0;i<9;i++)ropeIcons[i].SetPorts(s.RopePorts(i));
            for(int i=0;i<3;i++)foodLabels[i].text=$"[{i+1}] {VillageWorkStall.IngredientName(i)}: {s.IngredientCount(i)}";
            ropeMoves.text=$"SỐ LƯỢT XOAY\n{s.Moves}/{s.MoveLimit}";
            riceLabels[0].text=$"+{s.RiceSmall}  [1]";riceLabels[1].text=$"+{s.RiceLarge}  [2]";riceLabels[2].text=$"BỚT {s.RiceSmall}  [3]";riceLabels[3].text="ĐÓNG TÚI  [4]";
            riceFill.anchorMax=new Vector2(1,Mathf.Clamp01(s.RiceLoad/(float)Mathf.Max(1,s.RiceTarget)));
        }
        static GameObject Box(string name,Transform parent,Vector2 min,Vector2 max,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
            var r=(RectTransform)go.transform;r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;go.GetComponent<Image>().color=color;return go;
        }
        static TMP_Text Label(string name,Transform parent,Vector2 min,Vector2 max,float size,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);var r=(RectTransform)go.transform;r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;
            var t=go.GetComponent<TextMeshProUGUI>();StoryFont.Apply(t);t.fontSize=size;t.color=color;t.alignment=TextAlignmentOptions.Center;t.textWrappingMode=TextWrappingModes.Normal;return t;
        }
        static GameObject Click(string name,Transform parent,Vector2 min,Vector2 max,string caption,UnityEngine.Events.UnityAction action)
        {
            var go=Box(name,parent,min,max,new Color(.17f,.20f,.20f));var b=go.AddComponent<Button>();b.targetGraphic=go.GetComponent<Image>();b.navigation=new Navigation{mode=Navigation.Mode.None};b.onClick.AddListener(action);
            Label("Label",go.transform,new Vector2(.03f,.03f),new Vector2(.97f,.97f),20,Color.white).text=caption;return go;
        }
        static VillageWorkIcon Icon(Transform parent,int kind,Vector2 min,Vector2 max)
        {
            var go=new GameObject("GoodsIcon",typeof(RectTransform),typeof(CanvasRenderer));go.transform.SetParent(parent,false);var r=(RectTransform)go.transform;r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;
            var icon=go.AddComponent<VillageWorkIcon>();icon.kind=kind;icon.raycastTarget=false;icon.color=Color.white;icon.SetAllDirty();return icon;
        }
        void OnDestroy(){if(instance==this)instance=null;}
    }
}
