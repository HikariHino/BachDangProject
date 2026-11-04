using UnityEngine;
namespace BeachBoatStory
{
    /// <summary>A second worker supports the same sample timber from the inboard work planks.</summary>
    public sealed class StakeHoldingPartner
    {
        readonly Transform actor;readonly Animator rig;readonly HumanoidHandGrip left,right;readonly Transform[] bones;readonly Quaternion[] rest;readonly float sole;readonly Transform platform,bridge;Vector3 expected;readonly Transform hips;readonly Vector3 hipsRest;readonly Transform[] legs;readonly Quaternion[] legRest;
        public string Diagnosis=>"actor="+actor.position+", hands="+left.Contact+"/"+right.Contact+", shoulder="+bones[0].position+", rig="+rig.transform.lossyScale+", sole="+sole+", target="+expected;
        public float HighestHand=>Mathf.Max(left.Contact.y,right.Contact.y);
        public bool Holding {get;private set;}public float ContactError {get;private set;}
        public StakeHoldingPartner(Transform parent)
        {
            actor=new GameObject("Cong_binh_giu_coc_cho_nguoi_choi").transform;actor.SetParent(parent,true);
            var model=Object.Instantiate(RiverBoardingCombat.Instance.allyPrefab,actor,false);rig=model.GetComponentInChildren<Animator>();rig.runtimeAnimatorController=RiverBoardingCombat.Instance.FootController;rig.enabled=true;rig.Update(0);
            rig.transform.localScale*=EnemyDeckGuard.RigHeight(BeachBoatQuestSession.Instance.player.GetComponentInChildren<Animator>())/Mathf.Max(.01f,EnemyDeckGuard.RigHeight(rig));rig.Update(0);CinematicCharacterLOD.Ensure(rig);
            ActorSolidBody.WalkingDimensions(rig,out _,out sole,out _);foreach(var c in model.GetComponentsInChildren<Collider>(true)){c.enabled=false;Object.Destroy(c);}foreach(var skin in rig.GetComponentsInChildren<SkinnedMeshRenderer>(true)){skin.updateWhenOffscreen=true;skin.forceMatrixRecalculationPerRender=true;}rig.enabled=false;
            bones=new[]{rig.GetBoneTransform(HumanBodyBones.LeftUpperArm),rig.GetBoneTransform(HumanBodyBones.LeftLowerArm),rig.GetBoneTransform(HumanBodyBones.RightUpperArm),rig.GetBoneTransform(HumanBodyBones.RightLowerArm)};rest=new Quaternion[bones.Length];for(int i=0;i<bones.Length;i++)rest[i]=bones[i].localRotation;
            hips=rig.GetBoneTransform(HumanBodyBones.Hips);hipsRest=hips.localPosition;legs=new[]{rig.GetBoneTransform(HumanBodyBones.LeftUpperLeg),rig.GetBoneTransform(HumanBodyBones.LeftLowerLeg),rig.GetBoneTransform(HumanBodyBones.LeftFoot),rig.GetBoneTransform(HumanBodyBones.RightUpperLeg),rig.GetBoneTransform(HumanBodyBones.RightLowerLeg),rig.GetBoneTransform(HumanBodyBones.RightFoot)};legRest=new Quaternion[6];for(int i=0;i<6;i++)legRest[i]=legs[i].localRotation;
            left=new HumanoidHandGrip(rig,true,.35f);right=new HumanoidHandGrip(rig,false,.35f);actor.gameObject.SetActive(false);
            platform=GameObject.CreatePrimitive(PrimitiveType.Cube).transform;platform.name="Van_dung_giu_coc";platform.SetParent(parent,true);platform.GetComponent<Renderer>().sharedMaterial=RiverStakeZone.Instance.fieldStakes[0].GetComponentInChildren<Renderer>(true).sharedMaterial;platform.gameObject.SetActive(false);
            bridge=GameObject.CreatePrimitive(PrimitiveType.Cube).transform;bridge.name="Van_noi_tang_cong_binh";bridge.SetParent(parent,true);bridge.GetComponent<Renderer>().sharedMaterial=platform.GetComponent<Renderer>().sharedMaterial;bridge.gameObject.SetActive(false);
        }
        public void Pose(bool active,Vector3 deck,Quaternion boatRotation,Transform post,float beat)
        {
            Holding=active;actor.gameObject.SetActive(active);platform.gameObject.SetActive(active);bridge.gameObject.SetActive(active);if(!active)return;
            var position=post.position+post.up*.5f+boatRotation*Vector3.right*.5f+boatRotation*Vector3.forward*4.5f;float floorY=deck.y-3.5f;position.y=floorY-sole+.15f;
            // This is a real work plank from the inboard deck to the holder's feet, not an airborne IK anchor.
            var start=deck+boatRotation*Vector3.forward*4;var end=position+Vector3.up*sole;var center=(start+end)*.5f;center.y=floorY-.25f;platform.SetPositionAndRotation(center,boatRotation);bridge.SetPositionAndRotation((start+end)*.5f-Vector3.up*.25f,Quaternion.LookRotation(end-start));bridge.localScale=new Vector3(5,.5f,Vector3.Distance(start,end)+1);var local=Quaternion.Inverse(boatRotation)*(end-start);platform.localScale=new Vector3(Mathf.Abs(local.x)+9,.5f,12);var facing=post.position+post.up*8-position;facing.y=0;actor.SetPositionAndRotation(position,Quaternion.LookRotation(facing));
            for(int i=0;i<bones.Length;i++)bones[i].localRotation=rest[i];hips.localPosition=hipsRest;for(int i=0;i<6;i++)legs[i].localRotation=legRest[i];
            var feet=new[]{legs[2].position,legs[5].position};var rotations=new[]{legs[2].rotation,legs[5].rotation};float crouch=3.5f;float shoulder=(bones[0].position.y+bones[2].position.y)*.5f-crouch;
            float at=Mathf.Clamp((shoulder-3-post.position.y)/Mathf.Max(.1f,post.up.y),-8,1);var contact=post.position+post.up*at;
            float drop=crouch+Mathf.Clamp(shoulder-contact.y-4,0,3.5f);hips.position-=Vector3.up*drop;
            for(int leg=0;leg<2;leg++){int n=leg*3;QuestRowboat.SolveArm(legs[n],legs[n+1],legs[n+2],feet[leg],legs[n].position+actor.forward*8);legs[n+2].rotation=rotations[leg];}
            expected=contact;
            for(int pass=0;pass<4;pass++)
            {
                left.Align(bones[0],bones[1],contact+post.up*1.3f,bones[0].position-actor.right*4-Vector3.up*2,post.up);
                right.Align(bones[2],bones[3],contact-post.up*1.3f,bones[2].position+actor.right*4-Vector3.up*2,post.up);
            }
            HumanoidGripMesh.Apply(rig,left,right,1,1);
            ContactError=Mathf.Max(Vector3.Distance(left.Contact,contact+post.up*1.3f),Vector3.Distance(right.Contact,contact-post.up*1.3f));
        }
    }
}
