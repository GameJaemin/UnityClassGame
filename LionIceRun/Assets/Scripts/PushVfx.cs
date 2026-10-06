using UnityEngine;

// 호스트의 밀치기 시작 또는 원격 밀치기 경과 시간으로 한 번만 방출한다.
public class PushVfx : MonoBehaviour
{
    public ParticleSystem particles;
    float emittedAt = float.NegativeInfinity;
    public void Play(Vector3 direction)
    {
        if (particles == null || Time.unscaledTime - emittedAt < 0.65f) return;
        emittedAt = Time.unscaledTime;
        direction = Vector3.ProjectOnPlane(direction, Vector3.up);
        if (direction.sqrMagnitude < 0.01f) direction = Vector3.forward;
        direction.Normalize();
        particles.transform.SetPositionAndRotation(transform.position + Vector3.up * 0.45f + direction * 0.65f,
            Quaternion.LookRotation(direction, Vector3.up));
        particles.Play(false);
        particles.Emit(20);
    }
    public void ApplyReplica(float age, Vector3 direction)
    {
        if (age >= 0f && age < 0.25f) Play(direction);
    }
    void OnDisable() { if (particles != null) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
}
