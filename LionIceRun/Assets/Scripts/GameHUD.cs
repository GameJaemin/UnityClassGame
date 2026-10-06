using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 게임 화면 UI (Step 7). Fall Guys UI(gameuidatabase.com)의 구성을 따른다.
//  - 왼쪽 위: 화면 끝에 붙은 분홍 목표 띠 ("최후의 1인이 되세요!"), 그 아래 남색 띠 (맵 위 인원 / 전체)
//  - 위 가운데: "남은 얼음" + 기울어진 분홍 배지 (값이 바뀌면 톡 튐)
//  - 오른쪽 위: 화면 끝에 붙은 남색 띠 (경기 시간), 그 아래 우승 횟수
//  - 오른쪽 아래: 키 칩 ([WASD] 이동 [Space] 점프 [Ctrl] 다이브 [좌클릭] 밀치기)
//  - 시작 전 왼쪽 아래: 초록 제목 "얼음 깨기" + 설명 (움직이면 왼쪽으로 빠져나감)
//  - 화면을 가로지르는 띠 배너: 게임 시작 "시작!", 플레이어가 빠지면 "풍덩!", 경기 끝 "우승!" / "경기 끝!"
//  - 오른쪽: 플레이어 부활 카운트다운 + 죽은 NPC별 남은 시간. 플레이어 대기 중 게임 화면은 흑백
//  - 경기 끝 뒤: 결과 카드 (자홍 제목 상자에 순위, 1등이면 금색 띠, 우승 횟수, 다시 하기), R 키로도 재시작
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
    [Tooltip("우승 횟수")]
    public TMP_Text bestText;

    [Header("인원 · 다시 들어오기")]
    [Tooltip("맵 위에 남은 인원 / 전체 인원")]
    public TMP_Text playersText;
    public RectTransform playersBadge;
    [Tooltip("플레이어가 바다에 빠져 있는 동안 보이는 안내")]
    public GameObject respawnGroup;
    [Tooltip("남은 초 (큰 숫자)")]
    public TMP_Text respawnText;
    [Tooltip("남은 시간만큼 줄어드는 둥근 진행 막대 (Filled Radial)")]
    public Image respawnFill;
    public TMP_Text respawnCaption;
    [Tooltip("빠진 뒤 이 시간이 지나야 카운트다운이 나옴 (\"풍덩!\" 띠와 겹치지 않게)")]
    [Min(0f)] public float respawnShowDelay = 1.2f;

    [Header("오른쪽 NPC 부활 목록")]
    public GameObject npcRespawnGroup;
    public TMP_Text npcRespawnRowTemplate;
    readonly System.Collections.Generic.List<TMP_Text> npcRespawnRows = new System.Collections.Generic.List<TMP_Text>();
    Volume spectatorVolume;
    VolumeProfile spectatorProfile;
    UniversalAdditionalCameraData spectatorCamera;
    bool originalPostProcessing;
    LayerMask originalVolumeLayerMask;

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
    public Color winBandColor = new Color(1f, 0.82f, 0.3f);
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
    int lastOnMap = -1;
    int lastRespawnCount = -1;
    Vector2 introBase;
    Vector2 titleBase;
    Coroutine toastRoutine;
    Coroutine bannerRoutine;
    Coroutine respawnRoutine;
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
        if (npcRespawnGroup != null) npcRespawnGroup.SetActive(false);
        if (npcRespawnRowTemplate != null) npcRespawnRowTemplate.gameObject.SetActive(false);
        if (resultGroup != null) resultGroup.SetActive(false);
        if (banner != null) banner.gameObject.SetActive(false);
        if (respawnGroup != null)
        {
            // 튀어나올 때 돌아올 자리
            popBase[(RectTransform)respawnGroup.transform] = ((RectTransform)respawnGroup.transform).anchoredPosition;
            respawnGroup.SetActive(false);
        }
        if (introGroup != null)
        {
            introGroup.gameObject.SetActive(true);
            introBase = introGroup.anchoredPosition;
        }
    }

    void Start()
    {
        GameManager manager = GameManager.Instance;
        if (manager == null) return;
        manager.StateChanged += OnStateChanged;
        manager.Fell += OnFell;
        manager.Respawned += OnRespawned;
    }

    void OnDestroy()
    {
        ReleaseSpectatorEffect();
        if (Instance == this) Instance = null;
        GameManager manager = GameManager.Instance;
        if (manager == null) return;
        manager.StateChanged -= OnStateChanged;
        manager.Fell -= OnFell;
        manager.Respawned -= OnRespawned;
    }

    void OnFell(PlayerController body)
    {
        // 중앙을 가리는 사망 띠 대신 오른쪽 부활 카드에서 알린다.
        if (body == GameManager.Instance.player) UpdateSpectatorEffect(true);
    }

    public void ResetOnlineHud(PlayerPowerUps powerUps)
    {
        StopAllCoroutines();
        toastRoutine = bannerRoutine = respawnRoutine = null;
        playerPowerUps = powerUps;
        lastIceCount = lastOnMap = lastRespawnCount = -1;
        if (resultGroup != null) resultGroup.SetActive(false);
        if (banner != null) banner.gameObject.SetActive(false);
        if (toast != null) toast.gameObject.SetActive(false);
        if (title != null) title.gameObject.SetActive(false);
        if (introGroup != null) introGroup.gameObject.SetActive(false);
        if (respawnGroup != null) respawnGroup.SetActive(false);
        if (npcRespawnGroup != null) npcRespawnGroup.SetActive(false);
        if (controlsGroup != null) controlsGroup.SetActive(true);
        foreach (RectTransform rect in PopTargets())
        {
            if (popBase.TryGetValue(rect, out Vector2 position)) rect.anchoredPosition = position;
            rect.gameObject.SetActive(true);
        }
        ReleaseSpectatorEffect();
    }

    void OnRespawned(PlayerController body)
    {
        if (body == GameManager.Instance.player)
        {
            UpdateSpectatorEffect(false);
            ShowToast("다시 들어가요!", startBandColor);
        }
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
        GameManager manager = GameManager.Instance;
        SurvivalTimer timer = SurvivalTimer.Instance;
        if (timer != null && timerText != null) timerText.text = SurvivalTimer.Format(timer.Elapsed);
        if (manager != null)
        {
            if (bestText != null) bestText.text = $"우승  {manager.Wins}회";
            UpdatePlayers(manager);
            UpdateRespawn(manager);
            UpdateNpcRespawns(manager);
            UpdateSpectatorEffect(manager.PlayerOut);
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
        if (title != null && title.gameObject.activeSelf && manager != null
            && manager.State == GameManager.GameState.Ready)
        {
            float wave = Mathf.Sin(Time.unscaledTime * 1.6f);
            title.anchoredPosition = titleBase + Vector2.up * (wave * 10f);
            title.localScale = Vector3.one * (1f + wave * 0.015f);
        }

        if (manager != null && manager.State == GameManager.GameState.GameOver
            && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            manager.Restart();
    }

    // 맵 위 인원 / 전체 (바뀌면 톡 튐)
    void UpdatePlayers(GameManager manager)
    {
        if (playersText == null || manager.CompetitorCount == 0) return;
        int onMap = manager.OnMapCount;
        if (onMap == lastOnMap) return;
        if (lastOnMap >= 0 && playersBadge != null && playersBadge.gameObject.activeInHierarchy) StartCoroutine(Pop(playersBadge, 1.2f));
        lastOnMap = onMap;
        playersText.text = $"{onMap} / {manager.CompetitorCount}";
    }

    // 오른쪽의 작은 카드: 다시 들어가기까지 남은 초
    void UpdateRespawn(GameManager manager)
    {
        if (respawnGroup == null) return;
        float remaining = manager.PlayerRespawnRemaining;
        float total = Mathf.Max(0.01f, manager.respawnDelay);
        bool show = manager.State == GameManager.GameState.Playing && manager.PlayerOut
            && total - remaining >= respawnShowDelay;
        if (respawnGroup.activeSelf != show)
        {
            if (respawnRoutine != null) StopCoroutine(respawnRoutine);
            respawnGroup.SetActive(show);
            lastRespawnCount = -1;
            if (show) respawnRoutine = StartCoroutine(PopIn((RectTransform)respawnGroup.transform, Vector2.right * 380f, 0f));
        }
        if (!show) return;

        int count = Mathf.CeilToInt(remaining);
        bool waiting = manager.PlayerWaitingForSpace;
        if (respawnFill != null) respawnFill.fillAmount = waiting ? 0f : Mathf.Clamp01(remaining / total);
        if (respawnCaption != null) respawnCaption.text = waiting || count == 0 ? "들어갈 자리를 찾는 중..." : "하늘에서 다시 들어와요";
        if (waiting) count = 0;
        if (count == lastRespawnCount) return;
        if (respawnText != null)
        {
            respawnText.text = count.ToString();
            // 마지막 3초는 노랗게
            respawnText.color = count <= 3 ? new Color(1f, 0.86f, 0.25f) : Color.white;
            if (lastRespawnCount >= 0) StartCoroutine(Pop(respawnText.rectTransform, 1.35f));
        }
        lastRespawnCount = count;
    }

    void UpdateNpcRespawns(GameManager manager)
    {
        if (npcRespawnGroup == null || npcRespawnRowTemplate == null) return;
        int visible = 0;
        for (int i = 0; i < manager.CompetitorCount; i++)
        {
            if (!manager.TryGetRespawnStatus(i, out PlayerController body, out float remaining, out bool waiting)
                || body == manager.player) continue;
            if (visible == npcRespawnRows.Count)
            {
                TMP_Text row = Instantiate(npcRespawnRowTemplate, npcRespawnRowTemplate.transform.parent);
                row.name = $"NpcRespawnRow{visible + 1}";
                npcRespawnRows.Add(row);
            }
            TMP_Text text = npcRespawnRows[visible];
            text.gameObject.SetActive(true);
            string label = body.name.StartsWith("Bot") ? "NPC " + body.name.Substring(3) : body.name;
            BotBrain brain = body.GetComponent<BotBrain>();
            if (brain != null) label += brain.behaviorType == BotBrain.BehaviorType.Aggressive ? " (공격)" : " (신중)";
            text.text = remaining < 0f ? $"{label}  ·  기권" : waiting ? $"{label}  ·  자리 대기" : $"{label}  ·  {remaining:0.0}초";
            text.color = !waiting && remaining <= 3f ? new Color(1f, 0.86f, 0.25f) : Color.white;
            text.rectTransform.anchoredPosition = new Vector2(18f, -50f - visible * 28f);
            visible++;
        }
        for (int i = visible; i < npcRespawnRows.Count; i++) npcRespawnRows[i].gameObject.SetActive(false);
        npcRespawnGroup.SetActive(visible > 0);
        var rect = (RectTransform)npcRespawnGroup.transform;
        rect.anchoredPosition = new Vector2(0f, respawnGroup != null && respawnGroup.activeSelf ? -230f : 0f);
        rect.sizeDelta = new Vector2(340f, 60f + visible * 28f);
    }

    // 별도 런타임 볼륨으로 게임 화면만 흑백 처리한다. 원래 볼륨 에셋과 Overlay UI는 유지한다.
    void UpdateSpectatorEffect(bool outOfMap)
    {
        if (outOfMap && spectatorVolume == null)
        {
            Camera camera = Camera.main;
            if (camera == null) return;
            spectatorCamera = camera.GetUniversalAdditionalCameraData();
            originalPostProcessing = spectatorCamera.renderPostProcessing;
            originalVolumeLayerMask = spectatorCamera.volumeLayerMask;
            var go = new GameObject("SpectatorGrayscale");
            // 카메라가 원래 읽던 볼륨 레이어를 사용한다.
            int mask = spectatorCamera.volumeLayerMask.value;
            if (mask == 0) mask = 1;
            for (int layer = 0; layer < 32; layer++)
                if ((mask & (1 << layer)) != 0) { go.layer = layer; break; }
            spectatorVolume = go.AddComponent<Volume>();
            spectatorVolume.isGlobal = true;
            spectatorVolume.priority = float.MaxValue;
            spectatorVolume.weight = 0f;
            spectatorProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            spectatorProfile.Add<ColorAdjustments>().saturation.Override(-100f);
            spectatorVolume.sharedProfile = spectatorProfile;
        }
        if (spectatorVolume == null) return;
        if (outOfMap)
        {
            spectatorCamera.renderPostProcessing = true;
            if (originalVolumeLayerMask.value == 0) spectatorCamera.volumeLayerMask = 1;
        }
        spectatorVolume.weight = Mathf.MoveTowards(spectatorVolume.weight, outOfMap ? 1f : 0f, Time.unscaledDeltaTime / 0.25f);
        if (!outOfMap && spectatorVolume.weight == 0f && spectatorCamera != null)
        {
            spectatorCamera.renderPostProcessing = originalPostProcessing;
            spectatorCamera.volumeLayerMask = originalVolumeLayerMask;
        }
    }

    void OnDisable() => ReleaseSpectatorEffect();

    void ReleaseSpectatorEffect()
    {
        if (spectatorCamera != null)
        {
            spectatorCamera.renderPostProcessing = originalPostProcessing;
            spectatorCamera.volumeLayerMask = originalVolumeLayerMask;
        }
        if (spectatorVolume != null) Destroy(spectatorVolume.gameObject);
        if (spectatorProfile != null)
        {
            foreach (VolumeComponent component in spectatorProfile.components) Destroy(component);
            Destroy(spectatorProfile);
        }
        spectatorVolume = null;
        spectatorProfile = null;
        spectatorCamera = null;
    }

    void OnStateChanged(GameManager.GameState state)
    {
        if (state == GameManager.GameState.Playing)
        {
            if (introGroup != null) StartCoroutine(SlideOutIntro());
            if (title != null) StartCoroutine(HideTitle());
            StartCoroutine(PopInHud());
            ShowBanner("시작!", startBandColor, startBannerTime);
        }
        if (state == GameManager.GameState.GameOver)
        {
            if (respawnRoutine != null) StopCoroutine(respawnRoutine);
            if (controlsGroup != null) controlsGroup.SetActive(false);
            if (respawnGroup != null) respawnGroup.SetActive(false);
            if (npcRespawnGroup != null) npcRespawnGroup.SetActive(false);
            StartCoroutine(GameOverSequence());
        }
    }

    IEnumerator GameOverSequence()
    {
        bool won = GameManager.Instance != null && GameManager.Instance.PlayerWon;
        // "풍덩!" 띠가 지나가는 중이어도 바로 경기 끝 띠로 바꾼다
        if (bannerRoutine != null) StopCoroutine(bannerRoutine);
        bannerRoutine = StartCoroutine(PlayBanner(won ? "우승!" : "경기 끝!", won ? winBandColor : gameOverBandColor, gameOverBannerTime));
        yield return bannerRoutine;
        yield return ShowResult();
    }

    void ShowBanner(string text, Color bandColor, float hold)
    {
        if (bannerRoutine != null) StopCoroutine(bannerRoutine);
        bannerRoutine = StartCoroutine(PlayBanner(text, bandColor, hold));
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
        GameManager manager = GameManager.Instance;
        bool won = manager != null && manager.PlayerWon;
        if (manager != null)
        {
            if (resultTimeText != null) resultTimeText.text = won ? "1등!" : $"{manager.PlayerPlace}등";
            if (resultBestText != null) resultBestText.text = $"{manager.Wins}회";
        }
        if (newRecordRibbon != null)
        {
            newRecordRibbon.SetActive(won);
            TMP_Text ribbonText = newRecordRibbon.GetComponentInChildren<TMP_Text>(true);
            if (ribbonText != null) ribbonText.text = "우승!";
        }
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
        if (won && newRecordRibbon != null) StartCoroutine(Pop((RectTransform)newRecordRibbon.transform, 1.3f));
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
