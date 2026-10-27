using TMPro;
using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>One lightweight HUD guide; references are cached once, never a scene scan per frame.</summary>
    public sealed class StoryRouteGuide : MonoBehaviour
    {
        BeachBoatQuestSession session;
        BachDangStoryManager story;
        NPCQuestGiver commander;
        RiverStakeZone stakes;
        CampGateTransition[] gates;
        BeachBoatMinigameStation[] activities;
        VillagePreparationQuest villageWork;
        GameObject panel;
        TMP_Text caption, arrow;
        Transform target;
        Transform lureWait;
        float nextRefresh;

        public void Initialize(BeachBoatQuestSession owner, GameObject box, TMP_Text text, TMP_Text pointer)
        {
            session = owner; story = owner.GetComponent<BachDangStoryManager>();
            commander = owner.GetComponentInChildren<NPCQuestGiver>(true);
            stakes = owner.GetComponentInChildren<RiverStakeZone>(true);
            gates = owner.GetComponentsInChildren<CampGateTransition>(true);
            activities = FindObjectsByType<BeachBoatMinigameStation>(FindObjectsInactive.Exclude);
            villageWork = FindFirstObjectByType<VillagePreparationQuest>();
            panel = box; caption = text; arrow = pointer;
        }

        public Transform CurrentTarget => target;
        public float CurrentDistanceMetres=>target!=null?WorldDistance.ToMetres(QuestRowboat.FlatDistance(target.position,session.player.transform.position)):0;
        public bool InInteractionRange(Transform destination)
        {
            if(destination==null)return false;
            var player=session.player.transform;
            foreach(var activity in activities)
                if(activity!=null&&activity.transform==destination)return QuestRowboat.FlatDistance(player.position,destination.position)<=activity.interactDistance;
            if(villageWork!=null)foreach(var stall in villageWork.stalls)
                if(stall!=null&&(stall.interactionPoint==destination||stall.transform==destination))return QuestRowboat.FlatDistance(player.position,stall.InteractionPosition)<=stall.interactionDistance;
            foreach(var gate in gates)if(gate!=null&&gate.transform==destination)return gate.IsInRange(player);
            if(commander!=null&&commander.transform==destination)return commander.CanReachPlayer;
            foreach(var wood in session.woodItems)if(wood!=null&&wood.transform==destination)return Vector3.Distance(player.position,destination.position)<=wood.interactDistance;
            var boat=QuestRowboat.Instance;
            if(boat!=null&&!boat.IsAboard&&boat.boardingPoint==destination)return QuestRowboat.FlatDistance(player.position,destination.position)<=boat.boardingRange;
            return false;
        }

        // River travel shares quest states with work/reporting. Choose the local end of that leg.
        public Transform ResolveTarget(out string title)
        {
            title = null;
            if (story == null || session.player == null) return null;
            var state = story.currentState;
            if (state == BachDangStoryManager.StoryState.Act2_Completed &&
                (EnemyFleetDirector.Instance == null || !EnemyFleetDirector.Instance.IsConfigured)) return null;
            Transform best = null; float distance = float.PositiveInfinity;
            var boat = QuestRowboat.Instance;
            if (state == BachDangStoryManager.StoryState.Act1_Training ||
                state == BachDangStoryManager.StoryState.Act2_SupplyPreparation)
            {
                if(state==BachDangStoryManager.StoryState.Act2_SupplyPreparation&&villageWork!=null)
                {
                    var stall=villageWork.NearestUnfinished(session.player.transform.position);
                    title=stall!=null?$"{stall.stallName} • Chuẩn bị {villageWork.CompletedCount}/{villageWork.stalls.Length} sạp":"Vật tư đã đủ";
                    return ThroughGate(stall!=null?(stall.interactionPoint!=null?stall.interactionPoint:stall.transform):null,ref title);
                }
                var required = state == BachDangStoryManager.StoryState.Act1_Training
                    ? BeachBoatMinigameStation.ActivityKind.WarDrum
                    : BeachBoatMinigameStation.ActivityKind.SupplyRelay;
                foreach (var activity in activities)
                    if (activity != null && activity.kind == required)
                        Consider(activity.transform, required == BeachBoatMinigameStation.ActivityKind.WarDrum
                            ? "Trống trận • học hiệu lệnh" : "Điểm tập kết cọc và vật tư",
                            ref best, ref distance, ref title);
                return ThroughGate(best,ref title);
            }
            if (state == BachDangStoryManager.StoryState.Act3_BattleInProgress)
            {
                if(!RiverBattleObjective.Counterattack)
                {
                    if(lureWait==null)
                    {
                        lureWait=new GameObject("Vung_Nhu_Dich_938").transform;lureWait.SetParent(transform,false);
                    }
                    lureWait.position=boat!=null?boat.BattleLurePosition:session.player.transform.position;
                    title=RiverBattleObjective.DirectionTitle;return lureWait;
                }
                var guard=RiverPlayerCombat.Instance!=null?RiverPlayerCombat.Instance.PriorityGuard:null;
                if(guard!=null){title="Lính Nam Hán • Dùng cung hoặc kiếm";return guard.transform;}
                var fleet = EnemyFleetDirector.Instance;
                if (fleet != null)
                {
                    bool hasReady = false;
                    foreach (var enemy in fleet.enemyShips)
                        if (enemy != null && enemy.gameObject.activeInHierarchy && enemy.SignalReady)
                        { hasReady = true; break; }
                    foreach (var enemy in fleet.enemyShips)
                        if (enemy != null && enemy.gameObject.activeInHierarchy && !enemy.isDestroyed &&
                            (!hasReady || enemy.SignalReady))
                            Consider(enemy.transform, "Chiến thuyền mắc cọc • Áp sát phản công",
                                ref best, ref distance, ref title);
                }
                return best;
            }
            if (boat != null && boat.IsAboard)
            {
                if (state == BachDangStoryManager.StoryState.Act2_PlantStakesAtRiver && stakes != null && stakes.RecoveryRequired && stakes.RecoveryTarget != null)
                { title = "Bó cọc bị trôi • dừng thuyền, bấm E để vớt"; return stakes.RecoveryTarget; }
                if (state == BachDangStoryManager.StoryState.Act2_CompleteReturnToCamp || state == BachDangStoryManager.StoryState.Act3_Victory)
                { title = "Chèo về bến • dừng thuyền, bấm E lên bờ"; return boat.dockPoint; }
                int point = stakes != null ? stakes.NearestUnplanted(boat.transform.position) : -1;
                if (point >= 0)
                { title = $"Cụm cọc {point + 1} • Đã xong {stakes.CompletedClusterCount}/{stakes.ClusterCount} cụm"; return stakes.placements[point].approach; }
            }
            if (boat != null && !boat.IsAboard && state == BachDangStoryManager.StoryState.Act2_PlantStakesAtRiver)
                Consider(boat.boardingPoint, "Bến thuyền", ref best, ref distance, ref title);
            foreach (var gate in gates)
                if (gate != null && gate.isActiveAndEnabled && gate.destinationTarget != null &&
                    ((state==BachDangStoryManager.StoryState.Act1_GoToCampGate&&gate.gateType==CampGateTransition.GateType.CampGate)||
                     (state==BachDangStoryManager.StoryState.Act2_ReturnToCampGate&&gate.gateType==CampGateTransition.GateType.VillageGate)))
                    Consider(gate.transform, gate.gateType == CampGateTransition.GateType.CampGate ? "Cổng sang làng ven sông" :
                        gate.gateType == CampGateTransition.GateType.CampRiverGate ? "Cổng ra bãi cọc" : "Cổng về doanh trại",
                        ref best, ref distance, ref title);
            if (state == BachDangStoryManager.StoryState.Act1_ExploreCamp ||
                state == BachDangStoryManager.StoryState.Act2_ReportToNgoQuyen ||
                state == BachDangStoryManager.StoryState.Act2_CompleteReturnToCamp)
                if (commander != null) Consider(commander.transform, "Chủ tướng Ngô Quyền", ref best, ref distance, ref title);
            if (boat == null && state == BachDangStoryManager.StoryState.Act2_PlantStakesAtRiver && stakes != null)
            {
                int index = stakes.NearestUnplanted(session.player.transform.position);
                if (index >= 0) Consider(stakes.placements[index].approach,
                    $"Vị trí đóng cọc {index + 1} • {stakes.PlantedCount}/{stakes.RequiredCount}", ref best, ref distance, ref title);
            }
            if (state == BachDangStoryManager.StoryState.Act2_CollectWoodInVillage)
                foreach (var wood in session.woodItems)
                    if (wood != null && wood.isActiveAndEnabled && QuestRowboat.FlatDistance(session.player.transform.position,wood.transform.position)<=wood.interactDistance)
                        Consider(wood.transform, $"[E] Nhặt gỗ • {story.collectedWood}/{story.totalWoodRequired}", ref best, ref distance, ref title);
            if (state == BachDangStoryManager.StoryState.Act2_Completed || state == BachDangStoryManager.StoryState.Act3_PrepareForBattle ||
                state == BachDangStoryManager.StoryState.Act3_Victory)
                if (commander != null) Consider(commander.transform, "Chủ tướng Ngô Quyền", ref best, ref distance, ref title);
            if (state == BachDangStoryManager.StoryState.Act3_WaitForLowTide && boat != null)
                Consider(boat.boardingPoint, "Bến thuyền chiến đấu", ref best, ref distance, ref title);
            return ThroughGate(best,ref title);
        }

        Transform ThroughGate(Transform objective,ref string title)
        {
            if(objective==null)return null;
            float cost=QuestRowboat.FlatDistance(session.player.transform.position,objective.position);var selected=objective;
            foreach(var gate in gates)
            {
                if(gate==null||!gate.isActiveAndEnabled||gate.destinationTarget==null)continue;
                float route=QuestRowboat.FlatDistance(session.player.transform.position,gate.transform.position)+
                    QuestRowboat.FlatDistance(gate.destinationTarget.position,objective.position)+25;
                if(route>=cost-20)continue;
                cost=route;selected=gate.transform;
                title=gate.gateType==CampGateTransition.GateType.CampGate?"Cổng đến LÀNG • nhiệm vụ 2":
                    gate.gateType==CampGateTransition.GateType.CampRiverGate?"Cổng ra BẾN SÔNG":"Cổng về DOANH TRẠI";
            }
            return selected;
        }

        void Consider(Transform candidate, string name, ref Transform best, ref float distance, ref string title)
        {
            var offset = candidate.position - session.player.transform.position; offset.y = 0;
            if (offset.sqrMagnitude >= distance) return;
            best = candidate; distance = offset.sqrMagnitude; title = name;
        }

        void LateUpdate()
        {
            if (panel == null) return;
            if (BeachBoatQuestSession.InteractionBlocked || BeachBoatMinigameStation.Active != null)
            { panel.SetActive(false); nextRefresh = 0; return; }
            if (Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + .2f;
                target = ResolveTarget(out var title);
                panel.SetActive(target != null);
                if (target != null)
                {
                    var offset = target.position - session.player.transform.position; offset.y = 0;
                    caption.text = title + "\n" + WorldDistance.Format(offset.magnitude) +
                        (story.currentState==BachDangStoryManager.StoryState.Act3_BattleInProgress?" • "+RiverBattleObjective.ControlHint:
                        InInteractionRange(target)?" • Trong tầm tương tác • Bấm E":" • Đến gần để bấm E");
                }
            }
            if (target == null || session.storyCamera == null) return;
            var direction = target.position - session.player.transform.position; direction.y = 0;
            var forward = session.storyCamera.transform.forward; forward.y = 0;
            float angle = Vector3.SignedAngle(forward, direction, Vector3.up);
            arrow.rectTransform.localRotation = Quaternion.Euler(0, 0, 90 - angle);
        }
    }
}
