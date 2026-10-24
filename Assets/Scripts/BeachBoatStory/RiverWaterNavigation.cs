using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Shared deep-water grid; all authored stakes remain reserved even before planting.</summary>
    public static class RiverWaterNavigation
    {
        const float Cell=40;
        static Terrain terrain;
        static Vector3 origin;
        static int width,height;
        static float water;
        static readonly Dictionary<int,byte[]> maps=new Dictionary<int,byte[]>();
        static readonly Dictionary<int,int[]> components=new Dictionary<int,int[]>();
        static Bounds[] stakes;
        static Bounds stakeField;
        public static bool ReserveInvasionApproach=true;
        static int ProfileKey(float radius)=>Mathf.CeilToInt(radius/10)+(ReserveInvasionApproach?10000:0);
        static readonly Collider[] hits=new Collider[256];
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]public static void Reset(){terrain=null;maps.Clear();components.Clear();stakes=null;}
        static void Prepare()
        {
            var current=Terrain.activeTerrain;if(current==null)throw new InvalidOperationException("River terrain missing");
            if(terrain==current&&stakes!=null)return;
            terrain=current;origin=terrain.transform.position;width=Mathf.CeilToInt(terrain.terrainData.size.x/Cell);height=Mathf.CeilToInt(terrain.terrainData.size.z/Cell);
            var tide=UnityEngine.Object.FindAnyObjectByType<TideSystem>();water=tide!=null?tide.lowTideY:TideSystem.CurrentWaterHeight;maps.Clear();components.Clear();
            var zone=UnityEngine.Object.FindAnyObjectByType<RiverStakeZone>();var list=new List<Bounds>();
            foreach(var stake in zone!=null?zone.fieldStakes:Array.Empty<Transform>())
            {if(stake==null)continue;var renderer=stake.GetComponentInChildren<Renderer>(true);if(renderer!=null)list.Add(renderer.bounds);}
            stakes=list.ToArray();stakeField=stakes.Length>0?stakes[0]:default;foreach(var b in stakes)stakeField.Encapsulate(b);
        }
        public static bool StakeIntersection(Vector3 center,Vector3 half,Quaternion rotation)
        {
            Prepare();var inverse=Quaternion.Inverse(rotation);
            foreach(var stake in stakes)
            {
                // Reserve the horizontal footprint at any tide. This also prevents an
                // allied ship from parking above stakes that become exposed later.
                var p=inverse*(stake.center-center);float radius=new Vector2(stake.extents.x,stake.extents.z).magnitude+1;
                if(Mathf.Abs(p.x)<=half.x+radius&&Mathf.Abs(p.z)<=half.z+radius)return true;
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
            int key=ProfileKey(radius);if(!maps.TryGetValue(key,out var map)){map=new byte[width*height];maps[key]=map;}
            if(map[id]!=0)return map[id]==1;
            float r=Mathf.Ceil(radius/10)*10+Cell*.72f;var p=Point(id);bool clear=!(ReserveInvasionApproach&&stakes.Length>0&&p.z+r>stakeField.max.z-20&&p.x+r>stakeField.min.x-220);
            for(int i=0;i<9&&clear;i++)
            {
                var sample=p+(i==0?Vector3.zero:Quaternion.Euler(0,(i-1)*45,0)*Vector3.forward*r);var local=sample-origin;
                if(local.x<0||local.z<0||local.x>terrain.terrainData.size.x||local.z>terrain.terrainData.size.z||terrain.SampleHeight(sample)+origin.y>water-14){clear=false;break;}
            }
            if(clear)foreach(var stake in stakes)
            {var delta=stake.center-p;delta.y=0;if(delta.magnitude<r+new Vector2(stake.extents.x,stake.extents.z).magnitude){clear=false;break;}}
            if(clear)
            {
                int count=Physics.OverlapBoxNonAlloc(p+Vector3.up*8,new Vector3(r,20,r),hits,Quaternion.identity,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
                clear=count<hits.Length;
                for(int i=0;i<count&&clear;i++)if(StaticObstacle(hits[i]))clear=false;
            }
            map[id]=clear?(byte)1:(byte)2;return clear;
        }
        static int[] Components(float radius)
        {
            int key=ProfileKey(radius);if(components.TryGetValue(key,out var cached))return cached;
            var labels=new int[width*height];var queue=new int[labels.Length];int label=0;
            for(int start=0;start<labels.Length;start++)
            {
                if(labels[start]!=0)continue;if(!Clear(start,radius)){labels[start]=-1;continue;}
                int read=0,write=1;queue[0]=start;labels[start]=++label;
                while(read<write)
                {
                    int at=queue[read++],x=at%width,z=at/width;
                    foreach(int next in new[]{x>0?at-1:-1,x+1<width?at+1:-1,z>0?at-width:-1,z+1<height?at+width:-1})
                    {
                        if(next<0||labels[next]!=0)continue;
                        if(!Clear(next,radius)){labels[next]=-1;continue;}labels[next]=label;queue[write++]=next;
                    }
                }
            }
            components[key]=labels;return labels;
        }
        public static bool NearestClear(Vector3 requested,float radius,out Vector3 result,IList<Vector3> excluded=null,float separation=0,int connectedTo=-1)
        {
            Prepare();int start=Id(requested),x=start%width,z=start/width;float best=float.PositiveInfinity;int chosen=-1,first=-1;
            for(int ring=0;ring<55;ring++)
            {
                if(first>=0&&ring>first+2)break;
                for(int dz=-ring;dz<=ring;dz++)for(int dx=-ring;dx<=ring;dx++)
                {
                    if(Mathf.Abs(dx)!=ring&&Mathf.Abs(dz)!=ring)continue;
                    int nx=x+dx,nz=z+dz;if(nx<0||nz<0||nx>=width||nz>=height)continue;int id=nz*width+nx;
                    var p=Point(id);float score=(p-requested).sqrMagnitude;if(score>=best||!Clear(id,radius)||(connectedTo>=0&&Components(radius)[id]!=Components(radius)[connectedTo]))continue;
                    bool blocked=false;if(excluded!=null)foreach(var other in excluded)if(QuestRowboat.FlatDistance(p,other)<separation){blocked=true;break;}
                    if(blocked)continue;chosen=id;best=score;if(first<0)first=ring;
                }
            }
            result=chosen>=0?Point(chosen):requested;return chosen>=0;
        }
        public static bool FindPath(Vector3 from,Vector3 target,float radius,List<Vector3> path,Transform requester=null,bool adjustGoalForTraffic=true)
        {
            Prepare();path.Clear();
            if(!NearestClear(from,radius,out var startPoint)||!NearestClear(target,radius,out var goalPoint,connectedTo:Id(startPoint)))return false;
            var traffic=new List<BoatHullCollision>();var positions=new List<Vector3>();float spacing=radius*2+30;
            if(requester!=null)foreach(var other in BoatHullCollision.Active)
            {
                if(other.transform==requester||other.SolidHull==null||!other.SolidHull.enabled)continue;
                traffic.Add(other);positions.Add(other.transform.position);spacing=Mathf.Max(spacing,radius+other.Radius+30);
            }
            if(adjustGoalForTraffic&&positions.Count>0&&!NearestClear(target,radius,out goalPoint,positions,spacing,Id(startPoint)))return false;
            bool FreeOfTraffic(int id)
            {
                var p=Point(id);
                foreach(var other in traffic)
                {
                    float distance=QuestRowboat.FlatDistance(p,other.transform.position),initial=QuestRowboat.FlatDistance(from,other.transform.position);
                    if(distance<radius+other.Radius+30&&distance<initial+4)return false;
                }
                return true;
            }
            int start=Id(startPoint),goal=Id(goalPoint),size=width*height;
            var previous=new int[size];var cost=new float[size];var closed=new bool[size];var open=new SortedSet<(float score,int id,int serial)>();int serial=0;
            open.Add((Vector3.Distance(startPoint,goalPoint),start,serial++));
            for(int i=0;i<size;i++){cost[i]=float.PositiveInfinity;previous[i]=-1;}cost[start]=0;
            if(!FreeOfTraffic(goal))return false;
            int iterations=0;
            while(open.Count>0&&iterations<Mathf.Min(size,4096))
            {
                var first=open.Min;open.Remove(first);int current=first.id;if(closed[current])continue;closed[current]=true;iterations++;
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
                    int next=nz*width+nx;if(closed[next]||!Clear(next,radius)||(next!=goal&&!FreeOfTraffic(next)))continue;
                    if(dx!=0&&dz!=0&&(!Clear(z*width+nx,radius)||!Clear(nz*width+x,radius)))continue;
                    float g=cost[current]+Cell*(dx!=0&&dz!=0?1.414214f:1);
                    if(g>=cost[next])continue;cost[next]=g;previous[next]=current;open.Add((g+Vector3.Distance(Point(next),goalPoint),next,serial++));
                }
            }
            return false;
        }
    }
}
