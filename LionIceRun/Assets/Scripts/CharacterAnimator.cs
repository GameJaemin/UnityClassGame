using UnityEngine;

// PlayerController 상태를 Animator 파라미터로 넘긴다.
[RequireComponent(typeof(PlayerController))]
public class CharacterAnimator : MonoBehaviour
{
    [Tooltip("최고 속도로 걸을 때 Walk 클립 재생 속도")]
    [Min(0.1f)] public float walkAnimSpeedAtFullSpeed = 1.4f;

    static readonly int SpeedId = Animator.StringToHash("Speed");
    static readonly int WalkAnimSpeedId = Animator.StringToHash("WalkAnimSpeed");
    static readonly int GroundedId = Animator.StringToHash("Grounded");
    static readonly int VerticalSpeedId = Animator.StringToHash("VerticalSpeed");
    static readonly int DownId = Animator.StringToHash("Down");

    PlayerController player;
    Rigidbody body;
    Animator animator;

    public void SetAnimator(Animator target) => animator = target;

    void Awake()
    {
        player = GetComponent<PlayerController>();
        body = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (animator == null) return;

        float speed = player.HorizontalSpeed;
        animator.SetFloat(SpeedId, speed);
        animator.SetFloat(WalkAnimSpeedId, Mathf.Max(0.5f, speed / Mathf.Max(player.moveSpeed, 0.01f) * walkAnimSpeedAtFullSpeed));
        animator.SetBool(GroundedId, player.IsGrounded);
        animator.SetFloat(VerticalSpeedId, body.linearVelocity.y);
        // 다이브 중이거나 넘어져 있으면 Dive 자세 유지
        animator.SetBool(DownId, player.State != PlayerController.MoveState.Normal);
    }
}
