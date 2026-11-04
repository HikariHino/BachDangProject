using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BeachBoatStory
{
    /// <summary>A visible, swept collision test against the actual sample timber, before the field montage.</summary>
    [DefaultExecutionOrder(950)]
    public sealed class StakeProofTrial:MonoBehaviour
    {
        public static StakeProofTrial Active {get;private set;}
        public bool Released {get;private set;}public bool Completed {get;private set;}public bool Contacted {get;private set;}
        public float Travelled {get;private set;}
        Transform raft,post;Collider timber;GameObject root,hud;TMP_Text caption;Camera_Script camera;float elevation,settled;Vector3 flow;
        public void Begin(Transform sample)
        {
            Active=this;post=sample;timber=post.GetComponent<Collider>();camera=BeachBoatQuestSession.Instance.storyCamera.GetComponent<Camera_Script>();elevation=camera.Elevation;CampaignFieldCalls.Say("raft_ready");
            root=new GameObject("Be_thu_coc_3D");root.transform.SetParent(transform,false);raft=new GameObject("Be_thu_troi").transform;raft.SetParent(root.transform,false);
            flow=Vector3.ProjectOnPlane(-sample.up,Vector3.up).normalized;if(flow.sqrMagnitude<.1f)flow=Vector3.back;
            var center=sample.position;center.y=TideSystem.CurrentWaterHeight+1;raft.SetPositionAndRotation(center-flow*66,Quaternion.LookRotation(flow));
            var material=sample.GetComponentInChildren<Renderer>().sharedMaterial;
            for(int i=0;i<5;i++){var p=GameObject.CreatePrimitive(PrimitiveType.Cylinder);p.name="Cay_go_be_thu_"+i;p.transform.SetParent(raft,false);p.transform.localPosition=Vector3.right*(i-2)*1.8f;p.transform.localRotation=Quaternion.Euler(90,0,0);p.transform.localScale=new Vector3(1.7f,7,1.7f);p.GetComponent<Collider>().enabled=false;Destroy(p.GetComponent<Collider>());p.GetComponent<Renderer>().sharedMaterial=material;}
            hud=new GameObject("StakeProofHUD",typeof(Canvas),typeof(CanvasScaler));hud.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;hud.GetComponent<Canvas>().sortingOrder=110;var scale=hud.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1280,720);
            var card=new GameObject("ProofCard",typeof(RectTransform));card.transform.SetParent(hud.transform,false);CampaignHUDTheme.Dock(card,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,32),new Vector2(570,90));CampaignHUDTheme.Plate(card,true);
            caption=CampaignHUDTheme.Label("ProofInstructions",card.transform,new Vector2(20,-13),new Vector2(530,64),18);caption.alignment=TextAlignmentOptions.Center;
        }
        public bool ReleaseRaft()
        {if(Active!=this||Released||Completed||BachDangFrontEnd.IsBlocking||Time.timeScale<=0)return false;Released=true;BeachBoatSoundscape.Instance?.PlayInteraction();return true;}
        public void Step(float dt)
        {
            if(Active!=this||Completed||dt<=0||BachDangFrontEnd.IsBlocking)return;
            if(Contacted){settled+=dt;if(settled>=2.2f)Completed=true;return;}
            if(!Released)return;
            float length=23*dt;var from=raft.position;var to=from+flow*length;
            // Sweep the front of the real raft against the sample's solid collider.
            bool hit=false;float distance=length;
            foreach(var contact in Physics.SphereCastAll(from+flow*5,3.8f,flow,length,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                if(contact.collider==timber){hit=true;distance=Mathf.Min(distance,contact.distance);}
            if(hit)
            {raft.position=from+flow*Mathf.Max(0,distance-.15f);Contacted=true;CampaignFieldCalls.Say("raft_hit");BeachBoatSoundscape.Instance?.PlayCrewStake(post.position);}
            else {raft.position=to;Travelled+=length;if(Travelled>100){ // Keep the test recoverable if a load/terrain adjustment moved the waterline.
                var p=post.position-flow*66;p.y=post.position.y;raft.position=p;Travelled=0;
            }}
        }
        void Update(){if(Time.timeScale<=0||BachDangFrontEnd.IsBlocking)return;if(Input.GetKeyDown(KeyCode.E))ReleaseRaft();Step(Time.deltaTime);}
        void LateUpdate()
        {
            if(Active!=this||post==null)return;var focus=Vector3.Lerp(raft.position,post.position,.55f);camera.FocusHandwork(focus-flow*40+Vector3.Cross(Vector3.up,flow)*30+Vector3.up*38,focus,post);
            caption.text=Contacted?"CỌC ĐÃ GIỮ ĐƯỢC BÈ\nDân binh: Chắc rồi! Có thể dựng tiếp cả trận địa.":Released?"THỬ DÒNG CHẢY\nQuan sát bè bị chặn bởi chính chiếc cọc vừa đóng":"KIỂM TRA TRẬN ĐỊA\n[E] Ra hiệu thả bè thử • Quan sát hướng cọc chặn bè";
        }
        public void Close(){if(Active==this)Active=null;if(camera!=null){camera.ClearWorkFocus();camera.SetElevation(elevation);}if(root!=null){root.SetActive(false);Destroy(root);}if(hud!=null)Destroy(hud);Destroy(this);}
        void OnDestroy(){if(Active==this)Active=null;if(root!=null)Destroy(root);if(hud!=null)Destroy(hud);}
    }
}
