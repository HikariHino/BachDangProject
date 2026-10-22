using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Shared sword rig and bounded swim/climb poses on existing humanoid skeletons.</summary>
    [DefaultExecutionOrder(600)]
    public sealed class RiverFighterPose : MonoBehaviour
    {
        public enum Motion { Idle, Swim, Climb }
        public Motion Traversal {get;set;}
        public Animator Animator {get;private set;}
        public bool SwordVisible {get;private set;}
        public RuntimeAnimatorController swordController;
        Transform weapon;
        Transform rangedBow,leftHand,rightHand;
        bool ranged;
        float actorHeight;
        Transform[] bones;
        Quaternion[] rotations;
        Quaternion modelRotation;
        RuntimeAnimatorController previousController;
        bool usingSword;
        public void Initialize(Animator animator,GameObject sword,RuntimeAnimatorController controller,float length)
        {
            Animator=animator;swordController=controller;modelRotation=animator.transform.localRotation;actorHeight=length*1.6f;
            var ids=new[]{HumanBodyBones.LeftUpperArm,HumanBodyBones.RightUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.RightLowerArm,HumanBodyBones.LeftUpperLeg,HumanBodyBones.RightUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.RightLowerLeg};
            bones=new Transform[ids.Length];rotations=new Quaternion[ids.Length];for(int i=0;i<ids.Length;i++){bones[i]=animator.GetBoneTransform(ids[i]);rotations[i]=bones[i].localRotation;}
            weapon=Instantiate(sword,animator.GetBoneTransform(HumanBodyBones.RightHand),false).transform;weapon.name="River_Combat_Sword";
            foreach(var c in weapon.GetComponentsInChildren<Collider>(true))Destroy(c);
            var renderers=weapon.GetComponentsInChildren<Renderer>(true);var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            weapon.localScale*=length/Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
            weapon.localPosition=Vector3.zero;weapon.localRotation=Quaternion.Euler(0,90,-90);weapon.gameObject.SetActive(false);
            leftHand=animator.GetBoneTransform(HumanBodyBones.LeftHand);rightHand=animator.GetBoneTransform(HumanBodyBones.RightHand);
        }
        public void SetSword(bool value)
        {
            if(Animator==null)return;
            if(value)SetRanged(false);
            if(value&&!usingSword){previousController=Animator.runtimeAnimatorController;Animator.runtimeAnimatorController=swordController;usingSword=true;}
            if(!value&&usingSword){Animator.runtimeAnimatorController=previousController;usingSword=false;}
            SwordVisible=value;if(weapon!=null)weapon.gameObject.SetActive(value);Animator.speed=1;
        }
        public void Slash(){if(!SwordVisible)return;Animator.SetTrigger("attack");}
        public void Walk(bool value){if(usingSword)Animator.SetBool("isWalking",value);}
        public void Restore()
        {
            Traversal=Motion.Idle;SetRanged(false);SetSword(false);
            if(Animator!=null)Animator.transform.localRotation=modelRotation;
        }
        void LateUpdate()
        {
            if(Animator==null)return;
            if(Traversal==Motion.Idle)
            {
                if(ranged&&rangedBow!=null)
                {
                    rangedBow.SetPositionAndRotation(transform.position+Vector3.up*10+transform.forward*4,transform.rotation);
                    QuestRowboat.SolveArm(bones[0],bones[2],leftHand,rangedBow.position,bones[0].position-transform.right*4);
                    QuestRowboat.SolveArm(bones[1],bones[3],rightHand,rangedBow.position-transform.forward*2,bones[1].position+transform.right*4);
                }
                return;
            }
            float phase=Time.time*(Traversal==Motion.Swim?5:4);
            Animator.transform.localRotation=modelRotation*(Traversal==Motion.Swim?Quaternion.Euler(72,0,0):Quaternion.identity);
            for(int i=0;i<bones.Length;i++)
            {
                float side=i%2==0?1:-1;
                var q=Traversal==Motion.Swim?
                    Quaternion.Euler(i<2?Mathf.Sin(phase+side*Mathf.PI*.5f)*85:i<4?-25:Mathf.Sin(phase*1.6f+side*Mathf.PI*.5f)*24,0,i<2?side*25:0):
                    Quaternion.Euler(i<2?-105+Mathf.Sin(phase+side*Mathf.PI*.5f)*24:i<4?-48:i<6?-35+Mathf.Sin(phase+side*Mathf.PI*.5f)*30:50,0,0);
                if(bones[i]!=null)bones[i].localRotation=rotations[i]*q;
            }
            if(Traversal==Motion.Climb)
            {
                var grip=transform.position+Vector3.up*(actorHeight*.94f)+transform.forward*(actorHeight*.35f);
                QuestRowboat.SolveArm(bones[0],bones[2],leftHand,grip-transform.right*(actorHeight*.18f)+Vector3.up*(Mathf.Sin(phase)*actorHeight*.07f),bones[0].position-transform.right*actorHeight*.4f);
                QuestRowboat.SolveArm(bones[1],bones[3],rightHand,grip+transform.right*(actorHeight*.18f)-Vector3.up*(Mathf.Sin(phase)*actorHeight*.07f),bones[1].position+transform.right*actorHeight*.4f);
            }
        }
        public void FinishTraversal(){Traversal=Motion.Idle;if(Animator!=null)Animator.transform.localRotation=modelRotation;}
        public void SetRanged(bool value)
        {
            ranged=value;
            if(value&&rangedBow==null)
            {
                rangedBow=new GameObject("DaiViet_Boarding_Bow").transform;rangedBow.SetParent(transform,false);var scale=transform.lossyScale;rangedBow.localScale=new Vector3(1/scale.x,1/scale.y,1/scale.z);
                var line=rangedBow.gameObject.AddComponent<LineRenderer>();line.sharedMaterial=weapon.GetComponentInChildren<Renderer>().sharedMaterial;line.useWorldSpace=false;line.widthMultiplier=.4f;line.positionCount=9;
                for(int i=0;i<9;i++){float y=(i/8f-.5f)*10;line.SetPosition(i,new Vector3(0,y,2*(1-y*y/25)));}
            }
            if(rangedBow!=null)rangedBow.gameObject.SetActive(value);
        }
    }
}
