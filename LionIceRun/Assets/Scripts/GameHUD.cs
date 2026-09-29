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
    public IceGrid grid;

    [Header("위")]
    public TMP_Text iceCountText;
    public RectTransform iceBadge;
    public TMP_Text timerText;
    public TMP_Text bestText;

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

    void Awake()
    {
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
            if (alive != lastIceCount)
            {
                if (lastIceCount >= 0 && iceBadge != null) StartCoroutine(Pop(iceBadge, 1.25f));
                lastIceCount = alive;
                iceCountText.text = $"{alive} / {grid.IceBlocks.Count}";
            }
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
