using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BeachBoatStory;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
public static class BeachBoatSolidDebug
{
    const string Flag="SolidPhysicsDebug";static int phase;static double until;static object[] citizens;
    public static void RunAndExit(){EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;
        if(until==0){until=EditorApplication.timeSinceStartup+4;return;}if(EditorApplication.timeSinceStartup<until)return;
        var session=BeachBoatQuestSession.Instance;if(session==null)return;
        if(phase==0){BachDangFrontEnd.Instance.Play();Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();phase=1;until=EditorApplication.timeSinceStartup+5;return;}
        if(phase==1)
        {
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            citizens=Object.FindObjectsByType<NPCWander>(FindObjectsSortMode.None).Where(w=>w.isActiveAndEnabled).Take(25).Select(w=>
            {
                var target=(Vector3)typeof(NPCWander).GetField("target",flags).GetValue(w);var dir=target-w.transform.position;dir.y=0;dir.Normalize();
                var hits=Physics.CapsuleCastAll(w.transform.position+Vector3.up*4.55f,w.transform.position+Vector3.up*(w.NominalStrideSpeed/.85f),2.8f,dir,7,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
                return (object)new{w.name,w.CurrentSpeed,w.CurrentActivity,pos=w.transform.position.ToString(),target=target.ToString(),radius=w.PatrolRadius,hits=hits.Where(h=>!(h.collider is TerrainCollider)&&!h.transform.IsChildOf(w.transform)&&!w.transform.IsChildOf(h.transform)).Select(h=>new{h.collider.name,distance=h.distance,point=h.point.ToString(),normal=h.normal.ToString(),bounds=h.collider.bounds.ToString(),actor=h.collider.GetComponentInParent<Animator>()?.name})};
            }).ToArray();
            File.WriteAllText("Logs/SolidNpcDebug.json",Newtonsoft.Json.JsonConvert.SerializeObject(citizens,Newtonsoft.Json.Formatting.Indented));
            var story=BeachBoatStory.BachDangStoryManager.Instance;BeachBoatStory.RiverStakeZone.Instance.RestoreCheckpoint(Enumerable.Repeat(true,5).ToArray());story.collectedWood=story.totalWoodRequired;story.TransitionTo(BeachBoatStory.BachDangStoryManager.StoryState.Act3_WaitForLowTide,true);
            var boat=QuestRowboat.Instance;session.player.transform.position=boat.boardingPoint.position;session.player.GetComponent<Rigidbody>().position=session.player.transform.position;Physics.SyncTransforms();boat.TryBoard();RiverPlayerCombat.Instance.ReturnFireEnabled=false;RiverBoardingCombat.Instance.AlliedAIEnabled=false;
            foreach(var ship in EnemyFleetDirector.Instance.enemyShips)ship.moveSpeed=100;
            Time.timeScale=4;phase=2;until=EditorApplication.timeSinceStartup+25;return;
        }
        var fleet=EnemyFleetDirector.Instance;
        File.WriteAllText("Logs/SolidFleetDebug.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{fleet.EbbStarted,fleet.ShipsInside,ships=fleet.enemyShips.Select(s=>new{s.name,pos=s.transform.position.ToString(),target=s.targetPoint.position.ToString(),route=s.routeWaypoints.Length,blocked=s.GetComponent<BoatHullCollision>()?.RejectedMoves,by=s.GetComponent<BoatHullCollision>()?.LastBlocker?.name,owner=s.GetComponent<BoatHullCollision>()?.LastBlocker?.GetComponentInParent<BoatHullCollision>()?.name,bounds=s.GetComponent<BoatHullCollision>()?.LastBlocker?.bounds.ToString()})},Newtonsoft.Json.Formatting.Indented));
        SessionState.SetBool(Flag,false);BeachBoatQAExit.AfterPlayMode(0);
    }
}
