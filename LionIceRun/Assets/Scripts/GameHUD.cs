using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// 게임 화면 UI (Step 7). 배치는 UI 시트 시안의 "인게임 화면"과 "실패 화면"을 따른다.
//  - 왼쪽 위: 남은 얼음 수 (얼음 큐브 + "28 / 36")
//  - 오른쪽 위: 흰 타이머 배지 (스톱워치 + 버틴 시간), 그 아래 최고 기록
//  - 오른쪽 아래: 조작 안내 패널 (이동 / 점프 / 다이브)
//  - 시작 전: 아래 가운데 "움직이면 시작!" (통통 튐)
//  - 게임 오버: 결과 카드 (아쉽네요 배너 또는 새 기록 배지, 버틴 시간, 최고 기록, 다시 하기), R 키로도 재시작
public class GameHUD : MonoBehaviour
{
    public IceGrid grid;

    [Header("상단")]
    public TMP_Text iceCountText;
    public TMP_Text timerText;
    public TMP_Text bestText;

    [Header("아래")]
    public GameObject controlsPanel;
    public RectTransform readyPrompt;

    [Header("결과 화면")]
    public GameObject resultGroup;
    public RectTransform resultCard;
    public GameObject gameOverBanner;
    public GameObject newRecordBadge;
    public TMP_Text resultTimeText;
    public TMP_Text resultBestText;
    [Tooltip("바다에 빠지는 모습을 보여준 뒤 결과 화면을 띄울 때까지")]
    [Min(0f)] public float resultDelay = 0.8f;

    Vector2 readyPromptBase;

    void Awake()
    {
        if (readyPrompt != null)
        {
            readyPrompt.gameObject.SetActive(true);
            readyPromptBase = readyPrompt.anchoredPosition;
        }
        if (resultGroup != null) resultGroup.SetActive(false);
    }

    void Start()
    {
        if (GameManager.Instance != null) GameManager.Instance.StateChanged += OnStateChanged;
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.StateChanged -= OnStateChanged;
    }

    void Update()
    {
        SurvivalTimer timer = SurvivalTimer.Instance;
        if (timer != null)
        {
            if (timerText != null) timerText.text = SurvivalTimer.Format(timer.Elapsed);
            float best = Mathf.Max(timer.BestTime, timer.IsRunning ? timer.Elapsed : 0f);
            if (bestText != null) bestText.text = $"최고  {SurvivalTimer.Format(best)}";
        }

        if (iceCountText != null && grid != null && grid.IceBlocks.Count > 0)
        {
            int alive = 0;
            foreach (IceBlock block in grid.IceBlocks) if (!block.IsBroken) alive++;
            iceCountText.text = $"{alive} / {grid.IceBlocks.Count}";
        }

        // "움직이면 시작!" 통통 튀기
        if (readyPrompt != null && readyPrompt.gameObject.activeSelf)
        {
            float bounce = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3f));
            readyPrompt.anchoredPosition = readyPromptBase + Vector2.up * (bounce * 14f);
            readyPrompt.localScale = Vector3.one * (1f + bounce * 0.04f);
        }

        GameManager manager = GameManager.Instance;
        if (manager != null && manager.State == GameManager.GameState.GameOver
            && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            manager.Restart();
    }

    void OnStateChanged(GameManager.GameState state)
    {
        if (state == GameManager.GameState.Playing && readyPrompt != null) readyPrompt.gameObject.SetActive(false);
        if (state == GameManager.GameState.GameOver)
        {
            if (controlsPanel != null) controlsPanel.SetActive(false);
            StartCoroutine(ShowResult());
        }
    }

    IEnumerator ShowResult()
    {
        yield return new WaitForSeconds(resultDelay);

        SurvivalTimer timer = SurvivalTimer.Instance;
        bool newRecord = timer != null && timer.IsNewRecord;
        if (timer != null)
        {
            if (resultTimeText != null) resultTimeText.text = SurvivalTimer.Format(timer.Elapsed);
            if (resultBestText != null) resultBestText.text = SurvivalTimer.Format(timer.BestTime);
        }
        if (gameOverBanner != null) gameOverBanner.SetActive(!newRecord);
        if (newRecordBadge != null) newRecordBadge.SetActive(newRecord);
        resultGroup.SetActive(true);

        // 톡 튀어나오는 등장
        if (resultCard != null)
        {
            const float duration = 0.35f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float p = t / duration;
                float scale = p < 0.7f ? Mathf.Lerp(0.3f, 1.08f, p / 0.7f) : Mathf.Lerp(1.08f, 1f, (p - 0.7f) / 0.3f);
                resultCard.localScale = Vector3.one * scale;
                yield return null;
            }
            resultCard.localScale = Vector3.one;
        }
    }

    // 다시 하기 버튼 (Step 7 메뉴가 버튼 OnClick에 연결)
    public void OnRetryClicked()
    {
        if (GameManager.Instance != null) GameManager.Instance.Restart();
    }
}
