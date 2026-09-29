using System.Collections.Generic;
using UnityEngine;

// AI 봇의 머리. 몸은 플레이어와 같은 PlayerController(관성, 가변 점프, 다이브, 넘어짐)를 SetExternalInput으로 조종한다.
//  - 주변의 멀쩡한 칸을 목표로 고른다 (멀쩡할수록, 플레이어와 가까울수록 잘 고름 → 플레이어 주변 얼음을 깨서 압박)
//  - 멀쩡한 칸끼리는 걷고, 사이에 깨진 칸이 1개면 가장자리에서 점프(가끔 점프 꼭대기에서 다이브)로 넘는 길을 찾는다
//  - 도착하면 잠깐 서 있거나 제자리 점프 (봇이 밟아도 블럭이 깨진다)
//  - 흔들리는 칸 위에 있으면 바로 옆 칸으로 피한다
//  - 가끔 이유 없이 통통 뛰거나 앞으로 다이브해서 사람처럼 보이게 한다
[RequireComponent(typeof(PlayerController))]
public class BotBrain : MonoBehaviour
{
    public IceGrid grid;
    public BotManager manager;
    public Transform player;

    [Header("성격")]
    [Range(0.3f, 1f)] public float walkSpeed = 0.85f;
    [Min(1)] public int wanderRadius = 4;
    public Vector2 idleTime = new Vector2(0.3f, 1.4f);
    [Tooltip("도착해서 제자리 점프할 확률")]
    [Range(0f, 1f)] public float stompChance = 0.5f;
    [Tooltip("걷다가 이유 없이 통통 뛸 확률 (초당)")]
    [Min(0f)] public float hopPerSecond = 0.12f;
    [Tooltip("길게 곧은 길에서 앞으로 다이브할 확률 (초당)")]
    [Min(0f)] public float divePerSecond = 0.06f;
    [Tooltip("빈칸을 넘을 때 점프 꼭대기에서 다이브할 확률")]
    [Range(0f, 1f)] public float diveOverGapChance = 0.6f;
    [Tooltip("플레이어 가까운 칸을 얼마나 좋아할지 (0이면 무시)")]
    [Min(0f)] public float playerAttraction = 1.5f;

    // 빈칸 1개 너머 착지 칸 중심까지 이 거리 안에 들어오면 점프 (자기 칸 가장자리 근처)
    const float JumpTriggerDistance = 2.55f;
    const float ArriveDistance = 0.4f;

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
    bool stompQueued;
    bool jumping;
    bool wantDive;
    bool diveDone;
    Vector3 jumpDirection;
    Vector3 stuckCheckPosition;
    float stuckCheckTime;

    void Awake()
    {
        body = GetComponent<PlayerController>();
        rb = GetComponent<Rigidbody>();
    }

    public void Activate() => active = true;

    // 바다에 빠졌다가 다시 떨어져 들어올 때
    public void OnRespawn()
    {
        path = null;
        jumping = false;
        idleUntil = Time.time + 0.5f;
    }

    void Update()
    {
        if (!active || grid == null || grid.IceBlocks.Count == 0)
        {
            body.SetExternalInput(Vector3.zero, false, false, false);
            return;
        }
        if (transform.position.y < grid.SeaLevel)
        {
            manager.OnBotFell(this);
            return;
        }

        // 공중: 점프 중이면 그 방향으로 계속, 떨어지기 시작하면(꼭대기) 다이브
        if (!body.IsGrounded)
        {
            bool dive = false;
            if (jumping && wantDive && !diveDone && rb.linearVelocity.y < 0.8f)
            {
                dive = true;
                diveDone = true;
            }
            body.SetExternalInput(jumping ? jumpDirection : Vector3.zero, jumping, false, dive);
            return;
        }
        jumping = false;
        if (body.State != PlayerController.MoveState.Normal)
        {
            body.SetExternalInput(Vector3.zero, false, false, false);
            return;
        }

        Vector2Int here = grid.WorldToCell(transform.position);
        IceBlock under = grid.GetBlock(here);

        // 흔들리는 칸이면 서 있던 것도 그만두고 바로 옆으로 피한다
        if (under != null && under.CurrentStage == IceBlock.Stage.Shaking && (path == null || pathIndex >= path.Count || path[pathIndex].cell == here))
        {
            idleUntil = 0f;
            PlanTo(here, ChooseTarget(here, 1));
        }

        if (Time.time < idleUntil)
        {
            bool hop = stompQueued || Random.value < hopPerSecond * 2f * Time.deltaTime;
            stompQueued = false;
            body.SetExternalInput(Vector3.zero, false, hop, false);
            return;
        }

        if (path != null && pathIndex >= path.Count)
        {
            // 도착: 잠깐 쉬거나 제자리에서 쿵
            path = null;
            idleUntil = Time.time + Random.Range(idleTime.x, idleTime.y);
            stompQueued = Random.value < stompChance;
            body.SetExternalInput(Vector3.zero, false, false, false);
            return;
        }
        if (path == null || !PathStillValid())
        {
            if (!PlanTo(here, ChooseTarget(here, wanderRadius)))
            {
                idleUntil = Time.time + 0.4f;
                return;
            }
            stuckCheckPosition = transform.position;
            stuckCheckTime = Time.time;
        }

        Follow();
        CheckStuck(here);
    }

