using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Deterministic, bounded village routing against expanded world-space footprints.</summary>
public static class BachDangVillageRoutes
{
    const float Step = 1f;
    const float SampleStep = .4f;
    const float EndpointReach = 4f;
    const float Epsilon = .00001f;

    public static Vector3[] Route(Vector3[] worldControls, float worldHalfWidth,
        Transform reference, Bounds[] worldObstacles, float factor)
    {
        if (reference == null || worldControls == null || worldControls.Length < 2 || worldObstacles == null)
            throw new ArgumentException("Routing requires a reference, obstacles and at least two controls.");
        if (!Finite(factor) || factor <= 0 || !Finite(worldHalfWidth) || worldHalfWidth < 0)
            throw new ArgumentException("Routing dimensions must be finite and nonnegative.");
        if (Vector3.Dot(reference.up, Vector3.up) < .9999f ||
            Mathf.Abs(reference.lossyScale.x - factor) > .001f * factor ||
            Mathf.Abs(reference.lossyScale.z - factor) > .001f * factor)
            throw new ArgumentException("The village reference must have its uniform world factor and remain upright.");
        foreach (var point in worldControls)
            if (!Finite(point)) throw new ArgumentException("Route controls must be finite.");
        foreach (var bounds in worldObstacles)
            if (!Finite(bounds.min) || !Finite(bounds.max)) throw new ArgumentException("Obstacle bounds must be finite.");
        return new Router(worldControls, worldHalfWidth + .2f * factor, reference, worldObstacles).Find();
    }

    /// <summary>Exact XZ segment test. The caller includes any extra safety margin in worldHalfWidth.</summary>
    public static bool SegmentClear(Vector3 a, Vector3 b, Bounds[] worldObstacles, float worldHalfWidth)
    {
        if (worldObstacles == null || !Finite(a) || !Finite(b) || !Finite(worldHalfWidth) || worldHalfWidth < 0)
            throw new ArgumentException("Segment clearance requires finite points, radius and obstacles.");
        foreach (var obstacle in worldObstacles)
        {
            float enter = 0, leave = 1;
            if (Slab(a.x, b.x - a.x, obstacle.min.x - worldHalfWidth, obstacle.max.x + worldHalfWidth, ref enter, ref leave) &&
                Slab(a.z, b.z - a.z, obstacle.min.z - worldHalfWidth, obstacle.max.z + worldHalfWidth, ref enter, ref leave))
                return false;
        }
        return true;
    }

    static bool Slab(float position, float direction, float min, float max, ref float enter, ref float leave)
    {
        if (Mathf.Abs(direction) < Epsilon) return position >= min && position <= max;
        float a = (min - position) / direction, b = (max - position) / direction;
        if (a > b) { float swap = a; a = b; b = swap; }
        enter = Mathf.Max(enter, a); leave = Mathf.Min(leave, b);
        return enter <= leave;
    }

    sealed class Router
    {
        readonly Vector2[] controls;
        readonly Bounds[] obstacles;
        readonly Matrix4x4 localToWorld;
        readonly float radius;
        readonly int minX, minZ, width, depth;
        readonly Vector2[] points;
        readonly Vector3[] world;
        readonly bool[] open;
        readonly float[] distanceToControls;

        public Router(Vector3[] input, float radius, Transform reference, Bounds[] obstacles)
        {
            this.obstacles = obstacles; this.radius = radius; localToWorld = reference.localToWorldMatrix;
            controls = new Vector2[input.Length];
            float loX = float.MaxValue, loZ = float.MaxValue, hiX = float.MinValue, hiZ = float.MinValue;
            for (int i = 0; i < input.Length; i++)
            {
                var p = reference.InverseTransformPoint(input[i]); controls[i] = new Vector2(p.x, p.z);
                loX = Mathf.Min(loX, p.x); loZ = Mathf.Min(loZ, p.z); hiX = Mathf.Max(hiX, p.x); hiZ = Mathf.Max(hiZ, p.z);
            }
            // Detours may need to pass around an entire household or work yard.
            // Still bounded to this village; never allocate a world-sized navigation grid.
            minX = Mathf.Max(-105, Mathf.FloorToInt(loX - 28));
            minZ = Mathf.Max(-120, Mathf.FloorToInt(loZ - 28));
            int maxX = Mathf.Min(105, Mathf.CeilToInt(hiX + 28));
            int maxZ = Mathf.Min(120, Mathf.CeilToInt(hiZ + 28));
            if (maxX < minX || maxZ < minZ) throw new InvalidOperationException("Proposed route is outside the permitted village area.");
            width = maxX - minX + 1; depth = maxZ - minZ + 1;
            int count = width * depth;
            points = new Vector2[count]; world = new Vector3[count]; open = new bool[count]; distanceToControls = new float[count];
            for (int z = 0; z < depth; z++) for (int x = 0; x < width; x++)
            {
                int id = z * width + x;
                points[id] = new Vector2(minX + x * Step, minZ + z * Step); world[id] = World(points[id]);
                open[id] = SegmentClear(world[id], world[id], obstacles, radius);
                distanceToControls[id] = PolylineDistance(points[id]);
            }
        }

