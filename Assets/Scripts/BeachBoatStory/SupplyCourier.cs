using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace BeachBoatStory
{
    /// <summary>Temporarily borrows an existing villager, following its solid navigation to the stock and freight cart.</summary>
    public sealed class SupplyCourier
    {
        public static string LastDiagnosis {get;private set;}="";
        public readonly ProtectedCampNPC Actor;public readonly int Work;
        public float GripError {get;private set;}
        public bool Holding=>carrying;public bool Done {get;private set;}public bool Failed {get;private set;}
        readonly NPCWander walker;readonly Animator rig;readonly RuntimeAnimatorController controller;readonly bool rigEnabled,rootMotion;readonly float speed;readonly AnimatorCullingMode culling;readonly SkinnedMeshRenderer[] skins;readonly bool[] skinEnabled;readonly bool[] skinOffscreen;readonly bool[] matrixUpdate;readonly CinematicCharacterLOD[] lods;readonly bool[] important;
        readonly Behaviour[] routines;readonly bool[] wasEnabled;readonly List<Vector3> route=new List<Vector3>();readonly Vector3 stock,hub;
        readonly NPCWalkNavigation navigation;GameObject cargo;float cargoScale=1;int point;bool carrying,released;float replan,stuck;readonly HumanoidHandGrip leftGrip,rightGrip;
        SupplyCourier(ProtectedCampNPC actor,int work,VillageWorkStall stall,Vector3 warehouse)
        {
            Actor=actor;Work=work;walker=actor.GetComponent<NPCWander>();navigation=walker.Navigation;rig=actor.GetComponentInChildren<Animator>();controller=rig.runtimeAnimatorController;culling=rig.cullingMode;skins=rig.GetComponentsInChildren<SkinnedMeshRenderer>();skinEnabled=skins.Select(r=>r.enabled).ToArray();skinOffscreen=skins.Select(r=>r.updateWhenOffscreen).ToArray();matrixUpdate=skins.Select(r=>r.forceMatrixRecalculationPerRender).ToArray();lods=rig.GetComponentsInChildren<CinematicCharacterLOD>();important=lods.Select(l=>l.important).ToArray();foreach(var lod in lods)lod.important=true;walker.ExternalControl=true;rigEnabled=rig.enabled;rootMotion=rig.applyRootMotion;speed=rig.speed;
            routines=actor.GetComponentsInChildren<Behaviour>().Where(b=>b is NPCWander||b is NPCDailyActivity||b is NPCCampPose||b is NPCAmbientDialogue||b is NPCDialogue).ToArray();wasEnabled=routines.Select(b=>b.enabled).ToArray();foreach(var b in routines)b.enabled=false;
            rig.enabled=true;rig.applyRootMotion=false;rig.speed=1;rig.runtimeAnimatorController=RiverBoardingCombat.Instance.FootController;rig.cullingMode=AnimatorCullingMode.AlwaysAnimate;rig.Rebind();rig.Update(0);foreach(var skin in skins){skin.updateWhenOffscreen=true;skin.forceMatrixRecalculationPerRender=true;skin.enabled=true;skin.forceRenderingOff=false;}
            var pickup=stall.Workstation.Environment.TransformPoint(new Vector3(25,0,-30));stock=navigation.FindSpawn(pickup,out var safe)?safe:pickup;hub=warehouse+new Vector3(work%2==0?-7:7,0,-20);leftGrip=new HumanoidHandGrip(rig,true,.35f);rightGrip=new HumanoidHandGrip(rig,false,.35f);HumanoidFootSupport.Prepare(rig);
            if(!Plan(stock)){LastDiagnosis+=$" {actor.name}: pickup={stock}, raised={walker.IsRaisedPatrol}, project={navigation.Project(stock,out var point)}, occupied={navigation.Occupied(stock,false)};";Failed=Done=true;Release();}
        }
        public static SupplyCourier TryCreate(int work,VillageWorkStall stall,Vector3 hub,IEnumerable<ProtectedCampNPC> busy)
        {
            var camp=CampDiscipline.Instance;if(camp==null||stall.Workstation==null)return null;
            var selected=camp.Actors.Where(a=>a!=null&&a.Type==ProtectedCampNPC.Kind.Civilian&&!a.Dead&&!a.Retaliating&&!busy.Contains(a)&&a.GetComponent<NPCWander>()?.enabled==true&&a.GetComponent<NPCWander>().Navigation!=null&&!a.GetComponent<NPCWander>().IsRaisedPatrol&&QuestRowboat.FlatDistance(a.transform.position,stall.transform.position)<700).OrderBy(a=>(a.transform.position-stall.transform.position).sqrMagnitude).ToArray();LastDiagnosis=$"{stall.name}: {selected.Length} villagers eligible";
            foreach(var actor in selected.Take(8)){var courier=new SupplyCourier(actor,work,stall,hub);if(!courier.Failed)return courier;}return null;
        }
        bool Plan(Vector3 destination)
        {
            route.Clear();point=0;var from=Actor.transform.position;
            var delta=destination-from;delta.y=0;
            if(delta.magnitude<=110&&navigation.Route(from,destination,route))return true;
            foreach(float angle in new[]{0f,35f,-35f,70f,-70f})
            {
                var local=from+Quaternion.Euler(0,angle,0)*delta.normalized*Mathf.Min(65,delta.magnitude);
                if(navigation.FindSpawn(local,out local)&&navigation.Route(from,local,route))return true;
            }
            return false;
        }
        public float Tick(float dt)
        {
            if(Done||Actor==null)return 0;if(Actor.Dead||Actor.Retaliating){Failed=Done=true;return 0;}
            if(QuestRowboat.FlatDistance(BeachBoatQuestSession.Instance.player.transform.position,Actor.transform.position)>800){Done=true;return 0;}
            var goal=carrying?hub:stock;var t=Actor.transform;float distance=QuestRowboat.FlatDistance(t.position,goal);
            if(distance<6)
            {
                if(carrying){rig.SetBool("isWalking",false);Done=true;return 0;}
                carrying=true;cargo=PreparationCampaign.Instance.Cargo("Supply_carried_"+Work,null,Vector3.zero,(VillageWorkStall.WorkKind)Work);cargo.GetComponent<Collider>().enabled=false;cargoScale=Mathf.Clamp(EnemyDeckGuard.RigHeight(rig)/26f,.6f,1.1f);cargo.transform.localScale=Vector3.one*cargoScale;route.Clear();point=0;replan=0;BeachBoatSoundscape.Instance?.PlayInteraction();
            }
            replan-=dt;if(route.Count==0||point>=route.Count)
            {
                if(replan>0)return 0;replan=1.5f;point=0;route.Clear();Plan(carrying?hub:stock);
                if(route.Count==0){stuck+=dt;if(stuck>8){Failed=Done=true;}return 0;}
            }
            while(point<route.Count-1&&QuestRowboat.FlatDistance(t.position,route[point])<3)point++;
            var from=t.position;var delta=route[point]-from;delta.y=0;var next=from+delta.normalized*Mathf.Min(delta.magnitude,25*dt);
            if(navigation.Project(next,out var supported)&&navigation.Clear(from,supported,true))
            {
                t.position=supported;if(delta.sqrMagnitude>.01f)t.rotation=Quaternion.Slerp(t.rotation,Quaternion.LookRotation(delta),1-Mathf.Exp(-10*dt));stuck=0;rig.SetBool("isWalking",true);rig.SetBool("isRunning",false);
            }
            else{stuck+=dt;rig.SetBool("isWalking",false);if(replan<=0){route.Clear();point=0;}if(stuck>8){Failed=Done=true;}}
            return QuestRowboat.FlatDistance(from,t.position);
        }
        public void Pose()
        {
            if(Actor!=null){foreach(var skin in skins){skin.enabled=true;skin.forceRenderingOff=false;}rig.enabled=true;}
            if(!carrying||cargo==null||Actor==null)return;var t=Actor.transform;var chest=rig.GetBoneTransform(HumanBodyBones.Chest);
            float armReach=Vector3.Distance(rig.GetBoneTransform(HumanBodyBones.RightUpperArm).position,rig.GetBoneTransform(HumanBodyBones.RightLowerArm).position)+Vector3.Distance(rig.GetBoneTransform(HumanBodyBones.RightLowerArm).position,rig.GetBoneTransform(HumanBodyBones.RightHand).position);
            var shoulders=(rig.GetBoneTransform(HumanBodyBones.LeftUpperArm).position+rig.GetBoneTransform(HumanBodyBones.RightUpperArm).position)*.5f;
            cargo.transform.SetPositionAndRotation(shoulders+t.forward*(armReach*.5f)-Vector3.up*(armReach*.3f),t.rotation*Quaternion.Euler(0,Work<2?90:0,0));
            foreach(bool left in new[]{true,false})
            {
                var upper=rig.GetBoneTransform(left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm);var lower=rig.GetBoneTransform(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);var hand=rig.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);var at=cargo.transform.position+t.right*(left?-2.5f:2.5f)*cargoScale-Vector3.up*(cargoScale*.4f);
                (left?leftGrip:rightGrip).Align(upper,lower,at,upper.position+t.right*(left?-4:4)-Vector3.up*3,t.forward);
            }
            GripError=Mathf.Max(Vector3.Distance(leftGrip.Contact,cargo.transform.position-t.right*2.5f*cargoScale-Vector3.up*(cargoScale*.4f)),Vector3.Distance(rightGrip.Contact,cargo.transform.position+t.right*2.5f*cargoScale-Vector3.up*(cargoScale*.4f)));
            HumanoidGripMesh.Apply(rig,leftGrip,rightGrip,.65f,.65f);
        }
        public void Release()
        {
            if(released)return;released=true;if(cargo!=null){cargo.SetActive(false);Object.Destroy(cargo);}if(Actor==null)return;walker.ExternalControl=false;for(int i=0;i<lods.Length;i++)if(lods[i]!=null)lods[i].important=important[i];for(int i=0;i<skins.Length;i++)if(skins[i]!=null){skins[i].enabled=skinEnabled[i];skins[i].updateWhenOffscreen=skinOffscreen[i];skins[i].forceMatrixRecalculationPerRender=matrixUpdate[i];}rig.cullingMode=culling;if(Actor.Retaliating||Actor.Dead)return;
            HumanoidGripMesh.Restore(rig);rig.SetBool("isWalking",false);rig.runtimeAnimatorController=controller;rig.enabled=rigEnabled;rig.applyRootMotion=rootMotion;rig.speed=speed;
            for(int i=0;i<routines.Length;i++)if(routines[i]!=null)routines[i].enabled=wasEnabled[i];
        }
    }
}
