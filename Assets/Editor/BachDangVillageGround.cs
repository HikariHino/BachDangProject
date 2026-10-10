using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Creates owned terrain-conforming village overlays and combined grass geometry.</summary>
public sealed class BachDangVillageGround
{
    const string LayerFolder = "Assets/TerrainSampleAssets/TerrainLayers/";
    const string GrassFolder = "Assets/TerrainSampleAssets/";
    readonly Terrain terrain;
    readonly float factor;
    readonly string folder;
    readonly List<string> assets;
    readonly Material[] grassMaterials = new Material[2];
    readonly Mesh[] grassSources = new Mesh[2];
    int serial;

    public Material Earth { get; }
    public Material Mud { get; }
    public Material Pebbles { get; }
    /// <summary>Optional clearance test receiving a grounded world center and maximum world half-width.</summary>
    public Func<Vector3, float, bool> PathPointAllowed { get; set; }
    public Func<Vector3, Vector3, float, bool> PathSegmentAllowed { get; set; }

    public BachDangVillageGround(Terrain terrain, float factor, string ownedFolder, List<string> generatedAssets)
    {
        if (terrain == null || terrain.terrainData == null || !Finite(factor) || factor <= 0)
            throw new ArgumentException("Ground painter requires a terrain and positive finite reference scale.");
        if (generatedAssets == null) throw new ArgumentNullException(nameof(generatedAssets));
        if (string.IsNullOrWhiteSpace(ownedFolder)) throw new ArgumentException("An owned asset folder is required.");
        string normalized = ownedFolder.Replace('\\', '/').TrimEnd('/');
        if (!normalized.StartsWith("Assets/", StringComparison.Ordinal) || normalized.Contains("..") ||
            !AssetDatabase.IsValidFolder(normalized)) throw new DirectoryNotFoundException(normalized);
        this.terrain = terrain; this.factor = factor; folder = normalized; assets = generatedAssets;
        Earth = GroundMaterial("Village_Natural_Earth", "Soil_Rocks_TerrainLayer.terrainlayer", new Color(1.55f, 1.32f, .97f, .92f));
        Mud = GroundMaterial("Village_Natural_Mud", "Muddy_TerrainLayer.terrainlayer", new Color(.88f, .77f, .61f, .76f));
        Pebbles = GroundMaterial("Village_Natural_Pebbles", "Pebbles_B_TerrainLayer.terrainlayer", new Color(.96f, .93f, .83f, .64f));
    }

