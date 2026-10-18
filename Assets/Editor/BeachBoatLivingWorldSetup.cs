using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using BeachBoatStory;

namespace BeachBoatStoryEditor
{
    public static class BeachBoatLivingWorldSetup
    {
        const string RootName="BeachBoat_LivingWorld_Routes";

        [MenuItem("BeachBoat/Setup Living Villages And Camps")]
        public static void Setup()
        {
            var scene=BeachBoatToolSafety.Begin("LivingWorld");
            var old=GameObject.Find(RootName);if(old!=null)Undo.DestroyObjectImmediate(old);
            var root=new GameObject(RootName);Undo.RegisterCreatedObjectUndo(root,"Create living-world routes");
            var terrain=Terrain.activeTerrain;if(terrain==null)throw new System.InvalidOperationException("Terrain is missing.");
            int routed=0,talkers=0;
            foreach(var animator in Object.FindObjectsByType<Animator>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(animator.gameObject.scene!=scene||!animator.isHuman)continue;
                string path=AnimationUtility.CalculateTransformPath(animator.transform,null);
                string lower=(path+"/"+animator.name).ToLowerInvariant();
                bool player=animator.CompareTag("Player")||animator.GetComponentInParent<Character_Movement>()!=null;
                bool commander=animator.GetComponentInParent<BeachBoatStory.NPCQuestGiver>()!=null;
                bool boatCrew=lower.Contains("hạm đội")||lower.Contains("ham doi")||lower.Contains("thuyen_");
                if(player||commander||boatCrew)continue;

                var life=animator.GetComponent<NPCWander>();
                var role=NPCAmbientDialogue.Role.Guard;string display="Quân sĩ";
                if(lower.Contains("danlang"))
                {
                    int index=lower.Contains("_0")?0:lower.Contains("_1")?1:2;
                    role=index==0?NPCAmbientDialogue.Role.Merchant:index==1?NPCAmbientDialogue.Role.Fisher:NPCAmbientDialogue.Role.Farmer;
                    display=index==0?"Người bán hàng":index==1?"Ngư dân":"Nông dân";
                }
                else if(lower.Contains("lookout"))display="Lính gác chòi";
                else if(lower.Contains("sitting")||lower.Contains("laying"))display="Quân sĩ nghỉ ca";

                var talk=animator.GetComponent<NPCAmbientDialogue>();
                if(talk==null)talk=Undo.AddComponent<NPCAmbientDialogue>(animator.gameObject);
                talk.displayName=display;talk.role=role;talk.dailyLife=life;talk.interactDistance=12;EditorUtility.SetDirty(talk);talkers++;
                animator.cullingMode=AnimatorCullingMode.CullUpdateTransforms;

                if(life==null||!NPCWander.IsEligible(animator))continue;
                var route=new GameObject("Tuyen_"+animator.name).transform;route.SetParent(root.transform,false);
                Vector3[] points;string[] labels;
                if(lower.Contains("danlang_thai_0"))
                {
                    points=new[]{animator.transform.position,new Vector3(-6696,0,-4768),new Vector3(-6798,0,-4714),new Vector3(-6810,0,-4673)};
                    labels=new[]{"Đang dọn sân nhà","Đang lấy củi","Đang chuyển thùng hàng","Đang bán hàng ở chợ"};
                }
                else if(lower.Contains("danlang_thai_1"))
                {
                    points=new[]{animator.transform.position,new Vector3(-6652,0,-5097),new Vector3(-6680,0,-5108),new Vector3(-6615,0,-5140)};
                    labels=new[]{"Đang vá lưới","Đang mang cá về làng","Đang phân loại cá","Đang lấy nước"};
                }
                else if(lower.Contains("danlang_thai_2"))
                {
                    points=new[]{animator.transform.position,new Vector3(-6558,0,-5421),new Vector3(-6658,0,-5435),new Vector3(-6678,0,-5338)};
                    labels=new[]{"Đang chuẩn bị ra đồng","Đang lấy nước tưới","Đang xếp nông cụ","Đang chuyển thóc về nhà"};
                }
                else if(animator.transform.position.y-terrain.SampleHeight(animator.transform.position)-terrain.transform.position.y>15)
                {
                    var p=animator.transform.position;
                    points=new[]{p,p+animator.transform.right*4,p+animator.transform.forward*4,p-animator.transform.right*3};
                    labels=new[]{"Đang quan sát cổng trại","Đang tuần tra trên bục","Đang đổi vị trí gác","Đang kiểm tra doanh trại"};
                }
                else
                {
                    var p=animator.transform.position;
                    points=new[]{p,p+new Vector3(14,0,-7),p+new Vector3(6,0,-22),p+new Vector3(-10,0,-12)};
                    labels=new[]{"Đang trực cổng","Đang tuần tra hàng rào","Đang kiểm tra kho lương","Đang trở về vị trí gác"};
                }
                var waypoints=new Transform[points.Length];
                bool raised=animator.transform.position.y-(terrain.SampleHeight(animator.transform.position)+terrain.transform.position.y)>15;
                for(int i=0;i<points.Length;i++)
                {
                    var marker=new GameObject("Diem_"+(i+1)+"_"+labels[i]).transform;marker.SetParent(route,false);
                    var p=points[i];if(!raised)p.y=terrain.SampleHeight(p)+terrain.transform.position.y+(animator.transform.position.y-(terrain.SampleHeight(animator.transform.position)+terrain.transform.position.y));
                    marker.position=p;waypoints[i]=marker;
                }
                life.waypoints=waypoints;life.activityLabels=labels;life.autoGenerateWaypoints=false;
                life.wanderRadius=raised?6:35;life.walkSpeed=lower.Contains("guard")?5.2f:4.5f;
                life.minIdleTime=4;life.maxIdleTime=12;life.sitLayChance=lower.Contains("guard")?5:22;
                EditorUtility.SetDirty(life);routed++;
            }
            BeachBoatToolSafety.Save(scene);
            Debug.Log($"Living world configured: {routed} routed workers/guards and {talkers} conversational land NPCs.",root);
        }

