using System.Collections.Generic;
using UnityEngine;

// 맵 JSON에서 생성되는 기믹. 온라인은 동일 서버 시각으로 움직이고 타격은 호스트만 처리한다.
[DefaultExecutionOrder(-100)]
public class MapGimmickRig : MonoBehaviour
{
    class Device
    {
        public MapGimmickData data;
        public Transform root, head;
        public Rigidbody body;
        public MapMovingSurface surface;
        public Renderer lamp;
        public Vector3 origin;
        public int cycle = -1;
        public readonly List<IceBlock> blocks = new List<IceBlock>();
        public readonly HashSet<PlayerController> hit = new HashSet<PlayerController>();
        public readonly Dictionary<PlayerController, float> nextHit = new Dictionary<PlayerController, float>();
    }
    IceGrid grid;
    readonly List<Device> devices = new List<Device>();
    readonly List<Material> materials = new List<Material>();
    float nextDecay = 120;
    bool warned;
    public bool HasMovingIce { get; private set; }
    public int DeviceCount => devices.Count;

    public void Create(IceGrid owner)
    {
        grid = owner;
        Color accent = ThemeColor(grid.Map.theme);
        // 충돌 없는 외곽 장식으로 맵의 테마를 구분한다.
        for (int corner = 0; corner < 4; corner++)
        {
            Vector3 anchor = grid.StaticCellTop(new Vector2Int(corner % 2 == 0 ? 0 : grid.width - 1, corner < 2 ? 0 : grid.depth - 1));
            anchor += new Vector3(corner % 2 == 0 ? -1.5f : 1.5f, -0.5f, corner < 2 ? -1.5f : 1.5f);
            var tower = new GameObject("테마 표지"); tower.transform.SetParent(transform, false); tower.transform.position = anchor;
            Part(tower.transform, Vector3.zero, new Vector3(0.6f, 2.4f, 0.6f), accent, false);
            Part(tower.transform, new Vector3(0, 1.2f, 0), new Vector3(1.1f, 0.4f, 1.1f), Color.white, false);
        }
        foreach (var g in grid.Map.gimmicks)
        {
            if (g == null || !MapGimmickData.IsSupported(g.kind))
            { Debug.LogWarning("맵의 기믹 종류를 확인하세요."); continue; }
            var go = new GameObject("기믹_" + g.kind); go.transform.SetParent(transform, false);
            var d = new Device { data = g, root = go.transform, origin = grid.StaticCellTop(new Vector2Int(0, 0))
                + new Vector3(g.x * grid.Spacing, 0, g.z * grid.Spacing) };
            go.transform.position = d.origin;
            d.body = go.AddComponent<Rigidbody>(); d.body.isKinematic = true;
            if (MapGimmickData.IsSurface(g.kind))
            {
                d.surface = go.AddComponent<MapMovingSurface>();
                d.surface.MovingGeometry = g.kind != "conveyor" && g.kind != "blink";
                d.surface.Owner = grid; d.surface.RotationSpeed = g.kind == "rotate" ? g.speed : 0;
                d.surface.HitSpeed = g.hitSpeed; d.surface.LiftSpeed = g.liftSpeed; d.surface.DownSeconds = g.knockDownSeconds;
                d.surface.PreviousPosition = d.origin; d.surface.PreviousRotation = Quaternion.identity;
                foreach (var block in grid.IceBlocks)
                    if (Mathf.Abs(block.Cell.x - g.x) < g.width / 2 && Mathf.Abs(block.Cell.y - g.z) < g.depth / 2)
                    {
                        block.transform.SetParent(go.transform, true);
                        block.Init(grid, block.Cell);
                        d.blocks.Add(block);
                    }
                HasMovingIce |= g.kind != "conveyor";
                // 측면 테두리와 방향 표시만 생성: 부서진 얼음 아래에 영구 발판을 남기지 않는다.
                Part(d.root, new Vector3(0, -0.5f, -g.depth * grid.Spacing / 2),
                    new Vector3(g.width * grid.Spacing, 0.15f, 0.12f), accent, false);
                d.lamp = Part(d.root, new Vector3(0, 0.025f, 0), new Vector3(0.6f, 0.04f, 0.9f), Color.yellow, false).GetComponent<Renderer>();
            }
            else
            {
                go.transform.rotation = Quaternion.Euler(0, g.angle, 0);
                Part(d.root, new Vector3(0, -0.5f, 0), new Vector3(0.7f, 0.8f, 0.7f), new Color(0.12f, 0.2f, 0.35f), false);
                d.head = Part(d.root, new Vector3(0, 0.65f, 0),
                    new Vector3(g.width * grid.Spacing, 0.7f, g.depth * grid.Spacing), new Color(1, 0.25f, 0.45f), true);
                d.lamp = Part(d.head, new Vector3(0, 0.55f, 0), new Vector3(0.3f, 0.1f, 0.3f), Color.green, false).GetComponent<Renderer>();
                if (g.kind == "launcher")
                {
                    d.head.localPosition = new Vector3(0, 0.08f, 0);
                    d.head.localScale = new Vector3(g.width * grid.Spacing, 0.16f, g.depth * grid.Spacing);
                    d.head.GetComponent<Renderer>().sharedMaterial.color = new Color(1, 0.8f, 0.15f);
                }
                if (g.kind == "pendulum")
                {
                    d.origin += Vector3.up * 2.5f; d.root.position = d.origin;
                    d.head.localPosition = new Vector3(0, -1.5f, 0);
                    Part(d.root, new Vector3(0, -0.6f, 0), new Vector3(0.15f, 1.5f, 0.15f), Color.white, false);
                }
            }
            devices.Add(d);
        }
    }

