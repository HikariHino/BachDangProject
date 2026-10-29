using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using TMPro;
using BeachBoatStory;
using Object=UnityEngine.Object;

public static class BeachBoatHudLayoutPlaytest
{
    const string Flag="HudLayout30QA";
    static readonly Vector2Int[] sizes={new Vector2Int(640,360),new Vector2Int(800,600),new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(2560,1080)};
    static readonly List<string> checks=new List<string>();
    static readonly BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
    static int index=-1,frame,last;static double began;static float sampleAt;static Vector3[] targetPositions;static string failure;
    static Camera camera;static Canvas[] canvases;static RenderTexture target,priorTarget;
    public static void RunInEditor(){index=-1;last=-1;began=0;failure=null;camera=null;canvases=null;target=null;checks.Clear();BachDangSaveSystem.TestDirectory=Path.GetFullPath(".utmp/HudLayoutQA/"+DateTime.Now.ToString("yyyyMMddHHmmssfff"));SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
    static void Log(string text,string stack,LogType kind){if(SessionState.GetBool(Flag,false)&&(kind==LogType.Error||kind==LogType.Exception))failure=text+"\n"+stack;}
    static void Check(bool pass,string text){if(!pass)throw new Exception(text);checks.Add(text);}
    static void SetSize()
    {
        if(target!=null){camera.targetTexture=null;Object.DestroyImmediate(target);}var size=sizes[index];target=new RenderTexture(size.x,size.y,24);camera.targetTexture=target;
        foreach(var canvas in canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;}
        Canvas.ForceUpdateCanvases();camera.Render();frame=Time.frameCount+8;
    }
    static Rect Bounds(RectTransform r){var p=new Vector3[4];r.GetWorldCorners(p);var screen=p.Select(v=>RectTransformUtility.WorldToScreenPoint(camera,v)).ToArray();return Rect.MinMaxRect(screen.Min(v=>v.x),screen.Min(v=>v.y),screen.Max(v=>v.x),screen.Max(v=>v.y));}
    static void Capture()
    {
        camera.Render();RenderTexture.active=target;var pixels=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();File.WriteAllBytes("Logs/HudLayout30.png",pixels.EncodeToPNG());RenderTexture.active=null;Object.DestroyImmediate(pixels);
    }
    static void RestoreView()
    {
        if(camera!=null)camera.targetTexture=priorTarget;
        if(canvases!=null)foreach(var canvas in canvases){canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.worldCamera=null;}
        if(target!=null)Object.DestroyImmediate(target);target=null;Canvas.ForceUpdateCanvases();
    }
    static void Finish(string error)
    {
        SessionState.SetBool(Flag,false);ArcheryTraining.Active?.Close();RestoreView();
        File.WriteAllText("Logs/HudLayout30Playtest.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=error==null,error,checks},Newtonsoft.Json.Formatting.Indented));BachDangSaveSystem.TestDirectory=null;EditorApplication.isPlaying=false;
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;EditorApplication.QueuePlayerLoopUpdate();if(last==Time.frameCount)return;last=Time.frameCount;if(began==0)began=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-began<4)return;
        try
        {
            if(failure!=null)throw new Exception(failure);if(EditorApplication.timeSinceStartup-began>100)throw new Exception("HUD layout test timeout");
            if(index<0)
            {
                BachDangAutoSave.Instance.enabled=false;BachDangFrontEnd.Instance.Play();Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
                camera=BeachBoatQuestSession.Instance.storyCamera;priorTarget=camera.targetTexture;canvases=Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(c=>c.name=="BeachStoryHUD"||c.name=="HealingItemHUD").ToArray();
                index=0;SetSize();return;
            }
            if(index<sizes.Length)
            {
                if(Time.frameCount<frame||BeachBoatQuestSession.InputBlocked)return;Canvas.ForceUpdateCanvases();
                var root=Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).First(c=>c.name=="BeachStoryHUD").transform;
                RectTransform R(string name)=>(RectTransform)root.Find(name);
                var med=Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(r=>r.name=="Healing_item_slot");var m=Bounds(med);var v=Bounds(R("Vitals"));var help=Bounds(R("ControlsBar"));var prompt=Bounds(R("Interaction"));var size=sizes[index];
                Check(camera.pixelWidth==size.x&&camera.pixelHeight==size.y,"Actual render target resolution is "+size);
                Check(!m.Overlaps(help)&&!m.Overlaps(v)&&!v.Overlaps(help),"Medicine, vitals and key help occupy separate zones at "+size);
                Check(prompt.yMin>Mathf.Max(m.yMax,help.yMax)&&!prompt.Overlaps(v),"Interaction prompt stays above the bottom HUD at "+size);
                Check(new[]{m,v,help,prompt}.All(r=>r.xMin>=0&&r.xMax<=size.x+.1f&&r.yMin>=0&&r.yMax<=size.y+.1f),"Every HUD zone remains inside the visible screen at "+size);
                foreach(var text in R("ControlsBar").GetComponentsInChildren<TMP_Text>()){text.ForceMeshUpdate();Check(!text.isTextOverflowing,"The key help fits without clipping at "+size);}
                foreach(string label in new[]{"Vitals/HealthLabel","Vitals/StaminaLabel"}){var text=root.Find(label).GetComponent<TMP_Text>();text.ForceMeshUpdate();Check(!text.isTextOverflowing,label+" stays readable at "+size);}
                foreach(var text in med.GetComponentsInChildren<TMP_Text>()){text.ForceMeshUpdate();Check(!text.isTextOverflowing,"Medicine label and H hotkey fit at "+size+": "+text.name);}
                if(index==2)Capture();index++;if(index<sizes.Length){SetSize();return;}
                RestoreView();var range=ArcheryTraining.Instance;BeachBoatPhysicalVillagePlaytest.Place(range.Approach);Check(range.Begin(),"The smaller-target archery round opens normally");targetPositions=range.Targets.Select(t=>t.transform.position).ToArray();sampleAt=Time.time+1.5f;return;
            }
            if(Time.time<sampleAt)return;
            var archery=ArcheryTraining.Instance;
            Check(Vector3.Distance(targetPositions[0],archery.Targets[0].transform.position)<.001f,"The introductory target remains stationary");
            for(int i=1;i<3;i++){float d=Vector3.Distance(targetPositions[i],archery.Targets[i].transform.position);Check(d>.05f&&d<=archery.Targets[i].SwayAmplitude*2+.001f,"Target "+(i+1)+" sways gently within its physical range");}
            Check(archery.Targets.All(t=>Mathf.Abs(t.surface.transform.localScale.x-8.2f)<.001f),"Target diameters are reduced by eighteen percent");
            archery.Close();var campaign=CampaignProgress.Instance;var legacy=campaign.Capture();legacy.stakeStarted=true;legacy.stakeFinished=false;legacy.stakeTargetSeconds=90;campaign.Restore(legacy,(int)BeachBoatStory.BachDangStoryManager.StoryState.Act2_PlantStakesAtRiver);
            Check(campaign.StakeTargetSeconds>=180,"An unfinished old ninety-second stake record migrates to the five-location route target");
            legacy.stakeFinished=true;legacy.stakeTargetSeconds=90;campaign.Restore(legacy,(int)BeachBoatStory.BachDangStoryManager.StoryState.Act2_CompleteReturnToCamp);Check(campaign.StakeTargetSeconds==90,"A previously finished stake record retains its original target");Finish(null);
        }
        catch(Exception e){Finish(e.ToString());}
    }
}
