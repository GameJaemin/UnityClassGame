using System.Collections.Generic;
using UnityEngine;

// AI 봇의 머리. 몸은 플레이어와 같은 PlayerController(관성, 가변 점프, 다이브, 밀치기, 넘어짐)를 SetExternalInput으로 조종한다.
// 목표는 플레이어와 똑같이 맵 위에 마지막까지 남아 1등을 하는 것.
//  - 돌아다니기: 멀쩡하고 사방에 발 디딜 곳이 많은 칸(맵 끝·구멍 옆이 아닌 칸)을 목표로 고른다.
//    누가 서 있는 칸은 피하고, 공격적인 봇일수록 상대가 가까운 칸을 좋아한다
//  - 멀쩡한 칸끼리는 걷고, 사이에 깨진 칸이 1개면 가장자리에서 점프(가끔 점프 꼭대기에서 다이브)로 넘는 길을 찾는다
//  - 사냥: 가까운 상대가 구멍·맵 끝 옆에 서 있으면 반대편으로 돌아 들어가 구멍 쪽으로 밀친다
//    (구멍이 없으면 가끔 그냥 밀어 본다). 가는 앞이 깨진·흔들리는 칸이면 그만둔다
//  - 반사적으로: 바로 앞에 상대가 있으면 공격성에 따라 가끔 밀친다
//  - 밀려서 넘어지거나 몸으로 밀려 길에서 벗어나면 길을 새로 찾는다. 흔들리는 칸 위에 있으면 바로 옆 칸으로 피한다
//  - 위험 비용으로 우회로를 우선 찾고, 가장자리에서 감속한다. 점프는 착지 칸을 향해 제어한다
//  - 경기가 끝나면 1등 봇은 제자리에서 통통 뛰며 기뻐한다
// 바다에 빠짐·다시 들어오기는 GameManager가 처리한다 (빠지면 이 오브젝트가 꺼졌다가 다시 켜짐).
[RequireComponent(typeof(PlayerController))]
public class BotBrain : MonoBehaviour
{
    public enum BehaviorType
    {
        [InspectorName("신중형")] Cautious,
        [InspectorName("공격형")] Aggressive
    }

    public IceGrid grid;
    public PlayerController player;

    [Header("성격")]
    public BehaviorType behaviorType;
    public string BehaviorLabel => behaviorType == BehaviorType.Aggressive ? "공격형" : "신중형";
    bool Aggressive => behaviorType == BehaviorType.Aggressive;
    [Range(0.3f, 1f)] public float walkSpeed = 0.85f;
    [Min(1)] public int wanderRadius = 4;
    public Vector2 idleTime = new Vector2(0.3f, 1.4f);
    [Tooltip("빈칸을 넘을 때 점프 꼭대기에서 다이브할 확률")]
    [Range(0f, 1f)] public float diveOverGapChance = 0.6f;

    [Header("밀치기")]
    [Tooltip("상대를 노려 밀치러 가는 정도 (0이면 먼저 밀지 않음)")]
    [Range(0f, 1f)] public float aggression = 0.6f;
    [Tooltip("플레이어를 다른 봇보다 얼마나 더 노릴지 (1이면 똑같이)")]
    [Min(0f)] public float playerFocus = 1.2f;
    [Tooltip("이 거리 안의 상대만 노림")]
    [Min(1f)] public float huntRadius = 4.5f;
    [Tooltip("한 번 노리고 쫓아가는 최대 시간")]
    [Min(0.5f)] public float huntTime = 3f;
    [Tooltip("밀친 뒤(또는 포기한 뒤) 다시 노릴 때까지 쉬는 시간 (최소, 최대)")]
    public Vector2 huntRest = new Vector2(1.5f, 3.5f);

