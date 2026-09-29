using UnityEngine;

// 파도가 부드럽게 휘도록 잘게 나눈 바다 메시를 만든다 (Unity 기본 Plane은 10x10칸이라 너무 거칠다).
// Plane과 같은 10 x 10 크기라서 IceGrid가 바다 크기를 맞추는 방식(스케일)을 그대로 쓴다.
// 파도 모양과 색은 StylizedSea 셰이더가 처리한다.
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
public class SeaSurface : MonoBehaviour
{
    [Tooltip("가로·세로로 나누는 칸 수 (클수록 파도가 부드럽지만 무거움, 250이면 약 6만 정점)")]
    [Range(16, 250)] public int resolution = 250;

    Mesh mesh;
    int builtResolution;

    void OnEnable() => Build();

    // OnValidate에서 메시를 바꾸면 Unity가 경고를 내므로 다음 Update에서 다시 만든다
    void Update()
    {
        if (builtResolution != resolution || GetComponent<MeshFilter>().sharedMesh != mesh) Build();
    }

    void OnDestroy()
    {
        if (mesh != null) DestroyImmediate(mesh);
    }

    void Build()
    {
        if (mesh == null)
        {
            mesh = new Mesh { name = "SeaSurface", hideFlags = HideFlags.DontSave };
        }
        mesh.Clear();

        int n = resolution;
        var vertices = new Vector3[(n + 1) * (n + 1)];
        for (int z = 0; z <= n; z++)
        for (int x = 0; x <= n; x++)
            vertices[z * (n + 1) + x] = new Vector3(x / (float)n * 10f - 5f, 0f, z / (float)n * 10f - 5f);

        var triangles = new int[n * n * 6];
        int t = 0;
        for (int z = 0; z < n; z++)
        for (int x = 0; x < n; x++)
        {
            int i = z * (n + 1) + x;
            triangles[t++] = i;
            triangles[t++] = i + n + 1;
            triangles[t++] = i + 1;
            triangles[t++] = i + 1;
            triangles[t++] = i + n + 1;
            triangles[t++] = i + n + 2;
        }

        mesh.indexFormat = vertices.Length > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        // 셰이더가 정점을 위아래로 움직이므로 화면 밖 판정이 잘리지 않게 넉넉히
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(10f, 4f, 10f));
        GetComponent<MeshFilter>().sharedMesh = mesh;
        builtResolution = n;
    }
}
