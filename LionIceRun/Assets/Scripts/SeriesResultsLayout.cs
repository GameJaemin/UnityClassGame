using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 사용자가 요청한 결과 안내와 나가기 버튼만 배치한다. 기존 HUD는 건드리지 않는다.
public static class SeriesResultsLayout
{
    public static void Apply(OnlineFrontend front, bool complete)
    {
        var canvas = front.transform as RectTransform;
        CornerButton(front.resultLeaveButton, canvas);
        if (front.leavePanel != null && front.matchLeaveButton != null)
        {
            var panel = (RectTransform)front.leavePanel.transform;
            panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.one;
#if UNITY_WEBGL && !UNITY_EDITOR
            // 웹 화면의 시간/우승 HUD 아래에 놓는다. 결과 화면 버튼은 기존 우측 상단을 쓴다.
            panel.anchoredPosition = new Vector2(-24, -252);
#else
            panel.anchoredPosition = new Vector2(-24, -24);
#endif
            panel.sizeDelta = new Vector2(144, 44);
            var button = (RectTransform)front.matchLeaveButton.transform;
            button.anchorMin = Vector2.zero; button.anchorMax = Vector2.one; button.pivot = Vector2.one * 0.5f;
            button.offsetMin = button.offsetMax = Vector2.zero;
            QuietButton(front.matchLeaveButton);
        }
        if (front.resultsHint == null) return;
        var hint = (RectTransform)front.resultsHint.transform;
        hint.anchorMin = hint.anchorMax = hint.pivot = Vector2.one * 0.5f;
        hint.anchoredPosition = new Vector2(0, complete ? -160 : -215);
        hint.sizeDelta = new Vector2(800, complete ? 64 : 112);
        front.resultsHint.alignment = TextAlignmentOptions.Center;
        front.resultsHint.enableAutoSizing = true;
        front.resultsHint.fontSizeMin = complete ? 22 : 30;
        front.resultsHint.fontSizeMax = complete ? 28 : 42;
        front.resultsHint.fontStyle = FontStyles.Bold;
        front.resultsHint.color = complete ? Color.white : new Color(1, 0.87f, 0.25f);
        front.resultsHint.raycastTarget = false;
        if (front.resultText != null)
        {
            var ranking = (RectTransform)front.resultText.transform;
            ranking.anchoredPosition = new Vector2(0, 35); ranking.sizeDelta = new Vector2(760, 300);
        }
    }

    static void CornerButton(Button button, RectTransform canvas)
    {
        if (button == null || canvas == null) return;
        var rect = (RectTransform)button.transform;
        var parent = rect.parent as RectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
        Vector3 corner = parent.InverseTransformPoint(canvas.TransformPoint(new Vector3(canvas.rect.xMax, canvas.rect.yMax, 0)));
        rect.anchoredPosition = (Vector2)corner - parent.rect.max - new Vector2(24, 24);
        rect.sizeDelta = new Vector2(144, 44);
        QuietButton(button);
    }

    static void QuietButton(Button button)
    {
        var group = button.GetComponent<CanvasGroup>();
        if (group == null) group = button.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0.55f;
        var label = button.GetComponentInChildren<TMP_Text>(true);
        if (label == null) return;
        label.enableAutoSizing = true; label.fontSizeMin = 18; label.fontSizeMax = 22;
    }
}
