using UnityEngine;

// IceGrid 전체가 화면에 들어오도록 고정 쿼터뷰 위치를 계산한다.
// 그리드 크기나 화면 비율이 바뀌면 다시 맞춘다.
[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class QuarterViewCamera : MonoBehaviour
{
    public IceGrid grid;
    [Range(10f, 89f)] public float pitch = 50f;
    public float yaw = 45f;
    [Min(0f)] public float padding = 0.5f;

    Camera cam;
    float lastAspect;
    Vector2 lastSize;

    Camera Cam => cam != null ? cam : cam = GetComponent<Camera>();

    void Start() => Fit();

    void OnValidate() => Fit();

    void LateUpdate()
    {
        if (grid == null) return;
        if (!Mathf.Approximately(Cam.aspect, lastAspect) || grid.Size != lastSize) Fit();
    }

    [ContextMenu("카메라 맞추기")]
    public void Fit()
    {
        if (grid == null) return;

        // 그리드를 감싸는 원이 화면의 좁은 쪽 시야각에 들어오는 거리
        Vector2 size = grid.Size;
        float radius = size.magnitude / 2f + padding;
        float halfV = Cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float halfH = Mathf.Atan(Mathf.Tan(halfV) * Cam.aspect);
        float distance = radius / Mathf.Sin(Mathf.Min(halfV, halfH));

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        transform.SetPositionAndRotation(grid.transform.position - rotation * Vector3.forward * distance, rotation);

        lastAspect = Cam.aspect;
        lastSize = size;
    }
}
