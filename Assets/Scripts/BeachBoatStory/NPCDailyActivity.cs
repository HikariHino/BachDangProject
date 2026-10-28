using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Reserved local workplaces and visible chores. No teleporting or animation-only walking.</summary>
    [DefaultExecutionOrder(650)]
    public sealed class NPCDailyActivity : MonoBehaviour
    {
        public enum Task { Trade, Garden, Laundry, Water, Fish, Carry, Maintain }
        sealed class Site {public Transform location;public Task task;public NPCDailyActivity owner;}
        static readonly List<Site> sites=new List<Site>();static UnityEngine.SceneManagement.Scene workScene;
        NPCWander walker;Animator rig;NPCAmbientDialogue.Role role;Site reserved;
        Transform rightUpper,rightLower,rightHand,leftUpper,leftLower,leftHand,chest;GameObject prop;
        Material material,iron;float phase,lastCycle;Vector3 home;bool initialized;
        public Task CurrentTask {get;private set;}
        public bool Working {get;private set;}
        public int WorkCycles {get;private set;}
        public static int ReservedSites=>sites.Count(s=>s.owner!=null);
        public static int WorksiteCount=>sites.Count;
        public bool HasWorksite=>reserved!=null&&reserved.location!=null;
        public Vector3 WorkPosition {get;private set;}
        public bool ArrivedAtWorksite=>HasWorksite&&QuestRowboat.FlatDistance(transform.position,WorkPosition)<Mathf.Max(2,EnemyDeckGuard.RigHeight(rig)*.12f);
        public string WorksiteName=>reserved?.location!=null?reserved.location.name:"";
        public static System.Collections.IEnumerator AttachFixedWorkers()
        {
            yield return null;yield return null;
            foreach(var talker in FindObjectsByType<NPCAmbientDialogue>(FindObjectsSortMode.None))
            {
                if(talker.role==NPCAmbientDialogue.Role.Guard||talker.GetComponent<NPCWander>()!=null||talker.GetComponentInParent<AmbientRiverBoat>()!=null)continue;
                var animator=talker.GetComponent<Animator>();if(animator==null||!animator.isHuman||talker.GetComponentInParent<NPCQuestGiver>()!=null)continue;
                var activity=talker.GetComponent<NPCDailyActivity>()??talker.gameObject.AddComponent<NPCDailyActivity>();activity.Initialize(null,animator);
            }
        }
        public Vector3 WorkFocus=>reserved?.location!=null?reserved.location.position:transform.position+transform.forward*8;
        public void Initialize(NPCWander owner,Animator animator)
        {
            if(initialized)return;initialized=true;walker=owner;rig=animator;home=transform.position;
            role=GetComponent<NPCAmbientDialogue>()?.role??NPCAmbientDialogue.Role.Guard;
            rightUpper=rig.GetBoneTransform(HumanBodyBones.RightUpperArm);rightLower=rig.GetBoneTransform(HumanBodyBones.RightLowerArm);rightHand=rig.GetBoneTransform(HumanBodyBones.RightHand);
            leftUpper=rig.GetBoneTransform(HumanBodyBones.LeftUpperArm);leftLower=rig.GetBoneTransform(HumanBodyBones.LeftLowerArm);leftHand=rig.GetBoneTransform(HumanBodyBones.LeftHand);chest=rig.GetBoneTransform(HumanBodyBones.Chest)??rig.GetBoneTransform(HumanBodyBones.Spine);
            phase=Mathf.Abs(GetEntityId().GetHashCode()%100)*.37f;
            if(workScene!=gameObject.scene){sites.Clear();workScene=gameObject.scene;}
        }
        static string Label(Task task)=>task==Task.Garden?"Đang chăm và tưới rau":task==Task.Laundry?"Đang phơi và gấp vải":task==Task.Water?"Đang lấy nước":task==Task.Trade?"Đang xếp hàng, trao đổi ở chợ":task==Task.Fish?"Đang vá lưới, phân cá":task==Task.Carry?"Đang chuyển giỏ hàng":"Đang lau sửa dụng cụ";
        void DiscoverSites()
        {
            if(sites.Count>0)return;
            foreach(var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                Task? kind=t.name.StartsWith("Vuon_Rau_")?Task.Garden:t.name.StartsWith("San_Phoi_Vai_")?Task.Laundry:t.name=="Gieng_Nuoc_Chung"?Task.Water:t.name=="Hang_Hoa_Canh_Cho"?Task.Trade:t.name=="San_Lam_Nghe"?Task.Fish:t.name=="San_Bep_Va_Phoi_Thoc"?Task.Carry:null;
                if(kind.HasValue)sites.Add(new Site{location=t,task=kind.Value});
            }
        }
        public bool TryChooseWorksite(out Vector3 point,out string label)
        {
            point=home;label="";DiscoverSites();if(role==NPCAmbientDialogue.Role.Guard||walker.IsRaisedPatrol)return false;
            var preferred=role==NPCAmbientDialogue.Role.Farmer?Task.Garden:role==NPCAmbientDialogue.Role.Merchant?Task.Trade:role==NPCAmbientDialogue.Role.Fisher?Task.Fish:role==NPCAmbientDialogue.Role.Porter?Task.Carry:role==NPCAmbientDialogue.Role.Smith?Task.Fish:GetEntityId().GetHashCode()%2==0?Task.Laundry:Task.Water;
            var available=sites.Where(s=>s.location!=null&&(s.owner==null||s.owner==this)&&QuestRowboat.FlatDistance(s.location.position,home)<720).OrderBy(s=>(s.task==preferred?0:200)+QuestRowboat.FlatDistance(transform.position,s.location.position));
            foreach(var site in available)
            {
                for(int slot=0;slot<6;slot++)
                {
                    float local=site.task==Task.Garden?6:site.task==Task.Laundry?3:5;
                    var candidate=site.location.TransformPoint(new Vector3(Mathf.Sin(slot*Mathf.PI/3)*local,0,-Mathf.Cos(slot*Mathf.PI/3)*local));
                    if(NPCWander.AtGate(candidate)||!walker.TryWorkDestination(candidate,out candidate))continue;
                    site.owner=this;reserved=site;CurrentTask=site.task;WorkPosition=point=candidate;label=Label(CurrentTask);return true;
                }
            }
            return false;
        }
        public void ReleaseWorksite(){if(reserved!=null&&reserved.owner==this)reserved.owner=null;reserved=null;Working=false;if(prop!=null)prop.SetActive(false);}
        void MakeProp()
        {
            if(material==null)material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Daily work prop",color=new Color(.44f,.32f,.2f)};
            if(prop!=null){prop.SetActive(false);Destroy(prop);}
            prop=GameObject.CreatePrimitive(CurrentTask==Task.Laundry?PrimitiveType.Cube:CurrentTask==Task.Garden||CurrentTask==Task.Maintain?PrimitiveType.Cylinder:PrimitiveType.Cylinder);prop.name="Daily_"+CurrentTask;var collider=prop.GetComponent<Collider>();collider.enabled=false;Destroy(collider);prop.GetComponent<Renderer>().sharedMaterial=material;
            material.color=CurrentTask==Task.Laundry?new Color(.3f,.4f,.49f):new Color(.44f,.32f,.2f);
            if(CurrentTask==Task.Garden)
            {
                if(iron==null)iron=new Material(material){color=new Color(.22f,.24f,.24f)};
                var blade=GameObject.CreatePrimitive(PrimitiveType.Cube);blade.name="Luoi_cuoc";blade.transform.SetParent(prop.transform,false);blade.transform.localPosition=new Vector3(0,-.9f,.7f);blade.transform.localScale=new Vector3(8,.086f,6);blade.GetComponent<Renderer>().sharedMaterial=iron;blade.GetComponent<Collider>().enabled=false;Destroy(blade.GetComponent<Collider>());
            }
            if(CurrentTask==Task.Water||CurrentTask==Task.Carry)
            {
                var handle=new GameObject("Quai_gio").AddComponent<LineRenderer>();handle.transform.SetParent(prop.transform,false);handle.useWorldSpace=false;handle.sharedMaterial=material;handle.widthMultiplier=.1f;handle.positionCount=17;
                for(int i=0;i<17;i++){float angle=i/16f*Mathf.PI;handle.SetPosition(i,new Vector3(Mathf.Cos(angle)*.48f,1+Mathf.Sin(angle)*.55f,0));}
            }
            if(CurrentTask==Task.Fish)
            {
                prop.GetComponent<Renderer>().enabled=false;
                for(int i=0;i<5;i++)for(int axis=0;axis<2;axis++){var thread=new GameObject("Soi_luoi").AddComponent<LineRenderer>();thread.transform.SetParent(prop.transform,false);thread.useWorldSpace=false;thread.sharedMaterial=material;thread.widthMultiplier=.1f;thread.positionCount=2;float v=-.5f+i*.25f;thread.SetPosition(0,axis==0?new Vector3(v,-.5f,0):new Vector3(-.5f,v,0));thread.SetPosition(1,axis==0?new Vector3(v,.5f,0):new Vector3(.5f,v,0));}
            }
        }
        void LateUpdate()
        {
            if(!initialized||rig==null||!rig.enabled||!rig.isHuman||Time.timeScale<=0){Working=false;if(prop!=null)prop.SetActive(false);return;}
            bool stationary=walker==null;
            bool rest=stationary||walker.isActiveAndEnabled&&walker.IsResting&&!walker.IsSleeping;
            bool conversation=walker!=null&&walker.CurrentActivity=="Đang trò chuyện";
            bool chore=rest&&!conversation&&(ArrivedAtWorksite||role==NPCAmbientDialogue.Role.Guard||stationary);
            if(!chore){Working=false;if(prop!=null)prop.SetActive(false);return;}
            if(!Working){if(reserved==null)CurrentTask=role==NPCAmbientDialogue.Role.Guard?Task.Maintain:Task.Trade;MakeProp();Working=true;lastCycle=Time.time;}
            if(Time.time-lastCycle>3.5f){WorkCycles++;lastCycle=Time.time;}
            if(prop==null||rightHand==null||leftHand==null)return;
            prop.SetActive(true);float height=EnemyDeckGuard.RigHeight(rig);float unit=Mathf.Max(.5f,height/25);float wave=Mathf.Sin((Time.time+phase)*1.8f);
            if(reserved!=null){var facing=WorkFocus-transform.position;facing.y=0;if(facing.sqrMagnitude>1)transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(facing),Time.deltaTime*100);}
            var centre=chest.position+transform.forward*(5*unit);
            var right=centre+transform.right*2*unit;var left=centre-transform.right*2*unit;
            if(CurrentTask==Task.Garden){right+=Vector3.down*(5-wave*2)*unit;left+=Vector3.down*2*unit;prop.transform.localScale=new Vector3(.5f,7,.5f)*unit;}
            else if(CurrentTask==Task.Laundry){right+=Vector3.up*(2+wave*3)*unit;left+=Vector3.up*(2+wave*3)*unit;prop.transform.localScale=new Vector3(5,4,.15f)*unit;}
            else if(CurrentTask==Task.Water||CurrentTask==Task.Carry){right+=Vector3.down*3*unit;left+=Vector3.down*3*unit;prop.transform.localScale=new Vector3(4,2.5f,4)*unit;}
            else {right+=transform.forward*wave*unit;left-=transform.forward*wave*unit;prop.transform.localScale=(CurrentTask==Task.Fish?new Vector3(5,4,.1f):new Vector3(4,.35f,3))*unit;}
            QuestRowboat.SolveArm(rightUpper,rightLower,rightHand,right,rightUpper.position+transform.right*4*unit-Vector3.up*2*unit);
            QuestRowboat.SolveArm(leftUpper,leftLower,leftHand,left,leftUpper.position-transform.right*4*unit-Vector3.up*2*unit);
            prop.transform.SetPositionAndRotation((right+left)*.5f-Vector3.up*(CurrentTask==Task.Garden?5:CurrentTask==Task.Water||CurrentTask==Task.Carry?3.4f:CurrentTask==Task.Laundry?2:0)*unit,transform.rotation*Quaternion.Euler(CurrentTask==Task.Garden?25:0,0,CurrentTask==Task.Garden?25:0));
        }
        void OnDisable(){ReleaseWorksite();}
        void OnDestroy(){ReleaseWorksite();if(prop!=null)Destroy(prop);if(material!=null)Destroy(material);if(iron!=null)Destroy(iron);}
    }
}