        public static void SetupAndExit()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity",OpenSceneMode.Single);
            Setup();BeachBoatGateFix.FixAllGates();EditorApplication.Exit(0);
        }

        public static void AuditAndExit()
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity",OpenSceneMode.Single);
            var entries=Object.FindObjectsByType<Animator>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Where(a=>a.gameObject.scene==scene&&a.isHuman)
                .Select(a=>new{a.name,path=AnimationUtility.CalculateTransformPath(a.transform,null),position=a.transform.position.ToString("F1"),ground=(Terrain.activeTerrain.SampleHeight(a.transform.position)+Terrain.activeTerrain.transform.position.y).ToString("F1"),eligible=NPCWander.IsEligible(a),wander=a.GetComponent<NPCWander>()!=null}).ToArray();
            var roots=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true))
                .Where(t=>new[]{"01_Lang_Cho_Ben_Song","02_Xom_Chai_Bo_Dong","03_Xom_Vuon_Bo_Tay","04_Trai_Tiep_Van","05_Trai_Du_Bi","06_Trai_Bo_Dong","1_DaiBanDoanh_NgoQuyen"}.Contains(t.name))
                .Select(t=>new{t.name,path=AnimationUtility.CalculateTransformPath(t,null),position=t.position.ToString("F1")}).ToArray();
            var actorPositions=Object.FindObjectsByType<Animator>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(a=>a.gameObject.scene==scene&&a.isHuman).ToArray();
            var renderers=Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(r=>r.gameObject.scene==scene&&!r.GetComponentInParent<Animator>()).ToArray();
            var nearby=actorPositions.Where(a=>NPCWander.IsEligible(a)).Select(a=>new{actor=a.name,objects=renderers.Select(r=>new{r.name,position=r.bounds.center,distance=Vector3.Distance(a.transform.position,r.bounds.center)}).Where(x=>x.distance<140).OrderBy(x=>x.distance).Take(12).Select(x=>new{x.name,position=x.position.ToString("F1"),distance=x.distance.ToString("F0")}).ToArray()}).ToArray();
            System.IO.File.WriteAllText("Logs/LivingWorldAudit.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{actors=entries,areas=roots,nearby},Newtonsoft.Json.Formatting.Indented));
            EditorApplication.Exit(0);
        }

        public static void ValidateAndExit()
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity",OpenSceneMode.Single);
            var terrain=Terrain.activeTerrain;
            var lives=Object.FindObjectsByType<NPCWander>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(x=>x.gameObject.scene==scene&&x.enabled).ToArray();
            var talks=Object.FindObjectsByType<NPCAmbientDialogue>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(x=>x.gameObject.scene==scene).ToArray();
            var gates=Object.FindObjectsByType<BeachBoatStory.CampGateTransition>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(x=>x.gameObject.scene==scene).ToArray();
            int routed=lives.Count(x=>!x.autoGenerateWaypoints&&x.waypoints!=null&&x.waypoints.Length>=4&&x.waypoints.All(w=>w!=null));
            int raisedSafe=lives.Count(x=>
            {
                float ground=terrain.SampleHeight(x.transform.position)+terrain.transform.position.y;
                if(x.transform.position.y-ground<=15)return true;
                return x.waypoints.All(w=>Mathf.Abs(w.position.y-x.transform.position.y)<1);
            });
            bool exclusions=talks.All(x=>x.GetComponentInParent<Character_Movement>()==null&&x.GetComponentInParent<BeachBoatStory.NPCQuestGiver>()==null&&!AnimationUtility.CalculateTransformPath(x.transform,null).ToLowerInvariant().Contains("thuyen_"));
            var report=new{success=lives.Length==9&&routed==9&&raisedSafe==9&&talks.Length==12&&exclusions&&gates.Length==4&&gates.All(g=>g.GetComponent<Collider>()!=null),activeDailyNPCs=lives.Length,routed,raisedRoutesSafe=raisedSafe,conversationalNPCs=talks.Length,playerCommanderAndBoatCrewsExcluded=exclusions,gates=gates.Length,gatesWithColliders=gates.Count(g=>g.GetComponent<Collider>()!=null),routeObjects=GameObject.Find(RootName)!=null?GameObject.Find(RootName).transform.childCount:0};
            System.IO.File.WriteAllText("Logs/LivingWorldValidation.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
            EditorApplication.Exit(report.success?0:1);
        }

        public static void AuditGatesAndExit()
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity",OpenSceneMode.Single);
            var renderers=Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(r=>r.gameObject.scene==scene).ToArray();
            var gates=Object.FindObjectsByType<BeachBoatStory.CampGateTransition>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(g=>g.gameObject.scene==scene)
                .Select(g=>new{name=g.name,position=g.transform.position.ToString("F1"),rotation=g.transform.eulerAngles.ToString("F1"),size=(g.GetComponent<Collider>()!=null?g.GetComponent<Collider>().bounds.size:Vector3.zero).ToString("F1"),destination=g.destinationTarget!=null?g.destinationTarget.position.ToString("F1"):"missing",nearby=renderers.Select(r=>new{name=r.name,position=r.bounds.center,distance=Vector3.Distance(g.transform.position,r.bounds.center),size=r.bounds.size}).Where(x=>x.distance<170&&(x.name.ToLowerInvariant().Contains("fence")||x.name.ToLowerInvariant().Contains("rao")||x.name.ToLowerInvariant().Contains("gate")||x.name.ToLowerInvariant().Contains("cong")||x.name.ToLowerInvariant().Contains("wall"))).OrderBy(x=>x.distance).Take(16).Select(x=>new{x.name,position=x.position.ToString("F1"),distance=x.distance.ToString("F0"),size=x.size.ToString("F1")}).ToArray()}).ToArray();
            System.IO.File.WriteAllText("Logs/GateFenceAudit.json",Newtonsoft.Json.JsonConvert.SerializeObject(gates,Newtonsoft.Json.Formatting.Indented));
            EditorApplication.Exit(0);
        }

        public static void AuditFenceSourceAndExit()
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity",OpenSceneMode.Single);
            var names=new[]{"SharedFence_1960","Visible_Story_Gate"};
            var items=Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(t=>t.gameObject.scene==scene&&names.Contains(t.name)).Select(t=>new{t.name,path=AnimationUtility.CalculateTransformPath(t,null),position=t.position.ToString("F2"),rotation=t.eulerAngles.ToString("F2"),scale=t.lossyScale.ToString("F2"),mesh=t.GetComponent<MeshFilter>()?.sharedMesh?.name,meshPath=AssetDatabase.GetAssetPath(t.GetComponent<MeshFilter>()?.sharedMesh),materials=t.GetComponent<Renderer>()?.sharedMaterials.Select(m=>m!=null?AssetDatabase.GetAssetPath(m):"null").ToArray(),bounds=t.GetComponent<Renderer>()!=null?t.GetComponent<Renderer>().bounds.size.ToString("F2"):"none"}).ToArray();
            System.IO.File.WriteAllText("Logs/FenceSourceAudit.json",Newtonsoft.Json.JsonConvert.SerializeObject(items,Newtonsoft.Json.Formatting.Indented));EditorApplication.Exit(0);
        }

        [MenuItem("BeachBoat/Embed Gates In Existing Perimeters")]
        public static void EmbedGatesInExistingPerimeters()
        {
            var scene=BeachBoatToolSafety.Begin("CampGateFence");var terrain=Terrain.activeTerrain;
            var campGate=GameObject.Find("CampToVillage")?.GetComponent<BeachBoatStory.CampGateTransition>();
            var riverGate=GameObject.Find("CampToRiver")?.GetComponent<BeachBoatStory.CampGateTransition>();
            var old=GameObject.Find("CampGate_FenceConnection");if(old!=null)Undo.DestroyObjectImmediate(old);
            if(terrain==null||campGate==null||riverGate==null)throw new System.InvalidOperationException("Camp gates or terrain are missing.");

            var allRenderers=Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(r=>r.gameObject.scene==scene).ToArray();
            var fences=allRenderers.Where(IsExistingFence).ToArray();
            if(fences.Length==0)throw new System.InvalidOperationException("No existing outer fence could be found.");
            EmbedGate(campGate,new[]{campGate,riverGate},fences,allRenderers,terrain,false);

            var villageGate=GameObject.Find("VillageToCamp")?.GetComponent<BeachBoatStory.CampGateTransition>();
            if(villageGate!=null)EmbedGate(villageGate,new[]{villageGate},fences,allRenderers,terrain,true);
            BeachBoatToolSafety.Save(scene);Debug.Log("Both teleport gates are embedded in existing perimeter fences; the temporary 16-panel fence was removed.",campGate);
        }

        static bool IsExistingFence(Renderer renderer)
        {
            if(renderer==null||renderer.GetComponentInParent<BeachBoatStory.CampGateTransition>()!=null)return false;
            string path=AnimationUtility.CalculateTransformPath(renderer.transform,null).ToLowerInvariant();
            var filter=renderer.GetComponent<MeshFilter>();
            string asset=filter!=null&&filter.sharedMesh!=null?AssetDatabase.GetAssetPath(filter.sharedMesh).ToLowerInvariant():"";
            return path.Contains("fence")||path.Contains("hangrao")||path.Contains("hang_rao")||path.Contains("rao_")||asset.Contains("fence")||asset.Contains("wall_panel");
        }

        static bool IsMarketObject(Renderer renderer)
        {
            string value=(renderer.name+"/"+AnimationUtility.CalculateTransformPath(renderer.transform,null)).ToLowerInvariant();
            return value.Contains("stall")||value.Contains("market")||value.Contains("sap_")||value.Contains("counter")||value.Contains("table")||value.Contains("pot")||value.Contains("chum")||value.Contains("firewood");
        }

        static void EmbedGate(BeachBoatStory.CampGateTransition owner,BeachBoatStory.CampGateTransition[] linked,Renderer[] fences,Renderer[] allRenderers,Terrain terrain,bool avoidMarket)
        {
            var visual=owner.transform.Find("Visible_Story_Gate");
            if(visual==null)throw new System.InvalidOperationException(owner.name+" has no Visible_Story_Gate.");
            var market=allRenderers.Where(IsMarketObject).ToArray();
            Vector3 origin=visual.position;
            var candidates=fences.Where(f=>f!=null&&Vector3.Distance(origin,f.bounds.center)<520).Select(f=>new
            {
                fence=f,
                marketDistance=market.Length==0?9999:market.Min(m=>Vector3.Distance(f.bounds.center,m.bounds.center)),
                distance=Vector3.Distance(origin,f.bounds.center)
            });
            var selected=candidates.OrderBy(x=>(avoidMarket&&x.marketDistance<75?2000:0)+x.distance).FirstOrDefault();
            if(selected==null)throw new System.InvalidOperationException("No perimeter fence near "+owner.name+".");

            Quaternion facing=Quaternion.Euler(0,selected.fence.transform.eulerAngles.y,0);
            Vector3 opening=selected.fence.bounds.center;opening.y=terrain.SampleHeight(opening)+terrain.transform.position.y;
            // Remove only the few authored panels occupied by the arch. The rest of the original perimeter stays untouched.
            foreach(var fence in fences.Where(f=>Vector2.Distance(new Vector2(f.bounds.center.x,f.bounds.center.z),new Vector2(opening.x,opening.z))<43).ToArray())
                Undo.DestroyObjectImmediate(fence.gameObject);

            Undo.RecordObject(visual,"Embed visible gate in perimeter");
            visual.SetPositionAndRotation(new Vector3(opening.x,visual.position.y,opening.z),facing);
            var visuals=visual.GetComponentsInChildren<Renderer>(true);
            if(visuals.Length>0)
            {
                var bounds=visuals[0].bounds;for(int i=1;i<visuals.Length;i++)bounds.Encapsulate(visuals[i].bounds);
                visual.position+=Vector3.up*(opening.y-bounds.min.y);
            }
            foreach(var gate in linked)
            {
                Undo.RecordObject(gate.transform,"Move teleport into perimeter opening");
                var children=gate.transform.Cast<Transform>().Select(t=>new{t,pos=t.position,rot=t.rotation}).ToArray();
                gate.transform.SetPositionAndRotation(opening,facing);
                foreach(var child in children)child.t.SetPositionAndRotation(child.pos,child.rot);
                var box=gate.GetComponent<BoxCollider>();if(box==null)box=Undo.AddComponent<BoxCollider>(gate.gameObject);
                Undo.RecordObject(box,"Fit gate trigger");box.isTrigger=true;box.center=Vector3.up*11;box.size=new Vector3(30,24,20);
                gate.interactDistance=18;EditorUtility.SetDirty(gate);
            }
        }

        public static void ConnectCampGateAndExit()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity",OpenSceneMode.Single);EmbedGatesInExistingPerimeters();EditorApplication.Exit(0);
        }

        public static void ValidateAndCaptureCampGateAndExit()
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity",OpenSceneMode.Single);
            var camp=GameObject.Find("CampToVillage")?.GetComponent<BeachBoatStory.CampGateTransition>();var river=GameObject.Find("CampToRiver")?.GetComponent<BeachBoatStory.CampGateTransition>();
            var root=GameObject.Find("CampGate_FenceConnection");var visual=camp!=null?camp.transform.Find("Visible_Story_Gate"):null;
            bool aligned=camp!=null&&river!=null&&visual!=null&&Vector2.Distance(new Vector2(camp.transform.position.x,camp.transform.position.z),new Vector2(visual.position.x,visual.position.z))<1&&Vector3.Distance(camp.transform.position,river.transform.position)<.1f;
            var nearbyFences=Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(r=>r.gameObject.scene==scene&&IsExistingFence(r)&&Vector3.Distance(r.bounds.center,camp.transform.position)<120).ToArray();
            bool gapClear=nearbyFences.All(r=>Vector2.Distance(new Vector2(r.bounds.center.x,r.bounds.center.z),new Vector2(camp.transform.position.x,camp.transform.position.z))>=40);
            var village=GameObject.Find("VillageToCamp")?.GetComponent<BeachBoatStory.CampGateTransition>();
            var market=Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(r=>r.gameObject.scene==scene&&IsMarketObject(r)).ToArray();
            float villageMarketClearance=village!=null&&market.Length>0?market.Min(r=>Vector3.Distance(r.bounds.center,village.transform.position)):9999;
            var report=new{success=aligned&&root==null&&gapClear&&village!=null&&villageMarketClearance>45,triggersAlignedWithVisibleGate=aligned,temporaryFenceRemoved=root==null,passageClear=gapClear,villageMarketClearance};
            System.IO.File.WriteAllText("Logs/CampGateFenceValidation.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
            if(report.success)
            {
                System.IO.Directory.CreateDirectory(".utmp/GateFence");var cameraObject=new GameObject("GateReviewCamera");var camera=cameraObject.AddComponent<Camera>();
                Vector3 tangent=visual.rotation*Vector3.forward,right=visual.rotation*Vector3.right,focus=camp.transform.position+Vector3.up*35;
                camera.transform.SetPositionAndRotation(focus+right*190-tangent*25+Vector3.up*95,Quaternion.LookRotation(focus-(focus+right*190-tangent*25+Vector3.up*95)));camera.fieldOfView=58;camera.farClipPlane=1500;
                var rt=new RenderTexture(1200,800,24);var texture=new Texture2D(1200,800,TextureFormat.RGB24,false);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1200,800),0,0);texture.Apply();System.IO.File.WriteAllBytes(".utmp/GateFence/camp-gate.png",texture.EncodeToPNG());RenderTexture.active=null;Object.DestroyImmediate(rt);Object.DestroyImmediate(texture);Object.DestroyImmediate(cameraObject);
            }
            EditorApplication.Exit(report.success?0:1);
        }

        public static void AuditOuterFencesAndBoatsAndExit()
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity",OpenSceneMode.Single);
            var camp=GameObject.Find("CampToVillage")?.GetComponent<BeachBoatStory.CampGateTransition>();var village=GameObject.Find("VillageToCamp")?.GetComponent<BeachBoatStory.CampGateTransition>();
            var renderers=Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(r=>r.gameObject.scene==scene).ToArray();
            object Around(BeachBoatStory.CampGateTransition gate)=>renderers.Select(r=>new{r,path=AnimationUtility.CalculateTransformPath(r.transform,null),mesh=r.GetComponent<MeshFilter>()?.sharedMesh}).Where(x=>x.mesh!=null&&Vector3.Distance(gate.transform.position,x.r.bounds.center)<500&&(x.mesh.name.ToLowerInvariant().Contains("wall")||x.mesh.name.ToLowerInvariant().Contains("fence")||x.path.ToLowerInvariant().Contains("fence")||x.path.ToLowerInvariant().Contains("rao"))).OrderBy(x=>Vector3.Distance(gate.transform.position,x.r.bounds.center)).Take(80).Select(x=>new{name=x.r.name,path=x.path,mesh=x.mesh.name,meshPath=AssetDatabase.GetAssetPath(x.mesh),transform=x.r.transform.position.ToString("F1"),center=x.r.bounds.center.ToString("F1"),size=x.r.bounds.size.ToString("F1"),rotation=x.r.transform.eulerAngles.ToString("F1"),distance=Vector3.Distance(gate.transform.position,x.r.bounds.center).ToString("F0")}).ToArray();
            var boatWords=new[]{"boat","thuyen","chèo","cheo","canoe","ship"};
            var boats=Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(f=>f.gameObject.scene==scene&&f.sharedMesh!=null&&boatWords.Any(w=>(AnimationUtility.CalculateTransformPath(f.transform,null)+"/"+f.sharedMesh.name).ToLowerInvariant().Contains(w))).Select(f=>new{name=f.name,path=AnimationUtility.CalculateTransformPath(f.transform,null),mesh=f.sharedMesh.name,meshPath=AssetDatabase.GetAssetPath(f.sharedMesh),position=f.transform.position.ToString("F1"),scale=f.transform.lossyScale.ToString("F1"),active=f.gameObject.activeInHierarchy,bounds=f.GetComponent<Renderer>()!=null?f.GetComponent<Renderer>().bounds.size.ToString("F1"):"none"}).ToArray();
            System.IO.File.WriteAllText("Logs/OuterFenceBoatAudit.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{campFence=Around(camp),villageFence=Around(village),boats},Newtonsoft.Json.Formatting.Indented));EditorApplication.Exit(0);
        }
    }
}
