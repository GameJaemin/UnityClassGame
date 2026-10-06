using System;
using System.IO;
using System.Linq;
using System.Collections;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// 격리된 검증 빌드에서만 실행한다. 원본 씬이나 서비스 설정을 수정하지 않는다.
public class SeriesPlayRunner : MonoBehaviour
{
    IEnumerator Start()
    {
        string[] args = Environment.GetCommandLineArgs();
        bool host = args.Contains("-smokeHost");
        string output = args.SkipWhile(a => a != "-smokeOutput").Skip(1).First();
        string codeFile = Path.Combine(output, "room-code.txt");
        string resultFile = Path.Combine(output, host ? "host-result.txt" : "client-result.txt");
        var input = ScriptableObject.CreateInstance<InputActionAsset>();
        var map = input.AddActionMap("Player");
        map.AddAction("Move", InputActionType.Value).AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s").With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
        map.AddAction("Jump", InputActionType.Button, "<Keyboard>/space");
        input = InputSystem.actions; input.Enable();
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var camera = new GameObject("Camera", typeof(Camera), typeof(QuarterViewCamera));
        camera.tag = "MainCamera";

        var gridRoot = new GameObject("IceGrid"); gridRoot.SetActive(false);
        var grid = gridRoot.AddComponent<IceGrid>();
        var block = GameObject.CreatePrimitive(PrimitiveType.Cube); block.transform.position = Vector3.one * 10000;
        grid.blockPrefab = block; grid.mapFile = MapData.ListFiles().First();
        camera.GetComponent<QuarterViewCamera>().grid = grid;

        var actorTemplate = new GameObject("ActorTemplate"); actorTemplate.SetActive(false);
        actorTemplate.AddComponent<CapsuleCollider>().height = 2;
        actorTemplate.AddComponent<Rigidbody>();
        var templateController = actorTemplate.AddComponent<PlayerController>(); templateController.grid = grid;
        actorTemplate.AddComponent<IceStepTracker>().grid = grid;
        actorTemplate.AddComponent<CharacterAnimator>();
        actorTemplate.AddComponent<PlayerSkin>(); actorTemplate.AddComponent<PlayerPowerUps>();
        var original = Instantiate(actorTemplate); original.SetActive(true);

        var systems = new GameObject("Systems"); systems.SetActive(false);
        var timer = systems.AddComponent<SurvivalTimer>(); timer.showDebugHud = false;
        var gm = systems.AddComponent<GameManager>(); gm.player = original.GetComponent<PlayerController>(); gm.grid = grid; gm.timer = timer;
        systems.AddComponent<BotManager>().grid = grid;
        var networkRoot = new GameObject("Network", typeof(UnityTransport), typeof(NetworkManager));
        var network = networkRoot.GetComponent<NetworkManager>();
        network.NetworkConfig = new NetworkConfig();
        network.NetworkConfig.NetworkTransport = networkRoot.GetComponent<UnityTransport>();
        network.NetworkConfig.EnableSceneManagement = false; network.NetworkConfig.ForceSamePrefabs = false; network.NetworkConfig.ProtocolVersion = OnlineRules.Protocol;
        var r = systems.AddComponent<MapRoulette>(); r.enabled = false;
        r.manager = gm; r.gameCanvas = new GameObject("GameCanvas", typeof(Canvas)).GetComponent<Canvas>();
        r.selectionCanvas = new GameObject("SelectionCanvas", typeof(Canvas)).GetComponent<Canvas>();
        r.titleGroup = new GameObject("Title"); r.rouletteGroup = new GameObject("Roll");
        r.startButton = new GameObject("Start", typeof(UnityEngine.UI.Button)).GetComponent<UnityEngine.UI.Button>();
        r.stopButton = new GameObject("Stop", typeof(UnityEngine.UI.Button)).GetComponent<UnityEngine.UI.Button>();
        r.status = Label(); r.buttonText = Label();
        r.cards = new RectTransform[3]; r.previews = new UnityEngine.UI.Image[3]; r.names = new TMP_Text[3];
        for (int i = 0; i < 3; i++)
        { r.cards[i] = new GameObject("Card", typeof(RectTransform)).GetComponent<RectTransform>(); r.previews[i] = new GameObject("Preview", typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>(); r.names[i] = Label(); }
        var frontRoot = new GameObject("Frontend"); var front = frontRoot.AddComponent<OnlineFrontend>(); front.enabled = false;
        front.modesPanel = new GameObject("Modes"); front.lobbyPanel = new GameObject("Lobby"); front.loadingPanel = new GameObject("Loading");
        front.resultsPanel = new GameObject("Results"); front.leavePanel = new GameObject("Leave"); front.countdownText = Label();
        var online = systems.AddComponent<OnlineGameSession>(); online.network = network; online.manager = gm; online.roulette = r; online.frontend = front; online.actorPrefab = actorTemplate; front.session = online;
        gridRoot.SetActive(true); systems.SetActive(true);
        yield return null;

        float limit = Time.unscaledTime + 240;
        if (host) online.CreateRoom("검증 방장", 0, 2);
        else
        {
            while (!File.Exists(codeFile) && Time.unscaledTime < limit) yield return null;
            if (!File.Exists(codeFile)) { Finish(resultFile, "FAIL: no room code", 2); yield break; }
            online.JoinRoom(File.ReadAllText(codeFile).Trim(), "검증 친구", 1);
        }
        int readyCalls = 0, lastPlaying = 0, finished = 0;
        bool started = false, forced = false;
        float playingAt = 0;
        while (Time.unscaledTime < limit)
        {
            if (online.CurrentPhase == OnlineGameSession.Phase.Lobby)
            {
                if (finished > 0) { Finish(resultFile, "FAIL: returned to lobby between rounds", 3); yield break; }
                if (host && online.RoomCode.Length > 0 && !File.Exists(codeFile))
                { File.WriteAllText(codeFile + ".tmp", online.RoomCode); File.Move(codeFile + ".tmp", codeFile); }
                if (online.Members.Count == 2)
                {
                    var local = online.Members.FirstOrDefault(m => m.clientId == network.LocalClientId);
                    if (local != null && !local.ready && readyCalls == 0) { online.ToggleReady(); readyCalls++; }
                }
                if (host && online.CanStart && !started) { started = true; online.StartRound(); }
            }
            if (online.CurrentPhase == OnlineGameSession.Phase.Rolling && r.stopButton.gameObject.activeSelf)
            { Finish(resultFile, "FAIL: online stop button visible", 4); yield break; }
            if (online.CurrentPhase == OnlineGameSession.Phase.Playing)
            {
                if (lastPlaying != online.SeriesRound)
                {
                    lastPlaying = online.SeriesRound; playingAt = Time.unscaledTime; forced = false;
                    if (host) foreach (var brain in FindObjectsByType<BotBrain>(FindObjectsSortMode.None)) brain.enabled = false;
                }
                if (host && !forced && Time.unscaledTime - playingAt > 2)
                {
                    forced = true;
                    ulong winner = (ulong)((online.SeriesRound - 1) % 4 + 1);
                    foreach (var actor in FindObjectsByType<OnlineActor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                        if (actor.spawn.actorId != winner) gm.WithdrawOnline(actor.controller);
                }
            }
            if (online.CurrentPhase == OnlineGameSession.Phase.Results)
            {
                finished = Math.Max(finished, online.SeriesRound);
                if (!online.Members.All(m => m.ready)) { Finish(resultFile, "FAIL: ready was reset", 5); yield break; }
                if (online.SeriesComplete)
                {
                    var series = (OnlineSeries)typeof(OnlineGameSession).GetField("series", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(online);
                    int[] totals = series.Scores.OrderBy(s => s.actorId).Select(s => s.total).ToArray();
                    bool passed = finished == 5 && readyCalls == 1 && totals.SequenceEqual(new[] { 17, 14, 11, 8 });
                    File.WriteAllText(resultFile, (passed ? "PASS" : "FAIL") + $": rounds={finished}, readyCalls={readyCalls}, totals={string.Join(",", totals)}, localTotal={online.LocalTotal}\n" + online.ResultLines);
                    string peerFile = Path.Combine(output, host ? "client-result.txt" : "host-result.txt");
                    while (!File.Exists(peerFile) && Time.unscaledTime < limit) yield return null;
                    yield return new WaitForSecondsRealtime(host ? 3 : 1);
                    online.LeaveRoom(); yield return new WaitForSecondsRealtime(3);
                    UnityEditor.EditorApplication.Exit(passed ? 0 : 6); yield break;
                }
            }
            if (online.CurrentPhase == OnlineGameSession.Phase.Title && !online.Busy)
            { Finish(resultFile, "FAIL: " + online.Notice, 7); yield break; }
            yield return null;
        }
        File.WriteAllText(resultFile, "FAIL: timeout phase=" + online.CurrentPhase + ", notice=" + online.Notice);
        online.LeaveRoom(); yield return new WaitForSecondsRealtime(3); UnityEditor.EditorApplication.Exit(8);
    }
    static void Finish(string path, string result, int code) { File.WriteAllText(path, result); UnityEditor.EditorApplication.Exit(code); }
    static TMP_Text Label() => new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
}