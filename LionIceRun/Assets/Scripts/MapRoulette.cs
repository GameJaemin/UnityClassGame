using UnityEngine;
using UnityEngine.SceneManagement;

// JSON 맵의 사전 촬영 카드를 왼쪽에서 오른쪽으로 흘려 선택하는 시작 화면.
// 선택 후 같은 씬을 다시 로드하여 IceGrid·플레이어·봇이 처음부터 선택한 맵으로 생성된다.
[DefaultExecutionOrder(-500)]
public class MapRoulette : MonoBehaviour
{
    public GameManager manager;
    public Canvas selectionCanvas;
    public Canvas gameCanvas;
    public GameObject titleGroup;
    public GameObject rouletteGroup;
    public UnityEngine.UI.Button startButton;
    public GameObject titleDecorations;
    public RectTransform titleLogo;
    Vector2 logoBase;
    public RectTransform[] cards;
    public UnityEngine.UI.Image[] previews;
    public TMPro.TMP_Text[] names;
    public TMPro.TMP_Text status;
    public TMPro.TMP_Text buttonText;
    public UnityEngine.UI.Button stopButton;
    [Min(0.1f)] public float scrollSpeed = 2300f;
    [Min(0.5f)] public float stopDuration = 3.5f;
    [Min(100f)] public float cardSpacing = 440f;

    public static bool SkipNextSelection;
    public static bool AutoStartSolo;
    static string pendingMap;
    readonly System.Collections.Generic.List<string> files = new System.Collections.Generic.List<string>();
    readonly System.Collections.Generic.List<string> mapNames = new System.Collections.Generic.List<string>();
    readonly System.Collections.Generic.List<Sprite> screenshots = new System.Collections.Generic.List<Sprite>();
    bool countdown;
    bool choosing;
    bool stopping;
    bool completed;
    double position;
    double stopFrom;
    double stopTo;
    float stopTime;
    float brakingDuration;
    float frozenTimeScale;
    bool ownsPause;

    void Awake()
    {
        bool skip = SkipNextSelection;
#if UNITY_EDITOR
        skip |= UnityEditor.SessionState.GetBool("LionIceRun.MapToolPlay", false);
        UnityEditor.SessionState.EraseBool("LionIceRun.MapToolPlay");
#endif
        if (skip)
        {
            SkipNextSelection = false;
            if (selectionCanvas != null) selectionCanvas.gameObject.SetActive(false);
            if (titleDecorations != null) titleDecorations.SetActive(false);
            enabled = false;
            return;
        }
        if (manager == null || manager.grid == null || manager.player == null || selectionCanvas == null
            || titleGroup == null || rouletteGroup == null || startButton == null
            || stopButton == null || status == null || buttonText == null || cards == null || cards.Length < 3
            || previews == null || previews.Length != cards.Length || names == null || names.Length != cards.Length)
        {
            if (selectionCanvas != null) selectionCanvas.gameObject.SetActive(false);
            enabled = false;
            return;
        }
        if (pendingMap != null)
        {
            manager.grid.mapFile = pendingMap;
            pendingMap = null;
            countdown = true;
        }
        else
        {
            frozenTimeScale = Time.timeScale;
            // 타이틀에서는 바다와 유빙을 움직이되, Ready 상태와 입력 차단으로 경기는 시작하지 않는다.
        }
        manager.player.InputEnabled = false;
    }

