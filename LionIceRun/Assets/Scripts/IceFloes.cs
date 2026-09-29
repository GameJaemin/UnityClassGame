using UnityEngine;

// 얼음판 주변 바다에 떠다니는 유빙(장식). 게임 시작 시 만들어 둥실거리며 천천히 돈다.
// 충돌체가 없어서 게임에는 영향이 없다.
public class IceFloes : MonoBehaviour
{
    public IceGrid grid;
    public Material iceMaterial;
    [Min(0)] public int count = 22;
    [Tooltip("얼음판 가장자리에서 이만큼 떨어진 곳부터")]
    [Min(0f)] public float innerGap = 4f;
    [Min(1f)] public float outerRadius = 55f;
    public Vector2 sizeRange = new Vector2(0.8f, 4f);
    public int seed = 7;

    struct Floe
    {
        public Transform transform;
        public float phase;
        public float bob;
        public float spin;
    }

    Floe[] floes;

    void Start()
    {
        if (grid == null) return;
        var random = new System.Random(seed);
        float inner = grid.Size.magnitude * 0.5f + innerGap;
        floes = new Floe[count];

        for (int i = 0; i < count; i++)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            // 가까운 곳에 더 많이
            float radius = Mathf.Lerp(inner, outerRadius, Mathf.Pow((float)random.NextDouble(), 1.6f));
            float size = Mathf.Lerp(sizeRange.x, sizeRange.y, (float)random.NextDouble());

            GameObject floe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floe.name = "IceFloe";
            Destroy(floe.GetComponent<Collider>());
            floe.transform.SetParent(transform, false);
            floe.transform.position = grid.transform.position + new Vector3(Mathf.Cos(angle) * radius, grid.SeaLevel - grid.transform.position.y, Mathf.Sin(angle) * radius);
            floe.transform.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
            floe.transform.localScale = new Vector3(size, Mathf.Lerp(0.3f, 0.8f, (float)random.NextDouble()), size * Mathf.Lerp(0.6f, 1.2f, (float)random.NextDouble()));
            if (iceMaterial != null) floe.GetComponent<Renderer>().sharedMaterial = iceMaterial;

            floes[i] = new Floe
            {
                transform = floe.transform,
                phase = (float)random.NextDouble() * 10f,
                bob = Mathf.Lerp(0.08f, 0.2f, (float)random.NextDouble()),
                spin = Mathf.Lerp(-3f, 3f, (float)random.NextDouble()),
            };
        }
    }

    void Update()
    {
        if (floes == null || grid == null) return;
        float time = Time.time;
        foreach (Floe floe in floes)
        {
            Vector3 p = floe.transform.position;
            p.y = grid.SeaLevel + Mathf.Sin(time * 1.3f + floe.phase) * floe.bob;
            floe.transform.position = p;
            floe.transform.Rotate(0f, floe.spin * Time.deltaTime, 0f, Space.World);
        }
    }
}
