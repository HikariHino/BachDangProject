using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Carry bundles across the boat and drive anchor stakes in the actual third-person scene.</summary>
    [DefaultExecutionOrder(50)]
    public sealed class StakeConstruction : MonoBehaviour
    {
        public enum WorkStep { TakeBundle, CarryBundle, Hammer, Finished }
        public static StakeConstruction Active {get;private set;}
        public WorkStep Step {get;private set;}
        public float Progress=>Mathf.Clamp01((bundles*3+hits)/9f);
        public float CarryProgress {get;private set;}
        public int CompletedBundles=>bundles;
        int bundles,hits;float nextHit,swing,sole,walkingUntil;
        RiverStakeZone zone;QuestRowboat boat;Transform player,bundle,stake,hammer,stack;
        Animator rig;Transform arm,forearm;Quaternion armRest,forearmRest;bool rigEnabled;
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
            legs=new[]{rig.GetBoneTransform(HumanBodyBones.LeftUpperLeg),rig.GetBoneTransform(HumanBodyBones.RightUpperLeg),rig.GetBoneTransform(HumanBodyBones.LeftLowerLeg),rig.GetBoneTransform(HumanBodyBones.RightLowerLeg)};
            legRest=new Quaternion[4];for(int i=0;i<4;i++)legRest[i]=legs[i].localRotation;
            camera=BeachBoatQuestSession.Instance.storyCamera.GetComponent<Camera_Script>();cameraSettings=new Vector3(camera.distance,camera.heightOffset,camera.rightOffset);
            camera.distance=64;camera.heightOffset=24;camera.rightOffset=12;
            localStart=boat.transform.InverseTransformPoint(boat.seat.position);localStart.y=0;
            bundles=hits=0;CarryProgress=0;Step=WorkStep.TakeBundle;Active=this;
            props=new GameObject("Stake_Work_3D_Props");timber=zone.fieldStakes[0].GetComponentInChildren<Renderer>(true).sharedMaterial;
            stack=new GameObject("Bo_coc_tren_thuyen").transform;stack.SetParent(props.transform,false);
            for(int i=0;i<6;i++)
            {var log=Log("Coc_tren_bo",stack);log.localPosition=new Vector3((i%3-1)*2.2f,1+i/3*1.8f,0);log.localRotation=Quaternion.Euler(90,0,0);}
            bundle=new GameObject("Bo_coc_dang_vac").transform;bundle.SetParent(props.transform,false);
            for(int i=0;i<3;i++){var log=Log("Coc_go",bundle);log.localPosition=Vector3.right*(i-1)*1.8f;}
            stake=Log("Coc_dang_dong",props.transform);hammer=new GameObject("Bua_go").transform;hammer.SetParent(props.transform,false);
            var handle=GameObject.CreatePrimitive(PrimitiveType.Cylinder);handle.transform.SetParent(hammer,false);handle.transform.localScale=new Vector3(.6f,4,.6f);Decorative(handle);
            var head=GameObject.CreatePrimitive(PrimitiveType.Cube);head.transform.SetParent(hammer,false);head.transform.localPosition=Vector3.up*4;head.transform.localScale=new Vector3(4.5f,2.5f,2.5f);Decorative(head);
            bundle.gameObject.SetActive(false);stake.gameObject.SetActive(false);hammer.gameObject.SetActive(false);
        }
        Transform Log(string name,Transform parent)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name=name;go.transform.SetParent(parent,false);go.transform.localScale=new Vector3(1.6f,12,1.6f);Decorative(go);return go.transform;
        }
        void Decorative(GameObject go){Destroy(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=timber;}
        public bool Interact()
        {
            if(Active!=this||BachDangFrontEnd.IsBlocking||Time.timeScale<=0)return false;
            if(Step==WorkStep.TakeBundle){Step=WorkStep.CarryBundle;CarryProgress=0;bundle.gameObject.SetActive(true);BeachBoatSoundscape.Instance?.PlayInteraction();return true;}
            if(Step==WorkStep.CarryBundle&&CarryProgress>=1)
            {Step=WorkStep.Hammer;hits=0;bundle.gameObject.SetActive(false);stake.gameObject.SetActive(true);hammer.gameObject.SetActive(true);return true;}
            return false;
        }
        public void WalkBundle(float dt)
        {if(Active==this&&Step==WorkStep.CarryBundle&&Time.timeScale>0&&!BachDangFrontEnd.IsBlocking){CarryProgress=Mathf.Clamp01(CarryProgress+Mathf.Max(0,dt)/1.6f);walkingUntil=Time.time+.12f;}}
        public bool Strike()
        {
            if(Active!=this||Step!=WorkStep.Hammer||Time.time<nextHit||Time.timeScale<=0||BachDangFrontEnd.IsBlocking)return false;
            nextHit=Time.time+.65f;swing=.45f;hits++;BeachBoatSoundscape.Instance?.PlayStake();
            if(hits==3)
            {bundles++;hits=0;Step=bundles==3?WorkStep.Finished:WorkStep.TakeBundle;CarryProgress=0;stake.gameObject.SetActive(false);hammer.gameObject.SetActive(false);}
            return true;
        }
        void Update()
        {
            if(Active!=this||Time.timeScale<=0||BachDangFrontEnd.IsBlocking)return;
            if(Input.GetKeyDown(KeyCode.X)){zone.RestoreCheckpoint(zone.CaptureCheckpoint());return;}
            if(Input.GetKeyDown(KeyCode.E))Interact();
            if(Input.GetKey(KeyCode.W))WalkBundle(Time.deltaTime);
            if(Input.GetMouseButtonDown(0))Strike();
            swing=Mathf.Max(0,swing-Time.deltaTime);
        }
        void LateUpdate()
        {
            if(Active!=this||props==null)return;
            float walk=Step==WorkStep.CarryBundle?CarryProgress:Step==WorkStep.Hammer?1:0;
            var p=boat.transform.TransformPoint(localStart+Vector3.forward*(walk*10));
            if(BoatHullCollision.Get(boat.transform).TryDeckSurface(p,out var floor))p.y=floor.y-sole+.2f;
            else p.y=boat.seat.position.y+4;
            if(CarryProgress>0)p=RiverHullCollision.Move(player.position+Vector3.up*8,p+Vector3.up*8,3.5f,false,player,BoatHullCollision.Get(boat.transform))-Vector3.up*8;
            player.SetPositionAndRotation(p,boat.transform.rotation);var body=player.GetComponent<Rigidbody>();body.position=p;body.rotation=player.rotation;
            var stock=boat.transform.position-boat.transform.right*6-boat.transform.forward*3;
            if(BoatHullCollision.Get(boat.transform).TryDeckSurface(stock,out var stockFloor))stock.y=stockFloor.y+1;
            else stock.y=boat.seat.position.y+1;
            stack.position=stock;stack.rotation=boat.transform.rotation;
            bundle.SetPositionAndRotation(p+player.right*2+Vector3.up*15,player.rotation*Quaternion.Euler(0,0,28));
            var work=p+player.forward*7+player.right*10;work.y=TideSystem.CurrentWaterHeight+15-hits*3;
            stake.position=work;stake.rotation=Quaternion.Euler(0,0,-9);
            arm.localRotation=armRest*Quaternion.Euler(Step==WorkStep.Hammer?-80+Mathf.Sin(swing/.45f*Mathf.PI)*65:-55,0,-35);
            forearm.localRotation=forearmRest*Quaternion.Euler(-70,0,0);
            float stride=Step==WorkStep.CarryBundle&&CarryProgress<1&&Time.time<walkingUntil?Mathf.Sin(CarryProgress*Mathf.PI*6):0;
            for(int i=0;i<4;i++)legs[i].localRotation=legRest[i]*Quaternion.Euler(i<2?stride*(i==0?20:-20):Mathf.Max(0,stride*(i==2?-1:1))*25,0,0);
            hammer.SetPositionAndRotation(rig.GetBoneTransform(HumanBodyBones.RightHand).position,rig.GetBoneTransform(HumanBodyBones.RightHand).rotation*Quaternion.Euler(0,0,90));
        }
        void OnGUI()
        {
            if(Active!=this||BachDangFrontEnd.IsBlocking)return;
            var style=new GUIStyle(GUI.skin.box){fontSize=Mathf.RoundToInt(Screen.height*.025f),alignment=TextAnchor.MiddleCenter,wordWrap=true};
            string hint=Step==WorkStep.TakeBundle?"[E] Lấy bó cọc trên thuyền":Step==WorkStep.CarryBundle?(CarryProgress<1?"Giữ W • Vác bó cọc tới mũi thuyền":"[E] Đặt cọc xuống vị trí"):
                Step==WorkStep.Hammer?$"Chuột trái • Đóng búa {hits}/3":"Đã dựng xong cụm cọc";
            GUI.Box(new Rect(Screen.width*.19f,Screen.height*.81f,Screen.width*.62f,Screen.height*.09f),$"CÔNG BINH DỰNG TRẬN ĐỊA • Bó {Mathf.Min(3,bundles+1)}/3 • X: hủy\n"+hint,style);
        }
        public void FinishWork()
        {
            if(Active!=this)return;Active=null;
            if(props!=null)Destroy(props);if(rig!=null)rig.enabled=rigEnabled;
            if(camera!=null){camera.distance=cameraSettings.x;camera.heightOffset=cameraSettings.y;camera.rightOffset=cameraSettings.z;}
            if(boat!=null&&boat.IsAboard)boat.ResumeRowing();Destroy(this);
        }
        void OnDestroy(){if(Active==this){Active=null;if(props!=null)Destroy(props);}}
    }
}
