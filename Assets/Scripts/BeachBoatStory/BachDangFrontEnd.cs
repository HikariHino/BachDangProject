using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BeachBoatStory
{
    /// <summary>Campaign title, paused settings and explicit save-before-exit flow.</summary>
    [DefaultExecutionOrder(-900)]
    public sealed class BachDangFrontEnd : MonoBehaviour
    {
        public enum MenuPage { None, Home, Pause, Settings, ConfirmExit, Loading, Slots, ConfirmOverwrite }
        public static BachDangFrontEnd Instance {get;private set;}
        public static bool IsBlocking=>(Instance!=null&&Instance.Page!=MenuPage.None)||CampaignJournalUI.IsOpen;
        public static bool AtTitle=>Instance!=null&&!Instance.GameStarted;
        public MenuPage Page {get;private set;}
        public bool GameStarted {get;private set;}
        public bool QuitRequested {get;private set;}
#if UNITY_EDITOR
        public bool SuppressQuitForTests;
#endif
        GameObject background,home,pause,settings,confirm,loading,toast,slots,overwrite;
        TMP_Text slotsTitle,overwriteText;
        readonly Button[] slotButtons=new Button[BachDangSaveSystem.SlotCount];
        readonly TMP_Text[] slotLabels=new TMP_Text[BachDangSaveSystem.SlotCount];
        MenuPage slotsReturn;
        bool choosingLoad,quitAfterSave;
        int overwriteSlot;
        string saveLocation="Lưu nhanh";
        TMP_Text saveInfo,volumeValue,mouseValue,notice,confirmText;
        Button loadButton,continueButton,confirmSave;
        public float LastLoadSeconds {get;private set;}
        Slider volumeSlider,mouseSlider;
        MenuPage settingsReturn;
        readonly Dictionary<Canvas,bool> hidden=new Dictionary<Canvas,bool>();
        readonly Dictionary<SaBanHUD,bool> hiddenLegacy=new Dictionary<SaBanHUD,bool>();
        float savedTimeScale=1,toastUntil;
        CursorLockMode savedCursor;
        bool savedVisible;
        static readonly Color Gold=new Color(.88f,.69f,.38f),Cream=new Color(.95f,.90f,.78f),Ink=new Color(.025f,.06f,.055f,.96f);
        void Awake()
        {
            Instance=this;gameObject.AddComponent<BachDangMenuAudio>();gameObject.AddComponent<BachDangAutoSave>();Build();BachDangPreferences.Apply();
            GameStarted=BachDangSaveSystem.PendingLoad!=null;
            SetPage(GameStarted?MenuPage.Loading:MenuPage.Home);RefreshSaveInfo();
        }
        IEnumerator Start()
        {
            yield return null;yield return null;
            if(BachDangSaveSystem.PendingLoad!=null)
            {
                var saved=BachDangSaveSystem.PendingLoad;BachDangSaveSystem.PendingLoad=null;
                try{BachDangSaveSystem.Apply(saved);SetPage(MenuPage.None);BachDangAutoSave.Instance.BeginLoadedJourney(saved,BachDangSaveSystem.PendingLoadSelectedManual);BachDangSaveSystem.PendingLoadSelectedManual=false;Notify("Đã tải tiến trình • "+saved.location);}
                catch(Exception error){GameStarted=false;SetPage(MenuPage.Home);Notify("Không thể khôi phục bản lưu: "+error.Message);}
            }
            BachDangPreferences.Apply();
        }
        void Update()
        {
            if(toast!=null)toast.SetActive(Time.unscaledTime<toastUntil);
            if(CampaignJournalUI.IsOpen||StoryDialogueUI.InputConsumedThisFrame)return;
            if(Input.GetKeyDown(KeyCode.Escape))HandleEscape();
            if(GameStarted&&Page==MenuPage.None&&Input.GetKeyDown(KeyCode.F5))SaveNow();
        }
        /// <summary>One Escape press belongs to the topmost current activity, never two screens.</summary>
        public bool HandleEscape()
        {
            if(CampaignJournalUI.IsOpen||StoryDialogueUI.InputConsumedThisFrame||
                !(GameStarted||Page==MenuPage.Slots||Page==MenuPage.ConfirmOverwrite)||
                (QuestCheckpoint.Instance?.IsDefeated??false)||(ScreenFader.Instance?.IsTransitioning??false))return false;
            if(Page==MenuPage.None)
            {
                // This runs before minigame Update. Close only the current attempt;
                // each activity retains its earned levels, rewards and best times.
                if(CampRecoveryGame.Active!=null)CampRecoveryGame.Active.Close();
                else if(VillageWorkStall.Active!=null)VillageWorkStall.Active.Close();
                else if(BeachBoatMinigameStation.Active!=null)BeachBoatMinigameStation.Active.CancelActivity();
                else SetPage(MenuPage.Pause);
            }
            else if(Page==MenuPage.Settings)SetPage(settingsReturn);
            else if(Page==MenuPage.ConfirmOverwrite)CancelOverwrite();
            else if(Page==MenuPage.Slots)CancelSlots();
            else if(Page==MenuPage.ConfirmExit)SetPage(MenuPage.Pause);
            else if(Page==MenuPage.Pause)Resume();
            else return false;
            StoryDialogueUI.TryConsumeInput();return true;
        }
        public void Play()
        {
            if(Page!=MenuPage.Home)return;
            GameStarted=true;SetPage(MenuPage.None);BachDangAutoSave.Instance.BeginNewJourney();
        }
        public void Resume(){if(GameStarted)SetPage(MenuPage.None);}
        public void OpenSettings(){settingsReturn=GameStarted?MenuPage.Pause:MenuPage.Home;SetPage(MenuPage.Settings);}
        public void BackFromSettings(){SetPage(settingsReturn);}
        public void OpenPause(){if(GameStarted)SetPage(MenuPage.Pause);}
        public bool SaveNow()=>OpenSaveSlots("Lưu nhanh");
        public bool OpenSaveSlots(string location,bool exitAfter=false)
        {
            if(!GameStarted||!BachDangSaveSystem.CanSave){Notify("Hãy kết thúc thao tác đang làm trước khi lưu.");return false;}
            saveLocation=location;choosingLoad=false;quitAfterSave=exitAfter;
            slotsReturn=exitAfter?MenuPage.ConfirmExit:Page==MenuPage.None?MenuPage.None:MenuPage.Pause;
            ShowSlots();return true;
        }
        public bool LoadGame()
        {
            if(!BachDangSaveSystem.TryReadLatest(out _)){Notify("Chưa có tiến trình đã lưu.");RefreshSaveInfo();return false;}
            choosingLoad=true;quitAfterSave=false;slotsReturn=GameStarted?MenuPage.Pause:MenuPage.Home;
            ShowSlots();return true;
        }
        public bool ContinueGame()
        {
            if(!BachDangSaveSystem.TryReadResume(out var data,out int slot)){Notify("Chưa có tiến trình để tiếp tục.");return false;}
            if(slot>0)BachDangSaveSystem.SelectSlot(slot);
            return LoadSnapshot(data,false);
        }
        public bool LoadSnapshot(BachDangSaveData data,bool selectedManual)
        {
            if(data==null||Page==MenuPage.Loading)return false;
            GameStarted=true;SetPage(MenuPage.Loading);
            if(data.scene!=gameObject.scene.name)
            {
                BachDangSaveSystem.PendingLoad=data;BachDangSaveSystem.PendingLoadSelectedManual=selectedManual;
                Time.timeScale=1;SceneManager.LoadScene(data.scene);return true;
            }
            StartCoroutine(RestoreSnapshot(data,selectedManual));return true;
        }
        IEnumerator RestoreSnapshot(BachDangSaveData data,bool selectedManual)
        {
            // Let the loading panel render; restoring the existing scene avoids
            // rebuilding terrain, shaders, meshes and every NPC for each slot.
            yield return null;
            var watch=System.Diagnostics.Stopwatch.StartNew();
            try
            {
                BachDangSaveSystem.Apply(data);SetPage(MenuPage.None);
                BachDangAutoSave.Instance.BeginLoadedJourney(data,selectedManual);
                LastLoadSeconds=(float)watch.Elapsed.TotalSeconds;
                Notify("Đã tải tiến trình • "+data.location);
            }
            catch(Exception error){SetPage(MenuPage.Pause);Notify("Không thể khôi phục bản lưu: "+error.Message);}
        }
        void ShowSlots()
        {
            slotsTitle.text=choosingLoad?"CHỌN Ô TẢI HÀNH TRÌNH":"CHỌN Ô LƯU HÀNH TRÌNH";
            for(int slot=1;slot<=BachDangSaveSystem.SlotCount;slot++)
            {
                bool readable=BachDangSaveSystem.TryRead(slot,out var data);
                slotButtons[slot-1].interactable=!choosingLoad||readable;
                bool completed=readable&&data.campaign!=null&&data.campaign.finished;
                slotButtons[slot-1].GetComponent<Image>().color=completed?new Color(.055f,.29f,.20f,.97f):new Color(.07f,.15f,.13f,.94f);
                slotLabels[slot-1].color=completed?new Color(.62f,1,.76f):Cream;
                slotLabels[slot-1].text=$"Ô {slot:00}  •  "+(readable?(completed?"✓ HOÀN THÀNH • HẠNG "+data.campaign.finalRank:BachDangSaveSystem.Chapter(data.storyState))+"\n"+data.savedAt+"  •  "+data.location:BachDangSaveSystem.HasData(slot)?"BẢN LƯU LỖI • CÓ THỂ GHI ĐÈ":"TRỐNG • Lưu hành trình tại đây");
            }
            SetPage(MenuPage.Slots);
        }
        public bool ChooseSlot(int slot)
        {
            if(Page!=MenuPage.Slots||slot<1||slot>BachDangSaveSystem.SlotCount)return false;
            if(choosingLoad)
            {
                if(!BachDangSaveSystem.TryRead(slot,out var data)){Notify(BachDangSaveSystem.LastError);return false;}
                BachDangSaveSystem.SelectSlot(slot);return LoadSnapshot(data,true);
            }
            if(BachDangSaveSystem.HasData(slot))
            {
                overwriteSlot=slot;
                BachDangSaveSystem.TryRead(slot,out var old);
                overwriteText.text=$"Ô {slot:00} đã có tiến trình.\n"+(old!=null?old.savedAt+" • "+old.location+"\n\n":"\n")+"Bạn có muốn ghi đè tiến trình này?";
                SetPage(MenuPage.ConfirmOverwrite);return true;
            }
            return WriteSlot(slot);
        }
        public bool ConfirmOverwrite()=>Page==MenuPage.ConfirmOverwrite&&WriteSlot(overwriteSlot);
        public void CancelOverwrite(){if(Page==MenuPage.ConfirmOverwrite)ShowSlots();}
        public void CancelSlots(){quitAfterSave=false;SetPage(slotsReturn);}
        bool WriteSlot(int slot)
        {
            if(!BachDangSaveSystem.Save(slot,saveLocation)){Notify(BachDangSaveSystem.LastError);return false;}
            RefreshSaveInfo();Notify($"ĐÃ LƯU VÀO Ô {slot:00} • vị trí và tiến trình hiện tại");
            if(quitAfterSave)Quit();else SetPage(slotsReturn);
            return true;
        }
        public void AskExit()
        {
            if(!GameStarted)return;
            SetPage(MenuPage.ConfirmExit);confirmSave.interactable=BachDangSaveSystem.CanSave;
            confirmText.text="Bạn có muốn lưu tiến trình hiện tại trước khi thoát?\n\nVị trí đang đứng, nhiệm vụ và những phần việc đã hoàn thành sẽ được lưu.";
        }
        public bool ExitWithSave()
        {
            if(Page!=MenuPage.ConfirmExit)return false;
            return OpenSaveSlots("Lưu khi thoát",true);
        }
        public void ExitWithoutSave(){if(Page==MenuPage.ConfirmExit)Quit();}
        public void CancelExit(){SetPage(MenuPage.Pause);}
        void Quit()
        {
            QuitRequested=true;Time.timeScale=1;
#if UNITY_EDITOR
            if(!SuppressQuitForTests)UnityEditor.EditorApplication.isPlaying=false;
#else
            Application.Quit();
#endif
        }
        public void Notify(string message){notice.text=message;toastUntil=Time.unscaledTime+3.5f;toast.SetActive(true);}
        public void RefreshSaveInfo()
        {
            bool exists=BachDangSaveSystem.TryReadLatest(out var saved);loadButton.interactable=exists;
            bool resume=BachDangSaveSystem.TryReadResume(out var latest,out int slot);continueButton.interactable=resume;
            saveInfo.text=resume?(slot==0?"LƯU TẠM TỰ ĐỘNG":$"Ô LƯU {slot:00}")+" • "+latest.savedAt+"\n"+BachDangSaveSystem.Chapter(latest.storyState):"CHƯA CÓ BẢN LƯU\nBắt đầu hành trình để viết tiếp lịch sử.";
        }
        public void ReleaseHiddenUI()
        {
            foreach(var pair in hidden)if(pair.Key!=null)pair.Key.enabled=pair.Value;hidden.Clear();
            foreach(var pair in hiddenLegacy)if(pair.Key!=null)pair.Key.enabled=pair.Value;hiddenLegacy.Clear();
        }
        void SetPage(MenuPage next)
        {
            bool wasBlocking=Page!=MenuPage.None,blocking=next!=MenuPage.None;
            if(blocking&&!wasBlocking)
            {
                savedTimeScale=Time.timeScale>0?Time.timeScale:1;savedCursor=Cursor.lockState;savedVisible=Cursor.visible;
                foreach(var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                    if(canvas.gameObject!=gameObject){hidden[canvas]=canvas.enabled;canvas.enabled=false;}
                foreach(var legacy in FindObjectsByType<SaBanHUD>(FindObjectsInactive.Include,FindObjectsSortMode.None)){hiddenLegacy[legacy]=legacy.enabled;legacy.enabled=false;}
            }
            Page=next;Time.timeScale=blocking?0:savedTimeScale;
            if(blocking){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
            else
            {
                foreach(var pair in hidden)if(pair.Key!=null)pair.Key.enabled=pair.Value;hidden.Clear();
                foreach(var pair in hiddenLegacy)if(pair.Key!=null)pair.Key.enabled=pair.Value;hiddenLegacy.Clear();
                bool modal=OpeningEvent.IsOpeningActive||VillageWorkStall.Active!=null||CampRecoveryGame.Active!=null||BeachBoatMinigameStation.Active!=null||(StoryDialogueUI.Instance?.IsOpen??false);
                Cursor.lockState=modal?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=modal;
                StoryDialogueUI.TryConsumeInput();
            }
            background.SetActive(blocking);home.SetActive(next==MenuPage.Home);pause.SetActive(next==MenuPage.Pause);settings.SetActive(next==MenuPage.Settings);confirm.SetActive(next==MenuPage.ConfirmExit);loading.SetActive(next==MenuPage.Loading);slots.SetActive(next==MenuPage.Slots);overwrite.SetActive(next==MenuPage.ConfirmOverwrite);
            if(next==MenuPage.Settings){volumeSlider.SetValueWithoutNotify(BachDangPreferences.Volume);mouseSlider.SetValueWithoutNotify(BachDangPreferences.Sensitivity);RefreshSettingsLabels();}
        }
        void RefreshSettingsLabels(){volumeValue.text=Mathf.RoundToInt(BachDangPreferences.Volume*100)+"%";mouseValue.text=BachDangPreferences.Sensitivity.ToString("0.00")+"×";}
        void Build()
        {
            var canvas=gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=12000;
            var scaler=gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;gameObject.AddComponent<GraphicRaycaster>();
            background=Box("MenuBackground",transform,Vector2.zero,Vector2.one,Color.black);
            var art=new GameObject("BachDang938Artwork",typeof(RectTransform),typeof(RawImage),typeof(AspectRatioFitter));art.transform.SetParent(background.transform,false);
            var texture=Resources.Load<Texture2D>("BachDang938/MainMenu");art.GetComponent<RawImage>().texture=texture;art.GetComponent<RawImage>().raycastTarget=false;
            var fit=art.GetComponent<AspectRatioFitter>();fit.aspectMode=AspectRatioFitter.AspectMode.EnvelopeParent;fit.aspectRatio=texture!=null?(float)texture.width/texture.height:16f/9;
            var gradient=new GameObject("InkGradient",typeof(RectTransform),typeof(CanvasRenderer),typeof(BachDangMenuGradient));gradient.transform.SetParent(background.transform,false);Stretch((RectTransform)gradient.transform,Vector2.zero,Vector2.one);
            gradient.GetComponent<BachDangMenuGradient>().raycastTarget=false;
            Box("TopGold",background.transform,new Vector2(.035f,.954f),new Vector2(.965f,.956f),new Color(Gold.r,Gold.g,Gold.b,.35f));
            Label("Era",background.transform,new Vector2(.69f,.91f),new Vector2(.96f,.95f),12,Gold,"GIAO CHÂU • NĂM MẬU TUẤT",TextAlignmentOptions.Right);
            Label("Footer",background.transform,new Vector2(.68f,.04f),new Vector2(.96f,.08f),12,Cream,"ĐỊA HÌNH  /  THỦY TRIỀU  /  MƯU LƯỢC",TextAlignmentOptions.Right);
            home=Box("Home",transform,Vector2.zero,Vector2.one,Color.clear);
            Label("Eyebrow",home.transform,new Vector2(.075f,.83f),new Vector2(.48f,.89f),14,Gold,"NGÔ QUYỀN • HÀNH TRÌNH GIỮ NƯỚC");
            var heading=Label("Title",home.transform,new Vector2(.07f,.69f),new Vector2(.59f,.83f),64,Cream,"BẠCH ĐẰNG");heading.fontStyle=FontStyles.Bold;heading.characterSpacing=3;
            var year=Label("Year",home.transform,new Vector2(.075f,.58f),new Vector2(.4f,.70f),52,Gold,"938");year.characterSpacing=1;
            Box("TitleRule",home.transform,new Vector2(.077f,.565f),new Vector2(.35f,.568f),Gold);
            Click("Play",home.transform,new Vector2(.077f,.44f),new Vector2(.365f,.515f),"PLAY  /  CHƠI MỚI",Play,true);
            continueButton=Click("Continue",home.transform,new Vector2(.077f,.35f),new Vector2(.365f,.425f),"TIẾP TỤC",()=>ContinueGame());
            loadButton=Click("LoadGame",home.transform,new Vector2(.077f,.26f),new Vector2(.365f,.335f),"LOAD GAME  /  Ô LƯU",()=>LoadGame());
            Click("Settings",home.transform,new Vector2(.077f,.17f),new Vector2(.365f,.245f),"SETTINGS  /  CÀI ĐẶT",OpenSettings);
            Click("RankingGuide",home.transform,new Vector2(.077f,.08f),new Vector2(.365f,.155f),"HƯỚNG DẪN XẾP HẠNG",()=>CampaignJournalUI.Instance?.OpenRules());
            saveInfo=Label("SaveDetails",home.transform,new Vector2(.077f,.012f),new Vector2(.48f,.068f),13,new Color(.70f,.76f,.69f),"");
            pause=Card("Pause","TẠM DỪNG");
            Click("Resume",pause.transform,new Vector2(.12f,.67f),new Vector2(.88f,.77f),"TIẾP TỤC  [ESC]",Resume,true);
            Click("Settings",pause.transform,new Vector2(.12f,.54f),new Vector2(.88f,.64f),"CÀI ĐẶT",OpenSettings);
            Click("Save",pause.transform,new Vector2(.12f,.41f),new Vector2(.88f,.51f),"LƯU TIẾN TRÌNH  [F5]",()=>SaveNow());
            Click("Load",pause.transform,new Vector2(.12f,.28f),new Vector2(.88f,.38f),"LOAD GAME",()=>LoadGame());
            Click("Exit",pause.transform,new Vector2(.12f,.12f),new Vector2(.88f,.22f),"THOÁT GAME",AskExit);
            settings=Card("Settings","CÀI ĐẶT");
            Label("Volume",settings.transform,new Vector2(.10f,.65f),new Vector2(.72f,.75f),22,Cream,"ÂM LƯỢNG");volumeValue=Label("VolumeValue",settings.transform,new Vector2(.75f,.65f),new Vector2(.90f,.75f),22,Gold,"");
            volumeSlider=MakeSlider("VolumeSlider",settings.transform,new Vector2(.10f,.56f),new Vector2(.90f,.63f),0,1,BachDangPreferences.Volume);
            volumeSlider.onValueChanged.AddListener(v=>{BachDangPreferences.SetVolume(v);RefreshSettingsLabels();BachDangMenuAudio.SliderChanged();});
            Label("Mouse",settings.transform,new Vector2(.10f,.40f),new Vector2(.72f,.50f),22,Cream,"ĐỘ NHẠY CHUỘT");mouseValue=Label("MouseValue",settings.transform,new Vector2(.75f,.40f),new Vector2(.90f,.50f),22,Gold,"");
            mouseSlider=MakeSlider("MouseSlider",settings.transform,new Vector2(.10f,.31f),new Vector2(.90f,.38f),.25f,2.5f,BachDangPreferences.Sensitivity);
            mouseSlider.onValueChanged.AddListener(v=>{BachDangPreferences.SetSensitivity(v);RefreshSettingsLabels();BachDangMenuAudio.SliderChanged();});
            TMP_Text graphicsLabel=null;
            var graphicsButton=Click("GraphicsQuality",settings.transform,new Vector2(.10f,.215f),new Vector2(.90f,.29f),"ĐỒ HỌA: "+CinematicQuality.Label,()=>{CinematicQuality.Cycle();graphicsLabel.text="ĐỒ HỌA: "+CinematicQuality.Label;});
            graphicsLabel=graphicsButton.GetComponentInChildren<TMP_Text>();graphicsLabel.fontSize=18;
            Label("Applied",settings.transform,new Vector2(.10f,.17f),new Vector2(.90f,.21f),12,new Color(.65f,.74f,.67f),"Áp dụng ngay • Tự nhớ cho lần chơi sau",TextAlignmentOptions.Center);
            Click("Back",settings.transform,new Vector2(.24f,.07f),new Vector2(.76f,.17f),"QUAY LẠI",BackFromSettings,true);
            confirm=Card("ConfirmExit","LƯU TRƯỚC KHI THOÁT?");
            confirmText=Label("Message",confirm.transform,new Vector2(.10f,.43f),new Vector2(.90f,.78f),23,Cream,"",TextAlignmentOptions.Center);
            confirmSave=Click("Yes",confirm.transform,new Vector2(.10f,.29f),new Vector2(.90f,.39f),"YES  /  LƯU VÀ THOÁT",()=>ExitWithSave(),true);
            Click("No",confirm.transform,new Vector2(.10f,.16f),new Vector2(.90f,.26f),"NO  /  THOÁT KHÔNG LƯU",ExitWithoutSave);
            Click("Cancel",confirm.transform,new Vector2(.28f,.03f),new Vector2(.72f,.12f),"HỦY",CancelExit);
            slots=Box("SaveSlots",transform,new Vector2(.12f,.10f),new Vector2(.88f,.90f),Ink);
            Box("TopBorder",slots.transform,new Vector2(0,.995f),Vector2.one,Gold);
            slotsTitle=Label("Heading",slots.transform,new Vector2(.04f,.87f),new Vector2(.96f,.98f),28,Gold,"",TextAlignmentOptions.Center);
            for(int i=0;i<BachDangSaveSystem.SlotCount;i++)
            {
                int slot=i+1,column=i%2,row=i/2;float x=.035f+column*.485f,y=.725f-row*.137f;
                slotButtons[i]=Click("Slot"+slot,slots.transform,new Vector2(x,y),new Vector2(x+.445f,y+.118f),"",()=>ChooseSlot(slot));
                slotLabels[i]=slotButtons[i].transform.Find("Label").GetComponent<TMP_Text>();slotLabels[i].fontSize=16;
                slotLabels[i].enableAutoSizing=true;slotLabels[i].fontSizeMin=12;slotLabels[i].fontSizeMax=16;
            }
            Click("Cancel",slots.transform,new Vector2(.32f,.025f),new Vector2(.68f,.10f),"QUAY LẠI  [ESC]",CancelSlots);
            overwrite=Card("ConfirmOverwrite","GHI ĐÈ TIẾN TRÌNH?");
            overwriteText=Label("Message",overwrite.transform,new Vector2(.08f,.38f),new Vector2(.92f,.79f),22,Cream,"",TextAlignmentOptions.Center);
            Click("Yes",overwrite.transform,new Vector2(.10f,.22f),new Vector2(.90f,.34f),"CÓ • GHI ĐÈ",()=>ConfirmOverwrite(),true);
            Click("No",overwrite.transform,new Vector2(.10f,.07f),new Vector2(.90f,.19f),"KHÔNG • CHỌN Ô KHÁC",CancelOverwrite);
            loading=Box("Loading",transform,new Vector2(.3f,.4f),new Vector2(.7f,.6f),Ink);Label("Label",loading.transform,Vector2.zero,Vector2.one,25,Gold,"ĐANG TẢI HÀNH TRÌNH…",TextAlignmentOptions.Center);
            toast=Box("SaveNotice",transform,new Vector2(.25f,.02f),new Vector2(.75f,.10f),Ink);notice=Label("Text",toast.transform,new Vector2(.03f,.05f),new Vector2(.97f,.95f),17,Cream,"",TextAlignmentOptions.Center);toast.SetActive(false);
        }
        GameObject Card(string name,string title)
        {
            var go=Box(name,transform,new Vector2(.285f,.13f),new Vector2(.715f,.88f),Ink);
            Box("TopBorder",go.transform,new Vector2(0,.995f),Vector2.one,Gold);
            Label("Heading",go.transform,new Vector2(.05f,.81f),new Vector2(.95f,.96f),30,Gold,title,TextAlignmentOptions.Center);return go;
        }
        static Slider MakeSlider(string name,Transform parent,Vector2 min,Vector2 max,float low,float high,float value)
        {
            var go=Box(name,parent,min,max,Color.clear);var slider=go.AddComponent<Slider>();slider.minValue=low;slider.maxValue=high;
            Box("Track",go.transform,new Vector2(0,.40f),new Vector2(1,.60f),new Color(.19f,.29f,.25f));
            var fillArea=Box("FillArea",go.transform,new Vector2(.01f,.4f),new Vector2(.99f,.6f),Color.clear);
            slider.fillRect=(RectTransform)Box("Fill",fillArea.transform,Vector2.zero,Vector2.one,Gold).transform;
            var handle=Box("Handle",go.transform,new Vector2(0,.08f),new Vector2(0,.92f),Cream);var rect=(RectTransform)handle.transform;rect.sizeDelta=new Vector2(12,0);slider.handleRect=rect;slider.targetGraphic=handle.GetComponent<Image>();slider.SetValueWithoutNotify(value);return slider;
        }
        static Button Click(string name,Transform parent,Vector2 min,Vector2 max,string caption,UnityEngine.Events.UnityAction action,bool primary=false)
        {
            var color=primary?new Color(.47f,.30f,.13f,.96f):new Color(.07f,.15f,.13f,.94f);var go=Box(name,parent,min,max,color);
            Box("Rule",go.transform,Vector2.zero,new Vector2(.008f,1),Gold);
            var button=go.AddComponent<Button>();button.targetGraphic=go.GetComponent<Image>();var colors=button.colors;colors.highlightedColor=new Color(1.3f,1.25f,1.05f);colors.pressedColor=new Color(.70f,.65f,.5f);colors.disabledColor=new Color(.45f,.45f,.45f,.65f);button.colors=colors;button.navigation=new Navigation{mode=Navigation.Mode.Automatic};button.onClick.AddListener(()=>{BachDangMenuAudio.Click();action();});
            Label("Label",go.transform,new Vector2(.07f,.05f),new Vector2(.96f,.95f),21,Cream,caption);return button;
        }
        static void Stretch(RectTransform rect,Vector2 min,Vector2 max){rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;}
        static GameObject Box(string name,Transform parent,Vector2 min,Vector2 max,Color color)
        {var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);Stretch((RectTransform)go.transform,min,max);go.GetComponent<Image>().color=color;return go;}
        static TMP_Text Label(string name,Transform parent,Vector2 min,Vector2 max,float size,Color color,string text,TextAlignmentOptions alignment=TextAlignmentOptions.MidlineLeft)
        {var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);Stretch((RectTransform)go.transform,min,max);var label=go.GetComponent<TMP_Text>();StoryFont.Apply(label);label.fontSize=size;label.color=color;label.text=text;label.alignment=alignment;return label;}
        void OnDestroy(){if(Instance==this){Instance=null;Time.timeScale=1;}}
    }
}
