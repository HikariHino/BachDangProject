using System.Collections.Generic;
using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Short bounded paths for swimmers; each executed segment still sweeps solid geometry.</summary>
    public sealed class RiverActorNavigation
    {
        sealed class Node { public Vector2Int cell;public float cost,score;public Node parent;public bool closed; }
        readonly Dictionary<Vector2Int,Node> nodes=new Dictionary<Vector2Int,Node>();
        readonly List<Node> open=new List<Node>();
        readonly List<Vector3> path=new List<Vector3>();
        int waypoint;float nextPlan;Vector3 goal;
        static int planningFrame=-1,plans;
        const float Cell=12;
        public Vector3 Move(Transform actor,Vector3 from,Vector3 target,float travel,float radius,BoatHullCollision carrier=null)
        {
            Physics.SyncTransforms();
            var direct=target-from;direct.y=0;
            if(direct.sqrMagnitude<.001f)return RiverHullCollision.Move(from,from,radius,true,actor,carrier);
            // Probe ahead rather than replanning only after the actor is already touching a pole.
            var look=from+Vector3.ClampMagnitude(direct,Cell*3);
            bool clear=RiverHullCollision.ClearSegment(from,look,radius+.25f,actor,carrier);
            if(clear){path.Clear();waypoint=0;}
            else if(Time.unscaledTime>=nextPlan&&(path.Count==0||waypoint>=path.Count||(goal-target).sqrMagnitude>80*80))
            {
                if(planningFrame!=Time.frameCount){planningFrame=Time.frameCount;plans=0;}
                if(plans<2){plans++;Plan(actor,from,target,radius+.35f,carrier);nextPlan=Time.unscaledTime+.7f;goal=target;}
            }
            while(waypoint<path.Count&&QuestRowboat.FlatDistance(from,path[waypoint])<radius+1)waypoint++;
            var destination=waypoint<path.Count?path[waypoint]:target;destination.y=from.y;
            var moved=RiverHullCollision.Move(from,Vector3.MoveTowards(from,destination,travel),radius,true,actor,carrier);
            if((moved-from).sqrMagnitude<travel*travel*.04f){path.Clear();nextPlan=Mathf.Min(nextPlan,Time.unscaledTime+.15f);}
            return moved;
        }
        void Plan(Transform actor,Vector3 origin,Vector3 target,float radius,BoatHullCollision carrier)
        {
            nodes.Clear();open.Clear();path.Clear();waypoint=0;
            var offset=target-origin;offset.y=0;var localGoal=Vector3.ClampMagnitude(offset,180);
            var destination=new Vector2Int(Mathf.RoundToInt(localGoal.x/Cell),Mathf.RoundToInt(localGoal.z/Cell));
            var start=new Node{cell=Vector2Int.zero,score=localGoal.magnitude};nodes[start.cell]=start;open.Add(start);Node best=start;
            Vector3 Point(Vector2Int c)=>origin+new Vector3(c.x*Cell,0,c.y*Cell);
            for(int count=0;open.Count>0&&count<260;count++)
            {
                int at=0;for(int i=1;i<open.Count;i++)if(open[i].score<open[at].score)at=i;
                var current=open[at];open.RemoveAt(at);current.closed=true;
                if((current.cell-destination).sqrMagnitude<(best.cell-destination).sqrMagnitude)best=current;
                if(current.cell==destination){best=current;break;}
                for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)
                {
                    if(x==0&&z==0)continue;var cell=current.cell+new Vector2Int(x,z);
                    if(Mathf.Abs(cell.x)>18||Mathf.Abs(cell.y)>18)continue;
                    if(nodes.TryGetValue(cell,out var n)&&n.closed)continue;
                    var point=Point(cell);
                    if(carrier!=null&&!carrier.TryDeckSurface(point,out _))continue;
                    if(!RiverHullCollision.ClearSegment(Point(current.cell),point,radius,actor,carrier))continue;
                    float cost=current.cost+Cell*(x!=0&&z!=0?1.414214f:1);
                    if(n!=null&&cost>=n.cost)continue;
                    if(n==null){n=new Node{cell=cell};nodes[cell]=n;open.Add(n);}
                    n.cost=cost;n.score=cost+Vector2Int.Distance(cell,destination)*Cell;n.parent=current;
                }
            }
            while(best!=null&&best.parent!=null){path.Add(Point(best.cell));best=best.parent;}path.Reverse();
        }
    }
}