    static Color ThemeColor(string theme)
    {
        switch (theme)
        {
            case "candy": return new Color(1, 0.35f, 0.7f);
            case "factory": case "foundry": return new Color(1, 0.55f, 0.1f);
            case "cargo": return new Color(1, 0.85f, 0.2f);
            case "garden": return new Color(0.35f, 0.85f, 0.35f);
            case "clock": return new Color(0.7f, 0.4f, 1);
            case "lagoon": return new Color(0.2f, 1, 0.75f);
            default: return new Color(0.2f, 0.65f, 1);
        }
    }

    Transform Part(Transform parent, Vector3 position, Vector3 size, Color color, bool collider)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.transform.SetParent(parent, false);
        go.transform.localPosition = position; go.transform.localScale = size;
        if (!collider) { go.GetComponent<Collider>().enabled = false; Destroy(go.GetComponent<Collider>()); }
        else go.GetComponent<Collider>().isTrigger = true;
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit")); material.color = color;
        materials.Add(material); go.GetComponent<Renderer>().sharedMaterial = material;
        return go.transform;
    }

    void FixedUpdate()
    {
        if (grid == null) return;
        var gm = GameManager.Instance;
        bool playing = gm != null && gm.State == GameManager.GameState.Playing;
        if (OnlineGameSession.InRoom) playing = OnlineGameSession.MatchPlaying;
        float t = OnlineGameSession.InRoom ? OnlineGameSession.Instance.GimmickSeconds : gm != null && gm.timer != null ? gm.timer.Elapsed : 0;
        foreach (var d in devices)
        {
            var g = d.data;
            if (d.surface != null)
            {
                d.surface.PreviousPosition = d.root.position; d.surface.PreviousRotation = d.root.rotation;
                Vector3 position = d.origin;
                Quaternion rotation = Quaternion.identity;
                if (g.kind == "move") position += new Vector3(g.dx, 0, g.dz) * grid.Spacing
                    * (0.5f - 0.5f * Mathf.Cos(2 * Mathf.PI * t / Mathf.Max(2, g.period)));
                if (g.kind == "rotate") rotation = Quaternion.Euler(0, t * g.speed, 0);
                if (g.kind == "seesaw") rotation = Quaternion.Euler(0, g.angle, 0)
                    * Quaternion.Euler(0, 0, Mathf.Sin((t + g.phase) * 2 * Mathf.PI / Mathf.Max(3, g.period)) * Mathf.Clamp(g.tiltAngle, 5, 25));
                d.body.position = position; d.body.rotation = rotation;
                d.root.SetPositionAndRotation(position, rotation);
                d.surface.ConveyorVelocity = playing && g.kind == "conveyor" ? Quaternion.Euler(0, g.angle, 0) * Vector3.forward * g.speed : Vector3.zero;
                if (g.kind == "blink")
                {
                    float clock = Mathf.Repeat(t + g.phase, Mathf.Max(3, g.period));
                    float on = Mathf.Clamp(g.activeDuration, 1.5f, Mathf.Max(3, g.period) - 1);
                    bool exists = !playing || clock < on;
                    foreach (var block in d.blocks)
                    {
                        block.SetSurfacePresent(exists);
                        block.SetSurfaceWarning(playing && exists ? Mathf.Clamp01(clock - on + 1) : 0);
                    }
                    d.lamp.enabled = exists;
                    d.lamp.sharedMaterial.color = clock > on - 1 ? Color.red : Color.yellow;
                }
            }
            else if (d.head != null)
            {
                float period = Mathf.Max(3, g.period), clock = (t + g.phase) % period;
                int cycle = Mathf.FloorToInt((t + g.phase) / period);
                if (cycle != d.cycle) { d.cycle = cycle; d.hit.Clear(); }
                bool active;
                if (g.kind == "piston")
                {
                    float extension = clock < 0.9f ? 0 : clock < 1.25f ? (clock - 0.9f) / 0.35f
                        : clock < 1.6f ? 1 : clock < 2.3f ? 1 - (clock - 1.6f) / 0.7f : 0;
                    d.head.localPosition = new Vector3(0, 0.65f, extension * g.dz * grid.Spacing);
                    active = clock >= 0.9f && clock < 1.6f;
                    d.lamp.sharedMaterial.color = clock < 0.9f ? Color.yellow : active ? Color.red : Color.green;
                }
                else if (g.kind == "pendulum")
                {
                    d.body.rotation = Quaternion.Euler(0, g.angle, 0)
                        * Quaternion.Euler(0, 0, Mathf.Sin((t + g.phase) * 2 * Mathf.PI / period) * Mathf.Clamp(g.tiltAngle, 20, 70));
                    d.root.rotation = d.body.rotation;
                    active = true;
                }
                else if (g.kind == "launcher")
                {
                    active = true;
                    d.lamp.sharedMaterial.color = Color.yellow;
                }
                else
                {
                    d.body.rotation = Quaternion.Euler(0, g.angle + t * g.speed, 0);
                    d.root.rotation = d.body.rotation;
                    active = true;
                }
                if (playing && active && !grid.networkReplica) Hit(d);
            }
        }
        Physics.SyncTransforms();
        grid.RefreshMovingCells();
        if (playing && !warned && t >= 115)
        { warned = true; GameHUD.Instance?.ShowToast("5초 뒤 전체 빙판에 금이 가요!", new Color(1, 0.5f, 0.1f)); }
        // 일반 얼음의 전체 붕괴는 120초부터 천천히 진행하며 안전지대는 유지한다.
        if (playing && !grid.networkReplica && t >= nextDecay)
        { nextDecay += 10; foreach (var ice in grid.IceBlocks) ice.ForceAdvance(); }
    }

    void Hit(Device d)
    {
        var box = d.head.GetComponent<BoxCollider>();
        var targets = Physics.OverlapBox(d.head.TransformPoint(box.center), Vector3.Scale(box.size, d.head.lossyScale) * 0.5f,
            d.head.rotation, ~0, QueryTriggerInteraction.Ignore);
        foreach (var collider in targets)
        {
            var player = collider.GetComponentInParent<PlayerController>();
            if (player == null || player.networkReplica) continue;
            if (d.data.kind == "launcher")
            {
                if (!player.IsGrounded || (d.nextHit.TryGetValue(player, out float when) && Time.time < when)) continue;
                d.nextHit[player] = Time.time + 1f;
                player.Launch(Mathf.Clamp(d.data.liftSpeed, 5, 9));
                continue;
            }
            if (d.data.kind == "piston") { if (!d.hit.Add(player)) continue; }
            else
            {
                if (d.nextHit.TryGetValue(player, out float at) && Time.time < at) continue;
                d.nextHit[player] = Time.time + 1.2f;
            }
            if (PlayerPowerUps.Blocked(player)) continue;
            Vector3 direction = d.data.kind == "piston" ? d.root.forward
                : Vector3.Cross(Vector3.up, player.transform.position - d.origin).normalized * Mathf.Sign(d.data.speed);
            if (d.data.kind == "pendulum") direction = d.root.right
                * Mathf.Sign(Mathf.Cos((SurvivalTimer.Instance.Elapsed + d.data.phase) * 2 * Mathf.PI / Mathf.Max(3, d.data.period)));
            if (direction.sqrMagnitude < 0.01f) direction = d.root.right;
            float contactSpeed = d.data.kind == "piston" ? 0
                : Mathf.Abs(d.data.speed) * Mathf.Deg2Rad * Vector3.Distance(player.transform.position, d.origin);
            player.Knockback(direction * Mathf.Clamp(d.data.hitSpeed + contactSpeed * 0.3f, 6, 14)
                + Vector3.up * d.data.liftSpeed, d.data.knockDownSeconds);
        }
    }
    void OnDestroy() { foreach (var material in materials) if (material != null) Destroy(material); }
}
