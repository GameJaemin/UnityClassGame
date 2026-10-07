using UnityEngine;

public class PlayerEmoteBubble : MonoBehaviour
{
    IceEmoteIcon icon;
    CanvasGroup group;
    RectTransform bubble;
    float until, shownAt;
    int last = -1;
    public void Show(int expression)
    {
        if (!gameObject.activeInHierarchy) return;
        if (last == expression && Time.unscaledTime - shownAt < .5f) return;
        if (bubble == null)
        {
            var root = new GameObject("EmoteBubble", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            bubble = (RectTransform)root.transform; bubble.SetParent(transform, false);
            bubble.localPosition = new Vector3(0,3.35f,0); bubble.sizeDelta = new Vector2(112,112);
            root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            root.GetComponent<Canvas>().sortingOrder = 250;
            group = root.GetComponent<CanvasGroup>(); group.blocksRaycasts = false;
            var shape = root.AddComponent<IceUIShape>(); shape.radius = 8; shape.color = IceUI.Panel; shape.bottom = shape.color; shape.edge=IceUI.Ink; shape.border=3; shape.raycastTarget = false;
            var face = new GameObject("Face",typeof(RectTransform),typeof(IceEmoteIcon));
            face.transform.SetParent(root.transform,false); ((RectTransform)face.transform).sizeDelta = new Vector2(86,86);
            icon = face.GetComponent<IceEmoteIcon>(); icon.raycastTarget = false;
        }
        last = expression; shownAt = Time.unscaledTime; until = shownAt + 2.7f;
        icon.expression = expression; icon.SetAllDirty(); bubble.gameObject.SetActive(true);
    }
    void LateUpdate()
    {
        if (bubble == null || !bubble.gameObject.activeSelf) return;
        if (Time.unscaledTime >= until) { bubble.gameObject.SetActive(false); return; }
        if (Camera.main != null) bubble.rotation = Camera.main.transform.rotation;
        float t = Mathf.Clamp01((Time.unscaledTime-shownAt)/.18f);
        bubble.localScale = Vector3.one * (.012f * Mathf.Lerp(.7f,1,Mathf.SmoothStep(0,1,t)));
        group.alpha = Mathf.Clamp01((until-Time.unscaledTime)/.3f);
    }
    void OnDisable() { if (bubble != null) bubble.gameObject.SetActive(false); }
    public static void Display(PlayerController player, int expression)
    { if (player != null) (player.GetComponent<PlayerEmoteBubble>() ?? player.gameObject.AddComponent<PlayerEmoteBubble>()).Show(expression); }
}
