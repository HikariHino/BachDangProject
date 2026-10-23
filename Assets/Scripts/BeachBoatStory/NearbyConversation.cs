using System.Collections.Generic;
using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>One close, visible NPC in front of the player owns ordinary conversation.</summary>
    public static class NearbyConversation
    {
        static readonly List<MonoBehaviour> speakers=new List<MonoBehaviour>();
        static readonly RaycastHit[] hits=new RaycastHit[24];
        static int frame=-1;
        static MonoBehaviour chosen;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset(){speakers.Clear();frame=-1;chosen=null;}
        public static void Register(MonoBehaviour speaker){if(!speakers.Contains(speaker))speakers.Add(speaker);frame=-1;}
        public static void Remove(MonoBehaviour speaker){speakers.Remove(speaker);frame=-1;}
        public static bool InReach(Transform target,float distance)
        {
            var player=BeachBoatQuestSession.Instance?.player;
            if(player==null||!player.activeInHierarchy)return false;
            var capsule=player.GetComponent<CapsuleCollider>();float height=capsule.height*Mathf.Abs(player.transform.lossyScale.y);
            var offset=target.position-player.transform.position;
            if(Mathf.Abs(offset.y)>height*.55f)return false;offset.y=0;
            float range=Mathf.Min(distance,height*.42f);
            if(offset.sqrMagnitude>range*range)return false;
            if(offset.sqrMagnitude>1&&Vector3.Dot(player.transform.forward,offset.normalized)<.4f)return false;
            var start=player.transform.position+Vector3.up*(height*.55f);
            var end=target.position+Vector3.up*(height*.55f);var delta=end-start;
            int count=Physics.RaycastNonAlloc(start,delta.normalized,hits,delta.magnitude,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)if(!hits[i].transform.IsChildOf(player.transform)&&!hits[i].transform.IsChildOf(target))return false;
            return true;
        }
        public static bool IsPreferred(MonoBehaviour candidate)
        {
            if(BeachBoatQuestSession.InteractionBlocked||CampaignSavePoint.HasNearbyPoint())return false;
            if(frame!=Time.frameCount)
            {
                frame=Time.frameCount;chosen=null;float nearest=float.PositiveInfinity;
                foreach(var speaker in speakers)
                {
                    if(speaker==null||!speaker.isActiveAndEnabled)continue;
                    float range=speaker is NPCAmbientDialogue ambient?ambient.interactDistance:((NPCDialogue)speaker).interactDistance;
                    if(!InReach(speaker.transform,range))continue;
                    float d=(speaker.transform.position-BeachBoatQuestSession.Instance.player.transform.position).sqrMagnitude;
                    if(d<nearest){nearest=d;chosen=speaker;}
                }
            }
            return chosen==candidate;
        }
    }
}
