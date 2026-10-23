using UnityEngine;
namespace BeachBoatStory
{
    public sealed class EnemyShip : MonoBehaviour
    {
        public float moveSpeed=18,stakeHitDistance=12;
        [Min(1)] public float hullHalfLength=42f;
        public Transform targetPoint;
        public Transform[] routeWaypoints=new Transform[0];
        public Vector3 spawnPoint;
        [HideInInspector] public float ambushDelay;
        public bool isDestroyed;
        public bool SignalReady {get;private set;}
        public bool IsReleasedForAmbush=>attackReleased;
        public Transform WreckTarget=>wreckRaft!=null&&wreckRaft.gameObject.activeSelf?wreckRaft:null;
        public bool IsHullInsideStakeField=>RiverStakeZone.Instance!=null&&RiverStakeZone.Instance.TryHullContact(
            transform.position,transform.position,transform.forward,hullHalfLength,stakeHitDistance,false,out _);
        bool battleStarted;
        bool attackReleased;
        int routeIndex;
        float destroyedTime;
        float sinkDepth;
        public bool HasSettled {get;private set;}
        Transform wreckRaft;
        EnemyDeckGuard[] deckGuards;
        Vector3 sinkStart;Quaternion sinkRotation;
        GameObject[] debris;
        Rigidbody[] debrisBodies;
        MeshCollider[] wreckColliders;
        public bool HasSolidWreck=>isDestroyed&&wreckColliders!=null&&System.Array.Exists(wreckColliders,c=>c!=null&&c.enabled);
        void ConfigureWreckCollision(bool wreck)
        {
            if(wreckColliders==null)
            {
                var colliders=new System.Collections.Generic.List<MeshCollider>();var visual=transform.Find("RedSailJunkVisual");
                if(visual!=null)foreach(var filter in visual.GetComponentsInChildren<MeshFilter>(true))
                {
                    // Actual timber surfaces, never the large navigation box or fabric sails.
                    string n=filter.name;
                    if(!(n.Contains("Hull")||n.Contains("Deck")||n=="Roof"||n=="Upturned_Bow"||n=="Mast"||n=="Carved_Pillar"))continue;
                    if(filter.sharedMesh==null)continue;
                    var collider=filter.GetComponent<MeshCollider>();
                    if(collider==null)collider=filter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh=filter.sharedMesh;collider.convex=false;collider.isTrigger=false;colliders.Add(collider);
                }
                wreckColliders=colliders.ToArray();
            }
            foreach(var col in GetComponentsInChildren<Collider>(true))
                if(col.GetComponentInParent<EnemyDeckGuard>()==null)col.enabled=System.Array.IndexOf(wreckColliders,col)>=0?wreck:!wreck;
        }
        public void Activate()
        {
            isDestroyed=false;HasSettled=false;destroyedTime=0;battleStarted=true;
            attackReleased=false;SignalReady=false;routeIndex=0;
            var first=routeWaypoints!=null&&routeWaypoints.Length>0?routeWaypoints[0]:targetPoint;
            var direction=first!=null?first.position-transform.position:Vector3.forward;direction.y=0;
            transform.rotation=direction.sqrMagnitude>.001f?Quaternion.LookRotation(direction):Quaternion.identity;
            gameObject.SetActive(true);
            // The imported junk was originally seated against the low-tide surface.
            // Re-seat its visible hull at the actual water level for every battle wave.
            var visual = transform.Find("RedSailJunkVisual");
            if (visual != null)
            {
                var renderers = visual.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length > 0)
                {
                    var bounds = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                    visual.position += Vector3.up * (TideSystem.CurrentWaterHeight - 4f - bounds.min.y);
                }
            }
            EnsureDebrisPool();
            ConfigureWreckCollision(false);
        }
        void Update()=>Step(Time.deltaTime);
        public void Step(float dt)
        {
            if(dt<=0||!battleStarted)return;
            if(isDestroyed)
            {
                destroyedTime+=dt;float t=Mathf.Clamp01(destroyedTime/7f);
                float fall=Mathf.SmoothStep(0,1,t);
                transform.SetPositionAndRotation(sinkStart-transform.forward*(8f*Mathf.Sin(t*Mathf.PI))-Vector3.up*sinkDepth*fall,
                    sinkRotation*Quaternion.Euler(28*fall,0,42*fall));
                if(t>=1)
                {
                    HideDebris();
                    battleStarted=false;
                    HasSettled=true;
                    // Victory and the rising tide wait until the final sinking is visible.
                    BachDangStoryManager.Instance?.OnEnemyShipDestroyed();
                }
                return;
            }
            var pos=transform.position;pos.y=TideSystem.CurrentWaterHeight;transform.position=pos;
            if(targetPoint==null)return;
            Transform destination=targetPoint;
            if(routeWaypoints!=null)
                while(routeIndex<routeWaypoints.Length)
                {
                    var waypoint=routeWaypoints[routeIndex];
                    if(waypoint==null){routeIndex++;continue;}
                    if(QuestRowboat.FlatDistance(pos,waypoint.position)<=9){routeIndex++;continue;}
                    destination=waypoint;break;
                }
            bool finalLeg=destination==targetPoint;
            var end=destination.position;end.y=pos.y;
            float distance=Vector3.Distance(pos,end);
            // Orders control the allied volley, never the physics of an enemy hull.
            // Keep testing a stationary hull too: falling water can expose a stake underneath it.
            float holdDistance=hullHalfLength+stakeHitDistance*.5f;
            if(finalLeg&&!attackReleased&&distance<=holdDistance+.5f)
                SignalReady=true;
            float step=Mathf.Max(0,moveSpeed)*dt;
            var next=Vector3.MoveTowards(pos,end,step);
            if(finalLeg&&!attackReleased&&Vector3.Distance(next,end)<=holdDistance+.5f)SignalReady=true;
            var direction=end-pos;
            if(direction.sqrMagnitude>.001f)transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(direction),1-Mathf.Exp(-2*dt));
            transform.position=next;
            CheckStakeHit(pos,next);
        }
        public void ReleaseForAmbush()
        {
            if(!battleStarted&&!isDestroyed)return;
            attackReleased=true;SignalReady=false;
        }
        void CheckStakeHit(Vector3 from,Vector3 to)
        {
            var zone=RiverStakeZone.Instance;if(zone==null)return;
            if(zone.TryHullContact(from,to,transform.forward,hullHalfLength,stakeHitDistance,true,out var point))TakeDamage(point);
        }
        public void TakeDamage()
        { TakeDamage(transform.position+transform.forward*35); }
        public void TakeDamage(Vector3 impactPoint)
        {
            if(!battleStarted||isDestroyed)return;
            isDestroyed=true;destroyedTime=0;sinkStart=transform.position;sinkRotation=transform.rotation;
            SignalReady=true;
            // Settle the capsized hull on the riverbed and retain its actual mesh.
            // Tall masts may remain above shallow water, as a visible wreck landmark.
            float ground=TideSystem.CurrentWaterHeight-85;
            var terrain=Terrain.activeTerrain;if(terrain!=null)ground=terrain.SampleHeight(sinkStart)+terrain.transform.position.y;
            var finalRotation=sinkRotation*Quaternion.Euler(28,0,42);
            float hullBottom=float.PositiveInfinity;
            foreach(float x in new[]{-40f,40f})foreach(float z in new[]{-120f,120f})
                hullBottom=Mathf.Min(hullBottom,(finalRotation*new Vector3(x,-4,z)).y);
            sinkDepth=Mathf.Max(18,sinkStart.y-(ground-hullBottom+1));
            ConfigureWreckCollision(true);
            FloatSurvivors();
            LaunchDebris(impactPoint);
            BeachBoatSoundscape.Instance?.PlayCrash();
            FindAnyObjectByType<RiverBattleChoreography>()?.PlayShipImpact();
        }

        void FloatSurvivors()
        {
            if(deckGuards==null)deckGuards=GetComponentsInChildren<EnemyDeckGuard>(true);
            if(deckGuards.Length==0)return;
            if(wreckRaft==null)
            {
                wreckRaft=new GameObject(name+"_ViTri_XacTau").transform;
                wreckRaft.SetParent(transform.parent,true);
            }
            wreckRaft.gameObject.SetActive(true);
            wreckRaft.SetPositionAndRotation(new Vector3(sinkStart.x,TideSystem.CurrentWaterHeight+1,sinkStart.z),sinkRotation);
            // This empty anchor is only for command range; nobody stands on it.
            RiverBoardingCombat.Instance?.ReleaseShipwreckSurvivors(this,deckGuards);
        }
        public void ClearWreck()
        {
            if(deckGuards!=null)foreach(var guard in deckGuards)if(guard!=null){guard.IsIndependentBoarder=false;guard.RestoreDeck();guard.ResetGuard();}
            ConfigureWreckCollision(false);
            if(wreckRaft!=null)wreckRaft.gameObject.SetActive(false);
        }
        public BachDangSaveData.Ship CaptureSave()=>new BachDangSaveData.Ship {
            name=name,active=gameObject.activeSelf,started=battleStarted,destroyed=isDestroyed,settled=HasSettled,released=attackReleased,
            signal=SignalReady,route=routeIndex,destroyedTime=destroyedTime,sinkDepth=sinkDepth,
            position=transform.position,rotation=transform.rotation,sinkStart=sinkStart,sinkRotation=sinkRotation};
        public void RestoreSave(BachDangSaveData.Ship data)
        {
            // Activate seats the visual against the water. Calibrate at a floating root,
            // then apply the saved sinking pose so a wreck never gets a second vertical offset.
            var floating=data.position;floating.y=TideSystem.CurrentWaterHeight;
            transform.SetPositionAndRotation(floating,Quaternion.Euler(0,data.rotation.eulerAngles.y,0));
            if(data.active)Activate();
            if(deckGuards==null)deckGuards=GetComponentsInChildren<EnemyDeckGuard>(true);
            battleStarted=data.started;isDestroyed=data.destroyed;HasSettled=data.settled;attackReleased=data.released;
            SignalReady=data.signal;routeIndex=data.route;destroyedTime=data.destroyedTime;sinkDepth=data.sinkDepth;
            sinkStart=data.sinkStart;sinkRotation=data.sinkRotation;transform.SetPositionAndRotation(data.position,data.rotation);
            if(isDestroyed)
            {
                if(wreckRaft==null){wreckRaft=new GameObject(name+"_ViTri_XacTau").transform;wreckRaft.SetParent(transform.parent,true);}
                wreckRaft.gameObject.SetActive(true);wreckRaft.SetPositionAndRotation(new Vector3(sinkStart.x,TideSystem.CurrentWaterHeight+1,sinkStart.z),sinkRotation);
                ConfigureWreckCollision(true);
            }
            gameObject.SetActive(data.active);
        }

        void EnsureDebrisPool()
        {
            if(debris!=null)return;
            const int count=12;debris=new GameObject[count];debrisBodies=new Rigidbody[count];
            var parent=transform.parent;Material material=null;
            foreach(var candidate in GetComponentsInChildren<Renderer>(true))
            {
                if(candidate.sharedMaterial==null)continue;
                material=candidate.sharedMaterial;
                string n=material.name.ToLowerInvariant();
                if(n.Contains("hajotest")||n.Contains("fedelzet"))break; // imported hull/deck wood
            }
            for(int i=0;i<count;i++)
            {
                var piece=GameObject.CreatePrimitive(PrimitiveType.Cube);piece.name=name+"_ManhGo_"+(i+1);piece.transform.SetParent(parent,true);
                piece.transform.localScale=new Vector3(Random.Range(.45f,1.1f),Random.Range(.25f,.55f),Random.Range(2.5f,6.5f));
                var renderer=piece.GetComponent<Renderer>();if(material!=null)renderer.sharedMaterial=material;
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
                var body=piece.AddComponent<Rigidbody>();body.mass=.35f;body.linearDamping=.12f;body.angularDamping=.08f;
                piece.SetActive(false);debris[i]=piece;debrisBodies[i]=body;
            }
        }
        void LaunchDebris(Vector3 impactPoint)
        {
            EnsureDebrisPool();
            for(int i=0;i<debris.Length;i++)
            {
                var piece=debris[i];var body=debrisBodies[i];piece.SetActive(true);
                piece.transform.position=impactPoint+transform.right*Random.Range(-12f,12f)+Vector3.up*Random.Range(1f,8f);
                piece.transform.rotation=Random.rotation;
                body.linearVelocity=transform.forward*Random.Range(8f,20f)+transform.right*Random.Range(-18f,18f)+Vector3.up*Random.Range(12f,28f);
                body.angularVelocity=Random.insideUnitSphere*Random.Range(4f,11f);
            }
        }
        void HideDebris(){if(debris==null)return;foreach(var piece in debris)if(piece!=null)piece.SetActive(false);}
        public void ResetForCheckpoint()
        {
            ClearWreck();HideDebris();battleStarted=false;HasSettled=false;isDestroyed=false;attackReleased=false;SignalReady=false;routeIndex=0;destroyedTime=0;
            gameObject.SetActive(false);
        }
        void OnDestroy(){if(wreckRaft!=null)Destroy(wreckRaft.gameObject);if(debris==null)return;foreach(var piece in debris)if(piece!=null)Destroy(piece);}
    }
}
