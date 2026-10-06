using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// 폴가이즈 느낌의 Rigidbody 이동: 관성, 약한 공중 제어, 점프 보정, 다이브, 밀치기, 넘어짐.
// 점프는 슈퍼마리오처럼 길게 누르면 높게, 짧게 누르면 낮게 뛴다.
// 이동/점프 입력은 프로젝트 기본 액션(InputSystem_Actions)의 Player/Move, Player/Jump를 쓰고,
// 다이브(Ctrl / 게임패드 X)와 밀치기(마우스 왼쪽 / 게임패드 RT)는 이 스크립트에서 만든 액션을 쓴다.
// 밀치기: 앞쪽 부채꼴 안의 상대를 넘어뜨리지 않고 서 있는 채로 약 1.5칸(pushDistance) 밀어낸다.
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class PlayerController : MonoBehaviour
{
    public enum MoveState { Normal, Diving, KnockedDown }

    [Header("시작 위치")]
    public IceGrid grid;
    [Min(0f)] public float spawnHeight = 1.05f;
    [Tooltip("끄면 시작 칸으로 옮기지 않음 (봇은 스스로 자리를 잡는다)")]
    public bool spawnAtStartCell = true;

    [Header("입력")]
    [Tooltip("켜면 키보드 대신 SetExternalInput으로 조종 (AI 봇)")]
    public bool useExternalInput;
    [System.NonSerialized] public bool networkReplica;
    Vector3 replicaVelocity;
    public Vector3 Velocity => networkReplica ? replicaVelocity : body.linearVelocity;

    [Header("이동")]
    [Min(0f)] public float moveSpeed = 5f;
    [Min(0f)] public float groundAcceleration = 25f;
    [Min(0f)] public float groundDeceleration = 35f;
    [Range(0f, 1f)] public float airControl = 0.5f;
    [Min(0f)] public float turnSpeed = 540f;

    [Header("점프 (길게 누를 때 기준)")]
    [Min(0.1f)] public float jumpHeight = 1f;
    [Min(0.05f)] public float jumpTimeToApex = 0.25f;
    [Tooltip("점프 버튼을 떼면 올라가는 중력이 이만큼 세져서 낮게 뛴다")]
    [Min(1f)] public float jumpCutGravityScale = 3f;
    [Tooltip("떨어질 때 중력 배율 (올라갈 때 중력 기준)")]
    [Min(1f)] public float fallGravityScale = 1.5f;
    [Tooltip("최고 낙하 속도. 점프 후 떨어질 때(최대 약 9.8)는 걸리지 않고, 맵 밖으로 떨어질 때 Fall 애니메이션이 보이게 천천히 떨어진다")]
    [Min(1f)] public float maxFallSpeed = 12f;
    [Min(0f)] public float coyoteTime = 0.08f;
    [Min(0f)] public float jumpBufferTime = 0.12f;
    [Min(0f)] public float groundCheckDistance = 0.15f;

    [Header("다이브")]
    [Min(0f)] public float diveForwardSpeed = 6f;
    [Tooltip("땅에서 다이브할 때 떠오르는 속도")]
    [Min(0f)] public float groundDiveUpSpeed = 5f;
    [Tooltip("공중 다이브 시 위로 향하는 속도 최대값 (점프 직후 다이브하면 짧게 날아감)")]
    [Min(0f)] public float airDiveMaxUpSpeed = 2f;
    [Tooltip("다이브 중 중력 배율 (올라갈 때 중력 기준)")]
    [Min(0.1f)] public float diveGravityScale = 1f;
    [Min(0f)] public float diveRecoverTime = 0.4f;

    [Header("밀치기")]
    [Tooltip("밀 수 있는 거리 (두 캐릭터 몸 중심 사이 수평 거리, 몸이 맞닿으면 약 1m)")]
    [Min(0.1f)] public float pushReach = 2f;
    [Tooltip("바라보는(누르는) 방향에서 좌우로 이 각도 안에 있는 상대가 밀림. 그중 가장 잘 맞는 상대 쪽으로 몸을 돌려 민다")]
    [Range(10f, 180f)] public float pushArc = 80f;
    [Tooltip("누른 뒤 이 시간 동안 판정이 남아 있어서, 살짝 늦게 다가온 상대도 맞는다")]
    [Min(0f)] public float pushActiveTime = 0.2f;
    [Tooltip("밀린 쪽이 서 있는 채로 밀려나는 거리 (블럭 1칸 = 1.6m)")]
    [Min(0f)] public float pushDistance = 2.4f;
    [Tooltip("그 거리를 밀려나는 데 걸리는 시간. 이 동안 밀린 쪽은 조작할 수 없다")]
    [Min(0.05f)] public float pushDuration = 0.38f;
    [Tooltip("다시 밀 수 있을 때까지 걸리는 시간")]
    [Min(0f)] public float pushCooldown = 0.9f;
    [Tooltip("밀 때 앞으로 살짝 내딛는 속도")]
    [Min(0f)] public float pushLunge = 2.2f;
    [Tooltip("걸어서 몸으로 부딪힌 상대를 내 속도의 이만큼으로 밀어냄 (0이면 끔). 마주 밀면 더 센 쪽만 민다")]
    [Range(0f, 1.5f)] public float bodyShove = 0.8f;

    // 다이브 직후 아직 발이 땅 근처일 때 곧바로 착지로 판정하지 않기 위한 시간
    const float MinDiveAirTime = 0.15f;
    // 밀치기: 높이 차이가 이보다 크면 (한쪽이 높이 떠 있으면) 닿지 않음
    const float PushHeightTolerance = 1.4f;

    // 지금 켜져 있는 캐릭터 전부 (플레이어 + 봇). 밀치기 대상 찾기와 봇 판단에 쓴다
    public static readonly List<PlayerController> All = new List<PlayerController>();

    Rigidbody body;
    CapsuleCollider capsule;
    Transform cameraTransform;
    InputAction moveAction;
    InputAction jumpAction;
    InputAction diveAction;
    InputAction pushAction;

    Vector3 moveDirection;
    bool jumpHeld;
    Vector3 externalMove;
    bool externalJumpHeld;
    bool externalJumpPressed;
    bool externalDivePressed;
    bool externalPushPressed;
    Vector3 pendingPush;
    float boostJump = 1f;
    float boostSpeed = 1f;
    float boostUntil = float.NegativeInfinity;

    bool Boosted => Time.time < boostUntil;
    bool isJumping;
    float lastJumpPressedTime = float.NegativeInfinity;
    float lastGroundedTime = float.NegativeInfinity;
    bool diveQueued;
    bool pushQueued;
    float nextPushTime;
    float pushActiveUntil;
    Vector3 pushFacing;
    readonly List<PlayerController> pushHits = new List<PlayerController>();
    // 밀치기에 맞아 밀려나는 중: 처음 속도에서 0까지 일정하게 줄어들며 정해 둔 거리를 간다
    Vector3 shoveVelocity;
    float shoveStartTime;
    float shoveEndTime;
    float stateStartTime;
    float knockDownEndTime;

    public MoveState State { get; private set; }
    public bool IsGrounded { get; private set; }
    // 발밑 충돌체 (땅에 있을 때만). 얼음 블럭 판정에 쓴다
    public Collider GroundCollider { get; private set; }
    // 게임 시작 후 한 번이라도 이동·점프·다이브 입력을 했는지 (시작 블럭 보호용)
    public bool HasMoved { get; private set; }
    // false면 입력을 무시한다 (게임 오버 등). 물리(중력)는 그대로
    public bool InputEnabled { get; set; } = true;
    public float HorizontalSpeed => new Vector3(Velocity.x, 0f, Velocity.z).magnitude;
    // 마지막으로 밀치기 동작을 한 시간 (맞혔든 아니든). 팔 뻗는 자세에 쓴다
    public float LastPushTime { get; private set; } = float.NegativeInfinity;
    // 지금 밀치기를 할 수 있는지 (재사용 대기 중이거나 넘어져 있으면 false)
    public bool PushReady => State == MoveState.Normal && !IsShoved && Time.time >= nextPushTime;
    // 밀치기에 맞아 밀려나는 중인지 (넘어지지는 않고 서 있는 채로, 조작 불가)
    public bool IsShoved => Time.time < shoveEndTime;

    // 점프 높이와 꼭대기까지 걸리는 시간으로 올라갈 때 중력과 점프 속도를 정한다
    float RiseGravity => 2f * jumpHeight / (jumpTimeToApex * jumpTimeToApex);
    // 슈퍼 점프 중에는 같은 중력으로 더 높이 뛴다 (높이 배율 → 속도는 제곱근 배)
    float JumpSpeed => 2f * jumpHeight / jumpTimeToApex * (Boosted ? Mathf.Sqrt(boostJump) : 1f);
    float MoveSpeed => moveSpeed * (Boosted ? boostSpeed : 1f);

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;

        moveAction = InputSystem.actions.FindAction("Player/Move", true);
        jumpAction = InputSystem.actions.FindAction("Player/Jump", true);

        diveAction = new InputAction("Dive", InputActionType.Button);
        diveAction.AddBinding("<Keyboard>/leftCtrl");
        diveAction.AddBinding("<Keyboard>/rightCtrl");
        diveAction.AddBinding("<Gamepad>/buttonWest");

        pushAction = new InputAction("Push", InputActionType.Button);
        pushAction.AddBinding("<Mouse>/leftButton");
        pushAction.AddBinding("<Gamepad>/rightTrigger");
    }

    void OnEnable()
    {
        diveAction.Enable();
        pushAction.Enable();
        if (!All.Contains(this)) All.Add(this);
    }

    void OnDisable()
    {
        diveAction.Disable();
        pushAction.Disable();
        All.Remove(this);
    }

    void OnDestroy()
    {
        diveAction.Dispose();
        pushAction.Dispose();
    }

    void Start()
    {
        if (Camera.main != null) cameraTransform = Camera.main.transform;
        if (grid != null && spawnAtStartCell)
        {
            body.position = grid.CellTop(grid.StartCell) + Vector3.up * spawnHeight;
            transform.position = body.position;
        }
    }

    void Update()
    {
        if (networkReplica) return;
        if (!InputEnabled)
        {
            moveDirection = Vector3.zero;
            jumpHeld = false;
            pushQueued = false;
            return;
        }

        if (useExternalInput)
        {
            moveDirection = Vector3.ClampMagnitude(Vector3.ProjectOnPlane(externalMove, Vector3.up), 1f);
            jumpHeld = externalJumpHeld;
            if (externalJumpPressed) lastJumpPressedTime = Time.time;
            if (externalDivePressed) diveQueued = true;
            if (externalPushPressed) pushQueued = true;
            externalJumpPressed = externalDivePressed = externalPushPressed = false;
            if (moveDirection.sqrMagnitude > 0.01f || jumpHeld || diveQueued || pushQueued) HasMoved = true;
            return;
        }

        // 카메라가 보는 방향 기준으로 W가 화면 위쪽이 되게 한다
        Vector2 input = moveAction.ReadValue<Vector2>();
        Vector3 forward = Vector3.forward;
        Vector3 right = Vector3.right;
        if (cameraTransform != null)
        {
            forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
        }
        moveDirection = Vector3.ClampMagnitude(forward * input.y + right * input.x, 1f);

        jumpHeld = jumpAction.IsPressed();
        if (jumpAction.WasPressedThisFrame()) lastJumpPressedTime = Time.time;
        if (diveAction.WasPressedThisFrame()) diveQueued = true;
        if (pushAction.WasPressedThisFrame()) pushQueued = true;
        if (moveDirection.sqrMagnitude > 0.01f || jumpHeld || diveQueued || pushQueued) HasMoved = true;
    }

    void FixedUpdate()
    {
        if (networkReplica || body.isKinematic) return;
        Vector3 velocity = body.linearVelocity;

        // 위로 올라가는 중에는 땅으로 보지 않는다 (점프 직후 다시 착지 판정되는 것 방지)
        IsGrounded = velocity.y <= 0.5f && CheckGround();
        if (!IsGrounded) GroundCollider = null;
        if (IsGrounded)
        {
            lastGroundedTime = Time.time;
            isJumping = false;
        }

        if (IsShoved)
        {
            // 밀려나는 중에는 입력을 무시하고 정해 둔 속도 곡선대로 미끄러진다 (구멍 위로 가면 그대로 떨어짐)
            float left = (shoveEndTime - Time.time) / Mathf.Max(0.0001f, shoveEndTime - shoveStartTime);
            Vector3 horizontal = shoveVelocity * left;
            velocity = new Vector3(horizontal.x, velocity.y, horizontal.z);
        }
        else switch (State)
        {
            case MoveState.Normal:
                ApplyMovement(ref velocity, IsGrounded ? 1f : airControl);
                if (Time.time - lastGroundedTime <= coyoteTime && Time.time - lastJumpPressedTime <= jumpBufferTime)
                    Jump(ref velocity);
                else if (diveQueued)
                    StartDive(ref velocity);
                else if (pushQueued)
                    TryPush(ref velocity);
                if (State == MoveState.Normal) RotateTowardsMovement();
                break;

            case MoveState.Diving:
                if (IsGrounded && Time.time - stateStartTime >= MinDiveAirTime)
                    KnockDown(diveRecoverTime);
                break;

            case MoveState.KnockedDown:
                if (IsGrounded) ApplyMovement(ref velocity, 1f, Vector3.zero);
                if (Time.time >= knockDownEndTime) SetState(MoveState.Normal);
                break;
        }
        diveQueued = false;
        pushQueued = false;
        // 밀치기 판정이 남아 있으면 계속 확인 (넘어지거나 밀려나면 끝)
        if (Time.time < pushActiveUntil)
        {
            if (State == MoveState.Normal && !IsShoved) PushHits();
            else pushActiveUntil = 0f;
        }

        ApplyGravity(ref velocity);
        velocity += pendingPush * Time.fixedDeltaTime;
        pendingPush = Vector3.zero;
        body.linearVelocity = velocity;
    }

    // AI 봇용 입력. move는 월드 방향(크기 0~1), 누름(pressed)은 다음 Update에서 한 번 처리된다
    public void SetExternalInput(Vector3 move, bool holdJump, bool pressJump, bool pressDive, bool pressPush = false)
    {
        externalMove = move;
        externalJumpHeld = holdJump;
        externalJumpPressed |= pressJump;
        externalDivePressed |= pressDive;
        externalPushPressed |= pressPush;
    }

    // 시련: 세게 밀려나며 넘어짐 (펭귄 충돌 등)
    public void Knockback(Vector3 velocity, float downTime)
    {
        body.linearVelocity = velocity;
        IsGrounded = false;
        KnockDown(downTime);
    }

    // 다른 캐릭터에게 밀림: 넘어지지 않고 서 있는 채로 from.pushDistance만큼 밀려난다.
    // 플레이어는 방패가 있으면 막힌다. 반환: 밀렸는지
    public bool ReceivePush(Vector3 direction, PlayerController from)
    {
        if (PlayerPowerUps.Blocked(this)) return false;
        float duration = from.pushDuration;
        // 속도가 일정하게 0까지 줄면 간 거리 = 처음 속도 × 시간 / 2
        shoveVelocity = direction * (2f * from.pushDistance / duration);
        shoveStartTime = Time.time;
        shoveEndTime = Time.time + duration;
        if (grid != null) grid.PlayIceBurst(body.position + Vector3.up * 0.3f - direction * 0.4f, 14, 0.6f);
        return true;
    }

    // 시련: 계속 미는 힘 (찬바람). 부른 프레임의 물리 단계에서 한 번 더해진다
    public void AddPush(Vector3 acceleration) => pendingPush += acceleration;

    // 파워업: 일정 시간 점프 높이·이동 속도 배율 (슈퍼 점프)
    public void SetBoost(float jumpMultiplier, float speedMultiplier, float duration)
    {
        boostJump = jumpMultiplier;
        boostSpeed = speedMultiplier;
        boostUntil = Time.time + duration;
    }

    public float BoostRemaining => Mathf.Max(0f, boostUntil - Time.time);

    // 봇이 다시 떨어져 들어올 때 상태를 처음으로
    public void ResetMotion(Vector3 position)
    {
        body.isKinematic = false;
        body.position = position;
        transform.position = position;
        body.linearVelocity = Vector3.zero;
        SetState(MoveState.Normal);
        lastJumpPressedTime = float.NegativeInfinity;
        diveQueued = false;
        shoveEndTime = 0f;
        externalMove = Vector3.zero;
        externalJumpHeld = externalJumpPressed = externalDivePressed = externalPushPressed = false;
        HasMoved = false;
        pushQueued = false;
        pushActiveUntil = 0f;
        nextPushTime = 0f;
        LastPushTime = float.NegativeInfinity;
    }

    // 원격 캐릭터는 물리를 중복 계산하지 않고 호스트 상태를 애니메이션에 전달한다.
    public void ApplyReplicaMotion(Vector3 velocity, MoveState state, bool grounded, float pushAge, float boostRemaining)
    {
        replicaVelocity = velocity;
        State = state;
        IsGrounded = grounded;
        if (pushAge >= 0f) LastPushTime = Time.time - pushAge;
        boostUntil = Time.time + boostRemaining;
    }

    // 몇 초 동안 조작을 막는다. 다이브 착지, 눈덩이 기절 등에서 공통으로 쓴다.
    public void KnockDown(float duration)
    {
        float endTime = Time.time + duration;
        if (State == MoveState.KnockedDown) endTime = Mathf.Max(endTime, knockDownEndTime);
        knockDownEndTime = endTime;
        lastJumpPressedTime = float.NegativeInfinity;
        SetState(MoveState.KnockedDown);
    }

    void ApplyMovement(ref Vector3 velocity, float control) => ApplyMovement(ref velocity, control, moveDirection * MoveSpeed);

    void ApplyMovement(ref Vector3 velocity, float control, Vector3 targetHorizontal)
    {
        Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
        // 가속보다 감속을 강하게 해서 출발은 묵직하고 멈춤은 깔끔하게
        bool speedingUp = targetHorizontal.sqrMagnitude > 0.001f && Vector3.Dot(targetHorizontal, horizontal) >= 0f;
        float rate = (speedingUp ? groundAcceleration : groundDeceleration) * control;
        horizontal = Vector3.MoveTowards(horizontal, targetHorizontal, rate * Time.fixedDeltaTime);
        velocity = new Vector3(horizontal.x, velocity.y, horizontal.z);
    }

    void ApplyGravity(ref Vector3 velocity)
    {
        if (IsGrounded) return;

        float gravity;
        if (State == MoveState.Diving) gravity = RiseGravity * diveGravityScale;
        else if (velocity.y > 0f) gravity = isJumping && jumpHeld ? RiseGravity : RiseGravity * jumpCutGravityScale;
        else gravity = RiseGravity * fallGravityScale;

        // Unity 기본 중력은 물리 단계에서 따로 더해지므로 그 차이만큼만 더한다
        velocity.y -= (gravity + Physics.gravity.y) * Time.fixedDeltaTime;
        velocity.y = Mathf.Max(velocity.y, -maxFallSpeed);
    }

    void Jump(ref Vector3 velocity)
    {
        velocity.y = JumpSpeed;
        isJumping = true;
        lastGroundedTime = float.NegativeInfinity;
        lastJumpPressedTime = float.NegativeInfinity;
        IsGrounded = false;
    }

    void StartDive(ref Vector3 velocity)
    {
        Vector3 direction = moveDirection.sqrMagnitude > 0.001f
            ? moveDirection.normalized
            : Vector3.ProjectOnPlane(body.rotation * Vector3.forward, Vector3.up).normalized;

        // 점프 꼭대기에서 다이브해야 가장 멀리 간다
        float upSpeed = IsGrounded ? groundDiveUpSpeed : Mathf.Clamp(velocity.y, 0f, airDiveMaxUpSpeed);
        velocity = new Vector3(direction.x * diveForwardSpeed, upSpeed, direction.z * diveForwardSpeed);
        isJumping = false;
        body.MoveRotation(Quaternion.LookRotation(direction, Vector3.up));
        SetState(MoveState.Diving);
    }

    // 몸으로 밀기: 걸어서(또는 밀려 미끄러지며) 부딪힌 상대를 미는 방향으로 같이 밀려나게 한다.
    // 상대의 이동 코드가 매 물리 단계마다 속도를 0으로 줄여서, 그냥 두면 벽처럼 버티기 때문
    void OnCollisionStay(Collision collision)
    {
        if (networkReplica) return;
        if (bodyShove <= 0f || collision.rigidbody == null) return;
        PlayerController other = collision.rigidbody.GetComponent<PlayerController>();
        if (other == null || other == this || !other.isActiveAndEnabled) return;

        Vector3 to = other.body.position - body.position;
        if (Mathf.Abs(to.y) > 0.8f) return;  // 머리 위에 올라선 경우 등
        to.y = 0f;
        if (to.sqrMagnitude < 0.0001f) return;
        Vector3 direction = to.normalized;

        // 내가 상대 쪽으로 가려는 속도 (걷는 중이면 입력 방향, 미끄러지는 중이면 실제 속도)
        float intended = State == MoveState.Normal ? Vector3.Dot(moveDirection * MoveSpeed, direction) : 0f;
        float mine = Mathf.Max(intended, Vector3.Dot(body.linearVelocity, direction));
        if (mine < 1f) return;
        // 상대가 나를 향해 걸어오는 속도. 마주 밀면 더 빠른 쪽만 민다
        float resist = other.State == MoveState.Normal ? -Vector3.Dot(other.moveDirection * other.MoveSpeed, direction) : 0f;
        if (resist >= mine - 0.5f) return;

        float target = (mine - Mathf.Max(0f, resist)) * bodyShove;
        float theirs = Vector3.Dot(other.body.linearVelocity, direction);
        if (theirs < target) other.body.linearVelocity += direction * (target - theirs);
    }

    // 밀치기 시작. 판정을 너그럽게:
    //  - 부채꼴 안에서 가장 잘 맞는 상대 쪽으로 몸을 돌려 민다 (조준 보정)
    //  - 누른 뒤 pushActiveTime 동안 계속 판정해서, 살짝 늦게 다가온 상대도 맞는다 (한 상대는 한 번만)
    // 못 맞혀도 동작과 재사용 대기는 그대로
    void TryPush(ref Vector3 velocity)
    {
        if (Time.time < nextPushTime) return;
        nextPushTime = Time.time + pushCooldown;
        LastPushTime = Time.time;

        Vector3 facing = moveDirection.sqrMagnitude > 0.01f
            ? moveDirection.normalized
            : Vector3.ProjectOnPlane(body.rotation * Vector3.forward, Vector3.up).normalized;
        PlayerController aim = BestPushTarget(facing);
        if (aim != null)
        {
            Vector3 toAim = Vector3.ProjectOnPlane(aim.body.position - body.position, Vector3.up);
            if (toAim.sqrMagnitude > 0.0001f) facing = toAim.normalized;
        }
        body.MoveRotation(Quaternion.LookRotation(facing, Vector3.up));
        GetComponent<PushVfx>()?.Play(facing);
        velocity += facing * pushLunge;

        pushFacing = facing;
        pushActiveUntil = Time.time + pushActiveTime;
        pushHits.Clear();
        PushHits();
    }

    // 밀치기 판정 안에 있는지: 수평 거리, 부채꼴 각도, 높이 차이
    bool InPushZone(PlayerController other, Vector3 facing, out Vector3 toward, out float distance, out float angle)
    {
        toward = facing;
        distance = angle = 0f;
        if (other == this || !other.isActiveAndEnabled) return false;
        Vector3 to = other.body.position - body.position;
        if (Mathf.Abs(to.y) > PushHeightTolerance) return false;
        to.y = 0f;
        distance = to.magnitude;
        if (distance > pushReach) return false;
        if (distance > 0.01f) toward = to / distance;
        angle = Vector3.Angle(facing, toward);
        return angle <= pushArc;
    }

    // 부채꼴 안에서 가장 정면에 가깝고 가까운 상대
    PlayerController BestPushTarget(Vector3 facing)
    {
        PlayerController best = null;
        float bestScore = float.PositiveInfinity;
        foreach (PlayerController other in All)
        {
            if (!InPushZone(other, facing, out _, out float distance, out float angle)) continue;
            float score = angle / pushArc + distance / pushReach;
            if (score < bestScore)
            {
                bestScore = score;
                best = other;
            }
        }
        return best;
    }

    // 판정 시간 동안 부채꼴 안에 들어온 상대를 민다 (한 상대는 한 번만)
    void PushHits()
    {
        foreach (PlayerController other in All.ToArray())
        {
            if (pushHits.Contains(other)) continue;
            if (!InPushZone(other, pushFacing, out Vector3 toward, out _, out _)) continue;
            pushHits.Add(other);
            // 바라보는 방향과 상대 쪽 방향의 중간으로 민다
            other.ReceivePush((pushFacing + toward).normalized, this);
        }
    }

    void RotateTowardsMovement()
    {
        if (moveDirection.sqrMagnitude < 0.001f) return;
        Quaternion target = Quaternion.LookRotation(moveDirection, Vector3.up);
        body.MoveRotation(Quaternion.RotateTowards(body.rotation, target, turnSpeed * Time.fixedDeltaTime));
    }

    void SetState(MoveState state)
    {
        State = state;
        stateStartTime = Time.time;
    }

    bool CheckGround()
    {
        // 캡슐 아래쪽 반구 위치에서 조금 작은 구를 아래로 쏜다
        float radius = capsule.radius * 0.9f;
        Vector3 origin = body.position + body.rotation * capsule.center + Vector3.down * (capsule.height * 0.5f - capsule.radius);
        bool hit = Physics.SphereCast(origin, radius, Vector3.down, out RaycastHit info, capsule.radius - radius + groundCheckDistance,
            ~0, QueryTriggerInteraction.Ignore);
        GroundCollider = hit ? info.collider : null;
        return hit;
    }
}
