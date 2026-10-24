using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Separate solid contact volume; combat hit triggers keep their existing behavior.</summary>
    [DefaultExecutionOrder(700)]
    public sealed class ActorSolidBody : MonoBehaviour
    {
        public Transform Carrier {get;set;}
        Animator animator;
        CapsuleCollider solid;
        EnemyDeckGuard guard;
        Transform feet,otherFoot,head;
        public static void Ensure(GameObject actor,Animator rig)
        {
            if(rig==null||!rig.isHuman||actor.GetComponent<ActorSolidBody>()!=null)return;
            foreach(var c in actor.GetComponentsInChildren<CapsuleCollider>(true))if(!c.isTrigger)return;
            var body=actor.AddComponent<ActorSolidBody>();body.animator=rig;body.guard=actor.GetComponent<EnemyDeckGuard>();
            body.feet=rig.GetBoneTransform(HumanBodyBones.LeftFoot);body.otherFoot=rig.GetBoneTransform(HumanBodyBones.RightFoot);body.head=rig.GetBoneTransform(HumanBodyBones.Head);
            var go=new GameObject("Solid_Actor_Body");go.transform.SetParent(actor.transform,false);body.solid=go.AddComponent<CapsuleCollider>();body.solid.isTrigger=false;body.UpdateShape();
        }
        void LateUpdate()=>UpdateShape();
        void UpdateShape()
        {
            if(solid==null||feet==null||head==null)return;
            solid.enabled=guard==null||guard.Health>0;if(!solid.enabled)return;
            var bottom=(feet.position+otherFoot.position)*.5f;var axis=head.position-bottom;
            float length=Mathf.Max(3,axis.magnitude);float radius=Mathf.Max(1.5f,length*.16f);
            solid.transform.SetPositionAndRotation((bottom+head.position)*.5f+axis.normalized*(radius*.5f),Quaternion.FromToRotation(Vector3.up,axis));
            var scale=solid.transform.lossyScale;solid.radius=radius/Mathf.Max(.01f,Mathf.Abs(scale.x));solid.height=(length+radius)/Mathf.Max(.01f,Mathf.Abs(scale.y));solid.center=Vector3.zero;
        }
    }
}
