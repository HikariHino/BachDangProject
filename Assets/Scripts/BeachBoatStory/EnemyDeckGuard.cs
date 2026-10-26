using UnityEngine;

namespace BeachBoatStory
{
    [DefaultExecutionOrder(250)]
    public sealed class EnemyDeckGuard : MonoBehaviour
    {
        public EnemyShip ship;
        public Transform visual;
        public int waveIndex;
        public const int MaximumHealth=2;
        public int Health {get;private set;}=MaximumHealth;
        public float HealthFraction=>Health/(float)MaximumHealth;
        public bool IsOnWreck {get;private set;}
        public bool IsIndependentBoarder;
        public bool IsInMelee {get;private set;}
        public void SetMelee(bool value){IsInMelee=value;if(bow!=null)bow.gameObject.SetActive(!value&&Health>0);}
        public bool IsAlive=>Health>0&&ship!=null&&(!ship.isDestroyed||IsOnWreck||IsIndependentBoarder)&&gameObject.activeInHierarchy;
        public float BodyHeight {get;private set;}=17.6f;
        public Vector3 AimPoint=>transform.position+Vector3.up*(BodyHeight*.55f);
        static readonly HumanBodyBones[] heightChain={HumanBodyBones.LeftFoot,HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftUpperLeg,HumanBodyBones.Hips,
                HumanBodyBones.Spine,HumanBodyBones.Chest,HumanBodyBones.Neck,HumanBodyBones.Head};
        public static float RigHeight(Animator animator)
        {
            float length=0;Transform previous=null;
            foreach(var bone in heightChain){var next=animator.GetBoneTransform(bone);if(next==null)continue;if(previous!=null)length+=Vector3.Distance(previous.position,next.position);previous=next;}
            return length;
        }
        public void MatchPlayerSize()
        {
            Cache();if(animator.isActiveAndEnabled)animator.Update(0);ActorSolidBody.Ensure(gameObject,animator);var player=BeachBoatQuestSession.Instance.player;var reference=player.GetComponentInChildren<Animator>();
            float oldFeet=Mathf.Min(animator.GetBoneTransform(HumanBodyBones.LeftFoot).position.y,animator.GetBoneTransform(HumanBodyBones.RightFoot).position.y);
            visual.localScale*=RigHeight(reference)/Mathf.Max(.01f,RigHeight(animator));
            float newFeet=Mathf.Min(animator.GetBoneTransform(HumanBodyBones.LeftFoot).position.y,animator.GetBoneTransform(HumanBodyBones.RightFoot).position.y);
            visual.position+=Vector3.up*(oldFeet-newFeet);
            BodyHeight=player.GetComponent<CapsuleCollider>().height*Mathf.Abs(player.transform.lossyScale.y);
            if(GetComponent<Collider>() is CapsuleCollider capsule){capsule.height=BodyHeight/transform.lossyScale.y;capsule.radius=BodyHeight*.16f/Mathf.Abs(transform.lossyScale.x);capsule.center=Vector3.up*(capsule.height*.5f);}
            bow.localScale=Vector3.one*(BodyHeight/17.6f);
        }
        Animator animator;
        Quaternion restRotation;
        float fall;
        bool initialized;
        int sizeCheckFrames;float nextSizeCheck;
        Transform bow,leftUpper,leftFore,leftHand,rightUpper,rightFore,rightHand;
        LineRenderer bowString;
        float draw;
        Transform deckParent;
        Vector3 deckPosition,deckScale;
        Quaternion deckRotation;
        void Awake()=>Cache();
        void Cache()
        {
            if(initialized)return;deckParent=transform.parent;deckPosition=transform.localPosition;deckRotation=transform.localRotation;deckScale=transform.localScale;
            animator=GetComponentInChildren<Animator>(true);restRotation=visual.localRotation;initialized=true;
            leftUpper=animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);leftFore=animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);leftHand=animator.GetBoneTransform(HumanBodyBones.LeftHand);
            rightUpper=animator.GetBoneTransform(HumanBodyBones.RightUpperArm);rightFore=animator.GetBoneTransform(HumanBodyBones.RightLowerArm);rightHand=animator.GetBoneTransform(HumanBodyBones.RightHand);
            bow=new GameObject("Cung_Xa_Thu").transform;bow.SetParent(transform,false);
            var stave=bow.gameObject.AddComponent<LineRenderer>();stave.sharedMaterial=RiverPlayerCombat.Instance.bowMaterial;stave.useWorldSpace=false;stave.widthMultiplier=.45f;stave.positionCount=9;
            for(int i=0;i<9;i++){float y=(i/8f-.5f)*11;stave.SetPosition(i,new Vector3(0,y,2*(1-y*y/30.25f)));}
            bowString=new GameObject("Day_Cung").AddComponent<LineRenderer>();bowString.transform.SetParent(bow,false);bowString.sharedMaterial=stave.sharedMaterial;bowString.useWorldSpace=false;bowString.widthMultiplier=.1f;bowString.positionCount=3;
            bowString.SetPositions(new[]{new Vector3(0,-5.5f,0),Vector3.zero,new Vector3(0,5.5f,0)});
        }
        void OnEnable(){sizeCheckFrames=3;if(!IsOnWreck)ResetGuard();}
        public void MoveToWreck(Transform raft,Vector3 position)
        {
            Cache();IsOnWreck=true;transform.SetParent(raft,true);transform.localPosition=position;
            transform.rotation=Quaternion.Euler(0,transform.eulerAngles.y,0);
        }
        public void RestoreDeck()
        {
            Cache();IsOnWreck=false;transform.SetParent(deckParent,false);
            transform.localPosition=deckPosition;transform.localRotation=deckRotation;transform.localScale=deckScale;
            gameObject.SetActive(true);
        }
        public void DefeatFromVolley()
        {
            if(Health<=0)return;FinishDeathPose();Health=0;
            if(animator!=null)animator.enabled=false;
            GetComponent<Collider>().enabled=false;var body=GetComponent<ActorSolidBody>();if(body?.SolidCollider!=null)body.SolidCollider.enabled=false;bow.gameObject.SetActive(false);
        }
        void FinishDeathPose()
        {
            var pose=GetComponent<RiverFighterPose>();if(pose!=null){pose.FinishTraversal();pose.SetSword(false);pose.SetRanged(false);}
            fall=1;visual.localRotation=restRotation*Quaternion.Euler(88,0,8);
        }
        public void ResetGuard()
        {
            Cache();
            Health=2;fall=0;IsInMelee=false;if(visual==null)return;
            bow.gameObject.SetActive(true);
            visual.localRotation=restRotation;
            if(animator!=null)animator.enabled=true;
            var col=GetComponent<Collider>();if(col!=null)col.enabled=true;
        }
        public bool Hit(int damage,bool creditPlayer=true,CombatImpactKind kind=CombatImpactKind.Sword)
        {
            if(!IsAlive||damage<=0)return false;
            int previous=Health;Health=Mathf.Max(0,Health-damage);
            if(creditPlayer)CombatDamageNumbers.Show(AimPoint,previous-Health);
            CombatHitFeedback.Show(AimPoint,kind,creditPlayer);
            if(Health==0)
            {
                FinishDeathPose();
                if(animator!=null)animator.enabled=false;
                GetComponent<Collider>().enabled=false;var body=GetComponent<ActorSolidBody>();if(body?.SolidCollider!=null)body.SolidCollider.enabled=false;
                bow.gameObject.SetActive(false);
                if(creditPlayer)RiverPlayerCombat.Instance?.RegisterKill(this);else RiverPlayerCombat.Instance?.RegisterAlliedKill(this);
            }
            return true;
        }
        public void RestoreSave(BachDangSaveData.Guard data)
        {
            Cache();RestoreDeck();ResetGuard();
            foreach(var candidate in EnemyFleetDirector.Instance.enemyShips)if(candidate.name==data.ship){ship=candidate;break;}
            IsIndependentBoarder=data.independent;
            if(data.independent)transform.SetParent(BeachBoatQuestSession.Instance.transform,true);
            transform.SetPositionAndRotation(data.position,data.rotation);gameObject.SetActive(data.active);
            Health=Mathf.Clamp(data.health,0,2);fall=Health==0?1:0;
            GetComponent<Collider>().enabled=Health>0;animator.enabled=Health>0;
            SetMelee(data.independent);if(Health==0){FinishDeathPose();animator.enabled=false;bow.gameObject.SetActive(false);if(data.floatingCorpse)RiverCorpse.Ensure(this);}
        }
        public void FitStandingOnDeck()
        {
            MatchPlayerSize();
            float feet=Mathf.Min(animator.GetBoneTransform(HumanBodyBones.LeftFoot).position.y,animator.GetBoneTransform(HumanBodyBones.RightFoot).position.y);
            visual.position+=Vector3.up*(transform.position.y+.4f-feet);
        }
        void LateUpdate()
        {
            // Inactive pooled avatars have not evaluated their humanoid retargeting yet.
            // Validate after evaluation too, including restored and boarding characters.
            if((sizeCheckFrames>0||Time.time>=nextSizeCheck)&&Health>0&&animator!=null&&animator.isActiveAndEnabled)
            {
                sizeCheckFrames--;nextSizeCheck=Time.time+.5f;
                var reference=BeachBoatQuestSession.Instance.player.GetComponentInChildren<Animator>();
                float ratio=RigHeight(reference)/Mathf.Max(.01f,RigHeight(animator));
                if(ratio<.96f||ratio>1.04f)MatchPlayerSize();
            }
            if(IsOnWreck){var p=transform.parent.position;p.y=TideSystem.CurrentWaterHeight+1+Mathf.Sin(Time.time*1.5f)*.15f;transform.parent.position=p;}
            if(Health>0)
            {
                if(IsInMelee)return;
                var combat=RiverPlayerCombat.Instance;if(combat==null||!combat.BattleActive)return;
                var player=BeachBoatQuestSession.Instance.player.transform.position;var direction=player-transform.position;direction.y=0;
                if(direction.sqrMagnitude<500*500&&direction.sqrMagnitude>1)transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(direction),Time.deltaTime*90);
                draw=Mathf.MoveTowards(draw,combat.ThreatGuard==this?1:0,Time.deltaTime*2);
                float scale=BodyHeight/17.6f;
                bow.SetPositionAndRotation(transform.position+Vector3.up*(BodyHeight*.57f)+transform.forward*(4*scale),transform.rotation);
                bowString.SetPosition(1,new Vector3(0,0,-draw*2));
                QuestRowboat.SolveArm(leftUpper,leftFore,leftHand,bow.position,leftUpper.position-transform.right*(3*scale)-Vector3.up*(2*scale));
                QuestRowboat.SolveArm(rightUpper,rightFore,rightHand,bow.position-transform.forward*((1+draw*2)*scale),rightUpper.position+transform.right*(4*scale));
                return;
            }
            fall=Mathf.MoveTowards(fall,1,Time.deltaTime*2.5f);
            visual.localRotation=restRotation*Quaternion.Euler(88*Mathf.SmoothStep(0,1,fall),0,8*fall);
        }
    }
}