    System.Collections.IEnumerator Start()
    {
        if (gameCanvas != null) gameCanvas.enabled = false;
        selectionCanvas.gameObject.SetActive(true);
        titleGroup.SetActive(!countdown);
        rouletteGroup.SetActive(countdown);
        if (titleDecorations != null) titleDecorations.SetActive(!countdown);
        if (titleLogo != null) logoBase = titleLogo.anchoredPosition;
        if (OnlineGameSession.Instance != null && OnlineGameSession.Instance.isActiveAndEnabled)
            startButton.onClick.AddListener(OnlineGameSession.Instance.frontend.OpenModes);
        else startButton.onClick.AddListener(BeginChoosing);
        stopButton.onClick.AddListener(StopChoosing);
        if (countdown)
        {
            // 맵 선택 화면의 배경·제목·카드를 모두 숨기고 실제 맵 위에서 준비한다.
            selectionCanvas.gameObject.SetActive(false);
            if (gameCanvas != null) gameCanvas.enabled = true;
            GameHUD hud = gameCanvas != null ? gameCanvas.GetComponent<GameHUD>() : null;
            if (hud != null)
            {
                if (hud.title != null) hud.title.gameObject.SetActive(false);
                if (hud.introGroup != null) hud.introGroup.gameObject.SetActive(false);
            }
            MapData map = manager.grid.Map;
            for (int i = 3; i > 0; i--)
            {
                if (MatchCountdown.Instance != null) MatchCountdown.Instance.Show(i);
                else if (hud != null) hud.ShowToast($"{(map != null ? map.name : "경기 준비")} · {i}", new Color(1f, 0.86f, 0.25f));
                yield return new WaitForSecondsRealtime(1f);
            }
            if (gameCanvas != null) gameCanvas.enabled = true;
            selectionCanvas.gameObject.SetActive(false);
            manager.BeginSelectedMatch();
            MatchCountdown.Instance?.Hide();
            completed = true;
            yield break;
        }
        foreach (string file in MapData.ListFiles())
        {
            MapData map = MapData.Load(file);
            if (map == null || map.IceCount == 0) continue;
            files.Add(file);
            mapNames.Add(string.IsNullOrWhiteSpace(map.name) ? System.IO.Path.GetFileNameWithoutExtension(file) : map.name);
            screenshots.Add(Resources.Load<Sprite>("MapPreviews/" + System.IO.Path.GetFileNameWithoutExtension(file)));
        }
        if (files.Count == 0)
        {
            yield return new WaitUntil(() => choosing);
            status.text = "저장된 맵이 없어요\n현재 맵으로 시작합니다";
            stopButton.gameObject.SetActive(false);
            yield return new WaitForSecondsRealtime(1.5f);
            ReleasePause();
            if (gameCanvas != null) gameCanvas.enabled = true;
            selectionCanvas.gameObject.SetActive(false);
            manager.BeginSelectedMatch();
            completed = true;
            yield break;
        }
        // 파일 순서를 섞고 시작 지점도 무작위로 하여 매 경기 다른 조합으로 보인다.
        for (int i = files.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (files[i], files[j]) = (files[j], files[i]);
            (mapNames[i], mapNames[j]) = (mapNames[j], mapNames[i]);
            (screenshots[i], screenshots[j]) = (screenshots[j], screenshots[i]);
        }
        position = UnityEngine.Random.Range(0, files.Count) * cardSpacing;
        status.text = "어떤 맵에서 겨룰까요?";
        RenderCards();
        // 타이틀에서 기다리는 동안 카드 준비만 마치고, 시작하기 후 이동을 시작한다.
        if (AutoStartSolo) { AutoStartSolo = false; BeginChoosing(); }
        yield return new WaitUntil(() => choosing);
    }

    void Update()
    {
        if (OnlineGameSession.InRoom) return;
        if (!choosing && !countdown && titleLogo != null)
        {
            float wave = Mathf.Sin(Time.unscaledTime * 1.5f);
            titleLogo.anchoredPosition = logoBase + Vector2.up * (wave * 10f);
            titleLogo.localScale = Vector3.one * (1f + wave * 0.012f);
        }
        if (!choosing || completed || countdown || files.Count == 0) return;
        if (!stopping)
        {
            position += scrollSpeed * Time.unscaledDeltaTime;
        }
        else
        {
            float t = Mathf.Clamp01((Time.unscaledTime - stopTime) / brakingDuration);
            position = MapRouletteMotion.Position(stopFrom, stopTo, t);
            if (t >= 1f && !completed)
            {
                completed = true;
                StartCoroutine(ConfirmSelection());
            }
        }
        RenderCards();
    }

    public void BeginChoosing()
    {
        if (choosing || countdown || completed) return;
        startButton.interactable = false;
        ownsPause = true;
        Time.timeScale = 0f;
        titleGroup.SetActive(false);
        if (titleDecorations != null) titleDecorations.SetActive(false);
        rouletteGroup.SetActive(true);
        choosing = true;
    }

