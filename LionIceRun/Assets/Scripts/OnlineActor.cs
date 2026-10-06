using UnityEngine;

// NGO의 사용자 메시지로 전달한 호스트 물리를 표시한다. 참가자 PC에서는 충돌을 계산하지 않는다.
public class OnlineActor : MonoBehaviour
{
    public OnlineSpawn spawn;
    public PlayerController controller;
    Vector3 targetPosition, targetVelocity;
    Quaternion targetRotation;
    float receivedAt;
    bool hasPose;

    public void ApplyPose(Vector3 position, Quaternion rotation, Vector3 velocity,
        PlayerController.MoveState state, bool grounded, float pushAge, bool shield, float boost,
        bool active, bool fallen, float remaining, bool waiting)
    {
        bool snap = !hasPose || active != gameObject.activeSelf || Vector3.Distance(transform.position, position) > 3f;
        gameObject.SetActive(active);
        targetPosition = position;
        targetRotation = rotation;
        targetVelocity = velocity;
        receivedAt = Time.unscaledTime;
        hasPose = true;
        if (snap) transform.SetPositionAndRotation(position, rotation);
        controller.ApplyReplicaMotion(velocity, state, grounded, pushAge, boost);
        if (active) GetComponent<PushVfx>()?.ApplyReplica(pushAge, rotation * Vector3.forward);
        GetComponent<PlayerPowerUps>()?.ApplyOnlineShield(shield);
        GameManager.Instance?.ApplyOnlineStatus(controller, fallen, remaining, waiting);
    }

    void LateUpdate()
    {
        if (!controller.networkReplica || !hasPose) return;
        float age = Mathf.Min(0.08f, Time.unscaledTime - receivedAt);
        float blend = 1f - Mathf.Exp(-22f * Time.unscaledDeltaTime);
        transform.position = Vector3.Lerp(transform.position, targetPosition + targetVelocity * age, blend);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, blend);
    }
}
