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
        public bool AmbushStarted {get;private set;}
        public EnemyShip EbbTriggerShip {get;private set;}
        public RiverFleetPursuit Pursuit {get;private set;}
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
        void Awake(){Instance=this;Pursuit=GetComponent<RiverFleetPursuit>()??gameObject.AddComponent<RiverFleetPursuit>();if(GetComponent<RiverInvasionIntro>()==null)gameObject.AddComponent<RiverInvasionIntro>();}
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
            Pursuit.ResetLure();
            GetComponent<RiverInvasionIntro>().Queue();
            GetComponent<RiverBattleVoices>()?.ResetForNewBattle();
            CommandScore=0;
            EbbStarted=false;AmbushStarted=false;EbbTriggerShip=null;
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
            Pursuit.FillSave(data);RiverPlayerCombat.Instance?.FillSave(data);GetComponent<RiverBattleVoices>()?.FillSave(data);return data;
        }
        public void RestoreSave(BachDangSaveData.Fleet data)
        {
            GetComponent<RiverBattleVoices>()?.RestoreSave(data);
            BattleStarted=data.active;EbbStarted=data.ebb;CommandScore=data.commands;scoredWaves.Clear();
            Pursuit.RestoreSave(data);
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
            tideLoop=StartCoroutine(WatchStakeField(fleet));
            var pending=new System.Collections.Generic.List<EnemyShip>();
            foreach(var ship in fleet)if(!resume||(!ship.gameObject.activeSelf&&!ship.isDestroyed))pending.Add(ship);
            // Preflight the whole formation before activating any vessel. No timed waves.
            float shift=0;
            while(BattleStarted&&pending.Count>0)
            {
                bool clear=true;
                foreach(var ship in pending)
                {
                    var p=ship.spawnPoint+Vector3.forward*shift;p.y=TideSystem.CurrentWaterHeight;
                    if(!BoatHullCollision.Get(ship.transform).CanOccupy(p,Quaternion.Euler(0,180,0),false)){clear=false;break;}
                }
                if(clear)break;
                if(shift<240)shift+=80;else yield return new WaitForSeconds(.5f);
            }
            if(!BattleStarted)yield break;
            foreach(var ship in pending){var p=ship.spawnPoint+Vector3.forward*shift;p.y=TideSystem.CurrentWaterHeight;ship.transform.position=p;ship.Activate();}
            if(!resume&&fleet.Count>0)GetComponent<RiverInvasionIntro>().Begin(fleet[0]);
            BattleCaption=$"Hạm đội Nam Hán xuất hiện ngoài cửa sông • {fleet.Count} thuyền";

            watchedFleetIndex=0;
            if(IsShowingBattle&&fleet.Count>0)WatchShip(fleet[0]);

            int shownInside=-1,shownMoment=-1;
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
                int inside=ShipsInside,moment=(int)(BachDangBattleTimeline.Instance?.CurrentMoment??BachDangBattleTimeline.BattleMoment.LureAtHighTide);
                if(inside!=shownInside||moment!=shownMoment){shownInside=inside;shownMoment=moment;BachDangStoryManager.Instance?.UpdateUI();}
                if(IsShowingBattle&&WatchedShip!=null&&Time.unscaledTime>=commandMessageUntil)
                {
                    BattleCaption=RiverBattleObjective.Title+"\n"+RiverBattleObjective.Description;
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
                if(Pursuit.HasEnteredTrap&&ShipsInside>0&&EntryDistance(fleet)<=400)
                    foreach(var ship in fleet)
                        if(ship!=null&&ship.gameObject.activeInHierarchy&&!ship.isDestroyed&&ship.IsHullInsideStakeField)
                        {EbbTriggerShip=ship;break;}
                if(EbbTriggerShip==null)yield return null;
            }
            if(!BattleStarted)yield break;
            var timeline=BachDangBattleTimeline.Instance;
            if(!EbbStarted)
            {
                if(ShipsInside>=ShipCount){timeline?.MarkFleetInside();timeline?.BeginEbb();}
                else timeline?.BeginApproachEbb(CoveredWaterHeight(fleet));
                EbbStarted=true;
            }
            BachDangStoryManager.Instance?.UpdateUI();
            // The visible ebb overlaps the last rank's entry, but the water stays
            // above every keel/stake pair until the complete formation is inside.
            while(BattleStarted&&ShipsInside<ShipCount)yield return null;
            if(!BattleStarted)yield break;
            if(TideSystem.Instance.TargetWaterHeight>TideSystem.Instance.lowTideY+.01f)
            {timeline?.MarkFleetInside();timeline?.BeginEbb();BachDangStoryManager.Instance?.UpdateUI();}
            // Counterattack becomes available as tips emerge, while the ebb is still in progress.
            while(BattleStarted)
            {
                bool ready=RiverStakeZone.Instance!=null&&RiverStakeZone.Instance.ExposedStakeCount>0;
                foreach(var ship in fleet)
                    if(ship!=null&&!ship.isDestroyed&&(!ship.IsHullInsideStakeField||!RiverStakeZone.Instance.TryHullContact(ship.transform.position,ship.transform.position,ship.transform.forward,ship.hullHalfLength,ship.stakeHitDistance,true,out _)))ready=false;
                if(ready)break;yield return null;
            }
            if(!BattleStarted)yield break;
            AmbushStarted=true;
            timeline?.BeginAmbush();
            // The exposed field catches the whole formation in one ambush event.
            foreach(var ship in fleet)if(ship!=null&&!ship.isDestroyed&&RiverStakeZone.Instance.TryHullContact(ship.transform.position,ship.transform.position,ship.transform.forward,ship.hullHalfLength,ship.stakeHitDistance,true,out var point))ship.TakeDamage(point);
            choreography=GetComponent<RiverBattleChoreography>();choreography?.BeginAmbush(fleet);
            BachDangStoryManager.Instance?.UpdateUI();tideLoop=null;
        }
        float EntryDistance(System.Collections.Generic.List<EnemyShip> fleet)
        {
            var field=RiverStakeZone.Instance.FieldBounds;float remaining=0;
            foreach(var ship in fleet)
            {
                if(ship==null||ship.isDestroyed)continue;
                if(!ship.gameObject.activeInHierarchy)return float.PositiveInfinity;
                var b=BoatHullCollision.Get(ship.transform).SolidHull.bounds;
                remaining=Mathf.Max(remaining,Mathf.Max(Mathf.Max(b.max.z-field.max.z+8,field.min.z+8-b.min.z),
                    Mathf.Max(field.min.x+8-b.min.x,b.max.x-field.max.x+8)));
            }
            return remaining;
        }
        float CoveredWaterHeight(System.Collections.Generic.List<EnemyShip> fleet)
        {
            float top=float.NegativeInfinity,depth=0;
            foreach(var stake in RiverStakeZone.Instance.fieldStakes)
                if(stake!=null&&stake.gameObject.activeInHierarchy)
                    foreach(var r in stake.GetComponentsInChildren<Renderer>())top=Mathf.Max(top,r.bounds.max.y);
            foreach(var ship in fleet)if(ship!=null&&!ship.isDestroyed)
                depth=Mathf.Max(depth,ship.transform.position.y-BoatHullCollision.Get(ship.transform).SolidHull.bounds.min.y);
            return Mathf.Min(TideSystem.CurrentWaterHeight,top+depth+1.5f);
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
            BattleCaption="Chèo ra cửa sông tiếp cận hạm đội Nam Hán...";
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
            GetComponent<RiverInvasionIntro>()?.Cancel();
            GetComponent<RiverBattleVoices>()?.Silence();
            choreography?.EndBattle();
            EndBattleView();
            if(battleLoop!=null){StopCoroutine(battleLoop);battleLoop=null;}
            if(tideLoop!=null){StopCoroutine(tideLoop);tideLoop=null;}
            BattleStarted=false;
            Pursuit.ResetLure();
            foreach(var ship in enemyShips??System.Array.Empty<EnemyShip>())if(ship!=null){ship.ClearWreck();ship.gameObject.SetActive(false);}
        }
        public void ResetForCheckpoint()
        {
            GetComponent<RiverBattleVoices>()?.ResetForNewBattle();
            StopBattle();CommandScore=0;scoredWaves.Clear();watchedFleetIndex=0;commandMessageUntil=0;EbbStarted=false;AmbushStarted=false;EbbTriggerShip=null;
            RiverPlayerCombat.Instance?.ResetForCheckpoint();
            foreach(var ship in enemyShips??System.Array.Empty<EnemyShip>())if(ship!=null)ship.ResetForCheckpoint();
        }
    }
}
