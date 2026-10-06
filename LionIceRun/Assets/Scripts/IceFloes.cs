using System.Collections.Generic;
using UnityEngine;

// 얼음판 주변 바다에 떠다니는 유빙(장식). 충돌체가 없어서 게임에는 영향이 없다.
// 유빙은 이 오브젝트의 자식으로 씬에 미리 만들어 두고(메뉴 LionIceRun > Step 2 유빙 씬에 만들기) 씬 뷰에서 직접 옮긴다.
// 자식을 복제(Ctrl+D)하면 그대로 유빙으로 쓰인다.
// 실행 중에는 바다 파도를 따라 오르내리고 기울며 천천히 돈다.
// 에디터·실행 모두 유빙의 위치와 크기를 바다 셰이더에 넘겨 둘레에 물거품을 그린다.
[ExecuteAlways]
public class IceFloes : MonoBehaviour
{
    // StylizedSea 셰이더의 배열 크기와 같아야 한다
    public const int MaxFloes = 64;

    public IceGrid grid;
    public Material iceMaterial;

    [Header("새로 만들 때 (메뉴로 다시 만들 때만 사용)")]
    [Min(0)] public int count = 22;
    [Tooltip("얼음판 가장자리에서 이만큼 떨어진 곳부터")]
    [Min(0f)] public float innerGap = 4f;
    [Min(1f)] public float outerRadius = 55f;
    public Vector2 sizeRange = new Vector2(0.8f, 4f);
    public int seed = 7;

    [Header("움직임")]
    [Tooltip("파도 위에서 추가로 둥실거리는 높이")]
    [Min(0f)] public float extraBob = 0.06f;
    [Tooltip("파도 기울기를 따라 기우는 정도 (0이면 수평 유지)")]
    [Range(0f, 1f)] public float tilt = 0.5f;

    struct Floe
    {
        public Transform transform;
        public Vector3 basePosition;
        public Quaternion baseRotation;
        public float phase;
        public float spin;
        public float angle;
    }

    readonly List<Floe> floes = new List<Floe>();
    readonly Vector4[] foamBoxes = new Vector4[MaxFloes];
    readonly Vector4[] foamExtras = new Vector4[MaxFloes];
    Material seaMaterial;

    static readonly int FloeBoxesId = Shader.PropertyToID("_FloeBoxes");
    static readonly int FloeExtrasId = Shader.PropertyToID("_FloeExtras");
    static readonly int FloeCountId = Shader.PropertyToID("_FloeCount");

    void Start()
    {
        if (!Application.isPlaying) return;
        if (grid != null && grid.sea != null)
        {
            var seaRenderer = grid.sea.GetComponent<Renderer>();
            if (seaRenderer != null) seaMaterial = seaRenderer.sharedMaterial;
        }

        var random = new System.Random(seed);
        floes.Clear();
        foreach (Transform child in transform)
        {
            floes.Add(new Floe
            {
                transform = child,
                basePosition = child.position,
                baseRotation = child.rotation,
                phase = (float)random.NextDouble() * 10f,
                spin = Mathf.Lerp(-3f, 3f, (float)random.NextDouble()),
            });
        }
    }

    void OnDisable()
    {
        Shader.SetGlobalFloat(FloeCountId, 0f);
    }

    void Update()
    {
        if (Application.isPlaying) Animate();
        SendFoamData();
    }

