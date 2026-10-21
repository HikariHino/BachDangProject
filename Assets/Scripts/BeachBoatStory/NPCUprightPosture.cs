using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Small retargeting correction for the veteran's asymmetric bind pose. Leaves rest poses alone.</summary>
    [DisallowMultipleComponent]
    public sealed class NPCUprightPosture : MonoBehaviour
    {
        Animator animator;
        Transform hips,spine,head;
        NPCWander wander;
        NPCCampPose campPose;
        void Awake()=>Cache();
        void Cache()
        {
            animator=GetComponent<Animator>();wander=GetComponent<NPCWander>();campPose=GetComponent<NPCCampPose>();
            if(animator==null||!animator.isHuman)return;
            hips=animator.GetBoneTransform(HumanBodyBones.Hips);spine=animator.GetBoneTransform(HumanBodyBones.Spine);head=animator.GetBoneTransform(HumanBodyBones.Head);
        }
        void LateUpdate()=>Balance();
        public void Balance()
        {
            if(animator==null)Cache();
            if(spine==null||head==null||hips==null||!animator.enabled||
                (wander!=null&&wander.IsSleeping)||(campPose!=null&&campPose.pose!=NPCCampPose.PoseType.Standing))return;
            // Sitting/lying animation must retain its authored body orientation.
            if(animator.runtimeAnimatorController!=null)
            {var state=animator.GetCurrentAnimatorStateInfo(0);if(state.IsName("Sitting")||state.IsName("Laying"))return;}
            var localHead=transform.InverseTransformPoint(head.position);var localHips=transform.InverseTransformPoint(hips.position);
            var localSpine=transform.InverseTransformPoint(spine.position);
            float angle=Mathf.Atan2(localHead.x-localHips.x,Mathf.Max(.1f,localHead.y-localSpine.y))*Mathf.Rad2Deg;
            if(Mathf.Abs(angle)<1)return;
            spine.rotation=Quaternion.AngleAxis(Mathf.Clamp(angle,-12,12),transform.forward)*spine.rotation;
        }
    }
}
