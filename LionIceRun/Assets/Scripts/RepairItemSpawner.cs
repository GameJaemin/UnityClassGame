using System.Collections.Generic;
using UnityEngine;

// 얼음 복구 아이템을 가끔 멀쩡한 블럭 위에 띄운다.
//  - 생존 시간이 firstSpawnTime을 넘으면 시작, 한 번에 하나만, interval마다
//  - 깨진 칸이 하나도 없으면 띄우지 않음 (쓸모가 없으니)
//  - 주변(3x3)에 깨진 칸이 많은 블럭일수록 잘 뽑힘 (먹으러 갈 이유가 생기게)
//  - 플레이어가 서 있는 블럭, 깨졌거나 흔들리는 블럭에는 안 나옴
//  - 먹으면 주변 repairRadius 안의 깨진 칸이 다시 얼어붙음
public class RepairItemSpawner : MonoBehaviour
{
    public IceGrid grid;
    public PlayerController player;
    public RepairItem itemPrefab;

    [Header("등장")]
    [Tooltip("생존 시간이 이만큼 지나면 첫 아이템")]
    [Min(0f)] public float firstSpawnTime = 10f;
    [Tooltip("아이템이 사라지거나 먹힌 뒤 다음 아이템까지")]
    [Min(0f)] public float interval = 12f;
    [Tooltip("아이템이 떠 있는 시간")]
    [Min(1f)] public float lifetime = 8f;
    [Min(0f)] public float floatHeight = 0.7f;

    [Header("복구")]
    [Tooltip("먹은 칸에서 몇 칸 거리까지 복구할지 (1 = 주변 3x3)")]
    [Range(1, 3)] public int repairRadius = 1;

    RepairItem current;
    float nextSpawnTime;

    void Start() => nextSpawnTime = firstSpawnTime;

    void Update()
    {
        // 아이템이 놓인 블럭이 깨지면 아이템도 사라진다
        if (current != null)
        {
            IceBlock under = grid.GetBlock(current.Cell);
            if (under == null || under.IsBroken) Finish(current);
        }

        SurvivalTimer timer = SurvivalTimer.Instance;
        if (timer == null || !timer.IsRunning || current != null || itemPrefab == null) return;
        if (timer.Elapsed < nextSpawnTime) return;

        if (!TrySpawn()) nextSpawnTime = timer.Elapsed + 1f;  // 깨진 칸이 없으면 1초 뒤 다시 확인
    }

    bool TrySpawn()
    {
        IceBlock under = null;
        if (player != null)
        {
            IceStepTracker tracker = player.GetComponent<IceStepTracker>();
            if (tracker != null) under = tracker.CurrentBlock;
        }
        var candidates = new List<(IceBlock block, int weight)>();
        int totalWeight = 0;
        foreach (IceBlock block in grid.IceBlocks)
        {
            if (block.IsBroken || block.CurrentStage == IceBlock.Stage.Shaking || block == under) continue;
            int broken = CountBrokenAround(block.Cell);
            if (broken == 0) continue;
            int weight = broken * broken;
            candidates.Add((block, weight));
            totalWeight += weight;
        }
        if (candidates.Count == 0) return false;

        int pick = Random.Range(0, totalWeight);
        foreach (var (block, weight) in candidates)
        {
            pick -= weight;
            if (pick >= 0) continue;
            Vector3 position = grid.CellTop(block.Cell) + Vector3.up * floatHeight;
            current = Instantiate(itemPrefab, position, Quaternion.identity);
            current.Init(this, block.Cell, lifetime);
            grid.PlayIceBurst(position, 10, 0.4f);
            return true;
        }
        return false;
    }

    int CountBrokenAround(Vector2Int center)
    {
        int count = 0;
        for (int dx = -repairRadius; dx <= repairRadius; dx++)
        for (int dz = -repairRadius; dz <= repairRadius; dz++)
        {
            IceBlock block = grid.GetBlock(center + new Vector2Int(dx, dz));
            if (block != null && block.IsBroken) count++;
        }
        return count;
    }

    public void OnItemCollected(RepairItem item)
    {
        if (item != current) return;
        for (int dx = -repairRadius; dx <= repairRadius; dx++)
        for (int dz = -repairRadius; dz <= repairRadius; dz++)
        {
            Vector2Int cell = item.Cell + new Vector2Int(dx, dz);
            IceBlock block = grid.GetBlock(cell);
            if (block != null && block.IsBroken && !PlayerInside(cell)) block.Restore();
        }
        Finish(item);
    }

    public void OnItemExpired(RepairItem item)
    {
        if (item == current) Finish(item);
    }

    void Finish(RepairItem item)
    {
        Destroy(item.gameObject);
        current = null;
        if (SurvivalTimer.Instance != null) nextSpawnTime = SurvivalTimer.Instance.Elapsed + interval;
    }

    // 빈칸으로 떨어지는 중인 플레이어가 있는 칸은 얼리지 않는다 (블럭 속에 끼지 않게)
    bool PlayerInside(Vector2Int cell)
    {
        if (player == null) return false;
        Vector3 top = grid.CellTop(cell);
        Vector3 p = player.transform.position;
        float half = grid.blockSize * 0.5f + 0.5f;
        return Mathf.Abs(p.x - top.x) < half && Mathf.Abs(p.z - top.z) < half && p.y < top.y + 0.5f && p.y > top.y - 3f;
    }
}
