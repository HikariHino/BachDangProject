using System;
using System.Linq;
using UnityEngine;
using Story = BeachBoatStory.BachDangStoryManager;

namespace BeachBoatStory
{
    /// <summary>Sets up a playable checkpoint in the campaign scene.</summary>
    public static class CampaignQuickPlay
    {
        public enum Scenario { Drum, Timber, Rope, Rice, Provisions, Stakes, Lure, Archery }

        public static bool ArcheryReady => ArcheryTraining.Instance?.Targets?.Length > 0;

        public static void SetUp(Scenario scenario)
        {
            var session = BeachBoatQuestSession.Instance;
            var story = Story.Instance;
            var menu = BachDangFrontEnd.Instance;
            UnityEngine.Object.FindAnyObjectByType<OpeningEvent>()?.SkipOpening();
            StoryDialogueUI.Instance?.DismissForCheckpoint();
            if (scenario != Scenario.Drum)
            {
                foreach (var wood in session.woodItems)
                    if (wood != null) wood.RestoreCollection(true);
                story.collectedWood = story.totalWoodRequired;
            }

            if (scenario == Scenario.Drum)
            {
                var drum = UnityEngine.Object.FindObjectsByType<BeachBoatMinigameStation>()
                    .First(station => station.kind == BeachBoatMinigameStation.ActivityKind.WarDrum);
                var ground = drum.GetComponentInParent<DrumTrainingGround>();
                story.TransitionTo(Story.StoryState.Act1_Training, true);
                PlacePlayer(session.player, ground.Approach, ground.transform.rotation);
            }
            else if (scenario == Scenario.Archery)
            {
                var range = ArcheryTraining.Instance;
                if (!ArcheryReady)
                    throw new InvalidOperationException("Bãi tập cung chưa sẵn sàng.");
                story.TransitionTo(Story.StoryState.Act3_PrepareForBattle, true);
                var direction = Vector3.ProjectOnPlane(range.Targets[0].transform.position - range.Approach, Vector3.up);
                PlacePlayer(session.player, range.Approach, Quaternion.LookRotation(direction));
            }
            else if (scenario == Scenario.Stakes || scenario == Scenario.Lure)
                StartOnRiver(story, scenario == Scenario.Lure);
            else
            {
                var kind = (VillageWorkStall.WorkKind)((int)scenario - (int)Scenario.Timber);
                var stall = VillagePreparationQuest.Instance.stalls.First(item => item.kind == kind);
                story.TransitionTo(Story.StoryState.Act2_SupplyPreparation, true);
                PlacePlayer(session.player, stall.InteractionPosition, stall.transform.rotation);
            }

            session.storyCamera.GetComponent<BeachBoatStory.Camera_Script>().SnapToTarget();
            QuestCheckpoint.Instance?.Capture(story.currentState);
            BachDangAutoSave.Instance.AcceptSavedPoint(BachDangSaveSystem.Capture("Test nhanh • " + scenario));
            menu.Notify(scenario == Scenario.Lure ? "TEST NHANH • Chèo ra nhử địch" : "TEST NHANH • Nhấn E để bắt đầu");
        }

        static void PlacePlayer(GameObject player, Vector3 position, Quaternion rotation)
        {
            var terrain = Terrain.activeTerrain;
            var capsule = player.GetComponent<CapsuleCollider>();
            float bottom = (capsule.center.y - capsule.height * .5f) * player.transform.lossyScale.y;
            position.y = terrain.SampleHeight(position) + terrain.transform.position.y - bottom + .4f;
            if (!PlayerArrival.Teleport(player, position, rotation))
                throw new InvalidOperationException("Không có vị trí đứng an toàn gần mục tiêu.");
        }

        static void StartOnRiver(Story story, bool lure)
        {
            var zone = BeachBoatStory.RiverStakeZone.Instance;
            var boat = QuestRowboat.Instance;
            if (lure)
            {
                zone.RestoreCheckpoint(Enumerable.Repeat(true, zone.ClusterCount).ToArray());
                story.TransitionTo(Story.StoryState.Act3_WaitForLowTide, true);
                boat.RestoreSave(true, boat.BattleLurePosition, boat.transform.rotation);
                story.TransitionTo(Story.StoryState.Act3_BattleInProgress, true);
                EnemyFleetDirector.Instance.StartBattle();
                if (!EnemyFleetDirector.Instance.BattleStarted)
                    throw new InvalidOperationException("Hạm đội chưa sẵn sàng.");
                EnemyFleetDirector.Instance.GetComponent<RiverInvasionIntro>().Cancel();
                return;
            }

            story.TransitionTo(Story.StoryState.Act2_PlantStakesAtRiver, true);
            var approach = zone.placements[0].approach;
            var position = approach.position;
            position.y = TideSystem.CurrentWaterHeight;
            for (int direction = 0; direction < 8; direction++)
            {
                var rotation = Quaternion.Euler(0, direction * 45, 0);
                if (!boat.CanOccupy(position, rotation)) continue;
                boat.RestoreSave(true, position, rotation);
                return;
            }
            throw new InvalidOperationException("Không có chỗ đặt thuyền an toàn tại điểm đóng cọc.");
        }
    }
}
