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
        bool[] savedControls;
        string tourTag;
        bool tourCameraEnabled;
        bool tourAudioEnabled;
        AudioListener tourAudio;
        GameObject questCanvas;
        GameObject controlsPanel;
        TMP_Text controlsText;
        NPCDialogue[] talkers;
        NPCQuestGiver questGiver;
        CampGateTransition[] gates;
        public static bool InputBlocked => InteractionBlocked ||
            (BeachBoatMinigameStation.Active != null && BeachBoatMinigameStation.Active.kind == BeachBoatMinigameStation.ActivityKind.WarDrum) ||
            StoryDialogueUI.InputConsumedThisFrame;
        public static bool InteractionBlocked => OpeningEvent.IsOpeningActive ||
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
            if (player == null || storyCamera == null || tourCamera == null || playerSpawn == null)
            { Debug.LogError("BeachBoat story bindings are incomplete.", this); enabled = false; return; }
            savedControls = new bool[tourControls.Length];
            for (int i = 0; i < tourControls.Length; i++) savedControls[i] = tourControls[i] != null && tourControls[i].enabled;
            tourTag = tourCamera.tag; tourCameraEnabled = tourCamera.enabled;
            tourAudio = tourCamera.GetComponent<AudioListener>(); tourAudioEnabled = tourAudio != null && tourAudio.enabled;
            player.transform.SetPositionAndRotation(playerSpawn.position, playerSpawn.rotation);
            var body = player.GetComponent<Rigidbody>();
            body.position = playerSpawn.position; body.rotation = playerSpawn.rotation;
            body.linearVelocity = Vector3.zero;
            BuildQuestUI();
            var dialogue = Child("BeachStoryDialogue"); dialogue.AddComponent<StoryDialogueUI>();
            var fader = Child("BeachStoryFader"); fader.AddComponent<ScreenFader>();
            SetStoryMode(true);
            BuildIntro();
        }

        // One nearby NPC owns E. Mission dialogue takes priority in its three report stages.
        public bool IsPreferredTalker(NPCDialogue candidate)
        {
            if (player == null || !player.activeInHierarchy) return false;
            var position = player.transform.position;
            var state = GetComponent<BachDangStoryManager>().currentState;
            bool reporting = state == BachDangStoryManager.StoryState.Act1_ExploreCamp ||
                state == BachDangStoryManager.StoryState.Act2_ReportToNgoQuyen ||
                state == BachDangStoryManager.StoryState.Act2_CompleteReturnToCamp;
            if (reporting && questGiver != null && questGiver.isActiveAndEnabled &&
                (questGiver.transform.position - position).sqrMagnitude <= questGiver.interactDistance * questGiver.interactDistance)
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
                if (distance <= npc.interactDistance * npc.interactDistance && distance < best)
                { best = distance; nearest = npc; }
            }
            return nearest == candidate;
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
            bool aboard = QuestRowboat.Instance != null && QuestRowboat.Instance.IsAboard;
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
            bool focusedOverlay = BeachBoatMinigameStation.Active != null ||
                (StoryDialogueUI.Instance != null && StoryDialogueUI.Instance.IsOpen);
            if (story != null && story.objectiveUIPanel != null)
                story.objectiveUIPanel.SetActive(!focusedOverlay && !OpeningEvent.IsOpeningActive);
            if (controlsPanel != null) controlsPanel.SetActive(!focusedOverlay && !OpeningEvent.IsOpeningActive);
            if (controlsText != null)
            {
                if (EnemyFleetDirector.Instance != null && EnemyFleetDirector.Instance.IsShowingBattle)
                {
                    controlsText.text = "Chuột: quan sát  •  SPACE: phất cờ  •  M: bản đồ";
                }
                else if (story != null && story.currentState == BachDangStoryManager.StoryState.Act3_BattleInProgress)
                {
                    controlsText.text = "W/S: chèo  •  A/D: lái  •  M: bản đồ";
                }
                else if (QuestRowboat.Instance != null && QuestRowboat.Instance.IsAboard)
                {
                    controlsText.text = "W/S: chèo  •  A/D: lái  •  E: hành động  •  M: bản đồ";
                }
                else
                {
                    controlsText.text = "WASD: di chuyển  •  Shift: chạy  •  E: tương tác  •  M: bản đồ";
                }
            }
            if (Input.GetKeyDown(KeyCode.F9) && !OpeningEvent.IsOpeningActive &&
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
            var controls = Panel("ControlsBar",canvas.transform,new Vector2(.015f,.012f),new Vector2(.47f,.065f),new Color(.02f,.025f,.03f,.68f));
            controlsPanel = controls;
            var keys = Text("Controls",controls.transform,new Vector2(.025f,.05f),new Vector2(.975f,.95f),14);
            controlsText = keys;
            keys.text = "WASD: di chuyển  •  Shift: chạy  •  E: tương tác  •  M: bản đồ";
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
