using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Small retargeting correction for the veteran's asymmetric bind pose. Leaves rest poses alone.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class NPCUprightPosture : MonoBehaviour
    {
        Animator animator;
        Transform hips,spine,head,neck,leftShoulder,rightShoulder;
        Transform leftUpper,leftLower,leftHand,rightUpper,rightLower,rightHand,leftThigh,rightThigh;
        [SerializeField,HideInInspector] bool hasHandReference;
        [SerializeField,HideInInspector] Quaternion leftHandReference,rightHandReference;
        NPCWander wander;
        NPCCampPose campPose;
        void Awake()=>Cache();
        void OnEnable()=>Cache();
        void Start()
        {
            if(animator==null||!animator.enabled||animator.runtimeAnimatorController==null)return;
            // Some scene actors were saved with a sitting/lying preview. Evaluate the
            // selected runtime pose once even outside the camera before culling starts.
            var culling=animator.cullingMode;
            animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            animator.Update(0);
            Balance();
            animator.cullingMode=culling;
        }
        void Cache()
        {
            animator=GetComponent<Animator>();wander=GetComponent<NPCWander>();campPose=GetComponent<NPCCampPose>();
            if(animator==null||!animator.isHuman)return;
            hips=animator.GetBoneTransform(HumanBodyBones.Hips);spine=animator.GetBoneTransform(HumanBodyBones.Spine);head=animator.GetBoneTransform(HumanBodyBones.Head);
            neck=animator.GetBoneTransform(HumanBodyBones.Neck);leftShoulder=animator.GetBoneTransform(HumanBodyBones.LeftShoulder);rightShoulder=animator.GetBoneTransform(HumanBodyBones.RightShoulder);
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
            // Rolling the entire spine to level an asymmetric clavicle line made the
            // chest lean away from the pelvis. Center the neck over the hips first;
            // compensate the imported shoulder asymmetry at the shoulders themselves.
            var pivot=transform.InverseTransformPoint(spine.position);
            var top=transform.InverseTransformPoint(neck!=null?neck.position:head.position);
            var pelvis=transform.InverseTransformPoint(hips.position);
            var axis=new Vector2(top.x-pivot.x,top.y-pivot.y);
            if(axis.y>.1f)
            {
                float desiredX=Mathf.Clamp((pelvis.x-pivot.x)/axis.magnitude,-1,1);
                float roll=(Mathf.Atan2(axis.x,axis.y)-Mathf.Asin(desiredX))*Mathf.Rad2Deg;
                spine.rotation=Quaternion.AngleAxis(Mathf.Clamp(roll,-18,18),transform.forward)*spine.rotation;
            }
            if(leftShoulder!=null&&rightShoulder!=null)
            {
                var left=transform.InverseTransformPoint(leftShoulder.position);
                var right=transform.InverseTransformPoint(rightShoulder.position);
                left.y=right.y=(left.y+right.y)*.5f;
                leftShoulder.position=transform.TransformPoint(left);
                rightShoulder.position=transform.TransformPoint(right);
                LevelClavicle(leftShoulder,leftUpper,-1);LevelClavicle(rightShoulder,rightUpper,1);
            }
            if(neck!=null)
            {
                var direction=head.position-neck.position;
                var projected=Vector3.ProjectOnPlane(direction,transform.forward).normalized;
                if(projected.sqrMagnitude>.1f)neck.rotation=Quaternion.FromToRotation(projected,transform.up)*neck.rotation;
            }
            if(!hasHandReference)return;
            RelaxArm(leftUpper,leftLower,leftHand,leftThigh,-1,leftHandReference);
            RelaxArm(rightUpper,rightLower,rightHand,rightThigh,1,rightHandReference);
        }
        void LevelClavicle(Transform shoulder,Transform upper,int side)
        {
            if(upper==null)return;
            var outward=transform.TransformDirection(new Vector3(side,0,-.10f));
            shoulder.rotation=Quaternion.FromToRotation(upper.position-shoulder.position,outward)*shoulder.rotation;
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
