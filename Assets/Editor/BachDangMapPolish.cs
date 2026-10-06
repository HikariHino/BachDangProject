using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

/// <summary>Scoped, repeatable finishing pass for the existing battlefield.</summary>
public static class BachDangMapPolish
{
    const string ScenePath = "Assets/Scenes/beachBoat.unity";
    const string ArtPath = "Assets/BachDangAtmosphere";
    const string RootName = "BachDang_Atmosphere_And_Shore_Detail";

    [MenuItem("Bach Dang/Polish Current Battlefield")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != ScenePath)
            throw new InvalidOperationException("Open beachBoat in Edit Mode before applying the finishing pass.");
        var terrain = Terrain.activeTerrain;
        var water = GameObject.Find("Plane");
        if (terrain == null || water == null || Camera.main == null)
            throw new InvalidOperationException("Terrain, Plane and Main Camera are required.");

        // Save a recoverable copy, including any unsaved scene work, before mutation.
        Directory.CreateDirectory(".utmp/MapPolish");
        var backup = ".utmp/MapPolish/beachBoat-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity";
        if (!EditorSceneManager.SaveScene(scene, backup, true))
            throw new IOException("Could not back up the current scene.");
        if (!AssetDatabase.IsValidFolder(ArtPath)) AssetDatabase.CreateFolder("Assets", "BachDangAtmosphere");

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Bach Dang atmosphere and shoreline pass");
        var root = GameObject.Find(RootName);
        if (root == null)
        {
            root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Add atmosphere root");
        }

