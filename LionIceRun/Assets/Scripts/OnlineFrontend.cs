using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 기본 배치는 에디터에 저장하고 결과 안내/우측 상단 나가기는 화면 비율에 맞춰 유지한다.
public class OnlineFrontend : MonoBehaviour
{
    public OnlineGameSession session;
    public GameObject modesPanel, lobbyPanel, loadingPanel, resultsPanel, leavePanel;
    public TMP_InputField nickname, joinCode;
    public TMP_Dropdown skin, capacity, aggressive;
    public Toggle fillBots;
    public Button soloButton, createButton, joinButton, backButton, readyButton, startButton, copyButton, leaveButton, returnButton, resultLeaveButton;
    public TMP_Text notice, lobbyNotice, roomCode, readyText, countdownText, resultText;
    public TMP_Text[] slots;
    public TMP_Text[] slotSkins;
    public Button matchLeaveButton;
    public CharacterSelection characterSelection;
    public Image[] slotPortraits;
    public Sprite[] characterPortraits;
    public TMP_Text resultsTitle, resultsHint, seriesStatus;
    int SelectedSkin => characterSelection != null && characterSelection.IsConfigured ? characterSelection.Index : skin.value;
    bool ignoreOptions;

    void Start()
    {
        // 씬 재로드를 끈 에디터에서도 최신 설정 UI를 연결한다.
        if(GetComponent<IceExperienceUI>()==null) gameObject.AddComponent<IceExperienceUI>().front=this;
        // 에디터에서 저장한 UI 미리보기를 닫고 정상 타이틀 흐름으로 시작한다.
        HidePanels();
        if (resultsTitle == null) resultsTitle = resultsPanel.transform.Find("Title")?.GetComponent<TMP_Text>();
        if (resultsHint == null) resultsHint = resultsPanel.transform.Find("Hint")?.GetComponent<TMP_Text>();
        SeriesResultsLayout.Apply(this, false);
        nickname.text = GamePreferences.Nickname;
        if (nickname.placeholder is TMP_Text placeholder) placeholder.text = "비워두면 동물 이름으로 시작해요";
        BindActions();
    }

    void OnEnable() { if (Application.isPlaying) BindActions(); }
    void BindActions()
    {
        if (session == null) return;
        Bind(soloButton, StartSolo); Bind(createButton, CreateRoom); Bind(joinButton, JoinRoom);
        Bind(backButton, BackToTitle); Bind(readyButton, session.ToggleReady); Bind(startButton, session.StartRound);
        Bind(copyButton, CopyCode); Bind(leaveButton, session.LeaveRoom);
        Bind(returnButton, ReturnFromResults); Bind(resultLeaveButton, LeaveMatch); Bind(matchLeaveButton, LeaveMatch);
        if (fillBots != null) { fillBots.onValueChanged.RemoveListener(OnFillBots); fillBots.onValueChanged.AddListener(OnFillBots); }
        if (aggressive != null) { aggressive.onValueChanged.RemoveListener(OnAggressive); aggressive.onValueChanged.AddListener(OnAggressive); }
    }
    static void Bind(Button button, UnityEngine.Events.UnityAction action)
    { if (button != null) { button.onClick.RemoveListener(action); button.onClick.AddListener(action); } }
    void StartSolo() { SaveName(); session.StartSolo(); }
    void CreateRoom() { SaveName(); session.CreateRoom(GamePreferences.PublicName(SelectedSkin), SelectedSkin, capacity.value + 2); }
    void JoinRoom() { SaveName(); session.JoinRoom(joinCode.text, GamePreferences.PublicName(SelectedSkin), SelectedSkin); }
    void BackToTitle() { HidePanels(); session.roulette.ShowTitle(); }
    void CopyCode() => GUIUtility.systemCopyBuffer = session.RoomCode;
    void ReturnFromResults() { if (SoloGameSeries.Active) SoloGameSeries.ReturnToTitle(); else session.ReturnToLobby(); }
    void LeaveMatch() { if (SoloGameSeries.Active) SoloGameSeries.ReturnToTitle(); else session.LeaveRoom(); }
    void OnFillBots(bool value) { if (!ignoreOptions) session.SetBotOptions(fillBots.isOn, aggressive.value); }
    void OnAggressive(int value) { if (!ignoreOptions) session.SetBotOptions(fillBots.isOn, aggressive.value); }

