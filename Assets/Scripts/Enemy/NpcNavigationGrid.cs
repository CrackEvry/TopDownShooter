using System.Collections.Generic;
using UnityEngine;

// Shared room A*: static walls are baked once; dynamic people are handled by
// local separation. Eight neighbours, with diagonal corner cutting forbidden.
public class NpcNavigationGrid : MonoBehaviour
{
    public Rect bounds = new Rect(-15, -12, 30, 20);
    public Rect[] walkAreas;
    [Min(0.2f)] public float cellSize = 0.5f;
    public float agentRadius = 0.3f;
    int width, height;
    bool[] walkable, closed;
    float[] costs;
    int[] parents;
    readonly Collider2D[] overlap = new Collider2D[32];
    readonly RaycastHit2D[] clearanceHits = new RaycastHit2D[48];
    readonly List<int> heap = new List<int>();
    readonly List<float> priorities = new List<float>();
    public bool Ready => walkable != null;

    public void Bake()
    {
        Physics2D.SyncTransforms();
        width = Mathf.CeilToInt(bounds.width / cellSize); height = Mathf.CeilToInt(bounds.height / cellSize);
        walkable = new bool[width * height]; closed = new bool[walkable.Length]; costs = new float[walkable.Length]; parents = new int[walkable.Length];
        var filter = new ContactFilter2D(); filter.SetLayerMask(~0); filter.useTriggers = false;
        for (int i = 0; i < walkable.Length; i++)
        {
            Vector2 point = Point(i);
            bool allowed = walkAreas == null || walkAreas.Length == 0;
            if (!allowed) foreach (var area in walkAreas) if (area.Contains(point)) { allowed = true; break; }
            if (!allowed) continue;
            int count = Physics2D.OverlapCircle(point, agentRadius, filter, overlap);
            bool clear = true;
            for (int k = 0; k < count; k++) if (CombatBallistics.IsWall(overlap[k])) { clear = false; break; }
            walkable[i] = clear;
        }
    }
    Vector2 Point(int i) => bounds.min + new Vector2((i % width + 0.5f) * cellSize, (i / width + 0.5f) * cellSize);
    int Cell(Vector2 p)
    {
        int x = Mathf.FloorToInt((p.x - bounds.xMin) / cellSize), y = Mathf.FloorToInt((p.y - bounds.yMin) / cellSize);
        return x < 0 || y < 0 || x >= width || y >= height ? -1 : y * width + x;
    }
    int Nearest(Vector2 p)
    {
        int cell = Cell(p);
        if (cell < 0) return -1;
        if (walkable[cell]) return cell;
        int cx = cell % width, cy = cell / width;
        float best = float.PositiveInfinity; int result = -1;
        for (int y = Mathf.Max(0, cy - 4); y <= Mathf.Min(height - 1, cy + 4); y++)
            for (int x = Mathf.Max(0, cx - 4); x <= Mathf.Min(width - 1, cx + 4); x++)
            {
                int next = y * width + x; float d = (Point(next) - p).sqrMagnitude;
                if (walkable[next] && d < best) { best = d; result = next; }
            }
        return result;
    }
    public bool IsWalkable(Vector2 point) { if (!Ready) Bake(); int cell = Cell(point); return cell >= 0 && walkable[cell]; }
    public bool CanTravelDirect(Vector2 from, Vector2 to)
    {
        Vector2 delta = to - from;
        if (delta.sqrMagnitude < 0.0001f) return true;
        var filter = new ContactFilter2D(); filter.SetLayerMask(~0); filter.useTriggers = false;
        int count = Physics2D.CircleCast(from, agentRadius * 0.85f, delta.normalized, filter, clearanceHits, delta.magnitude);
        for (int i = 0; i < count; i++) if (CombatBallistics.IsWall(clearanceHits[i].collider)) return false;
        return true;
    }
    public bool FindPath(Vector2 from, Vector2 to, List<Vector2> result)
    {
        result.Clear(); if (!Ready) Bake();
        int start = Nearest(from), goal = Nearest(to);
        if (start < 0 || goal < 0) return false;
        for (int i = 0; i < costs.Length; i++) { costs[i] = float.PositiveInfinity; closed[i] = false; parents[i] = -1; }
        heap.Clear(); priorities.Clear(); costs[start] = 0; Push(start, 0);
        while (heap.Count > 0)
        {
            int current = Pop(); if (closed[current]) continue;
            if (current == goal)
            {
                for (int p = goal; p != start && p >= 0; p = parents[p]) result.Add(Point(p));
                result.Reverse(); if (result.Count == 0) result.Add(Point(goal)); return true;
            }
            closed[current] = true; int cx = current % width, cy = current / width;
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                int x = cx + dx, y = cy + dy;
                if (x < 0 || y < 0 || x >= width || y >= height) continue;
                int next = y * width + x;
                if (!walkable[next] || closed[next]) continue;
                if (dx != 0 && dy != 0 && (!walkable[cy * width + x] || !walkable[y * width + cx])) continue;
                float cost = costs[current] + (dx == 0 || dy == 0 ? 1 : 1.414214f);
                if (cost >= costs[next]) continue;
                costs[next] = cost; parents[next] = current;
                float heuristic = Vector2.Distance(Point(next), Point(goal)) / cellSize;
                Push(next, cost + heuristic);
            }
        }
        return false;
    }
    void Push(int node, float score)
    {
        int i = heap.Count; heap.Add(node); priorities.Add(score);
        while (i > 0)
        {
            int parent = (i - 1) / 2; if (priorities[parent] <= score) break;
            heap[i] = heap[parent]; priorities[i] = priorities[parent]; i = parent;
        }
        heap[i] = node; priorities[i] = score;
    }
    int Pop()
    {
        int result = heap[0], last = heap.Count - 1, node = heap[last]; float score = priorities[last];
        heap.RemoveAt(last); priorities.RemoveAt(last); if (last == 0) return result;
        int i = 0;
        while (i * 2 + 1 < last)
        {
            int child = i * 2 + 1;
            if (child + 1 < last && priorities[child + 1] < priorities[child]) child++;
            if (priorities[child] >= score) break;
            heap[i] = heap[child]; priorities[i] = priorities[child]; i = child;
        }
        heap[i] = node; priorities[i] = score; return result;
    }
}
