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
public static class BeachBoatEpiloguePlaytest
{
    const string Flag="Epilogue30QA";static int phase,frame;static double began;static float mark,health;static string failure;static BachDangSaveData save;
    static readonly List<string> checks=new List<string>();static readonly Dictionary<string,string> hashes=new Dictionary<string,string>();
    static string Hash(string p)=>Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(p)));
    static void Check(bool pass,string text){if(!pass)throw new Exception(text);checks.Add(text);}
    public static void RunInEditor(){phase=0;frame=-1;began=0;failure=null;checks.Clear();hashes.Clear();SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
    static void Log(string m,string stack,LogType kind){if(SessionState.GetBool(Flag,false)&&(kind==LogType.Error||kind==LogType.Exception))failure=m+"\n"+stack;}
    static void Finish(string error){SessionState.SetBool(Flag,false);Time.timeScale=1;File.WriteAllText("Logs/EpiloguePlaytest.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=error==null,error,phase,checks},Newtonsoft.Json.Formatting.Indented));BachDangSaveSystem.TestDirectory=null;EditorApplication.isPlaying=false;}
    static void VerifyArena(Vector3 eye,Vector3 look)
    {
        var v=new GameObject("QA_Boss_Bare_Camera").AddComponent<Camera>();v.transform.position=eye;v.transform.LookAt(look);v.clearFlags=CameraClearFlags.SolidColor;v.backgroundColor=Color.cyan;v.farClipPlane=2000;v.nearClipPlane=.1f;v.enabled=false;v.useOcclusionCulling=false;
        var rt=new RenderTexture(1280,800,24);v.targetTexture=rt;v.Render();RenderTexture.active=rt;var t=new Texture2D(1280,800,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,1280,800),0,0);t.Apply();var pixels=t.GetPixels32();var first=pixels[0];Check(pixels.Count(p=>Mathf.Abs(p.r-first.r)+Mathf.Abs(p.g-first.g)+Mathf.Abs(p.b-first.b)>30)>pixels.Length/5,"The actual 3D duel view contains visible deck and fighters rather than an obscuring solid surface");File.WriteAllBytes("Logs/Boss_Bare.png",t.EncodeToPNG());RenderTexture.active=null;Object.DestroyImmediate(t);Object.DestroyImmediate(rt);Object.DestroyImmediate(v.gameObject);
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;EditorApplication.QueuePlayerLoopUpdate();if(frame==Time.frameCount)return;frame=Time.frameCount;
        if(began==0)began=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-began<4)return;
        try
        {
            if(failure!=null)throw new Exception(failure);if(EditorApplication.timeSinceStartup-began>120)throw new Exception("Epilogue QA timed out");
            var session=BeachBoatQuestSession.Instance;var story=Story.Instance;var boss=RiverBossEncounter.Instance;var actor=session.player.transform;var vitals=actor.GetComponent<PlayerVitals>();
            if(phase==0)
            {
                foreach(var p in Directory.GetFiles(Application.persistentDataPath,"campaign938*"))hashes[p]=Hash(p);
                BachDangSaveSystem.TestDirectory=Path.GetFullPath(".utmp/EpilogueQA/"+DateTime.Now.ToString("yyyyMMddHHmmssfff"));BachDangAutoSave.Instance.enabled=false;
                BachDangFrontEnd.Instance.Play();Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
                story.TransitionTo(Story.StoryState.Act2_SupplyPreparation,true);
                foreach(var stall in VillagePreparationQuest.Instance.stalls)
                {
                    actor.position=stall.InteractionPosition;Check(stall.TryOpen(),"Open physical stall "+stall.kind);var work=stall.Physical;
                    for(int i=0;i<8;i++){var p=work.TargetPosition+Quaternion.Euler(0,i*45,0)*Vector3.forward*8;p.y=work.RequiredApproach.y;actor.position=p;Check(work.CanInteract,"Left pickup works close from angle "+i+" at "+stall.kind);}
                    actor.position=work.TargetPosition+Vector3.forward*40;Check(!work.CanInteract,"Far left pickup is rejected at "+stall.kind);stall.Close();
                }
                BeachBoatStory.RiverStakeZone.Instance.RestoreCheckpoint(Enumerable.Repeat(true,5).ToArray());story.TransitionTo(Story.StoryState.Act3_Victory,true);
                foreach(var ship in EnemyFleetDirector.Instance.enemyShips)
                {ship.RestoreSave(new BachDangSaveData.Ship{active=true,destroyed=true,settled=true,released=true,position=ship.targetPoint.position,rotation=Quaternion.Euler(0,180,0),sinkStart=ship.targetPoint.position,sinkRotation=Quaternion.Euler(0,180,0),destroyedTime=6,sinkDepth=20});}
                story.OnVictoryReported();Check(story.currentState==Story.StoryState.Act3_SearchPrisoners&&!CampaignProgress.Instance.Record.finished,"The first victory report starts prisoner search instead of ending the campaign");BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
                actor.position=session.playerSpawn.position;Check(!boss.InspectWreck(0),"A distant wreck cannot be inspected remotely");
                for(int i=0;i<3;i++)
                {
                    var ship=EnemyFleetDirector.Instance.enemyShips[i];actor.position=ship.transform.position-ship.transform.right*(ship.stakeHitDistance+45);Check(boss.InspectWreck(i),"Inspect wreck "+i+" at close range");BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
                    if(i==0){var partial=BachDangSaveSystem.Capture("QA partial search");BachDangSaveSystem.Apply(partial);Check(boss.SearchCount==1&&!boss.InArena,"A partial prisoner search survives save/load");}
                }
                Check(boss.SearchCount==3,"All three searched wrecks unlock the commander reveal");boss.BeginDuel();
                Check(boss.IsFighting&&story.currentState==Story.StoryState.Act3_CommanderDuel&&!CampaignProgress.Instance.Record.finished,"The reveal starts a separate duel without completing the game");
                Check(Vector3.Distance(actor.position,boss.ArenaCenter)<170&&Vector3.Distance(boss.Boss.position,boss.ArenaCenter)<170,"Both fighters stand on the spacious flagship deck");
                Check(BachDangAutoSave.Instance.RetryPoint?.boss?.inArena==true,"The duel writes a temporary retry point without touching manual saves");
                Check(boss.ClampDeck(boss.ArenaCenter+new Vector3(1000,0,1000)).x<=boss.ArenaCenter.x+96,"Deck movement clamps at the solid rail boundary");
                phase=10;return;
            }
            if(phase==10)
            {
                if(BeachBoatQuestSession.InputBlocked)return;
                actor.position=boss.Boss.position-Vector3.forward*25;actor.rotation=Quaternion.identity;int hp=boss.Health;Check(boss.TryPlayerStrike()&&boss.Health==hp-12,"A near facing sword strike removes twelve boss health");
                save=BachDangSaveSystem.Capture("QA mid duel");BachDangSaveSystem.Apply(save);
                Check(boss.IsFighting&&boss.Health==hp-12&&Vector3.Distance(actor.position,save.playerPosition)<1,"Mid-duel load restores boss health and player deck position");
                Check(!actor.GetComponent<BeachBoatStory.Character_Movement>().enabled&&actor.GetComponent<Rigidbody>().isKinematic,"Mid-duel load restores the dedicated 3D combat controller");
                var rig=boss.Boss.GetComponentInChildren<Animator>();BeachBoatReview29Playtest.Picture("Boss_Deck",actor.position-Vector3.forward*55+Vector3.up*35,boss.Boss.position+Vector3.up*12);
                mark=Time.time;phase=1;return;
            }
            if(phase==1)
            {
                if(Time.time-mark<1.8f)return;
                BeachBoatReview29Playtest.Picture("Boss_Deck",actor.position-Vector3.forward*55+Vector3.up*35,boss.Boss.position+Vector3.up*12);
                VerifyArena(actor.position-Vector3.forward*55+Vector3.up*35,boss.Boss.position+Vector3.up*12);
                File.WriteAllText("Logs/BossArenaRenderAudit.json",Newtonsoft.Json.JsonConvert.SerializeObject(new {actor=actor.position.ToString(),boss=boss.Boss.position.ToString(),camera=session.storyCamera.transform.position.ToString(),shell=Object.FindObjectsByType<Renderer>().Where(r=>r.name=="Van_boong_0"||r.name=="Mat_nuoc_duoi_soai_ham"||r.name.Contains("Hull")).Select(r=>new{r.name,b=r.bounds.ToString()})}));
                var target=boss.Boss.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Chest).position;var origin=target-Vector3.forward*100;
                typeof(RiverPlayerCombat).GetMethod("Launch",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(RiverPlayerCombat.Instance,new object[]{origin,target,null,2,false,1.1f});int before=boss.Health;RiverPlayerCombat.Instance.StepArrows(.3f);Check(boss.Health<before,"A physical arrow collider hit damages the boss");
                actor.position=boss.Boss.position+boss.Boss.forward*24;health=vitals.Health;Check(boss.TryDodge(Vector3.right),"A directional dodge consumes stamina and opens its short invulnerability window");boss.StepBoss(.1f);boss.StepBoss(1);boss.StepBoss(.3f);Check(vitals.Health==health,"A timed dodge avoids the committed boss sword strike");
                mark=Time.time;phase=2;return;
            }
            if(phase==2)
            {
                if(Time.time-mark<.5f)return;
                boss.TakeHit(1000,CombatImpactKind.Sword);Check(boss.Defeated&&story.currentState==Story.StoryState.Act3_FinalReport&&!CampaignProgress.Instance.Record.finished,"Defeating the boss requires a final report instead of ending immediately");
                var after=BachDangSaveSystem.Capture("QA defeated duel dialogue");BachDangSaveSystem.Apply(after);Check(boss.Defeated&&boss.InArena,"A save taken during the victory dialogue restores the defeated arena safely");
                BeachBoatStory.StoryDialogueUI.Instance.Close();Check(!boss.InArena&&actor.position.y<1000&&actor.GetComponent<BeachBoatStory.Character_Movement>().enabled,"Leaving the duel returns to the river and restores normal movement");
                story.OnFinalVictoryReported();Check(story.currentState==Story.StoryState.Act3_Celebration&&CampaignProgress.Instance.Record.finished,"Only the final commander report completes the campaign and ranking");
                Check(CampaignProgress.StoryOrder.Length==CampaignProgress.MissionTitles.Length&&CampaignProgress.StoryOrder.Contains(15)&&CampaignProgress.StoryOrder.Contains(16)&&CampaignProgress.StoryOrder.Contains(17),"The journal includes search, duel and final-report missions");
                foreach(var pair in hashes)Check(File.Exists(pair.Key)&&Hash(pair.Key)==pair.Value,"User save unchanged: "+Path.GetFileName(pair.Key));Finish(null);
            }
        }
        catch(Exception e){Finish(e.ToString());}
    }
}
