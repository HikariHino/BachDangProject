using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Carry bundles across the boat and drive anchor stakes in the actual third-person scene.</summary>
    [DefaultExecutionOrder(800)]
    public sealed class StakeConstruction : MonoBehaviour
    {
        public enum WorkStep { TakeBundle, CarryBundle, Hammer, Finished }
        public static StakeConstruction Active {get;private set;}
        public WorkStep Step {get;private set;}
        public float Progress=>Mathf.Clamp01((bundles*3+hits)/9f);
        public float CarryProgress {get;private set;}
        public int CompletedBundles=>bundles;
        public Vector2 PlacementOffset {get;private set;}
        public float HammerLift {get;private set;}
        public bool PlacementValid {get;private set;}
        public bool HasAimedStake {get;private set;}
        public Vector3 PlacementPoint {get;private set;}
        bool hammerHeld,pendingStrike;float releaseLift,visibleDepth;
        int bundles,hits;float nextHit,swing,sole,walkingUntil;
        RiverStakeZone zone;QuestRowboat boat;Transform player,bundle,stake,hammer,stack;
        Animator rig;Transform arm,forearm;Quaternion armRest,forearmRest;bool rigEnabled;
        Transform[] arms;Quaternion[] armPose;Transform rightHand,leftHand,chest;float cameraElevation;
        Material previewGood,previewBad;
        ParticleSystem splash;Material waterMaterial,bindingMaterial;float lean=12;
        Transform[] legs;Quaternion[] legRest;
        Camera_Script camera;Vector3 cameraSettings,localStart;GameObject props;
        Material timber;
        public static bool AllowsCamera=>Active!=null&&Time.timeScale>0&&!BachDangFrontEnd.IsBlocking&&
            !(StoryDialogueUI.Instance?.IsOpen??false)&&!OpeningEvent.IsOpeningActive;
        public void Begin(RiverStakeZone owner)
        {
            zone=owner;boat=QuestRowboat.Instance;player=BeachBoatQuestSession.Instance.player.transform;
            rig=player.GetComponentInChildren<Animator>();rigEnabled=rig.enabled;
            rig.enabled=true;rig.runtimeAnimatorController=RiverBoardingCombat.Instance.FootController;rig.Rebind();rig.Update(0);
            ActorSolidBody.WalkingDimensions(rig,out _,out sole,out _);rig.enabled=false;
            arm=rig.GetBoneTransform(HumanBodyBones.RightUpperArm);forearm=rig.GetBoneTransform(HumanBodyBones.RightLowerArm);
            armRest=arm.localRotation;forearmRest=forearm.localRotation;
            arms=new[]{arm,forearm,rig.GetBoneTransform(HumanBodyBones.LeftUpperArm),rig.GetBoneTransform(HumanBodyBones.LeftLowerArm)};
            armPose=new Quaternion[4];for(int i=0;i<4;i++)armPose[i]=arms[i].localRotation;
            rightHand=rig.GetBoneTransform(HumanBodyBones.RightHand);leftHand=rig.GetBoneTransform(HumanBodyBones.LeftHand);chest=rig.GetBoneTransform(HumanBodyBones.Chest)??rig.GetBoneTransform(HumanBodyBones.Spine);
            legs=new[]{rig.GetBoneTransform(HumanBodyBones.LeftUpperLeg),rig.GetBoneTransform(HumanBodyBones.RightUpperLeg),rig.GetBoneTransform(HumanBodyBones.LeftLowerLeg),rig.GetBoneTransform(HumanBodyBones.RightLowerLeg)};
            legRest=new Quaternion[4];for(int i=0;i<4;i++)legRest[i]=legs[i].localRotation;
            camera=BeachBoatQuestSession.Instance.storyCamera.GetComponent<Camera_Script>();cameraSettings=new Vector3(camera.distance,camera.heightOffset,camera.rightOffset);
            cameraElevation=camera.Elevation;camera.distance=32;camera.heightOffset=14;camera.rightOffset=9;
            localStart=boat.transform.InverseTransformPoint(boat.seat.position);localStart.y=0;
            bundles=hits=0;CarryProgress=0;Step=WorkStep.TakeBundle;Active=this;
            props=new GameObject("Stake_Work_3D_Props");timber=zone.fieldStakes[0].GetComponentInChildren<Renderer>(true).sharedMaterial;
            stack=new GameObject("Bo_coc_tren_thuyen").transform;stack.SetParent(props.transform,false);
            for(int i=0;i<6;i++)
            {var log=Log("Coc_tren_bo",stack);log.localPosition=new Vector3((i%3-1)*2.2f,1+i/3*1.8f,0);log.localRotation=Quaternion.Euler(90,0,0);}
            bundle=new GameObject("Bo_coc_dang_vac").transform;bundle.SetParent(props.transform,false);
            for(int i=0;i<3;i++){var log=Log("Coc_go",bundle);log.localPosition=i==2?new Vector3(0,0,1.1f):new Vector3(i==0?-.9f:.9f,0,-.5f);}
            bindingMaterial=new Material(timber){color=new Color(.45f,.32f,.17f)};
            foreach(int at in new[]{-7,7}){var tie=new GameObject("Day_buoc_bo_coc").AddComponent<LineRenderer>();tie.transform.SetParent(bundle,false);tie.useWorldSpace=false;tie.sharedMaterial=bindingMaterial;tie.widthMultiplier=.25f;tie.positionCount=33;for(int i=0;i<33;i++){float a=i/32f*Mathf.PI*2;tie.SetPosition(i,new Vector3(Mathf.Cos(a)*1.9f,at,Mathf.Sin(a)*1.9f));}}
            stake=Log("Coc_dang_dong",props.transform);hammer=new GameObject("Bua_go").transform;hammer.SetParent(props.transform,false);
            var handle=GameObject.CreatePrimitive(PrimitiveType.Cylinder);handle.transform.SetParent(hammer,false);handle.transform.localScale=new Vector3(.6f,4,.6f);Decorative(handle);
            var head=GameObject.CreatePrimitive(PrimitiveType.Cube);head.transform.SetParent(hammer,false);head.transform.localPosition=Vector3.up*4;head.transform.localScale=new Vector3(4.5f,2.5f,2.5f);Decorative(head);
            bundle.gameObject.SetActive(false);stake.gameObject.SetActive(false);hammer.gameObject.SetActive(false);
            previewGood=new Material(timber){color=new Color(.2f,.9f,.4f)};previewBad=new Material(timber){color=new Color(.95f,.15f,.1f)};
            var spray=new GameObject("Nuoc_ban_khi_dong_coc");spray.transform.SetParent(props.transform);splash=spray.AddComponent<ParticleSystem>();splash.Stop();
            var main=splash.main;main.loop=false;main.playOnAwake=false;main.startLifetime=.55f;main.startSpeed=9;main.startSize=.45f;main.gravityModifier=1;main.maxParticles=40;main.startColor=new Color(.68f,.85f,.83f,.8f);main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=splash.emission;emission.enabled=false;var shape=splash.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=40;shape.radius=.5f;
            waterMaterial=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));waterMaterial.color=new Color(.6f,.8f,.82f,.7f);splash.GetComponent<ParticleSystemRenderer>().sharedMaterial=waterMaterial;
        }
        Transform Log(string name,Transform parent)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name=name;go.transform.SetParent(parent,false);go.transform.localScale=new Vector3(1.6f,12,1.6f);Decorative(go);return go.transform;
        }
        void Decorative(GameObject go){Destroy(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=timber;}
        public bool Interact()
        {
            if(Active!=this||BachDangFrontEnd.IsBlocking||Time.timeScale<=0)return false;
            if(Step==WorkStep.TakeBundle){Step=WorkStep.CarryBundle;CarryProgress=0;HasAimedStake=false;PlacementOffset=Vector2.zero;bundle.gameObject.SetActive(true);BeachBoatSoundscape.Instance?.PlayInteraction();return true;}
            if(Step==WorkStep.CarryBundle&&CarryProgress>=1&&PlacementValid&&HasAimedStake)
            {Step=WorkStep.Hammer;hits=0;HammerLift=visibleDepth=0;pendingStrike=false;bundle.gameObject.SetActive(false);stake.gameObject.SetActive(true);stake.GetComponent<Renderer>().sharedMaterial=timber;hammer.gameObject.SetActive(true);return true;}
            return false;
        }
        public void WalkBundle(float dt)
        {if(Active==this&&Step==WorkStep.CarryBundle&&Time.timeScale>0&&!BachDangFrontEnd.IsBlocking){CarryProgress=Mathf.Clamp01(CarryProgress+Mathf.Max(0,dt)/1.6f);walkingUntil=Time.time+.12f;}}
        public bool Strike()
        {
            if(Active!=this||Step!=WorkStep.Hammer||pendingStrike||HammerLift<.65f||Time.time<nextHit||Time.timeScale<=0||BachDangFrontEnd.IsBlocking)return false;
            nextHit=Time.time+.65f;releaseLift=HammerLift;swing=.25f;HammerLift=0;pendingStrike=true;return true;
        }
        void Impact()
        {
            pendingStrike=false;hits++;BeachBoatSoundscape.Instance?.PlayStake();
            splash.transform.SetPositionAndRotation(PlacementPoint,Quaternion.Euler(-90,0,0));splash.Emit(14);
            if(hits==3)
            {bundles++;hits=0;Step=bundles==3?WorkStep.Finished:WorkStep.TakeBundle;CarryProgress=0;stake.gameObject.SetActive(false);hammer.gameObject.SetActive(false);}
        }
        public bool AimStake(Vector2 motion)
        {
            if(Active!=this||Step!=WorkStep.CarryBundle||CarryProgress<1||Time.timeScale<=0||BachDangFrontEnd.IsBlocking||!float.IsFinite(motion.x)||!float.IsFinite(motion.y))return false;
            PlacementOffset=Vector2.ClampMagnitude(PlacementOffset+Vector2.ClampMagnitude(motion,.2f),1);if(motion.sqrMagnitude>.00001f)HasAimedStake=true;return true;
        }
        public bool LiftHammer(float motion)
        {
            if(Active!=this||Step!=WorkStep.Hammer||pendingStrike||Time.timeScale<=0||BachDangFrontEnd.IsBlocking||!float.IsFinite(motion))return false;
            HammerLift=Mathf.Clamp01(HammerLift+Mathf.Clamp(motion,-.15f,.15f));return true;
        }
        void Update()
        {
            if(Active!=this||Time.timeScale<=0||BachDangFrontEnd.IsBlocking)return;
            if(Input.GetKeyDown(KeyCode.X)){zone.RestoreCheckpoint(zone.CaptureCheckpoint());return;}
            if(Input.GetKeyDown(KeyCode.E))Interact();
            if(Input.GetKey(KeyCode.W))WalkBundle(Time.deltaTime);
            if(Step==WorkStep.CarryBundle&&CarryProgress>=1)AimStake(new Vector2(Input.GetAxisRaw("Mouse X"),Input.GetAxisRaw("Mouse Y"))*.035f);
            if(Step==WorkStep.CarryBundle&&CarryProgress>=1)lean=Mathf.Clamp(lean+Input.mouseScrollDelta.y*2,6,22);
            if(Input.GetMouseButtonDown(0))hammerHeld=true;
            if(hammerHeld&&Input.GetMouseButton(0))LiftHammer(Input.GetAxisRaw("Mouse Y")*.065f);
            if(Input.GetMouseButtonUp(0)){hammerHeld=false;Strike();}
            swing=Mathf.Max(0,swing-Time.deltaTime);
            if(pendingStrike&&swing<=0)Impact();
        }
        void LateUpdate()
        {
            if(Active!=this||props==null)return;
            float walk=Step==WorkStep.CarryBundle?CarryProgress:Step==WorkStep.Hammer?1:0;
            var hull=BoatHullCollision.Get(boat.transform);float halfWidth=Vector3.Scale(hull.SolidHull.size,hull.SolidHull.transform.lossyScale).x*.5f;
            var p=boat.transform.TransformPoint(localStart+Vector3.forward*(walk*10));p+=boat.transform.right*(walk*Mathf.Max(0,halfWidth-4));
            if(BoatHullCollision.Get(boat.transform).TryDeckSurface(p,out var floor))p.y=floor.y-sole+.2f;
            else p.y=boat.seat.position.y+4;
            if(CarryProgress>0)p=RiverHullCollision.Move(player.position+Vector3.up*8,p+Vector3.up*8,3.5f,false,player,BoatHullCollision.Get(boat.transform))-Vector3.up*8;
            player.SetPositionAndRotation(p,boat.transform.rotation);var body=player.GetComponent<Rigidbody>();body.position=p;body.rotation=player.rotation;
            var stock=boat.transform.position-boat.transform.right*6-boat.transform.forward*3;
            if(BoatHullCollision.Get(boat.transform).TryDeckSurface(stock,out var stockFloor))stock.y=stockFloor.y+1;
            else stock.y=boat.seat.position.y+1;
            stack.position=stock;stack.rotation=boat.transform.rotation;
            var carry=chest.position+player.forward*4+Vector3.down;
            bundle.SetPositionAndRotation(carry,player.rotation*Quaternion.Euler(0,0,90));
            var work=p+boat.transform.forward*(4+PlacementOffset.y*4)+boat.transform.right*(8+PlacementOffset.x*3);work.y=TideSystem.CurrentWaterHeight;
            if(Step==WorkStep.Hammer)
            {
                var facing=work-p;facing.y=0;player.rotation=Quaternion.LookRotation(facing);body.rotation=player.rotation;
            }
            PlacementPoint=work;PlacementValid=true;
            foreach(var terrain in Terrain.activeTerrains){var local=work-terrain.transform.position;var size=terrain.terrainData.size;if(local.x>=0&&local.z>=0&&local.x<=size.x&&local.z<=size.z&&terrain.SampleHeight(work)+terrain.transform.position.y>TideSystem.CurrentWaterHeight-.7f)PlacementValid=false;}
            foreach(var other in BoatHullCollision.Active)if(other!=null&&other.SolidHull!=null&&Vector3.Distance(other.SolidHull.ClosestPoint(work),work)<1.6f)PlacementValid=false;
            foreach(var hit in Physics.OverlapCapsule(work-Vector3.up*5,work+Vector3.up*8,1.1f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                if(!(hit is TerrainCollider)&&!hit.transform.IsChildOf(player)&&!hit.transform.IsChildOf(props.transform))PlacementValid=false;
            stake.gameObject.SetActive(Step==WorkStep.Hammer||Step==WorkStep.CarryBundle&&CarryProgress>=1);
            visibleDepth=Mathf.MoveTowards(visibleDepth,hits*3,Time.deltaTime*16);
            stake.position=work+Vector3.up*(15-visibleDepth);stake.rotation=boat.transform.rotation*Quaternion.Euler(0,0,-lean);
            if(Step==WorkStep.CarryBundle)stake.GetComponent<Renderer>().sharedMaterial=PlacementValid?previewGood:previewBad;
            for(int i=0;i<4;i++)arms[i].localRotation=armPose[i];
            if(Step==WorkStep.CarryBundle)
            {
                QuestRowboat.SolveArm(arms[0],arms[1],rightHand,carry+player.right*3,arms[0].position+player.right*4-Vector3.up*4);
                QuestRowboat.SolveArm(arms[2],arms[3],leftHand,carry-player.right*3,arms[2].position-player.right*4-Vector3.up*4);
            }
            else if(Step==WorkStep.Hammer)
            {
                float lift=pendingStrike?releaseLift*Mathf.SmoothStep(0,1,swing/.25f):HammerLift;
                var headGoal=stake.position+stake.up*12+Vector3.up*(lift*6);var grip=headGoal-Vector3.up*4;
                var profile=RiverPlayerCombat.Instance?.GripProfile;var contact=grip;
                if(profile!=null){profile.Align(arms[0],arms[1],rightHand,grip,arms[0].position+player.right*4-Vector3.up*2,false,true);contact=rightHand.TransformPoint(profile.rightRodCenter);}
                else {QuestRowboat.SolveArm(arms[0],arms[1],rightHand,grip,arms[0].position+player.right*4-Vector3.up*2);contact=rightHand.position;}
                var direction=(headGoal-contact).normalized;
                if(profile!=null)
                {
                    profile.Align(arms[0],arms[1],rightHand,grip,arms[0].position+player.right*4-Vector3.up*2,false,true,direction);contact=rightHand.TransformPoint(profile.rightRodCenter);direction=(headGoal-contact).normalized;
                    profile.Align(arms[2],arms[3],leftHand,contact-direction*1.8f,arms[2].position-player.right*4-Vector3.up*2,true,true,direction);
                }
                else QuestRowboat.SolveArm(arms[2],arms[3],leftHand,contact-direction*1.8f,arms[2].position-player.right*4-Vector3.up*2);
                hammer.SetPositionAndRotation(contact,Quaternion.FromToRotation(Vector3.up,direction));hammer.localScale=new Vector3(1,Mathf.Clamp(Vector3.Distance(headGoal,contact)/4,.7f,1.6f),1);
            }
            float stride=Step==WorkStep.CarryBundle&&CarryProgress<1&&Time.time<walkingUntil?Mathf.Sin(CarryProgress*Mathf.PI*6):0;
            for(int i=0;i<4;i++)legs[i].localRotation=legRest[i]*Quaternion.Euler(i<2?stride*(i==0?20:-20):Mathf.Max(0,stride*(i==2?-1:1))*25,0,0);
            if(Step==WorkStep.Hammer)camera.FocusWork(Vector3.Lerp(chest.position,stake.position+Vector3.up*10,.5f),boat.transform.right,25);
            else if(Step==WorkStep.CarryBundle&&CarryProgress>=1)camera.FocusWork(Vector3.Lerp(chest.position,work+Vector3.up*12,.5f),boat.transform.right,27);
            else camera.ClearWorkFocus();
        }
        void OnGUI()
        {
            if(Active!=this||BachDangFrontEnd.IsBlocking)return;
            var style=new GUIStyle(GUI.skin.box){fontSize=Mathf.RoundToInt(Screen.height*.025f),alignment=TextAnchor.MiddleCenter,wordWrap=true};
            string hint=Step==WorkStep.TakeBundle?"[E] Lấy bó cọc trên thuyền":Step==WorkStep.CarryBundle?(CarryProgress<1?"Giữ W • Vác bó cọc tới mạn thuyền":"Rê chuột chọn chỗ • Cuộn chuột chỉnh nghiêng • XANH: E dựng • ĐỎ: vướng"):
                Step==WorkStep.Hammer?$"Giữ chuột trái, kéo LÊN nâng búa {HammerLift:0%} • Thả chuột để NỆN {hits}/3":"Đã dựng xong cụm cọc";
            GUI.Box(new Rect(Screen.width*.19f,Screen.height*.81f,Screen.width*.62f,Screen.height*.09f),$"CÔNG BINH DỰNG TRẬN ĐỊA • Bó {Mathf.Min(3,bundles+1)}/3 • X: hủy\n"+hint,style);
        }
        public void FinishWork()
        {
            if(Active!=this)return;Active=null;
            if(props!=null)Destroy(props);if(rig!=null)rig.enabled=rigEnabled;
            if(camera!=null){camera.ClearWorkFocus();camera.distance=cameraSettings.x;camera.heightOffset=cameraSettings.y;camera.rightOffset=cameraSettings.z;camera.SetElevation(cameraElevation);}
            if(previewGood!=null)Destroy(previewGood);if(previewBad!=null)Destroy(previewBad);if(waterMaterial!=null)Destroy(waterMaterial);if(bindingMaterial!=null)Destroy(bindingMaterial);
            if(boat!=null&&boat.IsAboard)boat.ResumeRowing();Destroy(this);
        }
        void OnDestroy(){if(Active==this){Active=null;if(props!=null)Destroy(props);}}
    }
}
