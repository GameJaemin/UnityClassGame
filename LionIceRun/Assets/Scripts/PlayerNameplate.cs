using TMPro;
using UnityEngine;

// 이름표의 배치는 씬/프리팹에 저장하고, 플레이 중에는 이름·색상·카메라 방향만 갱신한다.
[ExecuteAlways, DefaultExecutionOrder(500)]
public class PlayerNameplate : MonoBehaviour
{
    public RectTransform plate;
    public TMP_Text label;

    void Awake() { UIConsistency.ApplyWebFont(label); }

    public void SetName(string nickname, bool local, bool bot)
    {
        if (label == null) return;
        UIConsistency.ApplyWebFont(label);
        label.text = OnlineRules.Nickname(nickname) + (bot ? " · AI" : local ? " · 나" : "");
        label.color = local ? new Color(1f, 0.87f, 0.3f) : bot ? new Color(0.65f, 0.85f, 1f) : Color.white;
    }

    void LateUpdate()
    {
        if (Application.isPlaying) Face(Camera.main);
    }

    void Face(Camera camera)
    {
        if (plate != null && camera != null) plate.rotation = camera.transform.rotation;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!Application.isPlaying && UnityEditor.SceneView.currentDrawingSceneView != null)
            Face(UnityEditor.SceneView.currentDrawingSceneView.camera);
    }
#endif
}
