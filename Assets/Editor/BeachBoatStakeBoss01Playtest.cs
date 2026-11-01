using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using BeachBoatStory;
using Object=UnityEngine.Object;
using Story=BeachBoatStory.BachDangStoryManager;

public static class BeachBoatStakeBoss01Playtest
{
    const string Flag="StakeBoss01QA";
    static int phase,frame;static double began;static string failure;static float elapsed,minGap,startGap;static int startHealth;static long vertices;static float silhouette;
    static readonly List<string> checks=new List<string>();static readonly Dictionary<string,string> hashes=new Dictionary<string,string>();
    static string Hash(string p)=>Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(p)));
    static void Check(bool pass,string text){if(!pass)throw new Exception(text);checks.Add(text);}
    public static void RunInEditor(){phase=0;frame=-1;began=0;failure=null;elapsed=0;checks.Clear();hashes.Clear();BachDangSaveSystem.TestDirectory=Path.GetFullPath(".utmp/StakeBoss01/"+DateTime.Now.ToString("yyyyMMddHHmmssfff"));SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
    static void Log(string text,string stack,LogType kind){if(SessionState.GetBool(Flag,false)&&(kind==LogType.Error||kind==LogType.Exception||text.Contains("Cannot combine mesh")))failure=text+"\n"+stack;}
    static void Finish(string error){SessionState.SetBool(Flag,false);File.WriteAllText("Logs/StakeBoss01Playtest.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=error==null,error,phase,vertices,silhouette,minGap,startGap,elapsed,checks},Newtonsoft.Json.Formatting.Indented));BachDangSaveSystem.TestDirectory=null;EditorApplication.isPlaying=false;}
    static bool[] Mask(Camera c)
    {
        var rt=new RenderTexture(768,512,24);var previous=RenderTexture.active;c.targetTexture=rt;c.Render();RenderTexture.active=rt;var t=new Texture2D(768,512,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,768,512),0,0);t.Apply();var p=t.GetPixels32();var mask=p.Select(x=>x.g>15||x.r<225||x.b<225).ToArray();c.targetTexture=null;RenderTexture.active=previous;Object.DestroyImmediate(t);Object.DestroyImmediate(rt);return mask;
    }
    static void VerifyGeometry(BeachBoatStory.RiverStakeZone zone)
    {
        var chunks=zone.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name=="Static_stake_chunk").ToArray();var originals=zone.fieldStakes.SelectMany(t=>t.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
        Check(chunks.Length>0&&chunks.All(f=>f.sharedMesh.vertexCount>0),"All visual chunks contain actual geometry in ordinary Editor Play Mode");
        vertices=chunks.Sum(f=>(long)f.sharedMesh.vertexCount);Check(vertices<1000000,"The whole field renders under one million vertices instead of 14.8 million: "+vertices);
        var bounds=originals[0].bounds;foreach(var r in originals)bounds.Encapsulate(r.bounds);var combined=chunks[0].GetComponent<Renderer>().bounds;foreach(var f in chunks)combined.Encapsulate(f.GetComponent<Renderer>().bounds);
        Check(Vector3.Distance(bounds.center,combined.center)<2&&Mathf.Abs(bounds.size.x-combined.size.x)<4&&Mathf.Abs(bounds.size.z-combined.size.z)<4&&Mathf.Abs(bounds.max.y-combined.max.y)<1,"Combined field retains the authored world bounds and exposed tip heights: original="+bounds+" combined="+combined);
        var renderers=originals.Concat(chunks.Select(f=>f.GetComponent<MeshRenderer>())).ToArray();var layers=renderers.Select(x=>x.gameObject.layer).ToArray();foreach(var x in renderers)x.gameObject.layer=31;
        var cam=new GameObject("Stake_silhouette_probe").AddComponent<Camera>();cam.enabled=false;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.magenta;cam.cullingMask=1<<31;cam.orthographic=true;cam.orthographicSize=Mathf.Max(bounds.extents.z,bounds.extents.x/1.5f)+15;cam.farClipPlane=5000;cam.transform.position=bounds.center+Vector3.up*2000;cam.transform.LookAt(bounds.center,Vector3.forward);
        try
        {
            foreach(var x in originals)x.enabled=true;foreach(var f in chunks)f.gameObject.SetActive(false);var a=Mask(cam);
            foreach(var x in originals)x.enabled=false;foreach(var f in chunks)f.gameObject.SetActive(true);var b=Mask(cam);
            int union=0,intersection=0;for(int i=0;i<a.Length;i++){if(a[i]||b[i])union++;if(a[i]&&b[i])intersection++;}silhouette=union==0?0:intersection/(float)union;
            Check(union>1000&&silhouette>.80f,"Rendered optimized stakes preserve the original field silhouette: "+silhouette);
        }
        finally{for(int i=0;i<renderers.Length;i++)renderers[i].gameObject.layer=layers[i];Object.DestroyImmediate(cam.gameObject);}
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;EditorApplication.QueuePlayerLoopUpdate();if(frame==Time.frameCount)return;frame=Time.frameCount;
        if(began==0)began=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-began<5)return;
        try
        {
            if(failure!=null)throw new Exception(failure);if(EditorApplication.timeSinceStartup-began>150)throw new Exception("Stake/boss QA timed out");
            var session=BeachBoatQuestSession.Instance;var story=Story.Instance;var zone=BeachBoatStory.RiverStakeZone.Instance;var tide=TideSystem.Instance;var boss=RiverBossEncounter.Instance;
            if(phase==0)
            {
                foreach(var p in Directory.GetFiles(Application.persistentDataPath,"campaign938*"))hashes[p]=Hash(p);BachDangAutoSave.Instance.enabled=false;
                BachDangFrontEnd.Instance.Play();Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
                zone.RestoreCheckpoint(Enumerable.Repeat(true,5).ToArray());story.TransitionTo(Story.StoryState.Act3_BattleInProgress,true);BachDangBattleTimeline.Instance.BeginAmbush();tide.ReleaseTide(QuestRowboat.Instance);tide.HoldTide(BachDangBattleTimeline.Instance,false,true);
                var data=BachDangSaveSystem.Capture("QA low-tide field");BachDangSaveSystem.Apply(data);EnemyFleetDirector.Instance.enabled=false;RiverBoardingCombat.Instance.enabled=false;phase=1;return;
            }
            if(phase==1)
            {
                if(!zone.GetComponent<RiverStakeRendering>().Ready||BeachBoatQuestSession.InputBlocked)return;
                Check(zone.ExposedStakeCount>1000,"Low-tide save restores more than one thousand exposed physical stake tips");VerifyGeometry(zone);
                var center=zone.FieldBounds.center;BeachBoatReview29Playtest.Picture("Low_Tide_Stakes",center+new Vector3(0,110,-400),center+Vector3.up*10);
                var saved=BachDangSaveSystem.Capture("QA repeated field load");for(int i=0;i<3;i++)BachDangSaveSystem.Apply(saved);
                Check(zone.GetComponentsInChildren<MeshFilter>().Where(f=>f.name=="Static_stake_chunk").All(f=>f.sharedMesh.vertexCount>0&&f.GetComponent<Renderer>().enabled),"Repeated save loads retain nonempty visible field chunks");
                story.TransitionTo(Story.StoryState.Act3_CommanderDuel,true);boss.Restore(new BachDangSaveData.Boss{version=1,mask=7,health=100,inArena=true,bossPosition=new Vector3(0,0,75)});boss.enabled=false;
                session.player.transform.position=boss.ArenaCenter+new Vector3(0,1,-85);boss.Boss.position=boss.ArenaCenter+new Vector3(0,1,75);boss.SupportAfterAnimation();
                startGap=QuestRowboat.FlatDistance(session.player.transform.position,boss.Boss.position);minGap=startGap;startHealth=(int)session.player.GetComponent<PlayerVitals>().Health;elapsed=0;phase=2;return;
            }
            if(phase==2)
            {
                if(BeachBoatQuestSession.InputBlocked)return;float dt=Mathf.Min(Time.deltaTime,.05f);elapsed+=dt;var actor=session.player.transform;
                // A retreating archer, followed by continuous lateral movement, must not defeat the AI by merely walking.
                if(elapsed<2)actor.position=boss.ClampDeck(actor.position+Vector3.back*28*dt);
                else if(elapsed<8)actor.position=boss.ClampDeck(actor.position+Vector3.right*(Mathf.Sin(elapsed*1.3f)>=0?28:-28)*dt);
                boss.StepBoss(dt);boss.SupportAfterAnimation();minGap=Mathf.Min(minGap,QuestRowboat.FlatDistance(actor.position,boss.Boss.position));
                if(elapsed<8)return;
                Check(minGap>=8.9f&&minGap<20&&startGap-minGap>120,"Boss actively catches a retreating player and closes to real sword range");
                float damage=startHealth-session.player.GetComponent<PlayerVitals>().Health;Check(damage>=19,"Boss lands real sword damage against continuous ordinary movement: "+damage);
                int count=(int)typeof(RiverBossEncounter).GetField("attackNumber",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(boss);Check(count>=2,"Boss follows pressure with multiple committed attacks: "+count);
                Check(Vector3.Distance(boss.Boss.position,boss.ClampDeck(boss.Boss.position))<.1f,"Mobile boss stays inside the solid duel rails");
                foreach(var pair in hashes)Check(File.Exists(pair.Key)&&Hash(pair.Key)==pair.Value,"User save unchanged: "+Path.GetFileName(pair.Key));Finish(null);
            }
        }
        catch(Exception e){Finish(e.ToString());}
    }
}
