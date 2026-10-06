using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 레이아웃은 에디터 메뉴에서 씬에 저장한다. 런타임에는 상태/문구/활성 여부만 바꾼다.
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
    int SelectedSkin => characterSelection != null ? characterSelection.Index : skin.value;
    bool ignoreOptions;

    void Start()
    {
        nickname.text = PlayerPrefs.GetString("LionIceRun.Nickname", "얼음 친구");
        soloButton.onClick.AddListener(() => { SaveName(); session.StartSolo(); });
        createButton.onClick.AddListener(() => { SaveName(); session.CreateRoom(nickname.text, SelectedSkin, capacity.value + 2); });
        joinButton.onClick.AddListener(() => { SaveName(); session.JoinRoom(joinCode.text, nickname.text, SelectedSkin); });
        backButton.onClick.AddListener(() =>
        {
            HidePanels(); session.roulette.selectionCanvas.gameObject.SetActive(true);
            session.roulette.titleGroup.SetActive(true);
        });
        readyButton.onClick.AddListener(session.ToggleReady);
        startButton.onClick.AddListener(session.StartRound);
        copyButton.onClick.AddListener(() => GUIUtility.systemCopyBuffer = session.RoomCode);
        leaveButton.onClick.AddListener(session.LeaveRoom);
        returnButton.onClick.AddListener(session.ReturnToLobby);
        resultLeaveButton.onClick.AddListener(session.LeaveRoom);
        matchLeaveButton.onClick.AddListener(session.LeaveRoom);
        fillBots.onValueChanged.AddListener(_ => { if (!ignoreOptions) session.SetBotOptions(fillBots.isOn, aggressive.value); });
        aggressive.onValueChanged.AddListener(_ => { if (!ignoreOptions) session.SetBotOptions(fillBots.isOn, aggressive.value); });
    }

    void SaveName()
    {
        nickname.text = OnlineRules.Nickname(nickname.text);
        PlayerPrefs.SetString("LionIceRun.Nickname", nickname.text);
        PlayerPrefs.SetInt("LionIceRun.Skin", SelectedSkin);
        PlayerPrefs.Save();
    }

    public void HidePanels()
    {
        MatchCountdown.Instance?.Hide();
        modesPanel.SetActive(false); lobbyPanel.SetActive(false); loadingPanel.SetActive(false);
        resultsPanel.SetActive(false); leavePanel.SetActive(false);
    }
    public void OpenModes()
    {
        HidePanels(); modesPanel.SetActive(true);
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
        var phase = session.CurrentPhase;
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
            returnButton.interactable = session.IsHost && !busy;
            resultLeaveButton.interactable = !busy;
        }
    }
}
