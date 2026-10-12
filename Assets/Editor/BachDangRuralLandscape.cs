using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Small, terrain-conforming additions using shared meshes and existing textures.</summary>
public static class BachDangRuralLandscape
{
    const string Folder = "Assets/BachDangRuralLandscape";
    const string Marker = "13_Ruong_Vuon_Ben_Song";
    const float F = 7;

    [MenuItem("Bach Dang/Add Rural Landscape And Detail LOD")]
    public static void Apply()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/beachBoat.unity" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Open beachBoat in Edit Mode.");
        var settlement = scene.GetRootGameObjects().First(g => g.name == "BachDang_Living_Settlements").transform;
        if (settlement.Find(Marker) != null) { Debug.Log("Rural landscape already exists; nothing duplicated."); return; }
        if (!Mathf.Approximately(BachDangWorldScale.ForScene(scene), F)) throw new InvalidOperationException("Requires scale seven.");
        if (AssetDatabase.IsValidFolder(Folder)) throw new InvalidOperationException("Owned asset folder already exists without scene marker.");
        Directory.CreateDirectory(".utmp/BeachLife");
        if (!EditorSceneManager.SaveScene(scene, ".utmp/BeachLife/beachBoat-before.unity", true)) throw new IOException("Backup failed.");
        var terrain = Terrain.activeTerrain;
        int trees = terrain.terrainData.treeInstanceCount;
        var actors = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SkinnedMeshRenderer>(true)).ToDictionary(r => r, r => r.transform.localToWorldMatrix);
        Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Rural landscape and detail visibility");
        var assets = new List<string>();
        AssetDatabase.CreateFolder("Assets", "BachDangRuralLandscape");
        try
        {
            var root = Group(Marker, settlement);
            var b = new Builder(terrain, settlement, root, assets);
            b.Farms(); b.Fishing(); b.Shore();
            int detailLods = OptimizeDetails(settlement);
            // Keep the visible forest envelope while rendering fewer full-detail trees.
            Undo.RecordObject(terrain, "Reduce nearby full-detail tree budget");
            terrain.treeBillboardDistance = Mathf.Min(terrain.treeBillboardDistance, 350);
            terrain.treeMaximumFullLODCount = Mathf.Min(terrain.treeMaximumFullLODCount, 120);
            EditorUtility.SetDirty(terrain);
            long bytes = assets.Where(p => p.EndsWith(".asset")).Select(AssetDatabase.LoadAssetAtPath<Mesh>).Where(m => m != null).Sum(Profiler.GetRuntimeMemorySizeLong);
            int renderers = root.GetComponentsInChildren<Renderer>(true).Length;
            if (bytes > 4 * 1024 * 1024 || renderers > 240 || b.fields < 6 || b.fishing < 3 || b.shore < 8)
                throw new InvalidOperationException($"Landscape budget/coverage failed: {bytes} bytes, {renderers} renderers, {b.fields} fields, {b.fishing} fishing, {b.shore} shore.");
            if (trees != terrain.terrainData.treeInstanceCount || root.GetComponentsInChildren<Collider>(true).Length != 0 || root.GetComponentsInChildren<Light>(true).Length != 0)
                throw new InvalidOperationException("Tree/light/collider preservation failed.");
            foreach (var a in actors) if (a.Key == null || a.Key.transform.localToWorldMatrix != a.Value) throw new InvalidOperationException("Actor changed.");
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Save failed.");
            Undo.CollapseUndoOperations(undo);
            File.WriteAllText(".utmp/BeachLife/result.json", Newtonsoft.Json.JsonConvert.SerializeObject(new {
                fields=b.fields, fishingStations=b.fishing, shoreClusters=b.shore, renderers, meshBytes=bytes, detailLods, trees,
                newTextures=0, newLights=0, newColliders=0, actors=actors.Count,
                terrain.treeDistance, terrain.treeBillboardDistance, terrain.treeMaximumFullLODCount
            }, Newtonsoft.Json.Formatting.Indented));
        }
        catch
        {
            Undo.RevertAllDownToGroup(undo);
            foreach (string p in assets.AsEnumerable().Reverse()) AssetDatabase.DeleteAsset(p);
            AssetDatabase.DeleteAsset(Folder);
            throw;
        }
    }

    static Transform Group(string name, Transform parent)
    {
        var go = new GameObject(name); Undo.RegisterCreatedObjectUndo(go, "Rural landscape");
        go.transform.SetParent(parent, false); return go.transform;
    }

    // Native LOD supports both Scene and Game cameras without an Update script per prop.
    static bool Cull(Renderer r, float referenceDistance)
    {
        if (!r.enabled || r.GetComponentInParent<LODGroup>() != null || r.GetComponentsInChildren<LODGroup>(true).Length != 0) return false;
        var lod = Undo.AddComponent<LODGroup>(r.gameObject);
        lod.SetLODs(new[] { new LOD(.01f, new[] { r }) }); lod.RecalculateBounds();
        float size = lod.size * Mathf.Max(Mathf.Abs(lod.transform.lossyScale.x), Mathf.Abs(lod.transform.lossyScale.y), Mathf.Abs(lod.transform.lossyScale.z));
        // Nominal distance at 60 degrees FOV, accounting for the current quality LOD bias.
        float height = Mathf.Clamp(size * Mathf.Max(.01f, QualitySettings.lodBias) / (referenceDistance * F * 1.1547f), .0005f, .4f);
        lod.SetLODs(new[] { new LOD(height, new[] { r }) });
        lod.fadeMode = LODFadeMode.None; EditorUtility.SetDirty(lod);
        return true;
    }

    static int OptimizeDetails(Transform settlement)
    {
        int count = 0;
        foreach (string name in new[] { "09_Sinh_Hoat", "11_San_Nha_Va_Cho", "08_Ben_Ca_Dan_Sinh" })
        {
            var group = settlement.Find(name); if (group == null) continue;
            foreach (var r in group.GetComponentsInChildren<MeshRenderer>())
            {
                float size = r.bounds.size.magnitude / F;
                if (size > 14 || r.name.Contains("Duong") || r.name.Contains("Loi_") || r.name.Contains("Cau_")) continue;
                if (Cull(r, size < 3 ? 95 : 170)) count++;
            }
        }
        // Existing model LODs are preserved. Buildings without authored LOD meshes get
        // only a distant cull, not invented low-detail geometry or copied mesh buffers.
        foreach (var village in settlement.Cast<Transform>().Where(t => t.name.StartsWith("01_") || t.name.StartsWith("02_") || t.name.StartsWith("03_")))
        foreach (var house in village.Cast<Transform>().Where(t => t.name.StartsWith("Nha_Dan_") && t.childCount > 0))
        foreach (var r in house.GetChild(0).GetComponentsInChildren<MeshRenderer>())
            if (Cull(r, 600)) count++;
        return count;
    }

    sealed class Builder
    {
        readonly Terrain terrain;
        readonly Transform settlement, root;
        readonly List<string> assets;
        readonly List<Bounds> occupied;
        readonly Vector3[] trees;
        readonly BachDangVillageGround ground;
        readonly Material wood, straw, crops, reeds, stone, cloth;
        readonly Mesh cropMesh, reedMesh, hayMesh, netMesh, pebbleMesh, basket, table, shade, shadeFrame;
        int serial;
        public int fields, fishing, shore;
        public Builder(Terrain terrain, Transform settlement, Transform root, List<string> assets)
        {
            this.terrain=terrain; this.settlement=settlement; this.root=root; this.assets=assets;
            occupied=settlement.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled).Select(r=>r.bounds).ToList();
            trees=terrain.terrainData.treeInstances.Select(t=>terrain.transform.position+Vector3.Scale(t.position,terrain.terrainData.size)).ToArray();
            ground=new BachDangVillageGround(terrain,F,Folder,assets);
            wood=Load<Material>("Assets/BachDangCourtyards/Courtyard_Weathered_Wood.mat");
            straw=Load<Material>("Assets/BachDangCourtyards/Courtyard_Woven_Fibre.mat");
            cloth=Load<Material>("Assets/BachDangScaleAndLife/Cloth_Undyed_Linen.mat");
            crops=Tint("Rice_Leaf",new Color(.33f,.40f,.13f)); reeds=Tint("Shore_Reed",new Color(.39f,.42f,.23f)); stone=Tint("River_Pebble",new Color(.40f,.39f,.33f));
            cropMesh=Plant("Shared_Rice_Clump",.65f,3,false); reedMesh=Plant("Shared_Reed_Clump",1.5f,6,true);
            hayMesh=Lathe("Shared_Straw_Stack",new[]{new Vector2(.8f,0),new Vector2(1.15f,.5f),new Vector2(.85f,1.6f),new Vector2(.2f,2.1f)},12);
            pebbleMesh=Lathe("Shared_River_Stone",new[]{new Vector2(.35f,0),new Vector2(.55f,.16f),new Vector2(.3f,.4f),new Vector2(.02f,.44f)},7);
            var net=new Geometry();
            for(int i=0;i<=18;i++){float x=-2.4f+i*.26667f;net.Beam(new Vector3(x,.55f,0),new Vector3(x,2.6f-.25f*Mathf.Sin(i/18f*Mathf.PI),0),.018f);}
            for(int j=0;j<=10;j++)for(int i=0;i<12;i++){float x=-2.4f+i*.4f;float y=.55f+j*.19f;net.Beam(new Vector3(x,y-.2f*Mathf.Sin(i/12f*Mathf.PI),0),new Vector3(x+.4f,y-.2f*Mathf.Sin((i+1)/12f*Mathf.PI),0),.018f);}
            netMesh=Save(net.Mesh("Shared_Fishing_Net"));
            basket=Load<Mesh>("Assets/BachDangCourtyards/Shared_Woven_Basket.asset"); table=Load<Mesh>("Assets/BachDangCourtyards/Shared_Market_Table.asset");
            shade=Load<Mesh>("Assets/BachDangCourtyards/Shared_Linen_Shade.asset");shadeFrame=Load<Mesh>("Assets/BachDangCourtyards/Shared_Yard_Shade_Frame.asset");
        }
        T Load<T>(string p) where T:Object => AssetDatabase.LoadAssetAtPath<T>(p) ?? throw new InvalidOperationException("Missing "+p);
        Material Tint(string name,Color color)
        {
            var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,enableInstancing=true};
            m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.05f);m.SetFloat("_Cull",0);
            string p=Folder+"/"+name+".mat";AssetDatabase.CreateAsset(m,p);assets.Add(p);return m;
        }
        Mesh Save(Mesh m)
        {string p=Folder+"/"+(serial++).ToString("000")+"_"+m.name+".asset";AssetDatabase.CreateAsset(m,p);assets.Add(p);return m;}
        Vector3 Ground(Vector3 p,float lift=0){p.y=terrain.SampleHeight(p)+terrain.transform.position.y+lift*F;return p;}
        bool Free(Vector3 p,float width,float depth,bool dry=true)
        {
            float min=float.MaxValue,max=float.MinValue;
            for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++){float h=Ground(p+new Vector3(x*width*.5f*F,0,z*depth*.5f*F)).y/F;min=Mathf.Min(min,h);max=Mathf.Max(max,h);}
            if((dry && min<16.6f) || max-min>.5f) return false;
            float hx=width*.5f*F,hz=depth*.5f*F;
            if(occupied.Any(b=>b.max.x>p.x-hx && b.min.x<p.x+hx && b.max.z>p.z-hz && b.min.z<p.z+hz))return false;
            return !trees.Any(t=>Mathf.Abs(t.x-p.x)<hx+3*F && Mathf.Abs(t.z-p.z)<hz+3*F);
        }
        Transform MeshObject(string name,Transform parent,Mesh mesh,Material material,Vector3 position,Vector3 scale,float yaw=0,float distance=200)
        {
            var t=Group(name,parent);t.position=position;t.rotation=Quaternion.Euler(0,yaw,0);
            t.localScale=new Vector3(scale.x*F/parent.lossyScale.x,scale.y*F/parent.lossyScale.y,scale.z*F/parent.lossyScale.z);
            t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            var r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;
            Cull(r,distance);return t;
        }
        public void Farms()
        {
            foreach(string name in new[]{"01_Lang_Cho_Ben_Song","03_Xom_Vuon_Bo_Tay","02_Xom_Chai_Bo_Dong"})
            {
                var village=settlement.Find(name);var group=Group(name+"_Ruong_Vuon",root);int made=0;
                // Search the existing cleared outer envelope, then expand modestly.
                for(int ring=0;ring<4 && made<4;ring++)for(int step=0;step<32 && made<4;step++)
                {
                    float angle=step*Mathf.PI*2/32;var p=village.position+new Vector3(Mathf.Cos(angle)*(78+ring*14),0,Mathf.Sin(angle)*(78+ring*14))*F;
                    if(!Free(p,23,18))continue;
                    var plot=Group("Thua_Ruong_"+(made+1),group);plot.position=Ground(p);
                    var soil=new Geometry();var plants=new List<CombineInstance>();var ridges=new Geometry();
                    for(int x=0;x<10;x++)for(int z=0;z<7;z++)
                    {
                        float a=-10+x*2,b=-7+z*2;
                        soil.Quad(Local(a,b,.06f),Local(a,b+2,.06f),Local(a+2,b+2,.06f),Local(a+2,b,.06f));
                        for(int k=0;k<(x%2==0?2:1);k++)
                        {
                            var wp=Ground(p+new Vector3(a+.6f+k*.85f,0,b+.65f+k*.7f)*F,.08f);
                            plants.Add(new CombineInstance{mesh=cropMesh,transform=plot.worldToLocalMatrix*Matrix4x4.TRS(wp,Quaternion.Euler(0,(x*23+z*41+k*60)%180,0),Vector3.one*F*(.92f+(z%3)*.08f))});
                        }
                    }
                    for(int row=0;row<=7;row++)Berm(-10,-7+row*2,10,-7+row*2,.34f);
                    Berm(-10,-7,-10,7,.45f);Berm(10,-7,10,7,.45f);
                    var soilMesh=Save(soil.Mesh("Field_Soil"));var sr=Raw("Dat_Ruong",plot,soilMesh,ground.Mud,400);
                    Raw("Bo_Dat_Va_Luong",plot,Save(ridges.Mesh("Field_Bunds")),ground.Earth,320);
                    var pm=new Mesh{name="Field_Rice_Rows"};pm.CombineMeshes(plants.ToArray(),true,true);Raw("Lua_Non",plot,Save(pm),crops,175);
                    // One shallow, dark channel alongside each plot; it follows the existing terrain.
                    ground.CreatePath(group,"Muong_Nho_"+made,new[]{Ground(p+new Vector3(11,0,-8)*F),Ground(p+new Vector3(11,0,8)*F)},.55f,ground.Mud,made,new List<Bounds>());
                    MeshObject("Dong_Rom",plot,hayMesh,straw,Ground(p+new Vector3(-10.3f,0,8)*F),Vector3.one,17,160);
                    var fence=new Geometry();for(int i=0;i<6;i++){float x=-8+i*3;fence.Beam(Local(x,8,.03f),Local(x,8,1.1f),.09f);}
                    fence.Beam(Local(-8,8,.75f),Local(7,8,.75f),.06f);Raw("Rao_Thua",plot,Save(fence.Mesh("Field_Fence")),wood,180);
                    // A short footpath points inward but stops at the field edge, leaving roads intact.
                    Vector3 toward=(village.position-p);toward.y=0;toward.Normalize();
                    var start=p+toward*12*F;var end=p+toward*21*F;
                    if(Free((start+end)*.5f,3,3))ground.CreatePath(group,"Loi_Ruong_"+made,new[]{Ground(start),Ground(end)},1.2f,ground.Earth,fields,new List<Bounds>());
                    occupied.Add(new Bounds(Ground(p),new Vector3(25*F,2*F,20*F)));made++;fields++;
                    Vector3 Local(float x,float z,float h)=>plot.InverseTransformPoint(Ground(p+new Vector3(x,0,z)*F,h));
                    void Berm(float ax,float az,float bx,float bz,float width)
                    {
                        var side=new Vector2(-(bz-az),bx-ax).normalized*width;
                        for(int segment=0;segment<10;segment++)
                        {
                            var a=Vector2.Lerp(new Vector2(ax,az),new Vector2(bx,bz),segment/10f);
                            var b=Vector2.Lerp(new Vector2(ax,az),new Vector2(bx,bz),(segment+1)/10f);
                            var leftA=Local(a.x-side.x,a.y-side.y,.065f);var leftB=Local(b.x-side.x,b.y-side.y,.065f);
                            var topA=Local(a.x,a.y,.15f);var topB=Local(b.x,b.y,.15f);
                            var rightA=Local(a.x+side.x,a.y+side.y,.065f);var rightB=Local(b.x+side.x,b.y+side.y,.065f);
                            ridges.Quad(leftA,topA,topB,leftB);ridges.Quad(topA,rightA,rightB,topB);
                        }
                    }
                }
            }
        }
        Transform Raw(string name,Transform parent,Mesh mesh,Material mat,float distance)
        {
            var t=Group(name,parent);t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;var r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=mat;
            r.shadowCastingMode=ShadowCastingMode.Off;r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;Cull(r,distance);return t;
        }
        public void Fishing()
        {
            var group=Group("Ben_Ca_Do_Nghe",root);
            foreach(var p0 in new[]{new Vector3(15,0,90),new Vector3(24,0,60),new Vector3(43,0,95),new Vector3(58,0,57),new Vector3(82,0,95),new Vector3(28,0,105)})
            {
                var p=p0*F;if(!Free(p,8,6))continue;
                var station=Group("San_Dan_Luoi_"+(++fishing),group);station.position=Ground(p);
                var frame=new Geometry();frame.Beam(new Vector3(-2.6f,0,0),new Vector3(-2.6f,2.9f,0),.13f);frame.Beam(new Vector3(2.6f,0,0),new Vector3(2.6f,2.9f,0),.13f);frame.Beam(new Vector3(-2.7f,2.7f,0),new Vector3(2.7f,2.7f,0),.1f);
                MeshObject("Khung_Phoi_Luoi",station,Save(frame.Mesh("Net_Frame")),wood,Ground(p),Vector3.one,0,190);
                MeshObject("Luoi_Danh_Ca",station,netMesh,straw,Ground(p+Vector3.forward*.05f*F),Vector3.one,0,125);
                for(int j=0;j<3;j++)MeshObject("Gio_Ca_"+j,station,basket,straw,Ground(p+new Vector3(-1.8f+j*1.4f,0,-1.8f)*F),Vector3.one*.9f,j*27,95);
                MeshObject("Ban_Va_Luoi",station,table,wood,Ground(p+new Vector3(0,0,2)*F),new Vector3(.75f,.75f,.65f),180,160);
                ground.CreatePatch(group,"San_Dat_Ben_"+fishing,p,0,8,6,ground.Earth,fishing,new List<Bounds>());
                occupied.Add(new Bounds(Ground(p),new Vector3(9*F,4*F,7*F)));
            }
            // Reuse the small courtyard awning, positioned on an unobstructed patch of bank.
            foreach(var p0 in new[]{new Vector3(65,0,90),new Vector3(95,0,60),new Vector3(45,0,110)})
            {
                var p=p0*F;if(!Free(p,10,9))continue;
                MeshObject("Mai_Che_Hang_Ben",group,shade,cloth,Ground(p),Vector3.one,20,230);
                MeshObject("Cot_Mai_Che_Ben",group,shadeFrame,wood,Ground(p),Vector3.one,20,230);
                MeshObject("Ban_Duoi_Mai",group,table,wood,Ground(p),Vector3.one,20,160);break;
            }
        }
        public void Shore()
        {
            var group=Group("Bo_Song_Lau_Da_Bun",root);
            var anchors=new[]{new Vector3(0,0,75)*F,settlement.Find("01_Lang_Cho_Ben_Song").position,settlement.Find("03_Xom_Vuon_Bo_Tay").position};
            foreach(var anchor in anchors)
            for(int angle=0;angle<40;angle++)
            {
                var direction=Quaternion.Euler(0,angle*9,0)*Vector3.forward;Vector3? candidate=null;
                for(float r=30;r<390;r+=6)
                {
                    var p=anchor+direction*r*F;
                    // Reserve the cleared western island for the user's future military layout.
                    if(p.x < -150*F && p.z > -100*F && p.z < 550*F) continue;
                    float h=Ground(p).y/F;
                    if(h>=15.9f && h<=17.3f && Free(p,8,7,false)){candidate=p;break;}
                }
                if(!candidate.HasValue)continue;
                var center=candidate.Value;if(shore>=30)break;
                var patch=Group("Cum_Lau_"+(++shore),group);patch.position=Ground(center);
                var list=new List<CombineInstance>();
                for(int i=0;i<10;i++)
                {
                    float a=i*2.39996f,rad=Mathf.Sqrt(i/10f)*2.8f;var p=Ground(center+new Vector3(Mathf.Cos(a)*rad,0,Mathf.Sin(a)*rad)*F);
                    if(p.y/F<15.7f)continue;
                    list.Add(new CombineInstance{mesh=reedMesh,transform=patch.worldToLocalMatrix*Matrix4x4.TRS(p,Quaternion.Euler(0,i*73,0),Vector3.one*F*(.65f+(i%4)*.12f))});
                }
                var mesh=new Mesh{name="Shore_Reeds"};mesh.CombineMeshes(list.ToArray(),true,true);Raw("Lau_Thua",patch,Save(mesh),reeds,220);
                ground.CreatePatch(group,"Bun_Ven_Bo_"+shore,center,angle*9,7,5,ground.Mud,shore,new List<Bounds>());
                var offsets=new[]{new Vector3(-1.2f,0,1.1f),new Vector3(.1f,0,2.3f),new Vector3(1.6f,0,-.6f)};
                for(int i=0;i<3;i++)MeshObject("Da_Cuoi_"+i,patch,pebbleMesh,stone,Ground(center+offsets[i]*F),new Vector3(1,.7f,1.2f)*(.6f+i*.15f),i*33,130);
                occupied.Add(new Bounds(Ground(center),new Vector3(14*F,4*F,14*F)));
            }
        }
        Mesh Plant(string name,float height,int blades,bool tall)
        {
            var g=new Geometry();
            for(int i=0;i<blades;i++)
            {
                float a=i*2.39996f;var side=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*.065f;var p=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a))*.13f;
                var mid=p+Vector3.up*height*.58f;var tip=p+Vector3.up*height+side*4;
                g.Quad(p-side,p+side,mid+side*.6f,mid-side*.6f);g.Triangle(mid-side*.6f,mid+side*.6f,tip);
                if(tall && i%3==0)g.Beam(tip-Vector3.up*.17f,tip+Vector3.up*.12f,.035f);
            }
            return Save(g.Mesh(name));
        }
        Mesh Lathe(string name,Vector2[] rings,int segments)
        {
            var g=new Geometry();for(int j=0;j<rings.Length-1;j++)for(int i=0;i<segments;i++)
            {float a=i*2*Mathf.PI/segments,b=(i+1)*2*Mathf.PI/segments;g.Quad(P(rings[j],a),P(rings[j+1],a),P(rings[j+1],b),P(rings[j],b));}
            return Save(g.Mesh(name));
            Vector3 P(Vector2 r,float a)=>new Vector3(Mathf.Cos(a)*r.x*(1+.045f*Mathf.Sin(a*5)),r.y,Mathf.Sin(a)*r.x*(1+.07f*Mathf.Cos(a*3)));
        }
    }
    sealed class Geometry
    {
        readonly List<Vector3> v=new List<Vector3>();readonly List<int> t=new List<int>();
        public void Triangle(Vector3 a,Vector3 b,Vector3 c){int n=v.Count;v.AddRange(new[]{a,b,c});t.AddRange(new[]{n,n+1,n+2});}
        public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d){Triangle(a,b,c);Triangle(a,c,d);}
        public void Beam(Vector3 a,Vector3 b,float width)
        {
            var direction=(b-a).normalized;var side=Vector3.Cross(direction,Mathf.Abs(direction.y)>.95f?Vector3.forward:Vector3.up).normalized*width*.5f;var up=Vector3.Cross(direction,side).normalized*width*.5f;
            Quad(a-side-up,b-side-up,b+side-up,a+side-up);Quad(a+side-up,b+side-up,b+side+up,a+side+up);
            Quad(a+side+up,b+side+up,b-side+up,a-side+up);Quad(a-side+up,b-side+up,b-side-up,a-side-up);
        }
        public Mesh Mesh(string name)
        {
            var m=new Mesh{name=name};m.SetVertices(v);m.SetTriangles(t,0);m.SetUVs(0,v.Select(p=>new Vector2(p.x,p.z)).ToList());
            m.SetColors(Enumerable.Repeat(Color.white,v.Count).ToList());m.RecalculateNormals();m.RecalculateBounds();return m;
        }
    }
}
