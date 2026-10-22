using System;
using System.IO;
using System.Linq;
using BeachBoatStory;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using StoryOpening = BeachBoatStory.OpeningEvent;
using StoryDialogue = BeachBoatStory.StoryDialogueUI;
using StoryFader = BeachBoatStory.ScreenFader;
using StakeZone = BeachBoatStory.RiverStakeZone;

/// <summary>Headless Play Mode check that every new camp service changes gameplay state.</summary>
public static class BeachBoatCampFacilityPlaytest
{
    const string Flag = "BeachBoatCampFacilityPlaytest";
    const string PendingExit = "BeachBoatCampFacilityPlaytestExit";
    static double started;

    public static void RunAndExit()
    {
        SessionState.SetInt(PendingExit, -1);
        EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity", OpenSceneMode.Single);
        SessionState.SetBool(Flag, true);
        started = 0;
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    static void Register()
    {
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        int code = SessionState.GetInt(PendingExit, -1);
        if (state != PlayModeStateChange.EnteredEditMode || code < 0) return;
        SessionState.SetInt(PendingExit, -1);
        EditorApplication.delayCall += () => EditorApplication.Exit(code);
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Flag, false) || !EditorApplication.isPlaying) return;
        if (started == 0) started = EditorApplication.timeSinceStartup;
        if (EditorApplication.timeSinceStartup - started < 3) return;
        string failure = null;
        bool rested = false, armored = false, trained = false, supplied = false, healed = false, commandReachable = false;
        int facilityCount = 0;
        try
        {
            foreach (var opening in UnityEngine.Object.FindObjectsByType<StoryOpening>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                opening.SkipOpening();
            StoryDialogue.Instance?.Close();
            var session = BeachBoatQuestSession.Instance;
            var story = session != null ? session.GetComponent<BeachBoatStory.BachDangStoryManager>() : null;
            var vitals = session?.player?.GetComponent<PlayerVitals>();
            var facilities = UnityEngine.Object.FindObjectsByType<CampFacility>(FindObjectsSortMode.None);
            facilityCount = facilities.Length;
            if (session == null || story == null || vitals == null || facilityCount != 27)
            {
                if (EditorApplication.timeSinceStartup - started < 35) return;
                var allStories = UnityEngine.Object.FindObjectsByType<BeachBoatStory.BachDangStoryManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                throw new InvalidOperationException($"Missing scene bindings: session={session != null}({session?.name}), " +
                    $"story={story != null}, player={session != null && session.player != null}, " +
                    $"vitals={vitals != null}, facilities={facilityCount}, allStories={allStories.Length} " +
                    string.Join(",", allStories.Select(s => s.name + ":" + s.gameObject.activeInHierarchy)));
            }
            if (BeachBoatQuestSession.InteractionBlocked)
            {
                if (EditorApplication.timeSinceStartup - started < 35) return;
                throw new InvalidOperationException($"Interactions stayed blocked: opening={StoryOpening.IsOpeningActive}, " +
                    $"planting={StakeZone.Instance != null && StakeZone.Instance.IsPlanting}, " +
                    $"storyMode={session.storyMode}, dialogue={StoryDialogue.Instance != null && StoryDialogue.Instance.IsOpen}, " +
                    $"fader={StoryFader.Instance != null && StoryFader.Instance.IsTransitioning}.");
            }

            bool Use(CampFacility.FacilityKind kind)
            {
                StoryDialogue.Instance?.Close();
                var facility = facilities.First(f => f.kind == kind);
                session.player.transform.position = facility.transform.position;
                var body = session.player.GetComponent<Rigidbody>();
                if (body != null) { body.position = facility.transform.position; body.linearVelocity = Vector3.zero; }
                Physics.SyncTransforms();
                if(!facility.TryUse())return false;
                if(kind==CampFacility.FacilityKind.Forge)return true;
                var game=CampRecoveryGame.Active;
                if(game==null)return false;
                float hp=vitals.Health,sp=vitals.Stamina;
                if(game.ClaimReward()||vitals.Health!=hp||vitals.Stamina!=sp)throw new Exception("Opening a tent granted an unearned reward");
                game.StartAttempt();game.AdvanceTime(game.TimeLimit+1);
                if(game.Phase!=CampRecoveryGame.GamePhase.Lost||game.ClaimReward())throw new Exception("Timeout granted recovery");
                int levels=game.Training?3:1;float previousWidth=1,previousTime=10;
                for(int level=1;level<=levels;level++)
                {
                hp=vitals.Health;sp=vitals.Stamina;float beforeMaximum=vitals.maxStamina;
                game.StartAttempt();
                if(game.Training)
                {
                    if(game.TrainingLevel!=level||game.GreenWidth>=previousWidth||game.CrossingSeconds>=previousTime)throw new Exception("Training difficulty did not narrow and speed up");
                    previousWidth=game.GreenWidth;previousTime=game.CrossingSeconds;
                    var green=game.transform.Find("CampRecoveryCanvas/RecoveryOverlay/Card/Activity/Rhythm/BreathTrack/GoodZone").GetComponent<RectTransform>();
                    if(Mathf.Abs(green.anchorMax.x-green.anchorMin.x-game.GreenWidth)>.0001f)throw new Exception("Visible green zone differs from gameplay hit window");
                    game.AdvanceTime(.1f);float first=game.RhythmPosition;game.AdvanceTime(.1f);
                    if(Mathf.Abs(game.RhythmPosition-first-.1f/game.CrossingSeconds)>.001f)throw new Exception("Cursor did not accelerate with training difficulty");
                }
                Capture("Recovery_"+kind+"_Level"+level);
                float lastCorrect=-100;
                for(int i=0;i<game.Goal;i++)
                {
                    if(!game.Sequence)
                    {
                        for(int step=0;step<600;step++)
                        {
                            float phase=game.RhythmPosition;
                            if(phase>game.GreenMin+.01f&&phase<game.GreenMax-.01f&&game.Elapsed-lastCorrect>.67f&&game.Submit(game.ExpectedAction)){lastCorrect=game.Elapsed;break;}
                            game.AdvanceTime(.005f);
                            if(game.Phase!=CampRecoveryGame.GamePhase.Playing&&game.Phase!=CampRecoveryGame.GamePhase.Won)throw new Exception("Could not complete recovery rhythm");
                        }
                    }
                    else if(!game.Submit(game.ExpectedAction))throw new Exception("Recovery sequence rejected correct action");
                }
                if(game.Phase!=CampRecoveryGame.GamePhase.Won||vitals.Health!=hp||vitals.Stamina!=sp)throw new Exception("Reward must wait for explicit claim");
                if(!game.ClaimReward())throw new Exception("Won recovery could not be claimed");
                if(game.Training&&(vitals.maxStamina!=beforeMaximum+20||vitals.CompletedTrainingLevels!=level||game.ClaimReward()))throw new Exception("Training reward duplicated or did not add exactly 20");
                }
                if(kind==CampFacility.FacilityKind.Armory)
                {
                    float maximum=vitals.maxStamina;
                    if(vitals.TrainAtArmory(4)||!vitals.TrainAtArmory(3)||vitals.maxStamina!=maximum)throw new Exception("Completed levels can farm extra maximum stamina");
                    vitals.RestoreCheckpoint(vitals.ArmorCharges);
                    if(vitals.CompletedTrainingLevels!=3||vitals.maxStamina!=160)throw new Exception("Checkpoint erased training progression");
                }
                return CampRecoveryGame.Active==null;
            }

            vitals.TakeImpact(40, "QA");
            vitals.TryExert(25, 1);
            rested = Use(CampFacility.FacilityKind.Barracks) && vitals.Health > 90 &&
                     Mathf.Approximately(vitals.Stamina, vitals.maxStamina);
            armored = Use(CampFacility.FacilityKind.Forge) && vitals.ArmorCharges == 3;
            float beforeHit = vitals.Health;
            vitals.TakeImpact(20, "QA");
            armored &= Mathf.Abs((beforeHit - vitals.Health) - 14) < .1f && vitals.ArmorCharges == 2;
            trained = Use(CampFacility.FacilityKind.Armory) && vitals.maxStamina == 160 &&
                      Mathf.Approximately(vitals.Stamina, vitals.maxStamina);
            vitals.TakeImpact(20,"Provisions QA after training checkpoint");
            float beforeFood = vitals.Health;
            supplied = Use(CampFacility.FacilityKind.Provisions) && vitals.Health > beforeFood;
            vitals.TakeImpact(30,"QA");
            healed = Use(CampFacility.FacilityKind.Medical) && Mathf.Approximately(vitals.Health,vitals.maxHealth);
            commandReachable=BeachBoatCommandWalkQA.Check(session);
            if (!rested || !armored || !trained || !supplied || !healed || !commandReachable)
                failure = "A camp service failed or the relocated command stage could not be reached.";
        }
        catch (Exception error) { failure = error.ToString(); }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/BeachBoatCampFacilityPlaytest.json", JsonConvert.SerializeObject(new
        { success = failure == null, failure, facilityCount, rested, armored, trained, supplied, healed, commandReachable }, Formatting.Indented));
        SessionState.SetBool(Flag, false);
        SessionState.SetInt(PendingExit, failure == null ? 0 : 1);
        EditorApplication.isPlaying = false;
    }
    public static void Capture(string name)
    {
        Canvas.ForceUpdateCanvases();
        var texture=UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).First(c=>c.name=="CampRecoveryCanvas");
        var camera=BeachBoatQuestSession.Instance.storyCamera;var previous=texture.renderMode;var previousCamera=texture.worldCamera;
        texture.renderMode=RenderMode.ScreenSpaceCamera;texture.worldCamera=camera;texture.planeDistance=2;
        var target=new RenderTexture(1280,720,24);var png=new Texture2D(1280,720,TextureFormat.RGB24,false);
        camera.targetTexture=target;camera.Render();RenderTexture.active=target;png.ReadPixels(new Rect(0,0,1280,720),0,0);png.Apply();
        Directory.CreateDirectory("Logs");File.WriteAllBytes("Logs/"+name+".png",png.EncodeToPNG());
        camera.targetTexture=null;RenderTexture.active=null;texture.renderMode=previous;texture.worldCamera=previousCamera;
        UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(png);
    }
}
