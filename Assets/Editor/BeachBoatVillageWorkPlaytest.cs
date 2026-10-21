using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BeachBoatStory;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using Story=BeachBoatStory.BachDangStoryManager;

public static class BeachBoatVillageWorkPlaytest
{
    const string Flag="VillageWorkPlaytest";
    static double started,waitUntil;
    static int phase,index;
    static bool timeoutChecked,wrongChecked,retryChecked,difficultyChecked,drumChecked;
    static int randomizedAttempts;
    static string failure;
    static readonly List<object> Results=new List<object>();
    public static void RunAndExit(){EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
    static void Move(Vector3 p)
    {var player=BeachBoatQuestSession.Instance.player;player.transform.position=p;var body=player.GetComponent<Rigidbody>();body.position=p;body.linearVelocity=Vector3.zero;Physics.SyncTransforms();}
    static void Finish()
    {
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/VillageWorkPlaytest.json",JsonConvert.SerializeObject(new{success=failure==null,failure,timeoutChecked,wrongChecked,retryChecked,difficultyChecked,drumChecked,randomizedAttempts,results=Results,story=Story.Instance?.currentState.ToString()},Formatting.Indented));
        SessionState.SetBool(Flag,false);BeachBoatQAExit.AfterPlayMode(failure==null?0:1);
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;
        if(started==0)started=EditorApplication.timeSinceStartup;
        if(EditorApplication.timeSinceStartup-started<3||EditorApplication.timeSinceStartup<waitUntil)return;
        if(EditorApplication.timeSinceStartup-started>75){failure="Village work test timed out at phase "+phase;Finish();return;}
        try
        {
            var quest=VillagePreparationQuest.Instance;if(quest==null||quest.stalls.Length!=4)throw new Exception("Four scene-owned market stalls missing");
            if(phase==0)
            {
                foreach(var intro in UnityEngine.Object.FindObjectsByType<BeachBoatStory.OpeningEvent>(FindObjectsSortMode.None))intro.SkipOpening();
                BeachBoatStory.StoryDialogueUI.Instance?.Close();
                var drum=UnityEngine.Object.FindObjectsByType<BeachBoatMinigameStation>(FindObjectsSortMode.None).First(s=>s.kind==BeachBoatMinigameStation.ActivityKind.WarDrum);
                Story.Instance.TransitionTo(Story.StoryState.Act1_Training,true);Move(drum.transform.position+Vector3.right*10);drum.BeginActivity();
                if(BeachBoatMinigameStation.Active!=drum)throw new Exception("Drum interaction regressed");
                int safety=0;while(BeachBoatMinigameStation.Active==drum&&safety++<30)drum.SubmitRhythm(drum.ExpectedBeat);
                drumChecked=drum.HasCompleted;BeachBoatStory.StoryDialogueUI.Instance?.Close();BeachBoatStory.StoryDialogueUI.Instance?.Close();
                Story.Instance.TransitionTo(Story.StoryState.Act2_SupplyPreparation,true);phase=1;return;
            }
            var stall=quest.stalls[index];
            if(phase==1)
            {
                BeachBoatStory.StoryDialogueUI.Instance?.Close();Move(stall.InteractionPosition);
                BeachBoatQuestSession.Instance.player.transform.rotation=stall.transform.rotation;
                BeachBoatQuestSession.Instance.storyCamera.GetComponent<BeachBoatStory.Camera_Script>().SnapToTarget();
                if(!stall.TryOpen())throw new Exception("Cannot interact with stall "+stall.name);
                phase=10;waitUntil=EditorApplication.timeSinceStartup+.15;return;
            }
            if(phase==10)
            {
                Click("VillageWorkCanvas/WorkOverlay/Workbench/Instructions/Difficulty0");
                Click("VillageWorkCanvas/WorkOverlay/Workbench/Instructions/StartWork");
                if(stall.Phase!=VillageWorkStall.WorkPhase.Playing||stall.Difficulty!=0)throw new Exception("Mouse cannot start selected difficulty");
                float easy=stall.TimeLimit;stall.AdvanceTime(easy+1);
                timeoutChecked=stall.Phase==VillageWorkStall.WorkPhase.Lost&&!stall.Completed&&quest.CompletedCount==index;
                stall.StartAttempt(2);difficultyChecked|=stall.TimeLimit<easy;stall.AdvanceTime(stall.TimeLimit+1);stall.StartAttempt(1);
                retryChecked=stall.Phase==VillageWorkStall.WorkPhase.Playing&&stall.Elapsed==0&&stall.Progress==0;
                if(!timeoutChecked||!retryChecked||!difficultyChecked)throw new Exception("Timeout/retry/difficulty rules failed");
                if(stall.kind==VillageWorkStall.WorkKind.Timber)
                {int bad=Enumerable.Range(0,4).First(i=>!stall.IsSoundTimber(i));wrongChecked=!stall.SubmitOption(bad)&&stall.Mistakes==1&&!stall.Completed;}
                else if(stall.kind==VillageWorkStall.WorkKind.Provisions)wrongChecked&=!stall.SubmitOption(3)&&stall.Mistakes==1;
                else if(stall.kind==VillageWorkStall.WorkKind.Rope)wrongChecked&=!stall.SubmitOption(9)&&stall.Mistakes==1;
                phase=2;waitUntil=EditorApplication.timeSinceStartup+.3;return;
            }
            if(phase==2)
            {
                Capture("VillageWork_"+stall.kind);
                foreach(var icon in UnityEngine.Object.FindObjectsByType<VillageWorkIcon>(FindObjectsSortMode.None))
                    if(icon.isActiveAndEnabled&&(icon.GetComponent<CanvasRenderer>()==null||icon.DrawnVertices<3))throw new Exception("Missing goods illustration at "+stall.kind);
                CaptureStall(stall);
                SolveAttempt(stall);
                if(stall.Phase!=VillageWorkStall.WorkPhase.Won)throw new Exception("Could not complete "+stall.kind+": "+stall.Feedback);
                if(stall.Completed||quest.CompletedCount!=index)throw new Exception("Goods awarded before claiming the result");
                phase=3;waitUntil=EditorApplication.timeSinceStartup+.15;return;
            }
            if(phase==3)
            {
                Click("VillageWorkCanvas/WorkOverlay/Workbench/Result/ConfirmResult");
                if(quest.CompletedCount!=index+1||VillageWorkStall.Active!=null)throw new Exception("Mouse claim/progress/modal close failed");
                Results.Add(new{stall.stallName,stall.kind,stall.Completed,stall.Progress,stall.Mistakes,stall.Elapsed});index++;phase=1;
                if(index==4)
                {
                    BeachBoatStory.StoryDialogueUI.Instance?.Close();
                    if(Story.Instance.currentState!=Story.StoryState.Act2_ReturnToCampGate)throw new Exception("Four successful stalls did not advance quest 2");
                    BeachBoatStory.StoryDialogueUI.Instance?.Close();
                    var randomState=UnityEngine.Random.state;
                    foreach(var trade in quest.stalls)
                    {
                        Move(trade.InteractionPosition);if(!trade.TryOpen())throw new Exception("Replay could not open "+trade.kind);
                        for(int level=0;level<3;level++)for(int seed=0;seed<12;seed++)
                        {
                            UnityEngine.Random.InitState(10000+(int)trade.kind*1000+level*100+seed);
                            trade.StartAttempt(level);SolveAttempt(trade);
                            if(trade.Phase!=VillageWorkStall.WorkPhase.Won||trade.Mistakes!=0)throw new Exception("Unsolvable randomized order: "+trade.kind+" level "+level+" seed "+seed);
                            randomizedAttempts++;
                        }
                        trade.Close();
                    }
                    UnityEngine.Random.state=randomState;
                    if(!wrongChecked||!drumChecked)throw new Exception("Input or drum regression check failed");Finish();
                }
            }
        }
        catch(Exception e){failure=e.ToString();Finish();}
    }
    static void Click(string path)
    {
        var target=GameObject.Find(path);if(target==null||EventSystem.current==null)throw new Exception("Missing UI input target "+path);
        Canvas.ForceUpdateCanvases();var rect=target.GetComponent<RectTransform>();
        var point=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
        var pointer=new PointerEventData(EventSystem.current){position=point,button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        if(hits.Count==0||ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)!=target)throw new Exception("UI pointer blocked at "+path);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    static void SolveAttempt(VillageWorkStall stall)
    {
        int safety=0;
        while(stall.Phase==VillageWorkStall.WorkPhase.Playing&&safety++<100)
        {
            if(stall.kind==VillageWorkStall.WorkKind.Timber)
            {
                var pair=(from a in Enumerable.Range(0,4) from b in Enumerable.Range(a+1,3-a)
                    where stall.IsSoundTimber(a)&&stall.IsSoundTimber(b)&&stall.TimberLength(a)+stall.TimberLength(b)==stall.TimberTarget select new[]{a,b}).First();
                stall.SubmitOption(pair[0]);stall.SubmitOption(pair[1]);
            }
            else if(stall.kind==VillageWorkStall.WorkKind.Rope)
            {
                bool upper=stall.RopePorts(1)!=5&&stall.RopePorts(1)!=10;
                var targets=upper?new Dictionary<int,int>{{3,10},{4,9},{1,6},{2,12},{5,3}}:new Dictionary<int,int>{{3,10},{4,12},{7,3},{8,9},{5,6}};
                foreach(var target in targets)for(int n=0;n<4&&stall.RopePorts(target.Key)!=target.Value;n++)stall.SubmitOption(target.Key);
                if(!stall.RopeConnected())throw new Exception("Rope solution did not form a connected path");stall.SubmitOption(9);
            }
            else if(stall.kind==VillageWorkStall.WorkKind.Rice)
            {foreach(int action in SolveRice(stall.RiceTarget,stall.RiceSmall,stall.RiceLarge))stall.SubmitOption(action);stall.SubmitOption(3);}
            else
            {for(int i=0;i<3;i++)for(int n=0;n<stall.RequiredIngredient(i);n++)stall.SubmitOption(i);stall.SubmitOption(3);}
        }
    }
    static void Capture(string name)
    {
        var source=BeachBoatQuestSession.Instance.storyCamera;var go=new GameObject("VillageWorkQACamera");var camera=go.AddComponent<Camera>();camera.CopyFrom(source);camera.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);camera.enabled=false;
        var canvases=UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c=>c.isRootCanvas&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
        var rt=new RenderTexture(1280,720,24);var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);camera.targetTexture=rt;
        foreach(var c in canvases){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=2;}
        Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();Directory.CreateDirectory("Logs");File.WriteAllBytes("Logs/"+name+".png",pixels.EncodeToPNG());
        foreach(var c in canvases){c.renderMode=RenderMode.ScreenSpaceOverlay;c.worldCamera=null;}RenderTexture.active=null;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(go);
    }
    static int[] SolveRice(int target,int small,int large)
    {
        var queue=new Queue<int>();queue.Enqueue(0);var paths=new Dictionary<int,int[]>{{0,Array.Empty<int>()}};
        while(queue.Count>0)
        {
            int current=queue.Dequeue();if(current==target)return paths[current];
            for(int action=0;action<3;action++)
            {int next=current+(action==0?small:action==1?large:-small);if(next<0||next>50||paths.ContainsKey(next))continue;paths[next]=paths[current].Concat(new[]{action}).ToArray();queue.Enqueue(next);}
        }
        throw new Exception("Rice order has no solution");
    }
    static void CaptureStall(VillageWorkStall stall)
    {
        var go=new GameObject("StallQACamera");var camera=go.AddComponent<Camera>();camera.CopyFrom(BeachBoatQuestSession.Instance.storyCamera);camera.enabled=false;camera.farClipPlane=500;camera.fieldOfView=55;
        camera.transform.position=stall.transform.TransformPoint(new Vector3(70,65,-95));camera.transform.LookAt(stall.transform.position+Vector3.up*20);
        var rt=new RenderTexture(1000,750,24);var pixels=new Texture2D(1000,750,TextureFormat.RGB24,false);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1000,750),0,0);pixels.Apply();File.WriteAllBytes("Logs/Stall_"+stall.kind+".png",pixels.EncodeToPNG());
        RenderTexture.active=null;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(go);
    }
}
