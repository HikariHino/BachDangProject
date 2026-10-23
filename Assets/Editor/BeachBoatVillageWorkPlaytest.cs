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
    static int uniqueTimberOrders,hardRiceOrders,exhaustiveRiceCases;
    static readonly HashSet<string> timberHistory=new HashSet<string>();
    static bool layoutSpread,interactionClear,treesClear;
    static string failure;
    static readonly List<object> Results=new List<object>();
    public static void RunAndExit(){EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
    static void Move(Vector3 p)
    {var player=BeachBoatQuestSession.Instance.player;player.transform.position=p;var body=player.GetComponent<Rigidbody>();body.position=p;body.linearVelocity=Vector3.zero;Physics.SyncTransforms();}
    static void Finish()
    {
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/VillageWorkPlaytest.json",JsonConvert.SerializeObject(new{success=failure==null,failure,timeoutChecked,wrongChecked,retryChecked,difficultyChecked,drumChecked,randomizedAttempts,uniqueTimberOrders,hardRiceOrders,exhaustiveRiceCases,layoutSpread,interactionClear,treesClear,results=Results,story=Story.Instance?.currentState.ToString()},Formatting.Indented));
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
                var sites=quest.stalls.Select(s=>s.transform.position).ToArray();
                layoutSpread=sites.Max(p=>p.x)-sites.Min(p=>p.x)>500&&sites.Max(p=>p.z)-sites.Min(p=>p.z)>500;
                for(int a=0;a<sites.Length;a++)for(int b=a+1;b<sites.Length;b++)layoutSpread&=QuestRowboat.FlatDistance(sites[a],sites[b])>=180;
                interactionClear=quest.stalls.All(s=>!Physics.OverlapCapsule(s.InteractionPosition+Vector3.up*3,s.InteractionPosition+Vector3.up*19,4,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore).Any(c=>!(c is TerrainCollider)&&c.GetComponentInParent<Animator>()==null));
                var terrain=Terrain.activeTerrain;
                treesClear=terrain.terrainData.treeInstances.All(t=>sites.All(p=>QuestRowboat.FlatDistance(p,terrain.transform.position+Vector3.Scale(t.position,terrain.terrainData.size))>=55));
                if(!layoutSpread||!interactionClear||!treesClear)throw new Exception("Stalls are clustered, an interaction is blocked, or a terrain tree crosses a stall");
                BeachBoatStory.BachDangFrontEnd.Instance?.Play();
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
            if(phase==11)
            {
                Click("VillageWorkCanvas/WorkOverlay/Workbench/Instructions/StartWork");phase=2;waitUntil=EditorApplication.timeSinceStartup+.15;return;
            }
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
                stall.StartAttempt(2);difficultyChecked|=stall.Difficulty==0;stall.AdvanceTime(stall.TimeLimit+1);stall.StartAttempt(0);
                retryChecked=stall.Phase==VillageWorkStall.WorkPhase.Playing&&stall.Elapsed==0&&stall.Progress==0;
                if(!timeoutChecked||!retryChecked||!difficultyChecked)throw new Exception("Timeout/retry/difficulty rules failed");
                if(stall.kind==VillageWorkStall.WorkKind.Timber)
                {int bad=Enumerable.Range(0,stall.TimberOptions).First(i=>!stall.IsSoundTimber(i));wrongChecked=!stall.SubmitOption(bad)&&stall.Mistakes==1&&!stall.Completed;}
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
                if(stall.CompletedLevels<3)
                {
                    if(stall.Completed||quest.CompletedCount!=index||VillageWorkStall.Active!=stall||stall.Phase!=VillageWorkStall.WorkPhase.Instructions||stall.Difficulty!=stall.CompletedLevels)throw new Exception("Level unlock skipped progression or granted early goods");
                    phase=11;waitUntil=EditorApplication.timeSinceStartup+.2;return;
                }
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
                        if(trade.kind==VillageWorkStall.WorkKind.Timber)
                        for(int n=0;n<20;n++){UnityEngine.Random.InitState(938);trade.StartAttempt(2);SolveAttempt(trade);randomizedAttempts++;}
                        trade.Close();
                    }
                    var minimum=typeof(VillageWorkStall).GetMethod("MinimumScoops",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
                    foreach(var pair in new[]{new[]{2,3},new[]{3,5},new[]{4,7},new[]{5,8}})
                    for(int amount=9;amount<23;amount++)
                    {int expected=SolveRice(amount,pair[0],pair[1]).Length,actual=(int)minimum.Invoke(null,new object[]{amount,pair[0],pair[1]});if(actual!=expected)throw new Exception("Hard rice move limit is not solvable at minimum moves");exhaustiveRiceCases++;}
                    UnityEngine.Random.state=randomState;
                    if(!wrongChecked||!drumChecked)throw new Exception("Input or drum regression check failed");Finish();
                }
            }
        }
        catch(Exception e){failure=e.ToString();Finish();}
    }
    static bool RopeSolution(VillageWorkStall stall,int cell,int entry,int visited,Dictionary<int,int> solution)
    {
        if((visited&(1<<cell))!=0)return false;visited|=1<<cell;
        int ports=stall.RopePorts(cell);
        for(int turn=0;turn<4;turn++,ports=VillageWorkStall.RotatePorts(ports))
        {
            if((ports&entry)==0)continue;int exit=ports^entry;solution[cell]=ports;
            if(cell==5&&exit==2)return true;
            int next=exit==1?cell-3:exit==2?cell+1:exit==4?cell+3:cell-1;
            int nextEntry=exit==1?4:exit==2?8:exit==4?1:2;
            if(next>=0&&next<9&&Mathf.Abs(next%3-cell%3)+Mathf.Abs(next/3-cell/3)==1&&RopeSolution(stall,next,nextEntry,visited,solution))return true;
        }
        solution.Remove(cell);return false;
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
                var cards=Enumerable.Range(0,stall.TimberOptions).Select(i=>$"{stall.IsSoundTimber(i)}:{stall.TimberLength(i)}:{(stall.TimberNeedsStrength?stall.TimberStrength(i):0)}").OrderBy(v=>v,StringComparer.Ordinal);
                string key=$"{stall.Difficulty}:{stall.TimberTarget}:{stall.TimberStrengthTarget}:{stall.TimberBudget}|"+string.Join(";",cards);
                if(!timberHistory.Add(key))throw new Exception("A timber question repeated, including reordered cards");uniqueTimberOrders++;
                bool IndependentSolution(int mask)
                {
                    var selected=Enumerable.Range(0,stall.TimberOptions).Where(i=>(mask&(1<<i))!=0).ToArray();
                    return selected.Length==stall.TimberRequiredPieces&&selected.All(stall.IsSoundTimber)&&selected.Sum(stall.TimberLength)==stall.TimberTarget&&
                        (!stall.TimberNeedsStrength||selected.Sum(stall.TimberStrength)>=stall.TimberStrengthTarget);
                }
                var solutions=Enumerable.Range(1,(1<<stall.TimberOptions)-1).Where(IndependentSolution).ToArray();
                if(solutions.Length!=1)throw new Exception("Timber order does not have exactly one solution");
                for(int i=0;i<stall.TimberOptions;i++)if((solutions[0]&(1<<i))!=0)stall.SubmitOption(i);
                stall.SubmitOption(8);
            }
            else if(stall.kind==VillageWorkStall.WorkKind.Rope)
            {
                var targets=new Dictionary<int,int>();if(!RopeSolution(stall,3,8,0,targets))throw new Exception("Generated rope has no solution");
                foreach(var target in targets)for(int n=0;n<4&&stall.RopePorts(target.Key)!=target.Value;n++)stall.SubmitOption(target.Key);
                if(!stall.RopeConnected())throw new Exception("Rope solution did not form a connected path");stall.SubmitOption(9);
            }
            else if(stall.kind==VillageWorkStall.WorkKind.Rice)
            {var steps=SolveRice(stall.RiceTarget,stall.RiceSmall,stall.RiceLarge);if(stall.Difficulty==2){if(steps.Length!=stall.MoveLimit)throw new Exception("Hard rice budget disagrees with independently solved shortest route");hardRiceOrders++;}foreach(int action in steps)stall.SubmitOption(action);stall.SubmitOption(3);}
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
