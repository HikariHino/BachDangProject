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
        public int RemainingEnemies=>RiverPlayerCombat.Instance!=null?RiverPlayerCombat.Instance.RemainingEnemies:0;
        public int ShipsInside {get{int count=0;foreach(var ship in enemyShips)if(ship!=null&&ship.gameObject.activeInHierarchy&&ship.IsHullInsideStakeField)count++;return count;}}
        public bool AllShipsSunk {get{foreach(var ship in enemyShips)if(ship!=null&&!ship.HasSettled)return false;return ShipCount>0;}}
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
        void Start()=>RiverFleetLayout.Configure(this);
        void OnDestroy(){if(Instance==this)Instance=null;}
        void OnDisable(){StopBattle();}
        public void StartBattle()
        {
            if(BattleStarted||!IsConfigured)return;
            var zone=RiverStakeZone.Instance;
            if(zone==null||!zone.IsComplete)return;
            RiverFleetLayout.Configure(this);
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
        public BachDangSaveData.Fleet CaptureSave()
        {
            var data=new BachDangSaveData.Fleet{active=BattleStarted,ebb=EbbStarted,commands=CommandScore,scored=new System.Collections.Generic.List<int>(scoredWaves).ToArray()};
            RiverPlayerCombat.Instance?.FillSave(data);return data;
        }
        public void RestoreSave(BachDangSaveData.Fleet data)
        {
            BattleStarted=data.active;EbbStarted=data.ebb;CommandScore=data.commands;scoredWaves.Clear();
            foreach(int wave in data.scored??System.Array.Empty<int>())scoredWaves.Add(wave);
            if(BattleStarted){BeginBattleView();battleLoop=StartCoroutine(SpawnWaves(true));}
        }
        IEnumerator SpawnWaves(bool resume=false)
        {
            if(!resume)yield return new WaitForSeconds(2);
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
                    if(resume&&(ship.gameObject.activeSelf||ship.isDestroyed))continue;
                    var spawn=(spawnPoints!=null&&i<spawnPoints.Length&&spawnPoints[i]!=null)?spawnPoints[i].position:ship.spawnPoint;
                    spawn.y=TideSystem.CurrentWaterHeight;
                    var approach=ship.targetPoint.position-spawn;
                    foreach(var waypoint in ship.routeWaypoints)if(waypoint!=null&&QuestRowboat.FlatDistance(waypoint.position,spawn)>9){approach=waypoint.position-spawn;break;}
                    approach.y=0;var heading=approach.sqrMagnitude>.01f?Quaternion.LookRotation(approach):Quaternion.identity;
                    while(BattleStarted&&!BoatHullCollision.Get(ship.transform).CanOccupy(spawn,heading,false))yield return new WaitForSeconds(.5f);
                    if(!BattleStarted)yield break;
                    ship.transform.position=spawn;ship.Activate();
                    // Release vessels separately so long hulls can clear crossing
                    // northern approaches before the next vessel turns into them.
                    if(!resume&&i+1<waveEnd)yield return new WaitForSeconds(6);
                }
                watchedFleetIndex=waveStart;
                if(IsShowingBattle)WatchShip(fleet[watchedFleetIndex]);
                BattleCaption=$"Đợt {waveStart/waveSize+1}/{CommandOpportunityCount}: thuyền địch từ nhánh sông phía bắc tiến xuống";
                // Give the player time to see each invasion group enter the channel.
                // The old two-second cap spawned all ten ships almost at once, so the battle
                // could finish off-screen before the player understood what was happening.
                if(waveEnd<fleet.Count)yield return new WaitForSeconds(Mathf.Max(10,waveDelay));
            }

            watchedFleetIndex=0;
            if(IsShowingBattle&&fleet.Count>0)WatchShip(fleet[0]);

            while(BattleStarted)
            {
                bool anyActive=false;
                for(int i=0;i<fleet.Count;i++)
                {
                    if(!fleet[i].gameObject.activeSelf||fleet[i].HasSettled)continue;
                    anyActive=true;
                    if(WatchedShip==null||!WatchedShip.gameObject.activeSelf)
                    {watchedFleetIndex=i;WatchShip(fleet[i]);}
                }
                // Retained wreck meshes are no longer active vessels. Their surviving crew swim.
                // Surviving crew remain a combat objective after the final hull settles.
                if(!anyActive&&RemainingEnemies==0){BachDangStoryManager.Instance?.OnBattleVictory();break;}
                if(IsShowingBattle&&WatchedShip!=null&&Time.unscaledTime>=commandMessageUntil)
                {
                    BattleCaption=!EbbStarted ? $"Nhử địch vào sâu trận cọc • Đã vào {ShipsInside}/{ShipCount} thuyền" :
                        $"Thuyền đắm {BachDangStoryManager.Instance?.destroyedEnemyShips ?? 0}/{ShipCount} • Địch còn lại {RemainingEnemies}\nHạ hết địch và đánh chìm toàn bộ thuyền để thắng • Q: kiếm • SPACE: leo tàu";
                }
                yield return null;
            }
            battleLoop=null;
            BattleStarted=false;
            choreography?.EndBattle(true);
            EndBattleView();
        }
        IEnumerator WatchStakeField(System.Collections.Generic.List<EnemyShip> fleet)
        {
            while(BattleStarted&&!EbbStarted&&EbbTriggerShip==null)
            {
                if(ShipsInside>=ShipCount)
                    foreach(var ship in fleet)
                        if(ship!=null&&ship.gameObject.activeInHierarchy&&!ship.isDestroyed&&ship.IsHullInsideStakeField)
                        {EbbTriggerShip=ship;break;}
                if(EbbTriggerShip==null)yield return null;
            }
            if(!BattleStarted)yield break;
            var timeline=BachDangBattleTimeline.Instance;
            if(!EbbStarted){timeline?.MarkFleetInside();timeline?.BeginEbb();EbbStarted=true;}
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
        }
        // Retained for old save/tool compatibility. Combat has no flag action.
        public bool TrySignalAttack()=>false;
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
