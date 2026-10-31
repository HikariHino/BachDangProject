using System.Collections.Generic;
using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Short bounded paths for swimmers; each executed segment still sweeps solid geometry.</summary>
    public sealed class RiverActorNavigation
    {
        public Vector3 LastDirection {get;private set;}
        sealed class Node { public Vector2Int cell;public float cost,score;public Node parent;public bool closed; }
        readonly Dictionary<Vector2Int,Node> nodes=new Dictionary<Vector2Int,Node>();
        readonly List<Node> open=new List<Node>();
        readonly List<Vector3> path=new List<Vector3>();
        int waypoint;float nextPlan,nextProbe;bool clearProbe;Vector3 goal;
        static int planningFrame=-1,plans;
        public void Reset(){path.Clear();open.Clear();nodes.Clear();waypoint=0;nextPlan=nextProbe=0;clearProbe=false;goal=Vector3.zero;LastDirection=Vector3.zero;}
        const float Cell=12;
        static readonly float[] passingAngles={45f,-45f,80f,-80f,115f,-115f};
        readonly Collider[] neighbors=new Collider[48];
        Vector3 SeparateCrowdPose(Transform actor,Vector3 from,Vector3 position,float radius,bool turning=true)
        {
            var own=actor.GetComponent<ActorSolidBody>()?.SolidCollider??actor.GetComponent<Collider>();
            if(own==null||own.isTrigger)return position;
            // A swimming capsule is long and turns after movement. The sphere
            // sweep alone protects its centre but can leave the legs embedded
            // in another swimmer after that turn, even at an arrived goal.
            for(int pass=0;pass<3;pass++)
            {
                var motion=position-from;motion.y=0;
                var facing=turning&&motion.sqrMagnitude>.001f?Quaternion.LookRotation(motion):actor.rotation;
                var turn=facing*Quaternion.Inverse(actor.rotation);
                var predicted=actor.position+(position-from)+turn*(own.transform.position-actor.position);
                int count=Physics.OverlapSphereNonAlloc(predicted,42,neighbors,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
                bool changed=false;
                for(int i=0;i<count;i++)
                {
                    var other=neighbors[i];var body=other.GetComponentInParent<ActorSolidBody>();
                    if(body==null||body.transform==actor||body.transform.IsChildOf(actor)||Mathf.Abs(body.transform.position.y-actor.position.y)>12)continue;
                    if(!Physics.ComputePenetration(own,predicted,turn*own.transform.rotation,other,other.transform.position,other.transform.rotation,out var normal,out float depth))continue;
                    var horizontal=normal;horizontal.y=0;float planar=horizontal.magnitude;if(planar<.1f)continue;
                    var correction=horizontal/planar*(depth/planar+.04f);
                    if(!RiverHullCollision.ClearSegment(position,position+correction,radius,actor,null,true))continue;
                    position+=correction;predicted+=correction;changed=true;
                }
                if(!changed)break;
            }
            return position;
        }
        Vector3 SteerCrowd(Transform actor,Vector3 from,Vector3 desired,float radius)
        {
            var direction=(desired-from).normalized;var push=Vector3.zero;var right=Vector3.Cross(Vector3.up,direction);int id=actor.GetEntityId().GetHashCode();
            var own=actor.GetComponent<ActorSolidBody>()?.SolidCollider??actor.GetComponent<Collider>();float ownHalf=own!=null?Mathf.Max(own.bounds.extents.x,own.bounds.extents.z):radius;
            int count=Physics.OverlapSphereNonAlloc(from+Vector3.up*4,42,neighbors,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)
            {
                var other=neighbors[i].GetComponentInParent<ActorSolidBody>();if(other==null||other.transform.IsChildOf(actor)||Mathf.Abs(other.transform.position.y-from.y)>12)continue;
                var away=from-other.transform.position;away.y=0;float distance=away.magnitude;float otherHalf=Mathf.Max(neighbors[i].bounds.extents.x,neighbors[i].bounds.extents.z);float gap=Mathf.Max(22,ownHalf+otherHalf+radius*2+2);
                if(distance<gap+14&&Vector3.Dot(-away.normalized,direction)>.35f&&Vector3.Dot(other.transform.forward,direction)<.1f)push+=right*.9f;
                if(distance>=gap)continue;
                if(distance<.01f){int oid=other.transform.GetEntityId().GetHashCode();float a=((System.Math.Min(id,oid)^System.Math.Max(id,oid))&1023)*Mathf.PI/512;away=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*(id<oid?1:-1);distance=.01f;}
                float weight=1-distance/gap;push+=away.normalized*weight*1.3f;
                if(Vector3.Dot(-away.normalized,direction)>.2f)push+=right*weight;
            }
            return Vector3.ClampMagnitude(direction+push,1);
        }
        public Vector3 Move(Transform actor,Vector3 from,Vector3 target,float travel,float radius,BoatHullCollision carrier=null)
        {
            LastDirection=Vector3.zero;
            var direct=target-from;direct.y=0;
            if(direct.sqrMagnitude<.001f){var resting=RiverHullCollision.Move(from,from,radius,true,actor,carrier);return carrier==null?SeparateCrowdPose(actor,from,resting,radius,false):resting;}
            // Probe ahead rather than replanning only after the actor is already touching a pole.
            var look=from+Vector3.ClampMagnitude(direct,Cell*3);
            // Static route probing need not repeat for every actor every frame.
            // Actual movement below still synchronizes and sweeps every segment.
            if(Time.unscaledTime>=nextProbe){nextProbe=Time.unscaledTime+.15f;clearProbe=RiverHullCollision.ClearSegment(from,look,radius+.25f,actor,carrier,true);}
            bool clear=clearProbe;
            if(clear){path.Clear();waypoint=0;}
            else if(Time.unscaledTime>=nextPlan&&(path.Count==0||waypoint>=path.Count||(goal-target).sqrMagnitude>80*80))
            {
                if(planningFrame!=Time.frameCount){planningFrame=Time.frameCount;plans=0;}
                if(plans<2){plans++;Plan(actor,from,target,radius+.35f,carrier);nextPlan=Time.unscaledTime+.7f;goal=target;}
            }
            while(waypoint<path.Count&&QuestRowboat.FlatDistance(from,path[waypoint])<radius+1)waypoint++;
            var destination=waypoint<path.Count?path[waypoint]:target;destination.y=from.y;
            var step=Vector3.ClampMagnitude(destination-from,travel);
            if(carrier==null)step=SteerCrowd(actor,from,destination,radius)*Mathf.Min(travel,(destination-from).magnitude);
            var moved=RiverHullCollision.Move(from,from+step,radius,true,actor,carrier);
            if(carrier==null&&(moved-from).sqrMagnitude<travel*travel*.06f)
            {
                // Pick a clear passing direction instead of repeatedly walking into
                // the same moving capsule. Every candidate still performs a solid sweep.
                float best=(moved-from).sqrMagnitude;
                foreach(float angle in passingAngles)
                {var side=Quaternion.AngleAxis(angle,Vector3.up)*direct.normalized*travel;var attempt=RiverHullCollision.Move(from,from+side,radius,false,actor,carrier);float score=(attempt-from).sqrMagnitude;if(score>best*1.1f){best=score;moved=attempt;}}
            }
            if((moved-from).sqrMagnitude<travel*travel*.04f){path.Clear();nextPlan=Mathf.Min(nextPlan,Time.unscaledTime+.15f);}
            if(carrier==null)moved=SeparateCrowdPose(actor,from,moved,radius);
            var motion=moved-from;motion.y=0;LastDirection=motion.sqrMagnitude>.001f?motion.normalized:Vector3.zero;return moved;
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
                    if(!RiverHullCollision.ClearSegment(Point(current.cell),point,radius,actor,carrier,true))continue;
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
