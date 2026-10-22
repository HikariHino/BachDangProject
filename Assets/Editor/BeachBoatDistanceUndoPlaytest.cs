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
using UnityEngine.UI;
using Object=UnityEngine.Object;

public static class BeachBoatDistanceUndoPlaytest
{
    const string Flag="DistanceUndoQA";
    static double started,until;
    static int phase;
    static bool physicalMetres,nearText,farText,undoAll,restoreAll,penaltiesKept,roundBoundary,mouseUndo,mouseRestore;
    static VillageWorkStall food;
    public static void RunAndExit(){EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
    static void Finish(string failure)
    {
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/DistanceUndoPlaytest.json",JsonConvert.SerializeObject(new{success=failure==null,failure,physicalMetres,nearText,farText,undoAll,restoreAll,penaltiesKept,roundBoundary,mouseUndo,mouseRestore},Formatting.Indented));
        VillageWorkStall.Active?.Close();SessionState.SetBool(Flag,false);BeachBoatQAExit.AfterPlayMode(failure==null?0:1);
    }
    static void Move(Vector3 p)
    {var go=BeachBoatQuestSession.Instance.player;var body=go.GetComponent<Rigidbody>();body.isKinematic=true;go.transform.position=p;body.position=p;Physics.SyncTransforms();}
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;
        if(started==0)started=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-started<4||EditorApplication.timeSinceStartup<until)return;
        if(EditorApplication.timeSinceStartup-started>65){Finish("Timeout at "+phase);return;}
        try
        {
            var session=BeachBoatQuestSession.Instance;var story=BeachBoatStory.BachDangStoryManager.Instance;
            var drum=Object.FindObjectsByType<BeachBoatMinigameStation>(FindObjectsSortMode.None).First(s=>s.kind==BeachBoatMinigameStation.ActivityKind.WarDrum);
            var guide=Object.FindAnyObjectByType<StoryRouteGuide>();
            if(phase==0)
            {
                foreach(var intro in Object.FindObjectsByType<BeachBoatStory.OpeningEvent>(FindObjectsSortMode.None))intro.SkipOpening();BeachBoatStory.StoryDialogueUI.Instance?.Close();
                story.TransitionTo(BeachBoatStory.BachDangStoryManager.StoryState.Act1_Training,true);
                physicalMetres=Mathf.Abs(session.WorldUnitsPerMetre-14)<.01f&&Mathf.Abs(WorldDistance.ToMetres(21)-1.5f)<.001f;
                if(!physicalMetres)throw new Exception("21 scene units are still reported as 21 metres");
                Move(drum.transform.position+Vector3.forward*Mathf.Min(21,drum.interactDistance*.8f));phase=1;until=EditorApplication.timeSinceStartup+.5;return;
            }
            if(phase==1)
            {
                var text=GameObject.Find("RouteGuide/Destination");if(text==null)text=Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None).First(t=>t.name=="Destination").gameObject;
                var caption=text.GetComponent<TMPro.TMP_Text>().text;
                nearText=guide.CurrentTarget==drum.transform&&guide.InInteractionRange(drum.transform)&&guide.CurrentDistanceMetres<2&&caption.Contains("Trong tầm")&&!caption.Contains("21 m");
                if(!nearText)throw new Exception("Nearby drum guide still says approach or reports wrong metres: "+caption);
                Capture("Route_Distance_Near");Move(drum.transform.position+Vector3.forward*(drum.interactDistance+140));phase=2;until=EditorApplication.timeSinceStartup+.4;return;
            }
            if(phase==2)
            {
                var caption=Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None).First(t=>t.name=="Destination").text;
                farText=!guide.InInteractionRange(drum.transform)&&caption.Contains("Đến gần");if(!farText)throw new Exception("Guide falsely allows distant interaction");
                story.TransitionTo(BeachBoatStory.BachDangStoryManager.StoryState.Act2_SupplyPreparation,true);
                undoAll=restoreAll=true;
                foreach(var stall in VillagePreparationQuest.Instance.stalls)
                {
                    Move(stall.InteractionPosition);if(!stall.TryOpen())throw new Exception("Cannot open "+stall.kind);
                    if(stall.Undo()||stall.RestoreCurrentTask())throw new Exception("Undo is active in instructions");
                    stall.StartAttempt(1);stall.AdvanceTime(1.25f);int people=stall.People,target=stall.RiceTarget,wood=stall.TimberTarget;
                    int[] ports=Enumerable.Range(0,9).Select(stall.RopePorts).ToArray();
                    int option=stall.kind==VillageWorkStall.WorkKind.Timber?Enumerable.Range(0,4).First(stall.IsSoundTimber):0;
                    stall.SubmitOption(option);float elapsed=stall.Elapsed;int mistakes=stall.Mistakes;
                    if(!stall.Undo()||stall.CanUndo||stall.Elapsed!=elapsed||stall.Mistakes!=mistakes||stall.RiceLoad!=0||stall.TimberTotal!=0||stall.Moves!=0||Enumerable.Range(0,3).Any(i=>stall.IngredientCount(i)!=0)||!ports.SequenceEqual(Enumerable.Range(0,9).Select(stall.RopePorts)))undoAll=false;
                    stall.SubmitOption(option);stall.SubmitOption(stall.kind==VillageWorkStall.WorkKind.Timber?option:0);
                    elapsed=stall.Elapsed;mistakes=stall.Mistakes;
                    if(!stall.RestoreCurrentTask()||stall.CanUndo||stall.Elapsed!=elapsed||stall.Mistakes!=mistakes||stall.RiceLoad!=0||stall.TimberTotal!=0||stall.Moves!=0||stall.People!=people||stall.RiceTarget!=target||stall.TimberTarget!=wood||!ports.SequenceEqual(Enumerable.Range(0,9).Select(stall.RopePorts)))restoreAll=false;
                    stall.Close();
                }
                if(!undoAll||!restoreAll)throw new Exception("Undo/reset failed to restore editable work while retaining the task, timer or mistakes");
                food=VillagePreparationQuest.Instance.stalls.First(s=>s.kind==VillageWorkStall.WorkKind.Provisions);
                Move(food.InteractionPosition);if(!food.TryOpen())throw new Exception("Cannot reopen food stall");food.StartAttempt(1);
                for(int n=0;n<food.RequiredIngredient(0)+1;n++)food.SubmitOption(0);
                for(int i=1;i<3;i++)for(int n=0;n<food.RequiredIngredient(i);n++)food.SubmitOption(i);
                int rice=food.IngredientCount(0);food.SubmitOption(3);float time=food.Elapsed;
                penaltiesKept=food.Mistakes==1&&food.IngredientCount(0)==rice&&food.RestoreCurrentTask()&&food.Mistakes==1&&food.Elapsed==time;
                if(!penaltiesKept)throw new Exception("Fault cleared the ration or reset refunded error/time");
                food.SubmitOption(0);phase=3;until=EditorApplication.timeSinceStartup+.25;return;
            }
            if(phase==3)
            {
                Click("Work/Undo");mouseUndo=food.IngredientCount(0)==0;
                food.SubmitOption(0);food.SubmitOption(1);phase=4;until=EditorApplication.timeSinceStartup+.2;return;
            }
            if(phase==4)
            {
                Capture("VillageWork_Undo_Provisions");Click("Work/RestoreTask");mouseRestore=food.IngredientCount(0)==0&&food.IngredientCount(1)==0;
                for(int i=0;i<3;i++)for(int n=0;n<food.RequiredIngredient(i);n++)food.SubmitOption(i);
                food.SubmitOption(3);roundBoundary=food.Progress==1&&!food.CanUndo&&!food.Undo()&&!food.CanRestore;
                if(!mouseUndo||!mouseRestore||!roundBoundary)throw new Exception("UI undo/reset blocked or undo crosses a completed order");
                food.AdvanceTime(food.TimeLimit+1);if(food.Undo()||food.RestoreCurrentTask())throw new Exception("Undo bypasses a timed loss");
                Finish(null);
            }
        }
        catch(Exception error){Finish(error.ToString());}
    }
    static void Click(string path)
    {
        Canvas.ForceUpdateCanvases();var target=GameObject.Find("VillageWorkCanvas/WorkOverlay/Workbench/"+path);
        var rect=target.GetComponent<RectTransform>();var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        if(hits.Count==0||ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)!=target||!target.GetComponent<Button>().interactable)throw new Exception("Pointer blocked on "+path);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    static void Capture(string name)
    {
        var camera=BeachBoatQuestSession.Instance.storyCamera;
        var canvases=Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c=>c.isRootCanvas&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
        var target=new RenderTexture(1280,720,24);var png=new Texture2D(1280,720,TextureFormat.RGB24,false);camera.targetTexture=target;
        foreach(var c in canvases){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=2;}
        Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;png.ReadPixels(new Rect(0,0,1280,720),0,0);png.Apply();Directory.CreateDirectory("Logs");File.WriteAllBytes("Logs/"+name+".png",png.EncodeToPNG());
        foreach(var c in canvases){c.renderMode=RenderMode.ScreenSpaceOverlay;c.worldCamera=null;}
        camera.targetTexture=null;RenderTexture.active=null;Object.DestroyImmediate(target);Object.DestroyImmediate(png);
    }
}
