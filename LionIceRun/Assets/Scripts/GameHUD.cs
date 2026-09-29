using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 게임 화면 UI (Step 7). Fall Guys UI(gameuidatabase.com)의 구성을 따른다.
//  - 왼쪽 위: 화면 끝에 붙은 분홍 목표 띠 ("최대한 오래 버티세요!")
//  - 위 가운데: "남은 얼음" + 기울어진 분홍 배지 (값이 바뀌면 톡 튐)
//  - 오른쪽 위: 화면 끝에 붙은 남색 띠 (버틴 시간), 그 아래 최고 기록
//  - 오른쪽 아래: 키 칩 ([WASD] 이동 [Space] 점프 [Ctrl] 다이브)
//  - 시작 전 왼쪽 아래: 초록 제목 "얼음 깨기" + 설명 (움직이면 왼쪽으로 빠져나감)
//  - 게임 시작 / 게임 오버: 화면을 가로지르는 띠 배너 ("시작!" / "탈락!")
//  - 게임 오버 뒤: 결과 카드 (자홍 제목 상자에 버틴 시간, 새 기록이면 금색 띠, 다시 하기), R 키로도 재시작
public class GameHUD : MonoBehaviour
{
    public static GameHUD Instance { get; private set; }

    public IceGrid grid;

    [Header("안내 문구 (시련 예고, 파워업 획득)")]
    public RectTransform toast;
    public TMP_Text toastText;
    [Min(0.2f)] public float toastTime = 1.4f;

    [Header("파워업 상태")]
    public PlayerPowerUps playerPowerUps;
    public GameObject powerUpBadge;
    public TMP_Text powerUpText;

    [Header("위")]
    public TMP_Text iceCountText;
    public RectTransform iceBadge;
    public TMP_Text timerText;
    public TMP_Text bestText;

    [Header("대기 화면 타이틀")]
    public RectTransform title;

    [Header("게임 시작 때 튀어나오는 요소 (대기 중에는 숨김)")]
    [Tooltip("위에서 쑥 떨어지듯 나옴 (남은 얼음)")]
    public RectTransform[] popFromTop;
    [Tooltip("오른쪽에서 쑥 밀려 들어옴 (버틴 시간, 최고 기록)")]
    public RectTransform[] popFromRight;

    [Header("아래")]
    public RectTransform introGroup;
    public GameObject controlsGroup;

    [Header("띠 배너")]
    public RectTransform banner;
    public Image bannerBand;
    public TMP_Text bannerText;
    public Color startBandColor = new Color(0.55f, 0.85f, 1f);
    public Color gameOverBandColor = new Color(1f, 0.55f, 0.75f);
    [Min(0.1f)] public float startBannerTime = 0.9f;
    [Min(0.1f)] public float gameOverBannerTime = 1.3f;

    [Header("결과 화면")]
    public GameObject resultGroup;
    public RectTransform resultCard;
    public GameObject newRecordRibbon;
    public TMP_Text resultTimeText;
    public TMP_Text resultBestText;

    const float BannerTravel = 2800f;

    int lastIceCount = -1;
    Vector2 introBase;
    Vector2 titleBase;
    Coroutine toastRoutine;
    readonly System.Collections.Generic.Dictionary<RectTransform, Vector2> popBase = new System.Collections.Generic.Dictionary<RectTransform, Vector2>();

    void Awake()
    {
        Instance = this;
        if (title != null)
        {
            title.gameObject.SetActive(true);
            titleBase = title.anchoredPosition;
        }
        // 대기 중에는 숨겨 두고 게임 시작 때 튀어나오게
        foreach (RectTransform rect in PopTargets())
        {
            popBase[rect] = rect.anchoredPosition;
            rect.gameObject.SetActive(false);
        }
        if (toast != null) toast.gameObject.SetActive(false);
        if (resultGroup != null) resultGroup.SetActive(false);
        if (banner != null) banner.gameObject.SetActive(false);
        if (introGroup != null)
        {
            introGroup.gameObject.SetActive(true);
            introBase = introGroup.anchoredPosition;
        }
    }

    void Start()
    {
        if (GameManager.Instance != null) GameManager.Instance.StateChanged += OnStateChanged;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (GameManager.Instance != null) GameManager.Instance.StateChanged -= OnStateChanged;
    }

