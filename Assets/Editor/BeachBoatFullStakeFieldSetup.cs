using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using BeachBoatStory;

public static class BeachBoatFullStakeFieldSetup
{
    public static void ExtendAndExit()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/beachBoat.unity");
        var zone=UnityEngine.Object.FindAnyObjectByType<BeachBoatStory.RiverStakeZone>();
        var tide=UnityEngine.Object.FindAnyObjectByType<TideSystem>();
        var terrain=Terrain.activeTerrain;
        var stakes=zone.fieldStakes.ToList();int original=stakes.Count,added=0;
        var ships=UnityEngine.Object.FindObjectsByType<EnemyShip>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        foreach(var ship in ships)
        {
            var end=ship.targetPoint.position;
            var from=ship.routeWaypoints.Last().position;
            var heading=(end-from);heading.y=0;heading.Normalize();var right=Vector3.Cross(Vector3.up,heading);
            for(int distance=0;distance<=150;distance+=30)for(int side=-1;side<=1;side+=2)
            {
                var p=end-heading*distance+right*(18*side);
                if(zone.placements.Any(a=>QuestRowboat.FlatDistance(a.approach.position,p)<65)||
                    stakes.Any(s=>QuestRowboat.FlatDistance(BoundsOf(s).center,p)<16))continue;
                float ground=terrain.SampleHeight(p)+terrain.transform.position.y;
                if(ground>tide.lowTideY-2)continue;
                var stake=UnityEngine.Object.Instantiate(stakes[0].gameObject,stakes[0].parent).transform;
                stake.name="CocGo_Hang_MoRong_"+(++added).ToString("000");stake.gameObject.SetActive(true);
                var bounds=BoundsOf(stake);p.y=bounds.center.y;stake.position+=p-bounds.center;
                float bottom=ground-8,top=Mathf.Lerp(tide.lowTideY,tide.highTideY,.50f+(added%7)*.02f);
                bounds=BoundsOf(stake);stake.localScale*=(top-bottom)/bounds.size.y;
                bounds=BoundsOf(stake);stake.position+=Vector3.up*(bottom-bounds.min.y);
                stake.gameObject.SetActive(false);stakes.Add(stake);
            }
        }
        if(stakes.Count<80)throw new InvalidOperationException("Expanded stake coverage is unexpectedly small.");
        zone.fieldStakes=stakes.ToArray();
        var ordered=Enumerable.Range(0,stakes.Count).OrderBy(i=>BoundsOf(stakes[i]).center.z).ToArray();int offset=0;
        foreach(var point in zone.placements)
        {
            int count=stakes.Count/zone.placements.Length+(Array.IndexOf(zone.placements,point)<stakes.Count%zone.placements.Length?1:0);
            point.stakeIndices=ordered.Skip(offset).Take(count).ToArray();offset+=count;
            point.stake=stakes[point.stakeIndices[0]];
            var text=point.marker.GetComponentInChildren<TMPro.TMP_Text>(true);
            text.text=$"CỤM {Array.IndexOf(zone.placements,point)+1} • {count} CỌC";
        }
        foreach(var point in zone.placements)
            if(stakes.Any(s=>QuestRowboat.FlatDistance(BoundsOf(s).center,point.approach.position)<50))
                throw new InvalidOperationException("Expanded field obstructs a quest 2 planting approach.");
        var coverage=ships.Select(ship=>new{
            ship.name,
            covered=stakes.Any(s=>SegmentDistance(BoundsOf(s).center,ship.routeWaypoints.Last().position,ship.targetPoint.position)<=ship.stakeHitDistance),
            highHidden=stakes.All(s=>BoundsOf(s).max.y<tide.highTideY-1)
        }).ToArray();
        if(coverage.Any(c=>!c.covered||!c.highHidden))throw new InvalidOperationException("An enemy route misses the concealed field.");
        EditorUtility.SetDirty(zone);var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        System.IO.Directory.CreateDirectory("Logs");
        System.IO.File.WriteAllText("Logs/ExpandedStakeCoverage.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{original,added,total=stakes.Count,coverage},Newtonsoft.Json.Formatting.Indented));
        EditorApplication.Exit(0);
    }
    static float SegmentDistance(Vector3 p,Vector3 a,Vector3 b)
    {p.y=a.y=b.y=0;var d=b-a;float t=d.sqrMagnitude>.001f?Mathf.Clamp01(Vector3.Dot(p-a,d)/d.sqrMagnitude):0;return Vector3.Distance(p,a+d*t);}
    [MenuItem("Bach Dang/Configure Complete Quest Stake Field")]
    public static void Configure()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != "Assets/Scenes/beachBoat.unity" || scene.isDirty)
            throw new InvalidOperationException("Open and save beachBoat in Edit Mode first.");
        var zone = UnityEngine.Object.FindAnyObjectByType<BeachBoatStory.RiverStakeZone>();
        var tide = UnityEngine.Object.FindAnyObjectByType<TideSystem>();
        var terrain = Terrain.activeTerrain;
        var stakes = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(t => t.gameObject.scene == scene && t.name.StartsWith("CocGo_Hang"))
            .OrderBy(t => t.name, StringComparer.Ordinal).ToArray();
        if (zone == null || tide == null || terrain == null || stakes.Length < 48 || zone.placements.Length != 5)
            throw new InvalidOperationException("Expected the authored river stake field.");
        System.IO.Directory.CreateDirectory(".utmp/FullStakeField");
        string backup = ".utmp/FullStakeField/beachBoat-before-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity";
        if (!EditorSceneManager.SaveScene(scene, backup, true)) throw new InvalidOperationException("Backup failed.");
        for (int i = 0; i < stakes.Length; i++)
        {
            var stake = stakes[i];
            Undo.RecordObject(stake, "Fit stake below high tide");
            Undo.RecordObject(stake.gameObject, "Reserve whole field for quest");
            stake.gameObject.SetActive(true);
            foreach (var child in stake.GetComponentsInChildren<Transform>(true))
            { Undo.RecordObject(child.gameObject, "Enable planting animation"); GameObjectUtility.SetStaticEditorFlags(child.gameObject, 0); }
            // World-space mesh corners remain accurate for inactive objects and rotated stakes.
            var bounds = BoundsOf(stake);
            float bottom = terrain.SampleHeight(stake.position) + terrain.transform.position.y - 8;
            float top = Mathf.Lerp(tide.lowTideY, tide.highTideY, .48f + (i % 7) * .025f);
            if (top <= bottom || bounds.size.y < .01f) throw new InvalidOperationException("Invalid stake bounds.");
            stake.localScale *= (top - bottom) / bounds.size.y;
            bounds = BoundsOf(stake);
            stake.position += Vector3.up * (bottom - bounds.min.y);
            bounds = BoundsOf(stake);
            if (bounds.max.y <= tide.lowTideY + 10 || bounds.max.y >= tide.highTideY - 10)
                throw new InvalidOperationException("Stake must emerge at low tide and stay well below high tide.");
            stake.gameObject.SetActive(false);
        }
        Undo.RecordObject(zone, "Bind complete quest field");
        zone.fieldStakes = stakes; zone.plantingDuration = 3;
        // Five contiguous strips along the river with balanced stake counts.
        var ordered = Enumerable.Range(0, stakes.Length).OrderBy(i => BoundsOf(stakes[i]).center.z)
            .ThenBy(i => BoundsOf(stakes[i]).center.x).ToArray();
        // Authored approach positions kept clear when extending the field.
        // The west bank becomes too shallow beside the middle of the field.
        var approaches = new[] { new Vector2(-1830, 1370), new Vector2(-1850, 1710),
            new Vector2(-1810, 2070), new Vector2(-1910, 2410), new Vector2(-1830, 2710) };
        int offset = 0;
        for (int n = 0; n < zone.placements.Length; n++)
        {
            var point = zone.placements[n];
            int size = stakes.Length / zone.placements.Length + (n < stakes.Length % zone.placements.Length ? 1 : 0);
            point.stakeIndices = ordered.Skip(offset).Take(size).ToArray(); offset += size;
            point.stake = point.stakeIndices.Select(i => stakes[i]).OrderBy(t => BoundsOf(t).center.x).First();
            Undo.RecordObject(point.approach, "Keep boat clear of the new field");
            point.approach.position = new Vector3(approaches[n].x, tide.transform.position.y + 1, approaches[n].y);
            Undo.RecordObject(point.marker, "Hide completed work markers");
            point.marker.SetActive(false);
            var text = point.marker.GetComponentInChildren<TMPro.TMP_Text>(true);
            Undo.RecordObject(text, "Explain cluster interaction");
            text.text = $"CỤM {n + 1} • {size} CỌC";
        }
        EditorUtility.SetDirty(zone);
        RenderSettings.skybox = UnityEngine.Object.FindAnyObjectByType<DayNightCycle>().skyboxMaterial;
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Scene save failed.");
    }

    static Bounds BoundsOf(Transform root)
    {
        var filters = root.GetComponentsInChildren<MeshFilter>(true);
        bool found = false; Bounds result = default;
        foreach (var filter in filters)
        {
            if (filter.sharedMesh == null) continue;
            var b = filter.sharedMesh.bounds;
            for (int n = 0; n < 8; n++)
            {
                var p = filter.transform.TransformPoint(b.center + Vector3.Scale(b.extents,
                    new Vector3((n & 1) == 0 ? -1 : 1, (n & 2) == 0 ? -1 : 1, (n & 4) == 0 ? -1 : 1)));
                if (!found) { result = new Bounds(p, Vector3.zero); found = true; } else result.Encapsulate(p);
            }
        }
        if (!found) throw new InvalidOperationException("Stake mesh missing.");
        return result;
    }
}
