using System;
using System.IO;
using System.Linq;
using BeachBoatStory;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class BeachBoatBoardingPlaytest
{
    const string Flag="BoardingCombatQA";
    static double started,until;
    static int phase;
    static bool stallsClear,scaleCorrect,swam,climbed,swordHit,enemyHit,alliedHit,hostileBoarded,checkpoint;
    static EnemyDeckGuard target;
    static EnemyShip ship;
    public static void RunAndExit(){EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
    static void Finish(string failure)
    {
        var game=RiverBoardingCombat.Instance;Directory.CreateDirectory("Logs");File.WriteAllText("Logs/BoardingPlaytest.json",JsonConvert.SerializeObject(new{success=failure==null,failure,stallsClear,scaleCorrect,swam,climbed,swordHit,enemyHit,alliedHit,hostileBoarded,checkpoint,game?.PlayerSwordHits,game?.AlliedSwordHits,game?.AlliedArrowHits,game?.EnemySwordHits,game?.SwimsStarted,game?.ClimbsStarted,game?.HostileBoardings},Formatting.Indented));
        SessionState.SetBool(Flag,false);BeachBoatQAExit.AfterPlayMode(failure==null?0:1);
    }
    static void MovePlayer(Vector3 p,Quaternion q)
    {var player=BeachBoatQuestSession.Instance.player;player.transform.SetPositionAndRotation(p,q);var body=player.GetComponent<Rigidbody>();body.position=p;body.rotation=q;Physics.SyncTransforms();}
    static float Length(Transform t)
    {var q=t.rotation;t.rotation=Quaternion.identity;var b=BeachBoatWorldAudit.BoundsOf(t);t.rotation=q;return Mathf.Max(b.size.x,b.size.z);}
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;
        if(started==0)started=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-started<4||EditorApplication.timeSinceStartup<until)return;
        if(EditorApplication.timeSinceStartup-started>80){Finish("Boarding timeout at "+phase);return;}
        try
        {
            var session=BeachBoatQuestSession.Instance;var game=RiverBoardingCombat.Instance;var archery=RiverPlayerCombat.Instance;var fleet=EnemyFleetDirector.Instance;var boat=QuestRowboat.Instance;
            if(phase==0)
            {
                foreach(var intro in Object.FindObjectsByType<BeachBoatStory.OpeningEvent>(FindObjectsSortMode.None))intro.SkipOpening();BeachBoatStory.StoryDialogueUI.Instance?.Close();
                var fields=Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(r=>r.name=="Dat_Ruong"||r.name=="Lua_Non"||r.name.Contains("Field")||r.name.Contains("Rice_Rows")).Select(r=>r.bounds).ToArray();
                bool Overlap(Bounds a,Bounds b)=>a.min.x<b.max.x&&a.max.x>b.min.x&&a.min.z<b.max.z&&a.max.z>b.min.z;
                stallsClear=Object.FindObjectsByType<VillageWorkStall>(FindObjectsSortMode.None).All(s=>!fields.Any(b=>Overlap(BeachBoatWorldAudit.BoundsOf(s.transform),b)));
                float row=Length(boat.transform);scaleCorrect=fleet.enemyShips.All(s=>Length(s.transform.Find("RedSailJunkVisual"))>row*2.2f)&&game.friendlyBoats.All(b=>Length(b.transform.Find("DaiVietPatrolVisual_Rebuilt"))>row*1.9f);
                if(!stallsClear||!scaleCorrect)throw new Exception("A stall still overlaps farmland or ships remain too small");
                ship=fleet.enemyShips.First(s=>s.transform.Find("RedSailJunkVisual/Flagship_Timber_Hull")!=null);
                CompareShips(boat,game.friendlyBoats[0],fleet.enemyShips.First(s=>s!=ship),ship);
                BeachBoatStory.RiverStakeZone.Instance.RestoreCheckpoint(Enumerable.Repeat(true,BeachBoatStory.RiverStakeZone.Instance.ClusterCount).ToArray());
                BeachBoatStory.BachDangStoryManager.Instance.collectedWood=BeachBoatStory.BachDangStoryManager.Instance.totalWoodRequired;
                BeachBoatStory.BachDangStoryManager.Instance.TransitionTo(BeachBoatStory.BachDangStoryManager.StoryState.Act3_WaitForLowTide,true);
                MovePlayer(boat.boardingPoint.position,boat.boardingPoint.rotation);if(!boat.TryBoard())throw new Exception("Boarding fixture could not start battle");
                archery.ReturnFireEnabled=false;phase=1;until=EditorApplication.timeSinceStartup+3;return;
            }
            if(phase==1)
            {
                game.enabled=false;foreach(var vessel in fleet.enemyShips)vessel.moveSpeed=0;
                if(!ship.gameObject.activeSelf)ship.Activate();ship.transform.position=boat.transform.position+Vector3.forward*170;ship.transform.rotation=Quaternion.identity;
                swam=game.TryTraverse()&&game.State==RiverBoardingCombat.PlayerState.Swimming;
                if(!swam)throw new Exception("F did not start swimming");
                var p=ship.transform.TransformPoint(new Vector3(-(ship.stakeHitDistance+10),0,0));p.y=TideSystem.CurrentWaterHeight-5;MovePlayer(p,Quaternion.LookRotation(Vector3.right));
                phase=2;until=EditorApplication.timeSinceStartup+.15;return;
            }
            if(phase==2)
            {
                var p=session.player.transform.position;Capture("Boarding_Player_Swim",p+new Vector3(-55,32,-60),p+Vector3.up*7);
                if(!game.TryTraverse()||game.State!=RiverBoardingCombat.PlayerState.Climbing)throw new Exception("F cannot climb a nearby ship edge");
                game.StepPlayer(.8f);phase=21;until=EditorApplication.timeSinceStartup+.15;return;
            }
            if(phase==21)
            {
                game.StepPlayer(0);var old=session.player.transform.position;ship.transform.position+=Vector3.right*20;game.StepPlayer(0);
                if(Mathf.Abs(session.player.transform.position.x-old.x-20)>.1f)throw new Exception("Climber did not follow the moving hull");
                Capture("Boarding_Player_Climb",session.player.transform.position+new Vector3(-65,32,-60),session.player.transform.position+Vector3.up*12);
                game.StepPlayer(1.1f);climbed=game.State==RiverBoardingCombat.PlayerState.EnemyDeck&&game.SwordDrawn;
                if(!climbed)throw new Exception("Climb did not finish on deck with a drawn sword");
                target=archery.guards.First(g=>g.ship==ship&&!g.IsIndependentBoarder&&g.IsAlive);
                var p=target.transform.position-target.transform.forward*17;MovePlayer(p,Quaternion.LookRotation(target.transform.position-p));
                int hp=target.Health;swordHit=game.TrySlash()&&target.Health==hp-1;
                float health=session.player.GetComponent<PlayerVitals>().Health;game.StepEnemies(.1f);enemyHit=session.player.GetComponent<PlayerVitals>().Health<health;
                if(!swordHit||!enemyHit)throw new Exception("Player or enemy sword cannot deal real close-range damage");
                phase=3;until=EditorApplication.timeSinceStartup+.18;return;
            }
            if(phase==3)
            {
                var p=session.player.transform.position;Capture("Boarding_Sword_Duel",p+new Vector3(-45,38,-55),p+Vector3.up*13);
                for(int i=0;i<game.friendlyBoats.Length;i++)game.friendlyBoats[i].transform.position=ship.transform.position+new Vector3(-140-i*20,0,i*22);
                for(int n=0;n<450;n++){game.StepAllies(.1f);game.StepHostiles(.1f);game.StepEnemies(.1f);}
                alliedHit=game.AlliedSwordHits>0;hostileBoarded=game.HostileBoardings>0;
                if(!alliedHit||game.AlliedArrowHits<=0||!hostileBoarded||game.SwimsStarted<3||game.ClimbsStarted<3)throw new Exception("NPCs did not shoot, swim, climb and engage in two-sided boarding combat");
                phase=4;until=EditorApplication.timeSinceStartup+.2;return;
            }
            if(phase==4)
            {
                Capture("Boarding_Two_Sides",ship.transform.position+new Vector3(-220,130,-250),ship.transform.position+Vector3.up*25);
                game.ResetBattle();if(session.player.GetComponentInChildren<Animator>().runtimeAnimatorController!=boat.rowingController)throw new Exception("Returning from sword combat did not restore rowing animation");
                session.player.GetComponent<PlayerVitals>().TakeImpact(10000,"Boarding checkpoint QA");
                checkpoint=QuestCheckpoint.Instance.IsDefeated&&game.State==RiverBoardingCombat.PlayerState.Rowboat&&!game.SwordDrawn&&game.ActiveAllies==0&&QuestCheckpoint.Instance.Retry();
                if(!checkpoint)throw new Exception("Defeat left swimming, sword or NPC combat active at checkpoint");Finish(null);
            }
        }
        catch(Exception error){Finish(error.ToString());}
    }
    static void CompareShips(QuestRowboat row,AmbientRiverBoat patrol,EnemyShip red,EnemyShip flagship)
    {
        var roots=new[]{row.transform,patrol.transform,red.transform,flagship.transform};var positions=roots.Select(t=>t.position).ToArray();var rotations=roots.Select(t=>t.rotation).ToArray();var active=roots.Select(t=>t.gameObject.activeSelf).ToArray();
        for(int i=0;i<roots.Length;i++){roots[i].gameObject.SetActive(true);roots[i].SetPositionAndRotation(new Vector3(-3000+i*350,TideSystem.CurrentWaterHeight,2300),Quaternion.identity);}
        Capture("Fleet_Size_Comparison",new Vector3(-2475,TideSystem.CurrentWaterHeight+490,1200),new Vector3(-2475,TideSystem.CurrentWaterHeight+50,2300));
        Capture("NamHan_Indigo_Flagship",flagship.transform.position+new Vector3(-260,170,240),flagship.transform.position+Vector3.up*70);
        for(int i=0;i<roots.Length;i++){roots[i].SetPositionAndRotation(positions[i],rotations[i]);roots[i].gameObject.SetActive(active[i]);}
    }
    static void Capture(string name,Vector3 position,Vector3 focus)
    {
        var go=new GameObject("Boarding_QA_Camera");var camera=go.AddComponent<Camera>();camera.CopyFrom(BeachBoatQuestSession.Instance.storyCamera);camera.enabled=false;camera.farClipPlane=4500;camera.fieldOfView=60;go.transform.position=position;go.transform.LookAt(focus);
        var light=new GameObject("Boarding_QA_Sun").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;light.transform.rotation=Quaternion.Euler(50,135,0);
        var rt=new RenderTexture(1280,800,24);var image=new Texture2D(1280,800,TextureFormat.RGB24,false);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();
        Directory.CreateDirectory("Logs");File.WriteAllBytes("Logs/"+name+".png",image.EncodeToPNG());RenderTexture.active=null;camera.targetTexture=null;Object.DestroyImmediate(rt);Object.DestroyImmediate(image);Object.DestroyImmediate(light.gameObject);Object.DestroyImmediate(go);
    }
}
