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

public static class BeachBoatRiverGameplaySetup
{
    const string RootName = "BeachBoat_RiverLife_Optimized";
    static Terrain terrain;
    static TideSystem tide;
    static int props;

    [MenuItem("BeachBoat/Polish River Gameplay")]
    public static void Apply()
    {
        var scene = BeachBoatToolSafety.Begin("RiverGameplay");
        terrain = Terrain.activeTerrain; tide = Object.FindAnyObjectByType<TideSystem>();
        var zone = Object.FindAnyObjectByType<BeachBoatStory.RiverStakeZone>();
        if (terrain == null || tide == null || zone == null || zone.fieldStakes.Length != 48 || zone.placements.Length != 5)
            throw new InvalidOperationException("Expected terrain, tide and the complete five-cluster stake field.");
        var old = GameObject.Find(RootName); if (old != null) Undo.DestroyObjectImmediate(old);
        var root = new GameObject(RootName); Undo.RegisterCreatedObjectUndo(root, "Create river life"); props = 0;

        ShapeHistoricalStakeBarriers(zone);
        SetupAmbientBoats(root.transform);
        AddWorkingWaterfronts(root.transform);
        BeachBoatToolSafety.Save(scene);
        // Re-run the idempotent systems after stake relocation so shore actors and enemy targets
        // use the final authored positions.
        BeachBoatLivingWorldSetup.Setup();
        BeachBoatBattleSetup.SetupBattle();

        Directory.CreateDirectory("Logs");
        var moving = Object.FindObjectsByType<AmbientRiverBoat>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        File.WriteAllText("Logs/RiverGameplayResult.json", JsonConvert.SerializeObject(new {
            success = moving.Length == 4 && zone.placements.All(p => p.stakeIndices.Length >= 9),
            movingBoats = moving.Length, riverProps = props, stakeClusters = zone.placements.Select(p => p.stakeIndices.Length).ToArray(),
            note = "Five staggered cross-channel barriers, forward-only patrol motion, animated crew and streamed shared-prefab waterfront clutter."
        }, Formatting.Indented));
    }