    // 화면 위쪽 가운데에 잠깐 뜨는 안내 (새 안내가 오면 바로 바뀜)
    public void ShowToast(string text, Color color)
    {
        if (toast == null || toastText == null) return;
        if (toastRoutine != null) StopCoroutine(toastRoutine);
        toastRoutine = StartCoroutine(ToastRoutine(text, color));
    }

    IEnumerator ToastRoutine(string text, Color color)
    {
        toastText.text = text;
        toastText.color = color;
        toast.gameObject.SetActive(true);
        var group = toast.GetComponent<CanvasGroup>();
        if (group != null) group.alpha = 1f;
        yield return Pop(toast, 1.3f);
        yield return new WaitForSecondsRealtime(toastTime);
        for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
        {
            if (group != null) group.alpha = 1f - t / 0.25f;
            yield return null;
        }
        toast.gameObject.SetActive(false);
        toastRoutine = null;
    }

    void UpdatePowerUpBadge()
    {
        if (powerUpBadge == null || powerUpText == null || playerPowerUps == null) return;
        string status = null;
        float jump = playerPowerUps.SuperJumpRemaining;
        if (playerPowerUps.HasShield && jump > 0f) status = $"방패 · 슈퍼 점프 {jump:0.0}";
        else if (playerPowerUps.HasShield) status = "방패";
        else if (jump > 0f) status = $"슈퍼 점프 {jump:0.0}";
        powerUpBadge.SetActive(status != null);
        if (status != null) powerUpText.text = status;
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
            if (alive != lastIceCount)
            {
                if (lastIceCount >= 0 && iceBadge != null) StartCoroutine(Pop(iceBadge, 1.25f));
                lastIceCount = alive;
                iceCountText.text = $"{alive} / {grid.IceBlocks.Count}";
            }
        }

        UpdatePowerUpBadge();

        // 대기 화면 타이틀이 살짝 둥실거림
        if (title != null && title.gameObject.activeSelf && GameManager.Instance != null
            && GameManager.Instance.State == GameManager.GameState.Ready)
        {
            float wave = Mathf.Sin(Time.unscaledTime * 1.6f);
            title.anchoredPosition = titleBase + Vector2.up * (wave * 10f);
            title.localScale = Vector3.one * (1f + wave * 0.015f);
        }

        GameManager manager = GameManager.Instance;
        if (manager != null && manager.State == GameManager.GameState.GameOver
            && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            manager.Restart();
    }

    void OnStateChanged(GameManager.GameState state)
    {
        if (state == GameManager.GameState.Playing)
        {
            if (introGroup != null) StartCoroutine(SlideOutIntro());
            if (title != null) StartCoroutine(HideTitle());
            StartCoroutine(PopInHud());
            StartCoroutine(PlayBanner("시작!", startBandColor, startBannerTime));
        }
        if (state == GameManager.GameState.GameOver)
        {
            if (controlsGroup != null) controlsGroup.SetActive(false);
            StartCoroutine(GameOverSequence());
        }
    }

    IEnumerator GameOverSequence()
    {
        yield return PlayBanner("탈락!", gameOverBandColor, gameOverBannerTime);
        yield return ShowResult();
    }

    // 띠가 왼쪽에서 휙 들어와 잠깐 멈췄다가 오른쪽으로 빠져나간다
    IEnumerator PlayBanner(string text, Color bandColor, float hold)
    {
        if (banner == null) yield break;
        bannerText.text = text;
        bannerBand.color = bandColor;
        banner.gameObject.SetActive(true);

        yield return Slide(banner, -BannerTravel, 0f, 0.28f, EaseOutBack);
        StartCoroutine(Pop(bannerText.rectTransform, 1.15f));
        yield return new WaitForSecondsRealtime(hold);
        yield return Slide(banner, 0f, BannerTravel, 0.22f, EaseIn);
        banner.gameObject.SetActive(false);
    }

    System.Collections.Generic.IEnumerable<RectTransform> PopTargets()
    {
        if (popFromTop != null) foreach (RectTransform r in popFromTop) if (r != null) yield return r;
        if (popFromRight != null) foreach (RectTransform r in popFromRight) if (r != null) yield return r;
    }

