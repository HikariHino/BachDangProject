using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Shared sword rig and bounded swim/climb poses on existing humanoid skeletons.</summary>
    [DefaultExecutionOrder(600)]
    public sealed class RiverFighterPose : MonoBehaviour
    {
        public enum Motion { Idle, Swim, Climb }
        Motion traversal;
        public Motion Traversal
        {
            get=>traversal;
            set
            {
                if(value==traversal)return;
                if(traversal==Motion.Idle&&value!=Motion.Idle)BeginTraversalRig();
                if(value==Motion.Idle)RestoreTraversalRig();
                traversal=value;motionTime=0;lastStroke=-1;
            }
        }
        public bool SwimMoving {get;set;}=true;
        public bool SwimFast {get;set;}
        public float ClimbProgress {get;set;}
        public Vector3 ClimbRail {get;set;}
        public int SwimStrokes {get;private set;}
        public Animator Animator {get;private set;}
        public bool SwordVisible {get;private set;}
        public bool OwnsUpperBody=>SwordVisible||ranged||Traversal!=Motion.Idle;
        public RuntimeAnimatorController swordController;
        Transform weapon;
        Transform indexKnuckle,littleKnuckle,indexMiddle,littleMiddle;
        static readonly HumanBodyBones[] swordFingers={HumanBodyBones.RightIndexProximal,HumanBodyBones.RightMiddleProximal,HumanBodyBones.RightRingProximal,HumanBodyBones.RightLittleProximal};
        float swordScale;
        Vector3 gripInMesh;
        public Transform Weapon=>weapon;
        public Vector3 GripPosition {get;private set;}
        public Vector3 BladeDirection {get;private set;}
        Transform rangedBow,leftHand,rightHand,rightPalmEnd;
        LineRenderer rangedString;Transform rangedArrow;float bowBegan=-100;Vector3 bowTarget;
        HumanoidHandGrip leftGrip,rightGrip;BowGripProfile bowGrip;
        public Vector3 RangedGrip=>leftGrip.Contact;
        public Vector3 RangedDraw=>rightGrip.Contact;
        public int BowShots {get;private set;}
        public float BowDrawAmount=>Mathf.Clamp01((Time.time-bowBegan)/.7f)*(Time.time-bowBegan<.73f?1:Mathf.Clamp01(1-(Time.time-bowBegan-.73f)/.12f));
        public Vector3 BowMuzzle=>rangedBow!=null?rangedBow.position+rangedBow.forward*3:transform.position+Vector3.up*12;
        public void BeginBowShot(Vector3 target){SetSword(false);SetRanged(true);bowTarget=target;var face=target-transform.position;face.y=0;if(face.sqrMagnitude>.01f)transform.rotation=Quaternion.LookRotation(face);bowBegan=Time.time;BowShots++;}
        bool ranged;
        float actorHeight;
        Transform[] bones;
        Quaternion[] rotations;
        Quaternion modelRotation;
        RuntimeAnimatorController previousController;
        bool usingSword;
        Transform hips,neck,head,leftFoot,rightFoot;
        Transform[] traversalBones;
        Quaternion[] traversalRotations;
        Vector3[] traversalPositions;
        RuntimeAnimatorController traversalController;
        bool traversalAnimatorEnabled;
        float motionTime;
        int lastStroke=-1;
        int sizeChecks;
        public void MatchFriendlySize()
        {
            sizeChecks=3;MatchFriendlyStature();
        }
        void MatchFriendlyStature()
        {
            var reference=BeachBoatQuestSession.Instance.player.GetComponentInChildren<Animator>();
            if(Animator==reference)return;
            float feet=Mathf.Min(leftFoot.position.y,rightFoot.position.y);
            Animator.transform.localScale*=EnemyDeckGuard.RigHeight(reference)/Mathf.Max(.01f,EnemyDeckGuard.RigHeight(Animator));
            Animator.transform.position+=Vector3.up*(feet-Mathf.Min(leftFoot.position.y,rightFoot.position.y));
            var scale=weapon.parent.lossyScale;weapon.localScale=new Vector3(swordScale/scale.x,swordScale/scale.y,swordScale/scale.z);
        }
        public void Initialize(Animator animator,GameObject sword,RuntimeAnimatorController controller,float length)
        {
            Animator=animator;swordController=controller;modelRotation=animator.transform.localRotation;actorHeight=length*1.6f;
            var ids=new[]{HumanBodyBones.LeftUpperArm,HumanBodyBones.RightUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.RightLowerArm,HumanBodyBones.LeftUpperLeg,HumanBodyBones.RightUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.RightLowerLeg};
            bones=new Transform[ids.Length];rotations=new Quaternion[ids.Length];for(int i=0;i<ids.Length;i++){bones[i]=animator.GetBoneTransform(ids[i]);rotations[i]=bones[i].localRotation;}
            weapon=Instantiate(sword,animator.GetBoneTransform(HumanBodyBones.RightHand),false).transform;weapon.name="River_Combat_Sword";
            foreach(var c in weapon.GetComponentsInChildren<Collider>(true))Destroy(c);
            foreach(var lod in weapon.GetComponentsInChildren<LODGroup>(true))lod.enabled=false;
            var bounds=weapon.GetComponent<MeshFilter>().sharedMesh.bounds;
            swordScale=length/bounds.size.y;
            // This mesh is centred on the blade, with the handle in its negative-Y end.
            gripInMesh=new Vector3(bounds.center.x,bounds.min.y+bounds.size.y*.105f,bounds.center.z);
            var handScale=weapon.parent.lossyScale;
            weapon.localScale=new Vector3(swordScale/handScale.x,swordScale/handScale.y,swordScale/handScale.z);
            weapon.localPosition=Vector3.zero;weapon.localRotation=Quaternion.identity;weapon.gameObject.SetActive(false);
            leftHand=animator.GetBoneTransform(HumanBodyBones.LeftHand);rightHand=animator.GetBoneTransform(HumanBodyBones.RightHand);
            foreach(Transform child in rightHand)if(child!=weapon){rightPalmEnd=child;break;}
            indexKnuckle=animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);littleKnuckle=animator.GetBoneTransform(HumanBodyBones.RightLittleProximal);
            indexMiddle=animator.GetBoneTransform(HumanBodyBones.RightIndexIntermediate);littleMiddle=animator.GetBoneTransform(HumanBodyBones.RightLittleIntermediate);
            hips=animator.GetBoneTransform(HumanBodyBones.Hips);neck=animator.GetBoneTransform(HumanBodyBones.Neck);head=animator.GetBoneTransform(HumanBodyBones.Head);
            leftFoot=animator.GetBoneTransform(HumanBodyBones.LeftFoot);rightFoot=animator.GetBoneTransform(HumanBodyBones.RightFoot);
            leftGrip=new HumanoidHandGrip(animator,true);rightGrip=new HumanoidHandGrip(animator,false);
            var profile=BowGripProfile.Load();
            if(profile!=null)foreach(var skin in animator.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if(skin.sharedMesh==profile.source||skin.sharedMesh==profile.posed){bowGrip=profile;break;}
        }
        public void SetSword(bool value)
        {
            if(Animator==null)return;
            if(value){SetRanged(false);if(Traversal==Motion.Idle)Animator.enabled=true;Animator.applyRootMotion=false;}
            if(value&&!usingSword){previousController=Animator.runtimeAnimatorController;Animator.runtimeAnimatorController=swordController;usingSword=true;Animator.SetBool("isWalking",false);Animator.SetBool("isRunning",false);Animator.SetInteger("Pose",0);Animator.Play("sword_idle",0,0);if(Animator.gameObject.activeInHierarchy){Animator.Update(0);MatchFriendlyStature();}}
            if(value&&Traversal==Motion.Idle&&Animator.runtimeAnimatorController!=swordController)Animator.runtimeAnimatorController=swordController;
            if(!value&&usingSword){Animator.runtimeAnimatorController=previousController;usingSword=false;}
            SwordVisible=value;if(weapon!=null)weapon.gameObject.SetActive(value);Animator.speed=1;
            if(!value&&!ranged)HumanoidGripMesh.Restore(Animator);
        }
        public void Slash(){if(!SwordVisible)return;Animator.enabled=true;Animator.SetBool("isWalking",false);Animator.SetBool("isRunning",false);Animator.SetTrigger("attack");}
        public void Walk(bool value){if(usingSword)Animator.SetBool("isWalking",value);}
        public void Restore()
        {
            Traversal=Motion.Idle;SetRanged(false);SetSword(false);
            if(Animator!=null)HumanoidGripMesh.Restore(Animator);
        }
        void LateUpdate()
        {
            if(Animator==null)return;
            if(sizeChecks>0&&Traversal==Motion.Idle){sizeChecks--;MatchFriendlyStature();}
            if(SwordVisible){FitSwordGuard();FitSwordToGrip();CloseSwordHand();HumanoidGripMesh.Apply(Animator,leftGrip,rightGrip,0,1);}
            if(Traversal==Motion.Idle)
            {
                if(ranged&&rangedBow!=null)
                {
                    float h=EnemyDeckGuard.RigHeight(Animator),elapsed=Time.time-bowBegan,raise=Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/.18f))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.9f,1.3f,elapsed)));
                    var direction=bowTarget.sqrMagnitude>.01f?(bowTarget-head.position).normalized:transform.forward;
                    float pitch=Mathf.Clamp(Mathf.Asin(direction.y)*Mathf.Rad2Deg,-45,55);direction=Quaternion.AngleAxis(-pitch,transform.right)*transform.forward;
                    var support=bones[0].position+direction*(h*.29f)+Vector3.up*(h*(raise*.04f-.22f*(1-raise)));
                    rangedBow.SetPositionAndRotation(support,Quaternion.LookRotation(direction));var draw=Vector3.Lerp(support-direction*(h*.08f),head.position+transform.right*(h*.065f)+transform.forward*(h*.04f)-Vector3.up*(h*.025f),BowDrawAmount);
                    var profile=bowGrip;
                    if(profile!=null){profile.Align(bones[0],bones[2],leftHand,support,bones[0].position-transform.right*h*.25f-Vector3.up*h*.07f,true);profile.Align(bones[1],bones[3],rightHand,draw,bones[1].position+transform.right*h*.35f,false);}
                    else{leftGrip.Align(bones[0],bones[2],support,bones[0].position-transform.right*h*.25f-Vector3.up*h*.07f,Vector3.up);rightGrip.Align(bones[1],bones[3],draw,bones[1].position+transform.right*h*.35f,Vector3.up);}
                    // Attach the stave and string to the solved contacts, rather
                    // than leaving either one at an unreachable ideal IK goal.
                    rangedBow.position=profile!=null?leftHand.TransformPoint(profile.leftCenter):leftGrip.Contact;
                    var actualDraw=profile!=null?rightHand.TransformPoint(profile.rightCenter):rightGrip.Contact;
                    rangedString.SetPosition(1,rangedBow.InverseTransformPoint(actualDraw));rangedArrow.gameObject.SetActive(elapsed>=0&&elapsed<.73f);rangedArrow.SetPositionAndRotation(actualDraw+direction*3,Quaternion.FromToRotation(Vector3.up,direction));
                    HumanoidGripMesh.Apply(Animator,leftGrip,rightGrip,1,Mathf.Clamp01(BowDrawAmount*2));
                }
                return;
            }
            EvaluateTraversal(Time.deltaTime);
        }
        public void WarmTraversal(){if(Traversal==Motion.Idle&&traversalBones==null){BeginTraversalRig();RestoreTraversalRig();}}
        void BeginTraversalRig()
        {
            if(Animator==null)return;
            traversalController=Animator.runtimeAnimatorController;traversalAnimatorEnabled=Animator.enabled;
            Animator.enabled=true;Animator.runtimeAnimatorController=Resources.Load<RuntimeAnimatorController>("Player_Neutral");Animator.Update(0);MatchFriendlyStature();
            if(traversalBones!=null){ResetTraversalBones();Animator.enabled=false;SwimMoving=true;ClimbProgress=0;return;}
            var list=new System.Collections.Generic.List<Transform>();
            for(int i=0;i<(int)HumanBodyBones.LastBone;i++){var bone=Animator.GetBoneTransform((HumanBodyBones)i);if(bone!=null)list.Add(bone);}
            traversalBones=list.ToArray();traversalRotations=new Quaternion[list.Count];traversalPositions=new Vector3[list.Count];
            for(int i=0;i<list.Count;i++){traversalRotations[i]=list[i].localRotation;traversalPositions[i]=list[i].localPosition;}
            Animator.enabled=false;SwimMoving=true;ClimbProgress=0;
        }
        void RestoreTraversalRig()
        {
            if(traversalBones==null)return;
            ResetTraversalBones();Animator.runtimeAnimatorController=traversalController;Animator.enabled=traversalAnimatorEnabled;
            // Keep the neutral bone cache for later wreck/swim transitions.
        }
        void ResetTraversalBones()
        {
            for(int i=0;i<traversalBones.Length;i++)
            {traversalBones[i].localPosition=traversalPositions[i];traversalBones[i].localRotation=traversalRotations[i];}
        }
        public void EvaluateTraversal(float dt)
        {
            if(Traversal==Motion.Idle||traversalBones==null)return;
            ResetTraversalBones();motionTime+=Mathf.Max(0,dt);
            var forward=transform.forward;forward.y=0;forward.Normalize();var right=Vector3.Cross(Vector3.up,forward);float h=actorHeight;
            if(Traversal==Motion.Swim)
            {
                float rate=SwimMoving?(SwimFast?1.05f:.72f):.48f,cycle=motionTime*rate,t=cycle-Mathf.Floor(cycle);
                var axis=(head.position-hips.position).normalized;
                var desired=SwimMoving?(forward*.96f+Vector3.up*.28f).normalized:(forward*.20f+Vector3.up*.98f).normalized;
                hips.rotation=Quaternion.Slerp(Quaternion.identity,Quaternion.FromToRotation(axis,desired),Mathf.SmoothStep(0,1,motionTime/.25f))*hips.rotation;
                var p=hips.position;p.y=TideSystem.CurrentWaterHeight-(SwimMoving?.06f:.30f)*h+Mathf.Sin(t*Mathf.PI*2)*h*.012f;hips.position=p;
                if(neck!=null&&head!=null)neck.rotation=Quaternion.FromToRotation(head.position-neck.position,(forward*.38f+Vector3.up*.92f).normalized)*neck.rotation;
                float reach,sweep,depth;
                if(t<.25f){float u=t/.25f;reach=Mathf.Lerp(.12f,.34f,u);sweep=Mathf.Lerp(.11f,.13f,u);depth=-.025f;}
                else if(t<.65f){float u=(t-.25f)/.4f;reach=Mathf.Lerp(.34f,-.14f,u);sweep=.13f+Mathf.Sin(u*Mathf.PI)*.20f;depth=-.025f-Mathf.Sin(u*Mathf.PI)*.10f;}
                else{float u=(t-.65f)/.35f;reach=Mathf.Lerp(-.14f,.12f,u);sweep=Mathf.Lerp(.16f,.11f,u);depth=Mathf.Lerp(-.07f,-.025f,u);}
                if(!SwimMoving){reach=.15f+Mathf.Sin(t*Mathf.PI*2)*.05f;sweep=.23f;depth=-.07f;}
                for(int side=0;side<2;side++)
                {
                    float sign=side==0?-1:1;var shoulder=bones[side].position;
                    var grip=shoulder+forward*(reach*h)+right*(sign*sweep*h)+Vector3.up*(depth*h);
                    QuestRowboat.SolveArm(bones[side],bones[side+2],side==0?leftHand:rightHand,grip,shoulder+right*(sign*h*.4f)-Vector3.up*h*.15f);
                    float kick=Mathf.Sin(t*Mathf.PI*2+side*Mathf.PI);
                    var upper=bones[side+4];var lower=bones[side+6];var ankle=side==0?leftFoot:rightFoot;
                    float legLength=Vector3.Distance(upper.position,lower.position)+Vector3.Distance(lower.position,ankle.position);
                    var foot=SwimMoving?upper.position-forward*(legLength*.94f)+right*(sign*h*.025f)+Vector3.up*(kick*h*.018f-h*.025f):upper.position-Vector3.up*(legLength*.88f)+right*(sign*h*.04f)+forward*(kick*h*.045f);
                    // A nearly extended flutter kick and a downward knee plane
                    // avoid the old lateral IK fold that snapped heels behind the back.
                    QuestRowboat.SolveArm(upper,lower,ankle,foot,upper.position-forward*h*.13f-Vector3.up*h*.22f+right*(sign*h*.035f));
                    var toes=Animator.GetBoneTransform(side==0?HumanBodyBones.LeftToes:HumanBodyBones.RightToes);
                    if(toes!=null&&SwimMoving)ankle.rotation=Quaternion.FromToRotation(toes.position-ankle.position,-forward+Vector3.down*.08f)*ankle.rotation;
                }
                int stroke=Mathf.FloorToInt(cycle+.35f);
                if(stroke>lastStroke)
                {
                    if(lastStroke>=0){SwimStrokes++;if(BeachBoatQuestSession.Instance!=null&&gameObject==BeachBoatQuestSession.Instance.player)BeachBoatSoundscape.Instance?.PlaySwimStroke(SwimMoving);}
                    lastStroke=stroke;
                }
            }
            else
            {
                float t=Mathf.Clamp01(ClimbProgress),pull=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.32f,.78f,t));
                var rail=ClimbRail.sqrMagnitude>.1f?ClimbRail:transform.position+forward*h*.28f+Vector3.up*h*.8f;
                var axis=(head.position-hips.position).normalized;var desired=(Vector3.up+forward*Mathf.Lerp(.05f,.45f,pull)).normalized;
                hips.rotation=Quaternion.FromToRotation(axis,desired)*hips.rotation;
                for(int side=0;side<2;side++)
                {
                    float sign=side==0?-1:1;
                    var grip=rail+right*(sign*h*.17f)+forward*(Mathf.SmoothStep(0,1,Mathf.InverseLerp(.72f,1,t))*h*.18f);
                    QuestRowboat.SolveArm(bones[side],bones[side+2],side==0?leftHand:rightHand,grip,bones[side].position+right*(sign*h*.35f));
                    var kneeLift=side==0?Mathf.Sin(Mathf.Clamp01(t/.7f)*Mathf.PI):Mathf.Sin(Mathf.Clamp01((t-.2f)/.8f)*Mathf.PI);
                    var foot=transform.position+right*(sign*h*.12f)+forward*(h*.2f*kneeLift)+Vector3.up*(h*.25f*kneeLift);
                    QuestRowboat.SolveArm(bones[side+4],bones[side+6],side==0?leftFoot:rightFoot,foot,hips.position+right*(sign*h*.2f)+forward*h*.4f);
                }
            }
        }
        public void FitSwordToGrip()
        {
            if(weapon==null||rightHand==null)return;
            var scale=weapon.parent.lossyScale;weapon.localScale=new Vector3(swordScale/scale.x,swordScale/scale.y,swordScale/scale.z);
            // Follow the palm and its thumb side, rather than guessing bone Euler axes.
            var across=indexKnuckle!=null&&littleKnuckle!=null?indexKnuckle.position-littleKnuckle.position:rightGrip.ThumbAxis;
            BladeDirection=across.normalized;
            var knuckles=indexKnuckle!=null&&littleKnuckle!=null?(indexKnuckle.position+littleKnuckle.position)*.5f:rightPalmEnd!=null?rightPalmEnd.position:rightHand.position;
            GripPosition=indexMiddle!=null&&littleMiddle!=null?(knuckles+(indexMiddle.position+littleMiddle.position)*.5f)*.5f:rightGrip.Contact;
            var along=knuckles-rightHand.position;
            if(along.sqrMagnitude<.000001f)along=rightHand.forward;
            var normal=Vector3.Cross(along,BladeDirection).normalized;
            if(normal.sqrMagnitude<.5f)normal=rightHand.forward;
            var rotation=Quaternion.LookRotation(normal,BladeDirection);
            weapon.SetPositionAndRotation(GripPosition-rotation*(gripInMesh*swordScale),rotation);
        }
        void FitSwordGuard()
        {
            if(Traversal!=Motion.Idle||!Animator.enabled||!Animator.GetCurrentAnimatorStateInfo(0).IsName("sword_idle")||Animator.IsInTransition(0))return;
            float h=EnemyDeckGuard.RigHeight(Animator);
            var forward=transform.forward;var right=transform.right;
            var grip=hips.position+forward*(h*.23f)+right*(h*.18f)+Vector3.up*(h*.10f);
            rightGrip.Align(bones[1],bones[3],grip,bones[1].position+right*h*.25f,(forward*.7f+Vector3.up*.7f).normalized);
            QuestRowboat.SolveArm(bones[0],bones[2],leftHand,hips.position+forward*h*.16f-right*h*.22f+Vector3.up*h*.12f,bones[0].position-right*h*.3f);
        }
        void CloseSwordHand()
        {
            // Curl each finger around the handle in world space. Avatar rigs use
            // different local bone axes, so fixed Euler angles do not transfer.
            foreach(var id in swordFingers)
            {
                var finger=Animator.GetBoneTransform(id);if(finger==null||finger.childCount==0)continue;
                var joint=finger.GetChild(0);if(joint.childCount==0)continue;var tip=joint.GetChild(0);
                var axis=BladeDirection;var toward=GripPosition-finger.position;toward-=axis*Vector3.Dot(toward,axis);
                if(toward.sqrMagnitude<.00001f)continue;
                var target=toward.normalized*(joint.position-finger.position).magnitude;
                finger.rotation=Quaternion.FromToRotation(joint.position-finger.position,target)*finger.rotation;
                var curl=GripPosition-joint.position;curl-=axis*Vector3.Dot(curl,axis);
                if(curl.sqrMagnitude>.00001f)joint.rotation=Quaternion.FromToRotation(tip.position-joint.position,curl.normalized)*joint.rotation;
            }
        }
        public void FinishTraversal(){Traversal=Motion.Idle;ClimbProgress=0;ClimbRail=Vector3.zero;}
        public void SetRanged(bool value)
        {
            if(value&&SwordVisible)SetSword(false);
            ranged=value;
            if(value&&Traversal==Motion.Idle){Animator.enabled=true;Animator.applyRootMotion=false;Animator.speed=1;}
            if(value&&rangedBow==null)
            {
                rangedBow=new GameObject("DaiViet_Boarding_Bow").transform;rangedBow.SetParent(transform,false);var scale=transform.lossyScale;rangedBow.localScale=new Vector3(1/scale.x,1/scale.y,1/scale.z);
                var line=rangedBow.gameObject.AddComponent<LineRenderer>();line.sharedMaterial=weapon.GetComponentInChildren<Renderer>().sharedMaterial;line.useWorldSpace=false;line.widthMultiplier=.4f;line.positionCount=9;
                for(int i=0;i<9;i++){float y=(i/8f-.5f)*10;line.SetPosition(i,new Vector3(0,y,2*(1-y*y/25)));}
                var stringObject=new GameObject("Day_cung_keo_tha");stringObject.transform.SetParent(rangedBow,false);rangedString=stringObject.AddComponent<LineRenderer>();rangedString.useWorldSpace=false;rangedString.sharedMaterial=line.sharedMaterial;rangedString.widthMultiplier=.1f;rangedString.positionCount=3;rangedString.SetPositions(new[]{new Vector3(0,-5,0),Vector3.zero,new Vector3(0,5,0)});
                var arrow=GameObject.CreatePrimitive(PrimitiveType.Cylinder);arrow.name="Ten_dang_giu";arrow.transform.SetParent(rangedBow,false);arrow.transform.localScale=new Vector3(.13f,3,.13f);arrow.GetComponent<Collider>().enabled=false;Destroy(arrow.GetComponent<Collider>());arrow.GetComponent<Renderer>().sharedMaterial=line.sharedMaterial;rangedArrow=arrow.transform;
            }
            if(rangedBow!=null)rangedBow.gameObject.SetActive(value);
            if(!value&&!SwordVisible)HumanoidGripMesh.Restore(Animator);
        }
    }
}