    void SaveName()
    {
        nickname.text = OnlineRules.OptionalNickname(nickname.text);
        PlayerPrefs.SetString("LionIceRun.Nickname", nickname.text);
        PlayerPrefs.SetInt("LionIceRun.Skin", SelectedSkin);
        PlayerPrefs.Save();
        GamePreferences.Apply();
    }

    public void HidePanels()
    {
        MatchCountdown.Instance?.Hide();
        modesPanel.SetActive(false); lobbyPanel.SetActive(false); loadingPanel.SetActive(false);
        resultsPanel.SetActive(false); leavePanel.SetActive(false);
        if (seriesStatus != null) seriesStatus.gameObject.SetActive(false);
    }
    public void OpenModes()
    {
        HidePanels(); modesPanel.SetActive(true);
        characterSelection?.BindButtons();
        if (skin != null) skin.gameObject.SetActive(characterSelection == null || !characterSelection.IsConfigured);
        session.roulette.titleGroup.SetActive(false);
    }
    public void ShowLobby() { HidePanels(); lobbyPanel.SetActive(true); }
    public void ShowLoading() { HidePanels(); loadingPanel.SetActive(true); countdownText.text = "모두의 맵을 준비하고 있어요…"; }
    public void ShowCountdown(int remaining)
    {
        if (MatchCountdown.Instance != null)
        { loadingPanel.SetActive(false); MatchCountdown.Instance.Show(remaining); return; }
        loadingPanel.SetActive(true);
        countdownText.text = remaining > 0 ? remaining.ToString() : "시작!";
    }

