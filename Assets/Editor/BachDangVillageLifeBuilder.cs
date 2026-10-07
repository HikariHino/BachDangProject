using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Small civilian work areas using the village's existing art and canonical dimensions.</summary>
public static class BachDangVillageLifeBuilder
{
    const string Folder = "Assets/BachDangScaleAndLife";
    const string Props = "Assets/Prefabs/Props/pf_";
    const string GroupName = "09_Sinh_Hoat";

    // The caller owns the scene backup, undo group, vegetation edits and save.
    // All generated geometry is local to a parent which already carries factor.
    public static void Build(Terrain terrain, Transform settlements, float factor,
        List<Bounds> clearings, List<string> generatedAssets)
    {
        if (terrain == null || settlements == null || clearings == null || generatedAssets == null || factor <= 0)
            throw new ArgumentException("Village dressing requires terrain, settlement root and a positive world scale.");
        if (settlements.Find(GroupName) != null)
            throw new InvalidOperationException("Civilian work areas already exist; refusing to duplicate them.");
        if ((settlements.lossyScale - Vector3.one * factor).sqrMagnitude > .001f)
            throw new InvalidOperationException("Build civilian details after applying the settlement world scale.");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "BachDangScaleAndLife");
        var builder = new Builder(terrain, settlements, factor, clearings, generatedAssets);
        builder.BuildAll();
    }

    sealed class Builder
    {
        readonly Terrain terrain;
        readonly Transform settlements, root;
        readonly float factor;
        readonly List<Bounds> clearings;
        readonly List<string> assets;
        readonly Material soil, path, timber, stone, rope, wellWater, gardenFern, gardenBush;
        readonly Material[] cloth;
        readonly Mesh stoneBlock, clothMesh;
        int meshIndex;
        int skipped;

        public Builder(Terrain t, Transform s, float f, List<Bounds> c, List<string> a)
        {
            terrain = t; settlements = s; factor = f; clearings = c; assets = a;
            root = NewGroup(GroupName, settlements);
            path = AssetDatabase.LoadAssetAtPath<Material>("Assets/BachDangSettlements/Village_Packed_Earth.mat");
            if (path == null) throw new InvalidOperationException("The existing soft village path material is required.");
            soil = new Material(path) { name = "Village_Garden_Soil" };
            soil.SetColor("_BaseColor", new Color(.84f, .66f, .43f, .94f));
            Save(soil, "Village_Garden_Soil.mat");
            timber = SourceMaterial(Props + "plankpile_01.prefab");
            stone = BachDangVillageDetailPolish.GetWellStoneMaterial(assets);
            gardenFern = BachDangVillageDetailPolish.GetGardenMaterial(false, assets);
            gardenBush = BachDangVillageDetailPolish.GetGardenMaterial(true, assets);
            rope = Lit("Village_Hemp_Rope", new Color(.35f, .28f, .16f));
            wellWater = Lit("Village_Well_Interior", new Color(.055f, .095f, .085f));
            cloth = new[] {
                Lit("Cloth_Undyed_Linen", new Color(.66f, .60f, .44f), true),
                Lit("Cloth_Muted_Indigo", new Color(.23f, .31f, .35f), true),
                Lit("Cloth_Clay_Brown", new Color(.47f, .30f, .20f), true)
            };
            stoneBlock = WellStone(); Save(stoneBlock, "Well_Stone_Block.asset");
            clothMesh = HangingCloth(); Save(clothMesh, "Hanging_Linen.asset");
        }

        public void BuildAll()
        {
            string[] prefixes = { "01_", "02_", "03_" };
            for (int village = 0; village < prefixes.Length; village++)
            {
                Transform original = settlements.Cast<Transform>().FirstOrDefault(t => t.name.StartsWith(prefixes[village], StringComparison.Ordinal));
                if (original == null) throw new InvalidOperationException("Missing village group " + prefixes[village]);
                var group = NewGroup(original.name + "_Sinh_Hoat", root);
                group.SetPositionAndRotation(original.position, original.rotation);
                for (int plot = 0; plot < 3; plot++) Garden(group, new Vector2(85, -40 + plot * 30), plot, village);
                Well(group, new Vector2(-7, 76));
                Laundry(group, new Vector2(-86, -60), 0);
                Laundry(group, new Vector2(-86, 60), 1);
                WorkYard(group, new Vector2(-86, 5), false, village == 1);
                WorkYard(group, new Vector2(85, 75), true, village == 1);
                for (int side = -1; side <= 1; side += 2)
                {
                    var yard = GroundGroup("Hang_Hoa_Canh_Cho", group, new Vector2(side * 43, 66), new Vector2(7, 7));
                    if (yard == null) continue;
                    Patch(yard, "San_Hang_Hoa", 7, 6, path);
                    Prop("barrels_02", "Thung_Hang", yard, -1.7f, 0, 13, .85f);
                    Prop(village == 1 ? "barrel_fish_01" : "buckets_01", "Hang_Dan_Sinh", yard, 1.1f, 1.2f, -24, .85f);
                    Prop("plankpile_01", "Ke_Hang_Cho", yard, 1.6f, -1.2f, 83, .65f);
                }
            }
            Debug.Log("Civilian details: 9 garden plots, 3 wells, 6 linen yards and 6 work yards planned; " + skipped + " groups/props skipped on unsuitable ground.");
        }

        void Garden(Transform village, Vector2 center, int index, int villageIndex)
        {
            var plot = GroundGroup("Vuon_Rau_" + (index + 1), village, center, new Vector2(17, 19));
            if (plot == null) return;
            Patch(plot, "Dat_Vuon", 16, 18, soil);
            for (int row = 0; row < 4; row++) for (int column = 0; column < 5; column++)
            {
                float x = -5.5f + column * 2.7f;
                float z = -5.8f + row * 3.8f + (column % 2 == 0 ? .2f : -.2f);
                string flora = row == 3 && column % 2 == 0 ? "Bush_A" : "Fern_A";
                bool bush = flora == "Bush_A";
                float size = bush ? BachDangVillageDetailPolish.BushMultiplier : BachDangVillageDetailPolish.FernMultiplier;
                var plant = Place("Assets/TerrainSampleAssets/Prefabs/" + flora + ".prefab", "Rau_" + row + "_" + column,
                    plot, new Vector2(x, z), column * 73 + row * 47 + villageIndex * 11, size, false);
                if (plant != null)
                {
                    plant.transform.position += Vector3.up * (.06f * factor);
                    BachDangVillageDetailPolish.AssignGardenMaterial(plant, bush ? gardenBush : gardenFern);
                }
            }
            Prop("bucket_01", "Xo_Tuoi_Vuon", plot, -6.8f, -7.1f, 20, .85f);
            Prop("shovel_01", "Dung_Cu_Lam_Vuon", plot, -6.2f, -7.5f, -18, .9f);
            Prop("fence_02_double", "Rao_Vuon_Phia_Dong", plot, 8.2f, -3.5f, 90, .88f);
            Prop("fence_02_double", "Rao_Vuon_Phia_Dong", plot, 8.2f, 3.5f, 90, .88f);
            Path(village, "Loi_Ra_Vuon_" + index, 1.7f,
                new Vector2(59, center.y - 2), new Vector2(69, center.y + 1.2f), new Vector2(77.2f, center.y));
        }

        void Well(Transform village, Vector2 center)
        {
            var yard = GroundGroup("Gieng_Nuoc_Chung", village, center, new Vector2(6, 6));
            if (yard == null) return;
            Patch(yard, "San_Gieng", 6, 6, path);
            for (int i = 0; i < 12; i++)
            {
                var block = MeshObject("Da_Thanh_Gieng_" + i.ToString("00"), yard, stoneBlock, stone, true);
                block.localRotation = Quaternion.Euler(0, i * 30, 0);
            }
            Primitive("Long_Gieng_Toi", PrimitiveType.Cylinder, yard, new Vector3(0, .1f, 0), new Vector3(1.88f, .015f, 1.88f), wellWater, false);
            foreach (int side in new[] { -1, 1 })
                Primitive("Tru_Keo_Nuoc", PrimitiveType.Cylinder, yard, new Vector3(side * 1.65f, 1.38f, 0), new Vector3(.17f, 1.4f, .17f), timber, true);
            Primitive("Xa_Gieng", PrimitiveType.Cube, yard, new Vector3(0, 2.74f, 0), new Vector3(3.65f, .2f, .23f), timber, true);
            Rod("Day_Keo_Gau", yard, new Vector3(0, 2.65f, 0), new Vector3(0, .63f, 0), .034f, rope);
            Prop("bucket_01", "Gau_Nuoc_Ben_Gieng", yard, 1.9f, 1.3f, 18, .85f);
            Prop("buckets_01", "Xo_Nuoc_Dan_Lang", yard, -2.1f, 1.35f, -25, .7f);
        }

        void Laundry(Transform village, Vector2 center, int variant)
        {
            var yard = GroundGroup("San_Phoi_Vai_" + (variant + 1), village, center, new Vector2(10, 6));
            if (yard == null) return;
            Patch(yard, "San_Phoi", 10, 6, path);
            foreach (int side in new[] { -1, 1 })
                Primitive("Cot_Phoi_Go", PrimitiveType.Cylinder, yard, new Vector3(side * 4.1f, 1.3f, 0), new Vector3(.13f, 1.32f, .13f), timber, true);
            const int segments = 8;
            for (int i = 0; i < segments; i++)
            {
                float a = (float)i / segments, b = (float)(i + 1) / segments;
                Vector3 RopePoint(float u) => new Vector3(Mathf.Lerp(-4.1f, 4.1f, u), 2.5f - .16f * Mathf.Sin(u * Mathf.PI), 0);
                Rod("Day_Phoi_" + i, yard, RopePoint(a), RopePoint(b), .023f, rope);
            }
            for (int i = 0; i < 4; i++)
            {
                var panel = MeshObject("Vai_Phoi_" + i, yard, clothMesh, cloth[(i + variant) % cloth.Length], false);
                float x = -2.8f + i * 1.85f;
                panel.localPosition = new Vector3(x, 2.5f - .16f * Mathf.Sin((x + 4.1f) / 8.2f * Mathf.PI), 0);
                panel.localScale = new Vector3(i % 2 == 0 ? 1 : .85f, i % 2 == 0 ? 1.1f : .85f, 1);
            }
            Prop("buckets_01", "Chau_Giat_Va_Xo", yard, -3.6f, 1.8f, 13, .9f);
            Path(village, "Ngo_San_Phoi_" + variant, 1.65f,
                new Vector2(-60, center.y), new Vector2(-72, center.y + 2), new Vector2(-82, center.y + 1));
        }

        void WorkYard(Transform village, Vector2 center, bool cooking, bool fishing)
        {
            var yard = GroundGroup(cooking ? "San_Bep_Va_Phoi_Thoc" : "San_Lam_Nghe", village, center, new Vector2(15, 13));
            if (yard == null) return;
            Patch(yard, "San_Dat_Sinh_Hoat", 15, 13, path);
            Prop("shed_01", "Mai_Che_Do_Dung", yard, 0, 3.5f, 180, 1.1f);
            Prop("logpile_01", "Cui_Kho", yard, -4.6f, 2.7f, 90, .9f);
            Prop("barrels_01", "Thung_Tich_Tru", yard, 4.7f, 3, -14, .95f);
            Prop("buckets_01", "Xo_Nuoc", yard, -3.8f, -3.8f, 25, .9f);
            if (cooking)
            {
                var fire = Prop("torch_fire_01", "Bep_Lua_Nho", yard, 1.9f, -2.3f, 0, .75f);
                if (fire != null)
                {
                    foreach (var particle in fire.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        var main = particle.main; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(particle);
                    }
                    foreach (var light in fire.GetComponentsInChildren<Light>(true))
                    {
                        light.enabled = true; light.gameObject.SetActive(true);
                        light.range = 7.5f * factor; light.intensity = 1.6f * factor * factor;
                        light.color = new Color(1, .57f, .27f); light.shadows = LightShadows.None;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(light);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(light.gameObject);
                    }
                }
                Prop("barrel_fish_01", "Thuc_Pham", yard, 4.7f, -3.3f, -17, .78f);
                Primitive("Tam_Phoi_Nong_San", PrimitiveType.Cube, yard,
                    new Vector3(-1.8f, .065f, -2.3f), new Vector3(2.2f, .045f, 2.7f), cloth[0], false);
            }
            else
            {
                Prop(fishing ? "fish_stick_01" : "plankpile_02", fishing ? "Phoi_Ca" : "Van_Go_Dang_Lam", yard, 3.9f, -2.4f, 80, .9f);
                Prop("plankpile_01", "Go_Nguyen_Lieu", yard, -.8f, -2.5f, 7, .9f);
            }
            float approach = center.x < 0 ? -61 : 60;
            Path(village, cooking ? "Ngo_San_Bep" : "Ngo_San_Nghe", 2.1f,
                new Vector2(approach, center.y - 3), new Vector2((approach + center.x) * .5f, center.y - 5), new Vector2(center.x, center.y - 5));
        }

        Transform GroundGroup(string name, Transform parent, Vector2 center, Vector2 size)
        {
            Vector3 point = parent.TransformPoint(new Vector3(center.x, 0, center.y));
            float minimum = float.MaxValue, maximum = float.MinValue;
            for (int z = -1; z <= 1; z++) for (int x = -1; x <= 1; x++)
            {
                var p = parent.TransformPoint(new Vector3(center.x + size.x * .5f * x, 0, center.y + size.y * .5f * z));
                float h = Height(p); minimum = Mathf.Min(minimum, h); maximum = Mathf.Max(maximum, h);
            }
            if (minimum < 16.8f * factor || maximum - minimum > .9f * factor) { skipped++; return null; }
            var group = NewGroup(name, parent);
            point.y = Height(point); group.SetPositionAndRotation(point, parent.rotation);
            Bounds bounds = new Bounds(point, Vector3.zero);
            for (int z = -1; z <= 1; z += 2) for (int x = -1; x <= 1; x += 2)
                bounds.Encapsulate(group.TransformPoint(new Vector3(x * size.x * .5f, 0, z * size.y * .5f)));
            bounds.Expand(new Vector3(1, 1, 1) * factor); clearings.Add(bounds);
            return group;
        }

        GameObject Prop(string suffix, string name, Transform parent, float x, float z, float yaw, float multiplier)
            => Place(Props + suffix + ".prefab", name, parent, new Vector2(x, z), yaw, multiplier, true);

        GameObject Place(string prefab, string name, Transform parent, Vector2 local, float yaw, float multiplier, bool clear)
        {
            Vector3 point = parent.TransformPoint(new Vector3(local.x, 0, local.y)); point.y = Height(point);
            if (point.y < 16.8f * factor) { skipped++; return null; }
            var go = BachDangSettlementAssets.Place(prefab, name, parent, point, parent.eulerAngles.y + yaw, multiplier);
            var bounds = BachDangSettlementAssets.BoundsOf(go);
            float highest = point.y, lowest = point.y;
            for (int z = -1; z <= 1; z++) for (int x = -1; x <= 1; x++)
            {
                float height = Height(new Vector3(point.x + x * bounds.extents.x * .8f, 0, point.z + z * bounds.extents.z * .8f));
                highest = Mathf.Max(highest, height); lowest = Mathf.Min(lowest, height);
            }
            if (lowest < 16.8f * factor || highest - lowest > .9f * factor)
            { Undo.DestroyObjectImmediate(go); skipped++; return null; }
            go.transform.position += Vector3.up * (highest - point.y + .03f);
            if (clear) { bounds = BachDangSettlementAssets.BoundsOf(go); bounds.Expand(factor); clearings.Add(bounds); }
            return go;
        }

        void Patch(Transform parent, string name, float width, float depth, Material material)
        {
            const int count = 8;
            var vertices = new Vector3[(count + 1) * (count + 1)];
            var uv = new Vector2[vertices.Length]; var colors = new Color[vertices.Length];
            var triangles = new List<int>();
            for (int z = 0; z <= count; z++) for (int x = 0; x <= count; x++)
            {
                int index = z * (count + 1) + x;
                float localX = ((float)x / count - .5f) * width, localZ = ((float)z / count - .5f) * depth;
                var world = parent.TransformPoint(new Vector3(localX, 0, localZ)); world.y = Height(world) + .055f * factor;
                vertices[index] = parent.InverseTransformPoint(world); uv[index] = new Vector2(localX, localZ) * .22f;
                float edge = Mathf.Min(x, count - x, z, count - z);
                colors[index] = new Color(1, 1, 1, Mathf.Clamp01(edge));
                if (x < count && z < count)
                {
                    int next = index + count + 1;
                    triangles.AddRange(new[] { index, next, index + 1, index + 1, next, next + 1 });
                }
            }
            var mesh = new Mesh { name = name }; mesh.vertices = vertices; mesh.uv = uv; mesh.colors = colors; mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.RecalculateTangents(); Save(mesh, "Life_Ground_" + (++meshIndex).ToString("00") + ".asset");
            var go = MeshObject(name, parent, mesh, material, false);
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        void Path(Transform parent, string name, float width, params Vector2[] points)
        {
            var samples = new List<Vector2>();
            for (int i = 1; i < points.Length; i++)
            {
                int steps = Mathf.CeilToInt(Vector2.Distance(points[i - 1], points[i]) / 1.8f);
                for (int step = i == 1 ? 0 : 1; step <= steps; step++) samples.Add(Vector2.Lerp(points[i - 1], points[i], (float)step / steps));
            }
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var colors = new List<Color>(); var triangles = new List<int>();
            for (int i = 0; i < samples.Count; i++)
            {
                var direction = samples[Mathf.Min(i + 1, samples.Count - 1)] - samples[Mathf.Max(0, i - 1)]; direction.Normalize();
                var cross = new Vector2(-direction.y, direction.x);
                for (int band = 0; band < 4; band++)
                {
                    float lateral = new[] { -.5f, -.28f, .28f, .5f }[band];
                    var p = samples[i] + cross * lateral * width;
                    var world = parent.TransformPoint(new Vector3(p.x, 0, p.y)); world.y = Height(world) + .058f * factor;
                    if (world.y < 16.8f * factor) { skipped++; return; }
                    vertices.Add(parent.InverseTransformPoint(world)); uv.Add(p * .22f);
                    float endFade = Mathf.Clamp01(Mathf.Min(i, samples.Count - 1 - i));
                    colors.Add(new Color(1, 1, 1, (band == 0 || band == 3) ? 0 : endFade));
                }
                if (i > 0)
                {
                    int previous = (i - 1) * 4, next = i * 4;
                    for (int band = 0; band < 3; band++) triangles.AddRange(new[] { previous + band, previous + band + 1, next + band, previous + band + 1, next + band + 1, next + band });
                    var center = parent.TransformPoint(new Vector3(samples[i].x, 0, samples[i].y)); center.y = Height(center);
                    clearings.Add(new Bounds(center, new Vector3(width + 1.2f, 1, width + 1.2f) * factor));
                }
            }
            var mesh = new Mesh { name = name }; mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.RecalculateTangents(); Save(mesh, "Life_Path_" + (++meshIndex).ToString("00") + ".asset");
            var go = MeshObject(name, parent, mesh, path, false); go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        float Height(Vector3 p) => terrain.SampleHeight(p) + terrain.transform.position.y;

        void Save(Object asset, string filename)
        {
            string path = Folder + "/" + filename;
            if (AssetDatabase.LoadMainAssetAtPath(path) != null || File.Exists(path)) throw new IOException("Owned civilian detail asset already exists: " + path);
            AssetDatabase.CreateAsset(asset, path); assets.Add(path);
        }

        Material Lit(string name, Color color, bool twoSided = false)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader is unavailable.");
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", .05f);
            if (twoSided) material.SetFloat("_Cull", (float)CullMode.Off);
            Save(material, name + ".mat"); return material;
        }

        static Material SourceMaterial(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var material = prefab != null ? prefab.GetComponentsInChildren<MeshRenderer>(true).Select(r => r.sharedMaterial).FirstOrDefault(m => m != null) : null;
            if (material == null) throw new InvalidOperationException("Missing existing prop material: " + path);
            return material;
        }

        static Transform NewGroup(string name, Transform parent)
        {
            var group = new GameObject(name); Undo.RegisterCreatedObjectUndo(group, "Dress village life");
            group.transform.SetParent(parent, false); return group.transform;
        }

        static Transform MeshObject(string name, Transform parent, Mesh mesh, Material material, bool collide)
        {
            var group = NewGroup(name, parent);
            group.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            group.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (collide) group.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;
            GameObjectUtility.SetStaticEditorFlags(group.gameObject, StaticEditorFlags.BatchingStatic);
            return group;
        }

        static Transform Primitive(string name, PrimitiveType kind, Transform parent, Vector3 position, Vector3 scale, Material material, bool collide)
        {
            var go = GameObject.CreatePrimitive(kind); Undo.RegisterCreatedObjectUndo(go, "Dress village life");
            go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!collide) Object.DestroyImmediate(go.GetComponent<Collider>());
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic); return go.transform;
        }

        static void Rod(string name, Transform parent, Vector3 from, Vector3 to, float diameter, Material material)
        {
            var direction = to - from;
            var rod = Primitive(name, PrimitiveType.Cylinder, parent, (from + to) * .5f, new Vector3(diameter, direction.magnitude * .5f, diameter), material, false);
            rod.localRotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
        }

        static Mesh HangingCloth()
        {
            const int columns = 8, rows = 5;
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            for (int row = 0; row <= rows; row++) for (int col = 0; col <= columns; col++)
            {
                float u = (float)col / columns, v = (float)row / rows;
                vertices.Add(new Vector3((u - .5f) * 1.35f, -v * 1.3f - .045f * Mathf.Sin(u * Mathf.PI),
                    Mathf.Sin(u * Mathf.PI * 4) * .035f + Mathf.Sin(v * Mathf.PI * .75f) * .11f));
                uv.Add(new Vector2(u, v));
                if (row < rows && col < columns)
                {
                    int a = row * (columns + 1) + col, b = a + columns + 1;
                    triangles.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
                }
            }
            var mesh = new Mesh { name = "Curved hanging linen" }; mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.RecalculateTangents(); return mesh;
        }

        static Mesh WellStone()
        {
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            Vector3 Point(float radius, float angle, float y) => new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad) * radius, y, Mathf.Cos(angle * Mathf.Deg2Rad) * radius);
            var a = Point(.94f, -14.65f, 0); var b = Point(.94f, 14.65f, 0);
            var c = Point(1.35f, 14.65f, 0); var d = Point(1.35f, -14.65f, 0);
            var up = Vector3.up * .84f;
            void Face(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
            {
                int start = vertices.Count; vertices.AddRange(new[] { p0, p1, p2, p3 }); uv.AddRange(new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up });
                triangles.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            }
            Face(a + up, b + up, c + up, d + up); Face(d, c, b, a);
            Face(a, b, b + up, a + up); Face(b, c, c + up, b + up);
            Face(c, d, d + up, c + up); Face(d, a, a + up, d + up);
            var mesh = new Mesh { name = "Individual well masonry block" }; mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.RecalculateTangents(); return mesh;
        }
    }
}
