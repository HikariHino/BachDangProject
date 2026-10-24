using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using BeachBoatStory;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Object=UnityEngine.Object;
using Story=BeachBoatStory.BachDangStoryManager;
using Gate=BeachBoatStory.CampGateTransition;
using Intro=BeachBoatStory.OpeningEvent;

/// <summary>Real scene reloads and a separate-process load; never touches the player's own save.</summary>
public static class BeachBoatFrontEndPlaytest
{
    const string Flag="FrontEnd938QA",Cold="FrontEnd938ColdQA";
    static int phase,gateIndex;
    static double started,until;
    static float volume,sensitivity,elapsed;
    static bool preferencesCaptured;
    static BachDangSaveData expected;
    static byte[] previousSave;
    static readonly List<string> checks=new List<string>();
    static string runtimeError;
    static string DirectoryPath=>Path.GetFullPath("Logs/FrontEndSaveQA");
    public static void RunAndExit(){Start(false);}
    public static void ColdLoadAndExit(){Start(true);}
    static void Start(bool cold)
    {
        EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");SessionState.SetBool(Flag,true);SessionState.SetBool(Cold,cold);
        EditorApplication.isPlaying=true;
    }
    [InitializeOnLoadMethod]static void Register()
    {
        EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        Application.logMessageReceived-=RecordError;Application.logMessageReceived+=RecordError;
    }
    static void RecordError(string message,string stack,LogType type)
    {
        if(SessionState.GetBool(Flag,false)&&EditorApplication.isPlaying&&(type==LogType.Error||type==LogType.Exception||type==LogType.Assert))
            runtimeError=message+"\n"+stack;
    }
    static bool CompleteSlot(BachDangFrontEnd menu,int slot)
    {
        if(!menu.ChooseSlot(slot))return false;
        return menu.Page!=BachDangFrontEnd.MenuPage.ConfirmOverwrite||menu.ConfirmOverwrite();
    }
    static bool SaveOne(BachDangFrontEnd menu)=>menu.SaveNow()&&CompleteSlot(menu,1);
    static bool LoadOne(BachDangFrontEnd menu)=>menu.LoadGame()&&menu.ChooseSlot(1);
    static bool ExitOne(BachDangFrontEnd menu)=>menu.ExitWithSave()&&CompleteSlot(menu,1);
    static void CheckTenSlots(BachDangFrontEnd menu)
    {
        for(int slot=1;slot<=10;slot++)
        {
            Check(menu.OpenSaveSlots("QA ô "+slot)&&CompleteSlot(menu,slot),"Write selectable slot "+slot);
            Check(BachDangSaveSystem.TryRead(slot,out var data)&&data.location=="QA ô "+slot,"Read independent progress in slot "+slot);
        }
        var before=Enumerable.Range(1,10).Select(i=>File.ReadAllBytes(BachDangSaveSystem.SlotPath(i))).ToArray();
        menu.OpenSaveSlots("Thay thế ô 7");menu.ChooseSlot(7);
        Check(menu.Page==BachDangFrontEnd.MenuPage.ConfirmOverwrite,"Full slots require explicit overwrite confirmation");
        menu.CancelOverwrite();menu.CancelSlots();
        Check(Enumerable.Range(1,10).All(i=>before[i-1].SequenceEqual(File.ReadAllBytes(BachDangSaveSystem.SlotPath(i)))),"Cancel overwrite leaves all ten saves untouched");
        menu.OpenSaveSlots("Thay thế ô 7");menu.ChooseSlot(7);Check(menu.ConfirmOverwrite(),"Confirm overwrite succeeds");
        Check(BachDangSaveSystem.TryRead(7,out var replaced)&&replaced.location=="Thay thế ô 7","Chosen slot receives replacement progress");
        Check(Enumerable.Range(1,10).Where(i=>i!=7).All(i=>before[i-1].SequenceEqual(File.ReadAllBytes(BachDangSaveSystem.SlotPath(i)))),"Other nine slots remain byte-identical after overwrite");
        BachDangSaveSystem.SelectSlot(1);
    }
    static void PointerClick(string path)
    {
        var button=BachDangFrontEnd.Instance.transform.Find(path).GetComponent<Button>();
        Canvas.ForceUpdateCanvases();var events=EventSystem.current;
        Check(events!=null&&button.IsInteractable(),"Pointer can interact with "+path);
        var rect=(RectTransform)button.transform;
        var pointer=new PointerEventData(events){button=PointerEventData.InputButton.Left,pointerId=-1,
            position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center))};
        var hits=new List<RaycastResult>();events.RaycastAll(pointer,hits);
        Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Actual UI raycast reaches "+path+" (hits: "+string.Join(", ",hits.Take(3).Select(h=>h.gameObject.name))+ ")");
        pointer.pointerCurrentRaycast=hits[0];
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerUpHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);
        if(runtimeError!=null)throw new Exception(runtimeError);
    }
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add(label);}
    static void Move(Vector3 p)
    {
        var player=BeachBoatQuestSession.Instance.player;player.transform.position=p;var body=player.GetComponent<Rigidbody>();body.position=p;
        if(!body.isKinematic)body.linearVelocity=Vector3.zero;Physics.SyncTransforms();
    }
    static void Capture(string name)
    {
        var camera=BeachBoatQuestSession.Instance.storyCamera;var canvas=BachDangFrontEnd.Instance.GetComponent<Canvas>();
        canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=2;
        var rt=new RenderTexture(1280,720,24){hideFlags=HideFlags.HideAndDontSave};var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false){hideFlags=HideFlags.HideAndDontSave};
        camera.targetTexture=rt;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;
        pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();File.WriteAllBytes("Logs/BachDang938-"+name+".png",pixels.EncodeToPNG());
        canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.worldCamera=null;RenderTexture.active=null;camera.targetTexture=null;Object.DestroyImmediate(rt);Object.DestroyImmediate(pixels);
    }
    static void VerifyFootsteps()
    {
        var session=BeachBoatQuestSession.Instance;EnemyFleetDirector.Instance.ResetForCheckpoint();QuestRowboat.Instance.LeaveForCheckpoint();
        Move(session.playerSpawn.position);var motor=session.player.GetComponent<BeachBoatStory.Character_Movement>();motor.enabled=false;
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        typeof(BeachBoatStory.Character_Movement).GetField("<IsGrounded>k__BackingField",flags).SetValue(motor,true);
        var sprint=typeof(BeachBoatStory.Character_Movement).GetField("sprint",flags);
        var audio=BeachBoatSoundscape.Instance;var type=typeof(BeachBoatSoundscape);var tick=type.GetMethod("Update",flags);var next=type.GetField("nextStep",flags);
        type.GetField("previous",flags).SetValue(audio,session.player.transform.position);type.GetField("distance",flags).SetValue(audio,0f);next.SetValue(audio,-100f);
        sprint.SetValue(motor,false);Move(session.player.transform.position+Vector3.forward*15);tick.Invoke(audio,null);
        var walk=audio.LastFootstep;float walkInterval=(float)next.GetValue(audio)-Time.time;
        next.SetValue(audio,-100f);sprint.SetValue(motor,true);Move(session.player.transform.position+Vector3.forward*15);tick.Invoke(audio,null);
        float runInterval=(float)next.GetValue(audio)-Time.time;
        Check(walk!=null&&audio.LastFootstep!=walk&&audio.RunningFootstepsPlayed>0,"Real walking and running movement use different clips");
        Check(runInterval>0&&runInterval<walkInterval,"Running movement has a faster footfall cadence");
        int before=audio.FootstepsPlayed;next.SetValue(audio,-100f);tick.Invoke(audio,null);Check(audio.FootstepsPlayed==before,"Standing still produces no footsteps");
    }
    static void Finish(string failure)
    {
        if(preferencesCaptured){BachDangPreferences.SetVolume(volume);BachDangPreferences.SetSensitivity(sensitivity);}
        Directory.CreateDirectory("Logs");File.WriteAllText(SessionState.GetBool(Cold,false)?"Logs/FrontEndColdLoadPlaytest.json":"Logs/FrontEndPlaytest.json",
            JsonConvert.SerializeObject(new{success=failure==null,failure,phase,checks},Formatting.Indented));
        BachDangSaveSystem.TestDirectory=null;SessionState.SetBool(Flag,false);BeachBoatQAExit.AfterPlayMode(failure==null?0:1);
    }
    static void Compare(BachDangSaveData saved)
    {
        var session=BeachBoatQuestSession.Instance;var story=Story.Instance;var v=session.player.GetComponent<PlayerVitals>();
        Check((int)story.currentState==saved.storyState,"Loaded exact chapter");
        Check(Vector3.Distance(session.player.transform.position,saved.playerPosition)<1,"Loaded exact player coordinates");
        Check(story.collectedWood==saved.woodCount&&story.destroyedEnemyShips==saved.destroyedCount,"Loaded campaign counters");
        Check(session.woodItems.Select(w=>w.IsCollected).SequenceEqual(saved.woods),"Loaded individual collectibles");
        Check(BeachBoatStory.RiverStakeZone.Instance.CaptureCheckpoint().SequenceEqual(saved.stakes),"Loaded built stake clusters");
        Check(VillagePreparationQuest.Instance.stalls.Select(s=>s.CompletedLevels).SequenceEqual(saved.stallLevels),"Loaded each stall's easy-to-hard progress");
        Check(v.CompletedTrainingLevels==saved.trainingLevels&&Mathf.Abs(v.maxStamina-saved.maxStamina)<.01f&&Mathf.Abs(v.Health-saved.health)<.01f,"Loaded health and permanent stamina training");
        Check(session.storyCamera.GetComponent<BeachBoatStory.Camera_Script>().mouseXSpeed==2*BachDangPreferences.Sensitivity,"Sensitivity survives scene reload");
        Check(session.GetComponentsInChildren<Canvas>(true).Single(c=>c.name=="BeachStoryHUD").enabled,"Gameplay HUD restored after load");
        if(!saved.aboard)Check(session.player.GetComponent<BeachBoatStory.Character_Movement>().enabled&&!session.player.GetComponent<Rigidbody>().isKinematic&&session.player.GetComponent<CapsuleCollider>().enabled,"A foot save restores walking and physical collisions");
        if(saved.dialogue!=null&&saved.dialogue.active)Check(BeachBoatStory.StoryDialogueUI.Instance.IsOpen&&(BeachBoatStory.StoryDialogueUI.Instance.CaptureSave().action??"")==(saved.dialogue.action??""),"Pending quest dialogue and its action survive loading");
        if(saved.fleet.active)
        {
            var fleet=EnemyFleetDirector.Instance;
            Check(fleet.BattleStarted&&fleet.CommandScore==saved.fleet.commands&&fleet.EbbStarted==saved.fleet.ebb,"Battle and ebb resume without resetting");
            foreach(var ship in fleet.enemyShips.Where(s=>s.gameObject.activeSelf&&!s.isDestroyed))
            {
                var visual=ship.transform.Find("RedSailJunkVisual");var renderers=visual.GetComponentsInChildren<Renderer>(true);
                float bottom=renderers.Min(r=>r.bounds.min.y);Check(Mathf.Abs(bottom-(TideSystem.CurrentWaterHeight-4))<2,"Loaded live hull stays seated on water: "+ship.name);
            }
            var destroyed=saved.ships.Where(s=>s.destroyed).ToArray();
            Check(destroyed.Length>0&&destroyed.All(s=>fleet.enemyShips.First(vessel=>vessel.name==s.name).isDestroyed),"Sunk hull remains sunk after reload");
            Check(RiverPlayerCombat.Instance.Kills==saved.fleet.kills&&RiverPlayerCombat.Instance.Arrows==saved.fleet.arrows,"Archery progress restored");
            Check((int)RiverBoardingCombat.Instance.State==saved.boarding.state,"Player's swimming or boarding state restored");
            Check(saved.guards.Where(g=>g.independent&&g.active).All(g=>RiverPlayerCombat.Instance.guards.Any(x=>BachDangSaveSystem.GuardId(x)==g.id&&x.IsIndependentBoarder&&x.Health==g.health)),"Shipwreck survivors restored individually");
        }
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;
        if(started==0)started=EditorApplication.timeSinceStartup;
        if(EditorApplication.timeSinceStartup-started<4||EditorApplication.timeSinceStartup<until)return;
        if(EditorApplication.timeSinceStartup-started>160){Finish("Timeout");return;}
        try
        {
            if(runtimeError!=null)throw new Exception(runtimeError);
            var session=BeachBoatQuestSession.Instance;var front=BachDangFrontEnd.Instance;var story=Story.Instance;
            if(session==null||front==null)return;
            BachDangSaveSystem.TestDirectory=DirectoryPath;front.SuppressQuitForTests=true;
            // Never perform the corrupt-file/write-failure tests against real player saves.
            if(!DirectoryPath.StartsWith(Path.GetFullPath("Logs")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||
                !string.Equals(Path.GetDirectoryName(Path.GetFullPath(BachDangSaveSystem.SavePath)),DirectoryPath,StringComparison.OrdinalIgnoreCase))
                throw new Exception("QA save directory is not isolated");
            if(phase==0)
            {
                volume=BachDangPreferences.Volume;sensitivity=BachDangPreferences.Sensitivity;preferencesCaptured=true;
                Check(front.Page==BachDangFrontEnd.MenuPage.Home&&Time.timeScale==0,"Game starts paused on title lobby");
                Check(Object.FindFirstObjectByType<Intro>().CurrentLine==-1,"Intro waits for Play");
                var art=front.GetComponentInChildren<RawImage>(true);Check(art.texture!=null&&art.texture.width>=1200,"Historical title artwork loaded");
                Check(front.GetComponentsInChildren<Graphic>(true).All(g=>g.GetComponent<CanvasRenderer>()!=null),"Every menu graphic has a CanvasRenderer");
                Check(!front.GetComponentInChildren<BachDangMenuGradient>(true).raycastTarget,"Decorative gradient does not intercept clicks");
                if(SessionState.GetBool(Cold,false))
                {
                    Check(BachDangSaveSystem.TryRead(7,out expected),"A separate Unity process reads slot seven from disk");
                    Check(front.LoadGame()&&front.ChooseSlot(7),"Cold title Load Game loads the selected non-default slot");phase=90;return;
                }
                Directory.CreateDirectory(DirectoryPath);
                for(int slot=1;slot<=10;slot++)foreach(string suffix in new[]{"",".bak",".tmp"}){string file=BachDangSaveSystem.SlotPath(slot)+suffix;if(!Path.GetFullPath(file).StartsWith(DirectoryPath+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new Exception("Unsafe QA path");if(File.Exists(file))File.Delete(file);}
                front.RefreshSaveInfo();Check(!front.transform.Find("Home/LoadGame").GetComponent<Button>().interactable,"Load disabled before first save");
                Capture("Title");
                PointerClick("Home/Settings");Check(front.Page==BachDangFrontEnd.MenuPage.Settings,"Settings opens by pointer click");var sliders=front.GetComponentsInChildren<Slider>(true);sliders.First(s=>s.name=="VolumeSlider").value=.43f;sliders.First(s=>s.name=="MouseSlider").value=1.65f;
                Check(Mathf.Abs(AudioListener.volume-.43f)<.001f&&Mathf.Abs(session.storyCamera.GetComponent<BeachBoatStory.Camera_Script>().mouseXSpeed-3.3f)<.001f,"Both real settings sliders apply immediately");
                Capture("Settings");phase=10;until=EditorApplication.timeSinceStartup+.1;return;
            }
            if(phase==10){PointerClick("Settings/Back");phase=11;until=EditorApplication.timeSinceStartup+.1;return;}
            if(phase==11)
            {
                PointerClick("Home/Play");Check(!BachDangFrontEnd.IsBlocking&&Time.timeScale>0,"New Game pointer click resumes time and opens the intro");
                phase=1;until=EditorApplication.timeSinceStartup+1;return;
            }
            if(phase==1)
            {
                Check(Object.FindFirstObjectByType<Intro>().CurrentLine==0&&Intro.IsOpeningActive,"Play enters the existing story intro");
                var intro=Object.FindFirstObjectByType<Intro>();
                Check(intro.storyText.text.Contains("P: bỏ qua")&&!intro.HandleOpeningKey(KeyCode.Space),"Intro shows P to skip and no longer uses Space");
                Check(intro.HandleOpeningKey(KeyCode.P)&&intro.IsOpeningEnded,"P immediately skips the entire intro");BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
                story.TransitionTo(Story.StoryState.Act2_SupplyPreparation,true);story.collectedWood=2;
                for(int i=0;i<session.woodItems.Length;i++)session.woodItems[i].RestoreCollection(i<2);
                var stalls=VillagePreparationQuest.Instance.stalls;for(int i=0;i<stalls.Length;i++)stalls[i].RestoreLearningProgress(new[]{1,2,0,3}[i]);
                BeachBoatStory.RiverStakeZone.Instance.RestoreCheckpoint(new[]{true,true,false,false,false});
                var vitals=session.player.GetComponent<PlayerVitals>();vitals.TrainAtArmory(1);vitals.TrainAtArmory(2);vitals.TakeImpact(17,"QA");
                var points=Object.FindObjectsByType<CampaignSavePoint>(FindObjectsSortMode.None);Check(points.Length==3,"Three save tents exist");
                foreach(var point in points)
                {
                    Move(point.transform.position+point.transform.forward*17+Vector3.up*2);
                    Check(point.InRange&&PlayerArrival.IsClear(session.player,session.player.transform.position,Quaternion.identity)&&PlayerArrival.HasExit(session.player,session.player.transform.position,Quaternion.identity),"Save shelter has an accessible entrance: "+point.displayName);
                    Check(point.TryUse()&&CompleteSlot(front,1),"Save shelter opens slot picker and saves at "+point.displayName);
                }
                BeachBoatStory.StoryDialogueUI.Instance.Show("QA","Đơn hàng đã chuẩn bị; xác nhận để báo cáo.",story.OnSupplyPreparationComplete);
                Check(SaveOne(front),"Can save while a quest handoff dialogue is pending");
                CheckTenSlots(front);Check(SaveOne(front),"Restore dialogue fixture to slot one");
                expected=BachDangSaveSystem.Capture("QA");previousSave=File.ReadAllBytes(BachDangSaveSystem.SavePath);
                front.OpenPause();Check(Time.timeScale==0&&BeachBoatQuestSession.InputBlocked,"Pause blocks movement and world time");
                front.AskExit();Capture("ExitConfirmation");front.CancelExit();Check(front.Page==BachDangFrontEnd.MenuPage.Pause&&!front.QuitRequested&&previousSave.SequenceEqual(File.ReadAllBytes(BachDangSaveSystem.SavePath)),"Exit Cancel preserves game and save");
                front.AskExit();front.ExitWithoutSave();Check(front.QuitRequested&&previousSave.SequenceEqual(File.ReadAllBytes(BachDangSaveSystem.SavePath)),"Exit No does not overwrite save");
                front.Resume();
                string temp=BachDangSaveSystem.SavePath+".tmp";Directory.CreateDirectory(temp);
                Check(!SaveOne(front)&&previousSave.SequenceEqual(File.ReadAllBytes(BachDangSaveSystem.SavePath)),"Failed write preserves previous save");Directory.Delete(temp);
                File.WriteAllText(BachDangSaveSystem.SavePath,"corrupt");Check(BachDangSaveSystem.TryRead(out _),"Corrupt primary falls back to verified backup");File.WriteAllBytes(BachDangSaveSystem.SavePath,previousSave);
                story.collectedWood=0;foreach(var w in session.woodItems)w.RestoreCollection(false);foreach(var stall in stalls)stall.RestoreLearningProgress(0);
                LoadOne(front);phase=2;return;
            }
            if(phase==2)
            {
                if(BachDangFrontEnd.IsBlocking)return;Compare(expected);
                BeachBoatStory.StoryDialogueUI.Instance.Close();Check(story.currentState==Story.StoryState.Act2_ReturnToCampGate,"Loaded quest dialogue still advances the correct story step");
                BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
                var facility=Object.FindObjectsByType<CampFacility>(FindObjectsSortMode.None).First(f=>f.kind==CampFacility.FacilityKind.Medical);
                Move(facility.transform.position);Check(CampRecoveryGame.Open(facility),"Recovery minigame opens");CampRecoveryGame.Active.StartAttempt();
                elapsed=CampRecoveryGame.Active.Elapsed;front.OpenPause();phase=3;until=EditorApplication.timeSinceStartup+.7;return;
            }
            if(phase==3)
            {
                Check(Mathf.Abs(CampRecoveryGame.Active.Elapsed-elapsed)<.001f,"Pause also freezes minigame's unscaled timer");front.Resume();
                Check(Cursor.lockState==CursorLockMode.None&&Cursor.visible,"Resume returns cursor to the open minigame");CampRecoveryGame.Active.Close();
                foreach(Story.StoryState state in Enum.GetValues(typeof(Story.StoryState)))
                    Check(Object.FindObjectsByType<Gate>(FindObjectsSortMode.None).All(g=>g.IsAvailable(state)),"Travel gates unlocked in "+state);
                story.TransitionTo(Story.StoryState.Act1_ExploreCamp,true);phase=4;
            }
            if(phase==4)
            {
                if(BeachBoatStory.ScreenFader.Instance.IsTransitioning)return;
                Check(story.currentState==Story.StoryState.Act1_ExploreCamp,"Free travel does not skip story progress");
                var gates=Object.FindObjectsByType<Gate>(FindObjectsSortMode.None).OrderBy(g=>g.name).ToArray();
                if(gateIndex<gates.Length){var gate=gates[gateIndex++];Move(gate.transform.position);Check(gate.TryInteract(),"Early travel: "+gate.name);return;}
                story.collectedWood=story.totalWoodRequired;BeachBoatStory.RiverStakeZone.Instance.RestoreCheckpoint(Enumerable.Repeat(true,5).ToArray());
                story.TransitionTo(Story.StoryState.Act3_WaitForLowTide,true);Move(QuestRowboat.Instance.boardingPoint.position);
                Check(QuestRowboat.Instance.TryBoard(),"Board battle fixture");RiverPlayerCombat.Instance.ReturnFireEnabled=false;
                phase=5;until=EditorApplication.timeSinceStartup+2.5;return;
            }
            if(phase==5)
            {
                var fleet=EnemyFleetDirector.Instance;var ship=fleet.enemyShips.FirstOrDefault(s=>s.gameObject.activeSelf);if(ship==null)return;ship.TakeDamage();ship.Step(7.1f);
                Check(RiverBoardingCombat.Instance.TryTraverse(),"Begin swimming before save");
                front.OpenPause();front.AskExit();Check(ExitOne(front),"Exit Yes saves before quitting");
                Check(BachDangSaveSystem.TryRead(out expected)&&expected.fleet.active&&expected.ships.Any(s=>s.destroyed)&&expected.boarding.state==1,"Disk save contains battle, wreck and swimming state");
                Check(LoadOne(front),"Load fresh scene during active battle");phase=6;return;
            }
            if(phase==6||phase==90)
            {
                if(BachDangFrontEnd.IsBlocking)return;Compare(expected);
                phase=95;until=EditorApplication.timeSinceStartup+.3;return;
            }
            if(phase==95){BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();phase=96;until=EditorApplication.timeSinceStartup+.2;return;}
            if(phase==96){VerifyFootsteps();front.OpenPause();phase=99;until=EditorApplication.timeSinceStartup+.8;return;}
            if(phase==99){Capture("Pause");Finish(null);}
        }
        catch(Exception error){Finish(error.ToString());}
    }
}
