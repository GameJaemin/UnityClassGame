using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// 가로 x 세로 얼음 블럭 그리드를 만든다.
// 그리드 중앙이 이 오브젝트 위치에 오고, 블럭 윗면이 y = 0이 된다.
public class IceGrid : MonoBehaviour
{
    [Header("그리드 크기")]
    [Min(1)] public int width = 6;
    [Min(1)] public int depth = 6;

    [Header("블럭")]
    public GameObject blockPrefab;
    [Min(0.1f)] public float blockSize = 1.5f;
    [Min(0.05f)] public float blockHeight = 0.6f;
    [Min(0f)] public float gap = 0.1f;

    [Header("바다")]
    public Transform sea;
    [Tooltip("블럭 윗면에서 바다 수면까지 깊이. 떨어지는 모습이 보이도록 깊게 (6x6 기준 15m를 넘으면 화면 밖)")]
    [Min(0f)] public float seaDepth = 12f;
    [Tooltip("얼음판 바깥으로 바다를 얼마나 더 넓힐지 (화면 끝까지 바다가 보이게)")]
    [Min(0f)] public float seaMargin = 190f;

    [Header("얼음 깨짐 (Thin Ice 바탕, 무적 시간 없음)")]
    [Tooltip("같은 발걸음이 이음새에서 두 번 세어지지 않게 막는 짧은 시간. 점프 착지는 이보다 길어서 항상 바로 한 단계 진행")]
    [Min(0f)] public float touchDebounceTime = 0.2f;
    [Tooltip("흔들리기 시작해서 깨져 떨어질 때까지. 이 안에 점프로 탈출할 수 있고, 다시 밟으면 즉시 깨짐")]
    [Min(0f)] public float breakDelay = 1f;
    [Tooltip("한 블럭에 이 시간 이상 서 있으면 다시 밟지 않아도 한 단계 진행")]
    [Min(0.1f)] public float standTimeToCrack = 2f;
    [Tooltip("단계별 색: 멀쩡함, 살짝 금, 많이 금, 흔들림")]
    public Color[] stageColors =
    {
        new Color(0.72f, 0.90f, 0.98f),
        new Color(0.58f, 0.79f, 0.94f),
        new Color(0.45f, 0.64f, 0.88f),
        new Color(0.36f, 0.50f, 0.80f),
    };

    [Header("얼음 그래픽 (LionIceRun > Step 4 얼음 그래픽 세팅으로 자동 설정)")]
    [Tooltip("금 무늬 벌 수. 블럭마다 무작위로 한 벌을 고르고 가로·세로로 무작위로 뒤집는다")]
    [Min(1)] public int crackVariantCount = 1;
    [Tooltip("단계별 금 간 텍스처. 순서: 무늬마다 멀쩡함, 살짝 금, 많이 금, 흔들림")]
    public Texture2D[] stageTextures;
    [Tooltip("단계가 바뀔 때 새 금이 자라는 중간 텍스처. 순서: 무늬마다 단계1 프레임들, 단계2 프레임들, 단계3 프레임들")]
    public Texture2D[] crackGrowFrames;
    [Min(0)] public int crackGrowFramesPerStage = 3;
    [Tooltip("새 금이 끝까지 뻗는 데 걸리는 시간")]
    [Min(0f)] public float crackGrowTime = 0.25f;
    [Tooltip("깨질 때 튀는 조각 재질")]
    public Material shardMaterial;
    [Tooltip("금 갈 때·깨질 때 튀는 얼음 가루 재질")]
    public Material particleMaterial;
    [Tooltip("깨질 때 가로·세로로 나누는 조각 수 (높이는 한 칸 적게)")]
    [Range(1, 5)] public int shardsPerAxis = 3;

    readonly List<GameObject> blocks = new List<GameObject>();
    readonly List<IceBlock> iceBlocks = new List<IceBlock>();
    ParticleSystem iceBurst;

    public IReadOnlyList<GameObject> Blocks => blocks;
    // 게임 중에만 채워진다 (에디터 미리보기에서는 비어 있음)
    public IReadOnlyList<IceBlock> IceBlocks => iceBlocks;
    public float Spacing => blockSize + gap;
    public Vector2 Size => new Vector2(width * Spacing - gap, depth * Spacing - gap);
    public Vector2Int StartCell => new Vector2Int(width / 2, depth / 2);
    // 바다 수면 높이 (여기보다 아래로 떨어지면 게임 오버)
    public float SeaLevel => transform.position.y - seaDepth;

