using UnityEngine;

namespace BeachBoatStory
{
    [DefaultExecutionOrder(250)]
    public sealed class EnemyDeckGuard : MonoBehaviour
    {
        public EnemyShip ship;
        public Transform visual;
        public int waveIndex;
        public int Health {get;private set;}=2;
        public bool IsAlive=>Health>0&&ship!=null&&!ship.isDestroyed&&gameObject.activeInHierarchy;
        public Vector3 AimPoint=>transform.position+Vector3.up*9;
        Animator animator;
        Quaternion restRotation;
        float fall;
        bool initialized;
        Transform bow,leftUpper,leftFore,leftHand,rightUpper,rightFore,rightHand;
        LineRenderer bowString;
        float draw;
        void Awake()=>Cache();
        void Cache()
        {
            if(initialized)return;animator=GetComponentInChildren<Animator>(true);restRotation=visual.localRotation;initialized=true;
            leftUpper=animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);leftFore=animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);leftHand=animator.GetBoneTransform(HumanBodyBones.LeftHand);
            rightUpper=animator.GetBoneTransform(HumanBodyBones.RightUpperArm);rightFore=animator.GetBoneTransform(HumanBodyBones.RightLowerArm);rightHand=animator.GetBoneTransform(HumanBodyBones.RightHand);
            bow=new GameObject("Cung_Xa_Thu").transform;bow.SetParent(transform,false);
            var stave=bow.gameObject.AddComponent<LineRenderer>();stave.sharedMaterial=RiverPlayerCombat.Instance.bowMaterial;stave.useWorldSpace=false;stave.widthMultiplier=.45f;stave.positionCount=9;
            for(int i=0;i<9;i++){float y=(i/8f-.5f)*11;stave.SetPosition(i,new Vector3(0,y,2*(1-y*y/30.25f)));}
            bowString=new GameObject("Day_Cung").AddComponent<LineRenderer>();bowString.transform.SetParent(bow,false);bowString.sharedMaterial=stave.sharedMaterial;bowString.useWorldSpace=false;bowString.widthMultiplier=.1f;bowString.positionCount=3;
            bowString.SetPositions(new[]{new Vector3(0,-5.5f,0),Vector3.zero,new Vector3(0,5.5f,0)});
        }
        void OnEnable()=>ResetGuard();
        public void ResetGuard()
        {
            Cache();
            Health=2;fall=0;if(visual==null)return;
            bow.gameObject.SetActive(true);
            visual.localRotation=restRotation;
            if(animator!=null)animator.enabled=true;
            var col=GetComponent<Collider>();if(col!=null)col.enabled=true;
        }
        public bool Hit(int damage)
        {
            if(!IsAlive||damage<=0)return false;
            Health=Mathf.Max(0,Health-damage);
            BeachBoatSoundscape.Instance?.PlayHit();
            if(Health==0)
            {
                if(animator!=null)animator.enabled=false;
                GetComponent<Collider>().enabled=false;
                bow.gameObject.SetActive(false);
                RiverPlayerCombat.Instance?.RegisterKill(this);
            }
            return true;
        }
        void LateUpdate()
        {
            if(Health>0)
            {
                var combat=RiverPlayerCombat.Instance;if(combat==null||!combat.BattleActive)return;
                var player=BeachBoatQuestSession.Instance.player.transform.position;var direction=player-transform.position;direction.y=0;
                if(direction.sqrMagnitude<500*500&&direction.sqrMagnitude>1)transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(direction),Time.deltaTime*90);
                draw=Mathf.MoveTowards(draw,combat.ThreatGuard==this?1:0,Time.deltaTime*2);
                bow.SetPositionAndRotation(transform.position+Vector3.up*10+transform.forward*4,transform.rotation);
                bowString.SetPosition(1,new Vector3(0,0,-draw*2));
                QuestRowboat.SolveArm(leftUpper,leftFore,leftHand,bow.position,leftUpper.position-transform.right*3-Vector3.up*2);
                QuestRowboat.SolveArm(rightUpper,rightFore,rightHand,bow.position-transform.forward*(1+draw*2),rightUpper.position+transform.right*4);
                return;
            }
            fall=Mathf.MoveTowards(fall,1,Time.deltaTime*2.5f);
            visual.localRotation=restRotation*Quaternion.Euler(88*Mathf.SmoothStep(0,1,fall),0,8*fall);
        }
    }
}
