using System.Collections.Generic;
using UnityEngine;

// 안전 효과가 맵의 인접 얼음 칸을 따라 순환한다. 판 자체를 물리적으로 끌고 가지 않는다.
[DefaultExecutionOrder(100)]
public class MovingSafeZones : MonoBehaviour
{
    public const float HoldSeconds = 10f;
    public const float FadeSeconds = 2f;
    IceGrid grid;
    readonly List<List<IceBlock>> routes = new List<List<IceBlock>>();
    readonly HashSet<IceBlock> active = new HashSet<IceBlock>();
    int cycle = -1;
    public IReadOnlyList<List<IceBlock>> Routes => routes;
    public int Cycle => cycle;

    static readonly Vector2Int[] Directions = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };

    // 타이틀/로비 복귀에서 얼음 오브젝트가 지워지기 전에 오래된 경로를 해제한다.
    public void ClearRoutes()
    {
        grid = null; routes.Clear(); active.Clear(); cycle = -1;
    }

    public void Initialize(IceGrid owner)
    {
        ClearRoutes(); grid = owner;
        var cells = new Dictionary<Vector2Int, IceBlock>();
        var seeds = new List<Vector2Int>();
        foreach (var block in grid.IceBlocks)
        {
            // 이동/회전 발판은 자기 기믹에 속하므로 안전 경로에서 제외한다.
            if (block.transform.parent != grid.transform) continue;
            cells[block.Cell] = block;
            if (grid.Map != null && grid.Map.Get(block.Cell) == MapData.Safe) seeds.Add(block.Cell);
        }
        // 동시에 움직이는 안전 효과끼리 겹치지 않도록 연결 영역을 다중 시작 BFS로 분할한다.
        var owners = new Dictionary<Vector2Int, int>();
        var queue = new Queue<Vector2Int>();
        for (int i = 0; i < seeds.Count; i++) { owners[seeds[i]] = i; queue.Enqueue(seeds[i]); }
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            foreach (var direction in Directions)
            {
                var next = cell + direction;
                if (!cells.ContainsKey(next) || owners.ContainsKey(next)) continue;
                owners[next] = owners[cell]; queue.Enqueue(next);
            }
        }
        for (int i = 0; i < seeds.Count; i++)
        {
            var route = new List<IceBlock>();
            Walk(seeds[i], i, cells, owners, new HashSet<Vector2Int>(), route);
            // DFS의 마지막 귀환 칸은 첫 칸과 같으므로 순환 시 중복 10초 대기를 제거한다.
            if (route.Count > 1 && route[route.Count - 1] == route[0]) route.RemoveAt(route.Count - 1);
            routes.Add(route);
        }
        ApplyTime(0);
    }

    static void Walk(Vector2Int cell, int owner, Dictionary<Vector2Int, IceBlock> cells,
        Dictionary<Vector2Int, int> owners, HashSet<Vector2Int> visited, List<IceBlock> route)
    {
        visited.Add(cell); route.Add(cells[cell]);
        foreach (var direction in Directions)
        {
            var next = cell + direction;
            if (!owners.TryGetValue(next, out int region) || region != owner || visited.Contains(next)) continue;
            Walk(next, owner, cells, owners, visited, route);
            route.Add(cells[cell]);
        }
    }

    public static float Appearance(float elapsed)
    {
        float phase = Mathf.Repeat(Mathf.Max(0, elapsed), HoldSeconds);
        float entering = elapsed < HoldSeconds ? 1f : Mathf.Clamp01(phase / FadeSeconds);
        float leaving = Mathf.Clamp01((HoldSeconds - phase) / FadeSeconds);
        return Mathf.SmoothStep(0, 1, Mathf.Min(entering, leaving));
    }

    public void ApplyTime(float elapsed, bool changeProtection = true)
    {
        elapsed = Mathf.Max(0, elapsed);
        int nextCycle = Mathf.FloorToInt(elapsed / HoldSeconds);
        if (changeProtection && nextCycle != cycle)
        {
            foreach (var block in active) if (block != null) block.SetSafeProtection(false);
            active.Clear();
            foreach (var route in routes)
            {
                if (route.Count == 0) continue;
                var block = route[nextCycle % route.Count];
                if (block == null) continue;
                block.SetSafeProtection(true); active.Add(block);
            }
            cycle = nextCycle;
        }
        // 복제 PC의 예측 시계는 색만 보간한다. 무적 칸 변경은 호스트 스냅샷으로만 확정한다.
        float weight = nextCycle == cycle ? Appearance(elapsed) : 0f;
        foreach (var block in active) if (block != null) block.SetSafeAppearance(weight);
    }

    void Update()
    {
        var timer = SurvivalTimer.Instance;
        if (grid == null || timer == null) return;
        ApplyTime(timer.Elapsed, !grid.networkReplica);
    }
}
