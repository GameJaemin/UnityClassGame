using UnityEngine;

// 플레이어가 가진 파워업 상태.
//  - 방패: 시련에 한 번 맞는 것을 막아 준다 (눈덩이, 펭귄, 고드름 기절). 몸 주위에 반투명 거품
//  - 슈퍼 점프: 일정 시간 점프가 높고 발이 빨라져 깨진 칸 2개도 넘을 수 있다
// 얼음 굳히기는 먹는 순간 효과가 끝나서 상태가 없다 (RepairItemSpawner에서 처리).
[RequireComponent(typeof(PlayerController))]
public class PlayerPowerUps : MonoBehaviour
{
    [Header("방패")]
    public Material shieldMaterial;
    [Min(0.5f)] public float shieldSize = 2.6f;

    [Header("슈퍼 점프")]
    [Min(1f)] public float superJumpHeight = 1.6f;
    [Min(1f)] public float superJumpSpeed = 1.25f;
    [Min(0.5f)] public float superJumpDuration = 6f;

    PlayerController controller;
    GameObject bubble;

    public bool HasShield { get; private set; }
    public float SuperJumpRemaining => controller != null ? controller.BoostRemaining : 0f;

    void Awake() => controller = GetComponent<PlayerController>();

    void Update()
    {
        if (bubble != null)
        {
            // 거품이 살짝 숨 쉬듯 커졌다 작아짐
            float pulse = 1f + Mathf.Sin(Time.time * 4f) * 0.04f;
            bubble.transform.localScale = Vector3.one * shieldSize * pulse;
        }
    }

    public void GiveShield()
    {
        HasShield = true;
        if (bubble != null) return;
        bubble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bubble.name = "ShieldBubble";
        Destroy(bubble.GetComponent<Collider>());
        bubble.transform.SetParent(transform, false);
        bubble.transform.localPosition = Vector3.zero;
        var renderer = bubble.GetComponent<Renderer>();
        if (shieldMaterial != null) renderer.sharedMaterial = shieldMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    public void GiveSuperJump() => controller.SetBoost(superJumpHeight, superJumpSpeed, superJumpDuration);

    public void ApplyOnlineShield(bool shield)
    {
        if (shield == HasShield) return;
        if (shield) GiveShield();
        else
        {
            HasShield = false;
            if (bubble != null) Destroy(bubble);
            bubble = null;
        }
    }

    // 시련이 맞히기 전에 부른다. 방패가 있으면 쓰고 true (맞지 않음)
    public bool TryBlock()
    {
        if (!HasShield) return false;
        HasShield = false;
        if (bubble != null)
        {
            IceGrid grid = controller.grid;
            if (grid != null) grid.PlayIceBurst(bubble.transform.position, 30, 1.2f);
            Destroy(bubble);
            bubble = null;
        }
        if (GameHUD.Instance != null) GameHUD.Instance.ShowToast("방패가 막았어요!", new Color(0.45f, 0.75f, 1f));
        return true;
    }

    // 시련 쪽에서 쓰는 도우미: 방패가 있으면 막고 true
    public static bool Blocked(PlayerController target)
    {
        var powerUps = target.GetComponent<PlayerPowerUps>();
        return powerUps != null && powerUps.TryBlock();
    }
}
