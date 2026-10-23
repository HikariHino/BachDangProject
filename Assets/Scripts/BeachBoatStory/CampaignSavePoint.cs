using UnityEngine;
using System.Collections.Generic;

namespace BeachBoatStory
{
    [DefaultExecutionOrder(-50)]
    public sealed class CampaignSavePoint : MonoBehaviour
    {
        public string displayName="Lều lưu hành trình";
        public float interactDistance=20;
        bool showing;
        static readonly List<CampaignSavePoint> points=new List<CampaignSavePoint>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void ResetPoints()=>points.Clear();
        void OnEnable(){if(!points.Contains(this))points.Add(this);}
        void OnDisable(){points.Remove(this);}
        public bool InRange=>BeachBoatQuestSession.Instance!=null&&
            Mathf.Abs(BeachBoatQuestSession.Instance.player.transform.position.y-transform.position.y)<14&&
            QuestRowboat.FlatDistance(BeachBoatQuestSession.Instance.player.transform.position,transform.position)<=interactDistance;
        public static bool HasNearbyPoint()
        {
            foreach(var point in points)if(point!=null&&point.InRange)return true;
            return false;
        }
        public bool TryUse()
        {
            if(!InRange||BeachBoatQuestSession.InteractionBlocked||BeachBoatMinigameStation.Active!=null||(QuestRowboat.Instance?.IsAboard??false))return false;
            BeachBoatSoundscape.Instance?.PlayInteraction();
            bool saved=BachDangSaveSystem.Save(displayName);
            BachDangFrontEnd.Instance.Notify(saved?"ĐÃ LƯU HÀNH TRÌNH • "+displayName:BachDangSaveSystem.LastError);
            BachDangFrontEnd.Instance.RefreshSaveInfo();return saved;
        }
        void Update()
        {
            bool near=InRange&&!BeachBoatQuestSession.InputBlocked&&!(QuestRowboat.Instance?.IsAboard??false);
            var story=BachDangStoryManager.Instance;if(story==null)return;
            if(near){story.SetInteractPrompt(true,"[E] Lưu hành trình • "+displayName,this);showing=true;
                if(Input.GetKeyDown(KeyCode.E)&&StoryDialogueUI.TryConsumeInput())TryUse();}
            else if(showing){story.SetInteractPrompt(false,owner:this);showing=false;}
        }
    }
}
