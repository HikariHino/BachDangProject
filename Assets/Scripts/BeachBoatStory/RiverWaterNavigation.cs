using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Shared deep-water grid; all authored stakes remain reserved even before planting.</summary>
    public static class RiverWaterNavigation
    {
        static readonly Unity.Profiling.ProfilerMarker PathMarker=new Unity.Profiling.ProfilerMarker("BachDang.RiverPath");
        const float Cell=40;
        static Terrain terrain;
        static Vector3 origin;
        static int width,height;
        static float water;
        static readonly Dictionary<int,Dictionary<int,byte>> maps=new Dictionary<int,Dictionary<int,byte>>();
        static readonly Dictionary<int,int> previous=new Dictionary<int,int>(4096);
        static readonly Dictionary<int,float> costs=new Dictionary<int,float>(4096);
        static readonly HashSet<int> closed=new HashSet<int>();
        static readonly SortedSet<(float score,int id,int serial)> open=new SortedSet<(float score,int id,int serial)>();
        static int lastPlanningFrame=-1;
        public static bool ReservePlanningFrame(){if(lastPlanningFrame==Time.frameCount)return false;lastPlanningFrame=Time.frameCount;return true;}
        static Bounds[] stakes;
        const float StakeCell=160;
        static readonly Dictionary<Vector2Int,List<Bounds>> stakeBuckets=new Dictionary<Vector2Int,List<Bounds>>();
        static float maximumStakeRadius;
        static Vector2Int Bucket(Vector3 p)=>new Vector2Int(Mathf.FloorToInt(p.x/StakeCell),Mathf.FloorToInt(p.z/StakeCell));
        static float SqrXZ(Vector3 a,Vector3 b){float x=a.x-b.x,z=a.z-b.z;return x*x+z*z;}
        static Bounds stakeField;
        static Bounds invasionApproach;
        public static bool ReserveInvasionApproach=true;
        static int ProfileKey(float radius)=>Mathf.CeilToInt(radius/10)+(ReserveInvasionApproach?10000:0);
        static readonly Collider[] hits=new Collider[256];
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]public static void Reset(){terrain=null;maps.Clear();previous.Clear();costs.Clear();closed.Clear();open.Clear();stakes=null;lastPlanningFrame=-1;}
        static void Prepare()
        {
            var current=Terrain.activeTerrain;if(current==null)throw new InvalidOperationException("River terrain missing");
            if(terrain==current&&stakes!=null)return;
            terrain=current;origin=terrain.transform.position;width=Mathf.CeilToInt(terrain.terrainData.size.x/Cell);height=Mathf.CeilToInt(terrain.terrainData.size.z/Cell);
            var tide=UnityEngine.Object.FindAnyObjectByType<TideSystem>();water=tide!=null?tide.lowTideY:TideSystem.CurrentWaterHeight;maps.Clear();
            var zone=UnityEngine.Object.FindAnyObjectByType<RiverStakeZone>();var list=new List<Bounds>();
            foreach(var stake in zone!=null?zone.fieldStakes:Array.Empty<Transform>())
            {if(stake==null)continue;var renderer=stake.GetComponentInChildren<Renderer>(true);if(renderer!=null)list.Add(renderer.bounds);}
            stakes=list.ToArray();stakeField=stakes.Length>0?stakes[0]:default;foreach(var b in stakes)stakeField.Encapsulate(b);
            // Reserve the actual invasion lanes, rather than the entire upstream
            // half of the river. A bank-to-bank field still leaves western patrol water.
            bool hasApproach=false;invasionApproach=default;
            var fleet=UnityEngine.Object.FindAnyObjectByType<EnemyFleetDirector>();
            foreach(var ship in fleet!=null?fleet.enemyShips:Array.Empty<EnemyShip>())
            {
                if(ship==null)continue;
                var points=new List<Vector3>{ship.spawnPoint};
                foreach(var point in ship.routeWaypoints)if(point!=null)points.Add(point.position);
                foreach(var point in points)
                {
                    if(point.z<stakeField.max.z-20)continue;
                    var b=new Bounds(new Vector3(point.x,water,point.z),new Vector3((ship.stakeHitDistance+120)*2,1,400));
                    if(!hasApproach){invasionApproach=b;hasApproach=true;}else invasionApproach.Encapsulate(b);
                }
            }
            stakeBuckets.Clear();maximumStakeRadius=0;
            foreach(var b in stakes){var key=Bucket(b.center);if(!stakeBuckets.TryGetValue(key,out var bucket)){bucket=new List<Bounds>();stakeBuckets[key]=bucket;}bucket.Add(b);maximumStakeRadius=Mathf.Max(maximumStakeRadius,new Vector2(b.extents.x,b.extents.z).magnitude+1);}
        }
        public static bool StakeIntersection(Vector3 center,Vector3 half,Quaternion rotation)
        {
            Prepare();var inverse=Quaternion.Inverse(rotation);
            float range=new Vector2(half.x,half.z).magnitude+maximumStakeRadius;var min=Bucket(center-new Vector3(range,0,range));var max=Bucket(center+new Vector3(range,0,range));
            for(int x=min.x;x<=max.x;x++)for(int z=min.y;z<=max.y;z++)
            {
                if(!stakeBuckets.TryGetValue(new Vector2Int(x,z),out var bucket))continue;
                foreach(var stake in bucket)
                {
                // Reserve the horizontal footprint at any tide. This also prevents an
                // allied ship from parking above stakes that become exposed later.
                var p=inverse*(stake.center-center);float radius=new Vector2(stake.extents.x,stake.extents.z).magnitude+1;
                if(Mathf.Abs(p.x)<=half.x+radius&&Mathf.Abs(p.z)<=half.z+radius)return true;
                }
            }
            return false;
        }
        static Vector3 Point(int id)=>origin+new Vector3((id%width+.5f)*Cell,water,(id/width+.5f)*Cell);
        static int Id(Vector3 p)=>Mathf.Clamp(Mathf.FloorToInt((p.z-origin.z)/Cell),0,height-1)*width+Mathf.Clamp(Mathf.FloorToInt((p.x-origin.x)/Cell),0,width-1);
        static bool StaticObstacle(Collider c)=>c!=null&&!(c is TerrainCollider)&&c.GetComponent<TideSystem>()==null&&
            c.GetComponentInParent<AmbientRiverBoat>()==null&&c.GetComponentInParent<EnemyShip>()==null&&c.GetComponentInParent<QuestRowboat>()==null&&c.GetComponentInParent<Animator>()==null&&c.GetComponentInParent<ActorSolidBody>()==null;
        static bool Clear(int id,float radius)
        {
            if(id<0||id>=width*height)return false;
            int key=ProfileKey(radius);if(!maps.TryGetValue(key,out var map)){map=new Dictionary<int,byte>(2048);maps[key]=map;}
            if(map.TryGetValue(id,out var cached))return cached==1;
            float r=Mathf.Ceil(radius/10)*10+Cell*.72f;var p=Point(id);
            bool clear=!(ReserveInvasionApproach&&invasionApproach.size.x>0&&p.x+r>invasionApproach.min.x&&p.x-r<invasionApproach.max.x&&p.z+r>invasionApproach.min.z&&p.z-r<invasionApproach.max.z);
            for(int i=0;i<9&&clear;i++)
            {
                var sample=p+(i==0?Vector3.zero:Quaternion.Euler(0,(i-1)*45,0)*Vector3.forward*r);var local=sample-origin;
                if(local.x<0||local.z<0||local.x>terrain.terrainData.size.x||local.z>terrain.terrainData.size.z||terrain.SampleHeight(sample)+origin.y>water-14){clear=false;break;}
            }
            if(clear)
            {
                float range=r+maximumStakeRadius;var min=Bucket(p-new Vector3(range,0,range));var max=Bucket(p+new Vector3(range,0,range));
                for(int x=min.x;x<=max.x&&clear;x++)for(int z=min.y;z<=max.y&&clear;z++)
                {
                    if(!stakeBuckets.TryGetValue(new Vector2Int(x,z),out var bucket))continue;
                    foreach(var stake in bucket){float safe=r+new Vector2(stake.extents.x,stake.extents.z).magnitude;if(SqrXZ(stake.center,p)<safe*safe){clear=false;break;}}
                }
            }
            if(clear)
            {
                int count=Physics.OverlapBoxNonAlloc(p+Vector3.up*8,new Vector3(r,20,r),hits,Quaternion.identity,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
                clear=count<hits.Length;
                for(int i=0;i<count&&clear;i++)if(StaticObstacle(hits[i]))clear=false;
            }
            map[id]=clear?(byte)1:(byte)2;return clear;
        }
        public static bool NearestClear(Vector3 requested,float radius,out Vector3 result,IList<Vector3> excluded=null,float separation=0)
        {
            Prepare();int start=Id(requested),x=start%width,z=start/width;float best=float.PositiveInfinity;int chosen=-1,first=-1;
            for(int ring=0;ring<55;ring++)
            {
                if(first>=0&&ring>first+2)break;
                for(int dz=-ring;dz<=ring;dz++)for(int dx=-ring;dx<=ring;dx++)
                {
                    if(Mathf.Abs(dx)!=ring&&Mathf.Abs(dz)!=ring)continue;
                    int nx=x+dx,nz=z+dz;if(nx<0||nz<0||nx>=width||nz>=height)continue;int id=nz*width+nx;
                    var p=Point(id);float score=(p-requested).sqrMagnitude;if(score>=best||!Clear(id,radius))continue;
                    bool blocked=false;if(excluded!=null)foreach(var other in excluded)if(SqrXZ(p,other)<separation*separation){blocked=true;break;}
                    if(blocked)continue;chosen=id;best=score;if(first<0)first=ring;
                }
            }
            result=chosen>=0?Point(chosen):requested;return chosen>=0;
        }
        public static bool FindPath(Vector3 from,Vector3 target,float radius,List<Vector3> path,Transform requester=null,bool adjustGoalForTraffic=true)
        {
            using var sample=PathMarker.Auto();
            Prepare();path.Clear();
            if(!NearestClear(from,radius,out var startPoint)||!NearestClear(target,radius,out var goalPoint))return false;
            var requesterHull=requester!=null?requester.GetComponent<BoatHullCollision>():null;
            float padding=requesterHull!=null?requesterHull.TrafficPadding:30;
            var traffic=new List<(Vector3 position,float safeSquared,float initialSquared)>();var positions=new List<Vector3>();float spacing=radius*2+padding;
            if(requester!=null)foreach(var other in BoatHullCollision.Active)
            {
                if(other.transform==requester||other.SolidHull==null||!other.SolidHull.enabled)continue;
                var position=other.transform.position;float safe=radius+other.Radius+Mathf.Max(padding,other.TrafficPadding),initial=QuestRowboat.FlatDistance(from,position)+4;
                traffic.Add((position,safe*safe,initial*initial));positions.Add(position);spacing=Mathf.Max(spacing,safe);
            }
            if(adjustGoalForTraffic&&positions.Count>0&&!NearestClear(target,radius,out goalPoint,positions,spacing))return false;
            bool FreeOfTraffic(int id)
            {
                var p=Point(id);
                foreach(var other in traffic)
                {
                    float distance=SqrXZ(p,other.position);
                    if(distance<other.safeSquared&&distance<other.initialSquared)return false;
                }
                return true;
            }
            int start=Id(startPoint),goal=Id(goalPoint),size=width*height,closest=start;float closestDistance=Vector3.SqrMagnitude(startPoint-goalPoint);
            previous.Clear();costs.Clear();closed.Clear();open.Clear();int serial=0;
            open.Add((Vector3.Distance(startPoint,goalPoint),start,serial++));
            costs[start]=0;previous[start]=-1;
            if(!FreeOfTraffic(goal))return false;
            int iterations=0;
            while(open.Count>0&&iterations<Mathf.Min(size,4096))
            {
                var first=open.Min;open.Remove(first);int current=first.id;if(!closed.Add(current))continue;iterations++;
                float remaining=Vector3.SqrMagnitude(Point(current)-goalPoint);if(remaining<closestDistance){closestDistance=remaining;closest=current;}
                if(current==goal)
                {
                    for(int id=goal;id>=0;id=previous[id])path.Add(Point(id));path.Reverse();
                    // The first cell anchors graph search. Steering back to its centre
                    // can move toward traffic even though the next edge escapes it.
                    if(path.Count>1&&QuestRowboat.FlatDistance(from,path[0])<Cell*.8f)path.RemoveAt(0);return true;
                }
                int x=current%width,z=current/width;
                for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
                {
                    if(dx==0&&dz==0)continue;int nx=x+dx,nz=z+dz;if(nx<0||nz<0||nx>=width||nz>=height)continue;
                    int next=nz*width+nx;if(closed.Contains(next)||!Clear(next,radius)||(next!=goal&&!FreeOfTraffic(next)))continue;
                    if(dx!=0&&dz!=0&&(!Clear(z*width+nx,radius)||!Clear(nz*width+x,radius)))continue;
                    float g=costs[current]+Cell*(dx!=0&&dz!=0?1.414214f:1);
                    if(costs.TryGetValue(next,out var known)&&g>=known)continue;costs[next]=g;previous[next]=current;open.Add((g+Vector3.Distance(Point(next),goalPoint),next,serial++));
                }
            }
            // A target may be in another water pocket. Approach the closest
            // actually reachable cell; never invent an edge across land/stakes.
            if(closest!=start&&closestDistance<Cell*Cell*100)
            {for(int id=closest;id>=0;id=previous[id])path.Add(Point(id));path.Reverse();if(path.Count>1)path.RemoveAt(0);return true;}
            return false;
        }
    }
}
