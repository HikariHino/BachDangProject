using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using BeachBoatStory;
using Object=UnityEngine.Object;
using Story=BeachBoatStory.BachDangStoryManager;

public static class BeachBoatAudioBattlePlaytest
{
    const string Flag="AudioBattle938QA";static int phase,lastFrame=-1;static double began,waitUntil;static string runtimeError;
    static readonly List<string> checks=new List<string>();static EnemyShip sinking;static double sinkMilliseconds;static EnemyDeckGuard hitVictim;
    static readonly Vector3 OpenWater=new Vector3(-2400,0,1400);
    public static void RunInEditor(){phase=0;lastFrame=-1;began=0;waitUntil=0;runtimeError=null;checks.Clear();SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Record;Application.logMessageReceived+=Record;}
    static void Record(string text,string stack,LogType type){if(SessionState.GetBool(Flag,false)&&EditorApplication.isPlaying&&(type==LogType.Error||type==LogType.Exception))runtimeError=text+"\n"+stack;}
    static void Check(bool value,string label){if(!value)throw new Exception(label);checks.Add(label);}
    static void Finish(string failure){Directory.CreateDirectory("Logs");File.WriteAllText("Logs/AudioBattlePlaytest.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=failure==null,error=failure,phase,sinkMilliseconds,checks},Newtonsoft.Json.Formatting.Indented));SessionState.SetBool(Flag,false);Time.timeScale=1;EditorApplication.isPlaying=false;}
    static void Click(string name)
    {
        var button=BachDangFrontEnd.Instance.GetComponentsInChildren<Button>(true).Single(b=>b.name==name&&b.gameObject.activeInHierarchy);
        int before=BachDangMenuAudio.Instance.ClicksPlayed;button.onClick.Invoke();Check(BachDangMenuAudio.Instance.ClicksPlayed==before+1,"One audible click for "+name);
    }
    static void Setup()
    {
        Object.FindAnyObjectByType<BeachBoatStory.OpeningEvent>().SkipOpening();BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
        BeachBoatStory.RiverStakeZone.Instance.RestoreCheckpoint(Enumerable.Repeat(true,5).ToArray());Story.Instance.collectedWood=5;Story.Instance.TransitionTo(Story.StoryState.Act3_WaitForLowTide,true);
        var s=BeachBoatQuestSession.Instance;var boat=QuestRowboat.Instance;s.player.transform.position=boat.boardingPoint.position;Check(boat.TryBoard(),"Board battle fixture");
        var fleet=EnemyFleetDirector.Instance;fleet.StopAllCoroutines();RiverBoardingCombat.Instance.enabled=false;RiverPlayerCombat.Instance.enabled=false;RiverPlayerCombat.Instance.ReturnFireEnabled=false;
        foreach(var ship in fleet.enemyShips)ship.enabled=false;foreach(var friendly in RiverBoardingCombat.Instance.friendlyBoats)friendly.enabled=false;
        float stature=EnemyDeckGuard.RigHeight(s.player.GetComponentInChildren<Animator>());
        Check(RiverBoardingCombat.Instance.friendlyBoats.SelectMany(b=>b.GetComponentsInChildren<Animator>(true)).Where(a=>a.isHuman).All(a=>Mathf.Abs(EnemyDeckGuard.RigHeight(a)/stature-1)<.05f),"Every allied avatar, including observers and rowers, matches player stature");
        boat.transform.position=OpenWater+Vector3.up*TideSystem.CurrentWaterHeight;boat.ResumeRowing();BeachBoatStory.StoryDialogueUI.Instance.DismissForCheckpoint();
    }
    static void WaterCombat()
    {
        var game=RiverBoardingCombat.Instance;var combat=RiverPlayerCombat.Instance;var data=game.CaptureSave();
        foreach(var g in combat.guards)g.DefeatFromVolley();foreach(var a in data.allies){a.health=0;a.active=false;}foreach(var h in data.hostiles)h.active=false;foreach(var d in data.defenders)d.active=false;
        var h0=data.hostiles[0];var guard=combat.guards.First(g=>BachDangSaveSystem.GuardId(g)==h0.id);guard.ship=EnemyFleetDirector.Instance.enemyShips[0];guard.ship.gameObject.SetActive(true);guard.ship.isDestroyed=false;guard.IsIndependentBoarder=true;guard.gameObject.SetActive(true);guard.ResetGuard();
        var origin=OpenWater+Vector3.right*800+Vector3.up*(TideSystem.CurrentWaterHeight-4);
        h0.state=1;h0.active=true;h0.source=guard.ship.name;h0.boat=game.friendlyBoats[0].name;h0.targetPlayer=false;h0.position=origin;h0.fromWreck=true;
        var a0=data.allies[0];a0.health=6;a0.active=true;a0.state=1;a0.boat=null;a0.target=h0.id;a0.position=origin+Vector3.right*16;
        game.RestoreSave(data);Physics.SyncTransforms();
        var attacks=(Dictionary<EnemyDeckGuard,float>)typeof(RiverBoardingCombat).GetField("enemyAttacks",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(game);attacks[guard]=-1;
        game.StepHostiles(.01f);Check(game.CaptureSave().allies[0].health==5,"Enemy swimmer really attacks a nearby allied swimmer");
        game.StepAllies(.01f);Check(guard.Health==1,"Allied swimmer retaliates against the nearby enemy");Check(game.CaptureSave().allies[0].state==1,"Swimmer is not mistaken for a deck to climb");
        int particles=CombatHitFeedback.Instance.ConfirmedHits;Check(particles>=2,"Both factions produce blood only when health changes");
        data=game.CaptureSave();a0=data.allies[0];h0=data.hostiles[0];a0.position=origin+Vector3.right*150;game.RestoreSave(data);attacks[guard]=-1;game.StepEnemies(0);Check(game.CaptureSave().allies[0].health==5,"No phantom ranged chip damage to distant swimming allies");
        a0.position=origin+Vector3.right*16;game.RestoreSave(data);var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="QA solid obstruction";wall.transform.position=origin+new Vector3(8,8,0);wall.transform.localScale=new Vector3(3,30,40);Physics.SyncTransforms();attacks[guard]=-1;
        game.StepHostiles(.01f);Check(game.CaptureSave().allies[0].health==5,"Enemy cannot hit an allied swimmer through solid timber");Object.DestroyImmediate(wall);
        var fx=CombatHitFeedback.Instance;int arrows=fx.ArrowHits,sounds=BeachBoatSoundscape.Instance.CombatHitsPlayed;guard.ResetGuard();guard.Hit(1,true,CombatImpactKind.Arrow);Check(fx.ArrowHits==arrows+1&&BeachBoatSoundscape.Instance.CombatHitsPlayed==sounds+1,"Confirmed arrow hit emits blood and its distinct impact sound");
        Check(BeachBoatSoundscape.Instance.LastCombatImpact.name=="Arrow_Flesh_01","A real arrow hit uses the newly edited YouTube flesh impact");
        guard.Hit(2,true);int count=fx.ConfirmedHits;guard.Hit(1,true);Check(fx.ConfirmedHits==count&&fx.PoolSize==12,"Already dead targets emit no extra blood and the pool remains bounded");
    }
    static void RecordedAudioChecks()
    {
        var sound=BeachBoatSoundscape.Instance;var clips=Resources.LoadAll<AudioClip>("BachDang938/ActionAudio");
        Check(clips.Length==31,"All 31 edited action recordings are imported");
        foreach(var clip in clips)
        {
            Check(clip.frequency==32000&&clip.channels==1&&clip.length>0&&clip.length<1,"Short mono action asset: "+clip.name);
            clip.LoadAudioData();var samples=new float[clip.samples];Check(clip.GetData(samples,0)&&samples.Max(x=>Mathf.Abs(x))>.05f&&samples.All(x=>!float.IsNaN(x)&&Mathf.Abs(x)<.8f),"Recording contains audible unclipped samples: "+clip.name);
        }
        var heard=new HashSet<string>();for(int i=0;i<6;i++){sound.PlayFootstep();heard.Add(sound.LastFootstep.name);}
        Check(heard.Count==6&&heard.All(n=>n.StartsWith("Walk_Grass_")),"Walking rotates through six real grass footsteps");
        for(int i=0;i<6;i++)sound.PlayFootstep(true);Check(sound.LastFootstep.name.StartsWith("Run_Grass_"),"Running uses its separate short recordings");
        heard.Clear();for(int i=0;i<4;i++){sound.PlayOar();heard.Add(sound.LastOar.name);}Check(heard.Count==4&&heard.All(n=>n.StartsWith("Oar_Stroke_")),"Rowing varies four recorded oar strokes");
        heard.Clear();for(int i=0;i<3;i++){sound.PlayBow();heard.Add(sound.LastBow.name);}Check(heard.Count==3&&heard.All(n=>n.StartsWith("Bow_Release_")),"Bow release varies three edited shots");
        int confirmed=CombatHitFeedback.Instance.ConfirmedHits;heard.Clear();for(int i=0;i<5;i++){sound.PlaySwordSwing();heard.Add(sound.LastSwordSwing.name);}Check(heard.Count==5&&heard.All(n=>n.StartsWith("Sword_Swing_"))&&CombatHitFeedback.Instance.ConfirmedHits==confirmed,"Five sword swishes do not produce a false hit or blood");
        int effects=sound.EffectsPlayed;sound.SetMuted(true);sound.PlayOar();sound.PlayBow();sound.PlaySwordSwing();Check(sound.EffectsPlayed==effects&&AudioListener.volume==0,"Mute applies to every new action sound");sound.SetMuted(false);
    }
    static void Sink()
    {
        var game=RiverBoardingCombat.Instance;foreach(var b in game.friendlyBoats)b.Step(.01f);foreach(var h in game.CaptureSave().hostiles){var g=RiverPlayerCombat.Instance.guards.First(x=>BachDangSaveSystem.GuardId(x)==h.id);g.gameObject.SetActive(false);}
        sinking=EnemyFleetDirector.Instance.enemyShips[0];sinking.ClearWreck();sinking.transform.SetPositionAndRotation(OpenWater+Vector3.forward*650+Vector3.up*TideSystem.CurrentWaterHeight,Quaternion.identity);sinking.Activate();sinking.enabled=false;
        foreach(var other in EnemyFleetDirector.Instance.enemyShips)if(other!=sinking)other.gameObject.SetActive(false);
        // Separate the isolated fracture fixture from traffic before examining geometry.
        foreach(var b in game.friendlyBoats)b.gameObject.SetActive(false);
        sinking.PrepareWreck();int deaths=game.ShipwreckDeaths,survivors=game.ShipwreckSurvivors;var watch=System.Diagnostics.Stopwatch.StartNew();sinking.TakeDamage();watch.Stop();sinkMilliseconds=watch.Elapsed.TotalMilliseconds;
        Check(sinking.GetComponent<ShipwreckPresentation>().IsBroken,"Ship switches to prepared, genuinely clipped bow and stern");
        Check(game.ShipwreckDeaths>deaths&&game.ShipwreckSurvivors>survivors,"Sinking creates real casualties and surviving swimmers");
        Check(Object.FindObjectsByType<RiverCorpse>(FindObjectsSortMode.None).All(c=>c.GetComponent<EnemyDeckGuard>().Health==0&&!c.GetComponent<ActorSolidBody>().SolidCollider.enabled),"Floating corpses are dead and do not block the living");
        Check(BeachBoatSoundscape.Instance.WreckSoundsPlayed>0,"Sinking plays timber impact and a human cry");
        Check(sinking.GetComponent<ShipwreckPresentation>().Colliders.All(c=>c.enabled),"Both separated halves retain actual timber collision");
        sinking.Step(3.2f);foreach(var p in sinking.GetComponent<ShipwreckPresentation>().GetComponentsInChildren<ParticleSystem>())p.Simulate(.6f,true,false,true);
        var camera=BeachBoatQuestSession.Instance.storyCamera;camera.GetComponent<BeachBoatStory.Camera_Script>().enabled=false;camera.transform.position=sinking.transform.position+sinking.transform.right*390+Vector3.up*150-sinking.transform.forward*170;camera.transform.LookAt(sinking.transform.position+Vector3.up*30);
        Check(sinking.GetComponent<ShipwreckPresentation>().FireParticles>0,"Small fire and smoke emit on exposed timber");
        var save=sinking.CaptureSave();int sounds=BeachBoatSoundscape.Instance.WreckSoundsPlayed;sinking.RestoreSave(save);Check(sinking.GetComponent<ShipwreckPresentation>().IsBroken&&BeachBoatSoundscape.Instance.WreckSoundsPlayed==sounds,"Loading a sinking ship preserves breakup without replaying the crash");
    }
    static void DeckCombat()
    {
        var session=BeachBoatQuestSession.Instance;var game=RiverBoardingCombat.Instance;var combat=RiverPlayerCombat.Instance;
        var friendly=game.friendlyBoats[0];friendly.RestorePosition(OpenWater+Vector3.right*1300+Vector3.forward*600+Vector3.up*TideSystem.CurrentWaterHeight,Quaternion.identity);
        var hull=BoatHullCollision.Get(friendly.transform);var center=friendly.transform.TransformPoint(hull.SolidHull.center);
        Check(hull.TryDeckSurface(center,out var floor),"Deck fixture has real timber support");
        var data=game.CaptureSave();var h=data.hostiles[0];var a=data.allies[0];var guard=combat.guards.First(g=>BachDangSaveSystem.GuardId(g)==h.id);guard.gameObject.SetActive(true);guard.ResetGuard();guard.IsIndependentBoarder=true;
        h.state=3;h.boat=friendly.name;h.active=true;h.position=floor+Vector3.right*15+Vector3.up*.2f;h.local=friendly.transform.InverseTransformPoint(h.position);
        a.state=3;a.boat=friendly.name;a.active=true;a.health=6;a.position=floor-Vector3.right*10+Vector3.up*.2f;a.local=friendly.transform.InverseTransformPoint(a.position);a.target=h.id;
        game.RestoreSave(data);game.StepHostiles(0);var ally=session.transform.Find(a.id);ally.GetComponentInChildren<Animator>().Update(.2f);Physics.SyncTransforms();var before=ally.position;game.StepAllies(.2f);
        Check(QuestRowboat.FlatDistance(before,ally.position)>1,"Ally advances on the friendly deck despite enemy's original source ship");
        Check(ally.GetComponent<ActorSolidBody>().Carrier==friendly.transform&&game.CaptureSave().allies[0].boat==friendly.name,"Actual allied carrier is retained and serialized");
        var beforeDeck=game.CaptureSave();var playerBefore=session.player.transform.position;
        var onDeck=game.CaptureSave();onDeck.state=(int)RiverBoardingCombat.PlayerState.FriendlyDeck;onDeck.deckShip=friendly.name;onDeck.deckLocal=friendly.transform.InverseTransformPoint(floor);game.RestoreSave(onDeck);session.player.transform.position=floor;
        var sound=BeachBoatSoundscape.Instance;var tick=typeof(BeachBoatSoundscape).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic);tick.Invoke(sound,null);
        int footsteps=sound.FootstepsPlayed;var shipBefore=friendly.transform.position;friendly.transform.position+=Vector3.forward*20;session.player.transform.position+=Vector3.forward*20;tick.Invoke(sound,null);
        Check(sound.FootstepsPlayed==footsteps,"Standing on a moving deck does not produce footsteps");
        session.player.transform.position+=friendly.transform.forward*18;tick.Invoke(sound,null);
        Check(sound.FootstepsPlayed>footsteps&&sound.LastFootstep.name.StartsWith("Deck_Wood_"),"Walking across a deck plays timber footsteps instead of grass");
        friendly.transform.position=shipBefore;session.player.transform.position=playerBefore;game.RestoreSave(beforeDeck);
        // Review the actual sword/arrow impacts on open, walkable shoreline so sails cannot hide them.
        var terrain=Terrain.activeTerrain;var shore=QuestRowboat.Instance.dockPoint.position;bool found=false;
        for(int radius=20;radius<850&&!found;radius+=20)for(int angle=0;angle<360&&!found;angle+=20)
        {
            var at=shore+Quaternion.Euler(0,angle,0)*Vector3.forward*radius;float ground=terrain.SampleHeight(at)+terrain.transform.position.y;
            if(ground<TideSystem.CurrentWaterHeight+5||ground>TideSystem.CurrentWaterHeight+35)continue;
            if(Physics.OverlapSphere(new Vector3(at.x,ground+10,at.z),26).Any(c=>!(c is TerrainCollider)&&c.GetComponent<TideSystem>()==null))continue;
            shore=new Vector3(at.x,ground+.2f,at.z);found=true;
        }
        Check(found,"Find unobstructed real shoreline for impact rendering");
        data=game.CaptureSave();foreach(var v in data.allies){v.active=false;v.health=0;}data.state=(int)RiverBoardingCombat.PlayerState.Swimming;data.deckShip=null;data.sword=false;
        h=data.hostiles[0];h.state=4;h.position=shore+Vector3.forward*15;h.boat=null;game.RestoreSave(data);
        var player=session.player.transform;player.position=shore;player.GetComponent<Rigidbody>().position=shore;player.rotation=Quaternion.identity;
        Check(game.TryWalkAshore(shore)&&game.ToggleSword(),"Real swimming-to-shore path restores a standing sword rig");
        guard.transform.position=shore+Vector3.forward*15;guard.GetComponent<ActorSolidBody>().Carrier=null;guard.ResetGuard();guard.IsIndependentBoarder=true;guard.FitStandingOnDeck();Physics.SyncTransforms();
        int hits=CombatHitFeedback.Instance.SwordHits;Check(game.TrySlash()&&guard.Health==1&&CombatHitFeedback.Instance.SwordHits==hits+1,"Actual player sword hit removes health and emits one blood burst");
        Check(BeachBoatSoundscape.Instance.LastSwordSwing.name.StartsWith("Sword_Swing_"),"Actual player slash triggers the new recorded sword sound");
        var camera=session.storyCamera;camera.GetComponent<BeachBoatStory.Camera_Script>().enabled=false;camera.transform.position=player.position+new Vector3(24,15,-22);camera.transform.LookAt(guard.AimPoint);
        foreach(var particles in CombatHitFeedback.Instance.GetComponentsInChildren<ParticleSystem>())if(particles.particleCount>0)particles.Simulate(.14f,true,false,false);
        Capture("Blood_Hit_Checked");
        var launch=typeof(RiverPlayerCombat).GetMethod("Launch",BindingFlags.Instance|BindingFlags.NonPublic);hits=CombatHitFeedback.Instance.ArrowHits;launch.Invoke(combat,new object[]{player.position+Vector3.up*10,guard.AimPoint,guard,1,false});combat.StepArrows(1);
        hitVictim=guard;
        Check(guard.Health==0&&CombatHitFeedback.Instance.ArrowHits==hits+1,"Actual arrow arrival kills the target with one arrow impact effect");
        Check(CombatHitFeedback.Instance.LivingParticles>0,"Confirmed combat hits leave visible live blood particles");

    }
    static void Capture(string name)
    {
        var camera=BeachBoatQuestSession.Instance.storyCamera;var old=camera.targetTexture;var rt=new RenderTexture(1200,760,24);var image=new Texture2D(1200,760,TextureFormat.RGB24,false);camera.targetTexture=rt;camera.Render();var active=RenderTexture.active;RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1200,760),0,0);image.Apply();Directory.CreateDirectory("Logs");File.WriteAllBytes("Logs/"+name+".png",image.EncodeToPNG());RenderTexture.active=active;camera.targetTexture=old;Object.DestroyImmediate(image);Object.DestroyImmediate(rt);
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;EditorApplication.QueuePlayerLoopUpdate();if(began==0)began=EditorApplication.timeSinceStartup;
        if(EditorApplication.timeSinceStartup-began<5||lastFrame==Time.frameCount||EditorApplication.timeSinceStartup<waitUntil)return;lastFrame=Time.frameCount;
        try
        {
            if(runtimeError!=null)throw new Exception(runtimeError);if(EditorApplication.timeSinceStartup-began>100)throw new Exception("Audio/combat QA timed out");
            switch(phase)
            {
                case 0:
                    Check(Time.timeScale==0&&BachDangMenuAudio.Instance.Music.isPlaying&&BachDangMenuAudio.Instance.EntriesPlayed==1,"Title entry and looping music play while game time is paused");
                    Click("Settings");Click("Back");Click("RankingGuide");CampaignJournalUI.Instance.Close();Check(BachDangMenuAudio.Instance.EntriesPlayed==1,"Navigating title pages does not repeat the startup sound");Click("Play");waitUntil=EditorApplication.timeSinceStartup+1;break;
                case 1:Check(!BachDangMenuAudio.Instance.Music.isPlaying,"Lobby music fades out after starting gameplay");Setup();break;
                case 2:RecordedAudioChecks();WaterCombat();break;
                case 3:DeckCombat();break;
                case 4:
                    if(SessionState.GetBool("AudioBattleInspectDeck",false)){SessionState.SetBool("AudioBattleInspectDeck",false);EditorApplication.isPaused=true;return;}
                    var player=BeachBoatQuestSession.Instance.player;var rig=player.GetComponentInChildren<Animator>();var victimRig=hitVictim.GetComponentInChildren<Animator>();
                    Check(Mathf.Abs(EnemyDeckGuard.RigHeight(victimRig)/EnemyDeckGuard.RigHeight(rig)-1)<.05f,"Enemy stature remains equal after swim, boarding, sword and death transitions");
                    Sink();waitUntil=EditorApplication.timeSinceStartup+.5;break;
                case 5:Capture("Shipwreck_Fire_Checked");Finish(null);return;
            }
            phase++;
        }
        catch(Exception e){Finish(e.ToString());}
    }
}
