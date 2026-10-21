using System;
using System.IO;
using System.Linq;
using BeachBoatStory;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>One-time, scene-owned additions. Uses shared primitives/materials to fit the 16 GB laptop build.</summary>
public static class BeachBoatSupplyCampUpgrade
{
    const string ScenePath = "Assets/Scenes/beachBoat.unity";

    public static void ApplyAndExit()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Apply(scene);
        EditorApplication.Exit(0);
    }

    [MenuItem("BeachBoat/Upgrade Supply Signs, Camp Dorms and Rowboat")]
    public static void ApplyFromMenu() => Apply(SceneManager.GetActiveScene());

    public static void CaptureAndExit()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var cameraSource = UnityEngine.Object.FindFirstObjectByType<BeachBoatQuestSession>()?.storyCamera;
        var dorms = GameObject.Find("Living_Dormitories")?.transform;
        var supply = GameObject.Find("Minigame_TiepTe")?.transform;
        var boat = UnityEngine.Object.FindAnyObjectByType<QuestRowboat>();
        if (cameraSource == null || dorms == null || supply == null || boat == null) throw new InvalidOperationException("Capture targets missing.");
        var cameraObject = new GameObject("SupplyCamp_QA_Camera");
        var camera = cameraObject.AddComponent<Camera>(); camera.CopyFrom(cameraSource); camera.enabled = false;
        camera.fieldOfView = 67; camera.farClipPlane = 800;
        var render = new RenderTexture(1200, 750, 24);
        var pixels = new Texture2D(1200, 750, TextureFormat.RGB24, false);
        camera.targetTexture = render; Directory.CreateDirectory("Logs");
        void Shot(string name, Vector3 from, Vector3 to)
        {
            cameraObject.transform.position = from; cameraObject.transform.LookAt(to);
            camera.Render(); RenderTexture.active = render;
            pixels.ReadPixels(new Rect(0, 0, 1200, 750), 0, 0); pixels.Apply();
            File.WriteAllBytes("Logs/" + name + ".png", pixels.EncodeToPNG());
        }
        var west = dorms.Find("Nha_Nghi_Linh_Tay").position;
        Shot("SupplyCamp_Dorm", west + new Vector3(55, 19, 8), west + Vector3.up * 20);
        Shot("SupplyCamp_Supply", supply.position + new Vector3(0, 28, -75), supply.position + Vector3.up * 9);
        var station = supply.GetComponent<BeachBoatMinigameStation>();
        if (station != null && station.checkpointMarkers.Length > 0)
        {
            var marker = station.checkpointMarkers[0];
            marker.GetComponent<SupplyDestinationSign>()?.SetCargo("CỌC GỖ", 1);
            marker.SetActive(true);
            var point = station.checkpoints[0].position;
            Shot("SupplyCamp_Delivery", point + new Vector3(0, 24, -48), point + Vector3.up * 17);
            marker.SetActive(false);
        }
        Shot("SupplyCamp_Boat", boat.transform.position + boat.transform.rotation * new Vector3(80, 34, -95), boat.transform.position + Vector3.up * 7);
        var patrol = GameObject.Find("Thuyen_Nhu_Giac_1_TienPhong");
        if (patrol != null) Shot("SupplyCamp_Patrol", patrol.transform.position + patrol.transform.rotation * new Vector3(95, 43, -90), patrol.transform.position + Vector3.up * 9);
        RenderTexture.active = null; camera.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(render); UnityEngine.Object.DestroyImmediate(cameraObject);
        EditorApplication.Exit(0);
    }

    public static void RefineSignsAndExit()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var station = UnityEngine.Object.FindObjectsByType<BeachBoatMinigameStation>(FindObjectsSortMode.None)
            .FirstOrDefault(s => s.kind == BeachBoatMinigameStation.ActivityKind.SupplyRelay);
        if (station == null) throw new InvalidOperationException("Supply station missing.");
        Directory.CreateDirectory(".utmp/SupplyCampUpgrade");
        if (!EditorSceneManager.SaveScene(scene, ".utmp/SupplyCampUpgrade/before-sign-refine.unity", true)) throw new IOException("Backup failed.");
        for (int i = 0; i < 3; i++)
        {
            var sign = station.transform.Find("VisibleCargo_" + (i + 1))?.GetComponent<SupplyDestinationSign>();
            if (sign == null || sign.label == null) throw new InvalidOperationException("Cargo sign missing: " + i);
            SetSign(sign.label, new Vector3(0, 25, 0), 9, new Vector2(34, 34));
            sign.label.text = CargoLabel(i);
        }
        for (int i = 0; i < station.checkpointMarkers.Length; i++)
        {
            var marker = station.checkpointMarkers[i];
            var sign = marker?.GetComponent<SupplyDestinationSign>();
            if (sign == null || sign.label == null) throw new InvalidOperationException("Delivery sign missing.");
            marker.transform.localPosition = i == 0 ? new Vector3(30, 0, 0) : Vector3.zero;
            sign.board = marker.transform.Find("SignBoard");
            SetSign(sign.label, new Vector3(0, 19, -1.7f), 20, new Vector2(130, 60));
            EditorUtility.SetDirty(sign);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Save failed.");
        EditorApplication.Exit(0);
    }

    public static void AuditPatrolAndExit()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var patrols = UnityEngine.Object.FindObjectsByType<AmbientRiverBoat>(FindObjectsSortMode.None);
        var data = patrols.Select(p =>
        {
            var hull = p.transform.Find("DaiVietPatrolVisual_Rebuilt");
            var renderers = hull == null ? Array.Empty<Renderer>() : hull.GetComponentsInChildren<Renderer>(true);
            var bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds();
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return new { p.name, rootScale = p.transform.localScale.ToString("F2"), visual = hull?.name,
                visualScale = hull == null ? "missing" : hull.localScale.ToString("F2"),
                hullSize = bounds.size.ToString("F1"), crew = p.GetComponentsInChildren<Animator>(true)
                    .Where(a => a.isHuman).Select(a => new { a.name, parent = a.transform.parent.name,
                        localPosition = a.transform.localPosition.ToString("F2"), localScale = a.transform.localScale.ToString("F2") }).ToArray(),
                p.visualForwardYaw };
        }).ToArray();
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/SupplyCampPatrolAudit.json", Newtonsoft.Json.JsonConvert.SerializeObject(data, Newtonsoft.Json.Formatting.Indented));
        EditorApplication.Exit(0);
    }

    public static void EnlargePatrolAndExit()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var boats = UnityEngine.Object.FindObjectsByType<AmbientRiverBoat>(FindObjectsSortMode.None);
        if (boats.Length != 5 || boats.Any(b => b.transform.Find("DaiVietPatrolVisual_Rebuilt") == null))
            throw new InvalidOperationException("Expected five rebuilt friendly boats.");
        Directory.CreateDirectory(".utmp/SupplyCampUpgrade");
        if (!EditorSceneManager.SaveScene(scene, ".utmp/SupplyCampUpgrade/before-patrol-scale.unity", true))
            throw new IOException("Patrol backup failed.");
        foreach (var boat in boats)
        {
            var visual = boat.transform.Find("DaiVietPatrolVisual_Rebuilt");
            float factor = boat.name == "Thuyen_Tuan_Tieu_Ben_Doanh_Trai" ? 1.55f : 1.8f;
            visual.localScale *= factor;
            // The imported crew is a sibling of the hull. Move its anchors onto the
            // enlarged deck without enlarging the people themselves.
            foreach (var crew in boat.GetComponentsInChildren<Animator>(true).Where(a => a.isHuman))
                if (crew.transform.parent == boat.transform) crew.transform.localPosition *= factor;
            EditorUtility.SetDirty(boat);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Patrol scene save failed.");
        EditorApplication.Exit(0);
    }

    static string CargoLabel(int cargo) => cargo == 0 ? "1  CỌC\nGỖ" : cargo == 1 ? "2  LƯƠNG\nKHÔ" : "3  DÂY\nTHỪNG";

    static void SetSign(TextMeshPro label, Vector3 localPosition, int fontSize, Vector2 dimensions)
    {
        label.transform.localPosition = localPosition;
        label.transform.localRotation = Quaternion.identity;
        label.fontSize = fontSize;
        label.rectTransform.sizeDelta = dimensions;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        EditorUtility.SetDirty(label);
    }

    static void Apply(Scene scene)
    {
        if (Application.isPlaying || scene.path != ScenePath) throw new InvalidOperationException("Open beachBoat in Edit Mode.");
        var station = UnityEngine.Object.FindObjectsByType<BeachBoatMinigameStation>(FindObjectsSortMode.None)
            .FirstOrDefault(s => s.kind == BeachBoatMinigameStation.ActivityKind.SupplyRelay);
        var camp = GameObject.Find("TestQuest_Camp")?.transform.Find("Compact_Living_Camp");
        var boat = UnityEngine.Object.FindAnyObjectByType<QuestRowboat>();
        var terrain = Terrain.activeTerrain;
        if (station == null || camp == null || boat == null || terrain == null) throw new InvalidOperationException("Supply, camp, boat or terrain is missing.");
        if (station.checkpoints == null || station.checkpoints.Length != 4 ||
            station.checkpointMarkers == null || station.checkpointMarkers.Length != 4)
            throw new InvalidOperationException("Expected four existing supply checkpoints.");
        if (camp.Find("Living_Dormitories") != null) throw new InvalidOperationException("Upgrade already exists; refusing to duplicate it.");
        var tents = camp.Find("Barracks_Rows");
        if (tents == null || tents.Find("Barracks_Tent_05") == null || tents.Find("Barracks_Tent_13") == null)
            throw new InvalidOperationException("The two replaceable camp tents are missing.");
        var hull = boat.transform.Find("Thuyen_Go_Song");
        if (hull == null) throw new InvalidOperationException("The polished rowing hull is missing.");

        Directory.CreateDirectory(".utmp/SupplyCampUpgrade");
        if (!EditorSceneManager.SaveScene(scene, ".utmp/SupplyCampUpgrade/before-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity", true))
            throw new IOException("Scene backup failed.");

        var timber = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Buildings/Materials/mat_building_01.mat");
        var roof = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Buildings/Materials/mat_strawroof_01.mat");
        var earth = AssetDatabase.LoadAssetAtPath<Material>("Assets/BachDangSettlements/Village_Packed_Earth.mat");
        if (timber == null || roof == null || earth == null) throw new InvalidOperationException("Shared building materials are missing.");
        var rope = Material("Supply_Rope", new Color(.56f, .41f, .22f));
        var food = Material("Supply_Rations", new Color(.62f, .55f, .35f));
        var stake = Material("Supply_Stakes", new Color(.35f, .24f, .12f));

        BuildSupply(station, timber, rope, food, stake);
        BuildDormitories(camp, tents, terrain, timber, roof, earth);
        // Scale the visual hull alone: seat, paddle and camera remain at their tested positions.
        hull.localScale = new Vector3(1.18f, 1.06f, 1.18f);
        boat.hullProbeSpacing = 31f;
        boat.hullProbeRadius = 15f;
        EditorUtility.SetDirty(boat);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Scene save failed.");
        Debug.Log("Supply signs, two enterable dormitories, four sleeping soldiers, and larger quest hull saved to beachBoat.");
    }

    static Material Material(string name, Color color)
    {
        const string folder = "Assets/BeachBoatStory/SceneryMaterials";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/BeachBoatStory", "SceneryMaterials");
        string path = folder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
        material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", .08f); material.enableInstancing = true;
        EditorUtility.SetDirty(material); return material;
    }

    static GameObject Box(string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool solid = false)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
        go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        if (!solid) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static GameObject Cylinder(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); go.name = name;
        go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>()); return go;
    }

    static TextMeshPro Label(string name, Transform parent, Vector3 position, string message, int size)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = position;
        var text = go.AddComponent<TextMeshPro>(); StoryFont.Apply(text);
        text.text = message; text.fontSize = size; text.alignment = TextAlignmentOptions.Center;
        text.rectTransform.sizeDelta = new Vector2(110, 40);
        text.transform.localRotation = Quaternion.identity;
        text.color = new Color(1f, .87f, .55f);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    static void BuildSupply(BeachBoatMinigameStation station, Material wood, Material rope, Material food, Material stake)
    {
        var cargoMaterials = new[] { stake, food, rope };
        for (int i = 0; i < 3; i++)
        {
            var crate = station.transform.Find("SupplyCrate_" + (i + 1));
            if (crate == null) throw new InvalidOperationException("Supply crate " + (i + 1) + " missing.");
            var root = new GameObject("VisibleCargo_" + (i + 1)).transform;
            root.SetParent(station.transform, false); root.localPosition = crate.localPosition;
            var label = Label("CargoName", root, new Vector3(0, 25, 0), CargoLabel(i), 9);
            label.rectTransform.sizeDelta = new Vector2(34, 34);
            root.gameObject.AddComponent<SupplyDestinationSign>().label = label;
            if (i == 0)
            {
                for (int n = 0; n < 3; n++)
                {
                    var log = Cylinder("Cọc gỗ bó", root, new Vector3((n - 1) * 2.5f, 8, 0), new Vector3(1.1f, 5.5f, 1.1f), cargoMaterials[i]);
                    log.transform.localRotation = Quaternion.Euler(90, 0, 0);
                }
            }
            else if (i == 1)
                for (int n = -1; n <= 1; n += 2) Box("Bao lương khô", root, new Vector3(n * 2.7f, 7.5f, 0), new Vector3(4.5f, 5, 4), cargoMaterials[i]);
            else
                for (int n = -1; n <= 1; n += 2)
                {
                    var coil = Cylinder("Cuộn dây thừng", root, new Vector3(n * 3.2f, 7.2f, 0), new Vector3(2.5f, .8f, 2.5f), cargoMaterials[i]);
                    coil.transform.localRotation = Quaternion.Euler(90, 0, 0);
                }
        }
        for (int i = 0; i < station.checkpointMarkers.Length; i++)
        {
            var old = station.checkpointMarkers[i];
            var point = station.checkpoints[i];
            var marker = new GameObject("Supply_Delivery_Sign_" + (i + 1));
            marker.transform.SetParent(point, false); marker.transform.localPosition = i == 0 ? new Vector3(30, 0, 0) : Vector3.zero;
            Cylinder("WoodenPost", marker.transform, new Vector3(0, 8, 0), new Vector3(1.5f, 8, 1.5f), wood);
            var board = Box("SignBoard", marker.transform, new Vector3(0, 19, 0), new Vector3(43, 14, 2), wood);
            var label = Label("DeliveryText", marker.transform, new Vector3(0, 19, -1.7f), "ĐIỂM GIAO", 20);
            label.rectTransform.sizeDelta = new Vector2(130, 60);
            var sign = marker.AddComponent<SupplyDestinationSign>(); sign.label = label; sign.board = board.transform;
            marker.SetActive(false);
            station.checkpointMarkers[i] = marker;
            UnityEngine.Object.DestroyImmediate(old);
        }
        EditorUtility.SetDirty(station);
    }

    static void BuildDormitories(Transform camp, Transform tents, Terrain terrain, Material timber, Material roof, Material earth)
    {
        var root = new GameObject("Living_Dormitories").transform; root.SetParent(camp, false);
        var source = GameObject.Find("Camp_Worker_01");
        if (source == null || source.GetComponent<NPCWander>() == null) throw new InvalidOperationException("Camp soldier source missing.");
        foreach (int side in new[] { -1, 1 })
        {
            var tent = tents.Find(side < 0 ? "Barracks_Tent_05" : "Barracks_Tent_13");
            var center = tent.position;
            center.y = terrain.SampleHeight(center) + terrain.transform.position.y;
            var house = new GameObject(side < 0 ? "Nha_Nghi_Linh_Tay" : "Nha_Nghi_Linh_Dong").transform;
            house.SetParent(root, false); house.position = center;
            Box("Nền đất nện", house, new Vector3(0, .12f, 0), new Vector3(64, .2f, 64), earth);
            float frontX = -side * 32;
            Box("Vách sau", house, new Vector3(side * 32, 20, 0), new Vector3(3, 40, 64), timber, true);
            Box("Vách trái", house, new Vector3(0, 20, -32), new Vector3(64, 40, 3), timber, true);
            Box("Vách phải", house, new Vector3(0, 20, 32), new Vector3(64, 40, 3), timber, true);
            Box("Vách cửa trái", house, new Vector3(frontX, 20, -21), new Vector3(3, 40, 22), timber, true);
            Box("Vách cửa phải", house, new Vector3(frontX, 20, 21), new Vector3(3, 40, 22), timber, true);
            Box("Xà trên cửa", house, new Vector3(frontX, 35, 0), new Vector3(3, 10, 22), timber, true);
            var leftRoof = Box("Mái tranh trái", house, new Vector3(0, 46, -17), new Vector3(70, 3, 40), roof);
            leftRoof.transform.localRotation = Quaternion.Euler(-34, 0, 0);
            var rightRoof = Box("Mái tranh phải", house, new Vector3(0, 46, 17), new Vector3(70, 3, 40), roof);
            rightRoof.transform.localRotation = Quaternion.Euler(34, 0, 0);
            for (int bed = 0; bed < 2; bed++)
            {
                int zSide = bed == 0 ? -1 : 1;
                Box("Chiếu ngủ " + (bed + 1), house, new Vector3(side * 7, .3f, zSide * 15), new Vector3(24, .35f, 10), roof);
                Box("Gối gỗ " + (bed + 1), house, new Vector3(side * 17, .7f, zSide * 15), new Vector3(3, .8f, 9), timber);
            }
            Box("Bàn sinh hoạt", house, new Vector3(side * 18, 4, 0), new Vector3(10, 1, 10), timber);
            for (int soldier = 0; soldier < 2; soldier++)
            {
                var actor = UnityEngine.Object.Instantiate(source, root);
                actor.name = "Linh_Nghi_Ca_" + (side < 0 ? "Tay_" : "Dong_") + (soldier + 1);
                var outside = center + new Vector3(-side * 50, 0, soldier == 0 ? -4 : 4);
                outside.y = terrain.SampleHeight(outside) + terrain.transform.position.y + .2f;
                actor.transform.position = outside;
                actor.transform.rotation = Quaternion.Euler(0, side < 0 ? 90 : 270, 0);
                var route = new GameObject("Tuyến gác và ngủ " + (soldier + 1)).transform;
                route.SetParent(root, false);
                var stop = new GameObject("Cửa nhà").transform; stop.SetParent(route, false);
                var mat = new GameObject("Chiếu nghỉ").transform; mat.SetParent(route, false);
                stop.position = outside;
                var inside = center + new Vector3(side * 7, 0, soldier == 0 ? -8 : 8);
                inside.y = terrain.SampleHeight(inside) + terrain.transform.position.y + .2f;
                mat.position = inside; mat.rotation = Quaternion.Euler(0, side < 0 ? 90 : 270, 0);
                var wander = actor.GetComponent<NPCWander>();
                wander.enabled = true; wander.autoGenerateWaypoints = false;
                wander.waypoints = new[] { stop, mat };
                wander.activityLabels = new[] { "Đang đổi ca gác", "Đang ngủ sau ca trực" };
                wander.walkSpeed = 4.3f; wander.minIdleTime = 3; wander.maxIdleTime = 7;
                var dialogue = actor.GetComponent<NPCDialogue>();
                if (dialogue != null) { dialogue.speakerName = "Quân sĩ"; dialogue.dialogueText = "Tôi vừa đổi ca. Anh em có chỗ nghỉ để chuẩn bị cho trận đánh."; }
            }
            UnityEngine.Object.DestroyImmediate(tent.gameObject);
        }
    }
}
