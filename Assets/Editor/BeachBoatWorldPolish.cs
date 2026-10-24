using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BeachBoatStory;
using BeachBoatStoryEditor;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static class BeachBoatWorldPolish
{
    const string RootName = "BeachBoat_WorldPolish_Optimized";
    const float Scale = 7f;
    static Terrain terrain;
    static int props, buildings, collidersDisabled, shadowsDisabled;
    static readonly List<MapZoneStreamer.Zone> zones = new List<MapZoneStreamer.Zone>();

    [MenuItem("BeachBoat/Polish Entire Map Optimized")]
    public static void Apply()
    {
        var scene = BeachBoatToolSafety.Begin("WorldPolishOptimized");
        terrain = Terrain.activeTerrain;
        if (terrain == null) throw new InvalidOperationException("The beachBoat terrain is missing.");
        var old = GameObject.Find(RootName);
        if (old != null) Undo.DestroyObjectImmediate(old);
        props = buildings = collidersDisabled = shadowsDisabled = 0; zones.Clear();

        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Create optimized world polish");
        var streamer = root.AddComponent<MapZoneStreamer>();

        DressCommandCamp(root.transform);
        DressVillages(root.transform);
        DressEastCamp(root.transform);
        DressFishingLanding(root.transform);
        PolishWarDrum();
        OptimizeExistingDecoration(scene);
        AuthorWaypointFacing();
        ConfigureTerrain();
        AddExistingDecorationZones(scene);

        streamer.zones = zones.Where(z => z.content != null).GroupBy(z => z.content).Select(g => g.First()).ToArray();
        streamer.checkInterval = .65f;
        streamer.hideHysteresis = 1.16f;
        EditorUtility.SetDirty(streamer);
        BeachBoatToolSafety.Save(scene);

        Directory.CreateDirectory("Logs");
        var report = new {
            success = true, addedProps = props, addedBuildings = buildings,
            streamedDecorationZones = streamer.zones.Length,
            disabledDecorativeColliders = collidersDisabled, disabledSmallPropShadows = shadowsDisabled,
            terrainTrees = terrain.terrainData.treeInstanceCount, terrain.treeDistance,
            activeColliders = Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude).Count(c => c.gameObject.scene == scene && c.enabled),
            totalRenderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include).Count(r => r.gameObject.scene == scene),
            newTextureAssets = 0,
            note = "All additions reuse shared project prefabs/materials; no terrain trees, people, textures or per-prop lights were added."
        };
        File.WriteAllText("Logs/WorldPolishResult.json", JsonConvert.SerializeObject(report, Formatting.Indented));
        Debug.Log(JsonConvert.SerializeObject(report), root);
    }

    public static void ApplyAndExit()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity", OpenSceneMode.Single);
        Apply();
        EditorApplication.Exit(0);
    }

    public static void ValidateAndExit()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity", OpenSceneMode.Single);
        var root = GameObject.Find(RootName);
        var streamer = root != null ? root.GetComponent<MapZoneStreamer>() : null;
        var renderers = root != null ? root.GetComponentsInChildren<Renderer>(true) : Array.Empty<Renderer>();
        int missingMaterials = renderers.Sum(r => r.sharedMaterials.Count(m => m == null));
        int unsupportedMaterials = renderers.SelectMany(r => r.sharedMaterials).Where(m => m != null)
            .Distinct().Count(m => m.shader == null || !m.shader.isSupported);
        int activeDecorativeColliders = 0;
        var settlement = GameObject.Find("BachDang_Living_Settlements")?.transform;
        foreach (string name in new[] { "09_Sinh_Hoat", "10_Lang_Tu_Nhien", "11_San_Nha_Va_Cho", "13_Ruong_Vuon_Ben_Song" })
        {
            var group = settlement != null ? settlement.Find(name) : null;
            if (group != null) activeDecorativeColliders += group.GetComponentsInChildren<Collider>(true).Count(c => c.enabled);
        }
        bool success = root != null && streamer != null && streamer.zones.Length == 43 &&
            streamer.zones.All(z => z != null && z.content != null && z.content.activeSelf && z.visibleDistance >= 1000) &&
            missingMaterials == 0 && unsupportedMaterials == 0 && activeDecorativeColliders > 0 &&
            Terrain.activeTerrain != null && Terrain.activeTerrain.terrainData.treeInstanceCount == 49547 && Terrain.activeTerrain.treeDistance <= 1400;
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/WorldPolishValidation.json", JsonConvert.SerializeObject(new {
            success, generatedRenderers = renderers.Length, streamerZones = streamer != null ? streamer.zones.Length : 0,
            missingMaterials, unsupportedMaterials, activeDecorativeColliders,
            terrainTrees = Terrain.activeTerrain != null ? Terrain.activeTerrain.terrainData.treeInstanceCount : -1,
            treeDistance = Terrain.activeTerrain != null ? Terrain.activeTerrain.treeDistance : -1
        }, Formatting.Indented));
        EditorApplication.Exit(success ? 0 : 1);
    }

    public static void MemoryAndExit()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity", OpenSceneMode.Single);
        BachDangMapMemoryReport.Capture("world-polish-final-edit");
        EditorApplication.Exit(0);
    }

    static Transform Zone(Transform parent, string name, Vector3 center, float distance)
    {
        var go = new GameObject(name); Undo.RegisterCreatedObjectUndo(go, "Create world district");
        go.transform.SetParent(parent, false);
        zones.Add(new MapZoneStreamer.Zone { content = go, center = center, visibleDistance = distance, visible = true });
        return go.transform;
    }

    static Vector3 Ground(Vector3 p)
    {
        p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
        return p;
    }

    static GameObject Place(string path, string name, Transform parent, Vector3 point, float yaw, float scale = Scale, bool building = false)
    {
        var go = BachDangSettlementAssets.Place(path, name, parent, Ground(point), yaw, scale);
        foreach (var body in go.GetComponentsInChildren<Rigidbody>(true)) Undo.DestroyObjectImmediate(body);
        foreach (var collider in go.GetComponentsInChildren<Collider>(true)) Undo.DestroyObjectImmediate(collider);
        foreach (var light in go.GetComponentsInChildren<Light>(true)) Undo.DestroyObjectImmediate(light);
        foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
        {
            Undo.RecordObject(renderer, "Optimize world prop");
            renderer.shadowCastingMode = building ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = building;
            renderer.allowOcclusionWhenDynamic = true;
        }
        if (building) buildings++; else props++;
        return go;
    }

    static void DressCommandCamp(Transform root)
    {
        var stage = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include)
            .FirstOrDefault(t => t.name.Contains("Rustic_Wooden_Stage"));
        Vector3 center = stage != null ? stage.position : new Vector3(-5511, 0, 1128);
        var command = Zone(root, "01_Dai_Ban_Doanh_Co_Sinh_Khi", center, 1650);
        // A compact command courtyard: readable from ground level without filling the island with more tents.
        Place("Assets/Prefabs/Props/pf_halberd_01.prefab", "Gia_Giao_Tay", command, center + new Vector3(-92,0,48), 22);
        Place("Assets/Prefabs/Props/pf_halberd_01.prefab", "Gia_Giao_Dong", command, center + new Vector3(92,0,48), -22);
        Place("Assets/Prefabs/Props/pf_shield_02.prefab", "Khien_Chi_Huy_1", command, center + new Vector3(-68,0,-62), 18);
        Place("Assets/Prefabs/Props/pf_shield_03.prefab", "Khien_Chi_Huy_2", command, center + new Vector3(68,0,-62), -18);
        Place("Assets/Prefabs/Props/pf_barrels_01.prefab", "Luong_Thuc_Chi_Huy", command, center + new Vector3(-145,0,-118), 31);
        Place("Assets/Prefabs/Props/pf_buckets_01.prefab", "Nuoc_Uong_Quan_Si", command, center + new Vector3(142,0,-115), -18);
        Place("Assets/Prefabs/Props/pf_logpile_01.prefab", "Cui_Bep_Quan", command, center + new Vector3(185,0,96), 12);
        Place("Assets/Prefabs/Props/pf_plankpile_02.prefab", "Go_Sua_Chua", command, center + new Vector3(-188,0,98), 78);
        // The high variants use the project's already-converted URP materials. There are only
        // two of them, while every repeated house/tent elsewhere keeps its low/shared version.
        Place("Assets/Prefabs/Buildings/pf_build_blacksmith_01.prefab", "Lo_Ren_Quan_Do", command, center + new Vector3(305,0,215), 205, Scale, true);
        Place("Assets/Prefabs/Buildings/pf_build_storage_01.prefab", "Kho_Khi_Gioi_Nho", command, center + new Vector3(-315,0,215), 155, Scale, true);

        var supply = Zone(root, "02_Truc_Tiep_Van_Doanh_Trai", center + new Vector3(0,0,-420), 1500);
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 p = center + new Vector3(side * 245, 0, -365);
            Place("Assets/Prefabs/Props/pf_barrels_02.prefab", "Thung_Gao_" + side, supply, p, side * 20);
            Place("Assets/Prefabs/Props/pf_plankpile_01.prefab", "Van_Go_" + side, supply, p + new Vector3(side * 45,0,48), 90);
            Place("Assets/Prefabs/Props/pf_bucket_01.prefab", "Xo_Nuoc_" + side, supply, p + new Vector3(-side * 32,0,-34), side * 28);
        }
    }

    static void DressVillages(Transform root)
    {
        var settlements = GameObject.Find("BachDang_Living_Settlements")?.transform;
        if (settlements == null) return;
        string[] villageNames = { "01_Lang_Cho_Ben_Song", "02_Xom_Chai_Bo_Dong", "03_Xom_Vuon_Bo_Tay" };
        for (int v = 0; v < villageNames.Length; v++)
        {
            var village = settlements.Find(villageNames[v]); if (village == null) continue;
            var zone = Zone(root, "Lang_Song_Dong_" + (v + 1), village.position, 1450);
            var houses = village.Cast<Transform>().Where(t => t.name.StartsWith("Nha_Dan_"))
                .OrderBy(t => t.name).Where((t, i) => i % 4 == v % 2).Take(6).ToArray();
            int index = 0;
            foreach (var house in houses)
            {
                Bounds bounds; try { bounds = BachDangSettlementAssets.BoundsOf(house.gameObject); } catch { continue; }
                float side = index++ % 2 == 0 ? 1 : -1;
                Vector3 basePoint = bounds.center + house.right * side * (Mathf.Max(bounds.extents.x, bounds.extents.z) + 17);
                string main = v == 1 ? "Assets/Prefabs/Props/pf_barrel_fish_01.prefab" :
                    v == 2 ? "Assets/Prefabs/Props/pf_logpile_01.prefab" : "Assets/Prefabs/Props/pf_barrels_01.prefab";
                string second = v == 1 ? "Assets/Prefabs/Props/pf_fish_stick_01.prefab" :
                    v == 2 ? "Assets/Prefabs/Props/pf_shovel_01.prefab" : "Assets/Prefabs/Props/pf_buckets_01.prefab";
                Place(main, "San_Nha_Hoat_Dong_" + index, zone, basePoint, house.eulerAngles.y + side * 18);
                Place(second, "Dung_Cu_Sinh_Hoat_" + index, zone, basePoint + house.forward * 24 + house.right * side * 9,
                    house.eulerAngles.y - side * 25);
            }
        }
    }

    static void DressEastCamp(Transform root)
    {
        var camp = GameObject.Find("BachDang_Living_Settlements")?.transform.Find("06_Trai_Bo_Dong");
        if (camp == null) return;
        var zone = Zone(root, "06_Trai_Bo_Dong_Hoat_Dong", camp.position, 1500);
        Vector3 center = new Vector3(1260, 0, 2240);
        var layout = new[] {
            ("Assets/Prefabs/Props/pf_halberd_01.prefab","Gia_Giao_Cong",new Vector3(1185,0,1945),15f),
            ("Assets/Prefabs/Props/pf_shield_01.prefab","Khien_Canh_Gac",new Vector3(1370,0,1960),-18f),
            ("Assets/Prefabs/Props/pf_barrels_02.prefab","Kho_Thung",new Vector3(1120,0,2460),25f),
            ("Assets/Prefabs/Props/pf_logpile_01.prefab","Cui_Bep",new Vector3(1410,0,2460),-15f),
            ("Assets/Prefabs/Props/pf_plankpile_03.prefab","Go_Sua_Trai",new Vector3(1510,0,2180),70f),
            ("Assets/Prefabs/Props/pf_buckets_01.prefab","Nuoc_Sinh_Hoat",new Vector3(1010,0,2200),10f)
        };
        foreach (var item in layout) Place(item.Item1, item.Item2, zone, item.Item3, item.Item4);
    }

    static void DressFishingLanding(Transform root)
    {
        var landing = GameObject.Find("BachDang_Living_Settlements")?.transform.Find("08_Ben_Ca_Dan_Sinh");
        if (landing == null) return;
        Vector3 center = new Vector3(55, 0, 525);
        var zone = Zone(root, "08_Ben_Ca_Ngu_Dan_Hoat_Dong", center, 1200);
        Place("Assets/Prefabs/Props/pf_barrel_fish_01.prefab", "Me_Ca_Moi", zone, center + new Vector3(-55,0,20), 18);
        Place("Assets/Prefabs/Props/pf_fish_stick_01.prefab", "Gia_Phoi_Ca", zone, center + new Vector3(-82,0,48), 90);
        Place("Assets/Prefabs/Props/pf_buckets_01.prefab", "Xo_Rua_Ca", zone, center + new Vector3(48,0,18), -15);
        Place("Assets/Prefabs/Props/pf_plankpile_01.prefab", "Van_Sua_Cau", zone, center + new Vector3(78,0,55), 82);
    }

    static void PolishWarDrum()
    {
        var drum = GameObject.Find("Minigame_TrongTran"); if (drum == null) return;
        Undo.RecordObject(drum.transform, "Shape war drum");
        // A drum lies on its side. The old upright cylinder read as a giant red barrel.
        drum.transform.rotation = Quaternion.Euler(0, 18, 90);
        drum.transform.localScale = new Vector3(8, 6, 8);
        var rim = drum.transform.Find("WoodenRim");
        if (rim != null)
        {
            Undo.RecordObject(rim, "Shape drum rim");
            rim.localPosition = Vector3.zero;
            rim.localRotation = Quaternion.identity;
            rim.localScale = new Vector3(1.08f, .92f, 1.08f);
        }
        var collider = drum.GetComponent<Collider>();
        if (collider != null) { Undo.RecordObject(collider, "Fit drum collider"); collider.isTrigger = true; }
    }

    static void OptimizeExistingDecoration(UnityEngine.SceneManagement.Scene scene)
    {
        string[] decorativeRoots = { "09_Sinh_Hoat", "10_Lang_Tu_Nhien", "11_San_Nha_Va_Cho", "13_Ruong_Vuon_Ben_Song" };
        var settlement = GameObject.Find("BachDang_Living_Settlements")?.transform;
        foreach (string name in decorativeRoots)
        {
            var group = settlement != null ? settlement.Find(name) : null; if (group == null) continue;
            // Solid scenery stays collidable; visual optimization must not remove contact.
            foreach (var renderer in group.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is SkinnedMeshRenderer || renderer.shadowCastingMode == ShadowCastingMode.Off) continue;
                Undo.RecordObject(renderer, "Disable small scenery shadow"); renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; shadowsDisabled++;
            }
        }
        // Dock props keep their physical contact surfaces as well.
        foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
        {
            if (light.gameObject.scene != scene || light.type == LightType.Directional || light.shadows == LightShadows.None) continue;
            Undo.RecordObject(light, "Optimize local light"); light.shadows = LightShadows.None;
        }
    }

    static void AuthorWaypointFacing()
    {
        foreach (var life in Object.FindObjectsByType<NPCWander>(FindObjectsInactive.Include))
        {
            if (life.waypoints == null || life.waypoints.Length < 2) continue;
            for (int i = 0; i < life.waypoints.Length; i++)
            {
                var point = life.waypoints[i]; var next = life.waypoints[(i + 1) % life.waypoints.Length];
                if (point == null || next == null) continue;
                Vector3 direction = next.position - point.position; direction.y = 0;
                if (direction.sqrMagnitude < 1) continue;
                Undo.RecordObject(point, "Face NPC workplace"); point.rotation = Quaternion.LookRotation(direction.normalized);
            }
        }
    }

    static void ConfigureTerrain()
    {
        Undo.RecordObject(terrain, "Optimize terrain draw distances");
        terrain.treeDistance = Mathf.Min(terrain.treeDistance, 1400);
        terrain.treeBillboardDistance = Mathf.Min(terrain.treeBillboardDistance, 360);
        terrain.treeCrossFadeLength = Mathf.Min(terrain.treeCrossFadeLength, 80);
        terrain.treeMaximumFullLODCount = Mathf.Min(terrain.treeMaximumFullLODCount, 900);
        terrain.detailObjectDistance = 0;
        EditorUtility.SetDirty(terrain);
    }

    static void AddExistingDecorationZones(UnityEngine.SceneManagement.Scene scene)
    {
        var settlement = GameObject.Find("BachDang_Living_Settlements")?.transform;
        if (settlement != null)
        {
            foreach (string rootName in new[] { "09_Sinh_Hoat", "10_Lang_Tu_Nhien", "11_San_Nha_Va_Cho", "13_Ruong_Vuon_Ben_Song" })
            {
                var group = settlement.Find(rootName); if (group == null) continue;
                foreach (Transform child in group)
                {
                    var renderers = child.GetComponentsInChildren<Renderer>(true); if (renderers.Length == 0) continue;
                    var bounds = renderers[0].bounds; for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                    // Very long road/shore meshes must stay active; district-sized content is streamed.
                    if (Mathf.Max(bounds.size.x, bounds.size.z) > 2600) continue;
                    zones.Add(new MapZoneStreamer.Zone { content = child.gameObject, center = bounds.center, visibleDistance = 1450, visible = true });
                }
            }
        }
        var campLife = GameObject.Find("Island_Camp_Life")?.transform;
        if (campLife != null) foreach (Transform child in campLife)
        {
            if (!child.name.StartsWith("Cum_Quan_")) continue;
            zones.Add(new MapZoneStreamer.Zone { content = child.gameObject, center = child.position, visibleDistance = 1500, visible = true });
        }
    }
}
