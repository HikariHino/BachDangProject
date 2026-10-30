using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Solid hull geometry and a conservative sweep covering translation AND rotation.</summary>
    [DisallowMultipleComponent]
    public sealed class BoatHullCollision : MonoBehaviour
    {
        static readonly Unity.Profiling.ProfilerMarker SweepMarker=new Unity.Profiling.ProfilerMarker("BachDang.HullSweep");
        public static readonly System.Collections.Generic.HashSet<BoatHullCollision> Active=new System.Collections.Generic.HashSet<BoatHullCollision>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void ResetActive()=>Active.Clear();
        void OnEnable()=>Active.Add(this);
        void OnDisable()=>Active.Remove(this);
        [SerializeField] BoxCollider solidHull;
        readonly Collider[] overlaps=new Collider[256];
        struct DeckTriangle
        {
            public Vector3 a,b,c;public float denominator;
        }
        readonly System.Collections.Generic.Dictionary<Vector2Int,System.Collections.Generic.List<DeckTriangle>> deckCells=new System.Collections.Generic.Dictionary<Vector2Int,System.Collections.Generic.List<DeckTriangle>>();
        float deckCellSize=1;
        Transform deckVisual;float deckCeiling;bool deckCached;
        public BoxCollider SolidHull=>solidHull;
        public float Radius {get;private set;}
        public float TrafficPadding=>GetComponent<AmbientRiverBoat>()?.trafficPadding??30;
        public int RejectedMoves {get;private set;}
        public Collider LastBlocker {get;private set;}
        public static BoatHullCollision Get(Transform root)
        {
            var hull=root.GetComponent<BoatHullCollision>();if(hull==null)hull=root.gameObject.AddComponent<BoatHullCollision>();
            if(hull.solidHull==null||hull.deckVisual==null)hull.Initialize();else hull.UpdateRadius();return hull;
        }
        void Awake(){if(solidHull!=null)UpdateRadius();}
        void UpdateRadius(){var half=Vector3.Scale(solidHull.size,Abs(solidHull.transform.lossyScale))*.5f;Radius=new Vector2(half.x,half.z).magnitude+3;}
        static Vector3 Abs(Vector3 p)=>new Vector3(Mathf.Abs(p.x),Mathf.Abs(p.y),Mathf.Abs(p.z));
        public void Initialize()
        {
            var visual=transform.Find("DaiVietPatrolVisual_Rebuilt")??transform.Find("RedSailJunkVisual")??transform.Find("Thuyen_Go_Song");
            if(visual==null)throw new System.InvalidOperationException("No hull visual: "+name);
            bool found=false;Bounds bounds=default;
            foreach(var mesh in visual.GetComponentsInChildren<MeshFilter>(true))
            {
                if(mesh.sharedMesh==null)continue;var b=mesh.sharedMesh.bounds;var matrix=transform.worldToLocalMatrix*mesh.transform.localToWorldMatrix;
                for(int i=0;i<8;i++)
                {
                    var corner=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    var p=matrix.MultiplyPoint3x4(corner);if(!found){bounds=new Bounds(p,Vector3.zero);found=true;}else bounds.Encapsulate(p);
                }
            }
            if(!found)throw new System.InvalidOperationException("No hull meshes: "+name);
            if(solidHull==null)
            {
                var go=new GameObject("Solid_River_Hull");go.transform.SetParent(transform,false);solidHull=go.AddComponent<BoxCollider>();
            }
            var scale=Abs(transform.lossyScale);float top=GetComponent<QuestRowboat>()!=null?10:19;
            var deck=transform.Find("Boarding_Port");if(deck!=null)top=transform.InverseTransformPoint(deck.position).y*scale.y;
            var crew=GetComponentInChildren<FleetDeckCrew>(true);
            if(crew!=null&&crew.soldiers.Length>0)
            {
                var rig=crew.soldiers[0].GetComponentInChildren<Animator>();
                var left=rig!=null&&rig.isHuman?rig.GetBoneTransform(HumanBodyBones.LeftFoot):null;
                var right=rig!=null&&rig.isHuman?rig.GetBoneTransform(HumanBodyBones.RightFoot):null;
                // Avatar roots sit above their feet. The solid deck ends at the
                // actual soles, not halfway up every soldier's body.
                if(left!=null&&right!=null)top=transform.InverseTransformPoint(left.position.y<right.position.y?left.position:right.position).y*scale.y;
            }
            float height=Mathf.Max(5,top-bounds.min.y*scale.y);
            var center=bounds.center;center.y=bounds.min.y+height/(2*scale.y);
            solidHull.center=center;solidHull.size=new Vector3(bounds.size.x,height/scale.y,bounds.size.z);solidHull.isTrigger=false;
            deckVisual=visual;deckCeiling=top/scale.y;deckCached=false;deckCells.Clear();
            foreach(var old in GetComponentsInChildren<BoxCollider>(true))
                if(old!=solidHull&&!old.isTrigger&&old.GetComponentInParent<Animator>()==null)old.enabled=false;
            var body=GetComponent<Rigidbody>();if(body==null)body=gameObject.AddComponent<Rigidbody>();
            body.useGravity=false;body.isKinematic=true;body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
            UpdateRadius();
        }
        void CacheDeck(Transform visual,float ceiling)
        {
            deckCells.Clear();deckCellSize=16/Mathf.Max(.01f,Mathf.Abs(transform.lossyScale.x));
            foreach(var filter in visual.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh=filter.sharedMesh;if(mesh==null||!mesh.isReadable)continue;
                var matrix=transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                var vertices=mesh.vertices;for(int i=0;i<vertices.Length;i++)vertices[i]=matrix.MultiplyPoint3x4(vertices[i]);
                var indices=mesh.triangles;
                for(int i=0;i<indices.Length;i+=3)
                {
                    var a=vertices[indices[i]];var b=vertices[indices[i+1]];var c=vertices[indices[i+2]];
                    var normal=Vector3.Cross(b-a,c-a);
                    if(normal.sqrMagnitude<.0000001f||Mathf.Abs(normal.normalized.y)<.65f||Mathf.Min(a.y,Mathf.Min(b.y,c.y))>ceiling+.05f)continue;
                    float den=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);if(Mathf.Abs(den)<.000001f)continue;
                    var triangle=new DeckTriangle{a=a,b=b,c=c,denominator=den};
                    int minX=Mathf.FloorToInt(Mathf.Min(a.x,Mathf.Min(b.x,c.x))/deckCellSize),maxX=Mathf.FloorToInt(Mathf.Max(a.x,Mathf.Max(b.x,c.x))/deckCellSize);
                    int minZ=Mathf.FloorToInt(Mathf.Min(a.z,Mathf.Min(b.z,c.z))/deckCellSize),maxZ=Mathf.FloorToInt(Mathf.Max(a.z,Mathf.Max(b.z,c.z))/deckCellSize);
                    for(int x=minX;x<=maxX;x++)for(int z=minZ;z<=maxZ;z++)
                    {var key=new Vector2Int(x,z);if(!deckCells.TryGetValue(key,out var cell))deckCells[key]=cell=new System.Collections.Generic.List<DeckTriangle>();cell.Add(triangle);}
                }
            }
        }
        /// <summary>The timber directly below this point; the navigation box also covers raised decks and is not a floor.</summary>
        public bool TryDeckSurface(Vector3 world,out Vector3 surface)
        {
            if(!deckCached){CacheDeck(deckVisual,deckCeiling);deckCached=true;}
            var p=transform.InverseTransformPoint(world);surface=default;
            if(!deckCells.TryGetValue(new Vector2Int(Mathf.FloorToInt(p.x/deckCellSize),Mathf.FloorToInt(p.z/deckCellSize)),out var cell))return false;
            float best=float.NegativeInfinity;
            foreach(var t in cell)
            {
                float u=((t.b.z-t.c.z)*(p.x-t.c.x)+(t.c.x-t.b.x)*(p.z-t.c.z))/t.denominator;
                float v=((t.c.z-t.a.z)*(p.x-t.c.x)+(t.a.x-t.c.x)*(p.z-t.c.z))/t.denominator;
                if(u<-.0001f||v<-.0001f||u+v>1.0001f)continue;
                best=Mathf.Max(best,u*t.a.y+v*t.b.y+(1-u-v)*t.c.y);
            }
            if(float.IsNegativeInfinity(best))return false;p.y=best;surface=transform.TransformPoint(p);return true;
        }
        public bool IsOwn(Collider collider)
        {
            if(collider.transform.IsChildOf(transform))return true;
            var actor=collider.GetComponentInParent<ActorSolidBody>();if(actor!=null&&actor.Carrier==transform)return true;
            var session=BeachBoatQuestSession.Instance;var boarding=RiverBoardingCombat.Instance;
            if(boarding!=null&&boarding.PlayerDeckCarrier==transform&&session!=null&&collider.transform.IsChildOf(session.player.transform))return true;
            return GetComponent<QuestRowboat>()!=null&&session!=null&&QuestRowboat.Instance.IsAboard&&(boarding==null||!boarding.PlayerAway)&&collider.transform.IsChildOf(session.player.transform);
        }
        /// <summary>F leaves from an actual gunwale, even on a large warship or a raised stern.</summary>
        public bool TryWaterExit(Vector3 from,float radius,Transform actor,out Vector3 exit)
        {
            var local=transform.InverseTransformPoint(from);var scale=Abs(transform.lossyScale);
            float preferred=local.x<solidHull.center.x?-1:1;
            foreach(float side in new[]{preferred,-preferred})
            for(int offset=0;offset<9;offset++)
            {
                float along=offset==0?0:((offset+1)/2)*(offset%2==0?-1:1)*16/scale.z;
                var point=solidHull.center;
                point.x+=side*(solidHull.size.x*.5f+(radius+5)/scale.x);
                point.z=Mathf.Clamp(local.z+along,point.z-solidHull.size.z*.45f,point.z+solidHull.size.z*.45f);
                exit=transform.TransformPoint(point);exit.y=TideSystem.CurrentWaterHeight-2;
                if(RiverHullCollision.ClearAt(exit,radius,actor)){exit.y-=3;return true;}
            }
            exit=from;return false;
        }
        public bool TryClearDeckLanding(Vector3 near,float radius,Transform actor,out Vector3 floor)
        {
            var at=transform.InverseTransformPoint(near);var scale=Abs(transform.lossyScale);
            for(int ring=0;ring<5;ring++)for(int angle=0;angle<(ring==0?1:8);angle++)
            {
                var p=at+new Vector3(Mathf.Cos(angle*Mathf.PI*.25f)*ring*12/scale.x,0,Mathf.Sin(angle*Mathf.PI*.25f)*ring*12/scale.z);
                p.x=Mathf.Clamp(p.x,solidHull.center.x-solidHull.size.x*.5f+(radius+4)/scale.x,solidHull.center.x+solidHull.size.x*.5f-(radius+4)/scale.x);
                p.z=Mathf.Clamp(p.z,solidHull.center.z-solidHull.size.z*.5f+(radius+4)/scale.z,solidHull.center.z+solidHull.size.z*.5f-(radius+4)/scale.z);
                if(TryDeckSurface(transform.TransformPoint(p),out floor)&&RiverHullCollision.ClearAt(floor+Vector3.up*8,radius,actor,this))return true;
            }
            floor=default;return false;
        }
        bool EscapesExistingContact(Collider obstacle,Vector3 from,Quaternion fromRotation,Vector3 to,Quaternion toRotation)
        {
            var offset=transform.InverseTransformPoint(solidHull.transform.position);var relative=Quaternion.Inverse(transform.rotation)*solidHull.transform.rotation;
            Vector3 At(Vector3 p,Quaternion r)=>p+r*Vector3.Scale(offset,transform.lossyScale);
            if(!Physics.ComputePenetration(solidHull,At(from,fromRotation),fromRotation*relative,obstacle,obstacle.transform.position,obstacle.transform.rotation,out var normal,out float oldDepth))return false;
            if(!Physics.ComputePenetration(solidHull,At(to,toRotation),toRotation*relative,obstacle,obstacle.transform.position,obstacle.transform.rotation,out _,out float newDepth))return true;
            // Only release an overlap already present in an older checkpoint. Never
            // deepen it or tunnel through a new pole/hull while backing away.
            return newDepth<oldDepth-.00005f||newDepth<=oldDepth+.00005f&&Vector3.Dot(to-from,normal)>.00005f;
        }
        public bool CanMove(Vector3 from,Quaternion fromRotation,Vector3 to,Quaternion toRotation,bool reserveStakes=true,bool allowBankContact=false)
        {
            using var sample=SweepMarker.Auto();
            if(solidHull==null)Initialize();
            Physics.SyncTransforms();LastBlocker=null;
            var scale=Abs(transform.lossyScale);var local=Vector3.Scale(solidHull.center,scale);
            var a=from+fromRotation*local;var b=to+toRotation*local;
            var mid=Quaternion.Slerp(fromRotation,toRotation,.5f);
            float angle=Quaternion.Angle(fromRotation,toRotation)*Mathf.Deg2Rad;
            // Inflation bounds every corner throughout the entire step, including a
            // stationary turn. A thin stake cannot fall between endpoint samples.
            var delta=a-b;float margin=new Vector2(delta.x,delta.z).magnitude*.5f+(Radius+local.magnitude)*angle*.5f+.35f;
            float tilt=Mathf.Acos(Mathf.Clamp(Vector3.Dot(fromRotation*Vector3.up,toRotation*Vector3.up),-1,1));
            // Horizontal travel/yaw must not enlarge the keel down into submerged
            // stakes. Only actual heave/roll contributes to the vertical sweep.
            float vertical=Mathf.Abs(delta.y)*.5f+(Radius+local.magnitude)*tilt*.5f+.15f;
            var half=Vector3.Scale(solidHull.size,scale)*.5f+new Vector3(margin,vertical,margin);
            int count=Physics.OverlapBoxNonAlloc((a+b)*.5f,half,overlaps,mid,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            if(count==overlaps.Length){RejectedMoves++;return false;}
            for(int i=0;i<count;i++)
            {
                var c=overlaps[i];if(c==null||IsOwn(c)||c.GetComponent<TideSystem>()!=null)continue;
                // A grounded rowboat has separately sampled keel support and may
                // slide toward deeper water while touching sand. Other solids stay hard.
                if(allowBankContact&&c is TerrainCollider&&GetComponent<QuestRowboat>()!=null)continue;
                if(EscapesExistingContact(c,from,fromRotation,to,toRotation))continue;
                LastBlocker=c;RejectedMoves++;
                var rowboat=c.GetComponentInParent<QuestRowboat>();
                if(rowboat!=null&&rowboat.transform!=transform&&Time.deltaTime>0)
                    rowboat.ApplyCollisionImpact(QuestRowboat.BoatObstacleKind.Ship,Vector3.Distance(from,to)/Time.deltaTime);
                return false;
            }
            if(reserveStakes&&RiverWaterNavigation.StakeIntersection((a+b)*.5f,half,mid))
            {RejectedMoves++;return false;}
            return true;
        }
        public bool CanOccupy(Vector3 position,Quaternion rotation,bool reserveStakes=true)=>CanMove(position,rotation,position,rotation,reserveStakes);
    }
}
