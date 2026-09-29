using UnityEngine;
using UnityEngine.InputSystem;

// 폴가이즈 느낌의 Rigidbody 이동: 관성, 약한 공중 제어, 점프 보정, 다이브, 넘어짐.
// 점프는 슈퍼마리오처럼 길게 누르면 높게, 짧게 누르면 낮게 뛴다.
// 이동/점프 입력은 프로젝트 기본 액션(InputSystem_Actions)의 Player/Move, Player/Jump를 쓰고,
// 다이브는 이 스크립트에서 만든 액션(Ctrl / 게임패드 X)을 쓴다.
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

    // 다이브 직후 아직 발이 땅 근처일 때 곧바로 착지로 판정하지 않기 위한 시간
    const float MinDiveAirTime = 0.15f;

    Rigidbody body;
    CapsuleCollider capsule;
    Transform cameraTransform;
    InputAction moveAction;
    InputAction jumpAction;
    InputAction diveAction;

    Vector3 moveDirection;
    bool jumpHeld;
    Vector3 externalMove;
    bool externalJumpHeld;
    bool externalJumpPressed;
    bool externalDivePressed;
    Vector3 pendingPush;
    float boostJump = 1f;
    float boostSpeed = 1f;
    float boostUntil = float.NegativeInfinity;

    bool Boosted => Time.time < boostUntil;
    bool isJumping;
    float lastJumpPressedTime = float.NegativeInfinity;
    float lastGroundedTime = float.NegativeInfinity;
    bool diveQueued;
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
    public float HorizontalSpeed => new Vector3(body.linearVelocity.x, 0f, body.linearVelocity.z).magnitude;

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
    }

    void OnEnable() => diveAction.Enable();
    void OnDisable() => diveAction.Disable();
    void OnDestroy() => diveAction.Dispose();

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
        if (!InputEnabled)
        {
            moveDirection = Vector3.zero;
            jumpHeld = false;
            return;
        }

        if (useExternalInput)
        {
            moveDirection = Vector3.ClampMagnitude(Vector3.ProjectOnPlane(externalMove, Vector3.up), 1f);
            jumpHeld = externalJumpHeld;
            if (externalJumpPressed) lastJumpPressedTime = Time.time;
            if (externalDivePressed) diveQueued = true;
            externalJumpPressed = externalDivePressed = false;
            if (moveDirection.sqrMagnitude > 0.01f || jumpHeld || diveQueued) HasMoved = true;
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
        if (moveDirection.sqrMagnitude > 0.01f || jumpHeld || diveQueued) HasMoved = true;
    }

    void FixedUpdate()
    {
        Vector3 velocity = body.linearVelocity;

        // 위로 올라가는 중에는 땅으로 보지 않는다 (점프 직후 다시 착지 판정되는 것 방지)
        IsGrounded = velocity.y <= 0.5f && CheckGround();
        if (!IsGrounded) GroundCollider = null;
        if (IsGrounded)
        {
            lastGroundedTime = Time.time;
            isJumping = false;
        }

        switch (State)
        {
            case MoveState.Normal:
                ApplyMovement(ref velocity, IsGrounded ? 1f : airControl);
                if (Time.time - lastGroundedTime <= coyoteTime && Time.time - lastJumpPressedTime <= jumpBufferTime)
                    Jump(ref velocity);
                else if (diveQueued)
                    StartDive(ref velocity);
                RotateTowardsMovement();
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

        ApplyGravity(ref velocity);
        velocity += pendingPush * Time.fixedDeltaTime;
        pendingPush = Vector3.zero;
        body.linearVelocity = velocity;
    }

    // AI 봇용 입력. move는 월드 방향(크기 0~1), 누름(pressed)은 다음 Update에서 한 번 처리된다
    public void SetExternalInput(Vector3 move, bool holdJump, bool pressJump, bool pressDive)
    {
        externalMove = move;
        externalJumpHeld = holdJump;
        externalJumpPressed |= pressJump;
        externalDivePressed |= pressDive;
    }

    // 시련: 세게 밀려나며 넘어짐 (펭귄 충돌 등)
    public void Knockback(Vector3 velocity, float downTime)
    {
        body.linearVelocity = velocity;
        IsGrounded = false;
        KnockDown(downTime);
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
