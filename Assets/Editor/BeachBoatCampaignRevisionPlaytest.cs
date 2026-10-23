using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using BeachBoatStory;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

public static class BeachBoatCampaignRevisionPlaytest
{
    const string Flag="CampaignRevisionQA";
    static readonly List<string> checks=new List<string>();
    static int phase;
    static double started,until;
    static string runtimeError;
    static Vector3 shore;
    static EnemyDeckGuard victim;
    static void Check(bool value,string message){if(!value)throw new Exception(message);checks.Add(message);}
    public static void RunAndExit(){EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Record;Application.logMessageReceived+=Record;}
    static void Record(string text,string stack,LogType type){if(SessionState.GetBool(Flag,false)&&EditorApplication.isPlaying&&(type==LogType.Error||type==LogType.Exception))runtimeError=text+"\n"+stack;}
    static void Finish(string error)
    {
        File.WriteAllText("Logs/CampaignRevisionPlaytest.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=error==null,error,phase,checks},Newtonsoft.Json.Formatting.Indented));
        BachDangSaveSystem.TestDirectory=null;SessionState.SetBool(Flag,false);BeachBoatQAExit.AfterPlayMode(error==null?0:1);
    }
    static void Move(Vector3 point)
    {
        var player=BeachBoatQuestSession.Instance.player;player.transform.SetPositionAndRotation(point,Quaternion.identity);var body=player.GetComponent<Rigidbody>();body.position=point;body.rotation=Quaternion.identity;Physics.SyncTransforms();
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;
        if(started==0)started=EditorApplication.timeSinceStartup;
        if(EditorApplication.timeSinceStartup-started<4||EditorApplication.timeSinceStartup<until)return;
        try
        {
            if(runtimeError!=null)throw new Exception(runtimeError);
            var menu=BachDangFrontEnd.Instance;var game=RiverBoardingCombat.Instance;var boat=QuestRowboat.Instance;var session=BeachBoatQuestSession.Instance;
            var intro=Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>();var combat=RiverPlayerCombat.Instance;var fleet=EnemyFleetDirector.Instance;
            if(phase==0){menu.Play();phase=1;until=EditorApplication.timeSinceStartup+.7;return;}
            if(phase==1)
            {
                var narrator=intro.GetComponent<DialogueVoice>();Check(narrator.CurrentClip!=null&&narrator.ProfileId=="narrator"&&narrator.SpeechSource.isPlaying,"Introduction plays a separate narrator recording");
                BeachBoatStory.StoryDialogueUI.Instance.Show("Ngô Quyền","Ngươi đã đến. Đất nước đang cần người có lòng và có sức. Hãy đi quanh doanh trại, gặp quản quân để nhận lệnh.");
                var voice=BeachBoatStory.StoryDialogueUI.Instance.GetComponent<DialogueVoice>();
                // The actual authored commander line is covered by the complete dialogue test.
                var manifest=Newtonsoft.Json.Linq.JObject.Parse(Resources.Load<TextAsset>("BachDang938/Voices/manifest").text);
                Check(manifest["profiles"].First(v=>(string)v["id"]=="commander")["voice"].ToString()!=narrator.VoiceName,"Commander and narrator use distinct base voices");
                BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();intro.SkipOpening();
                Check(game.friendlyBoats.Length>=10,"Ten allied ships are registered with combat and saves");
                foreach(var ally in game.friendlyBoats)
                {
                    ally.enabled=false;Check(Vector3.Distance(ally.route[0].position,ally.route[2].position)>700,"Long patrol route: "+ally.name);
                    var start=ally.transform.position;for(int i=0;i<700;i++)ally.Step(.05f);
                    Check(QuestRowboat.FlatDistance(ally.transform.position,start)>320,"Patrol actually travels far beyond its starting position: "+ally.name);
                    ally.ResetForCheckpoint();
                }
                var hull=boat.transform.Find("Thuyen_Go_Song");var bounds=BeachBoatWorldAudit.BoundsOf(hull);
                Check(Mathf.Max(bounds.size.x,bounds.size.z)<65,"Player's hull is a compact one-person rowboat");
                Check(Mathf.Abs(session.player.transform.lossyScale.y-14)<.01f,"Rowboat resize preserves player dimensions");
                BeachBoatStory.RiverStakeZone.Instance.RestoreCheckpoint(Enumerable.Repeat(true,BeachBoatStory.RiverStakeZone.Instance.ClusterCount).ToArray());
                var story=BeachBoatStory.BachDangStoryManager.Instance;story.collectedWood=story.totalWoodRequired;story.TransitionTo(BeachBoatStory.BachDangStoryManager.StoryState.Act3_WaitForLowTide,true);Move(boat.boardingPoint.position);
                Check(boat.TryBoard(),"Can enter battle using the compact rowboat");combat.ReturnFireEnabled=false;game.enabled=false;phase=2;until=EditorApplication.timeSinceStartup+2;return;
            }
            if(phase==2)
            {
                foreach(var ship in fleet.enemyShips){ship.enabled=false;ship.moveSpeed=0;}
                game.AlliedAIEnabled=false;
                Check(game.TryTraverse(),"Can jump from rowboat into water");
                var terrain=Terrain.activeTerrain;bool found=false;var origin=boat.dockPoint.position;
                for(float radius=20;radius<700&&!found;radius+=15)for(int angle=0;angle<360&&!found;angle+=10)
                {
                    var point=origin+Quaternion.Euler(0,angle,0)*Vector3.forward*radius;float floor=terrain.SampleHeight(point)+terrain.transform.position.y;
                    if(floor<TideSystem.CurrentWaterHeight||floor>TideSystem.CurrentWaterHeight+10)continue;
                    point.y=TideSystem.CurrentWaterHeight-5;Move(point);game.StepPlayer(0);
                    if(game.State==RiverBoardingCombat.PlayerState.Land){shore=session.player.transform.position;found=true;}
                }
                Check(found,"Swimming into a shallow shore automatically enters walking state");
                var body=session.player.GetComponent<Rigidbody>();var motor=session.player.GetComponent<BeachBoatStory.Character_Movement>();
                Check(!body.isKinematic&&motor.enabled&&session.player.GetComponent<CapsuleCollider>().enabled,"Shore walking restores physical ground movement and collisions");
                var mode=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;
                var input=typeof(BeachBoatStory.Character_Movement).GetField("input",BindingFlags.Instance|BindingFlags.NonPublic);var step=typeof(BeachBoatStory.Character_Movement).GetMethod("FixedUpdate",BindingFlags.Instance|BindingFlags.NonPublic);
                var start=session.player.transform.position;
                try{for(int i=0;i<35;i++){input.SetValue(motor,new Vector2(0,1));step.Invoke(motor,null);Physics.Simulate(.02f);}}
                finally{input.SetValue(motor,Vector2.zero);Physics.simulationMode=mode;}
                Check(QuestRowboat.FlatDistance(start,session.player.transform.position)>8,"Restored ground motor actually moves the player on land");Move(shore);
                Check(game.ToggleSword()&&game.SwordDrawn,"Q draws sword on shore");
                victim=combat.guards.First(g=>g.IsAlive);victim.transform.position=shore+Vector3.forward*17;victim.transform.rotation=Quaternion.identity;Physics.SyncTransforms();
                int hp=victim.Health;Check(game.TrySlash()&&victim.Health==hp-1,"Sword deals real damage from shore");
                Check(CombatDamageNumbers.Instance!=null&&CombatDamageNumbers.Instance.LastDamage==1&&CombatDamageNumbers.Instance.DisplayedCount>0,"Sword hit displays actual lost health as a damage number");
                game.ToggleSword();combat.SetAim(true);Check(combat.IsAiming,"Bow can be aimed on shore");
                Check(combat.TryFire(new Ray(shore+Vector3.up*14,(victim.AimPoint-shore-Vector3.up*14).normalized),.1f),"Bow fires from shore");combat.StepArrows(3);
                Check(victim.Health==hp-2&&CombatDamageNumbers.Instance.DisplayedCount>=2,"Arrow arrival applies damage and displays a second damage number");
                combat.SetAim(false);Check(session.player.GetComponentInChildren<Animator>().runtimeAnimatorController==game.FootController,"Releasing bow aim restores foot animation on shore");
                BachDangSaveSystem.TestDirectory=Path.GetFullPath("Logs/ShoreSaveQA");
                Check(BachDangSaveSystem.Save(10,"QA trên bờ")&&BachDangSaveSystem.TryRead(10,out var data)&&data.boarding.state==4,"Slot ten stores shore movement state");
                Check(BachDangSaveSystem.TryRead(10,out var saved),"Can read shore save");
                var deep=boat.transform.position+Vector3.right*200;deep.y=TideSystem.CurrentWaterHeight-5;Move(deep);game.StepPlayer(0);
                Check(game.State==RiverBoardingCombat.PlayerState.Swimming&&body.isKinematic&&!motor.enabled,"Walking back into deep water resumes swimming");
                BachDangSaveSystem.Apply(saved);Check(game.State==RiverBoardingCombat.PlayerState.Land&&!body.isKinematic&&motor.enabled,"Loading a shore save restores walking, rather than attaching the player to the boat");
                menu.OpenSaveSlots("QA ảnh ô lưu");Capture("Ten_Save_Slots");menu.CancelSlots();
                menu.OpenSaveSlots("QA ảnh ghi đè");menu.ChooseSlot(10);Capture("Save_Overwrite_Confirmation");menu.CancelOverwrite();menu.CancelSlots();
                Finish(null);
            }
        }
        catch(Exception error){Finish(error.ToString());}
    }
    static void Capture(string name)
    {
        var camera=BeachBoatQuestSession.Instance.storyCamera;var menu=BachDangFrontEnd.Instance.GetComponent<Canvas>();
        var rt=new RenderTexture(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGB24,false);var old=RenderTexture.active;
        menu.renderMode=RenderMode.ScreenSpaceCamera;menu.worldCamera=camera;menu.planeDistance=2;camera.targetTexture=rt;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;
        image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("Logs/"+name+".png",image.EncodeToPNG());
        camera.targetTexture=null;RenderTexture.active=old;menu.renderMode=RenderMode.ScreenSpaceOverlay;Object.DestroyImmediate(rt);Object.DestroyImmediate(image);
    }
}
