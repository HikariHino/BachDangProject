using UnityEngine;
namespace BeachBoatStory
{
    // Boarding and input are owned by QuestRowboat; this adapter supports authored trigger references.
    public sealed class BattleTrigger : MonoBehaviour
    {
        public float triggerRadius=25;
        public string playerTag="Player";
        public QuestRowboat boat;
        public bool TryStartBattle()
        {
            if(!isActiveAndEnabled||BeachBoatQuestSession.InteractionBlocked)return false;
            var story=BachDangStoryManager.Instance;
            var rowboat=boat!=null?boat:QuestRowboat.Instance;
            if(story==null||story.currentState!=BachDangStoryManager.StoryState.Act3_WaitForLowTide||rowboat==null)return false;
            return rowboat.TryBoard();
        }
    }
}
