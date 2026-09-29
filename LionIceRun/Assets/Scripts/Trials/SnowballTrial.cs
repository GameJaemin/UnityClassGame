using System.Collections;
using UnityEngine;

// 시련: 눈덩이 낙하.
//  예고: 떨어질 칸에 그림자가 warnTime 동안 점점 커짐 (눈덩이도 하늘에서 내려오는 게 보임)
//  맞으면: stunTime 동안 기절 (조작 불가 + 머리 위 별). 방패가 있으면 막힘
//  블럭: 떨어진 칸이 한 단계 깨짐
//  시간이 지날수록 한 번에 떨어지는 개수가 늘어남 (30초마다 +1, 최대 maxCount)
public class SnowballTrial : TrialBase
{
    public Material shadowMaterial;
    public Material snowMaterial;
    public Material starMaterial;

    [Header("눈덩이")]
    [Min(0.3f)] public float warnTime = 1.5f;
    [Min(0f)] public float stunTime = 2f;
    [Min(0.2f)] public float hitRadius = 1.1f;
    [Min(1f)] public float dropHeight = 16f;
    [Min(1)] public int maxCount = 4;
    [Range(0f, 1f)] public float nearPlayerChance = 0.5f;

    void Reset()
    {
        startTime = 15f;
        intervalRange = new Vector2(4f, 6f);
        announcement = "눈덩이가 떨어져요!";
    }

    protected override IEnumerator Run(float elapsed)
    {
        int count = Mathf.Clamp(1 + Mathf.FloorToInt((elapsed - startTime) / 30f), 1, maxCount);
        for (int i = 0; i < count; i++)
        {
            if (PickCell(nearPlayerChance, 1, out Vector2Int cell)) StartCoroutine(Drop(cell));
            yield return new WaitForSeconds(0.35f);
        }
    }

    IEnumerator Drop(Vector2Int cell)
    {
        Vector3 top = grid.CellTop(cell);
        GameObject shadow = Disc(top, shadowMaterial, 0.3f);
        GameObject ball = Prop(PrimitiveType.Sphere, snowMaterial, top + Vector3.up * dropHeight, Vector3.one * 1.1f);

        for (float t = 0f; t < warnTime; t += Time.deltaTime)
        {
            float p = t / warnTime;
            float size = Mathf.Lerp(0.3f, 1.5f, p);
            shadow.transform.localScale = new Vector3(size, 0.01f, size);
            // 점점 빨라지며 떨어짐
            ball.transform.position = top + Vector3.up * (0.55f + (dropHeight - 0.55f) * (1f - p * p));
            ball.transform.Rotate(120f * Time.deltaTime, 0f, 60f * Time.deltaTime);
            yield return null;
        }

        Destroy(shadow);
        Destroy(ball);
        grid.PlayIceBurst(top + Vector3.up * 0.3f, 35, 1.1f);

        IceBlock block = grid.GetBlock(cell);
        if (block != null) block.ForceAdvance();

        foreach (PlayerController target in Targets())
        {
            if (!Near(target, top, hitRadius, 1.6f) || !Hit(target)) continue;
            target.KnockDown(stunTime);
            StunStars.Show(target.transform, stunTime, starMaterial);
        }
    }
}
