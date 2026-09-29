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
    [Min(0f)] public float seaMargin = 30f;

    readonly List<GameObject> blocks = new List<GameObject>();

    public IReadOnlyList<GameObject> Blocks => blocks;
    public float Spacing => blockSize + gap;
    public Vector2 Size => new Vector2(width * Spacing - gap, depth * Spacing - gap);
    public Vector2Int StartCell => new Vector2Int(width / 2, depth / 2);

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
            }
        }
        UpdateSea();
    }

    [ContextMenu("그리드 삭제")]
    public void Clear()
    {
        blocks.Clear();
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
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

    void UpdateSea()
    {
        if (sea == null) return;
        sea.position = transform.position + Vector3.down * seaDepth;
        // Unity 기본 Plane은 10 x 10 크기
        sea.localScale = new Vector3((Size.x + seaMargin) / 10f, 1f, (Size.y + seaMargin) / 10f);
    }
}