    // 빈칸 1개 너머 착지 칸 중심까지 이 거리 안에 들어오면 점프 (자기 칸 가장자리 근처)
    const float ArriveDistance = 0.18f;
    // 사냥할 때 상대 뒤(밀 방향 반대편)로 이만큼 떨어진 자리에 선다
    const float PushStandOff = 1.0f;
    // 이 거리 앞의 칸이 위험하면 그쪽으로 걷지 않는다
    const float LookAhead = 0.8f;
    // 상대 몸에서 이만큼 떨어진 곳이 위험하면 그쪽에 구멍이 있다고 본다
    const float HoleProbe = 1.1f;
    const float HuntCheckInterval = 0.25f;
    // 다음 걷기 칸 중심이 이보다 멀면 밀려서 길에서 벗어난 것 (이웃 칸 중심까지는 약 1.6m)
    const float ShovedOffPathDistance = 2.4f;

    struct Step
    {
        public Vector2Int cell;
        public bool jump;
    }

    PlayerController body;
    Rigidbody rb;
    bool active;
    List<Step> path;
    int pathIndex;
    float idleUntil;
    bool jumping;
    bool wantDive;
    bool diveDone;
    Vector3 jumpDirection;
    Vector2Int landingCell;
    bool leftGround;
    float jumpRequestedTime;
    readonly Dictionary<Vector2Int, float> visited = new Dictionary<Vector2Int, float>();
    Vector3 stuckCheckPosition;
    float stuckCheckTime;

    PlayerController huntTarget;
    float huntUntil;
    float huntReadyTime;
    float nextHuntCheck;

    // 같은 유형 안에서도 이동 속도·공격성이 조금씩 다르다. 물리 능력과 낙사 방지 규칙은 공통이다.
    public void ConfigureBehavior(BehaviorType type)
    {
        behaviorType = type;
        walkSpeed = Aggressive ? Random.Range(0.95f, 1f) : Random.Range(0.75f, 0.85f);
        aggression = Aggressive ? Random.Range(0.8f, 1f) : Random.Range(0.15f, 0.3f);
        idleTime = Aggressive ? new Vector2(0.03f, 0.12f) : new Vector2(0.25f, 0.5f);
        wanderRadius = Aggressive ? 5 : 3;
        huntRadius = Aggressive ? 5.5f : 3.2f;
        huntTime = Aggressive ? 4f : 2f;
        huntRest = Aggressive ? new Vector2(0.55f, 1.2f) : new Vector2(3f, 5f);
        diveOverGapChance = Aggressive ? 0.75f : 0.15f;
    }

    void Awake()
    {
        body = GetComponent<PlayerController>();
        rb = GetComponent<Rigidbody>();
    }

    // 처음 켜질 때와 바다에 빠졌다가 다시 떨어져 들어올 때
    void OnEnable()
    {
        path = null;
        jumping = false;
        leftGround = false;
        diveDone = false;
        visited.Clear();
        huntTarget = null;
        idleUntil = Time.time;
        huntReadyTime = Time.time + Random.Range(huntRest.x, huntRest.y);
    }

    public void Activate()
    {
        active = true;
        idleUntil = Time.time;
    }

