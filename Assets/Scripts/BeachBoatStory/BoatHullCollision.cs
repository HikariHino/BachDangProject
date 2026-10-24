using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Solid hull geometry and a conservative sweep covering translation AND rotation.</summary>
    [DisallowMultipleComponent]
    public sealed class BoatHullCollision : MonoBehaviour
    {
        public static readonly System.Collections.Generic.HashSet<BoatHullCollision> Active=new System.Collections.Generic.HashSet<BoatHullCollision>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void ResetActive()=>Active.Clear();
        void OnEnable()=>Active.Add(this);
        void OnDisable()=>Active.Remove(this);
        [SerializeField] BoxCollider solidHull;
        readonly Collider[] overlaps=new Collider[256];
        public BoxCollider SolidHull=>solidHull;
        public float Radius {get;private set;}
        public int RejectedMoves {get;private set;}
        public Collider LastBlocker {get;private set;}
        public static BoatHullCollision Get(Transform root)
        {
            var hull=root.GetComponent<BoatHullCollision>();if(hull==null)hull=root.gameObject.AddComponent<BoatHullCollision>();
            if(hull.solidHull==null)hull.Initialize();else hull.UpdateRadius();return hull;
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
            foreach(var old in GetComponentsInChildren<BoxCollider>(true))
                if(old!=solidHull&&!old.isTrigger&&old.GetComponentInParent<Animator>()==null)old.enabled=false;
            var body=GetComponent<Rigidbody>();if(body==null)body=gameObject.AddComponent<Rigidbody>();
            body.useGravity=false;body.isKinematic=true;body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
            UpdateRadius();
        }
        public bool IsOwn(Collider collider)
        {
            if(collider.transform.IsChildOf(transform))return true;
            var actor=collider.GetComponentInParent<ActorSolidBody>();if(actor!=null&&actor.Carrier==transform)return true;
            var session=BeachBoatQuestSession.Instance;var boarding=RiverBoardingCombat.Instance;
            return GetComponent<QuestRowboat>()!=null&&session!=null&&QuestRowboat.Instance.IsAboard&&(boarding==null||!boarding.PlayerAway)&&collider.transform.IsChildOf(session.player.transform);
        }
        public bool CanMove(Vector3 from,Quaternion fromRotation,Vector3 to,Quaternion toRotation,bool reserveStakes=true)
        {
            if(solidHull==null)Initialize();
            Physics.SyncTransforms();
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
                LastBlocker=c;RejectedMoves++;return false;
            }
            if(reserveStakes&&RiverWaterNavigation.StakeIntersection((a+b)*.5f,half,mid))
            {RejectedMoves++;return false;}
            return true;
        }
        public bool CanOccupy(Vector3 position,Quaternion rotation,bool reserveStakes=true)=>CanMove(position,rotation,position,rotation,reserveStakes);
    }
}
