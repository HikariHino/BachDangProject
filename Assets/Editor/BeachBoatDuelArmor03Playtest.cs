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

public static class BeachBoatDuelArmor03Playtest
{
    const string Flag="DuelArmor03QA";static int phase,frame;static double began;static string failure;static float previousVolume;static bool captured;
    static readonly List<string> checks=new List<string>();static readonly Dictionary<string,string> hashes=new Dictionary<string,string>();static BachDangSaveData baseline;
    const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    static string Hash(string p)=>Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(p)));
    static void Check(bool pass,string text){if(!pass)throw new Exception(text);checks.Add(text);}
    public static void RunInEditor(){phase=0;frame=-1;began=0;failure=null;captured=false;checks.Clear();hashes.Clear();BachDangSaveSystem.TestDirectory=Path.GetFullPath(".utmp/DuelArmor03/"+DateTime.Now.ToString("yyyyMMddHHmmssfff"));SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
    static void Log(string m,string stack,LogType kind){if(SessionState.GetBool(Flag,false)&&(kind==LogType.Error||kind==LogType.Exception))failure=m+"\n"+stack;}
    static void Finish(string error){if(captured)BachDangPreferences.SetVolume(previousVolume);SessionState.SetBool(Flag,false);File.WriteAllText("Logs/DuelArmor03Playtest.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=error==null,error,phase,checks},Newtonsoft.Json.Formatting.Indented));BachDangSaveSystem.TestDirectory=null;EditorApplication.isPlaying=false;}
    static RiverBossEncounter Duel=>RiverBossEncounter.Instance;
    static Transform Hero=>BeachBoatQuestSession.Instance.player.transform;
    static void Poses(float dt)
    {
        foreach(var root in new[]{Hero,Duel.Boss}){var pose=root.GetComponent<RiverFighterPose>();pose.Animator.Update(dt);typeof(RiverFighterPose).GetMethod("LateUpdate",Private).Invoke(pose,null);}Duel.SupportAfterAnimation();
    }
    static void Reset()
    {
        Hero.GetComponent<PlayerVitals>().RestoreSave(baseline);Duel.Restore(baseline.boss);Hero.SetPositionAndRotation(baseline.playerPosition,baseline.playerRotation);PlayerShield.Instance.Restore(false);Duel.enabled=false;RiverPlayerCombat.Instance.enabled=false;BeachBoatQuestSession.Instance.storyCamera.GetComponent<BeachBoatStory.Camera_Script>().enabled=false;BeachBoatQuestSession.Instance.storyCamera.transform.rotation=Quaternion.identity;
        typeof(RiverBossEncounter).GetField("attackAt",Private).SetValue(Duel,0f);typeof(RiverBossEncounter).GetField("recoverAt",Private).SetValue(Duel,0f);RiverPlayerCombat.Instance.SetAim(false);Poses(0);
    }
    static (int tested,int missed) ProbeSurface(Animator rig,DuelBladeContact contact,List<object> probes)
    {
        int tested=0,missed=0;
        foreach(var skin in rig.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var mesh=skin.sharedMesh;if(!mesh.isReadable)continue;var vertices=mesh.vertices;var weights=mesh.boneWeights;var binds=mesh.bindposes;var bones=skin.bones;var matrices=bones.Select((b,i)=>b.localToWorldMatrix*binds[i]).ToArray();
                    int stride=Mathf.Max(1,vertices.Length/120);
                    for(int i=0;i<vertices.Length;i+=stride)
                    {
                        var w=weights[i];var p=matrices[w.boneIndex0].MultiplyPoint3x4(vertices[i])*w.weight0;if(w.weight1>0)p+=matrices[w.boneIndex1].MultiplyPoint3x4(vertices[i])*w.weight1;if(w.weight2>0)p+=matrices[w.boneIndex2].MultiplyPoint3x4(vertices[i])*w.weight2;if(w.weight3>0)p+=matrices[w.boneIndex3].MultiplyPoint3x4(vertices[i])*w.weight3;
                        bool hit=contact.Touches(p,p,p,p,.15f,out _);tested++;if(!hit)missed++;probes.Add(new{skin.name,vertex=i,point=p.ToString(),hit});
                    }
                }
        return (tested,missed);
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;EditorApplication.QueuePlayerLoopUpdate();if(frame==Time.frameCount)return;frame=Time.frameCount;if(began==0)began=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-began<5)return;
        try
        {
            if(failure!=null)throw new Exception(failure);if(EditorApplication.timeSinceStartup-began>100)throw new Exception("Boss combat QA timed out");
            var session=BeachBoatQuestSession.Instance;var combat=RiverPlayerCombat.Instance;
            if(phase==0)
            {
                foreach(var p in Directory.GetFiles(Application.persistentDataPath,"campaign938*"))hashes[p]=Hash(p);BachDangAutoSave.Instance.enabled=false;previousVolume=BachDangPreferences.Volume;captured=true;BachDangPreferences.SetVolume(0);
                BachDangFrontEnd.Instance.Play();Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();Story.Instance.TransitionTo(Story.StoryState.Act3_CommanderDuel,true);BachDangBattleTimeline.Instance.RestoreSave(new BachDangSaveData.Tide{moment=(int)BachDangBattleTimeline.BattleMoment.Victory,hour=11.5f});
                Duel.Restore(new BachDangSaveData.Boss{version=1,mask=7,health=100,inArena=true,bossPosition=new Vector3(0,0,0)});Hero.position=Duel.ArenaCenter+Vector3.back*30;Duel.SupportAfterAnimation();baseline=BachDangSaveSystem.Capture("QA natural combat");phase=1;return;
            }
            if(phase==1)
            {
                Reset();var rig=Duel.Boss.GetComponentInChildren<Animator>();var contact=new DuelBladeContact(rig);int tested=0,missed=0;var probes=new List<object>();
                (tested,missed)=ProbeSurface(rig,contact,probes);
                File.WriteAllText("Logs/DuelArmor03Probes.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{tested,missed,probes},Newtonsoft.Json.Formatting.Indented));
                Check(tested>50&&missed<tested*.05f,"Visible skinned armor surface is covered by physical blade contact; missed="+missed+" / "+tested);
                var poseCoverage=new List<object>();
                foreach(string state in new[]{"sword_idle","Walking (1)","Running","Block","Attack","Attack2"})foreach(float at in new[]{.2f,.7f})
                {rig.Play(state,0,at);Poses(0);var result=ProbeSurface(rig,contact,new List<object>());poseCoverage.Add(new{state,at,result.tested,result.missed});Check(result.tested>50&&result.missed<result.tested*.05f,"Actual animated armor coverage: "+state+" at "+at+" missed "+result.missed+" / "+result.tested);}
                rig.Play("sword_idle",0,0);Poses(0);contact.CapturePose();var inside=rig.GetBoneTransform(HumanBodyBones.Chest).position;
                for(int i=0;i<20;i++)contact.Touches(inside,inside+Vector3.up,inside,inside+Vector3.up,.15f,out _);
                long allocated=System.GC.GetAllocatedBytesForCurrentThread();var watch=System.Diagnostics.Stopwatch.StartNew();for(int i=0;i<2000;i++)contact.Touches(inside,inside+Vector3.up,inside,inside+Vector3.up,.15f,out _);watch.Stop();long bytes=System.GC.GetAllocatedBytesForCurrentThread()-allocated;double milliseconds=watch.Elapsed.TotalMilliseconds/2000;
                Check(bytes<1024&&milliseconds<1,"Cached armor contact stays allocation-free and below 1 ms per check: bytes="+bytes+" ms="+milliseconds);
                File.WriteAllText("Logs/DuelArmor03PoseCoverage.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{poseCoverage,milliseconds,bytes},Newtonsoft.Json.Formatting.Indented));
                var root=Duel.Boss;var saved=root.position;var crossing=rig.GetBoneTransform(HumanBodyBones.Chest).position;
                root.position=saved-Vector3.right*18;contact.CapturePose();root.position=saved+Vector3.right*18;
                Check(!contact.Touches(crossing-Vector3.up,crossing+Vector3.up,crossing-Vector3.up,crossing+Vector3.up,.15f,out _),"A blade clear of the target's current armor does not hit by root distance");
                Check(contact.Touches(crossing-Vector3.up,crossing+Vector3.up,crossing-Vector3.up,crossing+Vector3.up,.15f,out _,true),"A moving target crossing the real blade between frames is detected in relative motion");root.position=saved;contact.CapturePose();
                // Read the actual imported samples: these three action groups are louder, rice is untouched.
                foreach(var name in new[]{"Saw","Rope","Cloth","Knot","Crease"}){var clip=Resources.Load<AudioClip>("BachDang938/WorkAudio/Trade_"+name);clip.LoadAudioData();var samples=new float[clip.samples];Check(clip.GetData(samples,0)&&samples.Max(v=>Mathf.Abs(v))>.25f&&samples.All(v=>Mathf.Abs(v)<.8f),"Louder unclipped recorded action: "+name);}
                foreach(float dt in new[]{.02f,.08f})
                {
                    Reset();Hero.SetPositionAndRotation(Duel.ArenaCenter+Vector3.back*9,Quaternion.identity);Duel.Boss.SetPositionAndRotation(Duel.ArenaCenter,Quaternion.Euler(0,180,0));Poses(0);Duel.ResetBladeContactHistory();int hp=Duel.Health;Check(Duel.TryPlayerStrike(),"Start an actual moving player sword swing at step "+dt);
                    for(float elapsed=0;elapsed<1.1f;elapsed+=dt){Duel.StepPlayerInput(dt,Vector2.up,false);Poses(dt);}Check(Duel.Health==hp-12,"Moving forward through the real full animation damages the boss once at step "+dt+"; damage="+(hp-Duel.Health));
                }
                Reset();Hero.SetPositionAndRotation(Duel.ArenaCenter+Vector3.back*9,Quaternion.identity);Duel.Boss.SetPositionAndRotation(Duel.ArenaCenter,Quaternion.Euler(0,180,0));Poses(0);Duel.ResetBladeContactHistory();int before=Duel.Health;Check(Duel.TryPlayerStrike(),"Start a sword strike for the frame spanning both cut-window edges");var heroPose=Hero.GetComponent<RiverFighterPose>();heroPose.Animator.Play("Attack",0,.5f);Poses(0);Check(Duel.Health==before,"The sampled lift before the downstroke cannot damage");heroPose.Animator.Play("Attack",0,.85f);Poses(0);Check(Duel.Health==before-12,"A real blade sweep spanning the start and end of the downstroke still damages once");
                foreach(var pair in hashes)Check(File.Exists(pair.Key)&&Hash(pair.Key)==pair.Value,"User save unchanged: "+Path.GetFileName(pair.Key));Finish(null);
            }
        }
        catch(Exception e){Finish(e.ToString());}
    }
}