    void Follow()
    {
        Step next = path[pathIndex];
        Vector3 to = grid.CellTop(next.cell) - transform.position;
        to.y = 0f;
        float distance = to.magnitude;
        Vector3 direction = distance > 0.001f ? to / distance : Vector3.zero;

        bool jump = false, hold = false, dive = false;
        Vector3 move = direction * walkSpeed;

        if (next.jump)
        {
            // 빈칸 앞 가장자리에서 전속력으로 길게 점프
            move = direction;
            if (distance < JumpTriggerDistance)
            {
                jump = hold = true;
                jumping = true;
                jumpDirection = direction;
                wantDive = Random.value < diveOverGapChance;
                diveDone = false;
                pathIndex++;
            }
        }
        else if (distance < ArriveDistance)
        {
            pathIndex++;
        }
        else
        {
            // 사람처럼: 가끔 통통, 곧은 길이 길면 가끔 다이브
            if (Random.value < hopPerSecond * Time.deltaTime) jump = true;
            else if (RemainingStraightWalk() > 4f && Random.value < divePerSecond * Time.deltaTime) dive = true;
        }
        body.SetExternalInput(move, hold, jump, dive);
    }

    // 1.5초 동안 거의 못 움직였으면 길을 새로 찾고 한 번 뛰어 본다
    void CheckStuck(Vector2Int here)
    {
        if (Time.time - stuckCheckTime < 1.5f) return;
        if ((transform.position - stuckCheckPosition).sqrMagnitude < 0.04f)
        {
            path = null;
            body.SetExternalInput(Vector3.zero, false, true, false);
        }
        stuckCheckPosition = transform.position;
        stuckCheckTime = Time.time;
    }

    float RemainingStraightWalk()
    {
        float total = 0f;
        Vector3 from = transform.position;
        for (int i = pathIndex; i < path.Count && !path[i].jump; i++)
        {
            Vector3 p = grid.CellTop(path[i].cell);
            total += Vector2.Distance(new Vector2(from.x, from.z), new Vector2(p.x, p.z));
            from = p;
        }
        return total;
    }

    static bool Walkable(IceBlock block) => block != null && !block.IsBroken && block.CurrentStage != IceBlock.Stage.Shaking;

    bool PathStillValid()
    {
        for (int i = pathIndex; i < path.Count; i++)
            if (!Walkable(grid.GetBlock(path[i].cell))) return false;
        return true;
    }

    // 목표 칸: 멀쩡할수록, 플레이어와 가까울수록 잘 뽑힌다
    Vector2Int ChooseTarget(Vector2Int here, int radius)
    {
        Vector2Int playerCell = player != null ? grid.WorldToCell(player.position) : here;
        var candidates = new List<(Vector2Int cell, float weight)>();
        float total = 0f;
        for (int dx = -radius; dx <= radius; dx++)
        for (int dz = -radius; dz <= radius; dz++)
        {
            if (dx == 0 && dz == 0) continue;
            Vector2Int cell = here + new Vector2Int(dx, dz);
            IceBlock block = grid.GetBlock(cell);
            if (!Walkable(block)) continue;
            float weight = block.CurrentStage == IceBlock.Stage.Clean ? 3f : block.CurrentStage == IceBlock.Stage.Cracked ? 2f : 1f;
            float playerDistance = Vector2Int.Distance(cell, playerCell);
            weight *= 1f + playerAttraction / (1f + playerDistance);
            candidates.Add((cell, weight));
            total += weight;
        }
        if (candidates.Count == 0) return here;

        float pick = Random.value * total;
        foreach (var (cell, weight) in candidates)
        {
            pick -= weight;
            if (pick <= 0f) return cell;
        }
        return candidates[candidates.Count - 1].cell;
    }

    static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    // 너비 우선 탐색: 멀쩡한 이웃 칸은 걷기, 깨진 칸 1개 너머의 멀쩡한 칸은 점프
    bool PlanTo(Vector2Int start, Vector2Int goal)
    {
        path = null;
        pathIndex = 0;
        if (goal == start) return false;

        var previous = new Dictionary<Vector2Int, (Vector2Int from, bool jump)>();
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(start);
        previous[start] = (start, false);
        while (queue.Count > 0)
        {
            Vector2Int cell = queue.Dequeue();
            if (cell == goal) break;
            foreach (Vector2Int d in Directions)
            {
                Vector2Int near = cell + d;
                if (Walkable(grid.GetBlock(near)))
                {
                    if (previous.ContainsKey(near)) continue;
                    previous[near] = (cell, false);
                    queue.Enqueue(near);
                }
                else if (grid.InBounds(near))
                {
                    Vector2Int far = cell + d * 2;
                    if (!Walkable(grid.GetBlock(far)) || previous.ContainsKey(far)) continue;
                    previous[far] = (cell, true);
                    queue.Enqueue(far);
                }
            }
        }
        if (!previous.ContainsKey(goal)) return false;

        var steps = new List<Step>();
        for (Vector2Int cell = goal; cell != start; cell = previous[cell].from)
            steps.Add(new Step { cell = cell, jump = previous[cell].jump });
        steps.Reverse();
        path = steps;
        return true;
    }
}
