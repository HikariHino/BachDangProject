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
        public float Progress=>Mathf.Clamp01(hits/3f);
        public float CarryProgress {get;private set;}
        public int CompletedBundles=>bundles;
        public Vector2 PlacementOffset {get;private set;}
        public float HammerLift {get;private set;}
        public bool PlacementValid {get;private set;}
        public bool HasAimedStake {get;private set;}
        public Vector3 PlacementPoint {get;private set;}
        public float RequiredLean=>zone.WorkingIndex%3==1?18:zone.WorkingIndex%3==2?9:12;
        public Vector3 FinishedStakePosition {get;private set;}
        public Quaternion FinishedStakeRotation {get;private set;}
        public float StakeLean=>lean;
        public float HammerAim {get;private set;}
        public float AimError=>HammerAim+Mathf.Sin(Time.time*1.3f)*.1f;
        public bool PowerReady=>HammerLift>=.65f&&HammerLift<=.95f;
        public int Misses {get;private set;}
        public bool HandsOnly=>hands?.BodyHidden??false;
        public bool IsSwinging=>pendingStrike;
        public float HandContactError=>hands?.MaxContactError??0;
        public float HandWristAngle=>hands?.MaxWristAngle??0;
        public float HandSegmentError=>hands?.MaxSegmentError??0;
        public float DisplayedGripWeight=>hands?.ActiveGripWeight??0;
        public float HammerHeadError {get;private set;}
        public string Feedback {get;private set;}="";
        WorkbenchHands hands;
        bool hammerHeld,pendingStrike,pendingGood;float releaseLift,visibleDepth;
        int bundles,hits;float nextHit,swing,sole,walkingUntil;
        RiverStakeZone zone;QuestRowboat boat;Transform player,bundle,stake,hammer,stack;
        Animator rig;Transform arm,forearm;Quaternion armRest,forearmRest;bool rigEnabled;
        Transform[] arms;Quaternion[] armPose;Transform rightHand,leftHand,chest;float cameraElevation;
        Material previewGood,previewBad;
        ParticleSystem splash;Material waterMaterial,bindingMaterial;float lean=12;
        Transform[] legs;Quaternion[] legRest;
        Camera_Script camera;Vector3 cameraSettings,localStart;GameObject props;
        Material timber;
        LineRenderer impactRing;
        StakeWorkCrew crew;
        float workStarted;
        public float BeatPosition=>Mathf.PingPong((Time.time-workStarted)/1.2f,1);
        public bool IsBeatReady=>BeatPosition>=.38f&&BeatPosition<=.82f;
        public bool SupportedOnDeck {get;private set;}
        public int WorkingCrew=>crew?.Count??0;
        public Mesh StakeMesh=>stake!=null?stake.GetComponentInChildren<MeshFilter>().sharedMesh:null;
        public static bool AllowsCamera=>Active!=null&&Time.timeScale>0&&!BachDangFrontEnd.IsBlocking&&
            !(StoryDialogueUI.Instance?.IsOpen??false)&&!OpeningEvent.IsOpeningActive;
        public void Begin(RiverStakeZone owner)
        {
            zone=owner;boat=QuestRowboat.Instance;player=BeachBoatQuestSession.Instance.player.transform;
            rig=player.GetComponentInChildren<Animator>();rigEnabled=rig.enabled;
            hands=new WorkbenchHands(player,rig);
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
            workStarted=Time.time;
            props=new GameObject("Stake_Work_3D_Props");timber=zone.fieldStakes[0].GetComponentInChildren<Renderer>(true).sharedMaterial;
            crew=new StakeWorkCrew(boat,zone.fieldStakes[0],props.transform,timber);
            stack=new GameObject("Bo_coc_tren_thuyen").transform;stack.SetParent(props.transform,false);
            for(int i=0;i<6;i++)
            {var log=Log("Coc_tren_bo",stack);log.localPosition=new Vector3((i%3-1)*2.2f,1+i/3*1.8f,0);log.localRotation=Quaternion.Euler(90,0,0);}
            bundle=new GameObject("Bo_coc_dang_vac").transform;bundle.SetParent(props.transform,false);
            for(int i=0;i<3;i++){var log=Log("Coc_go",bundle);log.localPosition=i==2?new Vector3(0,0,1.1f):new Vector3(i==0?-.9f:.9f,0,-.5f);}
            bindingMaterial=new Material(timber){color=new Color(.45f,.32f,.17f)};
            foreach(int at in new[]{-7,7}){var tie=new GameObject("Day_buoc_bo_coc").AddComponent<LineRenderer>();tie.transform.SetParent(bundle,false);tie.useWorldSpace=false;tie.sharedMaterial=bindingMaterial;tie.widthMultiplier=.25f;tie.positionCount=33;for(int i=0;i<33;i++){float a=i/32f*Mathf.PI*2;tie.SetPosition(i,new Vector3(Mathf.Cos(a)*1.9f,at,Mathf.Sin(a)*1.9f));}}
            stake=Log("Coc_dang_dong",props.transform);hammer=new GameObject("Bua_go").transform;hammer.SetParent(props.transform,false);
            var handle=GameObject.CreatePrimitive(PrimitiveType.Cylinder);handle.transform.SetParent(hammer,false);handle.transform.localScale=new Vector3(.6f,5.5f,.6f);Decorative(handle);
            var head=GameObject.CreatePrimitive(PrimitiveType.Cube);head.transform.SetParent(hammer,false);head.transform.localPosition=Vector3.up*4;head.transform.localScale=new Vector3(4.5f,2.5f,2.5f);Decorative(head);
            bundle.gameObject.SetActive(false);stake.gameObject.SetActive(false);hammer.gameObject.SetActive(false);
            previewGood=new Material(timber){color=new Color(.2f,.9f,.4f)};previewBad=new Material(timber){color=new Color(.95f,.15f,.1f)};
            impactRing=new GameObject("Tam_dau_coc").AddComponent<LineRenderer>();impactRing.transform.SetParent(props.transform,false);impactRing.useWorldSpace=true;impactRing.positionCount=33;impactRing.widthMultiplier=.18f;impactRing.sharedMaterial=previewGood;
            var spray=new GameObject("Nuoc_ban_khi_dong_coc");spray.transform.SetParent(props.transform);splash=spray.AddComponent<ParticleSystem>();splash.Stop();
            var main=splash.main;main.loop=false;main.playOnAwake=false;main.startLifetime=.55f;main.startSpeed=9;main.startSize=.45f;main.gravityModifier=1;main.maxParticles=40;main.startColor=new Color(.68f,.85f,.83f,.8f);main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=splash.emission;emission.enabled=false;var shape=splash.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=40;shape.radius=.5f;
            waterMaterial=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));waterMaterial.color=new Color(.6f,.8f,.82f,.7f);splash.GetComponent<ParticleSystemRenderer>().sharedMaterial=waterMaterial;
        }
        Transform Log(string name,Transform parent)
        {
            return StakeWorkVisual.Create(name,parent,zone.fieldStakes[0]);
        }
        void Decorative(GameObject go){Destroy(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=timber;}
        public bool Interact()
        {
            if(Active!=this||BachDangFrontEnd.IsBlocking||Time.timeScale<=0)return false;
            if(Step==WorkStep.TakeBundle){Step=WorkStep.CarryBundle;CarryProgress=0;HasAimedStake=false;PlacementOffset=Vector2.zero;lean=RequiredLean;Feedback="";bundle.gameObject.SetActive(true);BeachBoatSoundscape.Instance?.PlayInteraction();return true;}
            if(Step==WorkStep.CarryBundle)
            {
                if(CarryProgress<1)Feedback="Giữ W để đi tới chỗ đứng trong lòng thuyền";
                else if(!HasAimedStake)Feedback="Rê chuột chọn chỗ dựng cọc bên mạn";
                else if(!PlacementValid)Feedback="Chỗ đặt bị vướng • rê sang bên hoặc R đặt lại";
                else if(Mathf.Abs(lean-RequiredLean)>3)Feedback=$"Căn góc {RequiredLean:0}° • cuộn chuột hoặc A / D";
            }
            if(Step==WorkStep.CarryBundle&&CarryProgress>=1&&PlacementValid&&HasAimedStake&&Mathf.Abs(lean-RequiredLean)<=3)
            {Step=WorkStep.Hammer;hits=0;HammerLift=visibleDepth=0;pendingStrike=false;Feedback="";bundle.gameObject.SetActive(false);stake.gameObject.SetActive(true);hammer.gameObject.SetActive(true);return true;}
            return false;
        }
        public void WalkBundle(float dt)
        {if(Active==this&&Step==WorkStep.CarryBundle&&Time.timeScale>0&&!BachDangFrontEnd.IsBlocking){CarryProgress=Mathf.Clamp01(CarryProgress+Mathf.Max(0,dt)/1.6f);walkingUntil=Time.time+.12f;}}
        public bool Strike()
        {
            if(Active!=this||Step!=WorkStep.Hammer||pendingStrike||HammerLift<.25f||Time.time<nextHit||Time.timeScale<=0||BachDangFrontEnd.IsBlocking)return false;
            pendingGood=PowerReady&&Mathf.Abs(AimError)<=.28f&&IsBeatReady;
            Feedback=!PowerReady?(HammerLift<.65f?"Lực nhẹ • nâng búa vào dải xanh":"Quá lực • nhả sớm hơn trong dải xanh"):Mathf.Abs(AimError)>.28f?"Lệch đầu cọc • kéo ngang để căn búa":"Đúng tâm, đủ lực";
            if(PowerReady&&Mathf.Abs(AimError)<=.28f&&!IsBeatReady)Feedback="Lệch nhịp • thả khi vạch NHỊP NỆN vào xanh";
            nextHit=Time.time+.65f;releaseLift=HammerLift;swing=.25f;HammerLift=0;pendingStrike=true;return true;
        }
        void Impact()
        {
            pendingStrike=false;if(pendingGood)hits++;else Misses++;BeachBoatSoundscape.Instance?.PlayStake();
            splash.transform.SetPositionAndRotation(PlacementPoint,Quaternion.Euler(-90,0,0));splash.Emit(14);
            if(hits==3)
            {
                var planted=Instantiate(stake,props.transform);planted.name="Coc_mau_da_dong_"+bundles;planted.position=stake.position-Vector3.up*3;planted.gameObject.SetActive(true);
                FinishedStakePosition=planted.position;FinishedStakeRotation=planted.rotation;
                bundles++;Step=WorkStep.Finished;CarryProgress=0;HammerAim=bundles%2==0?-.36f:.36f;stake.gameObject.SetActive(false);hammer.gameObject.SetActive(false);
            }
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
        public void AdjustLean(float degrees){if(Active==this&&Step==WorkStep.CarryBundle&&float.IsFinite(degrees))lean=Mathf.Clamp(lean+degrees,6,22);}
        public void AimHammer(float delta){if(Active==this&&Step==WorkStep.Hammer&&!pendingStrike&&float.IsFinite(delta))HammerAim=Mathf.Clamp(HammerAim+Mathf.Clamp(delta,-.2f,.2f),-1,1);}
        void Update()
        {
            if(Active!=this||Time.timeScale<=0||BachDangFrontEnd.IsBlocking)return;
            if(Input.GetKeyDown(KeyCode.X)){zone.RestoreCheckpoint(zone.CaptureCheckpoint());return;}
            if(Input.GetKeyDown(KeyCode.E))Interact();
            if(Input.GetKey(KeyCode.W))WalkBundle(Time.deltaTime);
            if(Step==WorkStep.CarryBundle&&CarryProgress>=1)AimStake(new Vector2(Input.GetAxisRaw("Mouse X"),Input.GetAxisRaw("Mouse Y"))*.035f);
            if(Step==WorkStep.CarryBundle&&CarryProgress>=1)AdjustLean(Input.mouseScrollDelta.y*2);
            if(Step==WorkStep.CarryBundle&&CarryProgress>=1)
            {
                if(Input.GetKeyDown(KeyCode.A))AdjustLean(-2);if(Input.GetKeyDown(KeyCode.D))AdjustLean(2);
                if(Input.GetKeyDown(KeyCode.R)){PlacementOffset=Vector2.zero;lean=RequiredLean;HasAimedStake=false;Feedback="Rê chọn vị trí mới rồi E dựng cọc";}
            }
            if(Input.GetMouseButtonDown(0))hammerHeld=true;
            if(hammerHeld&&Input.GetMouseButton(0)){LiftHammer(Input.GetAxisRaw("Mouse Y")*.065f);AimHammer(Input.GetAxisRaw("Mouse X")*.05f);}
            if(Input.GetMouseButtonUp(0)){hammerHeld=false;Strike();}
            swing=Mathf.Max(0,swing-Time.deltaTime);
            if(pendingStrike&&swing<=0)Impact();
        }
        Vector3 SafeDeckPoint(BoatHullCollision hull,Vector3 requested,out bool supported)
        {
            bool Supported(Vector3 p,out Vector3 floor)
            {
                if(!hull.TryDeckSurface(p,out floor))return false;
                foreach(var offset in new[]{boat.transform.right*3,-boat.transform.right*3,boat.transform.forward*3,-boat.transform.forward*3})
                    if(!hull.TryDeckSurface(p+offset,out var foot)||Mathf.Abs(foot.y-floor.y)>2.5f)return false;
                return true;
            }
            if(Supported(requested,out var point)){supported=true;return point;}
            for(int ring=1;ring<=6;ring++)for(int i=0;i<12;i++)
            {
                float a=i*Mathf.PI/6;var test=requested+(boat.transform.right*Mathf.Cos(a)+boat.transform.forward*Mathf.Sin(a))*ring*2;
                if(Supported(test,out point)){supported=true;return point;}
            }
            supported=false;return hull.TryDeckSurface(boat.seat.position,out point)?point:boat.seat.position;
        }
        void LateUpdate()
        {
            if(Active!=this||props==null)return;
            hands.Show(Step==WorkStep.Hammer);hands.Grip(WorkbenchHands.GripPose.Rod,WorkbenchHands.GripPose.Rod);
            float walk=Step==WorkStep.CarryBundle?CarryProgress:Step==WorkStep.Hammer?1:0;
            var hull=BoatHullCollision.Get(boat.transform);var box=hull.SolidHull;var scale=boat.transform.lossyScale;
            var end=localStart;end.x=box.center.x+Mathf.Max(0,box.size.x*.5f-10/Mathf.Abs(scale.x));end.z=Mathf.Clamp(localStart.z+4/Mathf.Abs(scale.z),box.center.z-box.size.z*.3f,box.center.z+box.size.z*.3f);
            var wanted=boat.transform.TransformPoint(Vector3.Lerp(localStart,end,walk));var p=SafeDeckPoint(hull,wanted,out bool supported);SupportedOnDeck=supported;
            p.y-=sole-.2f;
            player.SetPositionAndRotation(p,boat.transform.rotation);var body=player.GetComponent<Rigidbody>();body.position=p;body.rotation=player.rotation;
            var stock=boat.transform.position-boat.transform.right*6-boat.transform.forward*3;
            if(BoatHullCollision.Get(boat.transform).TryDeckSurface(stock,out var stockFloor))stock.y=stockFloor.y+1;
            else stock.y=boat.seat.position.y+1;
            stack.position=stock;stack.rotation=boat.transform.rotation;
            var carry=chest.position+player.forward*4+Vector3.down*2;
            bundle.SetPositionAndRotation(carry,player.rotation*Quaternion.Euler(0,0,90));
            var workLocal=end;workLocal.x=box.center.x+box.size.x*.5f+(5+PlacementOffset.x*2)/Mathf.Abs(scale.x);workLocal.z=Mathf.Clamp(end.z+(bundles-1)*9/Mathf.Abs(scale.z)+PlacementOffset.y*4/Mathf.Abs(scale.z),box.center.z-box.size.z*.38f,box.center.z+box.size.z*.38f);
            var work=boat.transform.TransformPoint(workLocal);work.y=TideSystem.CurrentWaterHeight;
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
            stake.rotation=boat.transform.rotation*Quaternion.Euler(0,0,-lean);
            // A person standing on the planks drives a post at chest height.
            // Keeping its head above the helmet forced the first-person camera skyward.
            float headHeight=Mathf.Max(work.y+9,chest.position.y-2);
            stake.position=work+Vector3.up*(headHeight-work.y-stake.up.y*12-visibleDepth);
            impactRing.gameObject.SetActive(Step==WorkStep.Hammer);impactRing.sharedMaterial=Mathf.Abs(AimError)<=.28f?previewGood:previewBad;
            for(int n=0;n<33;n++){float a=n/32f*Mathf.PI*2;impactRing.SetPosition(n,stake.position+stake.up*12+Vector3.up*.15f+(boat.transform.right*Mathf.Cos(a)+boat.transform.forward*Mathf.Sin(a))*1.4f);}
            for(int i=0;i<4;i++)arms[i].localRotation=armPose[i];
            if(Step==WorkStep.CarryBundle)
            {
                QuestRowboat.SolveArm(arms[0],arms[1],rightHand,carry+player.right*3,arms[0].position+player.right*4-Vector3.up*4);
                QuestRowboat.SolveArm(arms[2],arms[3],leftHand,carry-player.right*3,arms[2].position-player.right*4-Vector3.up*4);
            }
            else if(Step==WorkStep.Hammer)
            {
                float lift=pendingStrike?releaseLift*Mathf.SmoothStep(0,1,swing/.25f):HammerLift;
                var headGoal=stake.position+stake.up*12+Vector3.up*(lift*6)+boat.transform.right*(AimError*3+lift*3);var grip=headGoal-Vector3.up*4;
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
                // The displayed tool and fixed-length display arms meet the real
                // stake head, independently of the hidden body's reach limits.
                contact=headGoal-Vector3.up*4;direction=Vector3.up;
                hammer.SetPositionAndRotation(contact,Quaternion.identity);hammer.localScale=Vector3.one;
                HammerHeadError=Vector3.Distance(hammer.TransformPoint(Vector3.up*4),headGoal);
                hands.Pose(camera.transform,contact,contact-direction*3.6f,false,false,direction,direction,true);
            }
            float stride=Step==WorkStep.CarryBundle&&CarryProgress<1&&Time.time<walkingUntil?Mathf.Sin(CarryProgress*Mathf.PI*4):0;
            // Imported leg-bone X axes are not the actor's knee hinge. Swing
            // around the character's actual right axis, keeping knees forward.
            for(int i=0;i<4;i++){float side=i%2==0?1:-1;float angle=i<2?stride*side*12:Mathf.Max(0,-stride*side)*14;legs[i].localRotation=legRest[i];legs[i].rotation=Quaternion.AngleAxis(angle,player.right)*legs[i].rotation;}
            crew?.Step(Time.time);
            if(Step==WorkStep.Hammer)camera.FocusHandwork(rig.GetBoneTransform(HumanBodyBones.Head).position+player.forward*1.5f,stake.position+stake.up*12+Vector3.up*2);
            else if(Step==WorkStep.CarryBundle&&CarryProgress>=1)camera.FocusWork(Vector3.Lerp(chest.position,work+Vector3.up*12,.5f),boat.transform.right,27);
            else camera.ClearWorkFocus();
        }
        void OnGUI()
        {
            if(Active!=this||BachDangFrontEnd.IsBlocking)return;
            var style=new GUIStyle(GUI.skin.box){fontSize=Mathf.RoundToInt(Screen.height*.025f),alignment=TextAnchor.MiddleCenter,wordWrap=true};
            string hint=Step==WorkStep.TakeBundle?"[E] Lấy cọc mẫu trên thuyền":Step==WorkStep.CarryBundle?(CarryProgress<1?"Giữ W • Vác cọc tới mạn thuyền":$"Rê chọn chỗ • Cuộn chuột: nghiêng {lean:0}° / cần {RequiredLean:0}° ±3 • E dựng"):
                    Step==WorkStep.Hammer?$"Giữ chuột trái • Kéo NGANG căn tâm, kéo LÊN nâng búa • THẢ trong dải xanh • Nện {hits}/3\n{Feedback}":"Cọc mẫu đã chắc • Chèo tới điểm tiếp theo";
            if(Step==WorkStep.CarryBundle)hint+="\n"+(string.IsNullOrEmpty(Feedback)?"A / D hoặc cuộn chuột chỉnh góc • R chọn lại vị trí":Feedback);
            if(!string.IsNullOrEmpty(crew?.CurrentCall))hint+="\nCông binh: "+crew.CurrentCall;
            GUI.Box(new Rect(Screen.width*.12f,Screen.height*.81f,Screen.width*.76f,Screen.height*.14f),$"CÔNG BINH • Vị trí {zone.WorkingIndex+1}/{zone.ClusterCount} • Đã xong {zone.CompletedClusterCount}/{zone.ClusterCount} • X: hủy\n"+hint,style);
            if(Step==WorkStep.Hammer)
            {
                void Gauge(float y,float value,float min,float max,string label){float x=Screen.width*.31f,w=Screen.width*.38f;GUI.color=Color.black;GUI.Box(new Rect(x,y,w,22),"");GUI.color=new Color(.2f,.8f,.4f);GUI.DrawTexture(new Rect(x+w*min,y,w*(max-min),22),Texture2D.whiteTexture);GUI.color=Color.white;GUI.DrawTexture(new Rect(x+w*Mathf.Clamp01(value)-3,y-3,6,28),Texture2D.whiteTexture);GUI.Label(new Rect(x,y-25,w,25),label,style);}
                Gauge(Screen.height*.66f,(AimError+1)*.5f,.36f,.64f,"CĂN TÂM • KÉO NGANG");Gauge(Screen.height*.75f,HammerLift,.65f,.95f,"LỰC NỆN • KÉO LÊN RỒI THẢ TRONG XANH");GUI.color=Color.white;
                Gauge(Screen.height*.57f,BeatPosition,.38f,.82f,"NHỊP NỆN • THẢ KHI VẠCH VÀO XANH");
            }
        }
        public void FinishWork()
        {
            if(Active!=this)return;Active=null;
            hands?.Dispose();hands=null;
            crew?.Dispose();crew=null;
            if(props!=null)Destroy(props);if(rig!=null)rig.enabled=rigEnabled;
            if(camera!=null){camera.ClearWorkFocus();camera.distance=cameraSettings.x;camera.heightOffset=cameraSettings.y;camera.rightOffset=cameraSettings.z;camera.SetElevation(cameraElevation);}
            if(previewGood!=null)Destroy(previewGood);if(previewBad!=null)Destroy(previewBad);if(waterMaterial!=null)Destroy(waterMaterial);if(bindingMaterial!=null)Destroy(bindingMaterial);
            if(boat!=null&&boat.IsAboard)boat.ResumeRowing();Destroy(this);
        }
        void OnDestroy(){hands?.Dispose();hands=null;if(Active==this){Active=null;if(props!=null)Destroy(props);}}
    }
}
