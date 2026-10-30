using UnityEngine;
namespace BeachBoatStory
{
    [DefaultExecutionOrder(340)]
    public sealed class PlayerShield : MonoBehaviour
    {
        public static PlayerShield Instance {get;private set;}
        public bool Equipped {get;private set;}
        public bool IsGuarding {get;private set;}
        public int BlockedArrows {get;private set;}
        public string Feedback {get;private set;}="";
        public float FeedbackUntil {get;private set;}
        BeachBoatQuestSession session;PlayerVitals vitals;Animator rig;Transform upper,lower,hand,chest,shield;
        Material wood,rim,metal;BowGripProfile grip;
        void Awake(){Instance=this;}
        void Start()
        {
            session=GetComponent<BeachBoatQuestSession>();vitals=session.player.GetComponent<PlayerVitals>();rig=session.player.GetComponentInChildren<Animator>();grip=BowGripProfile.Load();
            upper=rig.GetBoneTransform(HumanBodyBones.LeftUpperArm);lower=rig.GetBoneTransform(HumanBodyBones.LeftLowerArm);hand=rig.GetBoneTransform(HumanBodyBones.LeftHand);chest=rig.GetBoneTransform(HumanBodyBones.Chest);
            Material Make(Color color,float metallic=0){var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){color=color};m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",.2f);return m;}
            wood=Make(new Color(.29f,.16f,.065f));rim=Make(new Color(.12f,.075f,.04f));metal=Make(new Color(.32f,.29f,.22f),.65f);
            shield=new GameObject("Khien_go_nguoi_choi").transform;shield.SetParent(transform,false);
            void Part(string name,PrimitiveType shape,Vector3 position,Vector3 size,Material material,bool disc=false){var g=GameObject.CreatePrimitive(shape);g.name=name;g.transform.SetParent(shield,false);g.transform.localPosition=position;g.transform.localScale=size;if(disc)g.transform.localRotation=Quaternion.Euler(90,0,0);var c=g.GetComponent<Collider>();c.enabled=false;Destroy(c);g.GetComponent<Renderer>().sharedMaterial=material;}
            Part("Vanh_khien",PrimitiveType.Cylinder,Vector3.zero,new Vector3(8.8f,.32f,8.8f),rim,true);
            Part("Mat_go",PrimitiveType.Cylinder,Vector3.forward*.3f,new Vector3(8.1f,.2f,8.1f),wood,true);
            Part("Num_dong",PrimitiveType.Sphere,Vector3.forward*.65f,new Vector3(2.2f,2.2f,.9f),metal);
            Part("Tay_cam_mat_sau",PrimitiveType.Cylinder,Vector3.back*.7f,new Vector3(.45f,1.5f,.45f),rim);
            for(int i=-3;i<=3;i+=2){float y=Mathf.Sqrt(4*4-i*i);var line=new GameObject("Nep_van_go").AddComponent<LineRenderer>();line.transform.SetParent(shield,false);line.useWorldSpace=false;line.positionCount=2;line.widthMultiplier=.075f;line.sharedMaterial=rim;line.SetPosition(0,new Vector3(i,-y,.52f));line.SetPosition(1,new Vector3(i,y,.52f));}
            shield.gameObject.SetActive(false);gameObject.AddComponent<PlayerShieldPose>().owner=this;
        }
        bool Available=>session!=null&&session.storyMode&&!BeachBoatQuestSession.InputBlocked&&ArcheryTraining.Active==null&&RiverBoardingCombat.Instance?.IsTraversing!=true;
        public bool SetEquipped(bool value)
        {if(value&&!Available)return false;Equipped=value;IsGuarding=false;if(value)RiverPlayerCombat.Instance?.SetAim(false);return true;}
        public void Restore(bool value){Equipped=value;IsGuarding=false;BlockedArrows=0;Feedback="";}
        public void SetGuarding(bool value){IsGuarding=value&&Equipped&&Available&&vitals.Stamina>=8;if(IsGuarding)RiverPlayerCombat.Instance?.SetAim(false);}
        void Update(){if(Input.GetKeyDown(KeyCode.G)&&Available)SetEquipped(!Equipped);SetGuarding(Equipped&&Input.GetMouseButton(1)&&Cursor.lockState==CursorLockMode.Locked);}
        public bool TryBlockArrow(Vector3 origin)
        {
            if(!IsGuarding||!Available)return false;var incoming=origin-(session.player.transform.position+Vector3.up*10);
            if(incoming.sqrMagnitude<.01f||Vector3.Dot(session.player.transform.forward,incoming.normalized)<.5f||vitals.Stamina<8)return false;
            if(!vitals.TryExert(8,1))return false;BlockedArrows++;Feedback="ĐỠ TÊN • -8 thể lực";FeedbackUntil=Time.unscaledTime+2;
            BeachBoatSoundscape.Instance?.PlayShieldBlock();if(vitals.Stamina<8)IsGuarding=false;return true;
        }
        public void Pose()
        {
            if(shield==null)return;bool show=Equipped&&Available;shield.gameObject.SetActive(show);if(!show)return;
            var actor=session.player.transform;
            if(IsGuarding)
            {
                var forward=session.storyCamera.transform.forward;forward.y=0;if(forward.sqrMagnitude>.01f){actor.rotation=Quaternion.LookRotation(forward);actor.GetComponent<Rigidbody>().rotation=actor.rotation;}
                var contact=chest.position+actor.forward*4.5f-actor.right*2.3f;
                if(grip!=null)grip.Align(upper,lower,hand,contact,upper.position-actor.right*4-Vector3.up*2,true,true,Vector3.up);
                else QuestRowboat.SolveArm(upper,lower,hand,contact,upper.position-actor.right*4-Vector3.up*2);
            }
            var palm=grip!=null?hand.TransformPoint(grip.leftCenter):hand.position;
            shield.SetPositionAndRotation(palm+actor.forward*.8f,Quaternion.LookRotation(actor.forward));
        }
        void OnDestroy(){if(Instance==this)Instance=null;if(wood!=null)Destroy(wood);if(rim!=null)Destroy(rim);if(metal!=null)Destroy(metal);}
    }
    [DefaultExecutionOrder(780)]
    public sealed class PlayerShieldPose:MonoBehaviour {public PlayerShield owner;void LateUpdate(){owner?.Pose();}}
}
