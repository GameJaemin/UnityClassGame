using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 시련 공통: 버틴 시간이 startTime을 넘으면 intervalRange 간격으로 Run()을 반복한다 (게임 진행 중에만).
// 처음 나올 때 한 번 안내 문구를 띄운다. 모든 시련은 예고가 있어 피할 수 있게 만든다.
// 대상은 플레이어와 AI 봇 모두(PlayerController). 플레이어의 방패는 맞는 것을 한 번 막는다.
public abstract class TrialBase : MonoBehaviour
{
    public IceGrid grid;
    public PlayerController player;

    [Header("등장 (버틴 시간 기준)")]
    [Min(0f)] public float startTime = 15f;
    public Vector2 intervalRange = new Vector2(5f, 7f);
    [Tooltip("처음 나올 때 한 번 띄우는 안내")]
    public string announcement = "시련!";
    public Color announcementColor = new Color(1f, 0.55f, 0.75f);

    float nextTime = float.NegativeInfinity;
    bool announced;

    protected virtual void Start() => nextTime = startTime;

    void Update()
    {
        if (OnlineGameSession.InRoom && !OnlineGameSession.Authority) return;
        GameManager manager = GameManager.Instance;
        SurvivalTimer timer = SurvivalTimer.Instance;
        if (manager == null || timer == null || manager.State != GameManager.GameState.Playing || grid == null) return;
        if (timer.Elapsed < nextTime) return;

        if (!announced)
        {
            announced = true;
            Toast(announcement, announcementColor);
        }
        StartCoroutine(Run(timer.Elapsed));
        nextTime = timer.Elapsed + Random.Range(Mathf.Min(intervalRange.x, intervalRange.y), Mathf.Max(intervalRange.x, intervalRange.y));
    }

    protected abstract IEnumerator Run(float elapsed);

    // ---- 도우미 ----

    protected static void Toast(string text, Color color)
    {
        if (GameHUD.Instance != null) GameHUD.Instance.ShowToast(text, color);
        OnlineGameSession.BroadcastToast(text, color);
    }

    public void ResetMatch()
    {
        StopAllCoroutines();
        nextTime = startTime;
        announced = false;
    }

    // 지금 켜져 있는 캐릭터 (플레이어 + 봇)
    protected static List<PlayerController> Targets()
    {
        var list = new List<PlayerController>();
        foreach (PlayerController controller in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            if (controller.isActiveAndEnabled) list.Add(controller);
        return list;
    }

    // 맞았는지: 플레이어는 방패가 있으면 막힌다
    protected static bool Hit(PlayerController target) => !PlayerPowerUps.Blocked(target);

    // 깨지지 않은 칸 하나. nearPlayerChance 확률로 플레이어 주변 nearRadius 안에서
    protected bool PickCell(float nearPlayerChance, int nearRadius, out Vector2Int cell)
    {
        var candidates = new List<Vector2Int>();
        bool near = player != null && Random.value < nearPlayerChance;
        Vector2Int center = player != null ? grid.WorldToCell(player.transform.position) : grid.StartCell;
        foreach (IceBlock block in grid.IceBlocks)
        {
            if (block.IsBroken) continue;
            var candidate = grid.WorldToCell(block.transform.position);
            if (grid.GetBlock(candidate) != block) continue;
            if (near && (Mathf.Abs(candidate.x - center.x) > nearRadius || Mathf.Abs(candidate.y - center.y) > nearRadius)) continue;
            candidates.Add(candidate);
        }
        cell = candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : default;
        return candidates.Count > 0;
    }

    // 칸 위에 누운 원판 (예고 표시). 충돌 없음
    protected static GameObject Disc(Vector3 center, Material material, float diameter)
    {
        GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = "TrialMarker";
        OnlineWorldVisual.Track(disc, material != null && material.name.Contains("Danger") ? 7 : 0);
        Destroy(disc.GetComponent<Collider>());
        disc.transform.position = center + Vector3.up * 0.02f;
        disc.transform.localScale = new Vector3(diameter, 0.01f, diameter);
        var renderer = disc.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return disc;
    }

    // 떨어지는 물체용: 그림자처럼 충돌 없는 기본 도형
    protected static GameObject Prop(PrimitiveType type, Material material, Vector3 position, Vector3 scale)
    {
        GameObject prop = GameObject.CreatePrimitive(type);
        prop.name = "TrialProp";
        OnlineWorldVisual.Track(prop, type == PrimitiveType.Sphere ? 1 : type == PrimitiveType.Cylinder ? 2 : 3);
        Destroy(prop.GetComponent<Collider>());
        prop.transform.position = position;
        prop.transform.localScale = scale;
        if (material != null) prop.GetComponent<Renderer>().sharedMaterial = material;
        return prop;
    }

    // 캐릭터 발밑 기준으로 가까운지 (수평 거리 + 높이 범위)
    protected static bool Near(PlayerController target, Vector3 point, float radius, float height)
    {
        Vector3 feet = target.transform.position + Vector3.down;
        Vector2 d = new Vector2(feet.x - point.x, feet.z - point.z);
        return d.magnitude < radius && Mathf.Abs(feet.y - point.y) < height;
    }
}
