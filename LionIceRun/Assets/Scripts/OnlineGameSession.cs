using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// 방 코드 로비와 호스트 권한 경기. 씬을 재로드하지 않고 맵/참가자를 재구성해 Relay 접속을 유지한다.
[DefaultExecutionOrder(-550)]
public class OnlineGameSession : MonoBehaviour
{
    public enum Phase { Title, Connecting, Lobby, Rolling, Loading, Countdown, Playing, Results, Leaving }
    const string ControlChannel = "LionIceRun.Control.v1";
    const string InputChannel = "LionIceRun.Input.v1";
    const string PoseChannel = "LionIceRun.Pose.v1";
    public static OnlineGameSession Instance { get; private set; }
    public static bool InRoom => Instance != null && Instance.onlineMode;
    public static bool Authority => InRoom && Instance.network.IsServer;
    public static bool MatchPlaying => InRoom && Instance.CurrentPhase == Phase.Playing;

    public NetworkManager network;
    public GameManager manager;
    public MapRoulette roulette;
    public OnlineFrontend frontend;
    public GameObject actorPrefab;
    [Tooltip("Unity Gaming Services 환경 이름")]
    public string servicesEnvironment = "production";
    public Phase CurrentPhase { get; private set; }
    public bool Busy { get; private set; }
    public bool IsHost => network != null && network.IsServer;
    public string RoomCode => session != null ? session.Code : "";
    public string Notice { get; private set; } = "친구와 함께 얼음을 깨요!";
    public bool FillBots { get; private set; } = true;
    public int AggressiveBots { get; private set; } = 2;
    public IReadOnlyList<OnlineMember> Members => members;
    public IReadOnlyList<string> CommonFiles => OnlineRules.CommonMaps(members);
    public bool CanStart => IsHost && !Busy && CurrentPhase == Phase.Lobby && members.Count >= 2
        && members.Count == network.ConnectedClientsIds.Count && members.All(m => m.ready) && CommonFiles.Count > 0;

    ISession session;
    bool onlineMode, registered, leaving, disposed;
    PlayerController originalPlayer;
    readonly List<OnlineMember> members = new List<OnlineMember>();
    readonly Dictionary<ulong, OnlineActor> actors = new Dictionary<ulong, OnlineActor>();
    readonly Dictionary<int, GameObject> remoteVisuals = new Dictionary<int, GameObject>();
    readonly HashSet<ulong> loadedClients = new HashSet<ulong>();
    readonly Dictionary<ulong, double> awaitingHello = new Dictionary<ulong, double>();
    OnlineMapStamp[] catalog;
    OnlineMember localMember;
    bool helloAcknowledged;
    ulong[] finalRanking;
    OnlinePacket roll;
    string selectedFile;
    int round;
    readonly OnlineSeries series = new OnlineSeries();
    bool seriesActive;
    bool recordedSeriesWin;
    int seriesRound;
    double nextRoundAt;
    public int SeriesRound => seriesRound;
    public bool SeriesComplete => series.Complete;
    public string ResultsTitle => SeriesComplete ? "5판 최종 결과" : $"{seriesRound} / 5 라운드 결과";
    public string ResultsHint => SeriesComplete ? "합산 점수 우승: " + series.Winners()
        : $"{Math.Max(0, (int)Math.Ceiling(nextRoundAt - ServerNow))}초 뒤 {seriesRound + 1} / 5 라운드 자동 시작";
    public int LocalTotal => series.Scores.FirstOrDefault(s => actors.TryGetValue(s.actorId, out var a)
        && !a.spawn.bot && a.spawn.ownerId == network.LocalClientId)?.total ?? 0;
    double deadline, countdownAt;
    float nextPose, nextWorld, nextInput, nextHello;
    uint inputSequence, poseSequence, receivedPose;
    Vector3 inputMove;
    bool held, jump, dive, push;
    InputAction diveAction, pushAction;
    public double ServerNow => network != null && network.IsListening ? network.ServerTime.Time : Time.unscaledTimeAsDouble;
    public string ResultLines
    {
        get
        {
            var lines = new StringBuilder();
            int place = 0, previous = -1, index = 0;
            foreach (var score in series.Standings())
            {
                index++; if (score.total != previous) place = index;
                previous = score.total;
                lines.Append(place).Append("등  ").Append(score.nickname).Append("  ").Append(score.total).Append("점");
                if (!SeriesComplete) lines.Append(" (+").Append(score.lastPoints).Append(")");
                lines.AppendLine();
            }
            return lines.ToString();
        }
    }

    void Awake()
    {
        Instance = this;
        originalPlayer = manager != null ? manager.player : null;
        bool mapTool = MapRoulette.SkipNextSelection;
#if UNITY_EDITOR
        mapTool |= UnityEditor.SessionState.GetBool("LionIceRun.MapToolPlay", false);
#endif
        if (mapTool) { frontend.gameObject.SetActive(false); enabled = false; return; }
        if (network == null || manager == null || roulette == null || actorPrefab == null || frontend == null)
        {
            Debug.LogError("친구 멀티 세팅의 연결이 빠져 있습니다. LionIceRun > 친구 멀티 세팅을 실행하세요.");
            enabled = false; return;
        }
        network.OnClientConnectedCallback += OnConnected;
        network.NetworkConfig.ProtocolVersion = OnlineRules.Protocol;
        network.OnClientDisconnectCallback += OnDisconnected;
        diveAction = new InputAction("OnlineDive", InputActionType.Button, "<Keyboard>/leftCtrl");
        diveAction.AddBinding("<Keyboard>/rightCtrl"); diveAction.AddBinding("<Gamepad>/buttonWest");
        pushAction = new InputAction("OnlinePush", InputActionType.Button, "<Mouse>/leftButton");
        pushAction.AddBinding("<Gamepad>/rightTrigger");
        diveAction.Enable(); pushAction.Enable();
    }

    OnlineMapStamp[] ReadCatalog()
    {
        var result = new List<OnlineMapStamp>();
        foreach (string file in MapData.ListFiles().Take(48))
        {
            MapData map = MapData.Load(file);
            if (map == null || map.version != MapData.CurrentVersion || !OnlineRules.TrySpawns(map, 4, out _)) continue;
            result.Add(new OnlineMapStamp { file = file, hash = OnlineRules.MapHash(map) });
        }
        return result.ToArray();
    }

