using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BeachBoatStory;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
using Story=BeachBoatStory.BachDangStoryManager;

public static class BeachBoatWholeMapPlaytest
{
    const string Flag="WholeMapPlaytest";
    const string BoatsOnly="WholeMapBoatChecksOnly";
    static double started,phaseStarted;
    static int phase;
    static Transform root;
    static Transform[] districts;
    static Camera camera;
    static AmbientPopulationBudget budget;
    static readonly Dictionary<NPCWander,Vector3> initial=new Dictionary<NPCWander,Vector3>();
    static readonly List<object> results=new List<object>();
    static readonly List<object> boatCases=new List<object>();
    static object staticAudit;
    public static void RunAndExit(){EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    public static void BoatsAndExit(){SessionState.SetBool(BoatsOnly,true);RunAndExit();}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
    static void Capture(string name,Vector3 focus,Vector3 offset)
    {
        camera.transform.position=focus+offset;camera.transform.LookAt(focus);camera.ResetProjectionMatrix();camera.ResetWorldToCameraMatrix();
        foreach(var zone in Object.FindAnyObjectByType<MapZoneStreamer>().zones)
            if(zone.content!=null&&QuestRowboat.FlatDistance(camera.transform.position,zone.center)<zone.visibleDistance)zone.content.SetActive(true);
        budget.Refresh();
        var rt=new RenderTexture(1280,800,24);var image=new Texture2D(1280,800,TextureFormat.RGB24,false);camera.targetTexture=rt;
        camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();File.WriteAllBytes("Logs/"+name+".png",image.EncodeToPNG());
        RenderTexture.active=null;camera.targetTexture=null;Object.DestroyImmediate(rt);Object.DestroyImmediate(image);
    }
    static void Audit()
    {
        var terrain=Terrain.activeTerrain;var houses=root.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Regional_House_")).ToArray();
        var overlaps=new List<string>();
        for(int i=0;i<houses.Length;i++)for(int j=i+1;j<houses.Length;j++)
        {
            var a=BeachBoatWorldAudit.BoundsOf(houses[i]);var b=BeachBoatWorldAudit.BoundsOf(houses[j]);a.Expand(-2);b.Expand(-2);
            if(a.Intersects(b))overlaps.Add(houses[i].name+" / "+houses[j].name);
        }
        var invalid=new List<string>();
        foreach(var house in houses)
        {
            var b=BeachBoatWorldAudit.BoundsOf(house);float ground=terrain.SampleHeight(b.center)+terrain.transform.position.y;
            if(b.size.y<35||Mathf.Abs(b.min.y-ground)>3||ground<TideSystem.Instance.highTideY+4)invalid.Add(house.name);
        }
        var falls=root.Cast<Transform>().Where(t=>t.name.StartsWith("Thac_")).ToArray();
        if(falls.Length!=4||falls.Any(t=>t.GetComponent<AudioSource>()?.spatialBlend!=1||t.GetComponentInChildren<ParticleSystem>()==null))throw new Exception("Waterfall or localized sound is missing");
        if(overlaps.Count>0||invalid.Count>0)throw new Exception("Invalid houses: "+string.Join(",",invalid)+"; overlaps: "+string.Join(",",overlaps));
        staticAudit=new{regions=districts.Length,houses=houses.Length,waterfalls=falls.Length,people=budget.citizens.Length,animals=Object.FindObjectsByType<VillageChicken>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length,houseOverlaps=overlaps,invalidHouses=invalid};
    }
    static void Boats()
    {
        var boat=QuestRowboat.Instance;var session=BeachBoatQuestSession.Instance;var story=Story.Instance;
        var allies=Object.FindObjectsByType<AmbientRiverBoat>(FindObjectsInactive.Include,FindObjectsSortMode.None);foreach(var ally in allies)ally.enabled=false;
        story.collectedWood=story.totalWoodRequired;
        BeachBoatStory.RiverStakeZone.Instance.RestoreCheckpoint(Enumerable.Repeat(true,BeachBoatStory.RiverStakeZone.Instance.ClusterCount).ToArray());
        story.TransitionTo(Story.StoryState.Act3_WaitForLowTide,true);TideSystem.Instance.SetTide(true,true);
        boat.PrepareForBattle();session.player.transform.position=boat.boardingPoint.position;
        var body=session.player.GetComponent<Rigidbody>();body.position=boat.boardingPoint.position;body.linearVelocity=Vector3.zero;Physics.SyncTransforms();
        if(!boat.TryBoard())throw new Exception("Cannot board quest3 rowboat");
        BeachBoatStory.StoryDialogueUI.Instance?.Close();RiverPlayerCombat.Instance?.SetAim(false);
        var allied=allies.First(a=>a.gameObject.activeInHierarchy);allied.enabled=true;
        var start=new Vector3(-3200,TideSystem.CurrentWaterHeight,1880);boat.transform.SetPositionAndRotation(start,Quaternion.Euler(0,90,0));
        foreach(var other in allies.Where(a=>a!=allied))other.transform.position=new Vector3(-1000,100,-1000);
        foreach(var enemy in Object.FindObjectsByType<EnemyShip>(FindObjectsInactive.Include,FindObjectsSortMode.None))enemy.gameObject.SetActive(false);
        // Forced overlap reproduces the screenshot, with the player pointing away.
        allied.transform.position=start+Vector3.left*30;Physics.SyncTransforms();float hp=session.player.GetComponent<PlayerVitals>().Health;
        var before=boat.transform.position;for(int i=0;i<70;i++)boat.Step(1,0,.05f);float escape=QuestRowboat.FlatDistance(before,boat.transform.position);
        if(escape<90||session.player.GetComponent<PlayerVitals>().Health<hp)throw new Exception("Friendly overlap traps or damages escaping player: "+escape+", aboard="+boat.IsAboard+", blocked="+BeachBoatQuestSession.InteractionBlocked+", aiming="+RiverPlayerCombat.Instance.IsAiming+", canEscape="+boat.CanOccupy(before+Vector3.right,boat.transform.rotation)+", depth="+(TideSystem.CurrentWaterHeight-Terrain.activeTerrain.SampleHeight(before)));
        boatCases.Add(new{test="escape existing overlap",travel=escape,health=session.player.GetComponent<PlayerVitals>().Health});
        // A moving patrol yields before reaching the stationary rowboat.
        boat.transform.SetPositionAndRotation(start,Quaternion.Euler(0,90,0));allied.transform.SetPositionAndRotation(start+Vector3.right*155,Quaternion.Euler(0,-90-allied.visualForwardYaw,0));Physics.SyncTransforms();
        float minimum=9999;
        for(int i=0;i<220;i++){allied.Step(.05f);minimum=Mathf.Min(minimum,QuestRowboat.FlatDistance(boat.transform.position,allied.transform.position));}
        if(minimum<90)throw new Exception("Patrol ran into stationary player: "+minimum);
        boatCases.Add(new{test="patrol yields before contact",minimumSeparation=minimum,finalSeparation=QuestRowboat.FlatDistance(start,allied.transform.position)});
        // New impacts still block movement; escaping never grants general collision immunity.
        boat.transform.SetPositionAndRotation(start,Quaternion.Euler(0,90,0));allied.transform.position=start+Vector3.right*110;Physics.SyncTransforms();before=boat.transform.position;
        for(int i=0;i<100;i++)boat.Step(1,0,.05f);
        float stopped=QuestRowboat.FlatDistance(before,boat.transform.position);
        if(stopped>40||session.player.GetComponent<PlayerVitals>().Health>=hp)throw new Exception("New ship collision no longer blocks/damages the boat");
        boatCases.Add(new{test="new collision retains impact",travel=stopped,health=session.player.GetComponent<PlayerVitals>().Health});
    }
    static void Finish(string failure)
    {
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/WholeMapPlaytest.json",JsonConvert.SerializeObject(new{success=failure==null,failure,staticAudit,results,boatCases},Formatting.Indented));
        SessionState.SetBool(Flag,false);BeachBoatQAExit.AfterPlayMode(failure==null?0:1);
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;
        if(started==0)started=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-started<3)return;
        try
        {
            foreach(var intro in Object.FindObjectsByType<BeachBoatStory.OpeningEvent>(FindObjectsSortMode.None))intro.SkipOpening();BeachBoatStory.StoryDialogueUI.Instance?.Close();
            if(root==null)
            {
                root=GameObject.Find(BeachBoatWholeMapExpansion.RootName).transform;districts=root.Cast<Transform>().Where(t=>!t.name.StartsWith("Thac_")).ToArray();budget=Object.FindAnyObjectByType<AmbientPopulationBudget>();
                Audit();var session=BeachBoatQuestSession.Instance;camera=new GameObject("WholeMapQA_Camera").AddComponent<Camera>();camera.CopyFrom(session.storyCamera);session.storyCamera.enabled=false;session.storyCamera.tag="Untagged";camera.tag="MainCamera";camera.enabled=true;camera.farClipPlane=8000;camera.fieldOfView=65;
                foreach(var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))canvas.enabled=false;
                var sun=new GameObject("WholeMapQA_Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.2f;sun.transform.rotation=Quaternion.Euler(50,120,0);
            }
            int[] visits={0,3,8,15,22,30,38,45};
            if(SessionState.GetBool(BoatsOnly,false)){phase=visits.Length;SessionState.SetBool(BoatsOnly,false);}
            if(phase<visits.Length)
            {
                var district=districts[Mathf.Min(districts.Length-1,visits[phase])];district.gameObject.SetActive(true);
                if(phaseStarted==0)
                {
                    initial.Clear();foreach(var npc in district.GetComponentsInChildren<NPCWander>())initial[npc]=npc.transform.position;
                    camera.transform.position=district.position+new Vector3(-180,100,-240);camera.transform.LookAt(district.position+Vector3.up*15);budget.Refresh();phaseStarted=EditorApplication.timeSinceStartup;return;
                }
                if(EditorApplication.timeSinceStartup-phaseStarted<12)return;
                int moved=initial.Count(a=>QuestRowboat.FlatDistance(a.Key.transform.position,a.Value)>3);
                if(moved<3||budget.ActiveCount>44)throw new Exception("Remote settlement life failed: "+district.name+", moved "+moved);
                results.Add(new{district=district.name,people=initial.Count,moved,animated=budget.ActiveCount});
                Capture("WholeMap_"+district.name,district.position+Vector3.up*15,new Vector3(-200,190,-300));phase++;phaseStarted=0;return;
            }
            if(phase==visits.Length)
            {
                Capture("WholeMap_UpdatedVillage",districts[0].position+Vector3.up*15,new Vector3(-200,190,-300));
                var fall=root.Cast<Transform>().First(t=>t.name.StartsWith("Thac_"));fall.gameObject.SetActive(true);var cascade=fall.Find("Flowing_Cascade");var bounds=cascade.GetComponent<Renderer>().bounds;
                Capture("WholeMap_Waterfall",bounds.center,new Vector3(120,90,-190));
                var gate=Object.FindObjectsByType<BeachBoatStory.CampGateTransition>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(g=>g.gateType==BeachBoatStory.CampGateTransition.GateType.RiverGate);
                Capture("WholeMap_ShoreGate",gate.transform.position+Vector3.up*15,new Vector3(90,38,-110));
                var stall=Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(t=>t.name=="Open_Market_Stall");stall.gameObject.SetActive(true);var ancestor=stall.parent;while(ancestor!=null){ancestor.gameObject.SetActive(true);ancestor=ancestor.parent;}
                Capture("WholeMap_FixedStall",stall.position+Vector3.up*12,new Vector3(45,20,-48));
                Boats();Finish(null);phase++;
            }
        }
        catch(Exception error){Finish(error.ToString());}
    }
}