    void Update()
    {
        if (grid == null || grid.IceBlocks.Count == 0)
        {
            StandStill();
            return;
        }
        GameManager manager = GameManager.Instance;
        if (manager != null && manager.State == GameManager.GameState.GameOver)
        {
            Celebrate(manager.Winner == body);
            return;
        }
        if (!active)
        {
            StandStill();
            return;
        }

        if (body.State != PlayerController.MoveState.Normal || body.IsShoved)
        {
            path = null;
            jumping = false;
            huntTarget = null;
            StandStill();
            return;
        }

        // 공중에서는 예정된 착지 칸을 향하고, 중심에 가까워지면 미리 감속한다.
        if (!body.IsGrounded)
        {
            if (jumping) leftGround = true;
            bool dive = false;
            Vector3 toLanding = Flat(grid.CellTop(landingCell) - transform.position);
            if (jumping && wantDive && !diveDone && rb.linearVelocity.y < 0.8f
                && toLanding.magnitude > 1.1f && Walkable(grid.GetBlock(landingCell))
                && SafeSides(landingCell) >= 2)
            {
                dive = true;
                diveDone = true;
            }
            Vector3 airMove = jumping ? ArrivalMove(toLanding, true) : Vector3.zero;
            body.SetExternalInput(airMove, jumping, false, dive);
            return;
        }
        // 점프 입력과 물리 갱신 사이에는 아직 땅 판정이 남을 수 있다.
        if (jumping && !leftGround && Time.time - jumpRequestedTime < 0.15f)
        {
            body.SetExternalInput(jumpDirection, true, false, false);
            return;
        }
        if (jumping)
        {
            jumping = false;
            leftGround = false;
            path = null;
            idleUntil = 0f;
        }

        Vector2Int here = grid.WorldToCell(transform.position);
        IceBlock under = grid.GetBlock(here);
        // 칸 이음새나 밀린 가장자리에서는 실제로 발이 닿은 얼음에서 경로를 시작한다.
        if (under == null && body.GroundCollider != null)
        {
            under = body.GroundCollider.GetComponent<IceBlock>();
            if (under != null) here = grid.WorldToCell(under.transform.position);
        }

        // 흔들리는 칸이면 하던 것을 그만두고 바로 옆으로 피한다
        bool danger = under == null || under.CurrentStage >= IceBlock.Stage.HeavyCracked || under.SecondsUntilShaking < 1f;
        if (danger)
        {
            huntTarget = null;
            idleUntil = 0f;
            if (path == null || pathIndex >= path.Count || !PathStillValid()) PlanSafeRoute(here, 2);
        }

        if (!danger && huntTarget == null && Time.time >= nextHuntCheck)
        {
            nextHuntCheck = Time.time + HuntCheckInterval;
            ConsiderHunt();
        }
        if (huntTarget != null)
        {
            Hunt();
            return;
        }

        bool reflexPush = !danger && ReflexPush();

        if (Time.time < idleUntil)
        {
            body.SetExternalInput(Vector3.zero, false, false, false, reflexPush);
            return;
        }

        if (path != null && pathIndex >= path.Count)
        {
            // 안전한 칸에 도착하면 짧게 쉬고 다음 칸을 고른다.
            path = null;
            visited[here] = Time.time;
            idleUntil = danger ? 0f : Time.time + Mathf.Min(0.6f, Random.Range(idleTime.x, idleTime.y));
            body.SetExternalInput(Vector3.zero, false, false, false, reflexPush);
            return;
        }
        if (path == null || !PathStillValid())
        {
            if (!PlanSafeRoute(here, danger ? 2 : wanderRadius))
            {
                StandStill();
                idleUntil = Time.time + 0.1f;
                return;
            }
            stuckCheckPosition = transform.position;
            stuckCheckTime = Time.time;
        }

        Follow(reflexPush);
        CheckStuck();
    }

    void StandStill() => body.SetExternalInput(Vector3.zero, false, false, false);

    // 경기 끝: 1등이면 제자리에서 통통, 아니면 가만히
    void Celebrate(bool winner)
    {
        bool hop = winner && body.IsGrounded && Random.value < 2f * Time.deltaTime;
        body.SetExternalInput(Vector3.zero, hop, hop, false);
    }