    async Task InitializeServices()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
        {
            var options = new InitializationOptions();
            options.SetOption("com.unity.services.core.environment-name", servicesEnvironment);
            await UnityServices.InitializeAsync(options);
        }
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            string profile = Application.isEditor ? "editor" : "player";
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == "-onlineProfile") profile = args[i + 1];
            AuthenticationService.Instance.SwitchProfile(profile);
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
    }

    public async void CreateRoom(string nickname, int skin, int maxPlayers)
    {
        if (Busy || onlineMode) return;
        Busy = true; CurrentPhase = Phase.Connecting; Notice = "로비를 만들고 있어요…";
        try
        {
            EnterOnline();
            await InitializeServices();
            var options = new SessionOptions
            {
                Name = OnlineRules.Nickname(nickname) + "의 얼음 로비", Type = "LionIceRun.v1",
                MaxPlayers = Mathf.Clamp(maxPlayers, 2, 4), IsPrivate = true
            }.WithRelayNetwork();
            ISession created = await MultiplayerService.Instance.CreateSessionAsync(options);
            if (disposed) { await created.LeaveAsync(); return; }
            session = created;
            RegisterHandlers();
            members.Clear();
            localMember = NewMember(network.LocalClientId, nickname, skin);
            members.Add(localMember);
            CurrentPhase = Phase.Lobby;
            Notice = "방 코드를 복사해 친구에게 보내세요.";
            PublishLobby();
        }
        catch (Exception e) { await ConnectionFailed(e); }
        finally { if (!disposed) Busy = false; }
    }

    public async void JoinRoom(string code, string nickname, int skin)
    {
        code = OnlineRules.Code(code);
        if (Busy || onlineMode) return;
        if (code.Length < 4 || code.Length > 16 || code.Any(c => !((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))))
        { Notice = "방 코드를 확인해 주세요."; return; }
        Busy = true; CurrentPhase = Phase.Connecting; Notice = "친구의 로비에 연결하고 있어요…";
        try
        {
            EnterOnline();
            await InitializeServices();
            ISession joined = await MultiplayerService.Instance.JoinSessionByCodeAsync(code,
                new JoinSessionOptions { Type = "LionIceRun.v1" });
            if (disposed) { await joined.LeaveAsync(); return; }
            session = joined;
            RegisterHandlers();
            CurrentPhase = Phase.Lobby;
            members.Clear();
            localMember = NewMember(network.LocalClientId, nickname, skin);
            members.Add(localMember);
            helloAcknowledged = false;
            nextHello = 0f;
            Notice = "참가자 정보를 확인하고 있어요…";
        }
        catch (Exception e) { await ConnectionFailed(e); }
        finally { if (!disposed) Busy = false; }
    }

    OnlineMember NewMember(ulong id, string nickname, int skin) => new OnlineMember
    { clientId = id, nickname = OnlineRules.Nickname(nickname), skin = Mathf.Clamp(skin, 0, 2), maps = catalog, ready = false };

    void EnterOnline()
    {
        onlineMode = true;
        catalog = ReadCatalog();
        Time.timeScale = 1f;
        originalPlayer.InputEnabled = false;
        originalPlayer.gameObject.SetActive(false);
        var botManager = manager.GetComponent<BotManager>();
        if (botManager != null) botManager.enabled = false;
        foreach (var brain in FindObjectsByType<BotBrain>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        { brain.gameObject.SetActive(false); Destroy(brain.gameObject); }
        roulette.gameCanvas.enabled = false;
        roulette.selectionCanvas.gameObject.SetActive(false);
        manager.ResetOnlineMatch(null);
        if (roulette.titleDecorations != null) roulette.titleDecorations.SetActive(true);
    }

    async Task ConnectionFailed(Exception e)
    {
        Debug.LogException(e);
        Notice = e is SessionException error ? ConnectionErrorText(error.Error)
            : "접속하지 못했어요. 인터넷 연결과 Unity Services 설정을 확인해 주세요.";
        await CleanupSession();
        if (!disposed) RestoreTitle();
    }

    static string ConnectionErrorText(SessionError error)
    {
        switch (error)
        {
            case SessionError.SessionNotFound: case SessionError.SessionDeleted:
                return "방을 찾을 수 없어요. 방 코드를 확인해 주세요.";
            case SessionError.NotAuthorized: return "접속 권한을 확인하지 못했어요. 다시 시도해 주세요.";
            case SessionError.NetworkSetupFailed: case SessionError.NetworkManagerStartFailed:
                return "네트워크 연결을 시작하지 못했어요. 인터넷 연결을 확인해 주세요.";
            default: return "참가하지 못했어요. 방 코드·정원·경기 진행 상태를 확인하고 다시 시도해 주세요.";
        }
    }

    void RegisterHandlers()
    {
        if (registered || !network.IsListening) return;
        network.CustomMessagingManager.RegisterNamedMessageHandler(ControlChannel, OnControl);
        network.CustomMessagingManager.RegisterNamedMessageHandler(InputChannel, OnInput);
        network.CustomMessagingManager.RegisterNamedMessageHandler(PoseChannel, OnPose);
        registered = true;
    }

    void OnConnected(ulong id)
    {
        RegisterHandlers();
        if (network.IsServer && id != network.LocalClientId && CurrentPhase != Phase.Lobby && CurrentPhase != Phase.Connecting)
        { network.DisconnectClient(id); return; }
        if (network.IsServer && id != network.LocalClientId) awaitingHello[id] = ServerNow + 20;
    }

    void OnDisconnected(ulong id)
    {
        if (!onlineMode || leaving || disposed) return;
        if (network.IsServer)
        {
            awaitingHello.Remove(id);
            members.RemoveAll(m => m.clientId == id);
            if (seriesActive && !series.Complete) { AbortStart("참가자가 나가 5판 시리즈를 취소했어요."); return; }
            foreach (var actor in actors.Values)
                if (!actor.spawn.bot && actor.spawn.ownerId == id) manager.WithdrawOnline(actor.controller);
            if (CurrentPhase == Phase.Playing || CurrentPhase == Phase.Results) return;
            ReturnToLobby();
        }
        else if (id == network.LocalClientId || id == NetworkManager.ServerClientId)
        { Notice = "방장과의 연결이 종료되었어요."; LeaveRoom(); }
    }

    public void ToggleReady()
    {
        if (Busy || CurrentPhase != Phase.Lobby) return;
        OnlineMember local = members.Find(m => m.clientId == network.LocalClientId);
        if (local == null) return;
        if (IsHost) { local.ready = !local.ready; PublishLobby(); }
        else Send(new OnlinePacket { kind = "ready", ready = !local.ready }, NetworkManager.ServerClientId);
    }

    public void SetBotOptions(bool fill, int aggressive)
    {
        if (!IsHost || CurrentPhase != Phase.Lobby || Busy) return;
        FillBots = fill; AggressiveBots = Mathf.Clamp(aggressive, 0, 2);
        foreach (var m in members) m.ready = false;
        PublishLobby();
    }

    void PublishLobby()
    {
        SendAll(new OnlinePacket { kind = "lobby", members = members.ToArray(), fillBots = FillBots,
            aggressiveBots = AggressiveBots, maxPlayers = session != null ? session.MaxPlayers : 4, round = round });
    }

    void Send(OnlinePacket packet, ulong id)
    {
        if (!registered || !network.IsListening) return;
        byte[] data = Encoding.UTF8.GetBytes(JsonUtility.ToJson(packet));
        if (data.Length > OnlineRules.MaxPacketBytes) { Debug.LogError("온라인 메시지 크기 초과"); return; }
        using (var writer = new FastBufferWriter(data.Length + 4, Allocator.Temp))
        {
            writer.WriteValueSafe(data.Length); writer.WriteBytesSafe(data);
            network.CustomMessagingManager.SendNamedMessage(ControlChannel, id, writer, NetworkDelivery.ReliableFragmentedSequenced);
        }
    }

    void SendAll(OnlinePacket packet)
    {
        foreach (ulong id in network.ConnectedClientsIds.ToArray())
            if (id != network.LocalClientId) Send(packet, id);
    }

    void OnControl(ulong sender, FastBufferReader reader)
    {
        try
        {
            reader.ReadValueSafe(out int length);
            int limit = IsHost ? 16384 : OnlineRules.MaxPacketBytes;
            if (length < 2 || length > limit || reader.Length - reader.Position < length) return;
            byte[] data = new byte[length]; reader.ReadBytesSafe(ref data, length);
            var packet = JsonUtility.FromJson<OnlinePacket>(Encoding.UTF8.GetString(data));
            if (packet == null) return;
            if (IsHost) HandleCommand(sender, packet);
            else if (sender == NetworkManager.ServerClientId) HandleHostPacket(packet);
        }
        catch (Exception e) { Debug.LogWarning("온라인 메시지를 처리하지 못했습니다: " + e.Message); }
    }

    void HandleCommand(ulong sender, OnlinePacket p)
    {
        if (sender == network.LocalClientId || !network.ConnectedClientsIds.Contains(sender)) return;
        OnlineMember member = members.Find(m => m.clientId == sender);
        if (p.kind == "hello" && CurrentPhase == Phase.Lobby)
        {
            if (p.protocol != OnlineRules.Protocol || p.members == null || p.members.Length != 1
                || p.maps == null || p.maps.Length > 48)
            { Send(new OnlinePacket { kind = "error", message = "게임 버전이 맞지 않아요." }, sender); network.DisconnectClient(sender); return; }
            if (member == null && members.Count < session.MaxPlayers)
            {
                var hello = p.members[0];
                member = NewMember(sender, hello.nickname, hello.skin);
                member.maps = p.maps.Where(m => m != null && catalog.Any(c => c.file == m.file && c.hash == m.hash)).ToArray();
                members.Add(member); members.Sort((a, b) => a.clientId.CompareTo(b.clientId));
                // 기존 참가자의 준비는 유지하고, NewMember가 새 참가자만 준비 전으로 만든다.
            }
            awaitingHello.Remove(sender);
            PublishLobby(); return;
        }
        if (member == null || ServerNow - member.lastCommandTime < 0.15) return;
        member.lastCommandTime = ServerNow;
        if (p.kind == "ready" && CurrentPhase == Phase.Lobby)
        { member.ready = p.ready; PublishLobby(); }
        else if (p.kind == "loaded" && CurrentPhase == Phase.Loading && p.round == round)
        {
            if (p.mapHash != roll.mapHash) { AbortStart("맵 내용이 달라 시작을 취소했어요."); return; }
            loadedClients.Add(sender); TryCountdown();
        }
        else if (p.kind == "loadFailed" && p.round == round && CurrentPhase == Phase.Loading)
            AbortStart("참가자 맵을 준비하지 못해 시작을 취소했어요.");
    }

    void HandleHostPacket(OnlinePacket p)
    {
        switch (p.kind)
        {
            case "lobby":
                seriesActive = false; seriesRound = 0; nextRoundAt = 0; series.Clear();
                round = p.round; members.Clear(); members.AddRange(p.members ?? Array.Empty<OnlineMember>());
                helloAcknowledged = members.Any(m => m.clientId == network.LocalClientId);
                FillBots = p.fillBots; AggressiveBots = p.aggressiveBots;
                if (CurrentPhase != Phase.Lobby) ResetLobbyWorld();
                CurrentPhase = Phase.Lobby;
                Notice = CommonFiles.Count > 0 ? "준비를 누르면 방장이 경기를 시작할 수 있어요." : "공통 맵이 없어요. 같은 버전의 게임을 사용해 주세요.";
                break;
            case "roll":
                if (p.round <= round) return;
                seriesActive = true; seriesRound = p.seriesRound; nextRoundAt = 0;
                if (seriesRound == 1) { series.Clear(); recordedSeriesWin = false; }
                if (p.scores != null) series.ApplySnapshot(p.completedRounds, p.scores);
                round = p.round; roll = p; CurrentPhase = Phase.Rolling;
                roulette.cardSpacing = p.spacing;
                frontend.HidePanels(); roulette.ShowOnlineRoll(p.files, 0, false, $"{seriesRound} / 5 · 맵 자동 선택 중…");
                break;
            case "stop":
                if (p.round == round && CurrentPhase == Phase.Rolling)
                { roll.stopAt = p.stopAt; roll.stopFrom = p.stopFrom; roll.stopTo = p.stopTo; roll.duration = p.duration; }
                break;
            case "load":
                if (p.round != round) return;
                roll.mapHash = p.mapHash; selectedFile = p.mapFile;
                CurrentPhase = Phase.Loading; StartCoroutine(LoadMatch(p)); break;
            case "countdown":
                if (p.round != round || CurrentPhase != Phase.Loading) return;
                countdownAt = p.startAt; CurrentPhase = Phase.Countdown; break;
            case "world":
                if (p.round == round && (CurrentPhase == Phase.Playing || CurrentPhase == Phase.Countdown || CurrentPhase == Phase.Results)) ApplyWorld(p);
                break;
            case "toast":
                if (p.round == round) GameHUD.Instance?.ShowToast(p.message, new Color(0.7f, 0.9f, 1f)); break;
            case "error": Notice = p.message; break;
        }
    }

    public async void StartRound()
    {
        if (!CanStart) return;
        Busy = true;
        try
        {
            var host = session.AsHost(); host.IsLocked = true; await host.SavePropertiesAsync();
            if (!onlineMode || members.Count < 2 || !members.All(m => m.ready)
                || members.Count != network.ConnectedClientsIds.Count) return;
            string[] files = CommonFiles.ToArray();
            if (files.Length == 0) { Notice = "공통 맵이 없어요."; return; }
            series.Clear(); seriesRound = 0; seriesActive = true; recordedSeriesWin = false;
            BeginSeriesRound(files);
        }
        catch (Exception e) { Debug.LogException(e); Notice = "경기를 시작하지 못했어요. 다시 시도해 주세요."; }
        finally
        {
            Busy = false;
            if (CurrentPhase == Phase.Lobby && session != null) await UnlockRoom();
        }
    }

    double RollPosition(double now)
    {
        if (roll.stopAt <= 0 || now < roll.stopAt) return Math.Max(0, now - roll.startAt) * roll.speed;
        return MapRouletteMotion.Position(roll.stopFrom, roll.stopTo, (now - roll.stopAt) / roll.duration);
    }

    public void StopRoll()
    {
        // 온라인은 처음 전송한 서버 시각대로 자동 감속한다. 수동 버튼/명령은 무시한다.
    }

    void BeginSeriesRound(string[] files)
    {
        if (!seriesActive || seriesRound >= OnlineSeries.RoundCount) return;
        if (files.Length == 0 || members.Count < 2 || members.Count != network.ConnectedClientsIds.Count)
        { AbortStart("참가자 또는 공통 맵이 부족해 시리즈를 취소했어요."); return; }
        for (int i = files.Length - 1; i > 0; i--) { int j = UnityEngine.Random.Range(0, i + 1); (files[i], files[j]) = (files[j], files[i]); }
        seriesRound++; round++; receivedPose = poseSequence = 0; nextRoundAt = 0; finalRanking = null;
        roll = new OnlinePacket { kind = "roll", round = round, seriesRound = seriesRound, files = files,
            startAt = ServerNow + 0.5, speed = roulette.scrollSpeed, spacing = roulette.cardSpacing,
            scores = series.Scores.Count > 0 ? series.Snapshot() : null, completedRounds = series.CompletedRounds };
        roll.stopAt = roll.startAt + UnityEngine.Random.Range(3.5f, 5.5f);
        roll.stopFrom = (roll.stopAt - roll.startAt) * roll.speed;
        roll.stopTo = Math.Ceiling((roll.stopFrom + roll.speed * roulette.stopDuration / 3) / roll.spacing) * roll.spacing;
        roll.duration = (roll.stopTo - roll.stopFrom) * 3 / roll.speed;
        CurrentPhase = Phase.Rolling;
        frontend.HidePanels(); roulette.ShowOnlineRoll(files, 0, false, $"{seriesRound} / 5 · 맵 자동 선택 중…");
        SendAll(roll);
    }

    void Update()
    {
        if (!onlineMode || !network.IsListening || Busy) return;
        RegisterHandlers();
        if (!IsHost && CurrentPhase == Phase.Lobby && Time.unscaledTime >= nextHello && !helloAcknowledged)
        {
            nextHello = Time.unscaledTime + 1f;
            Send(new OnlinePacket { kind = "hello", protocol = OnlineRules.Protocol, members = new[] { localMember }, maps = catalog }, NetworkManager.ServerClientId);
        }
        if (IsHost)
            foreach (var pending in awaitingHello.ToArray())
                if (ServerNow > pending.Value) { awaitingHello.Remove(pending.Key); network.DisconnectClient(pending.Key); }
        if (CurrentPhase == Phase.Rolling)
        {
            double position = RollPosition(ServerNow);
            bool selected = roll.stopAt > 0 && ServerNow >= roll.stopAt + roll.duration;
            int index = MapRouletteMotion.CardIndex((int)Math.Round(position / roll.spacing), roll.files.Length);
            string caption = $"{seriesRound} / 5 · " + (selected ? MapData.Load(roll.files[index])?.name + " 선택!"
                : ServerNow >= roll.stopAt ? "점점 느려져요…" : "맵 자동 선택 중…");
            roulette.RenderOnlinePosition(position, false, caption);
            if (IsHost && selected && ServerNow >= roll.stopAt + roll.duration + 1) BeginLoad(roll.files[index]);
        }
        if (IsHost && CurrentPhase == Phase.Loading && ServerNow >= deadline) AbortStart("맵 로딩 시간이 초과되어 로비로 돌아왔어요.");
        if (CurrentPhase == Phase.Countdown)
        {
            int remaining = Mathf.Clamp(Mathf.CeilToInt((float)(countdownAt - ServerNow)), 0, 3);
            frontend.ShowCountdown(remaining);
            if (ServerNow >= countdownAt)
            {
                frontend.HidePanels(); CurrentPhase = Phase.Playing;
                foreach (var actor in actors.Values)
                {
                    if (!actor.controller.networkReplica) actor.GetComponent<Rigidbody>().isKinematic = false;
                    actor.controller.InputEnabled = true;
                    actor.GetComponent<BotBrain>()?.Activate();
                }
                manager.BeginSelectedMatch();
                nextPose = nextWorld = 0;
            }
        }
        if (CurrentPhase == Phase.Playing)
        {
            CaptureInput();
            if (IsHost)
            {
                foreach (var member in members)
                    if (ServerNow - member.lastInputTime > 0.3)
                        foreach (var actor in actors.Values)
                            if (!actor.spawn.bot && actor.spawn.ownerId == member.clientId) actor.controller.SetExternalInput(Vector3.zero, false, false, false);
                if (manager.State == GameManager.GameState.GameOver)
                {
                    finalRanking = manager.RankedBodies().Select(body => actors.Values.First(a => a.controller == body).spawn.actorId).ToArray();
                    if (!series.CompleteRound(seriesRound, finalRanking)) { AbortStart("라운드 점수를 확인하지 못해 시리즈를 취소했어요."); return; }
                    nextRoundAt = series.Complete ? 0 : ServerNow + OnlineSeries.ResultsSeconds;
                    RecordSeriesVictory();
                    CurrentPhase = Phase.Results;
                    foreach (var actor in actors.Values)
                    { actor.controller.InputEnabled = false; actor.GetComponent<Rigidbody>().isKinematic = true; }
                    ResetTrials();
                }
            }
        }
        if (IsHost && (CurrentPhase == Phase.Playing || CurrentPhase == Phase.Results))
        {
            if (Time.unscaledTime >= nextPose) { nextPose = Time.unscaledTime + 1f / 30f; SendPoses(); }
            if (Time.unscaledTime >= nextWorld) { nextWorld = Time.unscaledTime + 0.2f; SendWorld(); }
        }
        if (IsHost && seriesActive && CurrentPhase == Phase.Results && !series.Complete && ServerNow >= nextRoundAt)
            BeginSeriesRound(CommonFiles.ToArray());
    }

    void BeginLoad(string file)
    {
        selectedFile = file;
        MapData map = MapData.Load(file);
        int count = FillBots ? 4 : members.Count;
        if (map == null || !OnlineRules.TrySpawns(map, count, out List<Vector2Int> cells))
        { AbortStart("안전한 시작 칸이 부족해요."); return; }
        var spawns = new List<OnlineSpawn>();
        for (int i = 0; i < count; i++)
        {
            bool bot = i >= members.Count;
            OnlineMember member = bot ? null : members[i];
            int botIndex = i - members.Count;
            bool aggressive = bot && botIndex >= Math.Max(0, count - members.Count - AggressiveBots);
            spawns.Add(new OnlineSpawn { actorId = (ulong)i + 1, ownerId = bot ? ulong.MaxValue : member.clientId,
                nickname = bot ? $"NPC {botIndex + 1} ({(aggressive ? "공격" : "신중")})" : member.nickname,
                skin = bot ? i % 3 : member.skin, bot = bot, aggressive = aggressive, x = cells[i].x, y = cells[i].y });
        }
        roll.mapHash = OnlineRules.MapHash(map);
        if (seriesRound == 1) series.Begin(spawns);
        var packet = new OnlinePacket { kind = "load", round = round, mapFile = file, mapHash = roll.mapHash, spawns = spawns.ToArray() };
        loadedClients.Clear(); deadline = ServerNow + 30; CurrentPhase = Phase.Loading;
        SendAll(packet); StartCoroutine(LoadMatch(packet));
    }

    IEnumerator LoadMatch(OnlinePacket packet)
    {
        frontend.ShowLoading();
        roulette.selectionCanvas.gameObject.SetActive(false);
        if (roulette.titleDecorations != null) roulette.titleDecorations.SetActive(false);
        MapData map = MapData.Load(packet.mapFile);
        if (map == null || OnlineRules.MapHash(map) != packet.mapHash || packet.spawns == null || packet.spawns.Length > 4)
        {
            if (IsHost) AbortStart("맵 내용을 확인하지 못했어요.");
            else Send(new OnlinePacket { kind = "loadFailed", round = round }, NetworkManager.ServerClientId);
            yield break;
        }
        DestroyActors();
        manager.ResetOnlineMatch(null);
        ResetTrials();
        manager.grid.networkReplica = !IsHost;
        manager.grid.mapFile = packet.mapFile;
        manager.grid.Build();
        yield return null; // 이전 얼음과 참가자의 Destroy가 끝난 뒤 새 물리 대상을 배치한다.
        if (!onlineMode || round != packet.round || CurrentPhase != Phase.Loading) yield break;
        foreach (OnlineSpawn spawn in packet.spawns)
        {
            if (!map.IsIce(new Vector2Int(spawn.x, spawn.y)) || actors.ContainsKey(spawn.actorId))
            { if (IsHost) AbortStart("시작 위치가 올바르지 않아요."); else Send(new OnlinePacket { kind = "loadFailed", round = round }, 0); yield break; }
            var go = Instantiate(actorPrefab);
            go.name = OnlineRules.Nickname(spawn.nickname);
            go.SetActive(false);
            var controller = go.GetComponent<PlayerController>();
            controller.grid = manager.grid; controller.spawnAtStartCell = false;
            controller.useExternalInput = true; controller.networkReplica = !IsHost; controller.InputEnabled = false;
            go.GetComponent<IceStepTracker>().grid = manager.grid;
            var skin = go.GetComponent<PlayerSkin>(); skin.skin = (PlayerSkin.Skin)spawn.skin;
            var actor = go.AddComponent<OnlineActor>(); actor.spawn = spawn; actor.controller = controller;
            go.GetComponent<PlayerNameplate>()?.SetName(spawn.nickname, !spawn.bot && spawn.ownerId == network.LocalClientId, spawn.bot);
            if (spawn.bot && IsHost)
            {
                var brain = go.AddComponent<BotBrain>(); brain.grid = manager.grid;
                brain.player = actors.Values.FirstOrDefault(a => !a.spawn.bot)?.controller;
                brain.ConfigureBehavior(spawn.aggressive ? BotBrain.BehaviorType.Aggressive : BotBrain.BehaviorType.Cautious);
            }
            go.SetActive(true);
            controller.ResetMotion(manager.grid.CellTop(new Vector2Int(spawn.x, spawn.y)) + Vector3.up * controller.spawnHeight);
            go.GetComponent<Rigidbody>().isKinematic = true;
            actors.Add(spawn.actorId, actor); manager.Register(controller);
            if (!spawn.bot && spawn.ownerId == network.LocalClientId) manager.player = controller;
        }
        if (manager.player == null) { Notice = "내 참가자를 찾지 못했어요."; yield break; }
        inputSequence = 0; receivedPose = 0; jump = dive = push = held = false;
        foreach (var member in members) { member.lastInput = 0; member.lastInputTime = ServerNow; }
        var view = Camera.main != null ? Camera.main.GetComponent<QuarterViewCamera>() : null;
        if (view != null) { view.player = manager.player.transform; view.Fit(); }
        foreach (var trial in manager.GetComponents<TrialBase>()) trial.player = manager.player;
        var spawner = manager.GetComponent<RepairItemSpawner>(); if (spawner != null) spawner.player = manager.player;
        GameHUD.Instance?.ResetOnlineHud(manager.player.GetComponent<PlayerPowerUps>());
        roulette.gameCanvas.enabled = true;
        if (IsHost) { loadedClients.Add(network.LocalClientId); TryCountdown(); }
        else Send(new OnlinePacket { kind = "loaded", round = round, mapHash = packet.mapHash }, NetworkManager.ServerClientId);
    }

    void TryCountdown()
    {
        if (CurrentPhase != Phase.Loading || !members.All(m => loadedClients.Contains(m.clientId))) return;
        countdownAt = ServerNow + 3.5;
        CurrentPhase = Phase.Countdown;
        SendAll(new OnlinePacket { kind = "countdown", round = round, startAt = countdownAt });
    }

    void CaptureInput()
    {
        if (manager.player == null || !manager.player.gameObject.activeSelf)
        { held = jump = dive = push = false; inputMove = Vector3.zero; }
        else
        {
            Vector2 move = InputSystem.actions.FindAction("Player/Move").ReadValue<Vector2>();
            var jumpInput = InputSystem.actions.FindAction("Player/Jump");
            Transform camera = Camera.main != null ? Camera.main.transform : null;
            Vector3 forward = camera != null ? Vector3.ProjectOnPlane(camera.forward, Vector3.up).normalized : Vector3.forward;
            Vector3 right = camera != null ? Vector3.ProjectOnPlane(camera.right, Vector3.up).normalized : Vector3.right;
            inputMove = Vector3.ClampMagnitude(forward * move.y + right * move.x, 1f);
            held = jumpInput.IsPressed(); jump |= jumpInput.WasPressedThisFrame();
            dive |= diveAction.WasPressedThisFrame(); push |= pushAction.WasPressedThisFrame();
        }
        if (Time.unscaledTime < nextInput && !jump && !dive && !push) return;
        nextInput = Time.unscaledTime + 1f / 30f;
        inputSequence++;
        if (IsHost) ApplyInput(network.LocalClientId, inputSequence, inputMove, held, jump, dive, push);
        else
        {
            using (var writer = new FastBufferWriter(64, Allocator.Temp))
            {
                writer.WriteValueSafe(round); writer.WriteValueSafe(inputSequence); writer.WriteValueSafe(inputMove);
                writer.WriteValueSafe(held); writer.WriteValueSafe(jump); writer.WriteValueSafe(dive); writer.WriteValueSafe(push);
                network.CustomMessagingManager.SendNamedMessage(InputChannel, 0, writer,
                    NetworkDelivery.ReliableSequenced);
            }
        }
        jump = dive = push = false;
    }

    void OnInput(ulong sender, FastBufferReader reader)
    {
        if (!IsHost || CurrentPhase != Phase.Playing || reader.Length > 64) return;
        try
        {
            reader.ReadValueSafe(out int messageRound); reader.ReadValueSafe(out uint sequence);
            reader.ReadValueSafe(out Vector3 move); reader.ReadValueSafe(out bool holdJump);
            reader.ReadValueSafe(out bool pressJump); reader.ReadValueSafe(out bool pressDive); reader.ReadValueSafe(out bool pressPush);
            if (messageRound == round) ApplyInput(sender, sequence, move, holdJump, pressJump, pressDive, pressPush);
        }
        catch (Exception) { /* 잘린 입력 패킷은 무시한다. */ }
    }

    void ApplyInput(ulong sender, uint sequence, Vector3 move, bool holdJump, bool pressJump, bool pressDive, bool pressPush)
    {
        var member = members.Find(m => m.clientId == sender);
        if (member == null || !OnlineRules.Newer(sequence, member.lastInput)
            || float.IsNaN(move.x) || float.IsInfinity(move.x) || float.IsNaN(move.z) || float.IsInfinity(move.z)) return;
        member.lastInput = sequence; member.lastInputTime = ServerNow;
        foreach (var actor in actors.Values)
            if (!actor.spawn.bot && actor.spawn.ownerId == sender && actor.gameObject.activeSelf)
                actor.controller.SetExternalInput(Vector3.ClampMagnitude(new Vector3(move.x, 0, move.z), 1), holdJump, pressJump, pressDive, pressPush);
    }

    void SendPoses()
    {
        using (var writer = new FastBufferWriter(1024, Allocator.Temp))
        {
            writer.WriteValueSafe(round); writer.WriteValueSafe(++poseSequence); writer.WriteValueSafe(actors.Count);
            foreach (var actor in actors.Values)
            {
                var c = actor.controller;
                manager.StatusOf(c, out bool fallen, out float remaining, out bool waiting);
                writer.WriteValueSafe(actor.spawn.actorId); writer.WriteValueSafe(actor.gameObject.activeSelf);
                writer.WriteValueSafe(actor.transform.position); writer.WriteValueSafe(actor.transform.rotation);
                writer.WriteValueSafe(c.Velocity); writer.WriteValueSafe((byte)c.State); writer.WriteValueSafe(c.IsGrounded);
                writer.WriteValueSafe(float.IsNegativeInfinity(c.LastPushTime) ? -1f : Time.time - c.LastPushTime);
                writer.WriteValueSafe(actor.GetComponent<PlayerPowerUps>().HasShield); writer.WriteValueSafe(c.BoostRemaining);
                writer.WriteValueSafe(fallen); writer.WriteValueSafe(remaining); writer.WriteValueSafe(waiting);
            }
            foreach (ulong id in network.ConnectedClientsIds)
                if (id != network.LocalClientId) network.CustomMessagingManager.SendNamedMessage(PoseChannel, id, writer, NetworkDelivery.UnreliableSequenced);
        }
    }

    void OnPose(ulong sender, FastBufferReader reader)
    {
        if (IsHost || sender != 0 || reader.Length > 1024 || actors.Count == 0) return;
        try
        {
            reader.ReadValueSafe(out int messageRound); reader.ReadValueSafe(out uint sequence); reader.ReadValueSafe(out int count);
            if (messageRound != round || !OnlineRules.Newer(sequence, receivedPose) || count < 0 || count > 4) return;
            receivedPose = sequence;
            for (int i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out ulong id); reader.ReadValueSafe(out bool active);
                reader.ReadValueSafe(out Vector3 position); reader.ReadValueSafe(out Quaternion rotation);
                reader.ReadValueSafe(out Vector3 velocity); reader.ReadValueSafe(out byte state); reader.ReadValueSafe(out bool grounded);
                reader.ReadValueSafe(out float pushAge); reader.ReadValueSafe(out bool shield); reader.ReadValueSafe(out float boost);
                reader.ReadValueSafe(out bool fallen); reader.ReadValueSafe(out float remaining); reader.ReadValueSafe(out bool waiting);
                if (actors.TryGetValue(id, out var actor)) actor.ApplyPose(position, rotation, velocity,
                    (PlayerController.MoveState)state, grounded, pushAge, shield, boost, active, fallen, remaining, waiting);
            }
        }
        catch (Exception) { /* 잘린 스냅샷은 다음 패킷에서 복구한다. */ }
    }

    void SendWorld()
    {
        var stages = new StringBuilder();
        foreach (var block in manager.grid.IceBlocks) stages.Append((char)('0' + (int)block.CurrentStage));
        ulong[] ranking = manager.State == GameManager.GameState.GameOver
            ? manager.RankedBodies().Select(body => actors.Values.First(a => a.controller == body).spawn.actorId).ToArray() : null;
        SendAll(new OnlinePacket { kind = "world", round = round, stages = stages.ToString(), elapsed = manager.timer.Elapsed,
            state = (int)manager.State, ranking = ranking, seriesRound = seriesRound, completedRounds = series.CompletedRounds,
            nextRoundAt = nextRoundAt, scores = series.Snapshot(), visuals = OnlineWorldVisual.All.Where(v => v != null).Select(v => v.Capture()).ToArray() });
    }

    void ApplyWorld(OnlinePacket p)
    {
        seriesRound = p.seriesRound; nextRoundAt = p.nextRoundAt;
        if (p.scores != null) series.ApplySnapshot(p.completedRounds, p.scores);
        RecordSeriesVictory();
        if (p.stages != null && p.stages.Length == manager.grid.IceBlocks.Count)
            for (int i = 0; i < p.stages.Length; i++)
                if (p.stages[i] >= '0' && p.stages[i] <= '4') manager.grid.IceBlocks[i].ApplyNetworkStage((IceBlock.Stage)(p.stages[i] - '0'));
        manager.timer.ApplyOnlineTime(p.elapsed, p.state == (int)GameManager.GameState.Playing);
        ApplyVisuals(p.visuals ?? Array.Empty<OnlineVisualState>());
        if (p.state == (int)GameManager.GameState.GameOver && p.ranking != null && p.ranking.Length > 0)
        {
            finalRanking = p.ranking;
            int place = Array.FindIndex(p.ranking, id => actors.TryGetValue(id, out var a) && a.controller == manager.player) + 1;
            var winner = actors.TryGetValue(p.ranking[0], out var winningActor) ? winningActor.controller : null;
            manager.ApplyOnlineResult(winner, place); CurrentPhase = Phase.Results;
        }
    }

    void ApplyVisuals(OnlineVisualState[] visuals)
    {
        var ids = new HashSet<int>(); bool windActive = false;
        foreach (var v in visuals)
        {
            if (v.kind == 5)
            { manager.GetComponent<WindTrial>()?.ApplyNetworkWind(v.visible, v.rotation); windActive = v.visible; continue; }
            ids.Add(v.id);
            if (!remoteVisuals.TryGetValue(v.id, out GameObject go)) { go = CreateVisual(v.kind); if (go == null) continue; remoteVisuals[v.id] = go; }
            go.transform.SetPositionAndRotation(v.position, v.rotation); go.transform.localScale = v.scale;
            foreach (var renderer in go.GetComponentsInChildren<Renderer>()) renderer.enabled = v.visible;
            var animator = go.GetComponent<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null && v.animation != 0) animator.Play(v.animation, 0, v.animationTime);
        }
        if (!windActive) manager.GetComponent<WindTrial>()?.ApplyNetworkWind(false, Quaternion.identity);
        foreach (var entry in remoteVisuals.ToArray()) if (!ids.Contains(entry.Key)) { Destroy(entry.Value); remoteVisuals.Remove(entry.Key); }
    }

    GameObject CreateVisual(int kind)
    {
        GameObject go; Material material = null;
        if (kind == 4)
        {
            var penguin = manager.GetComponent<PenguinTrial>();
            if (penguin == null || penguin.penguinModel == null) return null;
            go = Instantiate(penguin.penguinModel);
            var animator = go.GetComponent<Animator>() ?? go.AddComponent<Animator>(); animator.runtimeAnimatorController = penguin.penguinAnimator;
        }
        else if (kind >= 10 && kind <= 13)
        {
            var spawner = manager.GetComponent<RepairItemSpawner>();
            if (spawner == null || spawner.itemPrefab == null || spawner.itemPrefab.visual == null) return null;
            go = Instantiate(spawner.itemPrefab.visual.gameObject);
            Color color = RepairItem.ColorOf((RepairItem.Kind)(kind - 10));
            var properties = new MaterialPropertyBlock(); properties.SetColor("_BaseColor", color); properties.SetColor("_EmissionColor", color * 1.2f);
            foreach (var renderer in go.GetComponentsInChildren<Renderer>()) renderer.SetPropertyBlock(properties);
        }
        else
        {
            go = GameObject.CreatePrimitive(kind == 1 ? PrimitiveType.Sphere : kind == 3 ? PrimitiveType.Cube : PrimitiveType.Cylinder);
            material = kind == 0 ? manager.GetComponent<SnowballTrial>()?.shadowMaterial
                : kind == 1 ? manager.GetComponent<SnowballTrial>()?.snowMaterial
                : kind == 2 ? manager.GetComponent<IcicleTrial>()?.iceMaterial : manager.GetComponent<IcicleTrial>()?.dangerMaterial;
            go.GetComponent<Renderer>().sharedMaterial = material;
        }
        go.name = "OnlineVisual";
        foreach (var collider in go.GetComponentsInChildren<Collider>()) { collider.enabled = false; Destroy(collider); }
        return go;
    }

    public static void BroadcastToast(string text, Color color)
    {
        if (Authority && MatchPlaying) Instance.SendAll(new OnlinePacket { kind = "toast", round = Instance.round, message = text });
    }

    void RecordSeriesVictory()
    {
        if (!series.Complete || recordedSeriesWin) return;
        recordedSeriesWin = true;
        var table = series.Standings();
        if (table.Length > 0 && LocalTotal == table[0].total) manager.RecordOnlineSeriesWin();
    }

    public async void ReturnToLobby()
    {
        if (!onlineMode) return;
        if (!IsHost) { Notice = "방장이 로비로 돌아가기를 기다려 주세요."; return; }
        if (Busy) return;
        if (seriesActive && !series.Complete) return;
        Busy = true;
        try
        {
            seriesActive = false; seriesRound = 0; nextRoundAt = 0; series.Clear();
            CurrentPhase = Phase.Lobby; ResetLobbyWorld();
            foreach (var member in members) member.ready = false;
            await UnlockRoom(); PublishLobby();
        }
        finally { Busy = false; }
    }

    void OnApplicationQuit()
    {
        leaving = true;
        if (session != null) _ = session.LeaveAsync();
    }

    async Task UnlockRoom()
    {
        try { if (session != null && IsHost) { var host = session.AsHost(); host.IsLocked = false; await host.SavePropertiesAsync(); } }
        catch (Exception e) { Debug.LogException(e); Notice = "로비 상태 갱신 실패. 나간 뒤 다시 만들어 주세요."; }
    }

    void AbortStart(string message) { Notice = message; seriesActive = false; ReturnToLobby(); }

    void ResetTrials()
    {
        foreach (var trial in manager.GetComponents<TrialBase>()) trial.ResetMatch();
        manager.GetComponent<WindTrial>()?.ApplyNetworkWind(false, Quaternion.identity);
        manager.GetComponent<RepairItemSpawner>()?.ResetMatch();
        foreach (var visual in OnlineWorldVisual.All.ToArray())
            if (visual != null) { if (visual.kind == 5) visual.GetComponent<ParticleSystem>()?.Stop(); else Destroy(visual.gameObject); }
        foreach (var go in remoteVisuals.Values) if (go != null) Destroy(go);
        remoteVisuals.Clear();
    }

    void DestroyActors()
    {
        foreach (var actor in actors.Values) if (actor != null) { actor.gameObject.SetActive(false); Destroy(actor.gameObject); }
        actors.Clear();
    }

    void ResetLobbyWorld()
    {
        StopAllCoroutines(); DestroyActors(); ResetTrials(); manager.ResetOnlineMatch(null);
        manager.grid.Clear();
        roulette.gameCanvas.enabled = false; roulette.selectionCanvas.gameObject.SetActive(false);
        if (roulette.titleDecorations != null) roulette.titleDecorations.SetActive(true);
        var view = Camera.main != null ? Camera.main.GetComponent<QuarterViewCamera>() : null;
        if (view != null) { view.player = null; view.Fit(); }
        frontend.ShowLobby();
    }

    public async void LeaveRoom()
    {
        if (leaving || Busy) return;
        leaving = true; Busy = true; CurrentPhase = Phase.Leaving;
        await CleanupSession();
        if (!disposed) RestoreTitle();
        Busy = leaving = false;
    }

    async Task CleanupSession()
    {
        ISession current = session; session = null;
        if (registered && network != null && network.CustomMessagingManager != null)
        {
            network.CustomMessagingManager.UnregisterNamedMessageHandler(ControlChannel);
            network.CustomMessagingManager.UnregisterNamedMessageHandler(InputChannel);
            network.CustomMessagingManager.UnregisterNamedMessageHandler(PoseChannel);
        }
        registered = false;
        try { if (current != null) await current.LeaveAsync(); }
        catch (Exception e) { Debug.LogWarning("로비 나가기 실패: " + e.Message); }
        // 세션 종료 후에도 남은 연결이 있으면 정리한다.
        if (network != null && network.IsListening) network.Shutdown();
    }

    void RestoreTitle()
    {
        seriesActive = false; seriesRound = 0; nextRoundAt = 0; series.Clear();
        ResetLobbyWorld(); onlineMode = false; CurrentPhase = Phase.Title; members.Clear(); awaitingHello.Clear();
        manager.grid.networkReplica = false;
        frontend.OpenModes();
    }

    public void StartSolo()
    {
        if (Busy || onlineMode) return;
        // 솔로는 기존의 시작/씬 재로드 경로를 사용한다.
        var selectedSkin = originalPlayer.GetComponent<PlayerSkin>();
        if (selectedSkin != null)
        {
            var choice = (PlayerSkin.Skin)Mathf.Clamp(PlayerPrefs.GetInt("LionIceRun.Skin", (int)selectedSkin.skin), 0, 2);
            if (selectedSkin.skin != choice) { selectedSkin.skin = choice; selectedSkin.Apply(); }
        }
        originalPlayer.gameObject.SetActive(true); manager.player = originalPlayer;
        var botManager = manager.GetComponent<BotManager>();
        if (botManager != null && !botManager.enabled) { MapRoulette.AutoStartSolo = true; ReloadSoloScene(); return; }
        frontend.HidePanels(); roulette.selectionCanvas.gameObject.SetActive(true); roulette.BeginChoosing();
    }

    public void ReloadSoloScene() => StartCoroutine(ReloadSolo());
    IEnumerator ReloadSolo()
    {
        if (network != null) Destroy(network.gameObject);
        yield return null; // 이전 NetworkManager.Singleton 정리가 끝난 뒤 다음 씬을 연다.
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    async void OnDestroy()
    {
        disposed = true;
        if (Instance == this) Instance = null;
        if (network != null) { network.OnClientConnectedCallback -= OnConnected; network.OnClientDisconnectCallback -= OnDisconnected; }
        diveAction?.Dispose(); pushAction?.Dispose();
        if (session != null) await CleanupSession();
    }
}
