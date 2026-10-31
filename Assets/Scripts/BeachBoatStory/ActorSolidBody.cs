using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Separate solid contact volume; combat hit triggers keep their existing behavior.</summary>
    [DefaultExecutionOrder(700)]
    public sealed class ActorSolidBody : MonoBehaviour
    {
        public const int ContactLayer=14;
        public Transform Carrier {get;set;}
        Animator animator;
        CapsuleCollider solid;
        EnemyDeckGuard guard;
        Transform feet,otherFoot,head;
        bool walkingBody;
        public CapsuleCollider SolidCollider=>solid;
        void OnEnable()
        {
            // Synthetic / legacy actors may supply their own contact capsule.
            var existing=GetComponent<Collider>();if(existing!=null&&existing.enabled&&!existing.isTrigger)existing.gameObject.layer=ContactLayer;
        }
        public float WalkingHeight {get;private set;}
        public float WalkingSoleOffset {get;private set;}
        public float WalkingRadius {get;private set;}
        public static void WalkingDimensions(Animator rig,out float height,out float sole,out float radius)
        {
            float stature=EnemyDeckGuard.RigHeight(rig);
            float lowest=Mathf.Min(rig.GetBoneTransform(HumanBodyBones.LeftFoot).position.y,rig.GetBoneTransform(HumanBodyBones.RightFoot).position.y);
            foreach(var bone in new[]{HumanBodyBones.LeftToes,HumanBodyBones.RightToes}){var toe=rig.GetBoneTransform(bone);if(toe!=null)lowest=Mathf.Min(lowest,toe.position.y);}
            sole=lowest-rig.transform.position.y-stature*.025f;
            var shoulders=rig.GetBoneTransform(HumanBodyBones.LeftUpperArm).position-rig.GetBoneTransform(HumanBodyBones.RightUpperArm).position;shoulders.y=0;
            radius=Mathf.Max(1.5f,Mathf.Max(stature*.19f,shoulders.magnitude*.5f+stature*.05f));
            height=Mathf.Max(radius*2,rig.GetBoneTransform(HumanBodyBones.Head).position.y-rig.transform.position.y-sole+radius);
        }
        public static void Ensure(GameObject actor,Animator rig)
        {
            if(rig==null||!rig.isHuman)return;
            bool walker=actor.GetComponent<NPCWander>()!=null;
            // Legacy mesh-sized boxes and the new capsule must never both block a walker.
            if(walker)foreach(var legacy in actor.GetComponents<Collider>())if(!legacy.isTrigger)legacy.enabled=false;
            if(actor.GetComponent<ActorSolidBody>()!=null)return;
            foreach(var c in actor.GetComponentsInChildren<CapsuleCollider>(true))if(c.enabled&&!c.isTrigger)return;
            var body=actor.AddComponent<ActorSolidBody>();body.animator=rig;body.guard=actor.GetComponent<EnemyDeckGuard>();
            body.walkingBody=walker;
            if(walker){WalkingDimensions(rig,out var h,out var sole,out var radius);body.WalkingHeight=h;body.WalkingSoleOffset=sole;body.WalkingRadius=radius;}
            body.feet=rig.GetBoneTransform(HumanBodyBones.LeftFoot);body.otherFoot=rig.GetBoneTransform(HumanBodyBones.RightFoot);body.head=rig.GetBoneTransform(HumanBodyBones.Head);
            var go=new GameObject("Solid_Actor_Body");go.transform.SetParent(actor.transform,false);go.layer=ContactLayer;body.solid=go.AddComponent<CapsuleCollider>();body.solid.isTrigger=false;body.UpdateShape();
        }
        void LateUpdate()=>UpdateShape();
        void UpdateShape()
        {
            if(solid==null||feet==null||head==null)return;
            solid.enabled=guard==null||guard.Health>0;if(!solid.enabled)return;
            if(walkingBody)
            {
                solid.transform.SetPositionAndRotation(transform.position+Vector3.up*(WalkingSoleOffset+WalkingHeight*.5f),Quaternion.identity);
                var size=solid.transform.lossyScale;solid.radius=WalkingRadius/Mathf.Max(.01f,Mathf.Abs(size.x));solid.height=WalkingHeight/Mathf.Max(.01f,Mathf.Abs(size.y));solid.center=Vector3.zero;return;
            }
            var bottom=(feet.position+otherFoot.position)*.5f;var axis=head.position-bottom;
            float length=Mathf.Max(3,axis.magnitude);float radius=Mathf.Max(1.5f,length*.16f);
            solid.transform.SetPositionAndRotation((bottom+head.position)*.5f+axis.normalized*(radius*.5f),Quaternion.FromToRotation(Vector3.up,axis));
            var scale=solid.transform.lossyScale;solid.radius=radius/Mathf.Max(.01f,Mathf.Abs(scale.x));solid.height=(length+radius)/Mathf.Max(.01f,Mathf.Abs(scale.y));solid.center=Vector3.zero;
        }
    }
}
