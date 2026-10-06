using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 모델·카메라·버튼은 에디터에서 저장한다. 이름 드롭다운 대신 모델을 좌우로 선택한다.
[ExecuteAlways]
public class CharacterSelection : MonoBehaviour
{
    public GameObject previewRig;
    public GameObject[] models;
    public Camera previewCamera;
    public Button previous, next;
    public TMP_Text caption, count;
    public RawImage image;
    [SerializeField] int selected;
    public int Index => selected;

    void Awake()
    {
        if (!Application.isPlaying) return;
        selected = Mathf.Clamp(PlayerPrefs.GetInt("LionIceRun.Skin", selected), 0, 2);
        previous?.onClick.AddListener(() => Select(selected - 1));
        next?.onClick.AddListener(() => Select(selected + 1));
    }
    void OnEnable()
    {
        if (previewRig != null) previewRig.SetActive(true);
        if (previewCamera != null) previewCamera.enabled = Application.isPlaying;
        Refresh();
#if UNITY_EDITOR
        if (!Application.isPlaying && previewCamera != null && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
            previewCamera.Render();
#endif
    }
    void OnDisable()
    {
        if (previewCamera != null) previewCamera.enabled = false;
        if (previewRig != null) previewRig.SetActive(false);
    }
    public void Select(int index) { selected = ((index % 3) + 3) % 3; Refresh(); }
    public void SetInteractable(bool value)
    { if (previous != null) previous.interactable = value; if (next != null) next.interactable = value; }
    void Refresh()
    {
        selected = Mathf.Clamp(selected, 0, 2);
        if (models != null) for (int i = 0; i < models.Length; i++) if (models[i] != null) models[i].SetActive(i == selected);
#if UNITY_EDITOR
        if (!Application.isPlaying && models != null && models[selected] != null && models[selected].activeInHierarchy)
        {
            var animator = models[selected].GetComponent<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.Rebind(); animator.Update(0f);
                foreach (var clip in animator.runtimeAnimatorController.animationClips)
                    if (clip.name == "Idle" || clip.name.EndsWith("|Idle")) { clip.SampleAnimation(animator.gameObject, 0f); break; }
            }
        }
#endif
        if (caption != null) caption.text = new[] { "사자", "북극곰", "호랑이" }[selected];
        if (count != null) count.text = (selected + 1) + " / 3";
    }
    void Update()
    {
        if (Application.isPlaying && models != null && selected < models.Length && models[selected] != null)
            models[selected].transform.localRotation = Quaternion.Euler(0f, Mathf.Sin(Time.unscaledTime * 0.65f) * 8f, 0f);
    }
}