    public static void ApplyAndExit()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity", OpenSceneMode.Single);
        Apply(); EditorApplication.Exit(0);
    }

    static void ShapeHistoricalStakeBarriers(BeachBoatStory.RiverStakeZone zone)
    {
        var stakes = zone.fieldStakes.Where(s => s != null).OrderBy(s => s.name, StringComparer.Ordinal).ToArray();
        int cursor = 0; int[] counts = { 10, 10, 10, 9, 9 };
        float[] bands = { 1390, 1725, 2060, 2395, 2730 };
        for (int g = 0; g < 5; g++)
        {
            int count = counts[g]; var indices = new int[count];
            for (int n = 0; n < count; n++)
            {
                var stake = stakes[cursor]; indices[n] = cursor;
                float t = count <= 1 ? .5f : n / (float)(count - 1);
                float x = Mathf.Lerp(-2040, -1640, t);
                float z = bands[g] + Mathf.Abs(t - .5f) * 145f + (n % 2 == 0 ? -24 : 24);
                MoveStake(stake, new Vector3(x, 0, z)); cursor++;
            }
            var placement = zone.placements[g]; placement.stakeIndices = indices; placement.stake = stakes[indices[count / 2]];
            // Keep the interaction boat in a clear eastern navigation lane, outside the barrier itself.
            Vector3 approach = FindWetApproach(new Vector3(-1480, tide.lowTideY, bands[g] + 45));
            Undo.RecordObject(placement.approach, "Move stake work approach"); placement.approach.position = approach;
            Undo.RecordObject(placement.marker, "Reset stake marker"); placement.marker.SetActive(false);
            var label = placement.marker.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (label != null) { Undo.RecordObject(label, "Label stake barrier"); label.text = $"DẢI CỌC {g + 1} • {count} CỌC • E"; }
        }
        Undo.RecordObject(zone, "Shape historical stake barriers"); zone.fieldStakes = stakes; zone.interactDistance = 42; zone.plantingDuration = 3;
        EditorUtility.SetDirty(zone);
    }

    static void MoveStake(Transform stake, Vector3 targetCenter)
    {
        stake.gameObject.SetActive(true); var b = BoundsOf(stake);
        Vector3 delta = targetCenter - b.center; delta.y = 0; Undo.RecordObject(stake, "Move stake into channel barrier"); stake.position += delta;
        b = BoundsOf(stake); float bottom = Ground(stake.position).y - 8f; stake.position += Vector3.up * (bottom - b.min.y);
        stake.gameObject.SetActive(false);
    }

    static Vector3 FindWetApproach(Vector3 preferred)
    {
        for (int ring = 0; ring < 12; ring++)
        {
            for (int side = 0; side < 2; side++)
            {
                float x = preferred.x + (side == 0 ? 1 : -1) * ring * 35;
                var p = new Vector3(x, tide.lowTideY + 1, preferred.z);
                bool wet = true;
                foreach (float dz in new[] { -28f, 0f, 28f }) if (Ground(p + Vector3.forward * dz).y + 8 > tide.lowTideY) wet = false;
                if (wet) return p;
            }
        }
        throw new InvalidOperationException("Could not find a low-tide navigation lane beside the stake field.");
    }

    static void SetupAmbientBoats(Transform root)
    {
        string[] names = { "Thuyen_Nhu_Giac_1_TienPhong", "Thuyen_Nhu_Giac_2_HuuDuc", "Thuyen_Mai_Phuc_Rach_Bac", "Thuyen_Mai_Phuc_Rach_Nam" };
        var rowing = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animation/NPC_Rowing_Fix.controller");
        var safeSeated = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/NPC_Sitting.controller");
        for (int i = 0; i < names.Length; i++)
        {
            var boat = GameObject.Find(names[i]); if (boat == null) throw new InvalidOperationException("Missing ambient boat " + names[i]);
            foreach (var old in boat.GetComponents<AmbientRiverBoat>()) Undo.DestroyObjectImmediate(old);
            var floating = boat.GetComponent<TidalBoatFloat>(); if (floating != null) { Undo.RecordObject(floating, "Hand boat motion to patrol"); floating.enabled = false; }
            foreach (var c in boat.GetComponentsInChildren<Collider>(true)) { Undo.RecordObject(c, "Disable ambient boat collision"); c.enabled = false; }
            var routeRoot = new GameObject("Tuyen_" + names[i]).transform; routeRoot.SetParent(root, false);
            Vector3 p = boat.transform.position; Vector3 f = boat.transform.forward; f.y = 0; f.Normalize(); Vector3 r = Vector3.Cross(Vector3.up, f);
            Vector3[] offsets = { f * 25, f * 115 + r * 35, f * 190, f * 100 - r * 38 };
            var route = new Transform[4];
            for (int n = 0; n < 4; n++) { var wp = new GameObject("Diem_" + (n + 1)).transform; wp.SetParent(routeRoot, false); wp.position = WetOrOriginal(p + offsets[n], p); route[n] = wp; }
            var patrol = Undo.AddComponent<AmbientRiverBoat>(boat); patrol.tide = tide; patrol.route = route; patrol.cruiseSpeed = 11 + i; patrol.turnSpeed = 20; patrol.pauseAtPoint = 1.5f;
            patrol.visualForwardYaw = HullRunsAlongLocalX(boat.transform) ? 90f : 0f;
            var crew = boat.GetComponentsInChildren<Animator>(true).Where(a => a.isHuman).Take(2).ToArray(); patrol.rowers = crew.Take(1).ToArray();
            if (crew.Length > 0) { Undo.RecordObject(crew[0], "Use rowing animation"); var repaired = boat.transform.Find("DaiVietPatrolVisual_Rebuilt") != null; var controller = repaired ? safeSeated : rowing; if (controller != null) crew[0].runtimeAnimatorController = controller; crew[0].applyRootMotion = false; crew[0].cullingMode = AnimatorCullingMode.CullUpdateTransforms; }
            if (crew.Length > 1) { var lookout = crew[1].GetComponent<BoatCrewLookout>(); if (lookout == null) lookout = Undo.AddComponent<BoatCrewLookout>(crew[1].gameObject); crew[1].cullingMode = AnimatorCullingMode.CullUpdateTransforms; }
            EditorUtility.SetDirty(patrol);
        }
        var dockBoat = GameObject.Find("Thuyen_Tuan_Tieu_Ben_Doanh_Trai");
        if (dockBoat != null)
        {
            var crew = dockBoat.GetComponentsInChildren<Animator>(true).Where(a => a.isHuman).ToArray();
            foreach (var actor in crew) if (actor.GetComponent<BoatCrewLookout>() == null) Undo.AddComponent<BoatCrewLookout>(actor.gameObject);
        }
    }

    static Vector3 WetOrOriginal(Vector3 candidate, Vector3 fallback)
    {
        candidate.y = tide.lowTideY + (fallback.y - tide.transform.position.y);
        return Ground(candidate).y + 7 < tide.lowTideY ? candidate : new Vector3(fallback.x, candidate.y, fallback.z);
    }

    static void AddWorkingWaterfronts(Transform root)
    {
        var dock = new GameObject("Ben_Ca_Xo_Bo_Co_Kiem_Soat").transform; dock.SetParent(root, false);
        Vector3 c = new Vector3(55, 0, 525);
        dock.position = Ground(c);
        var layout = new[] {
            ("pf_barrel_fish_01","Me_Ca_1",new Vector3(-105,0,15),12f),("pf_barrel_fish_01","Me_Ca_2",new Vector3(95,0,28),-16f),
            ("pf_fish_stick_01","Gia_Phoi_Ca_1",new Vector3(-145,0,72),88f),("pf_fish_stick_01","Gia_Phoi_Ca_2",new Vector3(138,0,68),94f),
            ("pf_buckets_01","Xo_Rua_Ca",new Vector3(-48,0,-18),5f),("pf_barrels_01","Thung_Muoi",new Vector3(58,0,-30),25f),
            ("pf_plankpile_01","Van_Sua_Thuyen",new Vector3(178,0,16),82f),("pf_logpile_01","Cui_Hun_Ca",new Vector3(-188,0,22),-10f),
            ("pf_scaffold_01","Gia_Sua_Luoi",new Vector3(212,0,86),90f),("pf_shed_01","Mai_Che_Ngu_Dan",new Vector3(-228,0,105),18f),
            ("pf_fish_01","Ca_Moi_1",new Vector3(-12,0,35),35f),("pf_fish_01","Ca_Moi_2",new Vector3(15,0,42),-20f)
        };
        foreach (var item in layout) Place("Assets/Prefabs/Props/" + item.Item1 + ".prefab", item.Item2, dock, c + item.Item3, item.Item4);
        // Reuse two existing bird flocks rather than adding per-animal AI or new textures.
        var birds = Object.FindObjectsByType<BirdFlight>(FindObjectsInactive.Include, FindObjectsSortMode.None).Take(2).ToArray();
        foreach (var bird in birds) { Undo.RecordObject(bird, "Widen bird activity over settlements"); bird.orbitRadius = Mathf.Max(bird.orbitRadius, 320); bird.orbitHeight = Mathf.Max(bird.orbitHeight, 150); }
        RegisterStreaming(dock.gameObject);
    }

    static void Place(string path, string name, Transform parent, Vector3 p, float yaw)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (prefab == null) return;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent); go.name = name; go.transform.position = Ground(p); go.transform.rotation = Quaternion.Euler(0, yaw, 0); go.transform.localScale *= 7;
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        foreach (var b in go.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(b);
        foreach (var l in go.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l);
        foreach (var r in go.GetComponentsInChildren<Renderer>(true)) { r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; }
        props++;
    }

    static void RegisterStreaming(GameObject root)
    {
        var streamer = Object.FindAnyObjectByType<MapZoneStreamer>(); if (streamer == null) return;
        Undo.RecordObject(streamer, "Stream river life");
        var list = streamer.zones.Where(z => z != null && z.content != null && z.content != root).ToList();
        list.Add(new MapZoneStreamer.Zone { content = root, center = root.transform.position, visibleDistance = 1700, visible = true });
        streamer.zones = list.ToArray(); EditorUtility.SetDirty(streamer);
    }

    static Vector3 Ground(Vector3 p) { p.y = terrain.SampleHeight(p) + terrain.transform.position.y; return p; }
    static Bounds BoundsOf(Transform root)
    {
        bool any = false; Bounds result = default;
        foreach (var r in root.GetComponentsInChildren<Renderer>(true)) { if (!any) { result = r.bounds; any = true; } else result.Encapsulate(r.bounds); }
        if (!any) throw new InvalidOperationException("Stake renderer missing."); return result;
    }
    static bool HullRunsAlongLocalX(Transform root)
    {
        bool any = false; Bounds local = default;
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            var b = renderer.bounds;
            for (int n = 0; n < 8; n++)
            {
                Vector3 world = b.center + Vector3.Scale(b.extents, new Vector3((n & 1) == 0 ? -1 : 1, (n & 2) == 0 ? -1 : 1, (n & 4) == 0 ? -1 : 1));
                Vector3 p = root.InverseTransformPoint(world);
                if (!any) { local = new Bounds(p, Vector3.zero); any = true; } else local.Encapsulate(p);
            }
        }
        return any && local.size.x > local.size.z * 1.2f;
    }
}
