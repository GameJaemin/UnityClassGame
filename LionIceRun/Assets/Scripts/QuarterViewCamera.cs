using UnityEngine;

// 고정 쿼터뷰 카메라.
//  - 에디터와 시작 전: IceGrid 전체가 화면에 들어오는 거리 (그리드 크기·화면 비율이 바뀌면 다시 맞춤)
//  - 게임 시작 후: playDistance로 부드럽게 다가가며, 화면 중심이 맵 중앙에서 플레이어 쪽으로 옮겨간다
//    (플레이어 높이는 따라가지 않아서 떨어질 때 바다로 빠지는 모습이 보인다)
[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class QuarterViewCamera : MonoBehaviour
{
    public enum ZoomTrigger { OnGameStart, OnSceneStart }

    public IceGrid grid;
    [Range(10f, 89f)] public float pitch = 50f;
    public float yaw = 45f;
    [Min(0f)] public float padding = 0.5f;

    [Header("게임 중 거리")]
    [Tooltip("끄면 게임 중에도 맵 전체가 보이는 거리를 유지")]
    public bool usePlayDistance = true;
    [Tooltip("게임 중 카메라 거리 (보는 지점에서 카메라까지)")]
    [Min(1f)] public float playDistance = 22f;
    [Tooltip("OnGameStart: 처음 움직이는 순간 / OnSceneStart: 씬이 시작하자마자")]
    public ZoomTrigger zoomTrigger = ZoomTrigger.OnGameStart;
    [Tooltip("정해 둔 거리까지 다가가는 데 걸리는 시간")]
    [Min(0.01f)] public float zoomDuration = 1.2f;

    [Header("플레이어 따라가기")]
    public Transform player;
    public bool followPlayer = true;
    [Tooltip("클수록 빠르게 따라감")]
    [Min(0.1f)] public float followSharpness = 5f;
    [Tooltip("화면 중심이 맵 가장자리 밖으로 나가지 않게 (1 = 맵 끝까지)")]
    [Range(0f, 1f)] public float followLimit = 0.8f;

    Camera cam;
    float lastAspect;
    Vector2 lastSize;
    float zoomStartTime = float.PositiveInfinity;
    Vector3 followPoint;
    bool followInitialized;

    Camera Cam => cam != null ? cam : cam = GetComponent<Camera>();

    void Start()
    {
        Fit();
        if (!Application.isPlaying) return;

        if (player == null)
        {
            PlayerController found = FindAnyObjectByType<PlayerController>();
            if (found != null) player = found.transform;
        }
        if (zoomTrigger == ZoomTrigger.OnSceneStart) BeginZoom();
        else if (GameManager.Instance != null) GameManager.Instance.StateChanged += OnStateChanged;
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.StateChanged -= OnStateChanged;
    }

    void OnValidate() => Fit();

    void OnStateChanged(GameManager.GameState state)
    {
        if (state == GameManager.GameState.Playing) BeginZoom();
    }

    void BeginZoom()
    {
        if (float.IsPositiveInfinity(zoomStartTime)) zoomStartTime = Time.time;
    }

    void LateUpdate()
    {
        if (grid == null) return;

        if (!Application.isPlaying || !usePlayDistance)
        {
            if (!Mathf.Approximately(Cam.aspect, lastAspect) || grid.Size != lastSize) Fit();
            return;
        }

        // 0(맵 전체) → 1(정해 둔 거리)로 부드럽게
        float blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.time - zoomStartTime) / zoomDuration));
        float distance = Mathf.Lerp(OverviewDistance(), playDistance, blend);

        Vector3 center = grid.transform.position;
        if (followPlayer && player != null)
        {
            Vector3 target = ClampToGrid(new Vector3(player.position.x, center.y, player.position.z));
            if (!followInitialized)
            {
                followPoint = target;
                followInitialized = true;
            }
            followPoint = Vector3.Lerp(followPoint, target, 1f - Mathf.Exp(-followSharpness * Time.deltaTime));
            center = Vector3.Lerp(center, followPoint, blend);
        }
        Place(center, distance);
    }

    [ContextMenu("카메라 맞추기")]
    public void Fit()
    {
        if (grid == null) return;
        Place(grid.transform.position, OverviewDistance());
        lastAspect = Cam.aspect;
        lastSize = grid.Size;
    }

    // 그리드를 감싸는 원이 화면의 좁은 쪽 시야각에 들어오는 거리
    float OverviewDistance()
    {
        float radius = grid.Size.magnitude / 2f + padding;
        float halfV = Cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float halfH = Mathf.Atan(Mathf.Tan(halfV) * Cam.aspect);
        return radius / Mathf.Sin(Mathf.Min(halfV, halfH));
    }

    Vector3 ClampToGrid(Vector3 point)
    {
        Vector3 local = point - grid.transform.position;
        Vector2 half = grid.Size * 0.5f * followLimit;
        local.x = Mathf.Clamp(local.x, -half.x, half.x);
        local.z = Mathf.Clamp(local.z, -half.y, half.y);
        return grid.transform.position + local;
    }

    void Place(Vector3 center, float distance)
    {
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        transform.SetPositionAndRotation(center - rotation * Vector3.forward * distance, rotation);
    }
}