    void Animate()
    {
        if (grid == null) return;
        float time = Time.timeSinceLevelLoad;  // 셰이더의 _Time.y와 같은 시간
        float seaLevel = grid.SeaLevel;
        for (int i = 0; i < floes.Count; i++)
        {
            Floe floe = floes[i];
            if (floe.transform == null) continue;

            Vector3 p = floe.basePosition;
            float wave = WaveHeight(new Vector2(p.x, p.z), time, out Vector2 slope);
            // 처음 놓인 높이(수면 기준)를 유지한 채 파도를 탄다
            p.y = floe.basePosition.y + wave + Mathf.Sin(time * 1.3f + floe.phase) * extraBob;
            floe.transform.position = p;

            floe.angle += floe.spin * Time.deltaTime;
            Vector3 normal = Vector3.Lerp(Vector3.up, new Vector3(-slope.x, 1f, -slope.y).normalized, tilt);
            floe.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0f, floe.angle, 0f) * floe.baseRotation;
            floes[i] = floe;
        }
    }

    // StylizedSea 셰이더의 Waves()와 같은 계산 (유빙이 보이는 파도와 함께 오르내리도록)
    static readonly Vector2[] WaveDirs = { new Vector2(1f, 0.31f), new Vector2(-0.43f, 1f), new Vector2(0.71f, -0.83f), new Vector2(-0.97f, -0.24f), new Vector2(0.19f, 0.94f) };
    static readonly float[] WaveLengths = { 1f, 0.618f, 0.433f, 0.291f, 0.173f };
    static readonly float[] WaveAmps = { 1f, 0.56f, 0.34f, 0.18f, 0.09f };
    static readonly float[] WaveSpeeds = { 1f, 1.27f, 1.61f, 1.93f, 2.37f };

    float WaveHeight(Vector2 p, float time, out Vector2 slope)
    {
        slope = Vector2.zero;
        if (seaMaterial == null || !seaMaterial.HasProperty("_WaveHeight")) return 0f;
        float height = seaMaterial.GetFloat("_WaveHeight");
        float length = Mathf.Max(seaMaterial.GetFloat("_WaveLength"), 0.01f);
        float t = time * seaMaterial.GetFloat("_WaveSpeed");
        float h = 0f;
        for (int i = 0; i < 5; i++)
        {
            Vector2 d = WaveDirs[i].normalized;
            float k = 2f * Mathf.PI / (length * WaveLengths[i]);
            float phase = Vector2.Dot(d, p) * k + t * WaveSpeeds[i] + i * 1.7f;
            float a = height * WaveAmps[i];
            h += Mathf.Sin(phase) * a;
            slope += d * (Mathf.Cos(phase) * a * k);
        }
        return h;
    }

    // 유빙마다 (중심 x, z, 반너비 x, z) / (가로축 방향 x, z, 바닥 높이, 윗면 높이)
    void SendFoamData()
    {
        int n = 0;
        foreach (Transform child in transform)
        {
            if (n >= MaxFloes) break;
            if (!child.gameObject.activeInHierarchy) continue;
            Vector3 right = child.right;
            Vector2 axis = new Vector2(right.x, right.z);
            if (axis.sqrMagnitude < 0.0001f) axis = Vector2.right;
            axis.Normalize();
            Vector3 scale = child.lossyScale;
            Bounds bounds = child.TryGetComponent(out Renderer renderer) ? renderer.bounds : new Bounds(child.position, scale);
            foamBoxes[n] = new Vector4(child.position.x, child.position.z, Mathf.Abs(scale.x) * 0.5f, Mathf.Abs(scale.z) * 0.5f);
            foamExtras[n] = new Vector4(axis.x, axis.y, bounds.min.y, bounds.max.y);
            n++;
        }
        Shader.SetGlobalVectorArray(FloeBoxesId, foamBoxes);
        Shader.SetGlobalVectorArray(FloeExtrasId, foamExtras);
        Shader.SetGlobalFloat(FloeCountId, n);
    }

    // 자식 유빙을 모두 지우고 설정값으로 새로 흩뿌린다 (에디터 메뉴에서 호출)
    public void Generate()
    {
        if (grid == null) return;
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        var random = new System.Random(seed);
        float inner = grid.Size.magnitude * 0.5f + innerGap;
        for (int i = 0; i < count; i++)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            // 가까운 곳에 더 많이
            float radius = Mathf.Lerp(inner, outerRadius, Mathf.Pow((float)random.NextDouble(), 1.6f));
            float size = Mathf.Lerp(sizeRange.x, sizeRange.y, (float)random.NextDouble());

            GameObject floe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floe.name = "IceFloe";
            DestroyImmediate(floe.GetComponent<Collider>());
            floe.transform.SetParent(transform, false);
            floe.transform.position = grid.transform.position + new Vector3(Mathf.Cos(angle) * radius, grid.SeaLevel - grid.transform.position.y, Mathf.Sin(angle) * radius);
            floe.transform.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
            floe.transform.localScale = new Vector3(size, Mathf.Lerp(0.3f, 0.8f, (float)random.NextDouble()), size * Mathf.Lerp(0.6f, 1.2f, (float)random.NextDouble()));
            if (iceMaterial != null) floe.GetComponent<Renderer>().sharedMaterial = iceMaterial;
        }
    }
}
