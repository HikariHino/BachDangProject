using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BeachBoatStory;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class BeachBoatArrivalRecoveryPlaytest
{
    const string Flag="ArrivalRecoveryQA";
    static double started;
    static int phase;
    static NPCAmbientDialogue npc,other;
    static Vector3 center;
    static bool spawnClear,walked,fallbackClear,nearTalk,farHidden,behindHidden,wallHidden,singleSpeaker,cancelSafe,wrongFailed,timeoutFailed,deathClosed;
    public static void RunAndExit(){EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
    static void Finish(string failure)
    {
        CampRecoveryGame.Active?.Close();Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/ArrivalRecoveryPlaytest.json",JsonConvert.SerializeObject(new{success=failure==null,failure,spawnClear,walked,fallbackClear,nearTalk,farHidden,behindHidden,wallHidden,singleSpeaker,cancelSafe,wrongFailed,timeoutFailed,deathClosed},Formatting.Indented));
        SessionState.SetBool(Flag,false);BeachBoatQAExit.AfterPlayMode(failure==null?0:1);
    }
    static void Move(Vector3 p,Quaternion rotation)
    {var player=BeachBoatQuestSession.Instance.player;player.transform.SetPositionAndRotation(p,rotation);var b=player.GetComponent<Rigidbody>();b.position=p;b.rotation=rotation;if(!b.isKinematic)b.linearVelocity=Vector3.zero;Physics.SyncTransforms();}
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;
        if(started==0)started=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-started<4)return;
        if(EditorApplication.timeSinceStartup-started>65){Finish("QA timeout at "+phase);return;}
        try
        {
            var session=BeachBoatQuestSession.Instance;var player=session.player;var vitals=player.GetComponent<PlayerVitals>();
            BeachBoatStory.BachDangFrontEnd.Instance?.Play();
            foreach(var intro in Object.FindObjectsByType<BeachBoatStory.OpeningEvent>(FindObjectsSortMode.None))intro.SkipOpening();
            BeachBoatStory.StoryDialogueUI.Instance?.Close();
            if(phase==0)
            {
                var gates=Object.FindObjectsByType<BeachBoatStory.CampGateTransition>(FindObjectsSortMode.None);
                var arrival=gates.First(g=>g.gateType==BeachBoatStory.CampGateTransition.GateType.CampRiverGate).destinationTarget;
                spawnClear=PlayerArrival.IsClear(player,arrival.position,arrival.rotation)&&PlayerArrival.HasExit(player,arrival.position,arrival.rotation);
                if(!spawnClear)throw new Exception("Saved river arrival is still blocked");
                var gate=gates.First(g=>g.gateType==BeachBoatStory.CampGateTransition.GateType.RiverGate);
                var old=gate.transform.position-Vector3.forward*26;old.y=Terrain.activeTerrain.SampleHeight(old)+Terrain.activeTerrain.transform.position.y+3.2f;
                if(PlayerArrival.IsClear(player,old,Quaternion.identity))throw new Exception("Test no longer reproduces the original post overlap");
                fallbackClear=PlayerArrival.Teleport(player,old,Quaternion.identity)&&PlayerArrival.IsClear(player,player.transform.position,player.transform.rotation)&&PlayerArrival.HasExit(player,player.transform.position,player.transform.rotation);
                if(!fallbackClear)throw new Exception("Blocked arrival fallback failed");
                walked=WalkFromArrival(session,arrival);if(!walked)throw new Exception("Real character cannot walk away from repaired river spawn");
                center=session.playerSpawn.position+Vector3.right*150;center.y=Terrain.activeTerrain.SampleHeight(center)+Terrain.activeTerrain.transform.position.y+3.2f;
                Move(center,Quaternion.identity);
                npc=new GameObject("QA_Close_Villager").AddComponent<NPCAmbientDialogue>();npc.interactDistance=200;npc.transform.position=center+Vector3.forward*8;
                other=new GameObject("QA_Other_Villager").AddComponent<NPCAmbientDialogue>();other.interactDistance=200;other.transform.position=center+Vector3.forward*10;
                phase=1;return;
            }
            if(phase==1)
            {
                BeachBoatStory.StoryDialogueUI.TryConsumeInput();nearTalk=npc.Talk();if(!nearTalk)throw new Exception("Nearby visible NPC cannot talk after E is consumed");
                BeachBoatStory.StoryDialogueUI.Instance.Close();
                singleSpeaker=!other.Talk();if(!singleSpeaker)throw new Exception("A second NPC stole the nearest speaker interaction");
                npc.transform.position=center+Vector3.forward*60;other.enabled=false;phase=2;return;
            }
            if(phase==2)
            {
                farHidden=!npc.Talk();if(!farHidden)throw new Exception("Oversized serialized range still allows distant conversation");
                npc.transform.position=center-Vector3.forward*8;phase=3;return;
            }
            if(phase==3)
            {
                behindHidden=!npc.Talk();if(!behindHidden)throw new Exception("NPC behind the player still offers conversation");
                npc.transform.position=center+Vector3.forward*9;
                var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="QA_Conversation_Wall";wall.transform.position=center+new Vector3(0,12,4);wall.transform.localScale=new Vector3(20,40,2);Physics.SyncTransforms();phase=4;return;
            }
            if(phase==4)
            {
                wallHidden=!npc.Talk();if(!wallHidden)throw new Exception("Conversation passes through a wall");
                Object.DestroyImmediate(GameObject.Find("QA_Conversation_Wall"));Object.DestroyImmediate(npc.gameObject);Object.DestroyImmediate(other.gameObject);
                var facility=Object.FindObjectsByType<CampFacility>(FindObjectsSortMode.None).First(f=>f.kind==CampFacility.FacilityKind.Medical);
                Move(facility.transform.position,Quaternion.identity);vitals.TakeImpact(35,"QA");vitals.TryExert(70,1);
                float hp=vitals.Health,sp=vitals.Stamina;var cursor=Cursor.lockState;
                if(!facility.TryUse())throw new Exception("Cannot open recovery game");
                CampRecoveryGame.Active.Close();cancelSafe=vitals.Health==hp&&vitals.Stamina==sp&&Cursor.lockState==cursor&&!BeachBoatQuestSession.InteractionBlocked;
                if(!cancelSafe||!facility.TryUse())throw new Exception("Cancel rewarded player, kept input blocked or consumed cooldown");
                var game=CampRecoveryGame.Active;game.StartAttempt();for(int i=0;i<3;i++)game.Submit((game.ExpectedAction+1)%3);
                wrongFailed=game.Phase==CampRecoveryGame.GamePhase.Lost&&!game.ClaimReward()&&vitals.Health==hp&&vitals.Stamina==sp;
                game.StartAttempt();game.AdvanceTime(game.TimeLimit+1);timeoutFailed=game.Phase==CampRecoveryGame.GamePhase.Lost&&!game.ClaimReward();
                if(!wrongFailed||!timeoutFailed)throw new Exception("Loss or timeout granted recovery");
                game.StartAttempt();vitals.TakeImpact(10000,"QA");deathClosed=CampRecoveryGame.Active==null&&QuestCheckpoint.Instance.IsDefeated;
                if(!deathClosed||!QuestCheckpoint.Instance.Retry())throw new Exception("Death did not close recovery and restore checkpoint");
                phase=5;return;
            }
            if(phase==5)
            {
                var facility=Object.FindObjectsByType<CampFacility>(FindObjectsSortMode.None).First(f=>f.kind==CampFacility.FacilityKind.Medical);
                Move(facility.transform.position,Quaternion.identity);vitals.TakeImpact(30,"UI QA");
                if(!facility.TryUse())throw new Exception("Cannot reopen medical game after checkpoint");phase=6;return;
            }
            if(phase==6)
            {
                var root=GameObject.Find("CampRecoveryCanvas").transform;
                if(string.IsNullOrWhiteSpace(root.Find("RecoveryOverlay/Card/Instructions/Start/Label").GetComponent<TMPro.TMP_Text>().text)||
                    string.IsNullOrWhiteSpace(root.Find("RecoveryOverlay/Card/Close/Label").GetComponent<TMPro.TMP_Text>().text))throw new Exception("Recovery buttons have no labels");
                BeachBoatCampFacilityPlaytest.Capture("Recovery_Instructions");
                Click("Card/Instructions/Start");
                var game=CampRecoveryGame.Active;if(game.Phase!=CampRecoveryGame.GamePhase.Playing)throw new Exception("Start button is not wired");
                for(int i=0;i<3;i++)Click("Card/Activity/Choices/Option"+game.ExpectedAction);
                if(game.Phase!=CampRecoveryGame.GamePhase.Won)throw new Exception("Choice buttons did not complete recovery");phase=7;return;
            }
            if(phase==7)
            {
                BeachBoatCampFacilityPlaytest.Capture("Recovery_Won");
                Click("Card/Result/RetryOrClaim");
                if(CampRecoveryGame.Active!=null||vitals.Health!=vitals.maxHealth)throw new Exception("Claim button did not grant earned recovery and close modal");
                Finish(null);
            }
        }
        catch(Exception error){Finish(error.ToString());}
    }
    static void Click(string path)
    {
        var canvas=GameObject.Find("CampRecoveryCanvas");var button=canvas.transform.Find("RecoveryOverlay/"+path).GetComponent<UnityEngine.UI.Button>();
        if(!button.isActiveAndEnabled||!button.interactable)throw new Exception("Inactive UI button "+path);
        UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject,new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current),UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
    }
    static bool WalkFromArrival(BeachBoatQuestSession session,Transform arrival)
    {
        var player=session.player;var movement=player.GetComponent<BeachBoatStory.Character_Movement>();var body=player.GetComponent<Rigidbody>();
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;var type=movement.GetType();var mode=Physics.simulationMode;
        var orbit=type.GetField("orbit",flags).GetValue(movement);var view=type.GetField("view",flags).GetValue(movement);
        try
        {
            movement.enabled=false;Move(arrival.position,arrival.rotation);body.isKinematic=false;
            type.GetField("orbit",flags).SetValue(movement,null);type.GetField("view",flags).SetValue(movement,null);
            type.GetField("freeLook",flags).SetValue(movement,true);type.GetField("input",flags).SetValue(movement,Vector2.up);
            Physics.simulationMode=SimulationMode.Script;var start=body.position;
            for(int i=0;i<65;i++){type.GetMethod("FixedUpdate",flags).Invoke(movement,null);Physics.Simulate(Time.fixedDeltaTime);}
            return QuestRowboat.FlatDistance(start,body.position)>18;
        }
        finally{Physics.simulationMode=mode;type.GetField("orbit",flags).SetValue(movement,orbit);type.GetField("view",flags).SetValue(movement,view);movement.enabled=true;}
    }
}
