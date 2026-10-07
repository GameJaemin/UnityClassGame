using System.Collections.Generic;
using UnityEngine;

// 플레이어와 1등을 겨루는 AI 봇들을 만든다.
//  - 게임 시작 전: 맵툴의 봇 시작 칸부터 사용하고, 부족한 수만 안전한 무작위 칸에 세운다
//  - 만들 때 GameManager에 경기 참가자로 등록한다 (바다에 빠짐·다시 들어오기·순위는 GameManager가 처리)
//  - 게임 시작(플레이어가 처음 움직임): 모두 움직이기 시작
// 봇의 몸은 플레이어와 같은 부품(PlayerController, IceStepTracker, CharacterAnimator, PlayerSkin)으로 만든다.
public class BotManager : MonoBehaviour
{
    public IceGrid grid;
    public PlayerController player;

    [Header("AI 봇 수")]
    [Tooltip("게임에 나오는 AI 봇 수 (0이면 봇 없음). 설 칸이 부족하면 자동으로 줄어듦")]
    [Range(0, 12)] public int botCount = 3;
    [Tooltip("무작위 배치에서 피할 플레이어 시작 칸 주변 거리 (맵툴 지정 위치에는 적용하지 않음)")]
    [Min(0)] public int startClearance = 1;
    [Header("AI 유형 배치")]
    [Tooltip("전체 Bot Count 중 공격형 수. 나머지는 신중형이며, 맵 지정 시작 위치 순서로 신중형부터 배치")]
    [Range(0, 12)] public int aggressiveBotCount = 2;

    public int AggressiveCount => Mathf.Clamp(aggressiveBotCount, 0, Mathf.Max(0, botCount));
    public int CautiousCount => Mathf.Max(0, botCount - AggressiveCount);

    readonly List<BotBrain> bots = new List<BotBrain>();

    System.Collections.IEnumerator Start()
    {
        if (grid == null || player == null) yield break;
        if (GameManager.Instance != null) GameManager.Instance.StateChanged += OnStateChanged;
        // IceGrid가 게임 시작 때 블럭을 만들 때까지 기다린다 (Start 순서는 정해져 있지 않음)
        while (grid.IceBlocks.Count == 0) yield return null;

        var used = new HashSet<Vector2Int>();
        MapData map = grid.Map;
        if (map != null && map.botStarts != null)
        {
            foreach (MapData.Point point in map.botStarts)
            {
                if (bots.Count >= botCount) break;
                Vector2Int cell = point.Cell;
                IceBlock block = grid.GetBlock(cell);
                if (block == null || block.IsBroken || block.CurrentStage == IceBlock.Stage.Shaking
                    || cell == grid.StartCell || used.Contains(cell))
                {
                    Debug.LogWarning($"BotManager: 맵의 봇 시작 칸 ({cell.x}, {cell.y})은 사용할 수 없어 건너뜁니다 (빈칸·깨진 칸·플레이어 칸·중복 확인).");
                    continue;
                }
                // 사용자가 지정한 칸은 가장자리·고립 여부와 startClearance보다 우선한다.
                used.Add(cell);
                bots.Add(CreateBot(bots.Count, cell));
            }
        }
        while (bots.Count < botCount)
        {
            if (!TryChooseSpawn(used, out Vector2Int cell)) break;
            used.Add(cell);
            bots.Add(CreateBot(bots.Count, cell));
        }
        if (bots.Count < botCount) Debug.LogWarning($"BotManager: 안전한 시작 칸이 부족해 봇을 {botCount}마리 대신 {bots.Count}마리만 세웁니다.");
        // 봇이 만들어지기 전에 게임이 이미 시작됐으면 바로 움직인다
        if (GameManager.Instance != null && GameManager.Instance.State == GameManager.GameState.Playing)
            foreach (BotBrain bot in bots) bot.Activate();
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.StateChanged -= OnStateChanged;
    }

    void OnStateChanged(GameManager.GameState state)
    {
        if (!isActiveAndEnabled || OnlineGameSession.InRoom || state != GameManager.GameState.Playing) return;
        foreach (BotBrain bot in bots) if (bot != null) bot.Activate();
    }

