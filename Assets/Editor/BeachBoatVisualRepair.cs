using System;
using System.IO;
using System.Linq;
using BeachBoatStory;
using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Focused visual audit and repair for the beachBoat camp and river.</summary>
public static class BeachBoatVisualRepair
{
    const string ScenePath = "Assets/Scenes/beachBoat.unity";
    const string TentMeshPath = "Assets/BeachBoatStory/Camp/CanvasFieldTent.asset";
    const string TentMaterialPath = "Assets/BeachBoatStory/Camp/CanvasFieldTent.mat";

    static Bounds BoundsOf(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) throw new InvalidOperationException("No renderer: " + root.name);
        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    static float Ground(Terrain terrain, Vector3 position) => terrain.SampleHeight(position) + terrain.transform.position.y;

    static void Seat(GameObject go, Terrain terrain, Vector3 position)
    {
        go.transform.position = new Vector3(position.x, Ground(terrain, position), position.z);
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
            go.transform.position += Vector3.up * (Ground(terrain, position) + .12f - renderers.Min(r => r.bounds.min.y));
    }

    sealed class TentMeshBuilder
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<int>[] triangles = { new List<int>(), new List<int>() };
        public void Triangle(Vector3 a, Vector3 b, Vector3 c, int material)
        {
            int i = vertices.Count;
            // Give front and back separate vertices. Shared opposite-facing triangles
            // cancel their calculated normals and make URP light the canvas black.
            vertices.AddRange(new[] { a, b, c, a, c, b });
            var normal = Vector3.Cross(b - a, c - a).normalized;
            Vector2 Map(Vector3 p)
            {
                if (Mathf.Abs(normal.y) >= Mathf.Abs(normal.x) && Mathf.Abs(normal.y) >= Mathf.Abs(normal.z))
                    return new Vector2(p.x / 8, p.z / 8);
                if (Mathf.Abs(normal.x) >= Mathf.Abs(normal.z)) return new Vector2(p.z / 8, p.y / 8);
                return new Vector2(p.x / 8, p.y / 8);
            }
            var ua = Map(a); var ub = Map(b); var uc = Map(c);
            uv.AddRange(new[] { ua, ub, uc, ua, uc, ub });
            triangles[material].AddRange(new[] { i, i + 1, i + 2, i + 3, i + 4, i + 5 });
        }
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int material)
        {
            Triangle(a, b, c, material); Triangle(a, c, d, material);
        }
        public void Beam(Vector3 a, Vector3 b, float width, int material)
        {
            var side = Vector3.Cross((b - a).normalized, Vector3.up);
            if (side.sqrMagnitude < .01f) side = Vector3.right;
            side = side.normalized * width * .5f;
            var front = Vector3.Cross((b - a).normalized, side).normalized * width * .5f;
            Quad(a - side - front, b - side - front, b + side - front, a + side - front, material);
            Quad(a - side + front, a + side + front, b + side + front, b - side + front, material);
            Quad(a - side - front, a - side + front, b - side + front, b - side - front, material);
            Quad(a + side - front, b + side - front, b + side + front, a + side + front, material);
        }
        public Mesh Make()
        {
            var mesh = new Mesh { name = "Canvas_Field_Tent", subMeshCount = 2 };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv);
            for (int i = 0; i < 2; i++) mesh.SetTriangles(triangles[i], i);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
    }

    static Mesh GetTentMesh()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(TentMeshPath);
        var b = new TentMeshBuilder();
        const float x = 18, z = 24, wall = 7, ridge = 22;
        // A closed A-frame with low canvas walls and two clean roof slopes.
        b.Quad(new Vector3(-x, wall, -z), new Vector3(0, ridge, -z), new Vector3(0, ridge, z), new Vector3(-x, wall, z), 0);
        b.Quad(new Vector3(0, ridge, -z), new Vector3(x, wall, -z), new Vector3(x, wall, z), new Vector3(0, ridge, z), 0);
        foreach (int side in new[] { -1, 1 })
        {
            float sx = side * x;
            b.Quad(new Vector3(sx, .3f, -z), new Vector3(sx, wall, -z), new Vector3(sx, wall, z), new Vector3(sx, .3f, z), 0);
        }
        foreach (int end in new[] { -1, 1 })
        {
            float ez = end * z;
            b.Triangle(new Vector3(-x, wall, ez), new Vector3(0, ridge, ez), new Vector3(x, wall, ez), 0);
            b.Quad(new Vector3(-x, .3f, ez), new Vector3(-x, wall, ez), new Vector3(x, wall, ez), new Vector3(x, .3f, ez), 0);
        }
        // A dark doorway and supporting poles give the tent readable scale from ground level.
        b.Quad(new Vector3(-5.4f, .35f, z + .12f), new Vector3(-5.4f, 12.5f, z + .12f),
            new Vector3(5.4f, 12.5f, z + .12f), new Vector3(5.4f, .35f, z + .12f), 1);
        foreach (float ez in new[] { -z, z }) b.Beam(new Vector3(0, .1f, ez), new Vector3(0, ridge + .6f, ez), .9f, 1);
        b.Beam(new Vector3(0, ridge, -z), new Vector3(0, ridge, z), .75f, 1);
        var mesh = b.Make();
        if (existing == null) { AssetDatabase.CreateAsset(mesh, TentMeshPath); return mesh; }
        EditorUtility.CopySerialized(mesh, existing);
        UnityEngine.Object.DestroyImmediate(mesh);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    public static void RepairTentLightingAndExit()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GetTentMesh(); GetCanvasMaterial(); AssetDatabase.SaveAssets();
        EditorApplication.Exit(0);
    }

    static Material GetCanvasMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(TentMaterialPath);
        var straw = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Buildings/Materials/mat_strawroof_01.mat");
        if (material == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader unavailable.");
            material = new Material(shader) { name = "Canvas_Field_Tent" };
            AssetDatabase.CreateAsset(material, TentMaterialPath);
        }
        material.SetColor("_BaseColor", new Color(.82f, .74f, .59f));
        material.SetFloat("_Smoothness", .08f);
        if (straw != null) material.SetTexture("_BaseMap", straw.GetTexture("_BaseMap"));
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    static void BuildFence(Transform parent, GameObject sample, Terrain terrain, Vector3 origin, Vector3 gate)
    {
        var corners = new[] { origin + new Vector3(-330, 0, -285), origin + new Vector3(330, 0, -285),
            origin + new Vector3(330, 0, 375), origin + new Vector3(-330, 0, 375) };
        for (int i = 0; i < corners.Length; i++) corners[i].y = Ground(terrain, corners[i]);
        int count = 0;
        for (int edge = 0; edge < 4; edge++)
        {
            var a = corners[edge]; var b = corners[(edge + 1) % 4];
            var direction = (b - a).normalized; float length = Vector3.Distance(a, b);
            int steps = Mathf.CeilToInt(length / 14f);
            for (int i = 0; i < steps; i++)
            {
                var p = a + direction * ((i + .5f) * length / steps);
                if (Vector3.Distance(p, gate) < 35) continue;
                var panel = UnityEngine.Object.Instantiate(sample, parent);
                panel.name = "Camp_Palisade_" + (count++).ToString("000");
                panel.transform.rotation = Quaternion.LookRotation(direction);
                foreach (var collider in panel.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
                Seat(panel, terrain, p);
            }
        }
        var segments = new[] { (corners[0], corners[1]), (corners[1], corners[2]),
            (corners[2], corners[3]), (corners[3], gate + Vector3.forward * 35),
            (gate + Vector3.back * 35, corners[0]) };
        for (int i = 0; i < segments.Length; i++)
        {
            var a = segments[i].Item1; var b = segments[i].Item2; var d = b - a;
            var barrier = new GameObject("Camp_Barrier_" + i); barrier.transform.SetParent(parent, false);
            barrier.transform.SetPositionAndRotation((a + b) * .5f + Vector3.up * 18, Quaternion.LookRotation(d));
            barrier.AddComponent<BoxCollider>().size = new Vector3(5, 38, d.magnitude);
        }
    }

    public static void ApplyAndExit()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode before repairing beachBoat.");
        var camp = GameObject.Find("TestQuest_Camp");
        var root = camp?.transform.Find("Compact_Living_Camp");
        var terrain = Terrain.activeTerrain;
        if (root == null || terrain == null) throw new InvalidOperationException("Compact camp or terrain missing.");
        var stage = camp.transform.Cast<Transform>().First(t => t.name.Contains("Rustic_Wooden_Stage"));
        var origin = stage.position;
        Directory.CreateDirectory(".utmp/BeachBoatVisualRepair");
        EditorSceneManager.SaveScene(scene, ".utmp/BeachBoatVisualRepair/before.unity", true);
        var rows = root.Find("Barracks_Rows");
        if (rows == null) throw new InvalidOperationException("Barracks_Rows missing.");
        foreach (Transform child in rows.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
        var tentMesh = GetTentMesh(); var canvas = GetCanvasMaterial();
        var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Buildings/Materials/mat_building_01.mat");
        if (wood == null) throw new InvalidOperationException("Shared timber material missing.");
        int index = 0;
        foreach (int side in new[] { -1, 1 })
        foreach (float z in new[] { -210f, -140f, -70f, 0f, 70f, 140f, 210f, 280f })
        {
            var go = new GameObject("Barracks_Tent_" + (++index).ToString("00")); go.transform.SetParent(rows, false);
            go.transform.rotation = Quaternion.Euler(0, side < 0 ? 90 : -90, 0);
            go.AddComponent<MeshFilter>().sharedMesh = tentMesh;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterials = new[] { canvas, wood };
            var collider = go.AddComponent<BoxCollider>(); collider.center = new Vector3(0, 9, 0);
            collider.size = new Vector3(35, 18, 46);
            Seat(go, terrain, origin + new Vector3(side * 210, 0, z));
        }
        var oldFence = root.Find("Camp_Palisade_Reused");
        var sample = oldFence?.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault()?.gameObject;
        if (sample == null) throw new InvalidOperationException("Original fence sample missing.");
        var enlarged = new GameObject("Camp_Palisade_Enlarged").transform; enlarged.SetParent(root, false);
        var gatePosition = origin + new Vector3(-330, 0, 150); gatePosition.y = Ground(terrain, gatePosition);
        BuildFence(enlarged, sample, terrain, origin, gatePosition);
        UnityEngine.Object.DestroyImmediate(oldFence.gameObject);
        enlarged.name = "Camp_Palisade_Reused";
        foreach (var name in new[] { "CampToVillage", "CampToRiver" })
        {
            var gate = GameObject.Find(name);
            if (gate == null) continue;
            gate.transform.position = gatePosition;
            var transition = gate.GetComponent<BeachBoatStory.CampGateTransition>();
            if (transition != null) transition.interactDistance = 28;
            var arch = gate.transform.Find("Visible_Story_Gate");
            if (arch == null) continue;
            if (name == "CampToRiver") { arch.gameObject.SetActive(false); continue; }
            arch.localScale *= 1.25f;
            Seat(arch.gameObject, terrain, gatePosition);
        }
        var tide = UnityEngine.Object.FindFirstObjectByType<TideSystem>();
        if (tide == null) throw new InvalidOperationException("TideSystem missing.");
        var soldierMaterial = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Model_NPC/OldSoldier/Meshy_AI_Dũng_the_Veteran_biped/Materials/Meshy_AI_Dũng_the_Veteran_biped_texture_0.mat");
        if (soldierMaterial == null || soldierMaterial.GetTexture("_BaseMap") == null)
            throw new InvalidOperationException("Textured patrol soldier material missing.");
        int crewFixed = 0, patrolsRaised = 0;
        foreach (var patrol in UnityEngine.Object.FindObjectsByType<AmbientRiverBoat>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var hull = patrol.transform.Find("DaiVietPatrolVisual_Rebuilt");
            if (hull == null) throw new InvalidOperationException("Patrol hull missing on " + patrol.name);
            var bounds = BoundsOf(hull.gameObject);
            patrol.transform.position += Vector3.up * (tide.transform.position.y - 3.2f - bounds.min.y);
            patrol.waterlineOffset = patrol.transform.position.y - tide.transform.position.y;
            patrolsRaised++;
            foreach (var soldier in patrol.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                soldier.sharedMaterials = Enumerable.Repeat(soldierMaterial, soldier.sharedMaterials.Length).ToArray();
                crewFixed++;
            }
        }
        var rowboat = UnityEngine.Object.FindFirstObjectByType<QuestRowboat>();
        if (rowboat == null || rowboat.dockPoint == null || rowboat.boardingPoint == null)
            throw new InvalidOperationException("Quest rowboat bindings missing.");
        // The old parking point put most of this 80 m hull on top of the wooden pier.
        // Keep its dock marker and shore boarding marker so Act 2/3 interactions still work.
        rowboat.transform.position += Vector3.right * 65f;
        if (!rowboat.AtDock) throw new InvalidOperationException("Quest rowboat moved outside docking range.");
        Physics.SyncTransforms(); AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Logs/BeachBoatVisualRepair.json", JsonConvert.SerializeObject(new {
            scene = scene.path, tents = rows.childCount, fencePanels = enlarged.GetComponentsInChildren<MeshRenderer>().Length,
            gate = gatePosition.ToString("F1"), gateBounds = BoundsOf(GameObject.Find("CampToVillage").transform.Find("Visible_Story_Gate").gameObject).size.ToString("F1"),
            campRenderers = camp.GetComponentsInChildren<Renderer>().Length,
            patrolsRaised, crewFixed, questBoat = rowboat.transform.position.ToString("F1")
        }, Formatting.Indented));
        EditorApplication.Exit(0);
    }

    public static void AuditAndExit()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var tide = UnityEngine.Object.FindFirstObjectByType<TideSystem>();
        var patrols = UnityEngine.Object.FindObjectsByType<AmbientRiverBoat>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var enemies = UnityEngine.Object.FindObjectsByType<EnemyShip>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var rowboat = UnityEngine.Object.FindFirstObjectByType<QuestRowboat>();
        var tent = GameObject.Find("Barracks_Tent_01");
        var gate = GameObject.Find("CampToVillage");
        var report = new {
            scene = SceneManager.GetActiveScene().path,
            tideY = tide == null ? float.NaN : tide.transform.position.y,
            tideLow = tide == null ? float.NaN : tide.lowTideY,
            tideHigh = tide == null ? float.NaN : tide.highTideY,
            tent = tent == null ? null : new { position = tent.transform.position.ToString("F2"), bounds = BoundsOf(tent).ToString(), scale = tent.transform.localScale.ToString("F2") },
            gate = gate == null ? null : new { position = gate.transform.position.ToString("F2"), arch = gate.transform.Find("Visible_Story_Gate") == null ? "missing" : BoundsOf(gate.transform.Find("Visible_Story_Gate").gameObject).ToString() },
            patrols = patrols.Select(p => {
                var visual = p.transform.Find("DaiVietPatrolVisual_Rebuilt");
                return new { p.name, position = p.transform.position.ToString("F2"), rootScale = p.transform.lossyScale.ToString("F2"), p.waterlineOffset,
                    hull = visual == null ? "missing" : BoundsOf(visual.gameObject).ToString(),
                    crew = p.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(r => new { r.name, bounds = r.bounds.ToString(),
                        materials = r.sharedMaterials.Select(m => new { name = m == null ? "null" : m.name,
                            path = AssetDatabase.GetAssetPath(m), shader = m == null ? "null" : m.shader?.name,
                            texture = m != null && m.HasProperty("_BaseMap") ? AssetDatabase.GetAssetPath(m.GetTexture("_BaseMap")) : "none" }).ToArray() }).ToArray() };
            }).ToArray(),
            enemies = enemies.Take(2).Select(e => new { e.name, position = e.transform.position.ToString("F2"),
                hull = e.transform.Find("RedSailJunkVisual") == null ? "missing" : BoundsOf(e.transform.Find("RedSailJunkVisual").gameObject).ToString() }).ToArray(),
            rowboat = rowboat == null ? null : new { position = rowboat.transform.position.ToString("F2"),
                rotation = rowboat.transform.rotation.eulerAngles.ToString("F1"), scale = rowboat.transform.lossyScale.ToString("F2"),
                dock = rowboat.dockPoint == null ? "missing" : rowboat.dockPoint.position.ToString("F2"),
                boarding = rowboat.boardingPoint == null ? "missing" : rowboat.boardingPoint.position.ToString("F2"),
                seat = rowboat.seat == null ? "missing" : rowboat.seat.position.ToString("F2"),
                children = rowboat.transform.Cast<Transform>().Select(t => new { t.name, position = t.position.ToString("F1"),
                    bounds = t.GetComponentsInChildren<Renderer>(true).Length == 0 ? "none" : BoundsOf(t.gameObject).ToString() }).ToArray(),
                hull = rowboat.transform.Find("Thuyen_Go_Song") == null ? "missing" : BoundsOf(rowboat.transform.Find("Thuyen_Go_Song").gameObject).ToString(),
                nearby = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .Where(r => r.GetComponent<Terrain>() == null && !r.transform.IsChildOf(rowboat.transform) &&
                        Vector3.Distance(r.bounds.center, rowboat.transform.position) < 130)
                    .OrderBy(r => Vector3.Distance(r.bounds.center, rowboat.transform.position)).Take(30)
                    .Select(r => new { r.name, bounds = r.bounds.ToString() }).ToArray() }
        };
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/BeachBoatVisualAudit.json", JsonConvert.SerializeObject(report, Formatting.Indented));
        EditorApplication.Exit(0);
    }

    public static void CaptureAndExit()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var source = UnityEngine.Object.FindFirstObjectByType<BeachBoatQuestSession>()?.storyCamera;
        if (source == null) throw new InvalidOperationException("Story camera missing.");
        var cameraObject = UnityEngine.Object.Instantiate(source.gameObject);
        var listener = cameraObject.GetComponent<AudioListener>(); if (listener != null) listener.enabled = false;
        var camera = cameraObject.GetComponent<Camera>(); camera.enabled = false;
        camera.clearFlags = CameraClearFlags.Skybox; camera.fieldOfView = 64; camera.farClipPlane = 1500;
        var rt = new RenderTexture(1400, 850, 24);
        var texture = new Texture2D(1400, 850, TextureFormat.RGB24, false);
        camera.targetTexture = rt;
        void Shot(string file, Vector3 position, Vector3 lookAt)
        {
            cameraObject.transform.position = position; cameraObject.transform.LookAt(lookAt);
            camera.Render(); RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, 1400, 850), 0, 0); texture.Apply();
            File.WriteAllBytes("Logs/" + file, texture.EncodeToPNG());
        }
        Directory.CreateDirectory("Logs");
        var camp = GameObject.Find("TestQuest_Camp");
        var stage = camp.transform.Cast<Transform>().First(t => t.name.Contains("Rustic_Wooden_Stage"));
        var origin = stage.position;
        Shot("BeachBoat_Camp_Expanded.png", origin + new Vector3(0, 420, 470), origin + new Vector3(0, 0, 35));
        var tent = GameObject.Find("Barracks_Tent_01");
        Shot("BeachBoat_Tent_Repaired.png", tent.transform.position + new Vector3(-62, 21, 34),
            tent.transform.position + Vector3.up * 12);
        var patrol = UnityEngine.Object.FindObjectsByType<AmbientRiverBoat>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .First(p => p.name.Contains("Doanh_Trai"));
        Shot("BeachBoat_Patrol_Afloat.png", patrol.transform.position + new Vector3(110, 38, 125),
            patrol.transform.Find("DaiVietPatrolVisual_Rebuilt").GetComponentInChildren<Renderer>().bounds.center);
        var rowboat = UnityEngine.Object.FindFirstObjectByType<QuestRowboat>();
        Shot("BeachBoat_QuestBoat_Dock.png", rowboat.transform.position + new Vector3(-35, 46, 100),
            rowboat.transform.position + Vector3.up * 5);
        RenderTexture.active = null; camera.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(texture);
        UnityEngine.Object.DestroyImmediate(cameraObject);
        EditorApplication.Exit(0);
    }

    public static void PlaytestAndExit()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        VisualPlayProbe.EnsureRegistered();
        SessionState.SetBool("BeachBoatVisualPlayProbe", true);
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoad]
    static class VisualPlayProbe
    {
        static double started;
        static int phase;
        static float initialHeading;
        static string error;
        static VisualPlayProbe() { EditorApplication.update += Tick; }
        internal static void EnsureRegistered() { }
        static void Finish(bool success)
        {
            var boat = UnityEngine.Object.FindFirstObjectByType<QuestRowboat>();
            var tide = UnityEngine.Object.FindFirstObjectByType<TideSystem>();
            var patrols = UnityEngine.Object.FindObjectsByType<AmbientRiverBoat>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var session = BeachBoatQuestSession.Instance;
            var report = new {
                success, error, water = TideSystem.CurrentWaterHeight,
                tideTarget = tide == null ? float.NaN : tide.TargetWaterHeight,
                aboard = boat != null && boat.IsAboard,
                boatAtDock = boat != null && boat.AtDock,
                boatHullBottom = boat == null ? float.NaN : BoundsOf(boat.transform.Find("Thuyen_Go_Song").gameObject).min.y,
                boatHeading = boat == null ? float.NaN : boat.transform.eulerAngles.y,
                headingChange = boat == null ? float.NaN : Mathf.Abs(Mathf.DeltaAngle(initialHeading, boat.transform.eulerAngles.y)),
                playerSeatDistance = session == null || boat == null ? float.NaN : Vector3.Distance(session.player.transform.position, boat.seat.position),
                patrols = patrols.Select(p => new { p.name,
                    hullBottom = BoundsOf(p.transform.Find("DaiVietPatrolVisual_Rebuilt").gameObject).min.y,
                    texturedCrew = p.GetComponentsInChildren<SkinnedMeshRenderer>(true).All(r => r.sharedMaterials.All(m => m != null && m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null)) }).ToArray()
            };
            File.WriteAllText("Logs/BeachBoatVisualPlaytest.json", JsonConvert.SerializeObject(report, Formatting.Indented));
            if (success && session?.storyCamera != null)
            {
                var camera = session.storyCamera; var rt = new RenderTexture(1280, 720, 24);
                var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
                File.WriteAllBytes("Logs/BeachBoat_Quest3Camera_Play.png", texture.EncodeToPNG());
                RenderTexture.active = null; camera.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(texture);
            }
            SessionState.SetBool("BeachBoatVisualPlayProbe", false);
            EditorApplication.Exit(success ? 0 : 1);
        }
        static void Tick()
        {
            if (!SessionState.GetBool("BeachBoatVisualPlayProbe", false) || !EditorApplication.isPlaying) return;
            if (started == 0) started = EditorApplication.timeSinceStartup;
            double elapsed = EditorApplication.timeSinceStartup - started;
            var session = BeachBoatQuestSession.Instance;
            var story = BeachBoatStory.BachDangStoryManager.Instance;
            var boat = UnityEngine.Object.FindFirstObjectByType<QuestRowboat>();
            if (elapsed > 20) { error = "Playtest timed out"; Finish(false); return; }
            if (session == null || story == null || boat == null) return;
            if (phase == 0 && elapsed > 2)
            {
                UnityEngine.Object.FindFirstObjectByType<BeachBoatStory.OpeningEvent>()?.SkipOpening();
                BeachBoatStory.StoryDialogueUI.Instance?.Close();
                story.collectedWood = story.totalWoodRequired;
                story.TransitionTo(BeachBoatStory.BachDangStoryManager.StoryState.Act2_PlantStakesAtRiver, true);
                var p = boat.boardingPoint.position;
                session.player.transform.position = p;
                var body = session.player.GetComponent<Rigidbody>();
                if (body != null) { body.position = p; body.linearVelocity = Vector3.zero; }
                Physics.SyncTransforms();
                phase = 1;
            }
            if (phase == 1 && elapsed > 3)
            {
                initialHeading = boat.transform.eulerAngles.y;
                if (!boat.TryBoard())
                {
                    error = $"Quest boat refused boarding: state={story.currentState}, wood={story.collectedWood}, " +
                        $"atDock={boat.AtDock}, blocked={BeachBoatQuestSession.InteractionBlocked}";
                    Finish(false); return;
                }
                boat.UseSoldierBattleCamera();
                phase = 2;
            }
            if (phase == 2 && elapsed > 4)
            {
                // Enter the high-water moment used when the enemy fleet first arrives.
                // The synthetic test bypasses Act 2 reporting, so release its lock explicitly.
                boat.tide?.ReleaseLowTide(boat);
                story.TransitionTo(BeachBoatStory.BachDangStoryManager.StoryState.Act3_WaitForLowTide, true);
                phase = 3;
            }
            if (phase == 3 && elapsed > 6)
            {
                var patrols = UnityEngine.Object.FindObjectsByType<AmbientRiverBoat>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                bool afloat = patrols.All(p => Mathf.Abs(BoundsOf(p.transform.Find("DaiVietPatrolVisual_Rebuilt").gameObject).min.y -
                    (TideSystem.CurrentWaterHeight - 3.2f)) < 1.5f);
                bool crew = patrols.All(p => p.GetComponentsInChildren<SkinnedMeshRenderer>(true).All(r =>
                    r.sharedMaterials.All(m => m != null && m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null)));
                bool seat = Vector3.Distance(session.player.transform.position, boat.seat.position) < 1;
                bool heading = Mathf.Abs(Mathf.DeltaAngle(initialHeading, boat.transform.eulerAngles.y)) < 1;
                bool boatFloat = Mathf.Abs(BoundsOf(boat.transform.Find("Thuyen_Go_Song").gameObject).min.y -
                    (TideSystem.CurrentWaterHeight - 5f)) < 2;
                error = afloat && crew && seat && heading && boatFloat ? null :
                    $"afloat={afloat}, texturedCrew={crew}, seated={seat}, headingStable={heading}, boatFloats={boatFloat}";
                Finish(error == null);
            }
        }
    }
}
