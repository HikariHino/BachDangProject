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
public static class BeachBoatDisciplinePlaytest
{
    const string Flag="Discipline30QA";static int phase,frame,placement;static double began;static string failure;static float mark;static BachDangSaveData baseline;
    static ProtectedCampNPC soldier,civilian,commander;static Vector3 testArea;static float health;static int arrows;
    static readonly List<string> checks=new List<string>();static readonly Dictionary<string,string> hashes=new Dictionary<string,string>();
    static string Hash(string p)=>Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(p)));
    static void Check(bool pass,string text){if(!pass)throw new Exception(text);checks.Add(text);}
    public static void RunInEditor(){phase=placement=0;frame=-1;began=0;failure=null;checks.Clear();hashes.Clear();SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
    static void Log(string m,string stack,LogType kind){if(SessionState.GetBool(Flag,false)&&(kind==LogType.Error||kind==LogType.Exception))failure=m+"\n"+stack;}
    static void Finish(string error){SessionState.SetBool(Flag,false);Time.timeScale=1;File.WriteAllText("Logs/DisciplinePlaytest.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=error==null,error,phase,checks},Newtonsoft.Json.Formatting.Indented));BachDangSaveSystem.TestDirectory=null;EditorApplication.isPlaying=false;}
    static void Recover(){Time.timeScale=1;QuestCheckpoint.Instance.PrepareForLoad();BachDangSaveSystem.Apply(baseline);BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();RiverPlayerCombat.Instance.SetAim(false);}
    static void Place(ProtectedCampNPC npc,float distance=22)
    {
        npc.transform.SetParent(BeachBoatQuestSession.Instance.transform,true);npc.gameObject.SetActive(true);
        var walker=npc.GetComponent<NPCWander>();if(walker!=null)walker.enabled=false;var daily=npc.GetComponent<NPCDailyActivity>();if(daily!=null)daily.enabled=false;
        var location=testArea+Vector3.right*(++placement*200);var actor=BeachBoatQuestSession.Instance.player;var body=actor.GetComponent<Rigidbody>();body.isKinematic=true;body.interpolation=RigidbodyInterpolation.None;actor.transform.SetPositionAndRotation(location,Quaternion.identity);body.position=location;body.rotation=Quaternion.identity;npc.transform.SetPositionAndRotation(location+Vector3.forward*distance,Quaternion.Euler(0,180,0));Physics.SyncTransforms();
    }
    static void Shoot(ProtectedCampNPC npc)
    {
        var end=npc.AimPoint;var start=end-Vector3.forward*80;typeof(RiverPlayerCombat).GetMethod("Launch",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(RiverPlayerCombat.Instance,new object[]{start,end,null,2,false,1.1f});RiverPlayerCombat.Instance.StepArrows(.35f);
    }
    static Ray CommandRay()=>new Ray(commander.AimPoint-Vector3.forward*80,Vector3.forward);
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;EditorApplication.QueuePlayerLoopUpdate();if(frame==Time.frameCount)return;frame=Time.frameCount;
        if(began==0)began=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-began<4)return;
        try
        {
            if(failure!=null)throw new Exception(failure);if(EditorApplication.timeSinceStartup-began>160)throw new Exception("Discipline QA timed out at "+phase);
            var session=BeachBoatQuestSession.Instance;var discipline=CampDiscipline.Instance;var player=session.player.transform;var combat=RiverPlayerCombat.Instance;var boarding=RiverBoardingCombat.Instance;var vitals=player.GetComponent<PlayerVitals>();
            if(phase==0)
            {
                foreach(var p in Directory.GetFiles(Application.persistentDataPath,"campaign938*"))hashes[p]=Hash(p);
                BachDangSaveSystem.TestDirectory=Path.GetFullPath(".utmp/DisciplineQA/"+DateTime.Now.ToString("yyyyMMddHHmmssfff"));BachDangAutoSave.Instance.enabled=false;
                BachDangFrontEnd.Instance.Play();Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
                discipline.Discover();Check(discipline.Actors.Count(a=>a.Type==ProtectedCampNPC.Kind.Commander)==1,"Exactly the real Ngo Quyen actor is classified as commander");
                Check(discipline.Actors.Count(a=>a.Type==ProtectedCampNPC.Kind.Soldier)>10&&discipline.Actors.Count(a=>a.Type==ProtectedCampNPC.Kind.Civilian)>10,"Camp guards, boat troops, villagers and market vendors have explicit friendly-fire roles");
                commander=discipline.Actors.Single(a=>a.Type==ProtectedCampNPC.Kind.Commander);soldier=discipline.Actors.First(a=>a.Type==ProtectedCampNPC.Kind.Soldier&&a.gameObject.activeInHierarchy&&a.GetComponent<NPCWander>()!=null);civilian=discipline.Actors.First(a=>a.Type==ProtectedCampNPC.Kind.Civilian&&a.gameObject.activeInHierarchy);
                Story.Instance.TransitionTo(Story.StoryState.Act2_SupplyPreparation,true);
                foreach(var stall in VillagePreparationQuest.Instance.stalls)
                {
                    var station=stall.EnsureWorkstation();typeof(VillageWorkstation).GetField("<HeightScale>k__BackingField",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(station,2.5f);station.Environment.localScale=new Vector3(1,2.5f,1);player.position=stall.InteractionPosition;Check(stall.TryOpen(),"Open tallest workbench "+stall.kind);var work=stall.Physical;Physics.SyncTransforms();var box=station.StockCollider.bounds;
                    for(int i=0;i<8;i++){var direction=Quaternion.Euler(0,i*45,0)*Vector3.forward;var p=box.center+Vector3.Scale(box.extents,direction)+direction*7;p.y=stall.transform.position.y;player.position=p;Check(work.CanInteract,"Real ground-level pickup works at edge angle "+i+" on height-scaled "+stall.kind);}
                    var far=box.center+Vector3.right*50;far.y=stall.transform.position.y;player.position=far;Check(!work.CanInteract,"Remote pickup is still rejected at "+stall.kind);
                    var side=box.center+Vector3.left*(box.extents.x+7);side.y=stall.transform.position.y;player.position=side;Check(work.Confirm(),"E actually takes the stock from the side at "+stall.kind);stall.Close();
                }
                Story.Instance.TransitionTo(Story.StoryState.Act1_ExploreCamp,true);baseline=BachDangSaveSystem.Capture("QA before discipline");testArea=BeachBoatStory.RiverStakeZone.Instance.FieldBounds.center+Vector3.up*500;phase=1;return;
            }
            if(phase==1)
            {
                if(BeachBoatQuestSession.InputBlocked)return;Check(!combat.BattleActive&&combat.CanUseBow&&CampDiscipline.WeaponsAvailable,"Sword and bow are available in Quest 1 without starting the river battle");
                Place(soldier);Check(boarding.ToggleSword()&&boarding.SwordDrawn,"Q draws the sword on ordinary camp ground");Check(boarding.TrySlash()&&soldier.Retaliating&&!QuestCheckpoint.Instance.IsDefeated,"An actual sword swing provokes an immortal allied soldier instead of killing him");
                health=vitals.Health;mark=Time.time;phase=10;return;
            }
            if(phase==10)
            {
                if(Time.time-mark<.4f)return;
                soldier.StepRetaliation(.02f);Check(vitals.Health<health,"The struck soldier actually swings back and removes player health");
                for(int i=0;i<20;i++)discipline.Hit(soldier,CombatImpactKind.Arrow);Check(soldier.Retaliating&&!soldier.Dead&&soldier.gameObject.activeInHierarchy,"Repeated player arrows cannot kill a retaliating ally");
                soldier.transform.position+=Vector3.right*4;var savedPursuer=soldier.transform.position;
                var aggro=BachDangSaveSystem.Capture("QA hostile ally");BachDangSaveSystem.Apply(aggro);Check(soldier.Retaliating&&aggro.discipline.attackers.Length>0&&Vector3.Distance(soldier.transform.position,savedPursuer)<.1f,"Saving and loading preserves the soldier's hostility");
                mark=Time.time;Time.timeScale=3;phase=11;return;
            }
            if(phase==11)
            {
                if(!QuestCheckpoint.Instance.IsDefeated){if(Time.time-mark>15)throw new Exception("Retaliation stopped before defeating the player");return;}
                Check(soldier.Retaliating&&!soldier.Dead,"The immortal soldier keeps attacking until the player dies");
                Recover();Place(civilian);mark=Time.time;phase=2;return;
            }
            if(phase==2)
            {
                if(BeachBoatQuestSession.InputBlocked||Time.time-mark<.8f)return;
                bool line=boarding.CanMeleeReach(player,civilian.transform,36);string rayNames=string.Join(";",Physics.RaycastAll(player.position+Vector3.up*8,(civilian.transform.position-player.position).normalized,22,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore).Select(h=>h.collider.name+"/"+h.transform.parent?.name));bool drew=boarding.ToggleSword(),slashed=boarding.TrySlash();Check(drew&&slashed&&civilian.Dead&&QuestCheckpoint.Instance.IsDefeated,"A real sword hit kills a civilian and immediately loses the game: draw="+drew+" slash="+slashed+" dead="+civilian.Dead+" defeated="+QuestCheckpoint.Instance.IsDefeated+" player="+player.position+" victim="+civilian.transform.position+" facing="+player.forward+" line="+line+" ray="+rayNames);
                Recover();Place(civilian);phase=3;return;
            }
            if(phase==3)
            {
                if(BeachBoatQuestSession.InputBlocked)return;Shoot(civilian);Check(civilian.Dead&&QuestCheckpoint.Instance.IsDefeated,"A physical arrow hitting a civilian also kills and defeats the player");
                Recover();Place(soldier);phase=4;return;
            }
            if(phase==4)
            {
                if(BeachBoatQuestSession.InputBlocked)return;combat.SetAim(true);arrows=combat.Arrows;Check(combat.TryFire(new Ray(soldier.AimPoint-Vector3.forward*80,Vector3.forward),1.1f)&&combat.Arrows==arrows-1,"The real bow fire action consumes ammunition outside any battle");combat.SetAim(false);combat.StepArrows(2);
                Shoot(soldier);Check(soldier.Retaliating&&!soldier.Dead&&!QuestCheckpoint.Instance.IsDefeated,"A physical arrow provokes a surviving allied soldier");
                Recover();Place(commander);mark=Time.time;phase=5;return;
            }
            if(phase==5)
            {
                if(BeachBoatQuestSession.InputBlocked||Time.time-mark<.8f)return;
                Check(boarding.ToggleSword()&&boarding.TrySlash()&&QuestCheckpoint.Instance.IsDefeated&&discipline.ArmyHostile&&discipline.RetaliatingSoldiers>10,"Striking Ngo Quyen orders the whole allied army against the player and immediately loses");
                Recover();Place(commander);phase=6;return;
            }
            if(phase==6)
            {
                if(BeachBoatQuestSession.InputBlocked)return;Shoot(commander);Check(QuestCheckpoint.Instance.IsDefeated&&discipline.ArmyHostile,"A physical arrow at Ngo Quyen receives the same immediate execution response");
                Recover();Place(commander);phase=7;return;
            }
            if(phase==7)
            {
                if(BeachBoatQuestSession.InputBlocked)return;
                for(int i=0;i<100;i++)discipline.ObserveCommanderAim(true,CommandRay(),.02f);
                Check(discipline.CommanderWarnings==1&&!QuestCheckpoint.Instance.IsDefeated&&!discipline.ArmyHostile,"Holding the bow on Ngo Quyen counts once and only gives a first warning");
                var voice=(DialogueVoice)typeof(CampDiscipline).GetField("voice",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(discipline);Check(voice.CurrentClip!=null,"The first commander warning has a real Vietnamese voice clip");
                discipline.ObserveCommanderAim(true,new Ray(commander.AimPoint-Vector3.forward*80,Vector3.right),.2f);discipline.ObserveCommanderAim(true,CommandRay(),.01f);Check(discipline.CommanderWarnings==1,"Brief sight jitter does not create a second aiming offence");
                var warn=BachDangSaveSystem.Capture("QA warned commander");BachDangSaveSystem.Apply(warn);Check(discipline.CommanderWarnings==1,"The commander warning count survives a save/load");
                discipline.ObserveCommanderAim(false,CommandRay(),.1f);discipline.ObserveCommanderAim(true,CommandRay(),.1f);Check(discipline.CommanderWarnings==2&&QuestCheckpoint.Instance.IsDefeated&&discipline.ArmyHostile,"A distinct second aim immediately calls the army and defeats the player");
                Check(voice.CurrentClip!=null,"The execution order has a real voice clip");
                Recover();Check(discipline.CommanderWarnings==0&&discipline.RetaliatingSoldiers==0&&!civilian.Dead,"Retrying the earlier save clears offences and restores the civilian");
                foreach(var pair in hashes)Check(File.Exists(pair.Key)&&Hash(pair.Key)==pair.Value,"User save unchanged: "+Path.GetFileName(pair.Key));Finish(null);
            }
        }
        catch(Exception e){Finish(e.ToString());}
    }
}
