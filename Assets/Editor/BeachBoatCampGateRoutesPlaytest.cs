using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BeachBoatStory;
using Newtonsoft.Json;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
using Gate=BeachBoatStory.CampGateTransition;
using Story=BeachBoatStory.BachDangStoryManager;
using Intro=BeachBoatStory.OpeningEvent;

public static class BeachBoatCampGateRoutesPlaytest
{
    const string Flag="SeparateCampRoutesQA";
    static double started,until;
    static int phase;
    static readonly List<string> checks=new List<string>();
    public static void RunAndExit(){EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);checks.Add(text);}
    static void Move(Vector3 p,Quaternion rotation)
    {var player=BeachBoatQuestSession.Instance.player;player.transform.SetPositionAndRotation(p,rotation);var body=player.GetComponent<Rigidbody>();body.position=p;body.rotation=rotation;body.linearVelocity=Vector3.zero;Physics.SyncTransforms();}
    static void Approach(Gate gate)
    {Move(gate.transform.position+gate.transform.forward*Mathf.Min(18,gate.interactDistance*.8f)+Vector3.up*2.4f,gate.transform.rotation*Quaternion.Euler(0,180,0));}
    static void Arrived(Gate gate)
    {
        var player=BeachBoatQuestSession.Instance.player;
        Check(QuestRowboat.FlatDistance(player.transform.position,gate.destinationTarget.position)<30,"Arrival matches "+gate.destinationTarget.name);
        Check(PlayerArrival.IsClear(player,player.transform.position,player.transform.rotation)&&PlayerArrival.HasExit(player,player.transform.position,player.transform.rotation),"Can walk away at "+gate.destinationTarget.name);
    }
    static void CaptureGates(Gate village,Gate river)
    {
        var camera=BeachBoatQuestSession.Instance.storyCamera;var position=camera.transform.position;var rotation=camera.transform.rotation;float fov=camera.fieldOfView;
        var center=(village.transform.position+river.transform.position)*.5f+Vector3.up*28;
        camera.transform.position=center+Vector3.right*170+Vector3.up*35;camera.transform.LookAt(center);camera.fieldOfView=60;
        var rt=new RenderTexture(1280,720,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
        var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();
        File.WriteAllBytes("Logs/Camp-Two-Destination-Gates.png",pixels.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=null;
        camera.transform.SetPositionAndRotation(position,rotation);camera.fieldOfView=fov;Object.DestroyImmediate(rt);Object.DestroyImmediate(pixels);
    }
    static void Finish(string failure)
    {
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CampGateRoutesPlaytest.json",JsonConvert.SerializeObject(new{success=failure==null,failure,phase,checks},Formatting.Indented));
        SessionState.SetBool(Flag,false);BeachBoatQAExit.AfterPlayMode(failure==null?0:1);
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;
        if(started==0)started=EditorApplication.timeSinceStartup;
        if(EditorApplication.timeSinceStartup-started<4||EditorApplication.timeSinceStartup<until)return;
        if(EditorApplication.timeSinceStartup-started>80){Finish("Timeout");return;}
        try
        {
            var session=BeachBoatQuestSession.Instance;var story=Story.Instance;var gates=session.GetComponentsInChildren<Gate>(true);
            var village=gates.Single(g=>g.gateType==Gate.GateType.CampGate);var river=gates.Single(g=>g.gateType==Gate.GateType.CampRiverGate);
            var fromVillage=gates.Single(g=>g.gateType==Gate.GateType.VillageGate);var fromRiver=gates.Single(g=>g.gateType==Gate.GateType.RiverGate);
            var guide=session.GetComponentInChildren<StoryRouteGuide>(true);
            if(phase==0)
            {
                var intro=Object.FindFirstObjectByType<Intro>();Check(!intro.HandleOpeningKey(KeyCode.P),"P cannot bypass the title menu");
                BachDangFrontEnd.Instance.Play();phase=1;until=EditorApplication.timeSinceStartup+.7;return;
            }
            if(phase==1)
            {
                var intro=Object.FindFirstObjectByType<Intro>();
                Check(intro.storyText.text.Contains("P: bỏ qua")&&!intro.storyText.text.Contains("Space"),"Intro displays P skip and Enter continue");
                Check(!intro.HandleOpeningKey(KeyCode.Space)&&!intro.IsOpeningEnded,"Space is no longer an intro control");
                Check(intro.HandleOpeningKey(KeyCode.P)&&intro.IsOpeningEnded&&!Intro.IsOpeningActive,"A single P skips all story lines");
                Check(QuestRowboat.FlatDistance(village.transform.position,river.transform.position)>village.interactDistance+river.interactDistance+30,"Camp gate interaction circles do not overlap");
                Check(village.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Contains("NHIỆM VỤ 2"))&&river.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Contains("BẾN SÔNG")),"Both physical gates have distinct destination signs");
                foreach(var gate in new[]{village,river})
                {
                    Approach(gate);Check(PlayerArrival.IsClear(session.player,session.player.transform.position,session.player.transform.rotation)&&PlayerArrival.HasExit(session.player,session.player.transform.position,session.player.transform.rotation),"Clear approach to "+gate.name);
                    var passage=gate.transform.position+Vector3.up*2.4f;
                    Check(PlayerArrival.IsClear(session.player,passage,gate.transform.rotation)&&PlayerArrival.HasExit(session.player,passage,gate.transform.rotation),"Can walk through the gate opening at "+gate.name);
                    Check(session.IsPreferredGate(gate)&&gates.Count(g=>g.IsInRange(session.player.transform))==1,"Exactly one gate receives E at "+gate.name);
                }
                CaptureGates(village,river);
                Move(session.playerSpawn.position,session.playerSpawn.rotation);story.TransitionTo(Story.StoryState.Act1_GoToCampGate,true);
                Check(guide.ResolveTarget(out _)==village.transform,"Quest 2 departure guide points to the village gate");
                story.TransitionTo(Story.StoryState.Act2_SupplyPreparation,true);Check(guide.ResolveTarget(out _)==village.transform,"Supply-work guide routes through the village gate from camp");
                story.TransitionTo(Story.StoryState.Act2_PlantStakesAtRiver,true);Check(guide.ResolveTarget(out _)==river.transform,"Stake-work guide routes through the river gate from camp");
                story.TransitionTo(Story.StoryState.Act1_GoToCampGate,true);Approach(village);
                Check(!river.TryInteract()&&village.TryInteract(),"E at the village gate cannot select the river gate");phase=2;return;
            }
            if(BeachBoatStory.ScreenFader.Instance.IsTransitioning)return;
            if(phase==2)
            {
                Arrived(village);Check(story.currentState==Story.StoryState.Act2_CollectWoodInVillage,"Village gate starts quest 2 normally");
                var guided=guide.ResolveTarget(out var guidedTitle);
                Check(guided?.GetComponent<BeachBoatStory.WoodCollectible>()!=null,"Quest guide points to village wood after arriving: "+guided?.name+" / "+guidedTitle);
                var wood=session.woodItems.OrderBy(w=>QuestRowboat.FlatDistance(w.transform.position,session.player.transform.position)).First();
                Check(QuestRowboat.FlatDistance(wood.transform.position,session.player.transform.position)<180,"Quest-2 wood is near the village arrival");
                Move(wood.transform.position+Vector3.up*2,Quaternion.identity);Check(wood.TryCollect()&&story.collectedWood==1,"Can collect quest-2 wood after using the village gate");
                Approach(fromVillage);Check(fromVillage.TryInteract(),"Village return gate works before quest completion");phase=3;return;
            }
            if(phase==3)
            {
                Arrived(fromVillage);Check(story.currentState==Story.StoryState.Act2_CollectWoodInVillage,"Free return keeps unfinished quest-2 progress");
                Approach(river);Check(!village.TryInteract()&&river.TryInteract(),"E at the river gate cannot select the village gate");phase=4;return;
            }
            if(phase==4)
            {
                Arrived(river);Check(QuestRowboat.FlatDistance(session.player.transform.position,QuestRowboat.Instance.boardingPoint.position)<260,"River gate reaches the stake-field boat harbor");
                Check(QuestRowboat.FlatDistance(session.player.transform.position,village.destinationTarget.position)>1000,"River and quest-2 village arrivals are different regions");
                Approach(fromRiver);Check(fromRiver.TryInteract(),"River return gate works during quest 2");phase=5;return;
            }
            if(phase==5)
            {
                Arrived(fromRiver);Check(story.currentState==Story.StoryState.Act2_CollectWoodInVillage&&story.collectedWood==1,"Round trips preserve quest and collected wood");
                var old=river.transform.position;river.transform.position=village.transform.position;Approach(village);
                Check(session.IsPreferredGate(village)&&!session.IsPreferredGate(river)&&!river.TryInteract(),"Even overlapping gates cannot steal the village interaction");river.transform.position=old;
                Finish(null);
            }
        }
        catch(Exception error){Finish(error.ToString());}
    }
}
