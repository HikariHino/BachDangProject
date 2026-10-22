using System.Collections;
using UnityEngine;
namespace BeachBoatStory
{
    public sealed class EnemyFleetDirector : MonoBehaviour
    {
        public static EnemyFleetDirector Instance {get;private set;}
        public EnemyShip[] enemyShips=new EnemyShip[0];
        public Transform[] spawnPoints=new Transform[0];
        public float spawnDistance=160,waveDelay=8;
        public int shipsPerWave=2;
        public float commandRange=650f;
        public bool BattleStarted {get;private set;}
        public bool IsShowingBattle {get;private set;}
        public string BattleCaption {get;private set;}
        public EnemyShip WatchedShip {get;private set;}
        public int CommandScore {get;private set;}
        public bool EbbStarted {get;private set;}
        public EnemyShip EbbTriggerShip {get;private set;}
        Coroutine battleLoop;
        Coroutine tideLoop;
        Camera battleCamera;
        Camera_Script playerCamera;
        bool savedCameraEnabled;
        float savedFieldOfView;
        Vector3 savedCameraPosition, shotPosition, shotFocus;
        Quaternion savedCameraRotation;
        CursorLockMode savedCursorLock;
        bool savedCursorVisible;
        float commandMessageUntil;
        int watchedFleetIndex;
        readonly System.Collections.Generic.HashSet<int> scoredWaves = new System.Collections.Generic.HashSet<int>();
        RiverBattleChoreography choreography;
        public int ShipCount
        {
            get
            {
                if(enemyShips==null)return 0;
                int count=0;
                for(int i=0;i<enemyShips.Length;i++)
                {
                    if(enemyShips[i]==null)continue;
                    bool duplicate=false;
                    for(int j=0;j<i;j++)if(enemyShips[j]==enemyShips[i]){duplicate=true;break;}
                    if(!duplicate)count++;
                }
                return count;
            }
        }
        public bool IsConfigured=>isActiveAndEnabled&&ShipCount>0;
        public int CommandOpportunityCount=>Mathf.CeilToInt(ShipCount/(float)Mathf.Max(1,shipsPerWave));
        public int PendingCommandWave
        {
            get
            {
                for(int i=0;i<enemyShips.Length;i++)if(enemyShips[i]!=null&&(enemyShips[i].SignalReady||enemyShips[i].gameObject.activeInHierarchy)&&!scoredWaves.Contains(i/Mathf.Max(1,shipsPerWave)))return i/Mathf.Max(1,shipsPerWave);
                return -1;
            }
        }
        public Transform CommandTarget
        {
            get
            {
                int wave=PendingCommandWave;if(wave<0)return null;
                for(int i=wave*Mathf.Max(1,shipsPerWave);i<Mathf.Min((wave+1)*Mathf.Max(1,shipsPerWave),enemyShips.Length);i++)
                    if(enemyShips[i]!=null&&enemyShips[i].SignalReady)return enemyShips[i].WreckTarget!=null?enemyShips[i].WreckTarget:enemyShips[i].transform;
                return null;
            }
        }
        void Awake(){Instance=this;}
        void OnDestroy(){if(Instance==this)Instance=null;}
        void OnDisable(){StopBattle();}
        public void StartBattle()
        {
            if(BattleStarted||!IsConfigured)return;
            var zone=RiverStakeZone.Instance;
            if(zone==null||!zone.IsComplete)return;
            BattleStarted=true;
            CommandScore=0;
            EbbStarted=false;EbbTriggerShip=null;
            scoredWaves.Clear();
            RiverPlayerCombat.Instance?.BeginBattle();
            foreach(var ship in enemyShips)if(ship!=null)ship.gameObject.SetActive(false);
            BachDangBattleTimeline.Instance?.BeginLure();
            BeginBattleView();
            battleLoop=StartCoroutine(SpawnWaves());
        }
        IEnumerator SpawnWaves()
        {
            yield return new WaitForSeconds(2);
            var seen=new System.Collections.Generic.HashSet<EnemyShip>();
            var fleet=new System.Collections.Generic.List<EnemyShip>();
            foreach(var candidate in enemyShips)
                if(candidate!=null&&seen.Add(candidate))fleet.Add(candidate);
            int waveSize=Mathf.Max(1,shipsPerWave);
            tideLoop=StartCoroutine(WatchStakeField(fleet));
            // Waves enter from the authored northern branch while the water still conceals the field.
            for(int waveStart=0;waveStart<fleet.Count;waveStart+=waveSize)
            {
                int waveEnd=Mathf.Min(waveStart+waveSize,fleet.Count);
                for(int i=waveStart;i<waveEnd;i++)
                {
                    var ship=fleet[i];
                    var spawn=(spawnPoints!=null&&i<spawnPoints.Length&&spawnPoints[i]!=null)?spawnPoints[i].position:ship.spawnPoint;
                    spawn.y=TideSystem.CurrentWaterHeight;
                    ship.transform.position=spawn;ship.Activate();
                }
                watchedFleetIndex=waveStart;
                if(IsShowingBattle)WatchShip(fleet[watchedFleetIndex]);
                BattleCaption=$"Đợt {waveStart/waveSize+1}/{CommandOpportunityCount}: thuyền địch từ nhánh sông phía bắc tiến xuống";
                // Give the player time to see each invasion group enter the channel.
                // The old two-second cap spawned all ten ships almost at once, so the battle
                // could finish off-screen before the player understood what was happening.
                if(waveEnd<fleet.Count)yield return new WaitForSeconds(Mathf.Clamp(waveDelay,3.5f,8f));
            }

            watchedFleetIndex=0;
            if(IsShowingBattle&&fleet.Count>0)WatchShip(fleet[0]);

            while(BattleStarted)
            {
                bool anyActive=false;
                for(int i=0;i<fleet.Count;i++)
                {
                    if(!fleet[i].gameObject.activeSelf)continue;
                    anyActive=true;
                    if(WatchedShip==null||!WatchedShip.gameObject.activeSelf)
                    {watchedFleetIndex=i;WatchShip(fleet[i]);}
                }
                // Sinking is automatic; surviving archers remain on broken deck rafts.
                // Keep controls and objective alive until all five counterattacks are ordered.
                if(!anyActive&&CommandScore>=CommandOpportunityCount){BachDangStoryManager.Instance?.OnBattleVictory();break;}
                if(IsShowingBattle&&WatchedShip!=null&&Time.unscaledTime>=commandMessageUntil)
                {
                    int ready=ReadyWave(fleet,waveSize);
                    var boat=QuestRowboat.Instance;
                    var readyShip=ReadyShip(fleet,ready,waveSize);
                    float range=boat!=null&&readyShip!=null?QuestRowboat.FlatDistance(boat.transform.position,readyShip.transform.position):float.PositiveInfinity;
                    bool suppressed=RiverPlayerCombat.Instance==null||RiverPlayerCombat.Instance.HasSuppressedWave(ready);
                    var timeline=BachDangBattleTimeline.Instance;
                    BattleCaption=!EbbStarted ? "Giữ kín mai phục — nhử thuyền địch tới bãi cọc chìm" :
                        timeline!=null&&timeline.CurrentMoment<BachDangBattleTimeline.BattleMoment.Ambush
                        ? "Nước đang rút dần — quan sát đầu cọc, chưa phất cờ sớm" : ready<0
                        ? $"Thuyền tự mắc cọc khi nước rút • Đã đắm {BachDangStoryManager.Instance?.destroyedEnemyShips ?? 0}/{ShipCount}"
                        : !suppressed ? $"HẠ XẠ THỦ ĐỢT {ready+1}: chuột phải ngắm, giữ/thả chuột trái bắn"
                        : range>commandRange
                            ? $"Chèo tới gần thuyền địch đợt {ready+1} để truyền lệnh • Còn {WorldDistance.Format(range)}"
                            : $"[SPACE] PHẤT CỜ ĐỢT {ready+1}!  Quân ta đánh dứt điểm xạ thủ còn lại";
                }
                yield return null;
            }
            battleLoop=null;
            BattleStarted=false;
            choreography?.EndBattle();
            EndBattleView();
        }
        IEnumerator WatchStakeField(System.Collections.Generic.List<EnemyShip> fleet)
        {
            while(BattleStarted&&EbbTriggerShip==null)
            {
                foreach(var ship in fleet)
                    if(ship!=null&&ship.gameObject.activeInHierarchy&&!ship.isDestroyed&&ship.IsHullInsideStakeField)
                    {EbbTriggerShip=ship;break;}
                if(EbbTriggerShip==null)yield return null;
            }
            if(!BattleStarted)yield break;
            var timeline=BachDangBattleTimeline.Instance;
            timeline?.MarkFleetInside();timeline?.BeginEbb();EbbStarted=true;
            BachDangStoryManager.Instance?.UpdateUI();
            // Counterattack becomes available as tips emerge, while the ebb is still in progress.
            while(BattleStarted&&RiverStakeZone.Instance!=null&&RiverStakeZone.Instance.ExposedStakeCount==0)yield return null;
            if(!BattleStarted)yield break;
            timeline?.BeginAmbush();
            choreography=GetComponent<RiverBattleChoreography>();choreography?.BeginAmbush(fleet);
            BachDangStoryManager.Instance?.UpdateUI();tideLoop=null;
        }
        int ReadyWave(System.Collections.Generic.List<EnemyShip> fleet,int waveSize)
        {
            if(BachDangBattleTimeline.Instance!=null&&
               BachDangBattleTimeline.Instance.CurrentMoment<BachDangBattleTimeline.BattleMoment.Ambush)return -1;
            for(int i=0;i<fleet.Count;i++)
                if(fleet[i]!=null&&fleet[i].SignalReady&&!scoredWaves.Contains(i/waveSize))return i/waveSize;
            return -1;
        }
        static EnemyShip ReadyShip(System.Collections.Generic.List<EnemyShip> fleet,int wave,int waveSize)
        {
            if(wave<0)return null;
            for(int i=wave*waveSize;i<Mathf.Min((wave+1)*waveSize,fleet.Count);i++)
                if(fleet[i]!=null&&fleet[i].SignalReady)return fleet[i];
            return null;
        }
        void BeginBattleView()
        {
            var session=BeachBoatQuestSession.Instance;
            if(session==null||!session.storyMode||session.storyCamera==null)return;
            battleCamera=session.storyCamera;playerCamera=battleCamera.GetComponent<Camera_Script>();
            savedFieldOfView=battleCamera.fieldOfView;
            savedCameraPosition=battleCamera.transform.position;savedCameraRotation=battleCamera.transform.rotation;
            savedCursorLock=Cursor.lockState;savedCursorVisible=Cursor.visible;
            savedCameraEnabled=playerCamera!=null&&playerCamera.enabled;
            // Keep the player's orbit camera active. The soldier can look around the battlefield
            // while rowing instead of being locked to a scripted side shot of an enemy ship.
            if(playerCamera!=null)playerCamera.enabled=true;
            QuestRowboat.Instance?.UseSoldierBattleCamera();
            Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;
            BattleCaption="Quân Nam Hán tiến vào bãi cọc...";
            IsShowingBattle=true;
        }
        void WatchShip(EnemyShip ship)
        {
            WatchedShip=ship;
        }
        void LateUpdate()
        {
            if(!IsShowingBattle)return;
            if(BeachBoatQuestSession.Instance==null||!BeachBoatQuestSession.Instance.storyMode)
            {EndBattleView();return;}
            if(Input.GetKeyDown(KeyCode.Space)) TrySignalAttack();
        }
        public bool TrySignalAttack()
        {
            if(!IsShowingBattle||!BattleStarted)return false;
            {
                int waveSize=Mathf.Max(1,shipsPerWave);
                var fleet=new System.Collections.Generic.List<EnemyShip>();
                var seen=new System.Collections.Generic.HashSet<EnemyShip>();
                foreach(var ship in enemyShips)if(ship!=null&&seen.Add(ship))fleet.Add(ship);
                int wave=ReadyWave(fleet,waveSize);
                if(wave>=0)
                {
                    if(RiverPlayerCombat.Instance!=null&&!RiverPlayerCombat.Instance.HasSuppressedWave(wave))
                    {commandMessageUntil=Time.unscaledTime+1.5f;BattleCaption=$"Hạ ít nhất một xạ thủ đợt {wave+1} để quân ta áp sát phản công";return false;}
                    var boat=QuestRowboat.Instance;
                    var readyShip=ReadyShip(fleet,wave,waveSize);
                    if(boat==null||!boat.IsAboard||readyShip==null||
                       QuestRowboat.FlatDistance(boat.transform.position,readyShip.transform.position)>commandRange)
                    {
                        commandMessageUntil=Time.unscaledTime+1f;
                        BattleCaption="Chèo thuyền tới gần bãi cọc để truyền lệnh cho quân mai phục";
                        return false;
                    }
                    scoredWaves.Add(wave);CommandScore++;commandMessageUntil=Time.unscaledTime+1.3f;
                    var released=new System.Collections.Generic.List<EnemyShip>();
                    for(int i=wave*waveSize;i<Mathf.Min((wave+1)*waveSize,fleet.Count);i++)
                    {fleet[i].ReleaseForAmbush();released.Add(fleet[i]);}
                    choreography=GetComponent<RiverBattleChoreography>();
                    choreography?.FireSignalVolley(released);
                    BeachBoatSoundscape.Instance?.PlayDrum(1);
                    var combat=RiverPlayerCombat.Instance;
                    if(combat!=null)foreach(var guard in combat.guards)if(guard!=null&&guard.waveIndex==wave)guard.DefeatFromVolley();
                    BattleCaption=$"HẠ LỆNH PHẢN CÔNG!  {CommandScore}/{CommandOpportunityCount} đợt";
                    BachDangStoryManager.Instance?.UpdateUI();
                }
                else
                {
                    commandMessageUntil=Time.unscaledTime+.8f;
                    BattleCaption="Chưa đến lúc: chờ nước rút và thuyền địch vào tầm cọc";
                }
                return wave>=0;
            }
        }
        public void EndBattleView()
        {
            if(!IsShowingBattle)return;
            IsShowingBattle=false;WatchedShip=null;BattleCaption=null;
            if(battleCamera!=null)
            {
                battleCamera.fieldOfView=savedFieldOfView;
            }
            if(playerCamera!=null)playerCamera.enabled=true;
            Cursor.lockState=savedCursorLock;Cursor.visible=savedCursorVisible;
        }
        public void StopBattle()
        {
            choreography?.EndBattle();
            EndBattleView();
            if(battleLoop!=null){StopCoroutine(battleLoop);battleLoop=null;}
            if(tideLoop!=null){StopCoroutine(tideLoop);tideLoop=null;}
            BattleStarted=false;
            foreach(var ship in enemyShips??System.Array.Empty<EnemyShip>())if(ship!=null){ship.ClearWreck();ship.gameObject.SetActive(false);}
        }
        public void ResetForCheckpoint()
        {
            StopBattle();CommandScore=0;scoredWaves.Clear();watchedFleetIndex=0;commandMessageUntil=0;EbbStarted=false;EbbTriggerShip=null;
            RiverPlayerCombat.Instance?.ResetForCheckpoint();
            foreach(var ship in enemyShips??System.Array.Empty<EnemyShip>())if(ship!=null)ship.ResetForCheckpoint();
        }
    }
}
