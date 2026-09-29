using UnityEngine;

// 깨진 얼음 블럭의 조각. 튀어나가며 떨어지다가 점점 작아져 사라진다.
// 충돌체가 없어서 플레이어나 다른 블럭에 부딪히지 않는 연출 전용 오브젝트다.
public class IceShard : MonoBehaviour
{
    float lifetime;
    float age;
    Vector3 startScale;

    public void Launch(Vector3 velocity, float life)
    {
        lifetime = life;
        startScale = transform.localScale;

        Rigidbody body = gameObject.AddComponent<Rigidbody>();
        body.mass = 0.2f;
        body.linearVelocity = velocity;
        body.angularVelocity = Random.insideUnitSphere * 8f;
    }

    void Update()
    {
        age += Time.deltaTime;
        // 마지막 40% 동안 작아진다
        float t = Mathf.InverseLerp(lifetime * 0.6f, lifetime, age);
        transform.localScale = startScale * (1f - t);
        if (age >= lifetime) Destroy(gameObject);
    }
}
