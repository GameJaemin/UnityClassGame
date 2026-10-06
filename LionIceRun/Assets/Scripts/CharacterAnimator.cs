using UnityEngine;

// PlayerController 상태를 Animator 파라미터로 넘긴다.
// 밀치기는 애니메이션 위에 모델의 예비 동작·전진·양팔 뻗기를 얹는다. 충돌체는 움직이지 않는다.
[RequireComponent(typeof(PlayerController))]
public class CharacterAnimator : MonoBehaviour
{
    [Tooltip("최고 속도로 걸을 때 Walk 클립 재생 속도")]
    [Min(0.1f)] public float walkAnimSpeedAtFullSpeed = 1.4f;
    [Tooltip("밀치기 때 팔을 뻗었다 거두는 전체 시간")]
    [Min(0.1f)] public float pushPoseTime = 0.55f;
    [Tooltip("밀치기 때 몸을 앞으로 기울이는 각도")]
    [Range(0f, 30f)] public float pushLean = 16f;
    [Tooltip("밀치기 때 모델만 앞으로 내미는 거리")]
    [Min(0f)] public float pushModelReach = 0.16f;

    static readonly int SpeedId = Animator.StringToHash("Speed");
    static readonly int WalkAnimSpeedId = Animator.StringToHash("WalkAnimSpeed");
    static readonly int GroundedId = Animator.StringToHash("Grounded");
    static readonly int VerticalSpeedId = Animator.StringToHash("VerticalSpeed");
    static readonly int DownId = Animator.StringToHash("Down");

    // 팔을 뻗는 데 걸리는 시간, 뻗은 채 유지하는 시간
    const float PushWindupTime = 0.055f;
    const float PushReachTime = 0.07f;
    const float PushHoldTime = 0.2f;

    PlayerController player;
    Animator animator;
    Vector3 modelRestPosition;
    Quaternion modelRestRotation;
    Transform armL, armR;
    // 처음 자세(팔을 내린 상태)에서 팔이 아래로 향하는 방향을 뼈 기준으로 기억
    Vector3 armLDown, armRDown;

    public void SetAnimator(Animator target)
    {
        animator = target;
        modelRestPosition = target.transform.localPosition;
        modelRestRotation = target.transform.localRotation;
        armL = FindDeep(target.transform, "Arm_L");
        armR = FindDeep(target.transform, "Arm_R");
        if (armL != null) armLDown = Quaternion.Inverse(armL.rotation) * Vector3.down;
        if (armR != null) armRDown = Quaternion.Inverse(armR.rotation) * Vector3.down;
    }

    void Awake()
    {
        player = GetComponent<PlayerController>();
    }

    void Update()
    {
        if (animator == null) return;

        float speed = player.HorizontalSpeed;
        animator.SetFloat(SpeedId, speed);
        animator.SetFloat(WalkAnimSpeedId, Mathf.Max(0.5f, speed / Mathf.Max(player.moveSpeed, 0.01f) * walkAnimSpeedAtFullSpeed));
        animator.SetBool(GroundedId, player.IsGrounded);
        animator.SetFloat(VerticalSpeedId, player.Velocity.y);
        // 다이브 중이거나 넘어져 있으면 Dive 자세 유지
        animator.SetBool(DownId, player.State != PlayerController.MoveState.Normal);
    }

    void LateUpdate()
    {
        if (animator == null) return;
        // 모델의 추가 동작을 기준 자세로 되돌려, 동작 종료나 부활 뒤에 기울기가 남지 않게 한다.
        animator.transform.localPosition = modelRestPosition;
        animator.transform.localRotation = modelRestRotation;
        float t = Time.time - player.LastPushTime;
        if (player.State != PlayerController.MoveState.Normal || t < 0f || t > pushPoseTime) return;

        float reachTime = Mathf.Max(0f, t - PushWindupTime);
        float weight = reachTime < PushReachTime ? Mathf.SmoothStep(0f, 1f, reachTime / PushReachTime)
            : 1f - Mathf.SmoothStep(0f, 1f, (reachTime - PushReachTime - PushHoldTime)
                / Mathf.Max(0.01f, pushPoseTime - PushWindupTime - PushReachTime - PushHoldTime));
        float lean = t < PushWindupTime ? -8f * Mathf.Sin(t / PushWindupTime * Mathf.PI) : pushLean * weight;
        animator.transform.localRotation = modelRestRotation * Quaternion.Euler(lean, 0f, 0f);
        animator.transform.localPosition = modelRestPosition + modelRestRotation * Vector3.forward * (pushModelReach * weight);
        Reach(armL, armLDown, (transform.forward + transform.right * 0.18f + Vector3.up * 0.12f).normalized, weight);
        Reach(armR, armRDown, (transform.forward - transform.right * 0.18f + Vector3.up * 0.12f).normalized, weight);
    }

    static void Reach(Transform arm, Vector3 localDown, Vector3 target, float weight)
    {
        if (arm == null) return;
        Quaternion full = Quaternion.FromToRotation(arm.rotation * localDown, target);
        arm.rotation = Quaternion.Slerp(Quaternion.identity, full, weight) * arm.rotation;
    }

    static Transform FindDeep(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        foreach (Transform child in parent)
        {
            Transform found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
