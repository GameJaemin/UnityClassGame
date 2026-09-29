using System.Collections;
using UnityEngine;

// 시련: 고드름.
//  예고: 떨어질 칸에 붉은 원이 warnTime 동안 깜빡이며 커짐 (고드름도 하늘에서 내려오는 게 보임)
//  블럭: 단계와 상관없이 즉시 깨짐 (위에 있으면 떨어진다 → 붉은 원을 보면 피해야 함)
//  맞으면: 짧게 기절. 방패는 기절만 막고 블럭이 깨지는 건 못 막는다
public class IcicleTrial : TrialBase
{
    public Material dangerMaterial;
    public Material iceMaterial;
    public Material starMaterial;

    [Header("고드름")]
    [Min(0.3f)] public float warnTime = 1.3f;
    [Min(0f)] public float stunTime = 1f;
    [Min(1f)] public float dropHeight = 18f;
    [Range(0f, 1f)] public float nearPlayerChance = 0.5f;

    void Reset()
    {
        startTime = 45f;
        intervalRange = new Vector2(6f, 9f);
        announcement = "고드름 주의!";
        announcementColor = new Color(1f, 0.4f, 0.4f);
    }

    protected override IEnumerator Run(float elapsed)
    {
        if (!PickCell(nearPlayerChance, 1, out Vector2Int cell)) yield break;

        Vector3 top = grid.CellTop(cell);
        GameObject marker = Disc(top, dangerMaterial, 1.4f);
        GameObject icicle = Prop(PrimitiveType.Cylinder, iceMaterial, top + Vector3.up * dropHeight, new Vector3(0.35f, 0.9f, 0.35f));

        for (float t = 0f; t < warnTime; t += Time.deltaTime)
        {
            float p = t / warnTime;
            // 떨어질 때가 가까울수록 빠르게 깜빡이며 커짐
            float pulse = 1f + Mathf.Sin(t * Mathf.Lerp(8f, 30f, p)) * 0.08f;
            float size = Mathf.Lerp(0.8f, 1.5f, p) * pulse;
            marker.transform.localScale = new Vector3(size, 0.01f, size);
            icicle.transform.position = top + Vector3.up * (0.9f + (dropHeight - 0.9f) * (1f - p * p));
            yield return null;
        }

        Destroy(marker);
        Destroy(icicle);
        grid.PlayIceBurst(top + Vector3.up * 0.3f, 25, 1.3f);

        foreach (PlayerController target in Targets())
        {
            if (!Near(target, top, 0.9f, 1.6f) || !Hit(target)) continue;
            target.KnockDown(stunTime);
            StunStars.Show(target.transform, stunTime, starMaterial);
        }

        IceBlock block = grid.GetBlock(cell);
        if (block != null) block.ForceBreak();
    }
}