    // 지정 시작 칸으로 채우지 못한 봇만 안전한 무작위 칸에 배치한다.
    bool TryChooseSpawn(HashSet<Vector2Int> used, out Vector2Int chosen)
    {
        chosen = default;
        var candidates = new List<(Vector2Int cell, float weight)>();
        float total = 0f;
        foreach (IceBlock block in grid.IceBlocks)
        {
            if (block.IsBroken || block.IsSafe || block.CurrentStage == IceBlock.Stage.Shaking) continue;
            if (used != null && used.Contains(block.Cell)) continue;
            Vector2Int d = block.Cell - grid.StartCell;
            if (Mathf.Abs(d.x) <= startClearance && Mathf.Abs(d.y) <= startClearance) continue;
            int exits = 0;
            foreach (Vector2Int direction in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
            {
                IceBlock neighbor = grid.GetBlock(block.Cell + direction);
                if (neighbor != null && !neighbor.IsBroken && neighbor.CurrentStage < IceBlock.Stage.HeavyCracked) exits++;
            }
            // 벗어날 길 없는 섬에는 세우지 않는다. 여러 출구가 있는 안쪽 칸을 더 잘 고른다.
            if (exits == 0 || block.CurrentStage >= IceBlock.Stage.HeavyCracked) continue;
            float weight = exits * exits;
            foreach (Vector2Int other in used)
                if ((other - block.Cell).sqrMagnitude <= 2) weight *= 0.15f;
            candidates.Add((block.Cell, weight));
            total += weight;
        }
        if (candidates.Count == 0) return false;
        float pick = Random.value * total;
        chosen = candidates[candidates.Count - 1].cell;
        foreach (var candidate in candidates)
        {
            pick -= candidate.weight;
            if (pick <= 0f) { chosen = candidate.cell; break; }
        }
        return true;
    }

    BotBrain CreateBot(int index, Vector2Int cell)
    {
        // 부품을 다 붙이고 값을 넣은 뒤에 켜야 Awake에서 제대로 초기화된다
        var go = new GameObject($"Bot{index + 1}");
        go.SetActive(false);
        go.transform.position = grid.CellTop(cell) + Vector3.up * player.spawnHeight;
        go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        var playerCapsule = player.GetComponent<CapsuleCollider>();
        var capsule = go.AddComponent<CapsuleCollider>();
        capsule.center = playerCapsule.center;
        capsule.radius = playerCapsule.radius;
        capsule.height = playerCapsule.height;
        capsule.sharedMaterial = playerCapsule.sharedMaterial;

        var playerBody = player.GetComponent<Rigidbody>();
        var body = go.AddComponent<Rigidbody>();
        body.mass = playerBody.mass;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // 조작감 수치는 플레이어와 똑같이
        var controller = go.AddComponent<PlayerController>();
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(player), controller);
        controller.grid = grid;
        controller.spawnAtStartCell = false;
        controller.useExternalInput = true;

        go.AddComponent<IceStepTracker>().grid = grid;
        go.AddComponent<CharacterAnimator>();
        var effectTemplate = player.GetComponent<PushVfx>();
        if (effectTemplate != null && effectTemplate.particles != null)
        {
            var effect = go.AddComponent<PushVfx>();
            effect.particles = Instantiate(effectTemplate.particles, go.transform, false);
        }

        PlayerSkin playerSkin = player.GetComponent<PlayerSkin>();
        if (playerSkin != null)
        {
            var skin = go.AddComponent<PlayerSkin>();
            skin.models = playerSkin.models;
            skin.animatorController = playerSkin.animatorController;
            skin.modelOffset = playerSkin.modelOffset;
            skin.modelYaw = playerSkin.modelYaw;
            skin.skin = (PlayerSkin.Skin)SoloGameSeries.BotSkin(index, Random.Range(0, System.Enum.GetValues(typeof(PlayerSkin.Skin)).Length));
        }

        var brain = go.AddComponent<BotBrain>();
        brain.grid = grid;
        brain.player = player;
        // 지정 시작 위치 순서대로 신중형을 먼저, 나머지를 공격형으로 배치한다.
        brain.ConfigureBehavior(index < CautiousCount ? BotBrain.BehaviorType.Cautious : BotBrain.BehaviorType.Aggressive);

        var nameTemplate = player.GetComponent<PlayerNameplate>();
        if (nameTemplate != null && nameTemplate.plate != null)
        {
            var plate = Instantiate(nameTemplate.plate, go.transform, false);
            var nameplate = go.AddComponent<PlayerNameplate>();
            nameplate.plate = plate; nameplate.label = plate.GetComponentInChildren<TMPro.TMP_Text>(true);
            nameplate.SetName(go.name, false, true);
        }

        go.SetActive(true);
        if (GameManager.Instance != null) GameManager.Instance.Register(controller);
        return brain;
    }
}
