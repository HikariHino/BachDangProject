using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BeachBoatStory
{
    /// <summary>Versioned manual saves. A verified temporary file replaces the old save atomically.</summary>
    public static class BachDangSaveSystem
    {
        [Serializable] sealed class Envelope {public string payload,sha256;}
        public static BachDangSaveData PendingLoad;
        public static bool PendingLoadSelectedManual;
#if UNITY_EDITOR
        public static string TestDirectory;
#endif
        public const int SlotCount=10;
        public static int ActiveSlot {get;private set;}=1;
        public static string SlotPath(int slot)
        {
            if(slot<1||slot>SlotCount)throw new ArgumentOutOfRangeException(nameof(slot));
            string directory=Application.persistentDataPath;
#if UNITY_EDITOR
            if(!string.IsNullOrEmpty(TestDirectory))directory=TestDirectory;
#endif
            // Slot one retains the original path, so existing journeys remain available.
            return Path.Combine(directory,slot==1?"campaign938.json":$"campaign938-{slot:00}.json");
        }
        public static string SavePath=>SlotPath(ActiveSlot);
        public static string TemporaryPath=>Path.Combine(Path.GetDirectoryName(SlotPath(1)),"campaign938-auto.json");
        public static bool TryReadTemporary(out BachDangSaveData data)=>ReadFile(TemporaryPath,out data)||ReadFile(TemporaryPath+".bak",out data);
        static long SaveTime(BachDangSaveData data)=>data.savedUtcTicks>0?data.savedUtcTicks:
            DateTime.TryParse(data.savedAt,System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.AssumeLocal,out var time)?time.ToUniversalTime().Ticks:0;
        public static bool TryReadResume(out BachDangSaveData data,out int slot)
        {
            data=null;slot=0;
            for(int i=1;i<=SlotCount;i++)if(TryRead(i,out var candidate)&&(data==null||SaveTime(candidate)>SaveTime(data))){data=candidate;slot=i;}
            if(TryReadTemporary(out var temporary)&&(data==null||SaveTime(temporary)>SaveTime(data))){data=temporary;slot=0;}
            return data!=null;
        }
        public static bool HasData(int slot)=>File.Exists(SlotPath(slot))||File.Exists(SlotPath(slot)+".bak");
        public static void SelectSlot(int slot){SlotPath(slot);ActiveSlot=slot;}
        public static bool TryRead(int slot,out BachDangSaveData data)
        {
            LastError="";string path=SlotPath(slot);
            if(ReadFile(path,out data))return true;
            if(ReadFile(path+".bak",out data)){LastError="Đang dùng bản lưu dự phòng gần nhất.";return true;}
            LastError=HasData(slot)?"Ô lưu bị lỗi. Tiến trình hiện tại chưa bị thay đổi.":"Ô lưu trống.";return false;
        }
        public static bool TryReadLatest(out BachDangSaveData data)
        {
            data=null;
            for(int slot=1;slot<=SlotCount;slot++)
                if(TryRead(slot,out var candidate)&&(data==null||string.CompareOrdinal(candidate.savedAt,data.savedAt)>0))data=candidate;
            return data!=null;
        }
        public static string LastError {get;private set;}
        public static string GuardId(EnemyDeckGuard guard)=>guard.name.StartsWith("Linh_Nam_Han_Nhay_Tau_",StringComparison.Ordinal)?"pool/"+guard.name:guard.ship.name+"/"+guard.name;
        static string Digest(string value){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(value)));}
        static bool Valid(BachDangSaveData data)
        {
            bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
            return data!=null&&data.version==1&&data.scene=="beachBoat"&&Enum.IsDefined(typeof(BachDangStoryManager.StoryState),data.storyState)&&
                Finite(data.playerPosition.x)&&Finite(data.playerPosition.y)&&Finite(data.playerPosition.z)&&
                Finite(data.health)&&data.health>0&&Finite(data.maxHealth)&&data.maxHealth>0&&data.woods!=null&&data.stakes!=null&&data.stallLevels!=null&&
                (data.rowboatDurabilityVersion==0||(Finite(data.boatDurability)&&data.boatDurability>0&&data.boatDurability<=QuestRowboat.MaximumDurability));
        }
        static bool ReadFile(string path,out BachDangSaveData data)
        {
            data=null;if(!File.Exists(path))return false;
            try
            {
                var info=new FileInfo(path);if(info.Length>4*1024*1024)return false;
                var envelope=JsonUtility.FromJson<Envelope>(File.ReadAllText(path,Encoding.UTF8));
                if(envelope==null||string.IsNullOrEmpty(envelope.payload)||envelope.sha256!=Digest(envelope.payload))return false;
                data=JsonUtility.FromJson<BachDangSaveData>(envelope.payload);return Valid(data);
            }
            catch(Exception e) when(e is IOException||e is UnauthorizedAccessException||e is ArgumentException){return false;}
        }
        public static bool TryRead(out BachDangSaveData data)=>TryRead(ActiveSlot,out data);
        public static bool Save(int slot,string location)
        {
            int previous=ActiveSlot;SelectSlot(slot);
            if(Save(location))return true;
            ActiveSlot=previous;return false;
        }
        public static bool CanSave=>CampRecoveryGame.Active==null&&ArcheryTraining.Active==null&&!RiverInvasionIntro.IsRunning&&StakeConstruction.Active==null&&VillageWorkStall.Active==null&&BeachBoatQuestSession.Instance!=null&&BeachBoatQuestSession.Instance.storyMode&&
            !(QuestCheckpoint.Instance?.IsDefeated??false)&&!(QuestRowboat.Instance?.IsBroken??false)&&!(ScreenFader.Instance?.IsTransitioning??false);
        static bool WriteData(string path,BachDangSaveData data)
        {
            LastError="";
            try
            {
                if(!Valid(data))throw new InvalidDataException("Invalid campaign state");
                var payload=JsonUtility.ToJson(data);var envelope=new Envelope{payload=payload,sha256=Digest(payload)};
                Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+".tmp";
                File.WriteAllText(temp,JsonUtility.ToJson(envelope),new UTF8Encoding(false));
                if(!ReadFile(temp,out _))throw new InvalidDataException("Save verification failed");
                if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);
                return true;
            }
            catch(Exception e) when(e is IOException||e is UnauthorizedAccessException||e is ArgumentException)
            {LastError="Không thể lưu: "+e.Message;return false;}
        }
        public static bool SaveTemporary(BachDangSaveData data)=>CanSave&&WriteData(TemporaryPath,data);
        public static bool Save(string location)
        {
            LastError="";
            if(!CanSave){LastError="Hãy kết thúc thao tác đang làm trước khi lưu.";return false;}
            try
            {
                var data=Capture(location);if(!WriteData(SavePath,data))return false;
                BachDangAutoSave.Instance?.AcceptSavedPoint(data);
                QuestCheckpoint.Instance?.Capture((BachDangStoryManager.StoryState)data.storyState);
                BeachBoatSoundscape.Instance?.PlayResult(true);return true;
            }
            catch(Exception e) when(e is IOException||e is UnauthorizedAccessException||e is ArgumentException)
            {LastError="Không thể lưu: "+e.Message;return false;}
        }
        public static BachDangSaveData Capture(string location)
        {
            var session=BeachBoatQuestSession.Instance;var story=BachDangStoryManager.Instance;var vitals=session.player.GetComponent<PlayerVitals>();var boat=QuestRowboat.Instance;
            return new BachDangSaveData{
                savedAt=DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),savedUtcTicks=DateTime.UtcNow.Ticks,location=location,storyState=(int)story.currentState,woodCount=story.collectedWood,destroyedCount=story.destroyedEnemyShips,
                rowboatDurabilityVersion=1,boatDurability=boat!=null?boat.Durability:QuestRowboat.MaximumDurability,
                archeryBestScore=ArcheryTraining.Instance?.BestScore??0,archeryTrainingCompleted=ArcheryTraining.Instance?.Completed??false,
                playerPosition=session.player.transform.position,playerRotation=session.player.transform.rotation,aboard=boat!=null&&boat.IsAboard,
                boatPosition=boat!=null?boat.transform.position:Vector3.zero,boatRotation=boat!=null?boat.transform.rotation:Quaternion.identity,
                health=vitals.Health,stamina=vitals.Stamina,maxHealth=vitals.maxHealth,maxStamina=vitals.maxStamina,armor=vitals.ArmorCharges,trainingLevels=vitals.CompletedTrainingLevels,healingItemsVersion=1,healingItems=vitals.HealingItems,lureBriefed=story.LureBriefed,
                shieldEquipped=PlayerShield.Instance?.Equipped??false,constructionSites=RiverConstructionCamp.Ensure(RiverStakeZone.Instance).Capture(),stakeSamplesVersion=1,stakeSamplePositions=RiverStakeZone.Instance?.CaptureSamplePositions(),stakeSampleRotations=RiverStakeZone.Instance?.CaptureSampleRotations(),
                woods=session.woodItems.Select(w=>w!=null&&w.IsCollected).ToArray(),stakes=RiverStakeZone.Instance?.CaptureCheckpoint()??Array.Empty<bool>(),
                woodIds=session.woodItems.Select(w=>w.name).ToArray(),woodPositions=session.woodItems.Select(w=>w.transform.position).ToArray(),woodRotations=session.woodItems.Select(w=>w.transform.rotation).ToArray(),
                stallLevels=VillagePreparationQuest.Instance.stalls.Select(s=>s.CompletedLevels).ToArray(),
                activities=UnityEngine.Object.FindObjectsByType<BeachBoatMinigameStation>(FindObjectsSortMode.None).Select(s=>new BachDangSaveData.Activity{name=s.name,completed=s.HasCompleted,bestTime=s.BestTime,bestMistakes=s.BestMistakes}).ToArray(),
                tide=TideSystem.Instance?.CaptureSave(),fleet=EnemyFleetDirector.Instance?.CaptureSave(),
                ships=EnemyFleetDirector.Instance.enemyShips.Select(s=>s.CaptureSave()).ToArray(),
                guards=RiverPlayerCombat.Instance.guards.Select(g=>new BachDangSaveData.Guard{id=GuardId(g),ship=g.ship.name,health=g.Health,active=g.gameObject.activeSelf,independent=g.IsIndependentBoarder,floatingCorpse=g.GetComponent<RiverCorpse>()!=null&&g.GetComponent<RiverCorpse>().enabled,position=g.transform.position,rotation=g.transform.rotation,anchored=!g.IsIndependentBoarder,localPosition=g.ship.transform.InverseTransformPoint(g.transform.position),localRotation=Quaternion.Inverse(g.ship.transform.rotation)*g.transform.rotation}).ToArray(),
                boarding=RiverBoardingCombat.Instance?.CaptureSave(),dialogue=StoryDialogueUI.Instance?.CaptureSave(),
                campaign=CampaignProgress.Instance?.Capture(),
                patrols=UnityEngine.Object.FindObjectsByType<AmbientRiverBoat>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(p=>new BachDangSaveData.Patrol{name=p.name,position=p.transform.position,rotation=p.transform.rotation,layoutVersion=AmbientRiverBoat.PatrolLayoutVersion}).ToArray()
            };
        }
        public static void Apply(BachDangSaveData data)
        {
            if(!Valid(data))throw new InvalidDataException("Unsupported campaign save");
            var session=BeachBoatQuestSession.Instance;var story=BachDangStoryManager.Instance;var boat=QuestRowboat.Instance;
            QuestCheckpoint.Instance?.PrepareForLoad();
            RiverBoardingCombat.Instance?.EndFreeWater();
            CampaignJournalUI.Instance?.Close();CampRecoveryGame.Active?.Close();VillageWorkStall.Active?.Close();BeachBoatMinigameStation.Active?.CancelActivity();ArcheryTraining.Instance?.Restore(data.archeryBestScore,data.archeryTrainingCompleted);
            BachDangFrontEnd.Instance?.ReleaseHiddenUI();
            foreach(var intro in UnityEngine.Object.FindObjectsByType<OpeningEvent>(FindObjectsInactive.Include,FindObjectsSortMode.None))intro.SkipOpening();
            StoryDialogueUI.Instance?.DismissForCheckpoint();EnemyFleetDirector.Instance.ResetForCheckpoint();
            EnemyFleetDirector.Instance.GetComponent<RiverShoreArchers>()?.ResetAmbush();
            VillageWoodHunt.Instance.Restore(data);
            RiverConstructionCamp.Ensure(RiverStakeZone.Instance).ResetCamp();
            RiverStakeZone.Instance.RestoreCheckpoint(data.stakes);
            RiverStakeZone.Instance.RestoreSamples(data);
            story.collectedWood=data.woodCount;story.destroyedEnemyShips=data.destroyedCount;story.RestoreLureBriefing(data.lureBriefed||data.storyState==10||data.storyState==11||data.storyState==14);
            story.TransitionTo((BachDangStoryManager.StoryState)data.storyState,true);
            var stalls=VillagePreparationQuest.Instance.stalls;for(int i=0;i<stalls.Length;i++)stalls[i].RestoreLearningProgress(i<data.stallLevels.Length?data.stallLevels[i]:0);
            foreach(var activity in data.activities??Array.Empty<BachDangSaveData.Activity>())
                foreach(var station in UnityEngine.Object.FindObjectsByType<BeachBoatMinigameStation>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(station.name==activity.name)station.RestoreSave(activity);
            if(data.tide!=null){BachDangBattleTimeline.Instance.RestoreSave(data.tide);TideSystem.Instance.RestoreSave(data.tide);}
            BachDangBattleTimeline.Instance.ReconcileAfterLoad((BachDangStoryManager.StoryState)data.storyState);
            RiverConstructionCamp.Ensure(RiverStakeZone.Instance).Restore(data);
            foreach(var patrol in UnityEngine.Object.FindObjectsByType<AmbientRiverBoat>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {var saved=(data.patrols??Array.Empty<BachDangSaveData.Patrol>()).FirstOrDefault(p=>p.name==patrol.name);if(saved!=null)patrol.RestorePatrol(saved);}
            boat.RestoreDurability(data.rowboatDurabilityVersion==0?QuestRowboat.MaximumDurability:data.boatDurability);
            boat.RestoreSave(data.aboard,data.boatPosition,data.boatRotation);
            foreach(var saved in data.ships??Array.Empty<BachDangSaveData.Ship>()){var ship=EnemyFleetDirector.Instance.enemyShips.FirstOrDefault(s=>s.name==saved.name);ship?.RestoreSave(saved);}
            foreach(var saved in data.guards??Array.Empty<BachDangSaveData.Guard>())
            {var guard=RiverPlayerCombat.Instance.guards.FirstOrDefault(g=>GuardId(g)==saved.id);guard?.RestoreSave(saved);}
            // Restore traversal after guards: their ResetGuard/RestoreDeck calls must
            // not replace an already-restored swimming, boarding or sword pose.
            RiverBoardingCombat.Instance.RestoreSave(data.boarding);
            if(data.fleet!=null){RiverPlayerCombat.Instance.RestoreSave(data.fleet);EnemyFleetDirector.Instance.RestoreSave(data.fleet);}
            var player=session.player;var body=player.GetComponent<Rigidbody>();player.transform.SetPositionAndRotation(data.playerPosition,data.playerRotation);body.position=data.playerPosition;body.rotation=data.playerRotation;
            if(!body.isKinematic){body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;}
            player.GetComponent<PlayerVitals>().RestoreSave(data);
            PlayerShield.Instance?.Restore(data.shieldEquipped);
            RiverBoardingCombat.Instance.ReconcileAfterLoad(data);Physics.SyncTransforms();
            StoryDialogueUI.Instance.RestoreSave(data.dialogue);
            session.storyCamera.GetComponent<Camera_Script>().SnapToTarget();BachDangPreferences.Apply();
            QuestCheckpoint.Instance.Capture((BachDangStoryManager.StoryState)data.storyState);story.UpdateUI();
            CampaignProgress.Instance?.Restore(data.campaign,data.storyState);
        }
        public static string Chapter(int state)
        {
            var s=(BachDangStoryManager.StoryState)state;
            if(s==BachDangStoryManager.StoryState.Act1_ExploreCamp||s==BachDangStoryManager.StoryState.Act1_Training||s==BachDangStoryManager.StoryState.Act1_GoToCampGate)return "HỒI I • DOANH TRẠI";
            if(s==BachDangStoryManager.StoryState.Act3_Celebration||s==BachDangStoryManager.StoryState.Act3_Victory)return "ĐẠI THẮNG BẠCH ĐẰNG";
            return state>=8&&state<=10?"HỒI III • THỦY CHIẾN":"HỒI II • TRẬN ĐỊA CỌC";
        }
    }
}
