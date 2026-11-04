using System.Linq;
using UnityEngine;
namespace BeachBoatStory
{
    /// <summary>A short nod and acknowledgement from the existing stall worker, without hijacking movement.</summary>
    [DefaultExecutionOrder(710)]
    public sealed class VillageVendorReaction:MonoBehaviour
    {
        Transform head,player;float began;Quaternion rest;bool posed;
        public static void Acknowledge(VillageWorkStall stall)
        {
            var actor=CampDiscipline.Instance?.Actors.Where(a=>a!=null&&a.Type==ProtectedCampNPC.Kind.Civilian&&!a.Dead&&!a.Retaliating&&a.GetComponent<NPCWander>()?.ExternalControl!=true&&QuestRowboat.FlatDistance(a.transform.position,stall.InteractionPosition)<65).OrderBy(a=>(a.transform.position-stall.InteractionPosition).sqrMagnitude).FirstOrDefault();
            if(actor==null)return;var reaction=actor.GetComponent<VillageVendorReaction>()??actor.gameObject.AddComponent<VillageVendorReaction>();reaction.Begin();
        }
        void Begin(){head=GetComponentInChildren<Animator>()?.GetBoneTransform(HumanBodyBones.Head);player=BeachBoatQuestSession.Instance.player.transform;began=Time.time;}
        void Update(){if(posed&&head!=null){head.localRotation=rest;posed=false;}}
        void LateUpdate(){if(head==null||Time.time-began>2.2f||GetComponent<NPCWander>()?.ExternalControl==true)return;rest=head.localRotation;float t=Time.time-began;float nod=Mathf.Sin(t*7)*Mathf.Sin(Mathf.Clamp01(t/2.2f)*Mathf.PI)*9;head.localRotation=rest*Quaternion.Euler(nod,0,0);posed=true;}
        void OnDisable(){if(posed&&head!=null)head.localRotation=rest;posed=false;}
    }
}