    void Update()
    {
        if (session == null || !session.isActiveAndEnabled) return;
        bool busy = session.Busy;
        createButton.interactable = joinButton.interactable = soloButton.interactable = backButton.interactable = !busy;
        nickname.interactable = skin.interactable = capacity.interactable = joinCode.interactable = !busy;
        if (characterSelection != null) characterSelection.SetInteractable(!busy);
        notice.text = session.Notice;
        if (SoloGameSeries.Active)
        {
            UpdateSoloSeries(); return;
        }
        var phase = session.CurrentPhase;
        if (seriesStatus != null)
        {
            seriesStatus.gameObject.SetActive(phase == OnlineGameSession.Phase.Playing || phase == OnlineGameSession.Phase.Countdown);
            seriesStatus.text = $"{session.SeriesRound} / 5 라운드 · 총 {session.LocalTotal}점";
        }
        leavePanel.SetActive(phase == OnlineGameSession.Phase.Playing || phase == OnlineGameSession.Phase.Countdown);
        if (phase == OnlineGameSession.Phase.Lobby && !lobbyPanel.activeSelf) ShowLobby();
        if (phase == OnlineGameSession.Phase.Lobby)
        {
            roomCode.text = "방 코드  " + session.RoomCode;
            bool host = session.IsHost;
            startButton.gameObject.SetActive(host); startButton.interactable = session.CanStart;
            readyButton.interactable = !busy; copyButton.interactable = session.RoomCode.Length > 0;
            leaveButton.interactable = !busy;
            fillBots.interactable = aggressive.interactable = host && !busy;
            ignoreOptions = true; fillBots.isOn = session.FillBots; aggressive.value = session.AggressiveBots; ignoreOptions = false;
            for (int i = 0; i < slots.Length; i++)
            {
                bool occupied = i < session.Members.Count;
                var member = occupied ? session.Members[i] : null;
                slots[i].text = occupied ? member.nickname + (member.clientId == 0 ? " · 방장" : "") + "\n" + (member.ready ? "준비 완료" : "준비 중") : "친구를 기다려요";
                slots[i].color = occupied && member.ready ? new Color(0.5f, 1f, 0.7f) : Color.white;
                slotSkins[i].text = occupied ? new[] { "사자", "북극곰", "호랑이" }[Mathf.Clamp(member.skin, 0, 2)] : "방 코드를 공유하세요";
                if (slotPortraits != null && i < slotPortraits.Length && slotPortraits[i] != null)
                {
                    slotPortraits[i].enabled = occupied;
                    if (occupied && characterPortraits != null && characterPortraits.Length == 3)
                        slotPortraits[i].sprite = characterPortraits[Mathf.Clamp(member.skin, 0, 2)];
                }
            }
            bool localReady = false, allReady = session.Members.Count >= 2;
            foreach (var member in session.Members)
            {
                if (member.clientId == session.network.LocalClientId) localReady = member.ready;
                allReady &= member.ready;
            }
            readyText.text = localReady ? "준비 취소" : "준비하기";
            lobbyNotice.text = session.CommonFiles.Count == 0 ? "같은 맵이 없어요. 게임 버전을 맞춰 주세요."
                : session.Members.Count < 2 ? "친구가 참가하면 경기를 시작할 수 있어요."
                : allReady ? (host ? "모두 준비됐어요! 경기를 시작하세요." : "모두 준비됐어요. 방장의 시작을 기다려요.") : "모두 준비하기를 눌러 주세요.";
        }
        if (phase == OnlineGameSession.Phase.Results)
        {
            if (!resultsPanel.activeSelf) { HidePanels(); resultsPanel.SetActive(true); }
            if (GameHUD.Instance != null && GameHUD.Instance.resultGroup != null) GameHUD.Instance.resultGroup.SetActive(false);
            resultText.text = session.ResultLines;
            if (resultsTitle != null) resultsTitle.text = session.ResultsTitle;
            if (resultsHint != null) resultsHint.text = session.ResultsHint;
            returnButton.gameObject.SetActive(session.SeriesComplete && session.IsHost);
            returnButton.interactable = session.IsHost && !busy;
            resultLeaveButton.interactable = !busy;
        }
    }

    void UpdateSoloSeries()
    {
        var manager = session.manager;
        bool playing = manager.State == GameManager.GameState.Playing;
        bool results = manager.State == GameManager.GameState.GameOver;
        if (seriesStatus != null)
        {
            seriesStatus.gameObject.SetActive(!results && session.roulette.gameCanvas.enabled);
            seriesStatus.text = $"{SoloGameSeries.Round} / 5 라운드 · 총 {SoloGameSeries.LocalTotal}점";
        }
        leavePanel.SetActive(playing);
        matchLeaveButton.GetComponentInChildren<TMP_Text>().text = "타이틀로";
        if (!results) return;
        if (!resultsPanel.activeSelf) { HidePanels(); resultsPanel.SetActive(true); }
        if (GameHUD.Instance != null && GameHUD.Instance.resultGroup != null) GameHUD.Instance.resultGroup.SetActive(false);
        resultText.text = SoloGameSeries.ResultLines;
        if (resultsTitle != null) resultsTitle.text = SoloGameSeries.Title;
        if (resultsHint != null) resultsHint.text = SoloGameSeries.Hint;
        returnButton.gameObject.SetActive(SoloGameSeries.Complete);
        returnButton.interactable = true;
        returnButton.GetComponentInChildren<TMP_Text>().text = "타이틀로 돌아가기";
        resultLeaveButton.interactable = true;
    }

    void LateUpdate()
    {
        // 화면 비율이 바뀌어도 화면 우측 상단을 유지한다.
        if (resultsPanel.activeSelf || leavePanel.activeSelf)
            SeriesResultsLayout.Apply(this, SoloGameSeries.Active ? SoloGameSeries.Complete : session != null && session.SeriesComplete);
    }
}
