using System.Collections.Generic;
using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Prepared archery groups cover dry riverbanks and islands around the stake field.</summary>
    public sealed class RiverShoreArchers : MonoBehaviour
    {
        sealed class Archer {public GameObject go;public RiverFighterPose pose;public float nextShot;public Vector3 bank;}
        sealed class Bank {public Vector3 point;public Vector2Int cell;public int island;}
        sealed class Arrow {public Transform visual;public EnemyDeckGuard target;public Vector3 start,end,previous;public float time,duration;public bool hit,lethal;}
        readonly List<Archer> archers=new List<Archer>();
        readonly List<Arrow> arrows=new List<Arrow>();
        Material material;bool fighting;
        public int ArcherCount=>archers.Count;
        public int CoveredLandmasses {get;private set;}
        public Vector3[] PreparedBanks=>archers.ConvertAll(a=>a.bank).ToArray();
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
            // prepared actors and arrows instead of instantiating the rigs at impact.
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
            var stations=FindShoreStations(terrain,focus,field.fieldStakes);
            for(int i=0;i<stations.Count;i++)
            {
                var bank=stations[i];
                var go=Instantiate(boarding.allyPrefab,transform);go.name="Cung_Thu_Mai_Phuc_Tren_Bo_"+(i+1);
                var pose=go.AddComponent<RiverFighterPose>();pose.Initialize(go.GetComponentInChildren<Animator>(),boarding.swordPrefab,boarding.swordController,17);
                pose.Animator.enabled=true;pose.Animator.applyRootMotion=false;pose.Animator.Update(0);pose.MatchFriendlySize();pose.SetRanged(true);
                go.transform.position=bank;go.transform.rotation=Quaternion.LookRotation(new Vector3(focus.x-bank.x,0,focus.z-bank.z));
                ActorSolidBody.WalkingDimensions(pose.Animator,out _,out float sole,out _);go.transform.position+=Vector3.up*(bank.y-go.transform.position.y-sole+.15f);
                ActorSolidBody.Ensure(go,pose.Animator);archers.Add(new Archer{go=go,pose=pose,bank=bank,nextShot=Time.time+Random.Range(.8f,6)});
            }
            if(archers.Count==0)return false;
            for(int i=0;i<24;i++)
            {
                var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name="MuiTen_MaiPhuc_"+i;go.transform.SetParent(transform,false);
                var c=go.GetComponent<Collider>();c.enabled=false;Destroy(c);go.GetComponent<Renderer>().sharedMaterial=material;
                go.transform.localScale=new Vector3(.22f,3.6f,.22f);go.SetActive(false);arrows.Add(new Arrow{visual=go.transform});
            }
            return archers.Count>0;
        }
        List<Vector3> FindShoreStations(Terrain terrain,Vector3 focus,Transform[] stakes)
        {
            const float grid=48;
            var bounds=new Bounds(focus,Vector3.zero);foreach(var stake in stakes)if(stake!=null)bounds.Encapsulate(stake.position);
            bounds.Expand(new Vector3(1500,0,1500));var size=terrain.terrainData.size;var origin=terrain.transform.position;
            float high=TideSystem.Instance.highTideY;
            var land=new Dictionary<Vector2Int,int>();var candidates=new List<Bank>();
            float Height(Vector3 p)=>terrain.SampleHeight(p)+origin.y;
            for(int x=0;x<=Mathf.CeilToInt(bounds.size.x/grid);x++)
            for(int z=0;z<=Mathf.CeilToInt(bounds.size.z/grid);z++)
            {
                var point=new Vector3(bounds.min.x+x*grid,0,bounds.min.z+z*grid);var local=point-origin;
                if(local.x<0||local.z<0||local.x>size.x||local.z>size.z)continue;
                point.y=Height(point);if(point.y<high+2)continue;
                var key=new Vector2Int(x,z);land[key]=-1;
                if(point.y>high+45||terrain.terrainData.GetInterpolatedNormal(local.x/size.x,local.z/size.z).y<.82f)continue;
                bool edge=false;
                foreach(var offset in new[]{Vector3.right,Vector3.left,Vector3.forward,Vector3.back})
                    if(Height(point+offset*72)<high-.5f){edge=true;break;}
                if(edge&&RiverHullCollision.ClearAt(point+Vector3.up*10,5))candidates.Add(new Bank{point=point,cell=key});
            }
            // Connected dry terrain distinguishes the small islands from the long
            // mainland banks. A radial search from the river center skipped them.
            int island=0;var queue=new Queue<Vector2Int>();var cells=new List<Vector2Int>(land.Keys);
            var directions=new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down};
            foreach(var cell in cells)
            {
                if(land[cell]>=0)continue;land[cell]=island;queue.Enqueue(cell);
                while(queue.Count>0){var next=queue.Dequeue();foreach(var d in directions){var k=next+d;if(land.TryGetValue(k,out int id)&&id<0){land[k]=island;queue.Enqueue(k);}}}island++;
            }
            var groups=new Dictionary<int,List<Bank>>();
            foreach(var bank in candidates){bank.island=land[bank.cell];if(!groups.TryGetValue(bank.island,out var list))groups[bank.island]=list=new List<Bank>();list.Add(bank);}
            var anchors=new List<Bank>();var ids=new List<int>(groups.Keys);
            ids.Sort((a,b)=>Nearest(groups[a],focus).CompareTo(Nearest(groups[b],focus)));
            Bank nearDock=null;float dockDistance=float.PositiveInfinity;
            foreach(var bank in candidates){float d=(bank.point-QuestRowboat.Instance.boardingPoint.position).sqrMagnitude;if(d<dockDistance){dockDistance=d;nearDock=bank;}}
            if(nearDock!=null){anchors.Add(nearDock);ids.Remove(nearDock.island);}
            foreach(int id in ids)
            {
                if(anchors.Count>=6)break;
                var reference=focus;
                Bank best=null;float distance=float.PositiveInfinity;
                foreach(var bank in groups[id]){float d=(bank.point-reference).sqrMagnitude;if(d<distance){distance=d;best=bank;}}
                if(best!=null)anchors.Add(best);
            }
            CoveredLandmasses=anchors.Count;
            while(anchors.Count<6)
            {
                Bank best=null;float separation=180*180;
                foreach(var bank in candidates)
                {
                    float nearest=float.PositiveInfinity;foreach(var a in anchors)nearest=Mathf.Min(nearest,(bank.point-a.point).sqrMagnitude);
                    if(nearest>separation){separation=nearest;best=bank;}
                }
                if(best==null)break;anchors.Add(best);
            }
            var result=new List<Vector3>();
            foreach(var anchor in anchors)
            {
                var group=new List<Bank>(groups[anchor.island]);group.Sort((a,b)=>(a.point-anchor.point).sqrMagnitude.CompareTo((b.point-anchor.point).sqrMagnitude));
                int count=0;foreach(var bank in group)
                {
                    bool near=false;foreach(var p in result)if((bank.point-p).sqrMagnitude<30*30){near=true;break;}
                    if(near)continue;result.Add(bank.point);if(++count==3)break;
                }
            }
            return result;
        }
        static float Nearest(List<Bank> banks,Vector3 point)
        {float distance=float.PositiveInfinity;foreach(var bank in banks)distance=Mathf.Min(distance,(bank.point-point).sqrMagnitude);return distance;}
        public void Step(float dt)
        {
            if(dt<=0||!ActiveAmbush)return;
            foreach(var a in archers)
            {
                if(Time.time<a.nextShot)continue;a.nextShot=Time.time+Random.Range(12f,18f);
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
