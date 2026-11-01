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

public static class BeachBoatDuelContactPlaytest
{
    const string Flag="DuelContact01QA";static int phase,frame;static double began;static string failure;static float previousVolume;static bool captured;
    static readonly List<string> checks=new List<string>();static readonly Dictionary<string,string> hashes=new Dictionary<string,string>();static BachDangSaveData baseline;static Vector3 retryPoint;
    const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    static string Hash(string p)=>Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(p)));
    static void Check(bool pass,string text){if(!pass)throw new Exception(text);checks.Add(text);}
    public static void RunInEditor(){phase=0;frame=-1;began=0;failure=null;captured=false;checks.Clear();hashes.Clear();BachDangSaveSystem.TestDirectory=Path.GetFullPath(".utmp/DuelContact/"+DateTime.Now.ToString("yyyyMMddHHmmssfff"));SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
    static void Log(string m,string stack,LogType kind){if(SessionState.GetBool(Flag,false)&&(kind==LogType.Error||kind==LogType.Exception))failure=m+"\n"+stack;}
    static void Finish(string error){if(captured)BachDangPreferences.SetVolume(previousVolume);SessionState.SetBool(Flag,false);File.WriteAllText("Logs/DuelContactPlaytest.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=error==null,error,phase,checks},Newtonsoft.Json.Formatting.Indented));BachDangSaveSystem.TestDirectory=null;EditorApplication.isPlaying=false;}
    static RiverBossEncounter Duel=>RiverBossEncounter.Instance;
    static Transform Hero=>BeachBoatQuestSession.Instance.player.transform;
    static void Poses(float dt)
    {
        foreach(var root in new[]{Hero,Duel.Boss}){var pose=root.GetComponent<RiverFighterPose>();pose.Animator.Update(dt);typeof(RiverFighterPose).GetMethod("LateUpdate",Private).Invoke(pose,null);}Duel.SupportAfterAnimation();
    }
    static float SkinnedBottom(Animator rig)
    {
        float bottom=float.PositiveInfinity;
        foreach(var skin in rig.GetComponentsInChildren<SkinnedMeshRenderer>())
        {var mesh=skin.sharedMesh;if(!mesh.isReadable)continue;var vs=mesh.vertices;var ws=mesh.boneWeights;var bs=mesh.bindposes;var bones=skin.bones;var matrices=bones.Select((b,i)=>b.localToWorldMatrix*bs[i]).ToArray();for(int i=0;i<vs.Length;i++){var v=vs[i];var w=ws[i];float y=0;if(w.weight0>0)y+=matrices[w.boneIndex0].MultiplyPoint3x4(v).y*w.weight0;if(w.weight1>0)y+=matrices[w.boneIndex1].MultiplyPoint3x4(v).y*w.weight1;if(w.weight2>0)y+=matrices[w.boneIndex2].MultiplyPoint3x4(v).y*w.weight2;if(w.weight3>0)y+=matrices[w.boneIndex3].MultiplyPoint3x4(v).y*w.weight3;bottom=Mathf.Min(bottom,y);}}return bottom;
    }
    static Bounds SkinnedBounds(Animator rig)
    {
        Bounds result=default;bool first=true;
        foreach(var skin in rig.GetComponentsInChildren<SkinnedMeshRenderer>())
        {var mesh=skin.sharedMesh;if(!mesh.isReadable)continue;var vs=mesh.vertices;var ws=mesh.boneWeights;var bs=mesh.bindposes;var bones=skin.bones;var ms=bones.Select((b,i)=>b.localToWorldMatrix*bs[i]).ToArray();for(int i=0;i<vs.Length;i++){var v=vs[i];var w=ws[i];var p=ms[w.boneIndex0].MultiplyPoint3x4(v)*w.weight0;if(w.weight1>0)p+=ms[w.boneIndex1].MultiplyPoint3x4(v)*w.weight1;if(w.weight2>0)p+=ms[w.boneIndex2].MultiplyPoint3x4(v)*w.weight2;if(w.weight3>0)p+=ms[w.boneIndex3].MultiplyPoint3x4(v)*w.weight3;if(first){result=new Bounds(p,Vector3.zero);first=false;}else result.Encapsulate(p);}}return result;
    }
    static void AuditSwing(RiverFighterPose pose)
    {
        var samples=new List<object>();
        foreach(string state in new[]{"Attack","Attack2"})for(int i=0;i<=20;i++)
        {pose.Animator.Play(state,0,i/20f);Poses(0);pose.TryBladeSegment(out var a,out var b,out _);var localA=Duel.Boss.InverseTransformPoint(a);var localB=Duel.Boss.InverseTransformPoint(b);samples.Add(new{state,t=i/20f,root=localA.ToString(),tip=localB.ToString(),tipY=b.y-Duel.ArenaCenter.y});}
        File.WriteAllText("Logs/DuelBladeAnimationAudit.json",Newtonsoft.Json.JsonConvert.SerializeObject(samples,Newtonsoft.Json.Formatting.Indented));
    }
    static void Reset()
    {
        Hero.GetComponent<PlayerVitals>().RestoreSave(baseline);Duel.Restore(baseline.boss);Hero.SetPositionAndRotation(baseline.playerPosition,baseline.playerRotation);PlayerShield.Instance.Restore(false);Duel.enabled=false;RiverPlayerCombat.Instance.enabled=false;BeachBoatQuestSession.Instance.storyCamera.GetComponent<BeachBoatStory.Camera_Script>().enabled=false;BeachBoatQuestSession.Instance.storyCamera.transform.rotation=Quaternion.identity;
        typeof(RiverBossEncounter).GetField("attackAt",Private).SetValue(Duel,0f);typeof(RiverBossEncounter).GetField("recoverAt",Private).SetValue(Duel,0f);RiverPlayerCombat.Instance.SetAim(false);Poses(0);
    }
    static void ArmBoss()
    {
        Duel.Boss.SetPositionAndRotation(Duel.ArenaCenter,Quaternion.identity);Hero.position=Duel.ArenaCenter+Vector3.forward*20;Poses(0);Duel.StepBoss(.02f);
        float hp=Hero.GetComponent<PlayerVitals>().Health;Poses(.03f);Check(Hero.GetComponent<PlayerVitals>().Health==hp,"Boss windup does not deal distance-based damage");Duel.StepBoss(.8f);
    }
    static void ContactTarget(Transform victim,RiverFighterPose attacker)
    {
        attacker.TryBladeSegment(out var a,out var b,out _);var rig=victim.GetComponentInChildren<Animator>();var chest=rig.GetBoneTransform(HumanBodyBones.Chest).position;
        float t=Mathf.Abs(b.y-a.y)>.01f?Mathf.Clamp01((chest.y-a.y)/(b.y-a.y)):.5f;var blade=Vector3.Lerp(a,b,t);var offset=chest-victim.position;var p=victim.position;p.x=blade.x-offset.x;p.z=blade.z-offset.z;victim.position=p;
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;EditorApplication.QueuePlayerLoopUpdate();if(frame==Time.frameCount)return;frame=Time.frameCount;if(began==0)began=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-began<5)return;
        try
        {
            if(failure!=null)throw new Exception(failure);if(EditorApplication.timeSinceStartup-began>150)throw new Exception("Duel contact QA timed out");
            var session=BeachBoatQuestSession.Instance;var vitals=Hero.GetComponent<PlayerVitals>();
            if(phase==0)
            {
                foreach(var p in Directory.GetFiles(Application.persistentDataPath,"campaign938*"))hashes[p]=Hash(p);BachDangAutoSave.Instance.enabled=false;previousVolume=BachDangPreferences.Volume;captured=true;BachDangPreferences.SetVolume(0);
                BachDangFrontEnd.Instance.Play();Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();Story.Instance.TransitionTo(Story.StoryState.Act3_CommanderDuel,true);BachDangBattleTimeline.Instance.RestoreSave(new BachDangSaveData.Tide{moment=(int)BachDangBattleTimeline.BattleMoment.Victory,hour=11.5f});
                Duel.Restore(new BachDangSaveData.Boss{version=1,mask=7,health=100,inArena=true,bossPosition=new Vector3(70,0,100)});Hero.position=Duel.ArenaCenter+new Vector3(-65,1,-100);Duel.SupportAfterAnimation();baseline=BachDangSaveSystem.Capture("QA duel controls");phase=10;return;
            }
            if(phase==10)
            {
                Reset();var start=Hero.position;for(int i=0;i<30;i++)Duel.StepPlayerInput(.02f,Vector2.up,false);float walk=QuestRowboat.FlatDistance(start,Hero.position);Hero.position=start;float stamina=vitals.Stamina;
                for(int i=0;i<30;i++)Duel.StepPlayerInput(.02f,Vector2.up,true);float run=QuestRowboat.FlatDistance(start,Hero.position);
                Check(Duel.IsRunning&&run>walk*1.6f&&run<walk*1.9f,"Held Shift sprints continuously instead of triggering a dodge: walk="+walk+" run="+run);
                Check(Mathf.Abs(stamina-vitals.Stamina-7.8f)<.1f,"Sprint consumes 13 stamina per second of actual movement");
                float rest=vitals.Stamina;Duel.StepPlayerInput(.02f,Vector2.zero,true);Check(!Duel.IsRunning&&vitals.Stamina==rest,"Holding Shift while stationary does not drain stamina");
                Check(Duel.TryJump()&&!Duel.TryJump(),"Space launches one real jump and cannot double-jump");float peak=0;bool jumped=false;
                for(int i=0;i<65;i++)
                {
                    Duel.StepPlayerInput(.02f,Vector2.zero,false);Poses(.02f);peak=Mathf.Max(peak,Duel.JumpOffset);
                    if(i==12||i==25||i==60){float low=SkinnedBottom(Hero.GetComponentInChildren<Animator>());Check(Mathf.Abs(low-(Duel.ArenaCenter.y+.15f+Duel.JumpOffset))<.6f,"Actual skinned boots follow airborne height without snapping onto the deck: "+i);}
                    if(i==25){jumped=Hero.GetComponentInChildren<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Duel_Jump");BeachBoatReview29Playtest.Picture("Boss_Duel_Jump",Hero.position+new Vector3(35,18,-40),Hero.position+Vector3.up*8);}
                }
                Check(jumped&&peak>5&&!Duel.IsAirborne&&Duel.JumpOffset==0,"Existing humanoid jump animation reaches an apex and lands on the same solid deck");
                Check(Duel.TryJump(),"A second grounded jump is available after landing");Duel.StepPlayerInput(.25f,Vector2.zero,false);Poses(.02f);var airborneSave=BachDangSaveSystem.Capture("QA midair");BachDangSaveSystem.Apply(airborneSave);Duel.SupportAfterAnimation();Check(!Duel.IsAirborne&&Mathf.Abs(SkinnedBottom(Hero.GetComponentInChildren<Animator>())-Duel.ArenaCenter.y-.15f)<.6f,"Midair checkpoint load returns to a supported controllable deck state");
                phase=1;return;
            }
            if(phase==1)
            {
                Reset();ArmBoss();var pose=Duel.Boss.GetComponent<RiverFighterPose>();Hero.position=Duel.ArenaCenter+new Vector3(-80,0,-80);AuditSwing(pose);pose.Animator.Play("Attack",0,.65f);Poses(0);
                pose.TryBladeSegment(out var a,out var b,out _);var dump=new{a=a.ToString(),b=b.ToString(),heroChest=Hero.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Chest).position.ToString(),state=pose.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime,armed=typeof(RiverBossEncounter).GetField("bossStrikeArmed",Private).GetValue(Duel),floor=Duel.ArenaCenter.y};File.WriteAllText("Logs/DuelBladePose.json",Newtonsoft.Json.JsonConvert.SerializeObject(dump));
                float hp=vitals.Health;
                pose.Animator.Play("Attack",0,.28f);Poses(0);ContactTarget(Hero,pose);Duel.SupportAfterAnimation();Check(vitals.Health==hp,"Even overlapping blade/body during the sword lift cannot deal damage");
                Hero.position=Duel.ArenaCenter+new Vector3(-80,0,-80);pose.Animator.Play("Attack",0,.65f);Poses(0);ContactTarget(Hero,pose);Duel.SupportAfterAnimation();
                Check(vitals.Health<hp&&Duel.BossMeleeHits==1,"Visible boss blade contacting posed player body deals one real hit");hp=vitals.Health;for(int i=0;i<20;i++)Duel.SupportAfterAnimation();Check(vitals.Health==hp&&Duel.BossMeleeHits==1,"The same swing cannot apply damage repeatedly across overlapping frames");
                phase=2;return;
            }
            if(phase==2)
            {
                Reset();ArmBoss();var enemyPose=Duel.Boss.GetComponent<RiverFighterPose>();Hero.position=Duel.ArenaCenter+new Vector3(-75,0,-75);float hp=vitals.Health;
                enemyPose.Animator.Play("Attack",0,.65f);Poses(0);enemyPose.TryBladeSegment(out var a,out var b,out float radius);var bladeBox=new Bounds(a,Vector3.zero);bladeBox.Encapsulate(b);bladeBox.Expand(radius*2);
                bool found=false;var original=Hero.position;
                foreach(float angle in new[]{0f,45,-45,60,-60,75,-75})
                {var offset=Quaternion.Euler(0,angle,0)*Vector3.forward*31;Hero.position=new Vector3(Duel.Boss.position.x+offset.x,original.y,Duel.Boss.position.z+offset.z);if(!SkinnedBounds(Hero.GetComponentInChildren<Animator>()).Intersects(bladeBox)){found=true;break;}}
                Check(found&&QuestRowboat.FlatDistance(Hero.position,Duel.Boss.position)<32&&Vector3.Dot(Duel.Boss.forward,(Hero.position-Duel.Boss.position).normalized)>.2f,"Independent skinned body bounds are clear of the visible blade while the player is inside the former radial damage zone");
                Duel.SupportAfterAnimation();for(int i=0;i<25;i++)Poses(.02f);Check(vitals.Health==hp&&Duel.BossMeleeHits==0,"Near-range missed blade swings cause no health loss over the full animation");
                Reset();ArmBoss();Hero.position=Duel.ArenaCenter+new Vector3(-75,0,-75);enemyPose=Duel.Boss.GetComponent<RiverFighterPose>();enemyPose.Animator.Play("Attack",0,.65f);Poses(0);Check(Duel.TryDodge(Vector3.right),"Dodge can be active during the physical contact check");hp=vitals.Health;ContactTarget(Hero,enemyPose);Duel.SupportAfterAnimation();Check(vitals.Health<hp,"Dodge grants no fake invulnerability: an actual body contact still counts");
                Reset();var pose=Hero.GetComponent<RiverFighterPose>();Hero.position=Duel.ArenaCenter+Vector3.back*20;Hero.rotation=Quaternion.identity;Duel.Boss.position=Duel.ArenaCenter+Vector3.right*75;Poses(0);int health=Duel.Health;Check(Duel.TryPlayerStrike()&&Duel.Health==health,"Player attack also waits for blade contact instead of instantly subtracting health by range");pose.Animator.Play("Attack",0,.65f);Poses(0);ContactTarget(Duel.Boss,pose);Duel.SupportAfterAnimation();Check(Duel.Health==health-12&&Duel.PlayerMeleeHits==1,"Actual player blade contact removes twelve boss health exactly once");
                Reset();ArmBoss();Hero.position=Duel.ArenaCenter+new Vector3(-75,0,-75);enemyPose=Duel.Boss.GetComponent<RiverFighterPose>();enemyPose.Animator.Play("Attack2",0,.73f);Poses(0);hp=vitals.Health;ContactTarget(Hero,enemyPose);typeof(RiverBossEncounter).GetField("bossStrikeDamage",Private).SetValue(Duel,28);Duel.SupportAfterAnimation();Check(vitals.Health==hp-28,"The heavy downward swing also requires actual blade/body contact");
                Reset();RiverPlayerCombat.Instance.SetAim(true);Duel.StepPlayerInput(.02f,Vector2.up,true);Check(!Duel.IsRunning,"Drawing the bow prevents sprint speed while aiming");Check(Duel.TryJump(),"Jump is available with the bow drawn");
                for(int i=0;i<16;i++){Duel.StepPlayerInput(.02f,Vector2.up,true);Poses(.02f);}var rig=Hero.GetComponentInChildren<Animator>();Check(rig.runtimeAnimatorController.name=="Boss_Duel_Bow"&&rig.GetCurrentAnimatorStateInfo(0).IsName("Duel_Jump"),"Bow aiming retains its upper body layer above the real duel jump animation");Check(Mathf.Abs(SkinnedBottom(rig)-Duel.ArenaCenter.y-.15f-Duel.JumpOffset)<.6f,"Aiming jump retains real boot clearance");
                Reset();Check(PlayerShield.Instance.SetEquipped(true),"Shield can be drawn on the duel deck");PlayerShield.Instance.SetGuarding(true);Duel.StepPlayerInput(.02f,Vector2.up,true);Check(!Duel.IsRunning,"Guarding prevents sprint speed");Check(Duel.TryJump(),"Jump is available with the shield drawn");for(int i=0;i<16;i++){Duel.StepPlayerInput(.02f,Vector2.up,false);Poses(.02f);}rig=Hero.GetComponentInChildren<Animator>();Check(rig.runtimeAnimatorController.name=="Boss_Duel_Foot"&&rig.GetCurrentAnimatorStateInfo(0).IsName("Duel_Jump"),"Shield and unarmed locomotion also use the real jump state");
                var hud=Object.FindAnyObjectByType<CompactRiverCombatHUD>();hud.Refresh("",false);var labels=hud.GetComponentsInChildren<TMPro.TMP_Text>(true).Select(t=>t.text).ToArray();Check(labels.Contains("SHIFT")&&labels.Contains("CTRL")&&labels.Contains("NHẢY")&&!labels.Contains("LEO TÀU"),"Compact boss HUD shows the actual sprint, dodge and jump bindings");
                phase=3;return;
            }
            if(phase==3)
            {
                Reset();var before=Hero.position;Check(Duel.TryDodge(Vector3.right),"Ctrl dodge remains available as a separate action");Duel.StepPlayerInput(.1f,Vector2.zero,false);Duel.SupportAfterAnimation();Check(QuestRowboat.FlatDistance(before,Hero.position)>8,"Separate dodge moves the physical player body");
                var saved=BachDangSaveSystem.Capture("QA moving duel retry");BachDangSaveSystem.Apply(saved);retryPoint=Hero.position;phase=4;return;
            }
            if(phase==4)
            {
                Duel.StepPlayerInput(.02f,Vector2.zero,false);Duel.SupportAfterAnimation();Check(QuestRowboat.FlatDistance(retryPoint,Hero.position)<.1f,"Retry cancels stale dodge direction instead of auto-sliding at spawn");var start=Hero.position;Duel.StepPlayerInput(.02f,Vector2.right,true);Duel.SupportAfterAnimation();Check(Duel.IsRunning&&QuestRowboat.FlatDistance(start,Hero.position)>.5f,"Movement and held sprint work on the first fresh input frame after retry");
                foreach(var pair in hashes)Check(File.Exists(pair.Key)&&Hash(pair.Key)==pair.Value,"User save unchanged: "+Path.GetFileName(pair.Key));Finish(null);
            }
        }
        catch(Exception e){Finish(e.ToString());}
    }
}