    public void StopChoosing()
    {
        if (OnlineGameSession.InRoom) { OnlineGameSession.Instance.StopRoll(); return; }
        if (!choosing || stopping || completed || files.Count == 0) return;
        stopping = true;
        stopButton.interactable = false;
        buttonText.text = "선택 중…";
        status.text = "점점 느려져요…";
        stopFrom = position;
        // 감속 시작 속도와 같은 초기 기울기를 갖고, 카드 중심에서 속도 0으로 멈춘다.
        stopTo = System.Math.Ceiling((position + scrollSpeed * stopDuration / 3f) / cardSpacing) * cardSpacing;
        brakingDuration = (float)((stopTo - stopFrom) * 3 / scrollSpeed);
        stopTime = Time.unscaledTime;
    }

    void RenderCards()
    {
        int center = (int)System.Math.Floor(position / cardSpacing);
        double remainder = position - center * cardSpacing;
        for (int i = 0; i < cards.Length; i++)
        {
            int slot = i - cards.Length / 2;
            int sequence = center - slot;
            // 이동량이 양수일 때 카드가 왼쪽에서 오른쪽으로 흐른다.
            int index = MapRouletteMotion.CardIndex(sequence, files.Count);
            cards[i].anchoredPosition = new Vector2((float)(slot * cardSpacing + remainder), 0f);
            previews[i].sprite = screenshots[index];
            previews[i].color = screenshots[index] != null ? Color.white : new Color(0.3f, 0.5f, 0.7f);
            names[i].text = mapNames[index];
            cards[i].localScale = Vector3.one * Mathf.Lerp(1f, 0.9f, Mathf.Clamp01(Mathf.Abs(cards[i].anchoredPosition.x) / cardSpacing));
        }
    }

    public void ShowOnlineRoll(string[] candidates, double atPosition, bool canStop, string caption)
    {
        StopAllCoroutines();
        selectionCanvas.gameObject.SetActive(true);
        if (gameCanvas != null) gameCanvas.enabled = false;
        titleGroup.SetActive(false);
        if (titleDecorations != null) titleDecorations.SetActive(false);
        rouletteGroup.SetActive(true);
        files.Clear(); mapNames.Clear(); screenshots.Clear();
        foreach (string file in candidates)
        {
            MapData map = MapData.Load(file);
            files.Add(file);
            mapNames.Add(map != null ? map.name : file);
            screenshots.Add(Resources.Load<Sprite>("MapPreviews/" + System.IO.Path.GetFileNameWithoutExtension(file)));
        }
        position = atPosition;
        status.text = caption;
        stopButton.interactable = canStop;
        buttonText.text = canStop ? "멈추기" : "방장이 선택 중";
        RenderCards();
    }

    public void RenderOnlinePosition(double atPosition, bool canStop, string caption)
    {
        position = atPosition;
        status.text = caption;
        stopButton.interactable = canStop;
        buttonText.text = canStop ? "멈추기" : "선택 중…";
        if (files.Count > 0) RenderCards();
    }

    System.Collections.IEnumerator ConfirmSelection()
    {
        int sequence = (int)System.Math.Round(position / cardSpacing);
        int index = MapRouletteMotion.CardIndex(sequence, files.Count);
        status.text = $"{mapNames[index]} 선택!";
        buttonText.text = "선택 완료!";
        yield return new WaitForSecondsRealtime(1f);
        pendingMap = files[index];
        ReleasePause();
        if (OnlineGameSession.Instance != null && OnlineGameSession.Instance.isActiveAndEnabled) OnlineGameSession.Instance.ReloadSoloScene();
        else SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void ReleasePause()
    {
        if (!ownsPause) return;
        Time.timeScale = frozenTimeScale > 0f ? frozenTimeScale : 1f;
        ownsPause = false;
    }

    void OnDestroy() => ReleasePause();
}

// 화면 프레임 수와 관계없이 진행하는 감속 곡선. 종료 위치는 중앙 카드의 정수 칸이다.
internal static class MapRouletteMotion
{
    internal static int CardIndex(int sequence, int count) => count > 0 ? ((-sequence % count) + count) % count : -1;

    internal static double Position(double from, double to, double t)
    {
        t = System.Math.Max(0, System.Math.Min(1, t));
        return from + (to - from) * (1 - System.Math.Pow(1 - t, 3));
    }
}
