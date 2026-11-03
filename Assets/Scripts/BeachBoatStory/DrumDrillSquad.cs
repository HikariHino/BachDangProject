using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Borrow nearby camp patrols for a drill, then return control to their normal routines.</summary>
    public sealed class DrumDrillSquad
    {
        sealed class Member
        {
            public ProtectedCampNPC actor;public Animator rig;public RuntimeAnimatorController controller;public bool rigEnabled;public float speed;
            public Behaviour[] routines;public bool[] enabled;public Vector3 goal;public RiverFighterPose pose;public bool sword;public bool rootMotion;public bool addedPose;
            public readonly RiverActorNavigation navigation=new RiverActorNavigation();
        }
        readonly List<Member> members=new List<Member>();readonly DrumTrainingGround ground;
        public int Count=>members.Count;public int GuardingCount=>members.Count(m=>m.pose!=null&&m.pose.SwordVisible&&m.rig.GetBool("isBlocking"));public int CommandsReceived {get;private set;}public float DistanceMoved {get;private set;}
        public DrumDrillSquad(DrumTrainingGround ground)
        {
            this.ground=ground;var discipline=CampDiscipline.Instance;if(discipline==null)return;discipline.Discover();
            var selected=discipline.Actors.Where(a=>a!=null&&a.Type==ProtectedCampNPC.Kind.Soldier&&!a.Retaliating&&!a.Dead&&a.gameObject.activeInHierarchy&&a.GetComponent<NPCWander>()?.enabled==true&&a.GetComponent<ActorSolidBody>()?.Carrier==null&&QuestRowboat.FlatDistance(a.transform.position,ground.transform.position)<350).OrderBy(a=>(a.transform.position-ground.transform.position).sqrMagnitude).Take(4);
            foreach(var actor in selected)
            {
                var rig=actor.GetComponentInChildren<Animator>();if(rig==null)continue;
                var routines=actor.GetComponentsInChildren<Behaviour>().Where(b=>b is NPCWander||b is NPCDailyActivity||b is NPCCampPose).ToArray();
                var m=new Member{actor=actor,rig=rig,controller=rig.runtimeAnimatorController,rigEnabled=rig.enabled,speed=rig.speed,rootMotion=rig.applyRootMotion,routines=routines,enabled=routines.Select(b=>b.enabled).ToArray(),pose=actor.GetComponent<RiverFighterPose>()};m.sword=m.pose?.SwordVisible??false;
                foreach(var routine in routines)routine.enabled=false;rig.enabled=true;rig.runtimeAnimatorController=RiverBoardingCombat.Instance.FootController;rig.applyRootMotion=false;rig.speed=1;rig.SetInteger("Pose",0);rig.SetBool("isWalking",false);rig.SetBool("isRunning",false);HumanoidFootSupport.Prepare(rig);
                int i=members.Count;float side=i<2?-1:1;var target=ground.transform.TransformPoint(new Vector3(side*(30+(i%2)*12),0,40+(i%2)*12));m.goal=target;members.Add(m);
            }
        }
        public void Receive(int order)
        {
            CommandsReceived++;
            for(int i=0;i<members.Count;i++)
            {
                float side=i<2?-1:1;var m=members[i];m.goal=ground.transform.TransformPoint(new Vector3(side*(24+(i%2)*12),0,36+(i%2)*12-(order>=1?12:0)));m.navigation.Reset();
                if(order==2)
                {
                    if(m.pose==null){m.pose=m.actor.gameObject.AddComponent<RiverFighterPose>();m.pose.Initialize(m.rig,RiverBoardingCombat.Instance.swordPrefab,RiverBoardingCombat.Instance.swordController,17);m.addedPose=true;}
                    m.goal=m.actor.transform.position;m.pose.SetSword(true);m.rig.SetBool("isBlocking",true);m.rig.CrossFadeInFixedTime("Block",.12f);
                }
            }
        }
        public void Tick(float dt)
        {
            foreach(var m in members)
            {
                if(m.actor==null||m.actor.Retaliating)continue;var t=m.actor.transform;var delta=m.goal-t.position;delta.y=0;bool moving=delta.magnitude>1.5f;
                if(moving)
                {
                    var from=t.position;var next=m.navigation.Move(t,from+Vector3.up*9,m.goal+Vector3.up*9,Mathf.Min(delta.magnitude,28*dt),3.5f)-Vector3.up*9;t.position=next;DistanceMoved+=QuestRowboat.FlatDistance(from,next);
                    var direction=next-from;direction.y=0;if(direction.sqrMagnitude>.001f)t.rotation=Quaternion.Slerp(t.rotation,Quaternion.LookRotation(direction),1-Mathf.Exp(-9*dt));
                }
                else t.rotation=Quaternion.Slerp(t.rotation,Quaternion.LookRotation(-ground.transform.forward),1-Mathf.Exp(-6*dt));
                m.rig.SetBool("isWalking",moving);m.rig.SetBool("isRunning",false);
            }
        }
        public void FitFeet()
        {
            foreach(var m in members)if(m.actor!=null&&!m.actor.Retaliating&&QuestRowboat.FlatDistance(m.actor.transform.position,ground.transform.position)<110&&HumanoidFootSupport.Lowest(m.rig,out float bottom))
            {var p=m.actor.transform.position;float y=Terrain.activeTerrain.SampleHeight(p)+Terrain.activeTerrain.transform.position.y;m.actor.transform.position+=Vector3.up*(y+.1f-bottom);}
        }
        public void Release()
        {
            foreach(var m in members)if(m.actor!=null&&!m.actor.Retaliating)
            {
                if(m.pose!=null){if(m.pose.SwordVisible)m.rig.SetBool("isBlocking",false);m.pose.SetSword(m.sword);}
                if(m.addedPose){if(m.pose.Weapon!=null)Object.Destroy(m.pose.Weapon.gameObject);Object.Destroy(m.pose);}
                m.rig.applyRootMotion=m.rootMotion;m.rig.runtimeAnimatorController=m.controller;m.rig.enabled=m.rigEnabled;m.rig.speed=m.speed;
                for(int i=0;i<m.routines.Length;i++)if(m.routines[i]!=null)m.routines[i].enabled=m.enabled[i];
            }
            members.Clear();
        }
    }
}
