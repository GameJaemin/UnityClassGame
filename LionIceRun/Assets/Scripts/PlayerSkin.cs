using UnityEngine;

// 게임 시작 시 선택한 스킨 모델을 Player 아래에 붙인다.
// 에디터에서는 캡슐이 자리 표시용으로 보이고, 플레이하면 캡슐을 숨기고 모델로 바뀐다.
public class PlayerSkin : MonoBehaviour
{
    public enum Skin { Lion, PolarBear, Tiger }

    public Skin skin = Skin.Lion;
    [Tooltip("Skin 순서대로: Lion, PolarBear, Tiger 모델(FBX)")]
    public GameObject[] models = new GameObject[3];
    public RuntimeAnimatorController animatorController;

    [Header("모델 위치")]
    [Tooltip("캡슐 중심에서 발바닥까지 (캡슐 높이 2 기준 -1)")]
    public Vector3 modelOffset = new Vector3(0f, -1f, 0f);
    public float modelYaw;

    public GameObject CurrentModel { get; private set; }

    void Awake()
    {
        var player = GetComponent<PlayerController>();
        if (player != null && !player.useExternalInput)
            skin = (Skin)Mathf.Clamp(PlayerPrefs.GetInt("LionIceRun.Skin", (int)skin), 0, 2);
        Apply();
    }

    public void Apply()
    {
        if (CurrentModel != null) Destroy(CurrentModel);

        int index = (int)skin;
        if (models == null || index >= models.Length || models[index] == null)
        {
            Debug.LogWarning($"PlayerSkin: {skin} 모델이 비어 있습니다.", this);
            return;
        }

        CurrentModel = Instantiate(models[index], transform);
        CurrentModel.name = "Model";
        CurrentModel.transform.localPosition = modelOffset;
        CurrentModel.transform.localRotation = Quaternion.Euler(0f, modelYaw, 0f);

        Animator animator = CurrentModel.GetComponent<Animator>();
        if (animator == null) animator = CurrentModel.AddComponent<Animator>();
        animator.runtimeAnimatorController = animatorController;
        animator.applyRootMotion = false;

        // 자리 표시용 캡슐은 숨긴다 (충돌은 그대로 캡슐로)
        MeshRenderer placeholder = GetComponent<MeshRenderer>();
        if (placeholder != null) placeholder.enabled = false;

        CharacterAnimator characterAnimator = GetComponent<CharacterAnimator>();
        if (characterAnimator != null) characterAnimator.SetAnimator(animator);
    }
}
