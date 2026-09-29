using System.Collections.Generic;
using UnityEngine;

// 맵을 돌아다니며 얼음을 깨는 AI 봇들을 만들고 관리한다.
//  - 게임 시작 전: 플레이어 시작 칸과 그 주변을 피해 무작위 칸에 세워 둔다 (가만히 있어서 얼음을 깨지 않음)
//  - 게임 시작(플레이어가 처음 움직임): 모두 움직이기 시작
//  - 바다에 빠지면 respawnDelay 뒤 멀쩡한 칸 위 하늘에서 다시 떨어져 들어온다 (Fall Guys식, 난이도 유지)
// 봇의 몸은 플레이어와 같은 부품(PlayerController, IceStepTracker, CharacterAnimator, PlayerSkin)으로 만든다.
public class BotManager : MonoBehaviour
{
    public IceGrid grid;
    public PlayerController player;

    [Header("AI 봇 수")]
    [Tooltip("게임에 나오는 AI 봇 수 (0이면 봇 없음). 설 칸이 부족하면 자동으로 줄어듦")]
    [Range(0, 12)] public int botCount = 3;
    [Tooltip("바다에 빠진 뒤 다시 들어올 때까지 기다리는 시간 (최소, 최대 사이에서 봇마다 무작위)")]
    public Vector2 respawnDelayRange = new Vector2(5f, 8f);
    [Tooltip("다시 들어올 때 블럭 위 얼마나 높은 곳에서 떨어지는지")]
    [Min(1f)] public float respawnHeight = 7f;
    [Tooltip("봇이 서지 않을 플레이어 시작 칸 주변 거리")]
    [Min(0)] public int startClearance = 1;

    readonly List<BotBrain> bots = new List<BotBrain>();
    readonly List<(BotBrain bot, float time)> waiting = new List<(BotBrain, float)>();

    System.Collections.IEnumerator Start()
    {
        if (grid == null || player == null) yield break;
        if (GameManager.Instance != null) GameManager.Instance.StateChanged += OnStateChanged;
        // IceGrid가 게임 시작 때 블럭을 만들 때까지 기다린다 (Start 순서는 정해져 있지 않음)
        while (grid.IceBlocks.Count == 0) yield return null;

        // 칸마다 한 마리씩만 세우므로, 설 수 있는 칸보다 많으면 줄인다 (플레이어 시작 칸 주변 제외)
        int clearArea = (startClearance * 2 + 1) * (startClearance * 2 + 1);
        int count = Mathf.Min(botCount, Mathf.Max(0, grid.IceBlocks.Count - clearArea));
        if (count < botCount) Debug.LogWarning($"BotManager: 맵이 작아 봇을 {botCount}마리 대신 {count}마리만 세웁니다.");

        var used = new HashSet<Vector2Int>();
        for (int i = 0; i < count; i++)
        {
            Vector2Int cell = RandomCell(used, true);
            used.Add(cell);
            bots.Add(CreateBot(i, cell));
        }
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
        if (state != GameManager.GameState.Playing) return;
        foreach (BotBrain bot in bots) bot.Activate();
    }

    void Update()
    {
        // 게임이 끝나면 더 들여보내지 않는다
        if (GameManager.Instance != null && GameManager.Instance.State == GameManager.GameState.GameOver) return;

        for (int i = waiting.Count - 1; i >= 0; i--)
        {
            if (Time.time < waiting[i].time) continue;
            Respawn(waiting[i].bot);
            waiting.RemoveAt(i);
        }
    }

    public void OnBotFell(BotBrain bot)
    {
        if (!bot.gameObject.activeSelf) return;
        Vector3 p = bot.transform.position;
        grid.PlayIceBurst(new Vector3(p.x, grid.SeaLevel, p.z), 30, 1.2f);
        bot.gameObject.SetActive(false);
        float delay = Random.Range(Mathf.Min(respawnDelayRange.x, respawnDelayRange.y), Mathf.Max(respawnDelayRange.x, respawnDelayRange.y));
        waiting.Add((bot, Time.time + delay));
    }

    void Respawn(BotBrain bot)
    {
        // 플레이어 머리 위로 떨어지지 않게 플레이어 칸과 바로 옆 칸은 피한다
        Vector2Int playerCell = grid.WorldToCell(player.transform.position);
        var nearPlayer = new HashSet<Vector2Int>();
        for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
                nearPlayer.Add(playerCell + new Vector2Int(dx, dz));
        Vector2Int cell = RandomCell(nearPlayer, false);
        bot.gameObject.SetActive(true);
        bot.GetComponent<PlayerController>().ResetMotion(grid.CellTop(cell) + Vector3.up * respawnHeight);
        bot.OnRespawn();
    }

    // 멀쩡하고 흔들리지 않는 칸 하나 (avoidStart면 플레이어 시작 칸 주변 제외)
    Vector2Int RandomCell(HashSet<Vector2Int> used, bool avoidStart)
    {
        var candidates = new List<Vector2Int>();
        foreach (IceBlock block in grid.IceBlocks)
        {
            if (block.IsBroken || block.CurrentStage == IceBlock.Stage.Shaking) continue;
            if (used != null && used.Contains(block.Cell)) continue;
            Vector2Int d = block.Cell - grid.StartCell;
            if (avoidStart && Mathf.Abs(d.x) <= startClearance && Mathf.Abs(d.y) <= startClearance) continue;
            candidates.Add(block.Cell);
        }
        if (candidates.Count == 0) return grid.StartCell;
        return candidates[Random.Range(0, candidates.Count)];
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

        PlayerSkin playerSkin = player.GetComponent<PlayerSkin>();
        if (playerSkin != null)
        {
            var skin = go.AddComponent<PlayerSkin>();
            skin.models = playerSkin.models;
            skin.animatorController = playerSkin.animatorController;
            skin.modelOffset = playerSkin.modelOffset;
            skin.modelYaw = playerSkin.modelYaw;
            skin.skin = (PlayerSkin.Skin)Random.Range(0, System.Enum.GetValues(typeof(PlayerSkin.Skin)).Length);
        }

        var brain = go.AddComponent<BotBrain>();
        brain.grid = grid;
        brain.manager = this;
        brain.player = player.transform;
        // 봇마다 성격을 조금씩 다르게
        brain.walkSpeed = Random.Range(0.75f, 0.95f);
        brain.stompChance = Random.Range(0.3f, 0.7f);

        go.SetActive(true);
        return brain;
    }
}