    /// <summary>Points are world positions; width and sampling interval are reference units.</summary>
    public Transform CreatePath(Transform parent, string name, Vector3[] worldPoints, float widthCanonical,
        Material material, int seed, List<Bounds> clearings)
    {
        Validate(parent, material, clearings, widthCanonical);
        if (worldPoints == null || worldPoints.Length < 2) throw new ArgumentException("A path requires at least two world points.");
        var controls = new List<Vector3>();
        foreach (var point in worldPoints)
        {
            CheckPoint(point);
            var flat = new Vector3(point.x, 0, point.z);
            if (controls.Count == 0 || (flat - controls[controls.Count - 1]).sqrMagnitude > .0001f * factor * factor)
                controls.Add(flat);
        }
        if (controls.Count < 2) throw new ArgumentException("Path points must not all coincide.");
        var samples = SmoothSamples(controls);
        float maximumHalfWidth = widthCanonical * factor * 1.1f * .5f;
        if (!PathSamplesAllowed(samples, maximumHalfWidth))
        {
            // A smoothed corner can cut across a house even when the caller's control route is clear.
            // Preserve the supplied route and check it more densely before creating geometry or assets.
            samples = LinearSamples(controls);
            if (!PathSamplesAllowed(samples, maximumHalfWidth))
                throw new InvalidOperationException("Village path failed the clearance check, including its maximum width: " + name);
        }
        var vertices = new List<Vector3>(samples.Count * 4);
        var uv = new List<Vector2>(samples.Count * 4);
        var colors = new List<Color>(samples.Count * 4);
        var triangles = new List<int>((samples.Count - 1) * 18);
        var boundsToClear = new List<Bounds>(samples.Count - 1);
        float travelled = 0;
        Vector3[] previousBand = null;
        float[] bands = { -.5f, -.29f, .29f, .5f };
        float noiseSeed = (seed & 0xffff) * .0137f;
        for (int row = 0; row < samples.Count; row++)
        {
            if (row > 0) travelled += Vector3.Distance(samples[row - 1], samples[row]) / factor;
            var forward = samples[Mathf.Min(row + 1, samples.Count - 1)] - samples[Mathf.Max(row - 1, 0)];
            forward.y = 0;
            if (forward.sqrMagnitude < .000001f) forward = Vector3.forward;
            forward.Normalize();
            var side = new Vector3(-forward.z, 0, forward.x);
            float variation = 1 + (Mathf.PerlinNoise(noiseSeed, travelled * .14f) * 2 - 1) * .1f;
            float width = widthCanonical * factor * variation;
            var worldBand = new Vector3[4];
            for (int band = 0; band < 4; band++)
            {
                var world = OnGround(samples[row] + side * (width * bands[band]), .075f);
                worldBand[band] = world;
                vertices.Add(parent.InverseTransformPoint(world));
                uv.Add(UV(world));
                float endpoint = row == 0 || row == samples.Count - 1 ? .45f : 1f;
                colors.Add(new Color(1, 1, 1, band == 0 || band == 3 ? 0 : endpoint));
            }
            if (row > 0)
            {
                int before = (row - 1) * 4, current = row * 4;
                for (int band = 0; band < 3; band++)
                    triangles.AddRange(new[] { before + band, before + band + 1, current + band,
                        before + band + 1, current + band + 1, current + band });
                var bounds = new Bounds(worldBand[0], Vector3.zero);
                bounds.Encapsulate(worldBand[3]); bounds.Encapsulate(previousBand[0]); bounds.Encapsulate(previousBand[3]);
                bounds.Expand(new Vector3(.25f, .15f, .25f) * factor); boundsToClear.Add(bounds);
            }
            previousBand = worldBand;
        }
        var mesh = MakeMesh(name, vertices, uv, colors, triangles);
        var result = MeshObject(parent, name, mesh, material, false);
        clearings.AddRange(boundsToClear);
        return result;
    }

    /// <summary>Grounded oval with a subtly irregular edge and three alpha-feathered rings.</summary>
    public Transform CreatePatch(Transform parent, string name, Vector3 worldCenter, float yaw,
        float widthCanonical, float depthCanonical, Material material, int seed, List<Bounds> clearings)
    {
        Validate(parent, material, clearings, widthCanonical);
        if (!Finite(depthCanonical) || depthCanonical <= 0 || !Finite(yaw)) throw new ArgumentException("Patch dimensions and rotation must be finite.");
        CheckPoint(worldCenter);
        const int slices = 24;
        float[] rings = { .42f, .76f, 1f };
        float[] alphas = { .77f, .65f, 0 };
        var vertices = new List<Vector3>(1 + slices * rings.Length);
        var uv = new List<Vector2>(vertices.Capacity);
        var colors = new List<Color>(vertices.Capacity);
        var triangles = new List<int>();
        Quaternion rotation = Quaternion.Euler(0, yaw, 0);
        var random = new System.Random(seed);
        float phase = (float)random.NextDouble() * Mathf.PI * 2;
        var center = OnGround(worldCenter, .075f);
        var bounds = new Bounds(center, Vector3.zero);
        vertices.Add(parent.InverseTransformPoint(center)); uv.Add(UV(center)); colors.Add(new Color(1, 1, 1, .8f));
        for (int ring = 0; ring < rings.Length; ring++) for (int slice = 0; slice < slices; slice++)
        {
            float angle = slice * Mathf.PI * 2 / slices;
            float irregular = 1 + .055f * Mathf.Sin(angle * 3 + phase) + .035f * Mathf.Cos(angle * 5 - phase);
            var offset = new Vector3(Mathf.Cos(angle) * widthCanonical * .5f, 0, Mathf.Sin(angle) * depthCanonical * .5f)
                * (rings[ring] * irregular * factor);
            var world = OnGround(worldCenter + rotation * offset, .075f);
            vertices.Add(parent.InverseTransformPoint(world)); uv.Add(UV(world)); colors.Add(new Color(1, 1, 1, alphas[ring]));
            bounds.Encapsulate(world);
            int a = 1 + ring * slices + slice, b = 1 + ring * slices + (slice + 1) % slices;
            if (ring == 0) triangles.AddRange(new[] { 0, b, a });
            else
            {
                int innerA = a - slices, innerB = b - slices;
                triangles.AddRange(new[] { innerA, innerB, a, innerB, b, a });
            }
        }
        var mesh = MakeMesh(name, vertices, uv, colors, triangles);
        var result = MeshObject(parent, name, mesh, material, false);
        bounds.Expand(new Vector3(.2f, .1f, .2f) * factor); clearings.Add(bounds);
        return result;
    }

