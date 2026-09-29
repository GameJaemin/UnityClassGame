using UnityEngine;

// 기절 연출: 머리 위에서 노란 별(작은 큐브)이 빙글빙글 돈다. duration 뒤 사라짐
public class StunStars : MonoBehaviour
{
    const int Count = 4;
    Transform[] stars;
    float endTime;

    public static void Show(Transform target, float duration, Material material)
    {
        StunStars existing = target.GetComponentInChildren<StunStars>();
        if (existing != null)
        {
            existing.endTime = Mathf.Max(existing.endTime, Time.time + duration);
            return;
        }
        var go = new GameObject("StunStars");
        go.transform.SetParent(target, false);
        go.transform.localPosition = Vector3.up * 1.35f;
        var stunStars = go.AddComponent<StunStars>();
        stunStars.endTime = Time.time + duration;
        stunStars.Build(material);
    }

    void Build(Material material)
    {
        var properties = new MaterialPropertyBlock();
        properties.SetColor("_BaseColor", new Color(1f, 0.9f, 0.2f));
        properties.SetColor("_EmissionColor", new Color(1f, 0.8f, 0.1f) * 1.5f);
        stars = new Transform[Count];
        for (int i = 0; i < Count; i++)
        {
            GameObject star = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(star.GetComponent<Collider>());
            star.transform.SetParent(transform, false);
            star.transform.localScale = Vector3.one * 0.18f;
            var renderer = star.GetComponent<Renderer>();
            if (material != null) renderer.sharedMaterial = material;
            renderer.SetPropertyBlock(properties);
            stars[i] = star.transform;
        }
    }

    void Update()
    {
        if (Time.time >= endTime)
        {
            Destroy(gameObject);
            return;
        }
        for (int i = 0; i < Count; i++)
        {
            float angle = Time.time * 5f + i * Mathf.PI * 2f / Count;
            stars[i].localPosition = new Vector3(Mathf.Cos(angle) * 0.55f, Mathf.Sin(angle * 2f) * 0.08f, Mathf.Sin(angle) * 0.55f);
            stars[i].localRotation = Quaternion.Euler(45f, angle * Mathf.Rad2Deg * 2f, 45f);
        }
    }
}
