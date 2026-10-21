using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Small retargeting correction for the veteran's asymmetric bind pose. Leaves rest poses alone.</summary>
    [DisallowMultipleComponent]
    public sealed class NPCUprightPosture : MonoBehaviour
    {
        Animator animator;
        Transform hips,spine,head;
        Transform leftUpper,leftLower,leftHand,rightUpper,rightLower,rightHand,leftThigh,rightThigh;
        [SerializeField,HideInInspector] bool hasHandReference;
        [SerializeField,HideInInspector] Quaternion leftHandReference,rightHandReference;
        NPCWander wander;
        NPCCampPose campPose;
        void Awake()=>Cache();
        void Cache()
        {
            animator=GetComponent<Animator>();wander=GetComponent<NPCWander>();campPose=GetComponent<NPCCampPose>();
            if(animator==null||!animator.isHuman)return;
            hips=animator.GetBoneTransform(HumanBodyBones.Hips);spine=animator.GetBoneTransform(HumanBodyBones.Spine);head=animator.GetBoneTransform(HumanBodyBones.Head);
            leftUpper=animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);leftLower=animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);leftHand=animator.GetBoneTransform(HumanBodyBones.LeftHand);
            rightUpper=animator.GetBoneTransform(HumanBodyBones.RightUpperArm);rightLower=animator.GetBoneTransform(HumanBodyBones.RightLowerArm);rightHand=animator.GetBoneTransform(HumanBodyBones.RightHand);
            leftThigh=animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);rightThigh=animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        }
        public void SetHandReference(Quaternion left,Quaternion right)
        {leftHandReference=left;rightHandReference=right;hasHandReference=true;}
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
            if(Mathf.Abs(angle)>=1)spine.rotation=Quaternion.AngleAxis(Mathf.Clamp(angle,-12,12),transform.forward)*spine.rotation;
            if(!hasHandReference)return;
            RelaxArm(leftUpper,leftLower,leftHand,leftThigh,-1,leftHandReference);
            RelaxArm(rightUpper,rightLower,rightHand,rightThigh,1,rightHandReference);
        }
        void RelaxArm(Transform upper,Transform lower,Transform hand,Transform thigh,int side,Quaternion wrist)
        {
            if(upper==null||lower==null||hand==null)return;
            // Follow the opposite leg's motion, keeping the armored upper arm outside
            // the torso. Rotate the actual bone-to-child axis rather than assuming
            // the imported FBX uses Unity's local Y as its arm axis.
            float swing=0;
            if(thigh!=null&&thigh.childCount>0)
            {
                var leg=transform.InverseTransformDirection((thigh.GetChild(0).position-thigh.position).normalized);
                swing=Mathf.Clamp(-leg.z*.65f,-.3f,.3f);
            }
            var upperDirection=transform.TransformDirection(new Vector3(side*.20f,-1,swing)).normalized;
            upper.rotation=Quaternion.FromToRotation(lower.position-upper.position,upperDirection)*upper.rotation;
            var lowerDirection=transform.TransformDirection(new Vector3(side*.08f,-1,swing+.20f)).normalized;
            lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,lowerDirection)*lower.rotation;
            hand.localRotation=wrist;
        }
    }
}
