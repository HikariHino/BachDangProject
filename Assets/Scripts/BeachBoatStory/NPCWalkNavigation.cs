using System.Collections.Generic;
using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Small, local walking routes around solid props. Raised routes require floor support.</summary>
    public sealed class NPCWalkNavigation
    {
        readonly NPCWander actor;
        readonly Terrain terrain;
        readonly bool raised;
        readonly float scale,height,feetOffset,groundOffset,surfaceOffset;
        public readonly float Radius;
        readonly RaycastHit[] hits=new RaycastHit[64];
        readonly Collider[] overlaps=new Collider[64];
        static int planningFrame=-1,plansThisFrame;
        sealed class Node
        {
            public Vector2Int cell;
            public Vector3 point;
            public float cost,score;
            public Node parent;
            public bool closed;
        }
        public NPCWalkNavigation(NPCWander actor,Terrain terrain,float scale,bool raised)
        {
            this.actor=actor;this.terrain=terrain;this.scale=scale;this.raised=raised;
            var rig=actor.animator;
            height=EnemyDeckGuard.RigHeight(rig);
            feetOffset=Mathf.Min(rig.GetBoneTransform(HumanBodyBones.LeftFoot).position.y,rig.GetBoneTransform(HumanBodyBones.RightFoot).position.y)-actor.transform.position.y;
            Radius=Mathf.Max(1.5f,height*.16f)+.15f;
            groundOffset=terrain!=null?Mathf.Clamp(actor.transform.position.y-Ground(actor.transform.position),0,scale*5):0;
            surfaceOffset=TryFloor(actor.transform.position,out float floor)?actor.transform.position.y-floor:0;
        }
        float Ground(Vector3 p)=>terrain.SampleHeight(p)+terrain.transform.position.y;
        bool Own(Collider c)=>c.transform.IsChildOf(actor.transform)||actor.transform.IsChildOf(c.transform);
        static bool Person(Collider c)=>c.GetComponentInParent<Animator>()!=null||c.GetComponentInParent<Character_Movement>()!=null;
        void Capsule(Vector3 p,out Vector3 lower,out Vector3 upper)
        {
            lower=p+Vector3.up*(feetOffset+Radius+.15f);
            upper=p+Vector3.up*(feetOffset+Mathf.Max(Radius,height));
        }
        bool TryFloor(Vector3 point,out float floor)
        {
            int count=Physics.RaycastNonAlloc(point+Vector3.up*(scale*.45f),Vector3.down,hits,scale*1.25f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            floor=float.NegativeInfinity;
            for(int i=0;i<count;i++)
            {
                var hit=hits[i];
                if(Own(hit.collider)||Person(hit.collider)||hit.normal.y<.7f)continue;
                if(hit.point.y>floor)floor=hit.point.y;
            }
            return !float.IsNegativeInfinity(floor);
        }
        public bool Project(Vector3 point,out Vector3 result)
        {
            result=point;
            if(terrain!=null)
            {
                var local=point-terrain.transform.position;var size=terrain.terrainData.size;
                if(local.x<0||local.z<0||local.x>size.x||local.z>size.z||Ground(point)<=TideSystem.CurrentWaterHeight+1)return false;
                result.y=Ground(point)+groundOffset;
            }
            if(!raised)return true;
            if(!TryFloor(result,out float floor))return false;
            result.y=floor+surfaceOffset;
            // A capsule must fit on the deck, including when turning by the edge.
            for(int i=0;i<4;i++)
            {
                var offset=(i<2?Vector3.right:Vector3.forward)*(i%2==0?1:-1)*Radius;
                if(!TryFloor(result+offset,out float edge)||Mathf.Abs(edge-floor)>scale*.5f)return false;
            }
            return Mathf.Abs(result.y-point.y)<scale;
        }
        public bool Occupied(Vector3 p,bool people)
        {
            Capsule(p,out var lower,out var upper);
            int count=Physics.OverlapCapsuleNonAlloc(lower,upper,Radius,overlaps,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)
            {
                var c=overlaps[i];if(Own(c)||c is TerrainCollider||(!people&&Person(c)))continue;
                return true;
            }
            return count==overlaps.Length;
        }
        public bool Clear(Vector3 from,Vector3 to,bool people)
        {
            var direction=to-from;direction.y=0;float length=direction.magnitude;
            if(length<.001f)return !Occupied(to,people);
            Capsule(from,out var lower,out var upper);
            int count=Physics.CapsuleCastNonAlloc(lower,upper,Radius,direction/length,hits,length+.08f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)
            {
                var h=hits[i];var c=h.collider;
                if(Own(c)||(!people&&Person(c)))continue;
                if(h.normal.y>.65f&&h.point.y<=Mathf.Max(from.y,to.y)+feetOffset+.2f)continue;
                return false;
            }
            return count<hits.Length&&!Occupied(to,people);
        }
        // Only used once at spawn: old authored actors can start inside newly solid props.
        public bool FindSpawn(Vector3 original,out Vector3 clear)
        {
            if(Project(original,out clear)&&!Occupied(clear,true))return true;
            for(int ring=1;ring<=5;ring++)for(int i=0;i<24;i++)
            {
                float angle=i*Mathf.PI/12;
                var p=original+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*Radius*ring*1.25f;
                if(Project(p,out clear)&&!Occupied(clear,true))return true;
            }
            clear=original;return false;
        }
        public bool Route(Vector3 start,Vector3 destination,List<Vector3> route)
        {
            route.Clear();
            if(!Project(destination,out destination)||Occupied(destination,false))return false;
            if(Clear(start,destination,false)&&SupportedSegment(start,destination)){route.Add(destination);return true;}
            // Spread blocked-route searches across frames in dense settlements.
            // Straight, clear walking routes do not consume this budget.
            if(planningFrame!=Time.frameCount){planningFrame=Time.frameCount;plansThisFrame=0;}
            if(plansThisFrame>=2)return false;plansThisFrame++;
            float cellSize=Mathf.Max(Radius*1.8f,scale*.8f);
            var goal=new Vector2Int(Mathf.RoundToInt((destination.x-start.x)/cellSize),Mathf.RoundToInt((destination.z-start.z)/cellSize));
            var nodes=new Dictionary<Vector2Int,Node>();var open=new List<Node>();
            var first=new Node{cell=Vector2Int.zero,point=start};nodes.Add(first.cell,first);open.Add(first);
            for(int expanded=0;open.Count>0&&expanded<384;expanded++)
            {
                int best=0;for(int i=1;i<open.Count;i++)if(open[i].score<open[best].score)best=i;
                var node=open[best];open.RemoveAt(best);node.closed=true;
                if(Vector2Int.Distance(node.cell,goal)<1.5f&&Clear(node.point,destination,false)&&SupportedSegment(node.point,destination))
                {
                    route.Add(destination);for(var p=node;p.parent!=null;p=p.parent)route.Add(p.point);route.Reverse();return true;
                }
                for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)
                {
                    if(x==0&&z==0)continue;
                    var cell=node.cell+new Vector2Int(x,z);
                    if(cell.x<Mathf.Min(0,goal.x)-10||cell.x>Mathf.Max(0,goal.x)+10||cell.y<Mathf.Min(0,goal.y)-10||cell.y>Mathf.Max(0,goal.y)+10)continue;
                    if(nodes.TryGetValue(cell,out var next)&&next.closed)continue;
                    var p=start+new Vector3(cell.x*cellSize,0,cell.y*cellSize);
                    if(!Project(p,out p)||!Clear(node.point,p,false)||!SupportedSegment(node.point,p))continue;
                    float cost=node.cost+Vector3.Distance(node.point,p);
                    if(next!=null&&cost>=next.cost)continue;
                    if(next==null){next=new Node{cell=cell};nodes.Add(cell,next);open.Add(next);}
                    next.point=p;next.cost=cost;next.score=cost+QuestRowboat.FlatDistance(p,destination);next.parent=node;
                }
            }
            return false;
        }
        bool SupportedSegment(Vector3 a,Vector3 b)
        {
            float length=QuestRowboat.FlatDistance(a,b);
            for(float d=Radius;d<length;d+=Mathf.Max(Radius,scale))
                if(!Project(Vector3.Lerp(a,b,d/length),out var point)||Mathf.Abs(point.y-Mathf.Lerp(a.y,b.y,d/length))>scale*.5f)return false;
            return true;
        }
    }
}
