using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Third-person trades. Progress requires carrying actual goods between reachable work points.</summary>
    [DefaultExecutionOrder(800)]
    public sealed class VillagePhysicalWork : MonoBehaviour
    {
        public enum Step { Take, CarryToBench, Saw, Tie, FillRice, LiftRice, TakeIngredient, PlaceIngredient, Wrap, Deliver }
        public Step Current {get;private set;}
        public float ActionProgress {get;private set;}
        public float RiceFill {get;private set;}
        public int Knots {get;private set;}
        public int Ingredients {get;private set;}
        public bool Carrying=>held!=null&&held.activeSelf;
        public const float Reach=9;
        public static VillagePhysicalWork Active=>VillageWorkStall.Active?.Physical;
        public static bool AllowsCamera=>Active!=null&&Time.timeScale>0&&!BachDangFrontEnd.IsBlocking&&!CampaignJournalUI.IsOpen;
        public static bool AllowsMovement=>AllowsCamera&&VillageWorkStall.Active.Phase==VillageWorkStall.WorkPhase.Playing&&!Active.busy;
        public Vector3 RequiredApproach=>World(Current==Step.Deliver?new Vector3(25,0,-24):Current==Step.Take||Current==Step.TakeIngredient?new Vector3(-25,0,-24):
            Current==Step.Tie?new Vector3(-10+Knots*10,0,-14):new Vector3(0,0,-14));
        public Vector3 TargetPosition=>World(Current==Step.Deliver?new Vector3(25,7,-14):Current==Step.TakeIngredient?new Vector3(-28+Ingredients*3,9,-18):Current==Step.Take?new Vector3(-25,9,-14):
            Current==Step.Tie?new Vector3(-10+Knots*10,11,-8):Current==Step.FillRice?new Vector3(6,12.6f,-8):new Vector3(0,11,-8));
        public string Hint
        {
            get
            {
                switch(Current)
                {
                    case Step.Take:return "E • "+(stall.kind==VillageWorkStall.WorkKind.Timber?"Lấy một khúc gỗ":stall.kind==VillageWorkStall.WorkKind.Rope?"Lấy cuộn dây":"Lấy bao gạo rỗng");
                    case Step.CarryToBench:return "Mang tới bàn • E đặt xuống";
                    case Step.Saw:return "Giữ chuột trái • Cưa theo rãnh trên gỗ";
                    case Step.Tie:return $"Tới nút đang sáng • Giữ chuột trái siết dây {Knots}/3";
                    case Step.FillRice:return "Giữ chuột trái đong gạo • Nhả khi gạo tới VẠCH XANH • E nhấc bao";
                    case Step.LiftRice:return "E • Nhấc bao gạo đã đủ";
                    case Step.TakeIngredient:return "E • Lấy "+new[]{"nắm gạo đã gói","cá khô","gói muối"}[Mathf.Min(2,Ingredients)];
                    case Step.PlaceIngredient:return "Mang tới khăn trên bàn • E đặt nguyên liệu";
                    case Step.Wrap:return "Giữ chuột trái • Gấp khăn và buộc gói lương";
                    default:return "Mang tới giá giao hàng • E đặt vật tư";
                }
            }
        }
        VillageWorkStall stall;Transform player;Animator rig;Transform[] arms;Quaternion[] armRest;Transform rightHand,leftHand,chest;
        GameObject props,held,benchLog,bag,grain,blade,parcel,marker,foodTray;
        LineRenderer rope,pour;TextMeshPro label;Material wood,fibre,cloth,rice,fish,salt,gold,green,red;
        readonly List<Material> owned=new List<Material>();readonly List<GameObject> hidden=new List<GameObject>();
        readonly List<GameObject> placed=new List<GameObject>();readonly List<GameObject> knots=new List<GameObject>();
        readonly List<Transform> flaps=new List<Transform>();
        readonly List<(Renderer renderer,bool hidden)> fabrics=new List<(Renderer,bool)>();
        readonly List<GameObject> bundleLogs=new List<GameObject>();
        bool busy;float actionUntil,nextSound;Vector3 origin;Quaternion rotation;
        Camera_Script camera;Vector3 cameraSettings;float cameraElevation;
        AnimatorCullingMode previousCulling;SkinnedMeshRenderer[] playerMeshes;bool[] previousOffscreen;
        Vector3 World(Vector3 local){var p=origin+rotation*local;return p;}
        Material Make(string name,Color color)
        {var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,color=color};m.SetFloat("_Smoothness",.12f);owned.Add(m);return m;}
        GameObject Shape(string name,PrimitiveType type,Vector3 p,Vector3 size,Material material,bool solid=false,Transform parent=null)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent??props.transform,false);go.transform.localPosition=p;go.transform.localScale=size;
            go.GetComponent<Renderer>().sharedMaterial=material;
            if(!solid){var collider=go.GetComponent<Collider>();collider.enabled=false;Destroy(collider);}return go;
        }
        LineRenderer Line(string name,Material material,float width)
        {var go=new GameObject(name);go.transform.SetParent(props.transform,false);var l=go.AddComponent<LineRenderer>();l.sharedMaterial=material;l.widthMultiplier=width;l.useWorldSpace=true;l.generateLightingData=true;l.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;return l;}
        public void Begin(VillageWorkStall owner)
        {
            Finish();stall=owner;player=BeachBoatQuestSession.Instance.player.transform;rig=player.GetComponentInChildren<Animator>();
            previousCulling=rig.cullingMode;rig.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            playerMeshes=player.GetComponentsInChildren<SkinnedMeshRenderer>();previousOffscreen=playerMeshes.Select(r=>r.updateWhenOffscreen).ToArray();
            foreach(var renderer in playerMeshes)renderer.updateWhenOffscreen=true;
            origin=stall.transform.position;rotation=Quaternion.Euler(0,stall.transform.eulerAngles.y,0);
            props=new GameObject("Village_3D_Work_"+stall.kind);props.transform.SetPositionAndRotation(origin,rotation);
            foreach(var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))if(r.name.StartsWith("Vai_Phoi_")&&QuestRowboat.FlatDistance(r.bounds.center,origin)<120)fabrics.Add((r,r.forceRenderingOff));
            foreach(Transform child in stall.transform)
                if(child.gameObject.activeSelf&&child.name!="Mai_Che_Mo_Mat_Truoc"&&child.name!="Nguoi_Ban_Hang"&&child.name!="Ten_Sap"&&child!=stall.interactionPoint)
                {hidden.Add(child.gameObject);child.gameObject.SetActive(false);}
            wood=Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault(m=>m.name.Contains("Courtyard_Weathered_Wood")&&m.mainTexture!=null)??Make("Trade wood",new Color(.32f,.20f,.1f));
            fibre=Make("Hemp",new Color(.57f,.40f,.21f));cloth=Make("Undyed linen",new Color(.62f,.54f,.36f));rice=Make("Rice",new Color(.91f,.84f,.61f));
            fish=Make("Dried fish",new Color(.39f,.23f,.1f));salt=Make("Salt",new Color(.88f,.9f,.85f));gold=Make("Work target",new Color(1,.65f,.12f));green=Make("Correct fill",new Color(.16f,.72f,.35f));red=Make("Excess fill",new Color(.8f,.14f,.06f));
            Shape("Solid_workbench",PrimitiveType.Cube,new Vector3(0,8,-3),new Vector3(32,2,13),wood,true);
            foreach(int x in new[]{-13,13})foreach(int z in new[]{-7,1})Shape("Table_leg",PrimitiveType.Cube,new Vector3(x,3.5f,z),new Vector3(2,7,2),wood,true);
            Shape("Delivery_crate",PrimitiveType.Cube,new Vector3(25,3,-14),new Vector3(10,6,12),wood,true);
            Shape("Stock_crate",PrimitiveType.Cube,new Vector3(-25,3,-14),new Vector3(10,6,12),wood,true);
            if(stall.kind==VillageWorkStall.WorkKind.Timber)
                for(int i=0;i<3;i++){var log=Shape("Uncut_stock",PrimitiveType.Cylinder,new Vector3(-25,7+i*1.8f,-14),new Vector3(2,7,2),wood);log.transform.localRotation=Quaternion.Euler(90,0,0);}
            else if(stall.kind==VillageWorkStall.WorkKind.Rope)
            {
                for(int i=0;i<4;i++){var log=Shape("Bundle_to_tie",PrimitiveType.Cylinder,new Vector3(0,10+i/2*1.8f,-8+(i%2)*2-1),new Vector3(2,12,2),wood);log.transform.localRotation=Quaternion.Euler(0,0,90);bundleLogs.Add(log);}
                var coil=Line("Stock_rope",fibre,.55f);coil.positionCount=65;for(int i=0;i<65;i++){float a=i*.32f;coil.SetPosition(i,World(new Vector3(-25+Mathf.Cos(a)*(2+i*.025f),7+i*.018f,-14+Mathf.Sin(a)*(2+i*.025f))));}
            }
            else if(stall.kind==VillageWorkStall.WorkKind.Rice)
            {
                Shape("Grain_basket",PrimitiveType.Cylinder,new Vector3(-9,10,-3),new Vector3(9,2,9),fibre);
                for(int i=0;i<28;i++)Shape("Grain_in_basket",PrimitiveType.Sphere,new Vector3(-9+Mathf.Sin(i*2.4f)*3,12+(i%3)*.15f,-3+Mathf.Cos(i*2.4f)*3),new Vector3(.7f,.35f,1),rice);
            }
            else
            {
                for(int i=0;i<3;i++)Shape("Ingredient_tray_"+i,PrimitiveType.Cylinder,new Vector3(-28+i*3,6.7f,-18),new Vector3(3,.5f,3.5f),fibre);
                Shape("Rice_portions",PrimitiveType.Sphere,new Vector3(-28,7.6f,-18),new Vector3(5,1.6f,3),rice);
                Shape("Dried_fish",PrimitiveType.Sphere,new Vector3(-25,7.5f,-18),new Vector3(6,.6f,2),fish);
                Shape("Salt_packet",PrimitiveType.Cube,new Vector3(-22,7.7f,-18),new Vector3(3,1.6f,3),salt);
            }
            camera=BeachBoatQuestSession.Instance.storyCamera.GetComponent<Camera_Script>();cameraSettings=new Vector3(camera.distance,camera.heightOffset,camera.rightOffset);
            cameraElevation=camera.Elevation;camera.distance=42;camera.heightOffset=15;camera.rightOffset=12;camera.SetElevation(28);
            arms=new[]{rig.GetBoneTransform(HumanBodyBones.RightUpperArm),rig.GetBoneTransform(HumanBodyBones.RightLowerArm),rig.GetBoneTransform(HumanBodyBones.LeftUpperArm),rig.GetBoneTransform(HumanBodyBones.LeftLowerArm)};
            rightHand=rig.GetBoneTransform(HumanBodyBones.RightHand);leftHand=rig.GetBoneTransform(HumanBodyBones.LeftHand);chest=rig.GetBoneTransform(HumanBodyBones.Chest)??rig.GetBoneTransform(HumanBodyBones.Spine);
            armRest=arms.Select(a=>a.localRotation).ToArray();
            marker=new GameObject("Active_work_target");marker.transform.SetParent(props.transform,false);var ring=marker.AddComponent<LineRenderer>();ring.sharedMaterial=gold;ring.useWorldSpace=false;ring.widthMultiplier=.35f;ring.positionCount=33;
            for(int i=0;i<33;i++){float a=i/32f*Mathf.PI*2;ring.SetPosition(i,new Vector3(Mathf.Cos(a)*4,0,Mathf.Sin(a)*4));}
            label=new GameObject("Context_prompt").AddComponent<TextMeshPro>();label.transform.SetParent(props.transform,false);StoryFont.Apply(label);label.fontSize=3.4f;label.alignment=TextAlignmentOptions.Center;label.rectTransform.sizeDelta=new Vector2(32,8);label.color=new Color(1,.82f,.37f);
            rope=Line("Rope_pulled_around_bundle",fibre,.5f);pour=Line("Rice_pour",rice,.3f);pour.enabled=false;
            NextOrder();Physics.SyncTransforms();
        }
        void ClearOrder()
        {
            foreach(var go in new[]{held,benchLog,bag,grain,blade,parcel,foodTray})if(go!=null)Destroy(go);
            foreach(var go in placed)if(go!=null)Destroy(go);placed.Clear();foreach(var go in knots)if(go!=null)Destroy(go);knots.Clear();
            held=benchLog=bag=grain=blade=parcel=foodTray=null;flaps.Clear();ActionProgress=RiceFill=0;Knots=Ingredients=0;busy=false;
            if(rope!=null)rope.positionCount=0;if(pour!=null)pour.enabled=false;
        }
        public void NextOrder(){ClearOrder();Current=stall.kind==VillageWorkStall.WorkKind.Provisions?Step.TakeIngredient:Step.Take;}
        void Carry(string name,Material material,Vector3 size)
        {
            if(held!=null)Destroy(held);
            if(name=="Carried_rope")
            {
                held=new GameObject(name);held.transform.SetParent(props.transform,false);var coil=held.AddComponent<LineRenderer>();coil.sharedMaterial=fibre;coil.useWorldSpace=false;coil.widthMultiplier=.5f;coil.positionCount=80;
                for(int i=0;i<80;i++){float a=i*.3f;coil.SetPosition(i,new Vector3(Mathf.Cos(a)*2.3f,Mathf.Sin(a)*2.3f,i*.02f));}return;
            }
            if(name=="Bound_timber_bundle")
            {
                held=new GameObject(name);held.transform.SetParent(props.transform,false);
                for(int i=0;i<3;i++){var log=Shape("Bound_log",PrimitiveType.Cylinder,new Vector3(0,i==2?1.5f:0,i==0?-1:1),new Vector3(2,6,2),wood,false,held.transform);log.transform.localRotation=Quaternion.Euler(0,0,90);}
                foreach(int x in new[]{-4,4}){var go=new GameObject("Binding");go.transform.SetParent(held.transform,false);var ring=go.AddComponent<LineRenderer>();ring.sharedMaterial=fibre;ring.useWorldSpace=false;ring.widthMultiplier=.4f;ring.positionCount=33;for(int i=0;i<33;i++){float a=i/32f*Mathf.PI*2;ring.SetPosition(i,new Vector3(x,.7f+Mathf.Cos(a)*2,Mathf.Sin(a)*2));}}return;
            }
            var type=stall.kind==VillageWorkStall.WorkKind.Timber||name=="Full_rice_sack"?PrimitiveType.Cylinder:name=="Wrapped_rations"?PrimitiveType.Sphere:PrimitiveType.Cube;
            if(name=="Full_rice_sack")size.y*=.5f;held=Shape(name,type,Vector3.zero,size,material);
        }
        bool Near()=>player!=null&&QuestRowboat.FlatDistance(player.position,RequiredApproach)<=Reach&&Mathf.Abs(player.position.y-RequiredApproach.y)<18;
        public bool CanInteract=>stall!=null&&VillageWorkStall.Active==stall&&stall.Phase==VillageWorkStall.WorkPhase.Playing&&Time.timeScale>0&&!BachDangFrontEnd.IsBlocking&&Near();
        public bool Interact()
        {
            if(!CanInteract)return false;
            switch(Current)
            {
                case Step.Take:
                    Carry(stall.kind==VillageWorkStall.WorkKind.Timber?"Carried_log":stall.kind==VillageWorkStall.WorkKind.Rope?"Carried_rope":"Empty_sack",stall.kind==VillageWorkStall.WorkKind.Timber?wood:fibre,stall.kind==VillageWorkStall.WorkKind.Timber?new Vector3(2.5f,7,2.5f):new Vector3(5,3,4));Current=Step.CarryToBench;break;
                case Step.CarryToBench:
                    Destroy(held);held=null;
                    if(stall.kind==VillageWorkStall.WorkKind.Timber)
                    {
                        benchLog=Shape("Log_on_saw_bench",PrimitiveType.Cylinder,new Vector3(0,11,-8),new Vector3(3,10,3),wood);benchLog.transform.localRotation=Quaternion.Euler(0,0,90);
                        blade=Shape("Hand_saw",PrimitiveType.Cube,new Vector3(0,14,-8),new Vector3(.35f,5,7),Make("Saw iron",new Color(.34f,.37f,.37f)));Current=Step.Saw;
                    }
                    else if(stall.kind==VillageWorkStall.WorkKind.Rope)Current=Step.Tie;
                    else
                    {
                        bag=Shape("Sack_at_fill_station",PrimitiveType.Cylinder,new Vector3(6,11.5f,-8),new Vector3(6,2.5f,6),cloth);
                        grain=Shape("Visible_rice_level",PrimitiveType.Cylinder,new Vector3(6,10,-8),new Vector3(5.4f,.1f,5.4f),rice);
                        var band=Line("Green_fill_band",green,.5f);band.positionCount=33;for(int i=0;i<33;i++){float a=i/32f*Mathf.PI*2;band.SetPosition(i,World(new Vector3(6+Mathf.Cos(a)*3.2f,12.6f,-8+Mathf.Sin(a)*3.2f)));}placed.Add(band.gameObject);Current=Step.FillRice;
                    }
                    break;
                case Step.FillRice:
                    if(RiceFill>=.65f&&RiceFill<=.85f){Carry("Full_rice_sack",cloth,new Vector3(5,6,5));bag.SetActive(false);grain.SetActive(false);foreach(var go in placed)if(go!=null)go.SetActive(false);Current=Step.Deliver;pour.enabled=false;}
                    else if(RiceFill>.85f){RiceFill=0;ActionProgress=0;stall.PhysicalMistake("Bao bị đầy quá vạch. Đã đổ lại để đong tiếp.");}
                    return RiceFill>=.65f||RiceFill==0;
                case Step.TakeIngredient:
                    Carry("Carried_ingredient_"+Ingredients,new[]{rice,fish,salt}[Ingredients],Ingredients==1?new Vector3(6,.8f,2):new Vector3(3,2,3));Current=Step.PlaceIngredient;break;
                case Step.PlaceIngredient:
                    if(parcel==null)
                    {
                        parcel=new GameObject("Packing_cloth");parcel.transform.SetParent(props.transform,false);parcel.transform.localPosition=new Vector3(0,9.2f,-8);
                        Shape("Cloth_base",PrimitiveType.Cube,Vector3.zero,new Vector3(12,.2f,8),cloth,false,parcel.transform);
                        for(int i=0;i<4;i++){var pivot=new GameObject("Fold_"+i).transform;pivot.SetParent(parcel.transform,false);pivot.localPosition=i<2?new Vector3(i==0?-6:6,0,0):new Vector3(0,0,i==2?-4:4);Shape("Cloth_flap",PrimitiveType.Cube,i<2?new Vector3(i==0?-2:2,0,0):new Vector3(0,0,i==2?-2:2),i<2?new Vector3(4,.2f,8):new Vector3(12,.2f,4),cloth,false,pivot);flaps.Add(pivot);}
                    }
                    Destroy(held);held=null;placed.Add(Shape("Ingredient_on_cloth_"+Ingredients,Ingredients==1?PrimitiveType.Sphere:PrimitiveType.Cube,new Vector3(-4+Ingredients*4,10,-8),Ingredients==1?new Vector3(6,1,2):new Vector3(3,1.5f,3),new[]{rice,fish,salt}[Ingredients]));
                    Ingredients++;Current=Ingredients==3?Step.Wrap:Step.TakeIngredient;break;
                case Step.Deliver:
                    if(!Carrying)return false;var goods=held;held=null;goods.name="Delivered_goods_"+stall.Progress;goods.transform.SetParent(props.transform,false);goods.transform.localPosition=new Vector3(25,6+stall.Progress*4,-14);goods.transform.localRotation=stall.kind==VillageWorkStall.WorkKind.Timber?Quaternion.Euler(0,0,90):Quaternion.identity;
                    var renderers=goods.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);goods.transform.position+=Vector3.up*(World(new Vector3(25,6+stall.Progress*4,-14)).y-bounds.min.y);
                    stall.CompletePhysicalOrder();break;
                default:return false;
            }
            BeachBoatSoundscape.Instance?.PlayInteraction();return true;
        }
        public bool Work(float seconds)
        {
            if(!CanInteract||seconds<=0||float.IsNaN(seconds)||float.IsInfinity(seconds))return false;
            float dt=Mathf.Min(seconds,.1f);float duration=Current==Step.Saw?3.5f:Current==Step.Tie?1.8f:Current==Step.Wrap?2.5f:0;
            if(Current==Step.FillRice)
            {
                RiceFill=Mathf.Min(1,RiceFill+dt*.18f);ActionProgress=RiceFill;busy=true;actionUntil=Time.time+.12f;return true;
            }
            if(duration==0)return false;
            busy=true;actionUntil=Time.time+.12f;ActionProgress=Mathf.Clamp01(ActionProgress+dt/duration);
            if(Time.time>=nextSound){nextSound=Time.time+.8f;BeachBoatSoundscape.Instance?.PlayInteraction();}
            if(ActionProgress<1)return true;ActionProgress=0;busy=false;
            if(Current==Step.Tie)
            {
                var knot=Shape("Tightened_knot_"+Knots,PrimitiveType.Sphere,new Vector3(-10+Knots*10,14.3f,-8),new Vector3(1.6f,1,2),fibre);knots.Add(knot);
                var loop=Line("Tied_binding_"+Knots,fibre,.5f);loop.positionCount=33;for(int i=0;i<33;i++){float a=i/32f*Mathf.PI*2;loop.SetPosition(i,World(new Vector3(-10+Knots*10,11.3f+Mathf.Cos(a)*3,-8+Mathf.Sin(a)*2.4f)));}placed.Add(loop.gameObject);Knots++;
                if(Knots==3){foreach(var go in bundleLogs)go.SetActive(false);foreach(var go in knots)go.SetActive(false);foreach(var go in placed)go.SetActive(false);Carry("Bound_timber_bundle",wood,new Vector3(12,4,4));Current=Step.Deliver;}
            }
            else if(Current==Step.Saw){if(benchLog!=null)benchLog.transform.localScale=new Vector3(3,5,3);if(blade!=null)blade.SetActive(false);Carry("Sawn_timber",wood,new Vector3(2.5f,5,2.5f));Current=Step.Deliver;}
            else {foreach(var go in placed)if(go!=null)go.SetActive(false);if(parcel!=null)parcel.SetActive(false);Carry("Wrapped_rations",cloth,new Vector3(7,4,5));Current=Step.Deliver;}
            return true;
        }
        public void Tick()
        {
            if(stall==null||props==null)return;
            if(QuestRowboat.FlatDistance(player.position,origin)>130){stall.Close();return;}
            if(Input.GetKeyDown(KeyCode.E))Interact();
            if(Input.GetMouseButton(0))Work(Time.deltaTime);
            if(Time.time>actionUntil)busy=false;
        }
        void LateUpdate()
        {
            if(stall==null||props==null)return;
            if(Camera.main!=null)
                foreach(var item in fabrics)if(item.renderer!=null)
                {var cameraPosition=Camera.main.transform.position;bool Blocks(Vector3 target){var delta=target-cameraPosition;return item.renderer.bounds.IntersectRay(new Ray(cameraPosition,delta.normalized),out float distance)&&distance<delta.magnitude;}item.renderer.forceRenderingOff=item.hidden||(item.renderer.bounds.ClosestPoint(cameraPosition)-cameraPosition).sqrMagnitude<400||Blocks(player.position+Vector3.up*10)||Blocks(TargetPosition);}
            marker.transform.position=TargetPosition+Vector3.up*.3f;
            label.transform.position=TargetPosition+Vector3.up*7;label.text=Current==Step.FillRice?(RiceFill<.65f?"THIẾU":RiceFill<=.85f?"ĐỦ • NHẢ CHUỘT, BẤM E":"DƯ • E ĐỔ LẠI"):Near()?(Current==Step.Saw||Current==Step.Tie||Current==Step.Wrap?"GIỮ CHUỘT TRÁI":"[E] TƯƠNG TÁC"):"ĐẾN ĐÂY";
            label.color=Current==Step.FillRice&&RiceFill>=.65f?(RiceFill<=.85f?new Color(.3f,1,.5f):new Color(1,.3f,.2f)):new Color(1,.82f,.37f);
            if(Camera.main!=null)label.transform.rotation=Camera.main.transform.rotation;
            if(held!=null){held.transform.position=chest.position+player.forward*4.5f+player.right*(stall.kind==VillageWorkStall.WorkKind.Timber?2:0);held.transform.rotation=player.rotation*(stall.kind==VillageWorkStall.WorkKind.Timber?Quaternion.Euler(0,0,90):Quaternion.identity);}
            if(busy){var facing=TargetPosition-player.position;facing.y=0;var body=player.GetComponent<Rigidbody>();if(body!=null&&facing.sqrMagnitude>.1f)body.MoveRotation(Quaternion.Slerp(body.rotation,Quaternion.LookRotation(facing),1-Mathf.Exp(-12*Time.deltaTime)));}
            if(Carrying||busy)
            {
                float stroke=busy?Mathf.Sin(Time.time*11)*1.3f:0;
                var grip=Carrying?held.transform.position:World(new Vector3(Current==Step.Tie?-10+Knots*10:0,12,-8));
                var right=grip+player.right*2+player.forward*stroke;var left=grip-player.right*2;
                QuestRowboat.SolveArm(arms[0],arms[1],rightHand,right,arms[0].position+player.right*4-Vector3.up*3);
                QuestRowboat.SolveArm(arms[2],arms[3],leftHand,left,arms[2].position-player.right*4-Vector3.up*3);
            }
            if(blade!=null&&Current==Step.Saw)blade.transform.localPosition=new Vector3(0,14-ActionProgress*3,-8+(busy?Mathf.Sin(Time.time*11)*2:0));
            if(grain!=null){grain.transform.localScale=new Vector3(5.4f,Mathf.Max(.05f,RiceFill*2.5f),5.4f);grain.transform.localPosition=new Vector3(6,9+RiceFill*2.5f,-8);grain.GetComponent<Renderer>().sharedMaterial=RiceFill>=.65f&&RiceFill<=.85f?green:RiceFill>.85f?red:rice;}
            if(pour!=null){pour.enabled=Current==Step.FillRice&&busy;if(pour.enabled){pour.positionCount=9;for(int i=0;i<9;i++){float t=i/8f;pour.SetPosition(i,World(Vector3.Lerp(new Vector3(-9,13,-8),new Vector3(6,12,-8),t)+Vector3.up*Mathf.Sin(t*Mathf.PI)*7));}}}
            if(rope!=null&&stall.kind==VillageWorkStall.WorkKind.Rope)
            {rope.positionCount=Current==Step.Tie?33:0;for(int i=0;i<rope.positionCount;i++){float a=i/32f*Mathf.PI*2;float radius=1.25f-ActionProgress*.25f;rope.SetPosition(i,World(new Vector3(-10+Knots*10,11.3f+Mathf.Cos(a)*3*radius,-8+Mathf.Sin(a)*2.4f*radius)));}}
            if(parcel!=null&&Current==Step.Wrap)for(int i=0;i<flaps.Count;i++){float a=Mathf.SmoothStep(0,1,Mathf.Clamp01(ActionProgress*4-i))*130;flaps[i].localRotation=i<2?Quaternion.Euler(0,0,i==0?-a:a):Quaternion.Euler(i==2?a:-a,0,0);}
        }
        public void Finish()
        {
            busy=false;if(props!=null){props.SetActive(false);Destroy(props);}props=null;
            foreach(var go in hidden)if(go!=null)go.SetActive(true);
            // Restoring the taller original counter must not enclose a player who
            // climbed onto the temporary workbench during an attempt.
            if(player!=null)
            {
                Physics.SyncTransforms();var capsule=player.GetComponent<CapsuleCollider>();var body=player.GetComponent<Rigidbody>();
                if(capsule!=null)for(int pass=0;pass<3;pass++)foreach(var go in hidden)if(go!=null)foreach(var other in go.GetComponentsInChildren<Collider>())
                    if(other.enabled&&!other.isTrigger&&Physics.ComputePenetration(capsule,capsule.transform.position,capsule.transform.rotation,other,other.transform.position,other.transform.rotation,out var direction,out float depth))
                    {player.position+=direction*(depth+.1f);if(body!=null)body.position=player.position;Physics.SyncTransforms();}
            }
            hidden.Clear();
            foreach(var item in fabrics)if(item.renderer!=null)item.renderer.forceRenderingOff=item.hidden;fabrics.Clear();bundleLogs.Clear();
            foreach(var m in owned)if(m!=null)Destroy(m);owned.Clear();placed.Clear();knots.Clear();
            if(camera!=null){camera.distance=cameraSettings.x;camera.heightOffset=cameraSettings.y;camera.rightOffset=cameraSettings.z;camera.SetElevation(cameraElevation);}camera=null;
            if(arms!=null&&armRest!=null)for(int i=0;i<arms.Length;i++)if(arms[i]!=null)arms[i].localRotation=armRest[i];
            if(rig!=null&&playerMeshes!=null){rig.cullingMode=previousCulling;for(int i=0;i<playerMeshes.Length;i++)if(playerMeshes[i]!=null)playerMeshes[i].updateWhenOffscreen=previousOffscreen[i];}playerMeshes=null;
            stall=null;held=null;
        }
        void OnDisable(){Finish();}
    }
}
