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

public static class BeachBoatBossCombat02Playtest
{
    const string Flag="BossCombat02QA";static int phase,frame;static double began;static string failure;static float previousVolume;static bool captured;
    static readonly List<string> checks=new List<string>();static readonly Dictionary<string,string> hashes=new Dictionary<string,string>();static BachDangSaveData baseline;
    const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    static string Hash(string p)=>Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(p)));
    static void Check(bool pass,string text){if(!pass)throw new Exception(text);checks.Add(text);}
    public static void RunInEditor(){phase=0;frame=-1;began=0;failure=null;captured=false;checks.Clear();hashes.Clear();BachDangSaveSystem.TestDirectory=Path.GetFullPath(".utmp/BossCombat02/"+DateTime.Now.ToString("yyyyMMddHHmmssfff"));SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
    static void Log(string m,string stack,LogType kind){if(SessionState.GetBool(Flag,false)&&(kind==LogType.Error||kind==LogType.Exception))failure=m+"\n"+stack;}
    static void Finish(string error){if(captured)BachDangPreferences.SetVolume(previousVolume);SessionState.SetBool(Flag,false);File.WriteAllText("Logs/BossCombat02Playtest.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=error==null,error,phase,checks},Newtonsoft.Json.Formatting.Indented));BachDangSaveSystem.TestDirectory=null;EditorApplication.isPlaying=false;}
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
                Reset();Hero.SetPositionAndRotation(Duel.ArenaCenter+Vector3.back*30,Quaternion.identity);Duel.Boss.SetPositionAndRotation(Duel.ArenaCenter,Quaternion.Euler(0,180,0));
                typeof(RiverBossEncounter).GetField("attackAt",Private).SetValue(Duel,Time.time+1000);
                for(int i=0;i<40;i++){Duel.StepPlayerInput(.02f,Vector2.up,false);Duel.StepBoss(.001f);Poses(.02f);}
                float gap=QuestRowboat.FlatDistance(Hero.position,Duel.Boss.position);int hp=Duel.Health;bool began=Duel.TryPlayerStrike();var trajectory=new List<object>();bool pictured=false;
                for(int i=0;i<55;i++){Duel.StepPlayerInput(.02f,Vector2.zero,false);Poses(.02f);var pose=Hero.GetComponent<RiverFighterPose>();pose.TryBladeSegment(out var a,out var b,out _);if(!pictured&&Duel.Health<hp){pictured=true;BeachBoatReview29Playtest.Picture("Boss_Combat02_Sword",Hero.position+new Vector3(35,20,-35),Hero.position+Vector3.forward*6+Vector3.up*10);}trajectory.Add(new{t=i*.02f,clip=pose.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime,root=(a-Hero.position).ToString(),tip=(b-Hero.position).ToString(),health=Duel.Health});}
                File.WriteAllText("Logs/BossCombat02NaturalSword.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{gap,began,damage=hp-Duel.Health,trajectory},Newtonsoft.Json.Formatting.Indented));
                Check(began&&Duel.Health==hp-12,"Walking up normally and playing the complete unmodified sword swing hits the stationary facing boss exactly once; gap="+gap+" damage="+(hp-Duel.Health));
                Check(gap>=8.9f,"Duel actors retain solid body clearance while allowing a visible sword to reach");
                var sound=BeachBoatSoundscape.Instance;typeof(BeachBoatSoundscape).GetField("nextStep",Private).SetValue(sound,0f);
                var clips=Resources.LoadAll<AudioClip>("BachDang938/ActionAudio").Where(c=>c.name.StartsWith("Deck_")).ToArray();Check(clips.Length==6&&clips.All(c=>c.length<=.17f),"All six replacement deck steps are shorter than the sprint cadence and cannot overlap into a ringing loop");
                for(int i=0;i<6;i++){sound.PlayFootstep(true);Check(sound.LastFootstep!=null&&sound.LastFootstep.name.StartsWith("Deck_"),"Running on the boss deck uses the new recorded wood step "+i);}
                phase=2;return;
            }
            if(phase==2)
            {
                foreach(float draw in new[]{.04f,.3f,1.1f})
                {
                    Reset();combat.RestoreSave(new BachDangSaveData.Fleet{arrows=6});Hero.SetPositionAndRotation(Duel.ArenaCenter+Vector3.back*40,Quaternion.identity);Duel.Boss.SetPositionAndRotation(Duel.ArenaCenter,Quaternion.Euler(0,180,0));Poses(0);
                    var target=Duel.Boss.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Chest).position;var cam=session.storyCamera;cam.transform.position=Hero.position+new Vector3(0,14,-30);cam.transform.LookAt(target);combat.SetAim(true);Duel.StepPlayerInput(.02f,Vector2.zero,false);Poses(.02f);typeof(RiverPlayerCombat).GetMethod("LateUpdate",Private).Invoke(combat,null);Duel.SupportAfterAnimation();Physics.SyncTransforms();int hp=Duel.Health;
                    combat.StepBowDraw(draw,true,true,false);bool fired=combat.StepBowDraw(0,false,false,true);Check(fired&&combat.Arrows==5,"Releasing a "+draw+" second draw actually fires an arrow from the posed bow during the boss fight; aiming="+combat.IsAiming+" allowed="+combat.CanUseBow+" reload="+combat.IsReloading+" blocked="+BeachBoatQuestSession.InputBlocked+" menu="+BachDangFrontEnd.IsBlocking+" dialogue="+BeachBoatStory.StoryDialogueUI.Instance.IsOpen+" state="+Story.Instance.currentState+" checkpoint="+QuestCheckpoint.Instance.IsDefeated+" consumed="+BeachBoatStory.StoryDialogueUI.InputConsumedThisFrame+" arrows="+combat.Arrows);combat.StepArrows(.4f);int expected=draw>=.55f?10:5;Check(hp-Duel.Health==expected,"The physical "+draw+" second arrow reaches the boss for "+expected+" damage without requiring a full meter; actual="+(hp-Duel.Health));Check(!combat.StepBowDraw(0,false,false,true)&&combat.Arrows==5,"A release cannot duplicate the previous shot");
                }
                combat.RestoreSave(new BachDangSaveData.Fleet{arrows=6});combat.SetAim(true);combat.StepBowDraw(.2f,true,true,false);combat.SetAim(false);combat.SetAim(true);Check(!combat.StepBowDraw(0,false,false,true)&&combat.Arrows==6,"Leaving aim cancels the old draw instead of firing it after aiming again");
                foreach(var pair in hashes)Check(File.Exists(pair.Key)&&Hash(pair.Key)==pair.Value,"User save unchanged: "+Path.GetFileName(pair.Key));Finish(null);
            }
        }
        catch(Exception e){Finish(e.ToString());}
    }
}