        Vector3 World(Vector2 p) => localToWorld.MultiplyPoint3x4(new Vector3(p.x, 0, p.y));
        bool InGrid(Vector2 p) => p.x >= minX && p.x <= minX + width - 1 && p.y >= minZ && p.y <= minZ + depth - 1;
        bool PointOpen(Vector2 p) => InGrid(p) && SegmentClear(World(p), World(p), obstacles, radius);

        Vector2 Anchor(Vector2 requested)
        {
            if (PointOpen(requested)) return requested;
            int best = -1; float nearest = EndpointReach * EndpointReach + Epsilon;
            foreach (int id in Nearby(requested))
            {
                float distance = (points[id] - requested).sqrMagnitude;
                if (!open[id] || distance >= nearest) continue;
                nearest = distance; best = id;
            }
            if (best < 0) throw new InvalidOperationException("A village route endpoint has no open location within four reference units.");
            return points[best];
        }

        IEnumerable<int> Nearby(Vector2 center)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(center.x - EndpointReach - minX));
            int x1 = Mathf.Min(width - 1, Mathf.CeilToInt(center.x + EndpointReach - minX));
            int z0 = Mathf.Max(0, Mathf.FloorToInt(center.y - EndpointReach - minZ));
            int z1 = Mathf.Min(depth - 1, Mathf.CeilToInt(center.y + EndpointReach - minZ));
            for (int z = z0; z <= z1; z++) for (int x = x0; x <= x1; x++)
            {
                int id = z * width + x;
                if ((points[id] - center).sqrMagnitude <= EndpointReach * EndpointReach + Epsilon) yield return id;
            }
        }

        public Vector3[] Find()
        {
            Vector2 start = Anchor(controls[0]), end = Anchor(controls[controls.Length - 1]);
            int count = points.Length;
            var scores = new float[count]; var parent = new int[count]; var closed = new bool[count]; var goalCost = new float[count];
            for (int i = 0; i < count; i++) { scores[i] = float.PositiveInfinity; parent[i] = -1; goalCost[i] = float.PositiveInfinity; }
            var heap = new Heap();
            foreach (int id in Nearby(start))
            {
                if (!open[id] || !Clear(start, points[id])) continue;
                scores[id] = Cost(start, points[id]); parent[id] = -2;
                heap.Push(new Entry(id, scores[id], scores[id] + Vector2.Distance(points[id], end)));
            }
            bool hasGoal = false;
            foreach (int id in Nearby(end))
            {
                if (!open[id] || !Clear(points[id], end)) continue;
                goalCost[id] = Cost(points[id], end); hasGoal = true;
            }
            if (heap.Count == 0 || !hasGoal)
                throw new InvalidOperationException("An open village endpoint cannot connect to the bounded routing grid.");

            int reached = -1; float best = float.PositiveInfinity;
            while (heap.Count != 0)
            {
                var item = heap.Pop(); int current = item.id;
                if (closed[current] || item.g > scores[current] + Epsilon) continue;
                if (item.f >= best) break;
                closed[current] = true;
                float total = scores[current] + goalCost[current];
                if (total < best) { reached = current; best = total; }
                int cx = current % width, cz = current / width;
                for (int dz = -1; dz <= 1; dz++) for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    int nx = cx + dx, nz = cz + dz;
                    if (nx < 0 || nz < 0 || nx >= width || nz >= depth) continue;
                    int next = nz * width + nx;
                    if (!open[next] || closed[next]) continue;
                    if (dx != 0 && dz != 0 && (!open[cz * width + nx] || !open[nz * width + cx])) continue;
                    if (!SegmentClear(world[current], world[next], obstacles, radius)) continue;
                    float length = dx == 0 || dz == 0 ? Step : Step * 1.41421356237f;
                    float score = scores[current] + length * (1 + .04f * (distanceToControls[current] + distanceToControls[next]) * .5f);
                    if (score + Epsilon >= scores[next]) continue;
                    scores[next] = score; parent[next] = current;
                    heap.Push(new Entry(next, score, score + Vector2.Distance(points[next], end)));
                }
            }
            if (reached < 0) throw new InvalidOperationException("No clear village route exists inside the proposed corridor and village limits.");
            var chain = new List<Vector2>();
            for (int at = reached; at >= 0; at = parent[at]) chain.Add(points[at]);
            chain.Reverse();
            if ((chain[0] - start).sqrMagnitude > Epsilon) chain.Insert(0, start); else chain[0] = start;
            if ((chain[chain.Count - 1] - end).sqrMagnitude > Epsilon) chain.Add(end); else chain[chain.Count - 1] = end;
            var simplified = Simplify(chain);
            if (simplified.Count == 1) simplified.Add(simplified[0]);
            var result = new Vector3[simplified.Count];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = World(simplified[i]);
                if (i > 0 && !Clear(simplified[i - 1], simplified[i]))
                    throw new InvalidOperationException("Final village route failed its clearance verification.");
            }
            return result;
        }

        bool Clear(Vector2 a, Vector2 b)
        {
            if (!InGrid(a) || !InGrid(b) || !SegmentClear(World(a), World(b), obstacles, radius)) return false;
            int samples = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / SampleStep));
            for (int i = 0; i <= samples; i++) if (!PointOpen(Vector2.Lerp(a, b, i / (float)samples))) return false;
            return true;
        }

        float PolylineDistance(Vector2 p)
        {
            float nearest = float.PositiveInfinity;
            for (int i = 1; i < controls.Length; i++)
            {
                Vector2 a = controls[i - 1], delta = controls[i] - a;
                float t = delta.sqrMagnitude > Epsilon ? Mathf.Clamp01(Vector2.Dot(p - a, delta) / delta.sqrMagnitude) : 0;
                nearest = Mathf.Min(nearest, Vector2.Distance(p, a + delta * t));
            }
            return nearest;
        }

        float Cost(Vector2 a, Vector2 b)
        {
            float length = Vector2.Distance(a, b); int count = Mathf.Max(1, Mathf.CeilToInt(length / SampleStep));
            float attraction = 0;
            for (int i = 0; i < count; i++) attraction += PolylineDistance(Vector2.Lerp(a, b, (i + .5f) / count));
            return length * (1 + .04f * attraction / count);
        }

        List<Vector2> Simplify(List<Vector2> source)
        {
            if (source.Count < 3) return source;
            var result = new List<Vector2> { source[0] };
            for (int current = 0; current < source.Count - 1;)
            {
                int furthest = current + 1; float originalCost = 0;
                for (int candidate = current + 1; candidate < source.Count; candidate++)
                {
                    originalCost += Cost(source[candidate - 1], source[candidate]);
                    // Keep curved-lane controls instead of flattening the whole village into one chord.
                    if (Vector2.Distance(source[current], source[candidate]) > 12f) break;
                    if (Clear(source[current], source[candidate]) && Cost(source[current], source[candidate]) <= originalCost * 1.015f + Epsilon)
                        furthest = candidate;
                }
                result.Add(source[furthest]); current = furthest;
            }
            return result;
        }
    }

    readonly struct Entry
    {
        public readonly int id;
        public readonly float g, f;
        public Entry(int id, float g, float f) { this.id = id; this.g = g; this.f = f; }
    }

    sealed class Heap
    {
        readonly List<Entry> items = new List<Entry>();
        public int Count => items.Count;
        static bool Before(Entry a, Entry b) => a.f < b.f || (a.f == b.f && (a.g < b.g || (a.g == b.g && a.id < b.id)));
        public void Push(Entry value)
        {
            int index = items.Count; items.Add(value);
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (!Before(value, items[parent])) break;
                items[index] = items[parent]; index = parent;
            }
            items[index] = value;
        }
        public Entry Pop()
        {
            var result = items[0]; var tail = items[items.Count - 1]; items.RemoveAt(items.Count - 1);
            if (items.Count == 0) return result;
            int index = 0;
            while (index * 2 + 1 < items.Count)
            {
                int child = index * 2 + 1;
                if (child + 1 < items.Count && Before(items[child + 1], items[child])) child++;
                if (!Before(items[child], tail)) break;
                items[index] = items[child]; index = child;
            }
            items[index] = tail; return result;
        }
    }

    static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
}