    /// <summary>Combines caller-filtered points into at most two grass meshes, without source prefab edits.</summary>
    public int AddGrass(Transform parent, string name, Vector3[] worldPoints, int seed)
    {
        if (parent == null) throw new ArgumentNullException(nameof(parent));
        if (worldPoints == null) throw new ArgumentNullException(nameof(worldPoints));
        if (worldPoints.Length == 0) return 0;
        foreach (var point in worldPoints) CheckPoint(point);
        var random = new System.Random(seed);
        var instances = new[] { new List<CombineInstance>(), new List<CombineInstance>() };
        for (int i = 0; i < worldPoints.Length; i++)
        {
            int kind = i % 3 == 0 ? 1 : 0;
            Mesh source = GrassSource(kind);
            if ((instances[kind].Count + 1) * source.vertexCount > 8000) continue;
            float desiredHeight = Mathf.Lerp(.25f, .4f, (float)random.NextDouble());
            float size = desiredHeight * factor / source.bounds.size.y;
            float yaw = (float)random.NextDouble() * 360;
            var rotation = Quaternion.Euler(0, yaw, 0);
            var anchor = new Vector3(source.bounds.center.x, source.bounds.min.y, source.bounds.center.z);
            var position = OnGround(worldPoints[i], .015f);
            // The parent may already carry scale seven; worldToLocal cancels it exactly once.
            Matrix4x4 matrix = parent.worldToLocalMatrix * Matrix4x4.TRS(position, rotation, Vector3.one * size)
                * Matrix4x4.Translate(-anchor);
            instances[kind].Add(new CombineInstance { mesh = source, subMeshIndex = 0, transform = matrix });
        }
        for (int kind = 0; kind < instances.Length; kind++)
        {
            if (instances[kind].Count == 0) continue;
            var source = GrassSource(kind);
            var mesh = new Mesh { name = name + (kind == 0 ? "_Co_Thap" : "_Co_Bui") };
            if ((long)source.vertexCount * instances[kind].Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.CombineMeshes(instances[kind].ToArray(), true, true, false);
            mesh.RecalculateBounds();
            MeshObject(parent, mesh.name, mesh, GrassMaterial(kind), true);
        }
        return instances[0].Count + instances[1].Count;
    }

    Material GroundMaterial(string name, string layerFile, Color color)
    {
        var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(LayerFolder + layerFile);
        var shader = Shader.Find("BachDang/Village Path");
        if (shader == null || layer == null || layer.diffuseTexture == null)
            throw new InvalidOperationException("Village path shader or source ground texture is missing: " + layerFile);
        var material = new Material(shader) { name = name, enableInstancing = true };
        material.SetTexture("_BaseMap", layer.diffuseTexture); material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", .04f); Save(material, name, ".mat");
        return material;
    }

    Mesh GrassSource(int kind)
    {
        if (grassSources[kind] != null) return grassSources[kind];
        string type = kind == 0 ? "Grass_A" : "Grass_C";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(GrassFolder + "Models/" + type + ".asset");
        if (mesh == null || !mesh.isReadable || mesh.subMeshCount != 1 || mesh.bounds.size.y <= .001f)
            throw new InvalidOperationException("Grass combining requires the existing readable single-submesh model: " + type);
        grassSources[kind] = mesh; return mesh;
    }

    Material GrassMaterial(int kind)
    {
        if (grassMaterials[kind] != null) return grassMaterials[kind];
        string type = kind == 0 ? "Grass_A" : "Grass_C";
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(GrassFolder + "Textures/Details/" + type + "_BaseColor.tif");
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(GrassFolder + "Textures/Details/" + type + "_Normal.tif");
        if (shader == null || albedo == null || normal == null) throw new InvalidOperationException("Missing URP grass source textures: " + type);
        var material = new Material(shader) { name = "Village_Edge_" + type, enableInstancing = true, doubleSidedGI = true };
        material.SetTexture("_BaseMap", albedo); material.SetTexture("_BumpMap", normal);
        material.SetColor("_BaseColor", new Color(.93f, .98f, .88f, 1));
        material.SetFloat("_BumpScale", .65f); material.SetFloat("_Smoothness", .12f);
        material.SetFloat("_Metallic", 0); material.SetFloat("_Surface", 0); material.SetFloat("_ZWrite", 1);
        material.SetFloat("_AlphaClip", 1); material.SetFloat("_Cutoff", .43f); material.SetFloat("_Cull", (float)CullMode.Off);
        material.SetFloat("_SrcBlend", (float)BlendMode.One); material.SetFloat("_DstBlend", (float)BlendMode.Zero);
        material.EnableKeyword("_ALPHATEST_ON"); material.EnableKeyword("_NORMALMAP");
        material.SetOverrideTag("RenderType", "TransparentCutout"); material.renderQueue = (int)RenderQueue.AlphaTest;
        Save(material, material.name, ".mat"); grassMaterials[kind] = material; return material;
    }

    List<Vector3> SmoothSamples(List<Vector3> controls)
    {
        var dense = new List<Vector3>();
        for (int segment = 0; segment < controls.Count - 1; segment++)
        {
            Vector3 a = controls[Mathf.Max(segment - 1, 0)], b = controls[segment];
            Vector3 c = controls[segment + 1], d = controls[Mathf.Min(segment + 2, controls.Count - 1)];
            int steps = Mathf.Max(4, Mathf.CeilToInt(Vector3.Distance(b, c) / (factor * .5f)));
            for (int step = segment == 0 ? 0 : 1; step <= steps; step++)
            {
                float t = (float)step / steps, t2 = t * t, t3 = t2 * t;
                dense.Add(.5f * ((2 * b) + (-a + c) * t + (2 * a - 5 * b + 4 * c - d) * t2 + (-a + 3 * b - 3 * c + d) * t3));
            }
        }
        // Resample arc length to keep the ground overlay conforming through corners.
        var result = new List<Vector3> { dense[0] };
        float interval = 1.5f * factor, carried = 0;
        for (int i = 1; i < dense.Count; i++)
        {
            var start = dense[i - 1]; var end = dense[i]; float length = Vector3.Distance(start, end);
            if (length < .00001f) continue;
            while (carried + length >= interval)
            {
                float fraction = (interval - carried) / length;
                start = Vector3.Lerp(start, end, fraction); result.Add(start);
                length = Vector3.Distance(start, end); carried = 0;
                if (length < .00001f) break;
            }
            carried += length;
        }
        var last = dense[dense.Count - 1];
        if (Vector3.Distance(result[result.Count - 1], last) > .01f * factor) result.Add(last);
        else result[result.Count - 1] = last;
        if (result.Count == 1) result.Insert(0, dense[0]);
        return result;
    }

    bool PathSamplesAllowed(List<Vector3> samples, float maximumHalfWidth)
    {
        var origin = terrain.transform.position; var size = terrain.terrainData.size;
        for (int index = 0; index < samples.Count; index++)
        {
            var point = samples[index];
            // Catmull-Rom may also overshoot the terrain edge; the linear fallback remains available.
            if (!Finite(point.x) || !Finite(point.z) || point.x < origin.x || point.z < origin.z ||
                point.x > origin.x + size.x || point.z > origin.z + size.z) return false;
            if (PathPointAllowed != null && !PathPointAllowed(OnGround(point, .075f), maximumHalfWidth)) return false;
            if (index > 0 && PathSegmentAllowed != null && !PathSegmentAllowed(samples[index - 1], point, maximumHalfWidth)) return false;
        }
        return true;
    }

    List<Vector3> LinearSamples(List<Vector3> controls)
    {
        var result = new List<Vector3>();
        for (int segment = 0; segment < controls.Count - 1; segment++)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(controls[segment], controls[segment + 1]) / (.5f * factor)));
            for (int step = segment == 0 ? 0 : 1; step <= steps; step++)
                result.Add(Vector3.Lerp(controls[segment], controls[segment + 1], (float)step / steps));
        }
        return result;
    }

    Mesh MakeMesh(string name, List<Vector3> vertices, List<Vector2> uv, List<Color> colors, List<int> triangles)
    {
        var mesh = new Mesh { name = name };
        if (vertices.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
        mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
        return mesh;
    }

    Transform MeshObject(Transform parent, string name, Mesh mesh, Material material, bool grass)
    {
        Save(mesh, name, ".asset");
        var go = new GameObject(name); Undo.RegisterCreatedObjectUndo(go, "Dress village ground");
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = true;
        // These meshes are unique (and grass already combined). Static batching would
        // duplicate their vertex buffers without the benefit of reusable source geometry.
        GameObjectUtility.SetStaticEditorFlags(go, 0);
        return go.transform;
    }

    void Save(Object asset, string label, string extension)
    {
        string safe = string.IsNullOrWhiteSpace(label) ? "Ground" : label;
        foreach (char invalid in Path.GetInvalidFileNameChars()) safe = safe.Replace(invalid, '_');
        safe = safe.Replace('/', '_').Replace('\\', '_');
        string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + (++serial).ToString("000") + "_" + safe + extension);
        AssetDatabase.CreateAsset(asset, path); assets.Add(path);
    }

    void Validate(Transform parent, Material material, List<Bounds> clearings, float width)
    {
        if (parent == null || material == null || clearings == null) throw new ArgumentNullException("A ground parent, material and clearing list are required.");
        if (!Finite(width) || width <= 0) throw new ArgumentException("Ground width must be positive and finite.");
    }

    Vector3 OnGround(Vector3 point, float lift)
    {
        CheckPoint(point);
        point.y = terrain.SampleHeight(point) + terrain.transform.position.y + lift * factor;
        return point;
    }

    Vector2 UV(Vector3 world) => new Vector2(world.x / factor, world.z / factor) * .22f;

    void CheckPoint(Vector3 point)
    {
        if (!Finite(point.x) || !Finite(point.y) || !Finite(point.z)) throw new ArgumentException("Ground point must be finite.");
        var origin = terrain.transform.position; var size = terrain.terrainData.size;
        if (point.x < origin.x || point.z < origin.z || point.x > origin.x + size.x || point.z > origin.z + size.z)
            throw new ArgumentOutOfRangeException(nameof(point), "Ground point falls outside the terrain.");
    }

    static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
