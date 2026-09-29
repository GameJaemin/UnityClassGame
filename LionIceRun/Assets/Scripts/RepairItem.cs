using UnityEngine;

// 파워업 아이템 (처음엔 얼음 복구만 있어서 이름이 RepairItem). 블럭 위에서 빙글빙글 돌며 둥실거리고,
// 사라지기 전에는 깜빡인다. 종류마다 결정 색이 다르다. 플레이어가 닿으면 스포너에게 알린다.
[RequireComponent(typeof(Collider))]
public class RepairItem : MonoBehaviour
{
    public enum Kind { Repair, Shield, SuperJump, Harden }

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [Tooltip("돌고 둥실거리는 모양 (자식 오브젝트)")]
    public Transform visual;
    [Min(0f)] public float spinSpeed = 120f;
    [Min(0f)] public float bobHeight = 0.12f;
    [Min(0f)] public float bobSpeed = 3f;
    [Tooltip("사라지기 전 깜빡이는 시간")]
    [Min(0f)] public float blinkTime = 2f;

    RepairItemSpawner spawner;
    Renderer[] renderers;
    float spawnTime;
    float lifetime;
    Vector3 visualBase;

    public Vector2Int Cell { get; private set; }
    public Kind ItemKind { get; private set; }

    public static Color ColorOf(Kind kind) => kind switch
    {
        Kind.Shield => new Color(0.70f, 0.50f, 1f),
        Kind.SuperJump => new Color(1f, 0.85f, 0.25f),
        Kind.Harden => new Color(0.95f, 0.98f, 1f),
        _ => new Color(0.6f, 0.95f, 1f),
    };

    public static string NameOf(Kind kind) => kind switch
    {
        Kind.Shield => "방패",
        Kind.SuperJump => "슈퍼 점프",
        Kind.Harden => "얼음 굳히기",
        _ => "얼음 복구",
    };

    public void Init(RepairItemSpawner owner, Vector2Int cell, float life, Kind kind)
    {
        spawner = owner;
        Cell = cell;
        lifetime = life;
        ItemKind = kind;
        spawnTime = Time.time;
        renderers = GetComponentsInChildren<Renderer>();
        if (visual == null && transform.childCount > 0) visual = transform.GetChild(0);
        if (visual != null) visualBase = visual.localPosition;

        Color color = ColorOf(kind);
        var properties = new MaterialPropertyBlock();
        properties.SetColor(BaseColorId, color);
        properties.SetColor(EmissionColorId, color * 1.2f);
        foreach (Renderer r in renderers) r.SetPropertyBlock(properties);
    }

    void Update()
    {
        float age = Time.time - spawnTime;
        if (visual != null)
        {
            visual.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
            visual.localPosition = visualBase + Vector3.up * (Mathf.Sin(age * bobSpeed) * bobHeight);
        }

        float remaining = lifetime - age;
        if (remaining <= 0f)
        {
            spawner.OnItemExpired(this);
            return;
        }
        // 사라지기 직전일수록 빠르게 깜빡임
        if (remaining < blinkTime)
        {
            float rate = Mathf.Lerp(12f, 4f, remaining / blinkTime);
            bool visible = Mathf.Sin(age * rate * Mathf.PI) > 0f;
            foreach (Renderer r in renderers) r.enabled = visible;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // 플레이어만 먹는다 (AI 봇은 지나가도 안 먹음)
        PlayerController controller = other.GetComponentInParent<PlayerController>();
        if (controller != null && !controller.useExternalInput) spawner.OnItemCollected(this, controller);
    }
}
