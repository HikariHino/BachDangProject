using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BeachBoatStory
{
    /// <summary>Scene-owned bindings for Thai's gameplay; contains no map-building or fixed TestQuest coordinates.</summary>
    [DefaultExecutionOrder(-500)]
    public sealed class BeachBoatQuestSession : MonoBehaviour
    {
        public static BeachBoatQuestSession Instance { get; private set; }
        public GameObject player;
        public Camera storyCamera, tourCamera;
        public Transform playerSpawn;
        public MonoBehaviour[] tourControls;
        public WoodCollectible[] woodItems;
        public bool storyMode = true;
        public float WorldUnitsPerMetre {get;private set;}=1;
        bool[] savedControls;
        string tourTag;
        bool tourCameraEnabled;
        bool tourAudioEnabled;
        AudioListener tourAudio;
        GameObject questCanvas;
        GameObject controlsPanel;
        GameObject vitalsPanel;
        TMP_Text controlsText;
        PlayerVitals vitals;
        Image healthFill, staminaFill;
        TMP_Text healthText, staminaText, impactText;
        GameObject boatPanel;TMP_Text boatText;Image boatFill;
        int shownHealth = -1, shownStamina = -1;
        NPCDialogue[] talkers;
        NPCQuestGiver questGiver;
        CampGateTransition[] gates;
        public static bool InputBlocked => InteractionBlocked ||
            (BeachBoatMinigameStation.Active != null &&
             (BeachBoatMinigameStation.Active.kind == BeachBoatMinigameStation.ActivityKind.WarDrum ||
              BeachBoatMinigameStation.Active.IsSelectingCargo || BeachBoatMinigameStation.Active.Failed)) ||
            StoryDialogueUI.InputConsumedThisFrame;
        public static bool InteractionBlocked => BachDangFrontEnd.IsBlocking || CampRecoveryGame.Active != null || OpeningEvent.IsOpeningActive || VillageWorkStall.Active != null ||
            (QuestCheckpoint.Instance != null && QuestCheckpoint.Instance.IsDefeated) ||
            (RiverStakeZone.Instance != null && RiverStakeZone.Instance.IsPlanting) ||
            (Instance != null && !Instance.storyMode) ||
            (StoryDialogueUI.Instance != null && StoryDialogueUI.Instance.IsOpen) ||
            (ScreenFader.Instance != null && ScreenFader.Instance.IsTransitioning);

        void Awake()
        {
            talkers = GetComponentsInChildren<NPCDialogue>(true);
            questGiver = GetComponentInChildren<NPCQuestGiver>(true);
            gates = GetComponentsInChildren<CampGateTransition>(true);
            Instance = this;
            RiverVesselScale.ApplyScene();
            foreach(var root in gameObject.scene.GetRootGameObjects())
            {
                var scale=root.GetComponent<BachDangWorldScale>();
                if(scale!=null&&scale.isActiveAndEnabled)
                {WorldUnitsPerMetre=Mathf.Max(.01f,scale.fittedCharacterHeight/Mathf.Max(.01f,scale.referenceCharacterHeight));break;}
            }
            if (player == null || storyCamera == null || tourCamera == null || playerSpawn == null)
            { Debug.LogError("BeachBoat story bindings are incomplete.", this); enabled = false; return; }
            vitals = player.GetComponent<PlayerVitals>();
            if (vitals == null) vitals = player.AddComponent<PlayerVitals>();
            savedControls = new bool[tourControls.Length];
            for (int i = 0; i < tourControls.Length; i++) savedControls[i] = tourControls[i] != null && tourControls[i].enabled;
            tourTag = tourCamera.tag; tourCameraEnabled = tourCamera.enabled;
            tourAudio = tourCamera.GetComponent<AudioListener>(); tourAudioEnabled = tourAudio != null && tourAudio.enabled;
            PlayerArrival.Teleport(player, playerSpawn.position, playerSpawn.rotation);
            var body = player.GetComponent<Rigidbody>();
            body.linearVelocity = Vector3.zero;
            BuildQuestUI();
            var dialogue = Child("BeachStoryDialogue"); dialogue.AddComponent<StoryDialogueUI>();
            var fader = Child("BeachStoryFader"); fader.AddComponent<ScreenFader>();
            SetStoryMode(true);
            BuildIntro();
            gameObject.AddComponent<QuestCheckpoint>();
            gameObject.AddComponent<VillageWoodHunt>();
            gameObject.AddComponent<CampaignProgress>();
            var frontEnd=Child("BachDangFrontEnd");frontEnd.AddComponent<BachDangFrontEnd>();
        }

        public bool IsPreferredGate(CampGateTransition candidate)
        {
            if(player==null||!player.activeInHierarchy)return false;
            CampGateTransition closest=null;float distance=float.PositiveInfinity;
            foreach(var gate in gates)
            {
                if(gate==null||!gate.isActiveAndEnabled||gate.destinationTarget==null||!gate.IsInRange(player.transform))continue;
                float current=QuestRowboat.FlatDistance(player.transform.position,gate.transform.position);
                if(current<distance-.001f||(Mathf.Abs(current-distance)<.001f&&(closest==null||(int)gate.gateType<(int)closest.gateType)))
                {closest=gate;distance=current;}
            }
            return closest==candidate;
        }

        // One nearby NPC owns E. Mission dialogue takes priority in its three report stages.
        public bool IsPreferredTalker(NPCDialogue candidate)
        {
            if (player == null || !player.activeInHierarchy || CampaignSavePoint.HasNearbyPoint()) return false;
            var position = player.transform.position;
            var state = GetComponent<BachDangStoryManager>().currentState;
            bool reporting = state == BachDangStoryManager.StoryState.Act1_ExploreCamp ||
                state == BachDangStoryManager.StoryState.Act2_ReportToNgoQuyen ||
                state == BachDangStoryManager.StoryState.Act2_CompleteReturnToCamp;
            if (reporting && questGiver != null && questGiver.isActiveAndEnabled &&
                questGiver.CanReachPlayer)
                return false;
            foreach (var gate in gates)
            {
                if (gate == null || !gate.isActiveAndEnabled || gate.destinationTarget == null) continue;
                if (gate.IsAvailable(state) && gate.IsInRange(player.transform)) return false;
            }
            if (state == BachDangStoryManager.StoryState.Act2_CollectWoodInVillage)
                foreach (var wood in woodItems)
                    if (wood != null && wood.isActiveAndEnabled &&
                        (wood.transform.position - position).sqrMagnitude <= wood.interactDistance * wood.interactDistance)
                        return false;
            NPCDialogue nearest = null;
            float best = float.PositiveInfinity;
            foreach (var npc in talkers)
            {
                if (npc == null || !npc.isActiveAndEnabled) continue;
                float distance = (npc.transform.position - position).sqrMagnitude;
                if (distance < best && NearbyConversation.InReach(npc.transform,npc.interactDistance))
                { best = distance; nearest = npc; }
            }
            return nearest == candidate && NearbyConversation.IsPreferred(candidate);
        }

        public void SetStoryMode(bool value)
        {
            if (!value && EnemyFleetDirector.Instance != null) EnemyFleetDirector.Instance.EndBattleView();
            storyMode = value;
            tourCamera.tag = value ? "Untagged" : "MainCamera";
            storyCamera.tag = value ? "MainCamera" : "Untagged";
            tourCamera.enabled = !value; storyCamera.enabled = value;
            if (tourAudio != null) tourAudio.enabled = !value;
            var audio = storyCamera.GetComponent<AudioListener>(); if (audio != null) audio.enabled = value;
            for (int i = 0; i < tourControls.Length; i++) if (tourControls[i] != null) tourControls[i].enabled = !value && savedControls[i];
            bool aboard = QuestRowboat.Instance != null && QuestRowboat.Instance.BlocksGroundInteractions;
            player.GetComponent<Character_Movement>().enabled = value && !aboard;
            var body = player.GetComponent<Rigidbody>();
            if (!body.isKinematic) body.linearVelocity = Vector3.zero;
            body.isKinematic = !value || aboard;
            var orbit = storyCamera.GetComponent<Camera_Script>(); orbit.enabled = value;
            if (value) orbit.SnapToTarget();
            orbit.CaptureCursor(value);
            if (questCanvas != null) questCanvas.SetActive(value);
        }
        void Update()
        {
            var story = GetComponent<BachDangStoryManager>();
            bool focusedOverlay = BachDangFrontEnd.IsBlocking || CampRecoveryGame.Active != null || BeachBoatMinigameStation.Active != null || VillageWorkStall.Active != null ||
                (QuestCheckpoint.Instance != null && QuestCheckpoint.Instance.IsDefeated) ||
                (StoryDialogueUI.Instance != null && StoryDialogueUI.Instance.IsOpen);
            if (story != null && story.objectiveUIPanel != null)
                story.objectiveUIPanel.SetActive(!focusedOverlay && !OpeningEvent.IsOpeningActive);
            if (controlsPanel != null) controlsPanel.SetActive(!focusedOverlay && !OpeningEvent.IsOpeningActive && !(RiverPlayerCombat.Instance?.BattleActive??false));
            if (vitalsPanel != null) vitalsPanel.SetActive(!focusedOverlay && !OpeningEvent.IsOpeningActive);
            if (impactText != null) impactText.gameObject.SetActive(!focusedOverlay && !OpeningEvent.IsOpeningActive);
            var rowboat=QuestRowboat.Instance;
            if(boatPanel!=null)
            {
                boatPanel.SetActive(!focusedOverlay&&!OpeningEvent.IsOpeningActive&&rowboat!=null&&rowboat.IsAboard);
                if(rowboat!=null){boatText.text=$"THUYỀN {rowboat.Durability:0}/100";boatFill.rectTransform.anchorMax=new Vector2(rowboat.Durability/QuestRowboat.MaximumDurability,1);boatFill.color=rowboat.Durability<=25?new Color(.9f,.24f,.16f):new Color(.86f,.66f,.29f);}
            }
            if (controlsText != null)
            {
                if (EnemyFleetDirector.Instance != null && EnemyFleetDirector.Instance.IsShowingBattle)
                {
                    controlsText.text = "WASD: chèo/bơi/đi • Q: rút/cất kiếm • F: xuống nước • Chuột trái: đánh • Chuột phải: ngắm/đỡ • Space: leo tàu";
                }
                else if (story != null && story.currentState == BachDangStoryManager.StoryState.Act3_BattleInProgress)
                {
                    controlsText.text = "W/S: chèo  •  A/D: lái  •  Shift: nhanh  •  M: bản đồ";
                }
                else if (QuestRowboat.Instance != null && QuestRowboat.Instance.IsAboard)
                {
                    controlsText.text = "W/S: chèo  •  A/D: lái  •  Shift: nhanh  •  E: hành động  •  M: bản đồ";
                }
                else
                {
                    controlsText.text = "WASD: di chuyển  •  Shift: chạy  •  E: tương tác  •  M: bản đồ  •  TAB: nhật ký";
                }
            }
            if (vitals != null && healthFill != null)
            {
                // These Images use Unity's plain white texture (no Sprite). Image.Type.Filled
                // ignores fillAmount in that case, so resize the colored rectangles instead.
                healthFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(vitals.HealthFraction), 1f);
                staminaFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(vitals.StaminaFraction), 1f);
                int hp = Mathf.CeilToInt(vitals.Health), sp = Mathf.CeilToInt(vitals.Stamina);
                if (hp != shownHealth) { healthText.text = $"Máu  {hp:0}/{vitals.maxHealth:0}"; shownHealth = hp; }
                if (sp != shownStamina) { staminaText.text = $"Thể lực  {sp:0}/{vitals.maxStamina:0}"; shownStamina = sp; }
                impactText.text = Time.unscaledTime < vitals.LastImpactUntil
                    ? vitals.LastImpact + (vitals.LastImpactDamage > 0 ? $"  -{vitals.LastImpactDamage:0} máu" : "")
                    : "";
                if(rowboat!=null&&Time.unscaledTime<rowboat.LastHullImpactUntil&&!string.IsNullOrEmpty(rowboat.LastHullImpact))impactText.text=rowboat.LastHullImpact;
            }
            if (Input.GetKeyDown(KeyCode.F9) && !InteractionBlocked && BeachBoatMinigameStation.Active == null &&
                !(StoryDialogueUI.Instance?.IsOpen ?? false) && !(ScreenFader.Instance?.IsTransitioning ?? false)) SetStoryMode(!storyMode);
        }
        GameObject Child(string name)
        { var go = new GameObject(name); go.transform.SetParent(transform, false); return go; }
        Canvas Canvas(string name)
        {
            var go = Child(name); var canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
            go.AddComponent<GraphicRaycaster>(); return canvas;
        }
        static GameObject Panel(string name, Transform parent, Vector2 min, Vector2 max, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform; rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
            go.AddComponent<Image>().color = color; return go;
        }
        static TextMeshProUGUI Text(string name, Transform parent, Vector2 min, Vector2 max, float size)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>(); StoryFont.Apply(text); text.fontSize = size; text.color = Color.white;
            text.textWrappingMode = TextWrappingModes.Normal;
            var rect = text.rectTransform; rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
            return text;
        }
        void BuildQuestUI()
        {
            if (UnityEngine.EventSystems.EventSystem.current == null)
                new GameObject("BeachStoryEventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.EventSystems.StandaloneInputModule)).transform.SetParent(transform, false);
            var canvas = Canvas("BeachStoryHUD"); canvas.sortingOrder = 100; questCanvas = canvas.gameObject;
            var panel = Panel("Objective", canvas.transform, new Vector2(.715f,.765f), new Vector2(.985f,.97f), new Color(.04f,.035f,.025f,.88f));
            var story = GetComponent<BachDangStoryManager>(); story.objectiveUIPanel = panel;
            story.objectiveTitleText = Text("Title",panel.transform,new Vector2(.06f,.72f),new Vector2(.94f,.94f),21);
            story.objectiveTitleText.color = new Color(1f,.83f,.38f);
            story.objectiveDescText = Text("Body",panel.transform,new Vector2(.06f,.08f),new Vector2(.94f,.70f),16);
            story.objectiveDescText.enableAutoSizing = true;
            story.objectiveDescText.fontSizeMin = 12;
            story.objectiveDescText.fontSizeMax = 16;
            var prompt = Panel("Interaction",canvas.transform,new Vector2(.30f,.095f),new Vector2(.70f,.16f),new Color(.02f,.025f,.03f,.9f));
            story.interactPromptPanel = prompt;
            story.interactPromptText = Text("Prompt",prompt.transform,new Vector2(.03f,.08f),new Vector2(.97f,.92f),19);
            story.interactPromptText.alignment = TextAlignmentOptions.Center; prompt.SetActive(false);
            var controls = Panel("ControlsBar",canvas.transform,new Vector2(.015f,.012f),new Vector2(.63f,.065f),new Color(.02f,.025f,.03f,.68f));
            controlsPanel = controls;
            var keys = Text("Controls",controls.transform,new Vector2(.025f,.05f),new Vector2(.975f,.95f),14);
            controlsText = keys;
            keys.enableAutoSizing = true; keys.fontSizeMin = 10; keys.fontSizeMax = 14;
            keys.text = "WASD: di chuyển  •  Shift: chạy  •  E: tương tác  •  M: bản đồ";
            vitalsPanel = Panel("Vitals", canvas.transform, new Vector2(.015f,.077f),
                new Vector2(.257f,.190f), new Color(.02f,.025f,.03f,.80f));
            healthText = Text("HealthLabel", vitalsPanel.transform, new Vector2(.04f,.56f),new Vector2(.44f,.96f),14);
            staminaText = Text("StaminaLabel", vitalsPanel.transform, new Vector2(.04f,.07f),new Vector2(.44f,.47f),14);
            var healthBack = Panel("HealthTrack", vitalsPanel.transform, new Vector2(.45f,.60f),
                new Vector2(.96f,.78f), new Color(.20f,.12f,.12f,.9f));
            healthFill = Panel("HealthFill", healthBack.transform, Vector2.zero,Vector2.one,
                new Color(.82f,.18f,.16f,1f)).GetComponent<Image>();
            healthFill.type = Image.Type.Simple;
            var staminaBack = Panel("StaminaTrack", vitalsPanel.transform, new Vector2(.45f,.12f),
                new Vector2(.96f,.30f), new Color(.10f,.20f,.19f,.9f));
            staminaFill = Panel("StaminaFill", staminaBack.transform, Vector2.zero,Vector2.one,
                new Color(.26f,.76f,.54f,1f)).GetComponent<Image>();
            staminaFill.type = Image.Type.Simple;
            impactText = Text("ImpactNotice",canvas.transform,new Vector2(.018f,.192f),new Vector2(.38f,.23f),15);
            impactText.color = new Color(1f,.67f,.53f); impactText.text = "";
            boatPanel=Panel("RowboatDurability",canvas.transform,new Vector2(.015f,.238f),new Vector2(.257f,.285f),new Color(.02f,.025f,.03f,.8f));
            boatText=Text("BoatDurabilityLabel",boatPanel.transform,new Vector2(.04f,.1f),new Vector2(.55f,.9f),13);
            var hullTrack=Panel("HullTrack",boatPanel.transform,new Vector2(.56f,.33f),new Vector2(.96f,.66f),new Color(.22f,.18f,.1f));
            boatFill=Panel("HullFill",hullTrack.transform,Vector2.zero,Vector2.one,new Color(.86f,.66f,.29f)).GetComponent<Image>();
            var guide = Panel("RouteGuide", canvas.transform, new Vector2(.35f,.875f), new Vector2(.65f,.97f), new Color(.04f,.035f,.025f,.82f));
            var pointer = Text("Direction",guide.transform,new Vector2(.02f,.2f),new Vector2(.16f,.8f),34);
            pointer.text = ">"; pointer.alignment = TextAlignmentOptions.Center;
            pointer.color = new Color(1f,.83f,.38f);
            var route = Text("Destination",guide.transform,new Vector2(.19f,.08f),new Vector2(.97f,.92f),16);
            route.alignment = TextAlignmentOptions.MidlineLeft;
            canvas.gameObject.AddComponent<StoryRouteGuide>().Initialize(this,guide,route,pointer);
            canvas.gameObject.AddComponent<BeachBoatMinimap>().Initialize(player.transform);
            canvas.gameObject.AddComponent<BeachBoatMinigameUI>().Initialize();
        }
        void BuildIntro()
        {
            var canvas = Canvas("BeachStoryIntro"); canvas.gameObject.SetActive(false);
            var panel = Panel("Background",canvas.transform,Vector2.zero,Vector2.one,Color.black);
            var text = Text("Story",panel.transform,new Vector2(.14f,.2f),new Vector2(.86f,.78f),30);
            text.alignment = TextAlignmentOptions.Center;
            var opening = canvas.gameObject.AddComponent<OpeningEvent>(); opening.openingPanel = panel; opening.storyText = text; opening.player = player;
            canvas.gameObject.SetActive(true);
        }
        void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            if (tourCamera != null) { tourCamera.enabled = tourCameraEnabled; tourCamera.tag = tourTag; }
            if (tourAudio != null) tourAudio.enabled = tourAudioEnabled;
            if (savedControls != null) for (int i=0;i<tourControls.Length;i++) if (tourControls[i] != null) tourControls[i].enabled = savedControls[i];
        }
    }
}
