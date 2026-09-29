using UnityEngine;

// 얼음 복구 아이템. 블럭 위에서 빙글빙글 돌며 둥실거리고, 사라지기 전에는 깜빡인다.
// 플레이어가 닿으면 스포너에게 알려 주변 깨진 칸을 다시 얼린다.
[RequireComponent(typeof(Collider))]
public class RepairItem : MonoBehaviour
{
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

    public void Init(RepairItemSpawner owner, Vector2Int cell, float life)
    {
        spawner = owner;
        Cell = cell;
        lifetime = life;
        spawnTime = Time.time;
        renderers = GetComponentsInChildren<Renderer>();
        if (visual == null && transform.childCount > 0) visual = transform.GetChild(0);
        if (visual != null) visualBase = visual.localPosition;
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
        if (other.GetComponentInParent<PlayerController>() != null) spawner.OnItemCollected(this);
    }
}
