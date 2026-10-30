using System.Collections.Generic;
using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Persistent paired construction teams; their planted poles stay fixed in the river.</summary>
    public sealed class StakeWorkCrew
    {
        sealed class Worker
        {
            public Transform boat,actor,stake,hammer,chest;public Animator rig;public Transform[] arms;public Quaternion[] rest;public Transform right,left;public float phase;public bool holder;public Quaternion chestRest;
        }
        readonly Vector3 origin;readonly Quaternion orientation;
        readonly List<Transform> poles=new List<Transform>();
        public bool Parked {get;private set;}
        readonly List<GameObject> boats=new List<GameObject>();readonly List<Worker> workers=new List<Worker>();
        readonly BowGripProfile grip=BowGripProfile.Load();readonly Material timber;
        public int Count=>workers.Count;
        public int Teams=>workers.Count/2;
        DialogueVoice callVoice;float began,callUntil;int calls;float nextImpact;int lastCycle=-1;
        ParticleSystem spray;Material water;
        public string CurrentCall {get;private set;}="";
        public StakeWorkCrew(QuestRowboat source,Transform template,Transform parent,Material material,BachDangSaveData.ConstructionSite saved)
        {
            origin=saved.origin;orientation=saved.rotation;Parked=saved.parked;
            timber=material;var sourceHull=BoatHullCollision.Get(source.transform);var box=sourceHull.SolidHull;var filters=source.GetComponentsInChildren<MeshFilter>(true);
            began=0;calls=saved.elapsed>=13?2:saved.elapsed>=1?1:0;callVoice=parent.gameObject.AddComponent<DialogueVoice>();
            var fx=new GameObject("Cong_binh_nuoc_ban");fx.transform.SetParent(parent,false);spray=fx.AddComponent<ParticleSystem>();spray.Stop();var main=spray.main;main.loop=true;main.playOnAwake=false;main.simulationSpace=ParticleSystemSimulationSpace.World;main.startLifetime=.65f;main.startSize=.35f;main.gravityModifier=1;main.maxParticles=70;main.startColor=new Color(.7f,.87f,.86f,.8f);var emission=spray.emission;emission.enabled=false;water=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));spray.GetComponent<ParticleSystemRenderer>().sharedMaterial=water;spray.Play();
            for(int i=0;i<(saved.boats!=null?saved.boats.Length:6);i++)
            {
                Vector3 at=origin+orientation*Vector3.right*(i%2==0?-120:120)+orientation*Vector3.forward*(45+i/2*95);
                bool clear=saved.boats!=null;
                if(clear)at=saved.boats[i];
                for(int n=0;n<10&&!clear;n++)
                {
                    var candidate=at+orientation*Vector3.forward*n*30;bool dry=false;
                    foreach(var terrain in Terrain.activeTerrains){var local=candidate-terrain.transform.position;var size=terrain.terrainData.size;if(local.x>=0&&local.z>=0&&local.x<=size.x&&local.z<=size.z&&terrain.SampleHeight(candidate)+terrain.transform.position.y>TideSystem.CurrentWaterHeight-8)dry=true;}
                    if(!dry&&RiverHullCollision.ClearAt(candidate+Vector3.up*8,42,null,sourceHull)){at=candidate;clear=true;break;}
                }
                if(!clear)continue;
                var boat=new GameObject("Thuyen_cong_binh_"+i);boat.transform.SetParent(parent,true);at.y=TideSystem.CurrentWaterHeight;boat.transform.SetPositionAndRotation(at,saved.boatRotations!=null&&i<saved.boatRotations.Length?saved.boatRotations[i]:orientation);boat.transform.localScale=source.transform.lossyScale;boats.Add(boat);
                foreach(var f in filters)
                {
                    if(f.sharedMesh==null||f.GetComponentInParent<Animator>()!=null)continue;var r=f.GetComponent<MeshRenderer>();if(r==null||!r.enabled||f.transform.IsChildOf(source.paddle))continue;
                    var go=new GameObject("Construction_"+f.name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(boat.transform,false);go.transform.localPosition=source.transform.InverseTransformPoint(f.transform.position);go.transform.localRotation=Quaternion.Inverse(source.transform.rotation)*f.transform.rotation;
                    var s=source.transform.lossyScale;var v=f.transform.lossyScale;go.transform.localScale=new Vector3(v.x/s.x,v.y/s.y,v.z/s.z);go.GetComponent<MeshFilter>().sharedMesh=f.sharedMesh;go.GetComponent<Renderer>().sharedMaterials=r.sharedMaterials;
                }
                var solid=boat.AddComponent<BoxCollider>();solid.center=box.center;solid.size=box.size;var body=boat.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;
                Transform teamStake=null;
                for(int member=0;member<2;member++)
                {
                    var local=source.transform.InverseTransformPoint(source.seat.position);local.x=box.center.x+box.size.x*.5f-2.8f/Mathf.Abs(source.transform.lossyScale.x);local.z+=(member==0?-3.8f:3.8f)/Mathf.Abs(source.transform.lossyScale.z);
                    var reference=source.transform.TransformPoint(local);Vector3 floor=default;bool supported=false;
                    for(int inset=0;inset<=8;inset++)if(sourceHull.TryDeckSurface(reference-source.transform.right*inset,out floor)){supported=true;break;}
                    if(!supported)continue;
                    // The narrow gunwale is extended by a solid work plank. Find
                    // its deck height inboard, then support the worker at the edge.
                    floor.x=reference.x;floor.z=reference.z;
                    var actor=new GameObject("Cong_binh_"+i+"_"+member).transform;actor.SetParent(boat.transform,true);actor.position=boat.transform.TransformPoint(source.transform.InverseTransformPoint(floor));actor.rotation=boat.transform.rotation*Quaternion.Euler(0,90,0);
                    float platformHeight=member==1?6:.6f;var platform=GameObject.CreatePrimitive(PrimitiveType.Cube);platform.name=member==1?"San_go_nguoi_nen_coc":"Van_go_nguoi_giu_coc";platform.transform.SetParent(boat.transform,true);platform.transform.SetPositionAndRotation(actor.position+Vector3.up*(platformHeight*.5f),boat.transform.rotation);platform.transform.localScale=new Vector3(8,platformHeight,7);platform.GetComponent<Renderer>().sharedMaterial=timber;actor.position+=Vector3.up*platformHeight;
                    var model=Object.Instantiate(RiverBoardingCombat.Instance.allyPrefab,actor,false);var rig=model.GetComponentInChildren<Animator>();rig.runtimeAnimatorController=RiverBoardingCombat.Instance.FootController;rig.enabled=true;rig.Update(0);
                    rig.transform.localScale*=EnemyDeckGuard.RigHeight(BeachBoatQuestSession.Instance.player.GetComponentInChildren<Animator>())/Mathf.Max(.01f,EnemyDeckGuard.RigHeight(rig));rig.Update(0);
                    ActorSolidBody.WalkingDimensions(rig,out _,out float sole,out _);actor.position-=Vector3.up*sole;rig.enabled=false;
                    foreach(var c in model.GetComponentsInChildren<Collider>(true)){c.enabled=false;Object.Destroy(c);}
                    ActorSolidBody.Ensure(actor.gameObject,rig);
                    if(teamStake==null){teamStake=StakeWorkVisual.Create("Coc_nhon_cong_binh",parent,template);var middle=source.transform.InverseTransformPoint(source.seat.position);var pole=boat.transform.TransformPoint(new Vector3(box.center.x+box.size.x*.5f+6/Mathf.Abs(source.transform.lossyScale.x),0,middle.z));pole.y=TideSystem.CurrentWaterHeight+12;teamStake.position=pole;teamStake.rotation=boat.transform.rotation*Quaternion.Euler(0,0,-12);
                        if(saved.posts!=null&&i<saved.posts.Length)teamStake.SetPositionAndRotation(saved.posts[i],saved.postRotations[i]);
                        poles.Add(teamStake);var poleCollider=teamStake.gameObject.AddComponent<CapsuleCollider>();poleCollider.height=24;poleCollider.radius=.8f;}
                    var stake=teamStake;var facing=stake.position-actor.position;facing.y=0;actor.rotation=Quaternion.LookRotation(facing);
                    Transform hammer=null;if(member==1){hammer=new GameObject("Bua_cong_binh").transform;hammer.SetParent(boat.transform,true);Part(hammer,PrimitiveType.Cylinder,Vector3.zero,new Vector3(.6f,5.5f,.6f));Part(hammer,PrimitiveType.Cube,Vector3.up*4,new Vector3(4.5f,2.5f,2.5f));}
                    var arms=new[]{rig.GetBoneTransform(HumanBodyBones.RightUpperArm),rig.GetBoneTransform(HumanBodyBones.RightLowerArm),rig.GetBoneTransform(HumanBodyBones.LeftUpperArm),rig.GetBoneTransform(HumanBodyBones.LeftLowerArm)};
                    var rest=new Quaternion[4];for(int a=0;a<4;a++)rest[a]=arms[a].localRotation;
                    var chest=rig.GetBoneTransform(HumanBodyBones.Chest)??rig.GetBoneTransform(HumanBodyBones.Spine);
                    workers.Add(new Worker{boat=boat.transform,actor=actor,stake=stake,hammer=hammer,rig=rig,arms=arms,rest=rest,right=rig.GetBoneTransform(HumanBodyBones.RightHand),left=rig.GetBoneTransform(HumanBodyBones.LeftHand),phase=i*.7f,holder=member==0,chest=chest,chestRest=chest.localRotation});
                }
            }
            Physics.SyncTransforms();
        }
        void Part(Transform parent,PrimitiveType type,Vector3 at,Vector3 size)
        {var go=GameObject.CreatePrimitive(type);go.transform.SetParent(parent,false);go.transform.localPosition=at;go.transform.localScale=size;var c=go.GetComponent<Collider>();c.enabled=false;Object.Destroy(c);go.GetComponent<Renderer>().sharedMaterial=timber;}
        public BachDangSaveData.ConstructionSite Capture(int index,float elapsed)=>new BachDangSaveData.ConstructionSite{index=index,elapsed=elapsed,origin=origin,rotation=orientation,parked=Parked,boats=boats.ConvertAll(b=>b.transform.position).ToArray(),boatRotations=boats.ConvertAll(b=>b.transform.rotation).ToArray(),posts=poles.ConvertAll(p=>p.position).ToArray(),postRotations=poles.ConvertAll(p=>p.rotation).ToArray()};
        public void Step(float time,bool keepWorking=false)
        {
            foreach(var boat in boats){var p=boat.transform.position;p.y=TideSystem.CurrentWaterHeight;boat.transform.position=p;}
            if(Parked||time>=18&&!keepWorking)
            {
                CurrentCall="";foreach(var w in workers){w.chest.localRotation=w.chestRest;for(int a=0;a<4;a++)w.arms[a].localRotation=w.rest[a];if(w.hammer!=null)w.hammer.gameObject.SetActive(false);}return;
            }
            if(calls<2&&time-began>(calls==0?1:13)){CurrentCall=calls==0?"Giữ chắc cọc! Một, hai, nện!":"Cọc đã chắc! Chuyển sang hàng tiếp theo!";callVoice.Play("Công binh",CurrentCall,"stake_crew",false,"crew_work");callVoice.SpeechSource.volume=.42f;callVoice.SpeechSource.spatialBlend=1;callVoice.SpeechSource.minDistance=45;callVoice.SpeechSource.maxDistance=350;callUntil=time+4;calls++;}
            if(time>callUntil)CurrentCall="";
            foreach(var w in workers)
            {
                float phase=Mathf.Repeat(time+w.phase,2.4f)/2.4f;float lift=phase<.72f?Mathf.SmoothStep(0,1,phase/.72f):1-Mathf.SmoothStep(0,1,(phase-.72f)/.28f);
                w.chest.localRotation=w.chestRest;w.chest.rotation=Quaternion.AngleAxis(w.holder?7:lift*5,w.actor.right)*w.chest.rotation;
                var head=w.stake.position+w.stake.up*12+Vector3.up*(lift*5);var contact=w.holder?w.stake.position+w.stake.up*4:head-Vector3.up*4;
                for(int a=0;a<4;a++)w.arms[a].localRotation=w.rest[a];
                if(grip!=null)
                {
                    grip.Align(w.arms[0],w.arms[1],w.right,contact,w.arms[0].position+w.actor.right*4,false,true,Vector3.up);
                    if(!w.holder)contact=w.right.TransformPoint(grip.rightRodCenter);
                    grip.Align(w.arms[2],w.arms[3],w.left,contact-Vector3.up*(w.holder?2:3.6f),w.arms[2].position-w.actor.right*4,true,true,Vector3.up);
                }
                if(!w.holder){w.hammer.gameObject.SetActive(true);w.hammer.SetPositionAndRotation(contact,Quaternion.identity);}
                int cycle=Mathf.FloorToInt((time+w.phase)/2.4f);if(!w.holder&&phase>.94f&&cycle!=lastCycle&&time>=nextImpact){lastCycle=cycle;nextImpact=time+1.1f;if(!(BeachBoatSoundscape.Instance?.Muted??false))BeachBoatSoundscape.Instance.PlayCrewStake(w.stake.position);for(int n=0;n<9;n++)spray.Emit(new ParticleSystem.EmitParams{position=new Vector3(w.stake.position.x,TideSystem.CurrentWaterHeight,w.stake.position.z),velocity=new Vector3(Random.Range(-4f,4f),Random.Range(4f,8f),Random.Range(-4f,4f))},1);}
            }
        }
        public void Dispose(){callVoice?.Stop();if(water!=null)Object.Destroy(water);foreach(var boat in boats)if(boat!=null){boat.SetActive(false);Object.Destroy(boat);}boats.Clear();workers.Clear();}
    }
}
