using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using BeachBoatStory;
using Object=UnityEngine.Object;
using Story=BeachBoatStory.BachDangStoryManager;

public static class BeachBoatRevision31Playtest
{
    const string Flag="Revision31QA";
    static int phase,frame,samples;static double began;static string failure;static Vector3 previous;static float maxCarryStep,carryDistance;static Vector3 startCarry;
    static readonly List<string> checks=new List<string>();static readonly Dictionary<string,string> hashes=new Dictionary<string,string>();
    const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    static string Hash(string p)=>Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(p)));
    static void Check(bool pass,string text){if(!pass)throw new Exception(text);checks.Add(text);}
    public static void RunInEditor(){phase=samples=0;frame=-1;began=0;failure=null;maxCarryStep=carryDistance=0;checks.Clear();hashes.Clear();SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
    static void Log(string text,string stack,LogType kind){if(SessionState.GetBool(Flag,false)&&(kind==LogType.Error||kind==LogType.Exception))failure=text+"\n"+stack;}
    static void Finish(string error){SessionState.SetBool(Flag,false);File.WriteAllText("Logs/Revision31Playtest.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=error==null,error,phase,maxCarryStep,carryDistance,checks},Newtonsoft.Json.Formatting.Indented));BachDangSaveSystem.TestDirectory=null;EditorApplication.isPlaying=false;}
    static void Move(Vector3 p){var actor=BeachBoatQuestSession.Instance.player;actor.transform.position=p;actor.GetComponent<Rigidbody>().position=p;Physics.SyncTransforms();}
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;EditorApplication.QueuePlayerLoopUpdate();if(frame==Time.frameCount)return;frame=Time.frameCount;
        if(began==0)began=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-began<5)return;
        try
        {
            if(failure!=null)throw new Exception(failure);if(EditorApplication.timeSinceStartup-began>150)throw new Exception("Revision QA timeout");
            var session=BeachBoatQuestSession.Instance;var story=Story.Instance;var boat=QuestRowboat.Instance;var zone=BeachBoatStory.RiverStakeZone.Instance;var game=RiverBoardingCombat.Instance;
            if(phase==0)
            {
                foreach(var p in Directory.GetFiles(Application.persistentDataPath,"campaign938*"))hashes[p]=Hash(p);
                BachDangSaveSystem.TestDirectory=Path.GetFullPath(".utmp/Revision31/"+DateTime.Now.ToString("yyyyMMddHHmmssfff"));BachDangAutoSave.Instance.enabled=false;
                BachDangFrontEnd.Instance.Play();Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();story.collectedWood=story.totalWoodRequired;
                zone.RestoreCheckpoint(new bool[5],false);story.TransitionTo(Story.StoryState.Act2_PlantStakesAtRiver,true);Move(boat.boardingPoint.position);
                Check(boat.TryBoard(),"Board the actual construction boat");var at=zone.placements[0].approach.position;boat.RestoreSave(true,at,Quaternion.identity);
                Check(zone.TryPlant(0),"Start physical construction at the first site");
                var work=StakeConstruction.Active;Check(work!=null&&work.Interact(),"Pick up the stake bundle");
                previous=startCarry=boat.transform.InverseTransformPoint(session.player.transform.position);phase=1;return;
            }
            if(phase==1)
            {
                if(BeachBoatQuestSession.InputBlocked&&!StakeConstruction.AllowsCamera)return;
                var work=StakeConstruction.Active;work.WalkBundle(.02f);typeof(StakeConstruction).GetMethod("LateUpdate",Private).Invoke(work,null);
                var local=boat.transform.InverseTransformPoint(session.player.transform.position);float step=Vector3.Distance(previous,local);if(samples>0){maxCarryStep=Mathf.Max(maxCarryStep,step);carryDistance+=QuestRowboat.FlatDistance(previous,local);}previous=local;
                Check(work.SupportedOnDeck,"Both feet stay on the construction deck at carry sample "+samples);
                if(++samples<80)return;
                Check(carryDistance>6&&maxCarryStep<1.2f,"Carrying follows a continuous inboard path without nearest-point snapping: distance="+carryDistance+" maximum step="+maxCarryStep);
                BeachBoatReview29Playtest.Picture("Construction_Walk",session.player.transform.position-boat.transform.forward*35+boat.transform.right*27+Vector3.up*20,session.player.transform.position+Vector3.up*9);
                BeachBoatReview29Playtest.Picture("Irregular_Work_Crew",boat.transform.position+Vector3.back*240+Vector3.up*180,boat.transform.position+Vector3.forward*50);
                var camp=zone.GetComponent<RiverConstructionCamp>();var saved=camp.Capture();Check(saved.Length==1&&saved[0].layoutVersion==1,"Irregular crew arrangement has a saved layout version");
                var site=saved[0];Check(site.boats.Length>=4&&site.boatRotations.Select(r=>Mathf.RoundToInt(r.eulerAngles.y/5)).Distinct().Count()>=3,"Workers have varied boat headings rather than facing in identical rows");
                float nearest=float.PositiveInfinity;for(int i=0;i<site.boats.Length;i++)for(int j=i+1;j<site.boats.Length;j++)nearest=Mathf.Min(nearest,QuestRowboat.FlatDistance(site.boats[i],site.boats[j]));Check(nearest>65,"Scattered work boats retain room between solid hulls");
                var data=BachDangSaveSystem.Capture("QA irregular crew");BachDangSaveSystem.Apply(data);var restored=zone.GetComponent<RiverConstructionCamp>().Capture();
                Check(restored.Length==1&&restored[0].boats.Length==site.boats.Length&&restored[0].boats.Zip(site.boats,(a,b)=>Vector3.Distance(a,b)<.05f).All(x=>x),"Load preserves the scattered crew's exact boat positions");
                phase=2;return;
            }
            if(phase==2)
            {
                if(BeachBoatQuestSession.InputBlocked)return;zone.RestoreCheckpoint(Enumerable.Repeat(true,5).ToArray());story.TransitionTo(Story.StoryState.Act3_SearchPrisoners,true);
                var guide=session.GetComponent<StoryRouteGuide>()??session.GetComponentInChildren<StoryRouteGuide>();var gates=session.GetComponentsInChildren<BeachBoatStory.CampGateTransition>(true);var gate=gates.First(g=>g.gateType==BeachBoatStory.CampGateTransition.GateType.CampRiverGate);
                var boss=RiverBossEncounter.Instance;boss.GuideTarget.position=EnemyFleetDirector.Instance.enemyShips[0].targetPoint.position;
                Move(gate.transform.position);Check(guide.ResolveTarget(out var title)==gate.transform&&title.Contains("BẾN SÔNG"),"Prisoner search in camp first points to the river teleport gate");
                Move(gate.destinationTarget.position);Check(guide.ResolveTarget(out _) ==boss.GuideTarget,"After the gate, guidance points to the wreck inspection objective");
                var batch=zone.GetComponent<RiverStakeRendering>();if(!batch.Ready)return;
                Check(batch.OriginalCount>=1000&&batch.ChunkCount<batch.OriginalCount/4,"The revealed field uses bounded visual chunks: "+batch.OriginalCount+" original renderers to "+batch.ChunkCount);
                Check(zone.fieldStakes.All(t=>t.gameObject.activeInHierarchy&&t.GetComponentsInChildren<Collider>().Any(c=>c.enabled)),"Visual batching preserves every active stake's actual solid collider");
                var copy=StakeWorkVisual.Create("QA_sample_copy",session.transform,zone.fieldStakes[0]);Check(copy.GetComponentInChildren<MeshRenderer>().enabled,"New player/NPC sample poles remain visible when field visuals are batched");Object.DestroyImmediate(copy.gameObject);
                game.enabled=false;EnemyFleetDirector.Instance.enabled=false;
                var ship=EnemyFleetDirector.Instance.enemyShips[0];var waterStart=ship.targetPoint.position;waterStart.y=TideSystem.CurrentWaterHeight;ship.RestoreSave(new BachDangSaveData.Ship{active=true,destroyed=true,settled=true,released=true,position=waterStart-Vector3.up*20,rotation=Quaternion.identity,sinkStart=waterStart,sinkRotation=Quaternion.identity,destroyedTime=6,sinkDepth=20});ship.enabled=false;boat.LeaveForCheckpoint();
                Physics.SyncTransforms();Vector3 landing=default;bool found=false;
                for(int x=-6;x<=6&&!found;x++)for(int z=-12;z<=12;z++){var probe=ship.transform.position+ship.transform.right*x*12+ship.transform.forward*z*12;if(ship.TryWreckSurfaceAt(probe,out var surface)&&ship.TryWreckWaterExit(surface,4,session.player.transform,out _)){landing=surface;found=true;break;}}
                Check(found,"Find an actual broken timber surface and clear gunwale at the real water height");
                typeof(RiverBoardingCombat).GetMethod("BeginSwim",Private).Invoke(game,new object[]{landing+ship.transform.right*8});
                Check(game.TryTraverse(),"Climb from the water onto actual wreck timber");game.StepPlayer(1.9f,Vector2.zero);Check(game.State==RiverBoardingCombat.PlayerState.EnemyDeck,"The real climb reaches the wreck deck");
                var actor=session.player.transform;var old=actor.position;session.storyCamera.transform.rotation=Quaternion.LookRotation(ship.transform.forward);
                for(int i=0;i<50;i++)game.StepPlayer(.02f,new Vector2(0,1));var current=actor.position;
                Check(game.State==RiverBoardingCombat.PlayerState.EnemyDeck,"Moving across the timber keeps the player on deck");
                Check(game.TryTraverse()&&game.State==RiverBoardingCombat.PlayerState.Swimming,"F leaves the wreck into nearby clear water");
                Check(QuestRowboat.FlatDistance(current,actor.position)<=65,"Water re-entry starts beside the current deck position rather than a remote cached swim origin");
                Check(RiverHullCollision.ClearAt(actor.position,3.5f,actor),"The chosen water re-entry location is outside real hull and stake solids");
                var point=actor.position;game.StepPlayer(.02f,Vector2.zero);Check(QuestRowboat.FlatDistance(point,actor.position)<1,"The next swim update does not snap back to the old climbing origin");
                foreach(var pair in hashes)Check(File.Exists(pair.Key)&&Hash(pair.Key)==pair.Value,"User save unchanged: "+Path.GetFileName(pair.Key));Finish(null);
            }
        }
        catch(Exception e){Finish(e.ToString());}
    }
}
