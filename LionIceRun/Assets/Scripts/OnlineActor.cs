using UnityEngine;

// 다른 참가자는 호스트 위치를 표시하고, 내 캐릭터는 입력을 즉시 계산한 뒤 과거 위치 차이만 보정한다.
public class OnlineActor : MonoBehaviour
{
    public OnlineSpawn spawn;
    public PlayerController controller;
    Vector3 targetPosition, targetVelocity, correction;
    Quaternion targetRotation;
    float receivedAt;
    bool hasPose, hasCapturedPose;
    Rigidbody body;
    PushVfx pushVfx;
    PlayerPowerUps powerUps;
    readonly OnlineMotionHistory history = new OnlineMotionHistory();
    public struct Motion
    {
        public Vector3 position, velocity;
        public Quaternion rotation;
        public uint acknowledged;
        public PlayerController.MoveState state;
        public bool grounded;
        public float down, shove;
    }
    Motion captured;
    void OnEnable() { hasCapturedPose = false; history.Clear(); correction = Vector3.zero; }
    void Awake()
    {
        body = GetComponent<Rigidbody>();
        pushVfx = GetComponent<PushVfx>();
        powerUps = GetComponent<PlayerPowerUps>();
    }
    public Motion CaptureMotion() => hasCapturedPose ? captured : ReadMotion();
    Motion ReadMotion() => new Motion {
        position = body.position, rotation = body.rotation, velocity = controller.Velocity,
        acknowledged = controller.SimulatedInputSequence, state = controller.State,
        grounded = controller.IsGrounded, down = controller.DownRemaining, shove = controller.ShoveRemaining
    };

    public void ApplyPose(Vector3 position, Quaternion rotation, Vector3 velocity,
        PlayerController.MoveState state, bool grounded, float pushAge, bool shield, float boost,
        bool active, bool fallen, float remaining, bool waiting, uint acknowledged = 0, float down = 0, float shove = 0)
    {
        bool changedActive = active != gameObject.activeSelf;
        bool snap = !hasPose || changedActive;
        targetPosition = position; targetRotation = rotation; targetVelocity = velocity;
        receivedAt = Time.unscaledTime;
        gameObject.SetActive(active);
        if (controller.localPrediction)
        {
            bool running = active && !fallen && OnlineGameSession.MatchPlaying;
            body.isKinematic = !running;
            if (!running) snap = true;
            if (!snap && history.Correction(acknowledged, position, out var error))
            {
                correction += error;
                if (correction.sqrMagnitude > 9f) snap = true;
            }
            else if (!snap && Vector3.Distance(body.position, position) > 5f) snap = true;
            if (snap)
            {
                body.position = position; body.rotation = rotation;
                transform.SetPositionAndRotation(position, rotation);
                history.Clear(); correction = Vector3.zero;
            }
            if (running) controller.ApplyPredictionAuthority(velocity, state, down, shove, boost, snap);
        }
        else
        {
            snap |= Vector3.Distance(transform.position, position) > 3f;
            if (snap) transform.SetPositionAndRotation(position, rotation);
            controller.ApplyReplicaMotion(velocity, state, grounded, pushAge, boost);
            if (active) pushVfx?.ApplyReplica(pushAge, rotation * Vector3.forward);
        }
        hasPose = true;
        powerUps?.ApplyOnlineShield(shield);
        GameManager.Instance?.ApplyOnlineStatus(controller, fallen, remaining, waiting);
    }

    void FixedUpdate()
    {
        if (!controller.localPrediction || body.isKinematic || !OnlineGameSession.MatchPlaying) return;
        Vector3 step = Vector3.ClampMagnitude(correction * (1f - Mathf.Exp(-12f * Time.fixedDeltaTime)), 0.18f);
        body.position += step; correction -= step;
    }
    void LateUpdate()
    {
        if (controller.localPrediction)
        {
            if (!body.isKinematic && OnlineGameSession.MatchPlaying)
                history.Record(controller.SimulatedInputSequence, body.position + correction);
            return;
        }
        if (!controller.networkReplica)
        {
            // 입력을 물리로 처리한 뒤의 위치와 처리 순번을 한 묶음으로 보낸다.
            captured = ReadMotion(); hasCapturedPose = true;
            return;
        }
        if (!hasPose) return;
        float age = Mathf.Min(0.08f, Time.unscaledTime - receivedAt);
        float blend = 1f - Mathf.Exp(-22f * Time.unscaledDeltaTime);
        transform.position = Vector3.Lerp(transform.position, targetPosition + targetVelocity * age, blend);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, blend);
    }
}
