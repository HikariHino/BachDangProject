using System;
using System.IO;
using System.Linq;
using BeachBoatStory;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class BeachBoatSwimmingWreckPlaytest
{
    const string Flag="SwimWreckRequestedQA";
    static double started,until;
    static int phase,strokesBefore,savedSurvivors,savedDeaths,savedStrokes;
    static EnemyShip ship;
    static RiverFighterPose pose;
    static bool cameraSafe,swimAligned,swimSound,climbAligned,wreckRetained,noRaft,mixedCrew,targetsBoth,deckDuel,checkpoint;
    public static void RunAndExit(){EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
    static void Move(Vector3 p)
    {var player=BeachBoatQuestSession.Instance.player;player.transform.position=p;player.GetComponent<Rigidbody>().position=p;Physics.SyncTransforms();}
    static void Finish(string failure)
    {
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/SwimmingWreckPlaytest.json",JsonConvert.SerializeObject(new{success=failure==null,failure,cameraSafe,swimAligned,swimSound,climbAligned,wreckRetained,noRaft,mixedCrew,targetsBoth,deckDuel,checkpoint,strokes=savedStrokes,survivors=savedSurvivors,deaths=savedDeaths},Formatting.Indented));
        SessionState.SetBool(Flag,false);BeachBoatQAExit.AfterPlayMode(failure==null?0:1);
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;
        if(started==0)started=EditorApplication.timeSinceStartup;
        if(EditorApplication.timeSinceStartup-started<4||EditorApplication.timeSinceStartup<until)return;
        if(EditorApplication.timeSinceStartup-started>75){Finish("Timeout at phase "+phase);return;}
        try
        {
            var session=BeachBoatQuestSession.Instance;var game=RiverBoardingCombat.Instance;var boat=QuestRowboat.Instance;var fleet=EnemyFleetDirector.Instance;
            if(phase==0)
            {
                foreach(var intro in Object.FindObjectsByType<BeachBoatStory.OpeningEvent>(FindObjectsSortMode.None))intro.SkipOpening();BeachBoatStory.StoryDialogueUI.Instance?.Close();
                BeachBoatStory.RiverStakeZone.Instance.RestoreCheckpoint(Enumerable.Repeat(true,5).ToArray());
                var story=BeachBoatStory.BachDangStoryManager.Instance;story.collectedWood=story.totalWoodRequired;story.TransitionTo(BeachBoatStory.BachDangStoryManager.StoryState.Act3_WaitForLowTide,true);
                Move(boat.boardingPoint.position);if(!boat.TryBoard())throw new Exception("Cannot board swimming test fixture");
                RiverPlayerCombat.Instance.ReturnFireEnabled=false;phase=1;until=EditorApplication.timeSinceStartup+3;return;
            }
            if(phase==1)
            {
                game.enabled=false;foreach(var vessel in fleet.enemyShips)vessel.moveSpeed=0;
                ship=fleet.enemyShips[0];if(!ship.gameObject.activeSelf)ship.Activate();ship.transform.position=boat.transform.position+Vector3.forward*170;ship.transform.rotation=Quaternion.identity;
                if(!game.TryTraverse())throw new Exception("Cannot enter swimming");
                pose=session.player.GetComponent<RiverFighterPose>();var p=ship.transform.TransformPoint(new Vector3(-53,0,0));p.y=TideSystem.CurrentWaterHeight-5;Move(p);
                session.player.transform.rotation=Quaternion.LookRotation(Vector3.right);pose.SwimMoving=true;
                var yaw=session.player.transform.rotation;pose.EvaluateTraversal(.4f);
                var hips=pose.Animator.GetBoneTransform(HumanBodyBones.Hips);var head=pose.Animator.GetBoneTransform(HumanBodyBones.Head);
                var axis=(head.position-hips.position).normalized;
                swimAligned=Quaternion.Angle(yaw,session.player.transform.rotation)<.1f&&Vector3.Dot(axis,session.player.transform.forward)>.65f&&Mathf.Abs(hips.position.y-TideSystem.CurrentWaterHeight)<5;
                if(!swimAligned)throw new Exception("Swimmer is vertical, yaws without input, or floats above water");
                strokesBefore=BeachBoatSoundscape.Instance.SwimStrokesPlayed;for(int i=0;i<180;i++)pose.EvaluateTraversal(.02f);
                swimSound=BeachBoatSoundscape.Instance.SwimStrokesPlayed-strokesBefore>=2;
                if(!swimSound)throw new Exception("Swimming stroke does not trigger repeated audible splashes");
                var camera=session.storyCamera.GetComponent<BeachBoatStory.Camera_Script>();cameraSafe=true;
                for(int yawAngle=0;yawAngle<360;yawAngle+=30)for(int pitch=-25;pitch<=70;pitch+=19)
                {
                    var q=Quaternion.Euler(pitch,yawAngle,0);var candidate=session.player.transform.position+Vector3.up*27+q*new Vector3(0,0,-70);
                    var safe=camera.KeepAboveWater(candidate);cameraSafe&=safe.y>=TideSystem.CurrentWaterHeight+1.9f;
                }
                var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/BachDangScaleAndLife/BachDang_Water_Player7.mat");
                cameraSafe&=material.GetFloat("_Cull")==0&&material.GetVector("_CurrentVelocity").sqrMagnitude>1&&!ShaderUtil.ShaderHasError(material.shader);
                if(!cameraSafe)throw new Exception("Water still disappears on a low orbit or current shader failed");
                Capture("New_Swim_Stroke",session.player.transform.position+new Vector3(-32,24,-48),session.player.transform.position+Vector3.up*5);
                Capture("New_Water_Low_Orbit",camera.KeepAboveWater(session.player.transform.position+new Vector3(-50,-15,-60)),session.player.transform.position+Vector3.up*4);
                if(!game.TryTraverse())throw new Exception("Cannot grab nearby rail");game.StepPlayer(.8f);phase=2;until=EditorApplication.timeSinceStartup+.15;return;
            }
            if(phase==2)
            {
                pose.EvaluateTraversal(.1f);var hips=pose.Animator.GetBoneTransform(HumanBodyBones.Hips);var head=pose.Animator.GetBoneTransform(HumanBodyBones.Head);
                climbAligned=game.State==RiverBoardingCombat.PlayerState.Climbing&&Vector3.Dot((head.position-hips.position).normalized,Vector3.up)>.65f;
                if(!climbAligned)throw new Exception("Climber does not face upright while gripping the rail");
                Capture("New_Climb_Rail",session.player.transform.position+new Vector3(-55,24,-45),session.player.transform.position+Vector3.up*16);
                game.StepPlayer(1.1f);if(game.State!=RiverBoardingCombat.PlayerState.EnemyDeck||!pose.Animator.enabled)throw new Exception("Climb did not restore sword animation on deck");
                ship.TakeDamage();for(int i=0;i<80;i++)ship.Step(.1f);
                mixedCrew=game.ShipwreckDeaths>0&&game.ShipwreckSurvivors>0;targetsBoth=game.SurvivorsTargetingPlayer>0&&game.SurvivorsTargetingAllies>0;
                noRaft=ship.WreckTarget!=null&&ship.WreckTarget.childCount==0;
                wreckRetained=ship.HasSettled&&ship.gameObject.activeSelf&&ship.transform.position.y<TideSystem.CurrentWaterHeight-10;
                if(!mixedCrew||!targetsBoth||!noRaft||!wreckRetained)throw new Exception("Wreck missing, crew not split, or an artificial raft remains");
                var survivors=RiverPlayerCombat.Instance.guards.Where(g=>g.ship==ship&&g.IsIndependentBoarder&&g.IsAlive).ToArray();
                var old=survivors.Select(g=>g.transform.position).ToArray();game.StepHostiles(.5f);
                if(!survivors.Where((g,i)=>(g.transform.position-old[i]).sqrMagnitude>1).Any())throw new Exception("Shipwreck survivors did not swim toward their targets");
                for(int i=0;i<survivors.Length;i++)survivors[i].GetComponent<RiverFighterPose>().EvaluateTraversal(.3f);
                Capture("New_Retained_Wreck_Survivors",ship.WreckTarget.position+new Vector3(-160,90,-170),ship.WreckTarget.position+Vector3.up*5);
                game.StepPlayer(0);Move(boat.transform.position+Vector3.up*10);if(!game.TryTraverse())throw new Exception("Cannot return from wreck to rowboat");
                for(int n=0;n<800;n++)game.StepHostiles(.1f);
                game.StepEnemies(.1f);game.StepDefenders();
                deckDuel=game.DefenderHits>0&&game.DefenderDamageTaken>0;
                if(!deckDuel)throw new Exception("Enemy survivors and the real patrol deck crew did not exchange sword damage");
                savedSurvivors=game.ShipwreckSurvivors;savedDeaths=game.ShipwreckDeaths;savedStrokes=BeachBoatSoundscape.Instance.SwimStrokesPlayed;
                survivors[0].Hit(100,false);game.StepHostiles(.1f);
                game.ResetBattle();session.player.GetComponent<PlayerVitals>().TakeImpact(10000,"Wreck retry QA");checkpoint=QuestCheckpoint.Instance.Retry()&&fleet.enemyShips.All(s=>!s.gameObject.activeSelf&&!s.isDestroyed)&&!game.PlayerAway&&fleet.enemyShips.SelectMany(s=>s.GetComponentsInChildren<EnemyDeckGuard>(true)).All(g=>g.gameObject.activeSelf&&g.Health==2);
                if(!checkpoint)throw new Exception("Retry kept wrecks or swimming actors from the old battle");Finish(null);
            }
        }
        catch(Exception error){Finish(error.ToString());}
    }
    static void Capture(string name,Vector3 position,Vector3 focus)
    {
        var go=new GameObject("RequestedQAView");var camera=go.AddComponent<Camera>();camera.CopyFrom(BeachBoatQuestSession.Instance.storyCamera);camera.enabled=false;camera.farClipPlane=3500;go.transform.position=position;go.transform.LookAt(focus);
        var rt=new RenderTexture(1280,800,24);var pixels=new Texture2D(1280,800,TextureFormat.RGB24,false);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1280,800),0,0);pixels.Apply();Directory.CreateDirectory("Logs");File.WriteAllBytes("Logs/"+name+".png",pixels.EncodeToPNG());
        RenderTexture.active=null;camera.targetTexture=null;Object.DestroyImmediate(rt);Object.DestroyImmediate(pixels);Object.DestroyImmediate(go);
    }
}