        SetupAtmosphere(root);
        SetupWater(water);
        SetupBoats(water.GetComponent<TideSystem>());
        SetupDock(root.transform, terrain);
        SetupShoreline(root.transform, terrain);
        BachDangNpcIdleSetup.Apply();
        SetupCamera();

        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene);
        Undo.CollapseUndoOperations(undoGroup);
        Debug.Log("Bach Dang finishing pass saved. Scene backup: " + backup);
    }

    static Material SceneMaterial(string name, Material source)
    {
        string path = ArtPath + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(source) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        Undo.RecordObject(material, "Tune battlefield material");
        return material;
    }

    static void SetupAtmosphere(GameObject root)
    {
        var dynamicCycle = Object.FindAnyObjectByType<DayNightCycle>();
        bool hasDynamicSky = dynamicCycle != null && dynamicCycle.isActiveAndEnabled && dynamicCycle.skyboxMaterial != null;
        if (!hasDynamicSky)
        {
        var sky = SceneMaterial("BachDang_MorningSky", RenderSettings.skybox);
        sky.SetFloat("_Exposure", 1.05f);
        sky.SetColor("_Tint", new Color(0.52f, 0.52f, 0.52f));
        RenderSettings.skybox = sky;
        EditorUtility.SetDirty(sky);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 380f;
        RenderSettings.fogEndDistance = 4600f;
        RenderSettings.fogColor = new Color(0.64f, 0.72f, 0.73f);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.70f, 0.76f, 0.80f);
        RenderSettings.ambientEquatorColor = new Color(0.50f, 0.54f, 0.48f);
        RenderSettings.ambientGroundColor = new Color(0.31f, 0.34f, 0.28f);

        var sun = RenderSettings.sun;
        if (sun != null)
        {
            Undo.RecordObjects(new Object[] { sun, sun.transform }, "Tune morning sunlight");
            sun.transform.rotation = Quaternion.Euler(34f, 235f, 0f);
            sun.color = new Color(1f, 0.92f, 0.79f);
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
            sun.shadowBias = 0.03f;
            sun.shadowNormalBias = 0.25f;
        }
        }
        else dynamicCycle.ApplyTimeOfDay();

        var volume = root.GetComponent<Volume>();
        if (volume == null) volume = Undo.AddComponent<Volume>(root);
        Undo.RecordObject(volume, "Set scene grading");
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ArtPath + "/BachDang_Morning.asset");
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ArtPath + "/BachDang_Morning.asset");
        }
        var tone = Effect<Tonemapping>(profile);
        tone.mode.Override(TonemappingMode.ACES);
        var grade = Effect<ColorAdjustments>(profile);
        grade.postExposure.Override(0.4f);
        grade.contrast.Override(5f);
        grade.saturation.Override(-6f);
        var bloom = Effect<Bloom>(profile);
        bloom.intensity.Override(0.12f);
        bloom.threshold.Override(1.15f);
        bloom.scatter.Override(0.55f);
        var vignette = Effect<Vignette>(profile);
        vignette.intensity.Override(0.12f);
        vignette.smoothness.Override(0.5f);
        volume.isGlobal = true;
        volume.priority = 5;
        volume.sharedProfile = profile;
        EditorUtility.SetDirty(profile);
        DynamicGI.UpdateEnvironment();
    }

    static T Effect<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (!profile.TryGet<T>(out var effect))
        {
            effect = profile.Add<T>(true);
            AssetDatabase.AddObjectToAsset(effect, profile);
        }
        Undo.RecordObject(effect, "Tune grading");
        EditorUtility.SetDirty(effect);
        return effect;
    }

    static void SetupWater(GameObject water)
    {
        var renderer = water.GetComponent<Renderer>();
        var mat = SceneMaterial("BachDang_River", renderer.sharedMaterial);
        mat.SetColor("_WaterColor", new Color(0.35f, 0.52f, 0.40f, 1f));
        mat.SetColor("_DeepWaterColor", new Color(0.22f, 0.40f, 0.36f, 1f));
        // This scene uses the sky/probe fallback, without a planar reflection camera.
        mat.SetFloat("_PlanarReflection", 0f);
        mat.SetFloat("_ReflectionIntensity", 0.65f);
        mat.SetFloat("_ShallowDeepBlendDepth", 10f);
        mat.SetFloat("_NormalStrength", 0.3f);
        mat.SetFloat("_NormalBlend", 0.5f);
        mat.SetFloat("_NormalWorldScale", 14f);
        mat.SetFloat("_FresnelBias", 0.04f);
        mat.SetFloat("_FresnelPower", 4f);
        mat.SetFloat("_WaveAmplitude", 0.3f);
        mat.SetFloat("_WaveSpeed", 0.3f);
        mat.SetFloat("_CrestGlowIntensity", 0.08f);
        mat.SetFloat("_CrestGlowPower", 3f);
        mat.SetFloat("_DeepFoamIntensity", 0f);
        mat.SetFloat("_FoamIntensity", 3f);
        mat.SetFloat("_FoamShorelineBoost", 0.65f);
        mat.SetFloat("_ShorelineAlphaFalloff", 2f);
        mat.SetFloat("_ShorelineDepthFade", 3f);
        mat.SetFloat("_ShoreWaveFoamStrength", 6f);
        mat.SetFloat("_ShoreWaveFoamTexTiling", 14f);
        mat.SetFloat("_ShoreWaveFoamMaskFloor", 0.12f);
        mat.SetFloat("_ShoreWaveFoamMaskPower", 1.5f);
        mat.SetFloat("_ShoreWaveRange", 3f);
        mat.SetFloat("_ShoreWaveFrequency", 0.7f);
        mat.SetFloat("_ShoreWaveNormalStrength", 0.15f);
        mat.SetFloat("_ShoreWaveMix", 0f);
        mat.SetFloat("_SunGlitterStrength", 0.55f);
        mat.SetFloat("_SunGlitterSparkle", 0.6f);
        mat.SetFloat("_SpecularIntensity", 0.45f);
        mat.SetFloat("_AlphaFullDepth", 1.8f);
        mat.SetFloat("_AlphaFalloffPower", 0.7f);
        mat.SetFloat("_WaterSurfaceHeight", water.transform.position.y);
        Undo.RecordObject(renderer, "Assign river material");
        renderer.sharedMaterial = mat;
        EditorUtility.SetDirty(mat);
        // Height tracking is separate from OptiWater's mesh-generating controller.
        if (water.GetComponent<TidalWaterSurface>() == null) Undo.AddComponent<TidalWaterSurface>(water);
    }

    static void SetupBoats(TideSystem tide)
    {
        var fleet = GameObject.Find("--- HẠM ĐỘI THUYỀN CHIẾN ĐẠI VIỆT (938) ---");
        if (fleet == null || tide == null) return;
        foreach (Transform boat in fleet.transform)
        {
            // The source building prefabs are static, but tide-driven instances move.
            foreach (Transform part in boat.GetComponentsInChildren<Transform>(true))
            {
                Undo.RecordObject(part.gameObject, "Allow boat tide movement");
                GameObjectUtility.SetStaticEditorFlags(part.gameObject, 0);
            }
            var floating = boat.GetComponent<TidalBoatFloat>();
            if (floating == null) floating = Undo.AddComponent<TidalBoatFloat>(boat.gameObject);
            Undo.RecordObjects(new Object[] { floating, boat }, "Fit boat hull to waterline");
            floating.tideSystem = tide;
            // These imported hull vertices begin about 0.49 m ABOVE the root pivot.
            // Fit the actual keel, rather than assuming root Y is the waterline.
            var hull = Array.Find(boat.GetComponentsInChildren<MeshRenderer>(), r => r.name == "build_boat_01");
            if (hull != null)
            {
                boat.position += Vector3.up * (tide.transform.position.y - 0.4f - hull.bounds.min.y);
                Physics.SyncTransforms();
                var hullCollider = hull.GetComponent<Collider>();
                if (hullCollider != null)
                    foreach (var crew in boat.GetComponentsInChildren<Animator>())
                    {
                        var skins = crew.GetComponentsInChildren<SkinnedMeshRenderer>();
                        if (skins.Length == 0) continue;
                        if (!hullCollider.Raycast(new Ray(crew.transform.position + Vector3.up * 15f,
                            Vector3.down), out var hit, 30f)) continue;
                        float feetY = float.PositiveInfinity;
                        foreach (var skin in skins) feetY = Mathf.Min(feetY, skin.bounds.min.y);
                        Undo.RecordObject(crew.transform, "Place crew feet on deck");
                        crew.transform.position += Vector3.up * (hit.point.y + 0.02f - feetY);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(crew.transform);
                    }
            }
            floating.CaptureWaterline();
            PrefabUtility.RecordPrefabInstancePropertyModifications(boat);
            EditorUtility.SetDirty(floating);
        }
    }

    static void SetupCamera()
    {
        var cam = Camera.main;
        Undo.RecordObjects(new Object[] { cam, cam.transform }, "Frame battlefield");
        cam.fieldOfView = 55f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 9500f;
        cam.allowHDR = true;
        var data = cam.GetUniversalAdditionalCameraData();
        Undo.RecordObject(data, "Enable scene grading and antialiasing");
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        data.antialiasingQuality = AntialiasingQuality.High;
        data.requiresDepthTexture = true;
        var director = cam.GetComponent<BattleCamera>();
        if (director != null)
        {
            Undo.RecordObject(director, "Open in spectator view");
            director.SetFreeFlyPose(new Vector3(-690f, 76f, 45f), new Vector3(-345f, 14f, 325f));
        }
    }

    static void SetupDock(Transform root, Terrain terrain)
    {
        var boat = GameObject.Find("Thuyen_Tuan_Tieu_Ben_Doanh_Trai");
        if (boat != null && terrain.SampleHeight(boat.transform.position) + terrain.transform.position.y > 8.5f)
        {
            Undo.RecordObject(boat.transform, "Move buried patrol boat into the river");
            boat.transform.position = new Vector3(-503f, boat.transform.position.y, 255f);
        }
        if (root.Find("Ben_Go_Ven_Song") != null) return;
        var camp = GameObject.Find("--- CHIẾN TRƯỜNG: ĐẠI BẢN DOANH & XƯỞNG CỌC BẠCH ĐẰNG ---");
        var walls = camp != null ? camp.transform.Find("1_DaiBanDoanh_NgoQuyen/Tuong_Go_Doanh_Trai") : null;
        var wallRenderer = walls != null ? walls.GetComponentInChildren<Renderer>() : null;
        if (wallRenderer == null) return;
        Material timber = wallRenderer.sharedMaterial;
        var group = new GameObject("Ben_Go_Ven_Song");
        Undo.RegisterCreatedObjectUndo(group, "Build river landing");
        group.transform.SetParent(root, false);
        // Existing plank-path prefab meshes have a baked offset hundreds of metres
        // from their pivots. Keep those instances for recovery but hide the misplaced copies.
        var oldPaths = camp.transform.Find("3_BenThuyen_Va_DuongGo");
        if (oldPaths != null)
            foreach (Transform child in oldPaths)
                if (child.name == "pf_plankpath_01")
                {
                    Undo.RecordObject(child.gameObject, "Hide displaced boardwalk mesh");
                    child.gameObject.SetActive(false);
                }
        for (int i = 0; i < 81; i++)
        {
            float x = -548f + i * 0.48f;
            float ground = terrain.SampleHeight(new Vector3(x, 0f, 250f)) + terrain.transform.position.y;
            float deck = Mathf.Max(ground + 0.16f, Mathf.Lerp(17.4f, 15.8f, i / 80f));
            var plank = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(plank, "Add dock plank");
            plank.name = "Van_Ben_" + i.ToString("D2");
            plank.transform.SetParent(group.transform, false);
            plank.transform.position = new Vector3(x, deck, 250f);
            plank.transform.localScale = new Vector3(0.46f, 0.14f, 3f + Mathf.Sin(i * 4.7f) * 0.06f);
            plank.GetComponent<Renderer>().sharedMaterial = timber;
            if (i % 10 != 0) continue;
            for (int side = -1; side <= 1; side += 2)
            {
                var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Undo.RegisterCreatedObjectUndo(post, "Add mooring pile");
                post.name = "Tru_Neo_" + i + "_" + side;
                post.transform.SetParent(group.transform, false);
                float baseY = terrain.SampleHeight(new Vector3(x, 0, 250f + side * 1.35f)) + terrain.transform.position.y - 0.2f;
                float topY = deck + 0.65f;
                post.transform.position = new Vector3(x, (baseY + topY) * 0.5f, 250f + side * 1.35f);
                post.transform.localScale = new Vector3(0.28f, (topY - baseY) * 0.5f, 0.28f);
                post.GetComponent<Renderer>().sharedMaterial = timber;
            }
        }
    }

    static void SetupShoreline(Transform root, Terrain terrain)
    {
        // An existing pass is left intact: rerunning never duplicates foliage.
        if (root.Find("Shoreline_Undergrowth") != null) return;
        var group = new GameObject("Shoreline_Undergrowth");
        Undo.RegisterCreatedObjectUndo(group, "Add shoreline detail");
        group.transform.SetParent(root, false);
        var fern = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TerrainSampleAssets/Prefabs/Fern_A.prefab");
        var grass = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TerrainSampleAssets/Prefabs/Grass_B.prefab");
        var bush = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TerrainSampleAssets/Prefabs/Bush_A.prefab");
        var rock = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Visual Design Cafe/Nature Renderer Demo/Realistic/Art/Rocks/Rock_A_02.prefab");
        var random = new System.Random(938);
        int placed = 0;
        for (int i = 0; i < 3800 && placed < 280; i++)
        {
            float x = -950f + (float)random.NextDouble() * 1000f;
            float z = 60f + (float)random.NextDouble() * 520f;
            var pos = new Vector3(x, 0, z);
            float y = terrain.SampleHeight(pos) + terrain.transform.position.y;
            // Only dry shoreline; keep workshop, paths and moorings unobstructed.
            if (y < 15.7f || y > 18.0f) continue;
            if (Mathf.Abs(z - 260f) < 16f && x < -570f) continue;
            if (Mathf.Abs(z - 320f) < 14f && x < -750f) continue;
            if (Vector2.Distance(new Vector2(x, z), new Vector2(-820, 320)) < 50f) continue;
            var size = terrain.terrainData.size;
            float slope = terrain.terrainData.GetSteepness((x - terrain.transform.position.x) / size.x,
                (z - terrain.transform.position.z) / size.z);
            if (slope > 28f) continue;
            // Clustered patches with open sand between them.
            if (Mathf.PerlinNoise(x * 0.034f + 50f, z * 0.034f) < 0.5f) continue;
            var prefab = placed % 11 == 0 ? rock : placed % 4 == 0 ? bush : placed % 3 == 0 ? fern : grass;
            if (prefab == null) continue;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group.transform);
            Undo.RegisterCreatedObjectUndo(instance, "Plant shoreline patch");
            bool isRock = prefab == rock;
            instance.transform.position = new Vector3(x, y - (isRock ? 0.25f : 0.03f), z);
            instance.transform.rotation = Quaternion.Euler(0, (float)random.NextDouble() * 360f, 0);
            instance.transform.localScale = Vector3.one * (isRock ? 0.55f : 1.1f + (float)random.NextDouble() * 0.9f);
            placed++;
        }
        Debug.Log("Added shoreline plants/rocks: " + placed);
    }
}