    public bool InBounds(Vector2Int cell) => cell.x >= 0 && cell.y >= 0 && cell.x < width && cell.y < depth;

    // 게임 중에만 동작. Build에서 x 바깥, z 안쪽 순서로 만들어서 index = x * depth + z
    public IceBlock GetBlock(Vector2Int cell)
    {
        if (!InBounds(cell)) return null;
        int index = cell.x * depth + cell.y;
        return index < iceBlocks.Count ? iceBlocks[index] : null;
    }

    // 해당 칸 블럭 윗면 중앙의 월드 좌표
    public Vector3 CellTop(Vector2Int cell)
    {
        Vector3 local = new Vector3((cell.x - (width - 1) / 2f) * Spacing, 0f, (cell.y - (depth - 1) / 2f) * Spacing);
        return transform.TransformPoint(local);
    }

    void Start() => Build();

    void OnValidate() => UpdateSea();

    [ContextMenu("그리드 생성")]
    public void Build()
    {
        Clear();
        if (blockPrefab == null)
        {
            Debug.LogWarning("IceGrid: 블럭 프리팹이 비어 있습니다.", this);
            return;
        }

        Vector3 origin = new Vector3(-(width - 1) * Spacing / 2f, -blockHeight / 2f, -(depth - 1) * Spacing / 2f);
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                GameObject block = CreateBlock();
                block.name = $"Ice_{x}_{z}";
                block.transform.localPosition = origin + new Vector3(x * Spacing, 0f, z * Spacing);
                block.transform.localRotation = Quaternion.identity;
                block.transform.localScale = new Vector3(blockSize, blockHeight, blockSize);
                blocks.Add(block);

                if (Application.isPlaying)
                {
                    IceBlock ice = block.GetComponent<IceBlock>();
                    if (ice == null) ice = block.AddComponent<IceBlock>();
                    ice.Init(this, new Vector2Int(x, z));
                    iceBlocks.Add(ice);
                }
            }
        }
        UpdateSea();
    }

    [ContextMenu("그리드 삭제")]
    public void Clear()
    {
        blocks.Clear();
        iceBlocks.Clear();
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
    }

    // 얼음 가루를 튀긴다. spread: 퍼지는 세기 배율
    public void PlayIceBurst(Vector3 position, int count, float spread)
    {
        if (particleMaterial == null) return;
        if (iceBurst == null) iceBurst = CreateIceBurst();

        var emit = new ParticleSystem.EmitParams { applyShapeToPosition = true };
        for (int i = 0; i < count; i++)
        {
            emit.position = position;
            emit.velocity = (Random.insideUnitSphere + Vector3.up) * Random.Range(1.5f, 3.5f) * spread;
            emit.startSize = Random.Range(0.05f, 0.14f);
            iceBurst.Emit(emit, 1);
        }
    }

    ParticleSystem CreateIceBurst()
    {
        // 그리드를 다시 만들 때 지워지지 않도록 그리드 밖(씬 루트)에 둔다
        var go = new GameObject("IceBurst");
        ParticleSystem system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = system.main;
        main.playOnAwake = false;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f), new Color(0.75f, 0.9f, 1f));
        main.gravityModifier = 1.5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 500;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(blockSize, 0.1f, blockSize);

        ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        ParticleSystem.RotationOverLifetimeModule rotation = system.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-6f, 6f);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = particleMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        return system;
    }

    GameObject CreateBlock()
    {
#if UNITY_EDITOR
        // 에디터 미리보기에서는 프리팹 연결을 유지한다
        if (!Application.isPlaying)
            return (GameObject)PrefabUtility.InstantiatePrefab(blockPrefab, transform);
#endif
        return Instantiate(blockPrefab, transform);
    }

    public void UpdateSea()
    {
        if (sea == null) return;
        sea.position = transform.position + Vector3.down * seaDepth;
        // Unity 기본 Plane은 10 x 10 크기
        sea.localScale = new Vector3((Size.x + seaMargin) / 10f, 1f, (Size.y + seaMargin) / 10f);
    }
}
