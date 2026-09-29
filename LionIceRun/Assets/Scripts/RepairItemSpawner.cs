using System.Collections.Generic;
using UnityEngine;

// 파워업 아이템을 가끔 멀쩡한 블럭 위에 띄운다 (처음엔 얼음 복구만 있어서 이름이 RepairItemSpawner).
//  - 생존 시간이 firstSpawnTime을 넘으면 시작, 한 번에 하나만, interval마다
//  - 종류: 얼음 복구(주변 깨진 칸 복구), 방패(시련 1번 막기), 슈퍼 점프(깨진 칸 2개도 넘기), 얼음 굳히기(주변 금 간 칸을 멀쩡하게)
//  - 쓸모 없는 종류는 안 나옴: 깨진 칸이 없으면 복구 X, 금 간 칸이 없으면 굳히기 X, 이미 방패가 있으면 방패 X
//  - 복구·굳히기는 효과가 클 칸일수록, 방패·슈퍼 점프는 플레이어와 가까운 칸일수록 잘 뽑힘
//  - 플레이어가 서 있는 블럭, 깨졌거나 흔들리는 블럭에는 안 나옴. 놓인 블럭이 깨지면 같이 사라짐
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

    [Header("종류별 나올 확률 (비율)")]
    [Min(0f)] public float repairWeight = 3f;
    [Min(0f)] public float shieldWeight = 2f;
    [Min(0f)] public float superJumpWeight = 2f;
    [Min(0f)] public float hardenWeight = 2f;

    [Header("효과 범위")]
    [Tooltip("얼음 복구: 먹은 칸에서 몇 칸 거리까지 (1 = 주변 3x3)")]
    [Range(1, 3)] public int repairRadius = 1;
    [Tooltip("얼음 굳히기: 먹은 칸에서 몇 칸 거리까지 (2 = 주변 5x5)")]
    [Range(1, 4)] public int hardenRadius = 2;

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

        if (!TrySpawn()) nextSpawnTime = timer.Elapsed + 1f;  // 나올 만한 게 없으면 1초 뒤 다시 확인
    }

    bool TrySpawn()
    {
        IceBlock under = null;
        PlayerPowerUps powerUps = null;
        if (player != null)
        {
            IceStepTracker tracker = player.GetComponent<IceStepTracker>();
            if (tracker != null) under = tracker.CurrentBlock;
            powerUps = player.GetComponent<PlayerPowerUps>();
        }

        // 1) 종류 고르기
        bool anyBroken = false, anyCracked = false;
        foreach (IceBlock block in grid.IceBlocks)
        {
            if (block.IsBroken) anyBroken = true;
            else if (block.CurrentStage != IceBlock.Stage.Clean) anyCracked = true;
        }
        var kinds = new List<(RepairItem.Kind kind, float weight)>();
        if (anyBroken) kinds.Add((RepairItem.Kind.Repair, repairWeight));
        if (anyCracked) kinds.Add((RepairItem.Kind.Harden, hardenWeight));
        if (powerUps != null && !powerUps.HasShield) kinds.Add((RepairItem.Kind.Shield, shieldWeight));
        if (powerUps != null) kinds.Add((RepairItem.Kind.SuperJump, superJumpWeight));
        if (!Pick(kinds, out RepairItem.Kind chosen)) return false;

        // 2) 칸 고르기
        Vector2Int playerCell = player != null ? grid.WorldToCell(player.transform.position) : grid.StartCell;
        var cells = new List<(IceBlock block, float weight)>();
        foreach (IceBlock block in grid.IceBlocks)
        {
            if (block.IsBroken || block.CurrentStage == IceBlock.Stage.Shaking || block == under) continue;
            float weight = chosen switch
            {
                RepairItem.Kind.Repair => Squared(Count(block.Cell, repairRadius, b => b.IsBroken)),
                RepairItem.Kind.Harden => Squared(Count(block.Cell, hardenRadius, b => !b.IsBroken && b.CurrentStage != IceBlock.Stage.Clean)),
                _ => 1f / (1f + Vector2Int.Distance(block.Cell, playerCell)),
            };
            if (weight > 0f) cells.Add((block, weight));
        }
        if (!Pick(cells, out IceBlock target)) return false;

        Vector3 position = grid.CellTop(target.Cell) + Vector3.up * floatHeight;
        current = Instantiate(itemPrefab, position, Quaternion.identity);
        current.Init(this, target.Cell, lifetime, chosen);
        grid.PlayIceBurst(position, 10, 0.4f);
        return true;
    }

    static float Squared(int n) => n * n;

    static bool Pick<T>(List<(T item, float weight)> options, out T picked)
    {
        picked = default;
        float total = 0f;
        foreach (var option in options) total += option.weight;
        if (total <= 0f) return false;
        float roll = Random.value * total;
        foreach (var option in options)
        {
            picked = option.item;
            roll -= option.weight;
            if (roll <= 0f) return true;
        }
        return true;
    }

    int Count(Vector2Int center, int radius, System.Func<IceBlock, bool> match)
    {
        int count = 0;
        for (int dx = -radius; dx <= radius; dx++)
        for (int dz = -radius; dz <= radius; dz++)
        {
            IceBlock block = grid.GetBlock(center + new Vector2Int(dx, dz));
            if (block != null && match(block)) count++;
        }
        return count;
    }

    public void OnItemCollected(RepairItem item, PlayerController collector)
    {
        if (item != current) return;
        PlayerPowerUps powerUps = collector.GetComponent<PlayerPowerUps>();

        switch (item.ItemKind)
        {
            case RepairItem.Kind.Repair:
                ForEachAround(item.Cell, repairRadius, (cell, block) =>
                {
                    if (block.IsBroken && !PlayerInside(cell)) block.Restore();
                });
                break;
            case RepairItem.Kind.Harden:
                ForEachAround(item.Cell, hardenRadius, (cell, block) => block.Heal());
                break;
            case RepairItem.Kind.Shield:
                if (powerUps != null) powerUps.GiveShield();
                break;
            case RepairItem.Kind.SuperJump:
                if (powerUps != null) powerUps.GiveSuperJump();
                break;
        }
        if (GameHUD.Instance != null) GameHUD.Instance.ShowToast($"{RepairItem.NameOf(item.ItemKind)}!", RepairItem.ColorOf(item.ItemKind));
        Finish(item);
    }

    void ForEachAround(Vector2Int center, int radius, System.Action<Vector2Int, IceBlock> action)
    {
        for (int dx = -radius; dx <= radius; dx++)
        for (int dz = -radius; dz <= radius; dz++)
        {
            Vector2Int cell = center + new Vector2Int(dx, dz);
            IceBlock block = grid.GetBlock(cell);
            if (block != null) action(cell, block);
        }
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
