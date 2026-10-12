using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Story = BeachBoatStory;

// One-time migration tool. Requires the temporary exact scene blob from Thai 03aad75.
// Never rebuilds terrain, architecture, vegetation or the original TestQuest scene.
public static class BeachBoatQuestInstaller
{
    const string RootName = "BeachBoat_Thai_Gameplay";
    static Transform root;
    static Terrain terrain;
    static GameObject[] sources;
    static Vector3 Ground(float x, float z) => new Vector3(x, terrain.SampleHeight(new Vector3(x,0,z)) + terrain.transform.position.y + .15f, z);
    static Transform Anchor(string name, float x, float z, float yaw = 0)
    {
        var go = new GameObject(name); go.transform.SetParent(root, false);
        go.transform.SetPositionAndRotation(Ground(x,z), Quaternion.Euler(0,yaw,0)); return go.transform;
    }
    static Bounds Bounds(GameObject go)
    {
        var rs=go.GetComponentsInChildren<Renderer>().Where(r=>r is MeshRenderer || r is SkinnedMeshRenderer).ToArray();
        if(rs.Length==0) throw new Exception("Missing visual: "+go.name);
        var b=rs[0].bounds; foreach(var r in rs.Skip(1)) b.Encapsulate(r.bounds); return b;
    }
    static GameObject Clone(string sourceName, string name, Vector3 position, float yaw)
    {
        var source=sources.Single(g=>g.name==sourceName);
        var go=UnityEngine.Object.Instantiate(source); go.name=name;
        SceneManager.MoveGameObjectToScene(go,root.gameObject.scene); go.transform.SetParent(root,true);
        foreach(var c in go.GetComponentsInChildren<MonoBehaviour>(true)) if(c!=null) UnityEngine.Object.DestroyImmediate(c);
        foreach(var t in go.GetComponentsInChildren<Transform>(true)) GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
        foreach(var c in go.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
        foreach(var c in go.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(c);
        go.SetActive(true); go.transform.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,0));
        var b=Bounds(go); go.transform.position+=Vector3.up*(position.y-b.min.y);
        foreach(var r in go.GetComponentsInChildren<SkinnedMeshRenderer>()) r.updateWhenOffscreen=false;
        foreach(var a in go.GetComponentsInChildren<Animator>()) {a.enabled=true; a.applyRootMotion=false; a.cullingMode=AnimatorCullingMode.CullUpdateTransforms;}
        return go;
    }
    static void Marker(GameObject go, Story.StoryLandmarkMarker.Target type,string label)
    {var m=go.AddComponent<Story.StoryLandmarkMarker>(); m.target=type; m.label=label;}
    static Story.CampGateTransition Gate(string name, float x,float z,Story.CampGateTransition.GateType type,Transform destination)
    {
        var a=Anchor(name,x,z); var g=a.gameObject.AddComponent<Story.CampGateTransition>();
        g.gateType=type; g.destinationTarget=destination; g.interactDistance=32; return g;
    }
    public static string Install()
    {
        var scene=SceneManager.GetActiveScene();
        if(Application.isPlaying || scene.path!="Assets/Scenes/beachBoat.unity") throw new Exception("Open beachBoat in Edit Mode first.");
        if(GameObject.Find(RootName)!=null) throw new Exception("Already installed; refusing to duplicate gameplay.");
        terrain=Terrain.activeTerrains.Single(t=>t.gameObject.scene==scene);
        var original=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
        var before=original.Select(t=>new { t, p=t.position, r=t.rotation, s=t.localScale, active=t.gameObject.activeSelf }).ToArray();
        int trees=terrain.terrainData.treeInstanceCount;
        var camera=Camera.main;
        root=new GameObject(RootName).transform;
        var preview=EditorSceneManager.OpenPreviewScene("Assets/Editor/BeachQuestSource/ThaiTestQuest.unity");
        try
        {
            sources=preview.GetRootGameObjects();
            var story=root.gameObject.AddComponent<Story.BachDangStoryManager>();
            var session=root.gameObject.AddComponent<Story.BeachBoatQuestSession>();
            var spawn=Anchor("CampArrival",-7170,-2990,12);
            var player=Clone("Main_Character","Player_Thai",spawn.position,12);
            // Model and capsule use the original character scale 7, never map scale x7 again.
            var pb=Bounds(player); var cap=player.AddComponent<CapsuleCollider>();
            cap.center=player.transform.InverseTransformPoint(pb.center); cap.height=pb.size.y/player.transform.lossyScale.y;
            cap.radius=.38f; cap.direction=1;
            var body=player.AddComponent<Rigidbody>(); body.mass=80; body.constraints=RigidbodyConstraints.FreezeRotation;
            player.AddComponent<Story.Character_Movement>(); player.tag="Player";
            var controller=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/BeachBoatStory/SourceAssets/Player_Neutral.controller");
            foreach(var a in player.GetComponentsInChildren<Animator>()) a.runtimeAnimatorController=controller;
            // Arrival anchors represent the model pivot, with feet resting above the terrain.
            float pivotLift=player.transform.position.y-spawn.position.y; spawn.position+=Vector3.up*pivotLift;
            var ngo=Clone("NPC_NgoQuyen (1)","NgoQuyen_QuestGiver",Ground(-7118,-2780),192);
            ngo.AddComponent<Story.NPCQuestGiver>().interactDistance=28;
            Marker(ngo,Story.StoryLandmarkMarker.Target.NgoQuyen,"NGÔ QUYỀN • E");
            var village=Anchor("VillageArrival",-6720,-4700,180); village.position+=Vector3.up*pivotLift;
            string[] lines={"Bà con đã gom gỗ dọc đường làng. Xin mang về cho quân ta dựng trận địa cọc!","Nước sông lên xuống theo con triều. Mong quân ta chọn đúng thời cơ đánh giặc.","Củi gỗ của làng đều xin góp cho đại quân. Hãy tìm đủ năm khúc rồi trở về báo tin."};
            Vector2[] npcPos={new Vector2(-6750,-4770),new Vector2(-6650,-5040),new Vector2(-6610,-5390)};
            for(int i=0;i<3;i++)
            {
                var npc=Clone("NPC_DanLang_"+i,"DanLang_Thai_"+i,Ground(npcPos[i].x,npcPos[i].y),270);
                var d=npc.AddComponent<Story.NPCDialogue>(); d.interactDistance=20; d.dialogueText=lines[i];
                d.speakerName=i==0?"Bô lão trong làng":i==1?"Người dân ven sông":"Người góp gỗ";
                foreach(var a in npc.GetComponentsInChildren<Animator>())
                    if(a.runtimeAnimatorController==null) a.runtimeAnimatorController=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/BachDangAtmosphere/BachDang_GuardIdle.controller");
            }
            Vector2[] woodPos={new Vector2(-6675,-4795),new Vector2(-6640,-4905),new Vector2(-6610,-5140),new Vector2(-6580,-5310),new Vector2(-6550,-5440)};
            session.woodItems=new Story.WoodCollectible[5];
            for(int i=0;i<5;i++)
            {
                var wood=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Props/pf_logpile_01.prefab"),root);
                wood.name="Go_NhiemVu_"+(i+1);
                wood.transform.SetPositionAndRotation(Ground(woodPos[i].x,woodPos[i].y),Quaternion.Euler(0,i*37,0));
                foreach(var c in wood.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
                var b=Bounds(wood); float longest=Mathf.Max(b.size.x,Mathf.Max(b.size.y,b.size.z));
                wood.transform.localScale*=9f/longest; b=Bounds(wood);
                wood.transform.position+=Vector3.up*(Ground(woodPos[i].x,woodPos[i].y).y-b.min.y);
                session.woodItems[i]=wood.AddComponent<Story.WoodCollectible>(); session.woodItems[i].interactDistance=17;
                Marker(wood,Story.StoryLandmarkMarker.Target.Wood,"GỖ LÀM CỌC • E");
            }
            var river=Anchor("RiverArrival",-3920,2100,90); river.position+=Vector3.up*pivotLift;
            var campGate=Gate("CampToVillage",-7211,-3205,Story.CampGateTransition.GateType.CampGate,village);
            Marker(campGate.gameObject,Story.StoryLandmarkMarker.Target.CampGate,"ĐẾN LÀNG VEN SÔNG • E");
            var villageGate=Gate("VillageToCamp",-6720,-4640,Story.CampGateTransition.GateType.VillageGate,spawn);
            Marker(villageGate.gameObject,Story.StoryLandmarkMarker.Target.VillageGate,"VỀ DOANH TRẠI • E");
            var riverGate=Gate("CampToRiver",-7211,-3205,Story.CampGateTransition.GateType.CampRiverGate,river);
            Marker(riverGate.gameObject,Story.StoryLandmarkMarker.Target.RiverStakes,"RA BỜ SÔNG • E");
            var returnGate=Gate("RiverToCamp",-3960,2100,Story.CampGateTransition.GateType.RiverGate,spawn);
            Marker(returnGate.gameObject,Story.StoryLandmarkMarker.Target.ReturnFromRiver,"VỀ BÁO TIN • E");
            var zone=Anchor("RiverStakeWork",-3860,2100); zone.gameObject.AddComponent<Story.RiverStakeZone>().interactDistance=30;
            Marker(zone.gameObject,Story.StoryLandmarkMarker.Target.RiverStakes,"BÃI CỌC BẠCH ĐẰNG • E");
            story.campSpawnPoint=spawn; story.campGatePoint=campGate.transform; story.villageSpawnPoint=village;
            story.villageGatePoint=villageGate.transform; story.riverStakePoint=zone;
            var camGo=new GameObject("Camera_Thai_ThirdPerson"); camGo.transform.SetParent(root,false);
            var cam=camGo.AddComponent<Camera>(); cam.CopyFrom(camera); cam.enabled=false; cam.nearClipPlane=.3f; cam.fieldOfView=65;
            camGo.AddComponent<AudioListener>().enabled=false;
            var orbit=camGo.AddComponent<Story.Camera_Script>(); orbit.target=player.transform; orbit.enabled=false;
            session.player=player; session.playerSpawn=spawn; session.storyCamera=cam; session.tourCamera=camera;
            session.tourControls=camera.GetComponents<MonoBehaviour>().Where(c=>c is BattleCamera || c is SaBanHUD).ToArray();
            // Collision-only proxies for existing buildings, not duplicate architecture.
            var collision=new GameObject("ExistingBuildingCollisions"); collision.transform.SetParent(root,false);
            foreach(var siteName in new[]{"05_Trai_Du_Bi","01_Lang_Cho_Ben_Song"})
            foreach(Transform building in GameObject.Find(siteName).transform)
            {
                if(!(building.name.StartsWith("Lan_Quan") || building.name.StartsWith("Nha_") || building.name.StartsWith("Kho_"))) continue;
                if(building.GetComponentsInChildren<Collider>().Any(c=>!c.isTrigger)) continue;
                var b=Bounds(building.gameObject); var proxy=new GameObject(building.name+"_Collision"); proxy.transform.SetParent(collision.transform,false);
                proxy.transform.position=b.center; var box=proxy.AddComponent<BoxCollider>(); box.size=new Vector3(b.size.x*.75f,b.size.y,b.size.z*.75f);
            }
            Physics.SyncTransforms();
            if(before.Any(b=>b.t==null || b.t.position!=b.p || b.t.rotation!=b.r || b.t.localScale!=b.s || b.t.gameObject.activeSelf!=b.active) || terrain.terrainData.treeInstanceCount!=trees)
                throw new Exception("Original map changed unexpectedly.");
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            return "Installed: 1 player, 4 NPCs, 5 wood pickups, 4 transitions. Original "+original.Length+" transforms and "+trees+" trees unchanged.";
        }
        catch { UnityEngine.Object.DestroyImmediate(root.gameObject); throw; }
        finally { EditorSceneManager.ClosePreviewScene(preview); sources=null; }
    }
}
