using System.Collections.Generic;
using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Temporary construction boats and soldiers, owned by the work session.</summary>
    public sealed class StakeWorkCrew
    {
        sealed class Worker
        {
            public Transform boat,actor,stake,hammer;public Animator rig;public Transform[] arms;public Quaternion[] rest;public Transform right,left;public float phase,offset;public Vector3 waterPosition;
        }
        readonly List<GameObject> boats=new List<GameObject>();readonly List<Worker> workers=new List<Worker>();
        readonly BowGripProfile grip=BowGripProfile.Load();readonly Material timber;
        public int Count=>workers.Count;
        public StakeWorkCrew(QuestRowboat source,Transform template,Transform parent,Material material)
        {
            timber=material;var sourceHull=BoatHullCollision.Get(source.transform);var box=sourceHull.SolidHull;var filters=source.GetComponentsInChildren<MeshFilter>(true);
            for(int i=0;i<2;i++)
            {
                Vector3 at=source.transform.position+source.transform.right*(i==0?-90:90)+source.transform.forward*45;
                bool clear=false;
                for(int n=0;n<8;n++)
                {
                    var candidate=at+source.transform.forward*n*35;bool dry=false;
                    foreach(var terrain in Terrain.activeTerrains){var local=candidate-terrain.transform.position;var size=terrain.terrainData.size;if(local.x>=0&&local.z>=0&&local.x<=size.x&&local.z<=size.z&&terrain.SampleHeight(candidate)+terrain.transform.position.y>TideSystem.CurrentWaterHeight-8)dry=true;}
                    if(!dry&&RiverHullCollision.ClearAt(candidate+Vector3.up*8,42,null,sourceHull)){at=candidate;clear=true;break;}
                }
                if(!clear)continue;
                var boat=new GameObject("Thuyen_cong_binh_"+i);boat.transform.SetParent(parent,true);boat.transform.SetPositionAndRotation(at,source.transform.rotation);boat.transform.localScale=source.transform.lossyScale;boats.Add(boat);
                foreach(var f in filters)
                {
                    if(f.sharedMesh==null||f.GetComponentInParent<Animator>()!=null)continue;var r=f.GetComponent<MeshRenderer>();if(r==null||!r.enabled||f.transform.IsChildOf(source.paddle))continue;
                    var go=new GameObject("Construction_"+f.name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(boat.transform,false);go.transform.localPosition=source.transform.InverseTransformPoint(f.transform.position);go.transform.localRotation=Quaternion.Inverse(source.transform.rotation)*f.transform.rotation;
                    var s=source.transform.lossyScale;var v=f.transform.lossyScale;go.transform.localScale=new Vector3(v.x/s.x,v.y/s.y,v.z/s.z);go.GetComponent<MeshFilter>().sharedMesh=f.sharedMesh;go.GetComponent<Renderer>().sharedMaterials=r.sharedMaterials;
                }
                var solid=boat.AddComponent<BoxCollider>();solid.center=box.center;solid.size=box.size;var body=boat.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;
                for(int member=0;member<2;member++)
                {
                    var local=source.transform.InverseTransformPoint(source.seat.position);local.x=box.center.x;local.z+=(member==0?-12:12)/Mathf.Abs(source.transform.lossyScale.z);
                    var reference=source.transform.TransformPoint(local);if(!sourceHull.TryDeckSurface(reference,out var floor))continue;
                    var actor=new GameObject("Cong_binh_"+i+"_"+member).transform;actor.SetParent(parent,true);actor.position=boat.transform.TransformPoint(source.transform.InverseTransformPoint(floor));actor.rotation=boat.transform.rotation*Quaternion.Euler(0,90,0);
                    var model=Object.Instantiate(RiverBoardingCombat.Instance.allyPrefab,actor,false);var rig=model.GetComponentInChildren<Animator>();rig.runtimeAnimatorController=RiverBoardingCombat.Instance.FootController;rig.enabled=true;rig.Update(0);
                    rig.transform.localScale*=EnemyDeckGuard.RigHeight(BeachBoatQuestSession.Instance.player.GetComponentInChildren<Animator>())/Mathf.Max(.01f,EnemyDeckGuard.RigHeight(rig));rig.Update(0);
                    ActorSolidBody.WalkingDimensions(rig,out _,out float sole,out _);actor.position-=Vector3.up*sole;rig.enabled=false;
                    foreach(var c in model.GetComponentsInChildren<Collider>(true)){c.enabled=false;Object.Destroy(c);}
                    ActorSolidBody.Ensure(actor.gameObject,rig);
                    var stake=StakeWorkVisual.Create("Coc_nhon_cong_binh",parent,template);var pole=boat.transform.TransformPoint(new Vector3(box.center.x+box.size.x*.5f+5/Mathf.Abs(source.transform.lossyScale.x),0,local.z));pole.y=TideSystem.CurrentWaterHeight+6;stake.position=pole;stake.rotation=boat.transform.rotation*Quaternion.Euler(0,0,-12);
                    var hammer=new GameObject("Bua_cong_binh").transform;hammer.SetParent(parent,true);Part(hammer,PrimitiveType.Cylinder,Vector3.zero,new Vector3(.6f,5.5f,.6f));Part(hammer,PrimitiveType.Cube,Vector3.up*4,new Vector3(4.5f,2.5f,2.5f));
                    var arms=new[]{rig.GetBoneTransform(HumanBodyBones.RightUpperArm),rig.GetBoneTransform(HumanBodyBones.RightLowerArm),rig.GetBoneTransform(HumanBodyBones.LeftUpperArm),rig.GetBoneTransform(HumanBodyBones.LeftLowerArm)};
                    var rest=new Quaternion[4];for(int a=0;a<4;a++)rest[a]=arms[a].localRotation;
                    workers.Add(new Worker{boat=boat.transform,actor=actor,stake=stake,hammer=hammer,rig=rig,arms=arms,rest=rest,right=rig.GetBoneTransform(HumanBodyBones.RightHand),left=rig.GetBoneTransform(HumanBodyBones.LeftHand),phase=(i*2+member)*.8f,waterPosition=pole,offset=at.y-TideSystem.CurrentWaterHeight});
                }
            }
            Physics.SyncTransforms();
        }
        void Part(Transform parent,PrimitiveType type,Vector3 at,Vector3 size)
        {var go=GameObject.CreatePrimitive(type);go.transform.SetParent(parent,false);go.transform.localPosition=at;go.transform.localScale=size;var c=go.GetComponent<Collider>();c.enabled=false;Object.Destroy(c);go.GetComponent<Renderer>().sharedMaterial=timber;}
        public void Step(float time)
        {
            foreach(var w in workers)
            {
                float phase=Mathf.Repeat(time+w.phase,2.4f)/2.4f;float lift=phase<.72f?Mathf.SmoothStep(0,1,phase/.72f):1-Mathf.SmoothStep(0,1,(phase-.72f)/.28f);
                var head=w.stake.position+w.stake.up*12+Vector3.up*(lift*6);var contact=head-Vector3.up*4;w.hammer.SetPositionAndRotation(contact,Quaternion.identity);
                for(int a=0;a<4;a++)w.arms[a].localRotation=w.rest[a];
                if(grip!=null)
                {
                    grip.Align(w.arms[0],w.arms[1],w.right,contact,w.arms[0].position+w.actor.right*4,false,true,Vector3.up);
                    grip.Align(w.arms[2],w.arms[3],w.left,contact-Vector3.up*3.6f,w.arms[2].position-w.actor.right*4,true,true,Vector3.up);
                }
            }
        }
        public void Dispose(){foreach(var boat in boats)if(boat!=null){boat.SetActive(false);Object.Destroy(boat);}boats.Clear();workers.Clear();}
    }
}
