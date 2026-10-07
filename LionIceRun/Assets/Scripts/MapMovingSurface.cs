using UnityEngine;

// 이전 프레임의 접촉 위치를 현재 발판 좌표로 옮긴다. 캐릭터를 자식으로 붙이지 않는다.
public class MapMovingSurface : MonoBehaviour
{
    public Vector3 PreviousPosition;
    public Quaternion PreviousRotation;
    public Vector3 ConveyorVelocity;
    public bool MovingGeometry;
    public IceGrid Owner;
    public float RotationSpeed, HitSpeed = 10, LiftSpeed = 3.2f, DownSeconds = 1.05f;
    readonly System.Collections.Generic.Dictionary<PlayerController, float> nextHit = new System.Collections.Generic.Dictionary<PlayerController, float>();
    public Vector3 CarryDelta(Vector3 world)
    {
        Vector3 local = Quaternion.Inverse(PreviousRotation) * (world - PreviousPosition);
        return transform.position + transform.rotation * local - world + ConveyorVelocity * Time.fixedDeltaTime;
    }

    void OnCollisionEnter(Collision collision) => SideImpact(collision);
    void OnCollisionStay(Collision collision) => SideImpact(collision);

    void SideImpact(Collision collision)
    {
        if (!MovingGeometry || Owner == null || Owner.networkReplica || GameManager.Instance == null
            || GameManager.Instance.State != GameManager.GameState.Playing) return;
        var player = collision.gameObject.GetComponentInParent<PlayerController>();
        if (player == null || player.networkReplica || (nextHit.TryGetValue(player, out float at) && Time.time < at)) return;
        foreach (var contact in collision.contacts)
        {
            if (Mathf.Abs(contact.normal.y) > 0.5f) continue; // 윗면에 서 있는 승객은 충돌 타격 대상이 아니다.
            var deltaRotation = transform.rotation * Quaternion.Inverse(PreviousRotation);
            deltaRotation.ToAngleAxis(out float degrees, out Vector3 axis);
            if (degrees > 180) degrees -= 360;
            var angularVelocity = degrees > 0.001f || degrees < -0.001f
                ? axis * (degrees * Mathf.Deg2Rad / Time.fixedDeltaTime) : Vector3.zero;
            Vector3 speed = (transform.position - PreviousPosition) / Time.fixedDeltaTime
                + Vector3.Cross(angularVelocity, contact.point - transform.position);
            speed.y = 0;
            if (speed.magnitude < 0.7f) continue;
            nextHit[player] = Time.time + 1.2f;
            if (!PlayerPowerUps.Blocked(player)) player.Knockback(speed.normalized * Mathf.Clamp(HitSpeed, 4, 16)
                + Vector3.up * LiftSpeed, DownSeconds);
            break;
        }
    }
}