    // 타이틀: 톡 커지며 사라짐
    IEnumerator HideTitle()
    {
        var group = title.GetComponent<CanvasGroup>();
        Vector3 start = title.localScale;
        for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
        {
            float p = t / 0.3f;
            title.localScale = start * Mathf.Lerp(1f, 1.25f, p);
            if (group != null) group.alpha = 1f - p;
            yield return null;
        }
        title.gameObject.SetActive(false);
    }

    // 남은 얼음은 위에서, 버틴 시간·최고 기록은 오른쪽에서 차례로 쑥 튀어나옴
    IEnumerator PopInHud()
    {
        const float delayStep = 0.08f;
        float delay = 0.1f;
        if (popFromTop != null)
            foreach (RectTransform rect in popFromTop)
            {
                if (rect == null) continue;
                StartCoroutine(PopIn(rect, Vector2.up * 260f, delay));
                delay += delayStep;
            }
        delay = 0.15f;
        if (popFromRight != null)
            foreach (RectTransform rect in popFromRight)
            {
                if (rect == null) continue;
                StartCoroutine(PopIn(rect, Vector2.right * 700f, delay));
                delay += delayStep;
            }
        yield break;
    }

    IEnumerator PopIn(RectTransform rect, Vector2 offset, float delay)
    {
        Vector2 target = popBase.TryGetValue(rect, out Vector2 basePosition) ? basePosition : rect.anchoredPosition;
        rect.anchoredPosition = target + offset;
        yield return new WaitForSecondsRealtime(delay);
        rect.gameObject.SetActive(true);
        const float duration = 0.4f;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            rect.anchoredPosition = target + offset * (1f - EaseOutBack(t / duration));
            yield return null;
        }
        rect.anchoredPosition = target;
    }

    IEnumerator SlideOutIntro()
    {
        Vector2 start = introBase;
        for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
        {
            introGroup.anchoredPosition = start + Vector2.left * (EaseIn(t / 0.35f) * 1600f);
            yield return null;
        }
        introGroup.gameObject.SetActive(false);
    }

    IEnumerator ShowResult()
    {
        SurvivalTimer timer = SurvivalTimer.Instance;
        bool newRecord = timer != null && timer.IsNewRecord;
        if (timer != null)
        {
            if (resultTimeText != null) resultTimeText.text = $"{SurvivalTimer.Format(timer.Elapsed)} 버텼어요!";
            if (resultBestText != null) resultBestText.text = SurvivalTimer.Format(timer.BestTime);
        }
        if (newRecordRibbon != null) newRecordRibbon.SetActive(newRecord);
        resultGroup.SetActive(true);

        if (resultCard != null)
        {
            // 아래에서 톡 튀어 올라오기
            Vector2 target = resultCard.anchoredPosition;
            for (float t = 0f; t < 0.4f; t += Time.unscaledDeltaTime)
            {
                float p = EaseOutBack(t / 0.4f);
                resultCard.anchoredPosition = target + Vector2.down * ((1f - p) * 700f);
                yield return null;
            }
            resultCard.anchoredPosition = target;
        }
        if (newRecord && newRecordRibbon != null) StartCoroutine(Pop((RectTransform)newRecordRibbon.transform, 1.3f));
    }

    IEnumerator Slide(RectTransform rect, float fromX, float toX, float duration, System.Func<float, float> ease)
    {
        Vector2 p = rect.anchoredPosition;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            rect.anchoredPosition = new Vector2(Mathf.LerpUnclamped(fromX, toX, ease(t / duration)), p.y);
            yield return null;
        }
        rect.anchoredPosition = new Vector2(toX, p.y);
    }

    static IEnumerator Pop(RectTransform rect, float peak)
    {
        const float duration = 0.25f;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float p = t / duration;
            rect.localScale = Vector3.one * Mathf.Lerp(peak, 1f, p * p);
            yield return null;
        }
        rect.localScale = Vector3.one;
    }

    static float EaseIn(float t) => t * t * t;

    static float EaseOutBack(float t)
    {
        const float c = 1.70158f;
        t = Mathf.Clamp01(t) - 1f;
        return 1f + (c + 1f) * t * t * t + c * t * t;
    }

    // 다시 하기 버튼 (Step 7 메뉴가 버튼 OnClick에 연결)
    public void OnRetryClicked()
    {
        if (GameManager.Instance != null) GameManager.Instance.Restart();
    }
}
