using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 시련: 펭귄 슬라이딩.
//  예고: 맵 한쪽 끝 바깥에 펭귄이 나타나 readyTime 동안 뒤뚱거림(Ready) + 지나갈 줄이 붉게 표시됨
//  효과: 배로 엎드려(Slide) 한 줄을 끝까지 미끄러짐. 부딪히면 진행 방향으로 크게 밀려나며 넘어짐 (방패가 있으면 막힘)
//  깨진 칸 위도 그대로 미끄러져 지나감. 반대편 끝에서 바다로 머리부터 뛰어들고(Exit) 사라짐
//  60% 확률로 플레이어가 있는 줄을 고른다
public class PenguinTrial : TrialBase
{
    public GameObject penguinModel;
    public RuntimeAnimatorController penguinAnimator;
    public Material dangerMaterial;

    [Header("펭귄")]
    [Min(0.3f)] public float readyTime = 1f;
    [Min(1f)] public float slideSpeed = 9f;
    [Tooltip("부딪혔을 때 밀려나는 속도 (앞으로, 위로)")]
    public Vector2 knockback = new Vector2(9f, 4f);
    [Min(0f)] public float knockDownTime = 0.6f;
    [Min(0.2f)] public float hitRadius = 1f;
    [Range(0f, 1f)] public float playerLineChance = 0.6f;

    void Reset()
    {
        startTime = 60f;
        intervalRange = new Vector2(7f, 10f);
        announcement = "펭귄이 온다!";
        announcementColor = new Color(0.55f, 0.75f, 1f);
    }

    protected override IEnumerator Run(float elapsed)
    {
        if (penguinModel == null) yield break;

        // 가로줄 또는 세로줄 하나, 방향도 무작위
        bool alongX = Random.value < 0.5f;
        int lineCount = alongX ? grid.depth : grid.width;
        int lineLength = alongX ? grid.width : grid.depth;
        int line = Random.Range(0, lineCount);
        if (player != null && Random.value < playerLineChance)
        {
            Vector2Int p = grid.WorldToCell(player.transform.position);
            line = Mathf.Clamp(alongX ? p.y : p.x, 0, lineCount - 1);
        }
        bool forward = Random.value < 0.5f;

        Vector2Int CellAt(int i) => alongX ? new Vector2Int(i, line) : new Vector2Int(line, i);
        Vector3 first = grid.CellTop(CellAt(forward ? 0 : lineLength - 1));
        Vector3 last = grid.CellTop(CellAt(forward ? lineLength - 1 : 0));
        Vector3 direction = (last - first).normalized;
        Vector3 start = first - direction * (grid.Spacing * 1.5f);
        Vector3 end = last + direction * (grid.Spacing * 1.5f);

        // 예고: 등장 + 지나갈 줄 표시
        GameObject penguin = Instantiate(penguinModel, start, Quaternion.LookRotation(direction));
        var animator = penguin.GetComponent<Animator>();
        if (animator == null) animator = penguin.AddComponent<Animator>();
        animator.runtimeAnimatorController = penguinAnimator;
        animator.applyRootMotion = false;
        Play(animator, "Ready");
        grid.PlayIceBurst(start, 15, 0.5f);

        GameObject lane = Prop(PrimitiveType.Cube, dangerMaterial, (first + last) * 0.5f + Vector3.up * 0.03f,
            new Vector3(0.9f, 0.02f, Vector3.Distance(first, last) + grid.blockSize));
        lane.transform.rotation = Quaternion.LookRotation(direction);
        lane.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        yield return new WaitForSeconds(readyTime);
        Destroy(lane);

        // 슬라이딩
        Play(animator, "Slide");
        var alreadyHit = new HashSet<PlayerController>();
        float length = Vector3.Distance(start, end);
        for (float travelled = 0f; travelled < length; travelled += slideSpeed * Time.deltaTime)
        {
            Vector3 position = start + direction * travelled;
            penguin.transform.position = position;
            foreach (PlayerController target in Targets())
            {
                if (alreadyHit.Contains(target) || !Near(target, position, hitRadius, 1.5f)) continue;
                alreadyHit.Add(target);
                if (!Hit(target)) continue;
                target.Knockback(direction * knockback.x + Vector3.up * knockback.y, knockDownTime);
                grid.PlayIceBurst(target.transform.position, 12, 0.6f);
            }
            yield return null;
        }

        // 퇴장: 바다로 머리부터 뛰어듦
        Play(animator, "Exit");
        Vector3 velocity = direction * slideSpeed * 0.6f + Vector3.up * 3f;
        while (penguin.transform.position.y > grid.SeaLevel)
        {
            velocity += Physics.gravity * 2f * Time.deltaTime;
            penguin.transform.position += velocity * Time.deltaTime;
            yield return null;
        }
        Vector3 splash = penguin.transform.position;
        grid.PlayIceBurst(new Vector3(splash.x, grid.SeaLevel, splash.z), 30, 1.2f);
        Destroy(penguin);
    }

    static void Play(Animator animator, string state)
    {
        if (animator.runtimeAnimatorController != null) animator.Play(state, 0, 0f);
    }
}
