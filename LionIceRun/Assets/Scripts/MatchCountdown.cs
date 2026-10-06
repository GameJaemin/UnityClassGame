using TMPro;
using UnityEngine;

// 레이아웃은 에디터에서 저장한다. 숫자가 바뀔 때만 짧게 확대했다가 돌아온다.
public class MatchCountdown : MonoBehaviour
{
    public static MatchCountdown Instance { get; private set; }
    public TMP_Text number;
    int shown = -1;
    float changedAt;
    void Awake() { Instance = this; Hide(); }
    void OnDestroy() { if (Instance == this) Instance = null; }
    public void Show(int remaining)
    {
        if (number == null) return;
        number.gameObject.SetActive(true);
        if (shown == remaining) return;
        shown = remaining; changedAt = Time.unscaledTime;
        number.text = remaining > 0 ? remaining.ToString() : "시작!";
    }
    public void Hide()
    {
        shown = -1;
        if (number != null) { number.gameObject.SetActive(false); number.transform.localScale = Vector3.one; }
    }
    void Update()
    {
        if (number != null && number.gameObject.activeSelf)
            number.transform.localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, Mathf.Clamp01((Time.unscaledTime - changedAt) / 0.2f));
    }
}
