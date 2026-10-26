using System.Collections.Generic;
using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Six visible archers on dry riverbanks; finite pooled arrows apply real allied damage on arrival.</summary>
    public sealed class RiverShoreArchers : MonoBehaviour
    {
        sealed class Archer {public GameObject go;public RiverFighterPose pose;public float nextShot;}
        sealed class Arrow {public Transform visual;public EnemyDeckGuard target;public Vector3 start,end,previous;public float time,duration;public bool hit,lethal;}
        readonly List<Archer> archers=new List<Archer>();
        readonly List<Arrow> arrows=new List<Arrow>();
        Material material;bool fighting;
        public int ArcherCount=>archers.Count;
        public int ShotsFired {get;private set;}
        public int Hits {get;private set;}
        public int Kills {get;private set;}
        public const float MaximumRange=4200;
        public static float HitChance(float range)=>Mathf.Lerp(.65f,.25f,Mathf.Clamp01(range/MaximumRange));
        public static bool RollHit(float roll,float range)=>roll<HitChance(range);
        public static bool RollLethal(float roll)=>roll<.25f;
        public bool ActiveAmbush=>BachDangStoryManager.Instance!=null&&
            BachDangStoryManager.Instance.currentState==BachDangStoryManager.StoryState.Act3_BattleInProgress&&
            (EnemyFleetDirector.Instance?.BattleStarted??false)&&
            (EnemyFleetDirector.Instance.AmbushStarted||BachDangBattleTimeline.Instance?.CurrentMoment==BachDangBattleTimeline.BattleMoment.Ambush)&&
            !(QuestCheckpoint.Instance?.IsDefeated??false);
        System.Collections.IEnumerator Start()
        {
            // Build while the title/loading screen is up; the ambush only enables
            // prepared actors and arrows instead of instantiating six rigs at impact.
            yield return null;
            if(BuildArchers())ResetAmbush();
        }
        void Update()
        {
            if(Time.deltaTime<=0)return;
            if(!ActiveAmbush){if(fighting)ResetAmbush();return;}
            if(!fighting){if(!BuildArchers())return;fighting=true;}
            Step(Time.deltaTime);
        }
        bool BuildArchers()
        {
            if(archers.Count>0){foreach(var a in archers){a.go.SetActive(true);a.nextShot=Time.time+Random.Range(1,3);}return true;}
            var boarding=RiverBoardingCombat.Instance;var terrain=Terrain.activeTerrain;
            if(boarding==null||boarding.allyPrefab==null||terrain==null)return false;
            var field=RiverStakeZone.Instance;Vector3 focus=Vector3.zero;int n=0;
            foreach(var stake in field.fieldStakes)if(stake!=null){focus+=stake.position;n++;}if(n==0)return false;focus/=n;
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.SetColor("_BaseColor",new Color(.33f,.21f,.09f));}
            for(int i=0;i<6;i++)
            {
                var direction=Quaternion.Euler(0,30+i*60,0)*Vector3.forward;
                if(!FindBank(terrain,focus,direction,out var bank)&&
                    !FindBank(terrain,focus,Quaternion.Euler(0,15,0)*direction,out bank)&&
                    !FindBank(terrain,focus,Quaternion.Euler(0,-15,0)*direction,out bank))continue;
                var go=Instantiate(boarding.allyPrefab,transform);go.name="Cung_Thu_Mai_Phuc_Tren_Bo_"+(i+1);
                var pose=go.AddComponent<RiverFighterPose>();pose.Initialize(go.GetComponentInChildren<Animator>(),boarding.swordPrefab,boarding.swordController,17);
                pose.Animator.enabled=true;pose.Animator.applyRootMotion=false;pose.Animator.Update(0);pose.MatchFriendlySize();pose.SetRanged(true);
                go.transform.position=bank;go.transform.rotation=Quaternion.LookRotation(new Vector3(focus.x-bank.x,0,focus.z-bank.z));
                ActorSolidBody.WalkingDimensions(pose.Animator,out _,out float sole,out _);go.transform.position+=Vector3.up*(bank.y-go.transform.position.y-sole+.15f);
                ActorSolidBody.Ensure(go,pose.Animator);archers.Add(new Archer{go=go,pose=pose,nextShot=Time.time+Random.Range(.8f,3)});
            }
            if(archers.Count==0)return false;
            for(int i=0;i<12;i++)
            {
                var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name="MuiTen_MaiPhuc_"+i;go.transform.SetParent(transform,false);
                var c=go.GetComponent<Collider>();c.enabled=false;Destroy(c);go.GetComponent<Renderer>().sharedMaterial=material;
                go.transform.localScale=new Vector3(.22f,3.6f,.22f);go.SetActive(false);arrows.Add(new Arrow{visual=go.transform});
            }
            return archers.Count>0;
        }
        static bool FindBank(Terrain terrain,Vector3 focus,Vector3 direction,out Vector3 bank)
        {
            var size=terrain.terrainData.size;float high=TideSystem.Instance.highTideY;
            for(float distance=200;distance<7000;distance+=35)
            {
                var p=focus+direction*distance;var local=p-terrain.transform.position;
                if(local.x<0||local.z<0||local.x>size.x||local.z>size.z)break;
                p.y=terrain.SampleHeight(p)+terrain.transform.position.y;
                if(p.y<high+2||terrain.terrainData.GetInterpolatedNormal(local.x/size.x,local.z/size.z).y<.8f)continue;
                if(!RiverHullCollision.ClearAt(p+Vector3.up*10,5))continue;
                bank=p;return true;
            }
            bank=default;return false;
        }
        public void Step(float dt)
        {
            if(dt<=0||!ActiveAmbush)return;
            foreach(var a in archers)
            {
                if(Time.time<a.nextShot)continue;a.nextShot=Time.time+Random.Range(4.5f,6.5f);
                EnemyDeckGuard target=null;float best=MaximumRange;
                foreach(var g in RiverPlayerCombat.Instance.guards)
                {if(g==null||!g.IsAlive||!g.ship.isDestroyed)continue;float d=QuestRowboat.FlatDistance(a.go.transform.position,g.transform.position);if(d<best){best=d;target=g;}}
                if(target==null)continue;
                Arrow arrow=null;foreach(var shot in arrows)if(!shot.visual.gameObject.activeSelf){arrow=shot;break;}if(arrow==null)continue;
                var face=target.transform.position-a.go.transform.position;face.y=0;a.go.transform.rotation=Quaternion.LookRotation(face);
                arrow.start=a.go.transform.position+Vector3.up*12+a.go.transform.forward*7;
                arrow.hit=RollHit(Random.value,best);arrow.lethal=RollLethal(Random.value);arrow.target=target;
                arrow.end=target.AimPoint+(arrow.hit?Vector3.zero:Quaternion.Euler(0,Random.Range(0,360),0)*Vector3.forward*Random.Range(18,30));
                arrow.time=0;arrow.duration=Mathf.Clamp(best/550,.55f,5.5f);arrow.previous=arrow.start;
                arrow.visual.position=arrow.start;arrow.visual.gameObject.SetActive(true);ShotsFired++;
            }
            foreach(var shot in arrows)
            {
                if(!shot.visual.gameObject.activeSelf)continue;shot.time+=dt;float t=Mathf.Clamp01(shot.time/shot.duration);
                if(shot.hit&&shot.target!=null&&shot.target.IsAlive)shot.end=shot.target.AimPoint;
                var point=Vector3.Lerp(shot.start,shot.end,t)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*Mathf.Min(140,Vector3.Distance(shot.start,shot.end)*.12f));
                bool blocked=Physics.Linecast(shot.previous,point,out var contact,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)&&
                    !(shot.target!=null&&contact.collider.transform.IsChildOf(shot.target.transform));
                var direction=point-shot.previous;if(direction.sqrMagnitude>.0001f)shot.visual.rotation=Quaternion.FromToRotation(Vector3.up,direction);
                shot.visual.position=point;shot.previous=point;
                if(blocked||t>=1)
                {
                    if(!blocked&&shot.hit&&shot.target!=null&&shot.target.IsAlive)
                    {bool wasAlive=shot.target.IsAlive;if(shot.target.Hit(shot.lethal?EnemyDeckGuard.MaximumHealth:1,false,CombatImpactKind.Arrow)){Hits++;if(wasAlive&&!shot.target.IsAlive)Kills++;}}
                    shot.visual.gameObject.SetActive(false);shot.target=null;
                }
            }
        }
        public void ResetAmbush()
        {
            fighting=false;foreach(var a in archers)a.go.SetActive(false);
            foreach(var shot in arrows){shot.visual.gameObject.SetActive(false);shot.target=null;}
        }
        void OnDestroy(){if(material!=null)Destroy(material);}
    }
}