    void Follow(bool push)
    {
        Step next = path[pathIndex];
        Vector3 to = grid.CellTop(next.cell) - transform.position;
        to.y = 0f;
        float distance = to.magnitude;
        Vector3 direction = distance > 0.001f ? to / distance : Vector3.zero;

        // 몸으로 밀려 길에서 멀어졌으면 곧장 걸어가다 구멍을 지날 수 있으니 길을 새로 찾는다
        if (!next.jump && distance > ShovedOffPathDistance)
        {
            path = null;
            StandStill();
            return;
        }

        bool jump = false, hold = false, dive = false;
        Vector3 move = ArrivalMove(to, false);

        if (next.jump)
        {
            // 빈칸 앞 가장자리에서 전속력으로 길게 점프
            move = direction;
            push = false;
            float trigger = grid.Spacing + grid.blockSize * 0.5f + 0.12f;
            if (distance < trigger)
            {
                // 반드시 이륙 칸 중앙선에서 빈칸 하나만 넘는다.
                Vector2Int takeoff = grid.WorldToCell(transform.position);
                Vector2Int delta = next.cell - takeoff;
                Vector3 axis = Flat(grid.CellTop(next.cell) - grid.CellTop(takeoff)).normalized;
                Vector3 lateral = Flat(transform.position - grid.CellTop(takeoff));
                lateral -= axis * Vector3.Dot(lateral, axis);
                if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) != 2 || (delta.x != 0 && delta.y != 0)
                    || lateral.magnitude > 0.2f)
                {
                    path = null;
                    StandStill();
                    return;
                }
                jump = hold = true;
                jumping = true;
                leftGround = false;
                jumpRequestedTime = Time.time;
                landingCell = next.cell;
                jumpDirection = axis;
                wantDive = Random.value < diveOverGapChance && SafeSides(next.cell) >= 2;
                diveDone = false;
                pathIndex++;
            }
        }
        else if (distance < ArriveDistance)
        {
            pathIndex++;
            move = Vector3.zero;
        }
        else
        {
            // 관성이나 대각선 이동 때문에 구멍을 가로지르기 전에 멈춘다.
            if (!SafeSegment(transform.position, grid.CellTop(next.cell)) || !SafeAhead(direction))
            {
                path = null;
                StandStill();
                return;
            }
        }
        body.SetExternalInput(move, hold, jump, dive, push);
    }

    Vector3 ArrivalMove(Vector3 to, bool airborne)
    {
        float distance = to.magnitude;
        if (distance < 0.06f) return Vector3.zero;
        float speed = Mathf.Max(0.1f, body.moveSpeed);
        float deceleration = Mathf.Max(0.1f, body.groundDeceleration * (airborne ? body.airControl : 1f));
        float targetSpeed = Mathf.Min(speed * (airborne ? 1f : walkSpeed), Mathf.Sqrt(2f * deceleration * distance) * (Aggressive ? 0.9f : 0.7f), distance * (airborne ? 7f : Aggressive ? 7.5f : 6f));
        Vector3 desired = to / distance * targetSpeed;
        // 도착할 때 옆 방향 관성도 줄이고, 칸 중앙을 지난 뒤 방향을 바꾼다.
        Vector3 correction = desired - Flat(rb.linearVelocity) * (airborne || Aggressive ? 0.2f : 0.35f);
        return Vector3.ClampMagnitude(correction / speed, 1f);
    }

    // 1.5초 동안 거의 못 움직였으면 멈추고 길을 새로 찾는다.
    void CheckStuck()
    {
        if (Time.time - stuckCheckTime < 1.5f) return;
        if ((transform.position - stuckCheckPosition).sqrMagnitude < 0.04f)
        {
            path = null;
            StandStill();
        }
        stuckCheckPosition = transform.position;
        stuckCheckTime = Time.time;
    }

    // ---- 밀치기 ----

    // 나를 뺀, 지금 맵 위에 있는 캐릭터들
    IEnumerable<PlayerController> Opponents()
    {
        foreach (PlayerController other in PlayerController.All)
            if (other != body && other.isActiveAndEnabled) yield return other;
    }

    static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }

    // 가까운 상대 중 밀어 떨어뜨리기 좋은 상대를 골라 노리기 시작한다
    void ConsiderHunt()
    {
        if (aggression <= 0f || Time.time < huntReadyTime) return;

        PlayerController best = null;
        float bestScore = 0f;
        foreach (PlayerController other in Opponents())
        {
            // 공중에 뜬 상대는 어디 떨어질지 몰라서 노리지 않는다
            if (!other.IsGrounded || Mathf.Abs(other.transform.position.y - transform.position.y) > 1f) continue;
            Vector3 to = Flat(other.transform.position - transform.position);
            float distance = to.magnitude;
            if (distance > huntRadius || distance < 0.01f) continue;

            PushDirection(other, to / distance, out bool hole);
            float score = (hole ? 1f : 0.15f) * (1f - distance / (huntRadius + 0.01f));
            if (!other.useExternalInput) score *= playerFocus;
            if (score > bestScore)
            {
                bestScore = score;
                best = other;
            }
        }
        if (best == null) return;

        // 공격적일수록, 떨어뜨리기 좋은 상대일수록 잘 덤빈다
        if (Random.value > aggression * bestScore * 2.5f) return;
        huntTarget = best;
        huntUntil = Time.time + huntTime;
        path = null;
    }

    // 상대를 어느 쪽으로 밀면 좋을지: 상대 바로 옆에 구멍·맵 끝이 있는 방향 (다가가는 방향에 가까운 쪽 우선)
    Vector3 PushDirection(PlayerController target, Vector3 approach, out bool hole)
    {
        Vector3 center = target.transform.position;
        Vector3 best = approach;
        float bestDot = float.NegativeInfinity;
        hole = false;
        foreach (Vector3 d in new[] { approach, Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
        {
            if (Walkable(grid.GetBlock(grid.WorldToCell(center + d * HoleProbe)))) continue;
            float dot = Vector3.Dot(d, approach);
            if (dot > bestDot)
            {
                bestDot = dot;
                best = d;
                hole = true;
            }
        }
        return best;
    }

    // 상대 뒤(밀 방향 반대편)로 돌아 들어가서 밀 방향을 보고 있으면 밀친다
    void Hunt()
    {
        if (huntTarget == null || !huntTarget.isActiveAndEnabled || Time.time > huntUntil)
        {
            EndHunt();
            return;
        }
        Vector3 me = transform.position;
        Vector3 them = huntTarget.transform.position;
        Vector3 to = Flat(them - me);
        float distance = to.magnitude;
        if (distance > huntRadius + 1.5f || Mathf.Abs(them.y - me.y) > 1.5f)
        {
            EndHunt();
            return;
        }
        Vector3 toward = distance > 0.01f ? to / distance : transform.forward;
        Vector3 pushDirection = PushDirection(huntTarget, toward, out _);

        // 밀 방향 쪽에 서 있으면(상대가 나와 구멍 사이) 곧장 다가가고, 아니면 반대편 자리로 돌아간다
        bool lined = Vector3.Dot(toward, pushDirection) > 0.5f;
        Vector3 goal = lined ? them : them - pushDirection * PushStandOff;
        Vector3 move = Flat(goal - me);
        Vector3 direction = move.sqrMagnitude > 0.0025f ? move.normalized : toward;

        if (!SafeAhead(direction) || !SafeSegment(me, goal))
        {
            EndHunt();
            StandStill();
            return;
        }

        bool push = lined && distance < body.pushReach * 0.7f && body.PushReady;
        body.SetExternalInput(push ? toward * 0.2f : ArrivalMove(move, false), false, false, false, push);
        if (push) EndHunt();
    }

    void EndHunt()
    {
        huntTarget = null;
        huntReadyTime = Time.time + Random.Range(huntRest.x, huntRest.y);
        path = null;
        idleUntil = Time.time + 0.2f;
    }

    // 걸어가는 바로 앞이 멀쩡한 칸인지
    bool SafeAhead(Vector3 direction)
    {
        float speed = Flat(rb.linearVelocity).magnitude;
        float stopping = speed * speed / (2f * Mathf.Max(0.1f, body.groundDeceleration));
        return SafeSegment(transform.position, transform.position + direction * Mathf.Max(LookAhead, stopping + 0.2f));
    }

    bool SafeSegment(Vector3 from, Vector3 to)
    {
        float length = Flat(to - from).magnitude;
        int samples = Mathf.Max(1, Mathf.CeilToInt(length / (grid.Spacing * 0.2f)));
        Vector2Int previous = grid.WorldToCell(from);
        Vector2Int origin = previous;
        IceBlock originBlock = grid.GetBlock(origin);
        bool canLeave = originBlock != null && !originBlock.IsBroken;
        bool leftOrigin = false;
        for (int i = 1; i <= samples; i++)
        {
            Vector2Int cell = grid.WorldToCell(Vector3.Lerp(from, to, (float)i / samples));
            if (cell != origin) leftOrigin = true;
            // 흔들리는 현재 칸에서 나가는 것은 허용하되, 위험한 칸으로 들어가는 것은 막는다.
            if (!Walkable(grid.GetBlock(cell)) && !(canLeave && !leftOrigin && cell == origin)) return false;
            if (cell.x != previous.x && cell.y != previous.y
                && (!Walkable(grid.GetBlock(new Vector2Int(cell.x, previous.y)))
                    || !Walkable(grid.GetBlock(new Vector2Int(previous.x, cell.y))))) return false;
            previous = cell;
        }
        return true;
    }

    // 바로 앞에 상대가 있으면 공격성에 따라 가끔 밀친다
    bool ReflexPush()
    {
        if (aggression <= 0f || !body.PushReady) return false;
        if (Random.value > aggression * 3f * Time.deltaTime) return false;
        Vector3 facing = Flat(transform.forward).normalized;
        if (!SafeAhead(facing)) return false;
        foreach (PlayerController other in Opponents())
        {
            Vector3 to = other.transform.position - transform.position;
            if (Mathf.Abs(to.y) > 1f) continue;
            to = Flat(to);
            if (to.magnitude < body.pushReach * 0.7f && Vector3.Angle(facing, to) < body.pushArc * 0.6f) return true;
        }
        return false;
    }

    // ---- 길 찾기 ----

    static bool Walkable(IceBlock block) => block != null && !block.IsBroken && block.CurrentStage != IceBlock.Stage.Shaking;

    bool PathStillValid()
    {
        float arrival = 0f;
        Vector3 from = transform.position;
        for (int i = pathIndex; i < path.Count; i++)
        {
            IceBlock block = grid.GetBlock(path[i].cell);
            Vector3 destination = grid.CellTop(path[i].cell);
            arrival += Flat(destination - from).magnitude / Mathf.Max(0.5f, body.moveSpeed * walkSpeed) + 0.1f;
            if (!Walkable(block) || block.SecondsUntilShaking <= arrival + (Aggressive ? 0.15f : 0.5f)) return false;
            from = destination;
        }
        return true;
    }

    static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    // 목표 칸: 멀쩡할수록, 사방에 발 디딜 곳이 많을수록(가장자리·구멍 옆이 아닐수록) 잘 뽑힌다.
    // 누가 서 있는 칸은 피하고, 공격적인 봇은 상대 가까운 칸을 좋아한다
    int SafeSides(Vector2Int cell)
    {
        int count = 0;
        foreach (Vector2Int d in Directions) if (Walkable(grid.GetBlock(cell + d))) count++;
        return count;
    }

    // 도달 가능한 칸만 고른다. 점프·금 간 칸·막힌 모서리에 비용을 주어 안전한 우회로를 우선한다.
    bool PlanSafeRoute(Vector2Int start, int radius)
    {
        path = null;
        pathIndex = 0;
        if (!grid.InBounds(start)) return false;
        var occupied = new HashSet<Vector2Int>();
        foreach (PlayerController other in Opponents())
            if (other.IsGrounded) occupied.Add(grid.WorldToCell(other.transform.position));
        var costs = new float[grid.width * grid.depth];
        for (int z = 0; z < grid.depth; z++)
        for (int x = 0; x < grid.width; x++)
        {
            Vector2Int cell = new Vector2Int(x, z);
            IceBlock block = grid.GetBlock(cell);
            costs[z * grid.width + x] = Walkable(block) && block.SecondsUntilShaking > 0.8f
                ? 1f + (int)block.CurrentStage * (Aggressive ? 0.9f : 2.5f)
                    + (4 - SafeSides(cell)) * (Aggressive ? 0.12f : 0.5f)
                    + (occupied.Contains(cell) ? Aggressive ? 0.6f : 4f : 0f)
                : -1f;
        }
        int origin = start.y * grid.width + start.x;
        costs[origin] = 1f;
        // 긴 빈칸을 넘을 수 없는 설정에서는 점프 경로 자체를 만들지 않는다.
        float flight = body.jumpTimeToApex * (1f + 1f / Mathf.Sqrt(body.fallGravityScale));
        bool allowJump = body.moveSpeed * flight + 0.25f >= grid.Spacing;
        Dictionary<int, BotRouteSearch.Node> routes = BotRouteSearch.Build(costs, grid.width, grid.depth, origin, allowJump, Aggressive ? 1.8f : 6f);
        int goal = -1;
        float bestScore = float.NegativeInfinity;
        foreach (var pair in routes)
        {
            if (pair.Key == origin) continue;
            Vector2Int cell = new Vector2Int(pair.Key % grid.width, pair.Key / grid.width);
            if (Mathf.Max(Mathf.Abs(cell.x - start.x), Mathf.Abs(cell.y - start.y)) > radius) continue;
            IceBlock block = grid.GetBlock(cell);
            float score = 10f - (int)block.CurrentStage * (Aggressive ? 1.2f : 3f)
                + SafeSides(cell) * (Aggressive ? 0.6f : 2f) - pair.Value.cost * 0.7f;
            if (visited.TryGetValue(cell, out float when) && Time.time - when < 5f) score -= 4f;
            if (occupied.Contains(cell)) score -= Aggressive ? 0.75f : 5f;
            float nearest = float.PositiveInfinity;
            foreach (Vector2Int opponent in occupied) nearest = Mathf.Min(nearest, Vector2Int.Distance(cell, opponent));
            if (!float.IsPositiveInfinity(nearest)) score += (Aggressive ? 6f : -3f) / (1f + nearest);
            score += Random.Range(0f, 1f);
            if (score > bestScore) { bestScore = score; goal = pair.Key; }
        }
        if (goal < 0) return false;

        var steps = new List<Step>();
        for (int cell = goal; cell != origin; cell = routes[cell].from)
            steps.Add(new Step { cell = new Vector2Int(cell % grid.width, cell / grid.width), jump = routes[cell].jump });
        steps.Reverse();
        IceBlock startBlock = grid.GetBlock(start);
        if (startBlock != null && startBlock.CurrentStage < IceBlock.Stage.HeavyCracked
            && Flat(transform.position - grid.CellTop(start)).magnitude > 0.25f)
            steps.Insert(0, new Step { cell = start, jump = false });
        path = steps;
        if (!PathStillValid()) { path = null; return false; }
        return true;
    }
}

// 물리와 분리한 경로 탐색. 비용이 음수인 칸은 걸을 수 없고 빈칸 하나만 점프로 넘는다.
internal static class BotRouteSearch
{
    internal struct Node
    {
        public int from;
        public bool jump;
        public float cost;
    }

    internal static Dictionary<int, Node> Build(float[] costs, int width, int depth, int start, bool allowJump, float jumpCost = 6f)
    {
        var result = new Dictionary<int, Node>();
        if (width <= 0 || depth <= 0 || costs == null || costs.Length != width * depth || start < 0 || start >= costs.Length) return result;
        var open = new List<int> { start };
        result[start] = new Node { from = start };
        int[] dx = { 0, 0, -1, 1 }, dz = { -1, 1, 0, 0 };
        while (open.Count > 0)
        {
            int best = 0;
            for (int i = 1; i < open.Count; i++) if (result[open[i]].cost < result[open[best]].cost) best = i;
            int current = open[best];
            open.RemoveAt(best);
            for (int d = 0; d < 4; d++)
            {
                int x = current % width + dx[d], z = current / width + dz[d];
                if (x < 0 || x >= width || z < 0 || z >= depth) continue;
                int next = z * width + x;
                bool jump = costs[next] < 0f;
                if (jump)
                {
                    if (!allowJump) continue;
                    x += dx[d]; z += dz[d];
                    if (x < 0 || x >= width || z < 0 || z >= depth) continue;
                    next = z * width + x;
                    if (costs[next] < 0f) continue;
                }
                float cost = result[current].cost + costs[next] + (jump ? System.Math.Max(0f, jumpCost) : 0f);
                Node previous;
                if (result.TryGetValue(next, out previous) && previous.cost <= cost) continue;
                result[next] = new Node { from = current, jump = jump, cost = cost };
                if (!open.Contains(next)) open.Add(next);
            }
        }
        return result;
    }
}
