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
public class BatchOnlineRunner : MonoBehaviour
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
        grid.blockPrefab = block; grid.mapFile = "BigMap.json";
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
        network.NetworkConfig.EnableSceneManagement = false; network.NetworkConfig.ForceSamePrefabs = false; network.NetworkConfig.ProtocolVersion = 1;
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

        float limit = Time.unscaledTime + 180;
        if (host) online.CreateRoom("검증 방장", 0, 2);
        else
        {
            while (!File.Exists(codeFile) && Time.unscaledTime < limit) yield return null;
            if (!File.Exists(codeFile)) { File.WriteAllText(resultFile, "FAIL: no code"); Application.Quit(2); yield break; }
            online.JoinRoom(File.ReadAllText(codeFile).Trim(), "검증 친구", 1);
        }
        int completedRounds = 0; bool injectedFall = false, sawFall = false, sawReturn = false, checkedRound = false; bool ready = false, started = false, stopped = false, playing = false;
        float playAt = 0; Vector3 firstPosition = Vector3.zero; bool moved = false, cracked = false;
        while (Time.unscaledTime < limit)
        {
            if (online.CurrentPhase == OnlineGameSession.Phase.Lobby)
            {
                if (host && online.RoomCode.Length > 0 && !File.Exists(codeFile))
                { File.WriteAllText(codeFile + ".tmp", online.RoomCode); File.Move(codeFile + ".tmp", codeFile); }
                if (playing) { completedRounds++; playing = started = stopped = checkedRound = injectedFall = sawFall = sawReturn = moved = cracked = false; }
                if (online.Members.Count == 2)
                {
                    var local = online.Members.FirstOrDefault(m => m.clientId == network.LocalClientId);
                    if (local != null && !local.ready) { online.ToggleReady(); ready = true; }
                }
                if (host && online.CanStart && !started) { started = true; online.StartRound(); }
            }
            if (host && online.CurrentPhase == OnlineGameSession.Phase.Rolling && !stopped)
            { yield return new WaitForSecondsRealtime(0.7f); online.StopRoll(); stopped = true; }
            if (online.CurrentPhase == OnlineGameSession.Phase.Playing)
            {
                if (!playing) { playing = true; playAt = Time.unscaledTime; firstPosition = gm.player.transform.position; if (host) foreach (var brain in FindObjectsByType<BotBrain>(FindObjectsSortMode.None)) { brain.enabled = false; brain.GetComponent<PlayerController>().InputEnabled = false; } }
                if (Time.unscaledTime - playAt < 1.5f) InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                else InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                moved |= Vector3.Distance(gm.player.transform.position, firstPosition) > 0.5f;
                cracked |= grid.IceBlocks.Any(b => b.CurrentStage != IceBlock.Stage.Clean);
                if (host && !injectedFall && Time.unscaledTime - playAt > 2) { injectedFall = true; gm.player.ResetMotion(new Vector3(0, grid.SeaLevel - 2, 0)); }
                var hostActor = FindObjectsByType<OnlineActor>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(a => !a.spawn.bot && a.spawn.ownerId == 0);
                if (hostActor != null && gm.StatusOf(hostActor.controller, out bool fallen, out _, out _)) { sawFall |= fallen; sawReturn |= sawFall && !fallen && hostActor.transform.position.y > grid.SeaLevel; }
            }
            if (playing && Time.unscaledTime - playAt > 16)
            {
                bool passed = ready && moved && cracked && sawFall && sawReturn && gm.CompetitorCount == 4;
                string roundFile = Path.Combine(output, (host ? "host" : "client") + "-round" + completedRounds + ".txt");
                if (!checkedRound) {
                    File.WriteAllText(roundFile, (passed ? "PASS" : "FAIL") + $": moved={moved}, iceChanged={cracked}, fall={sawFall}, respawn={sawReturn}, count={gm.CompetitorCount}, map={grid.mapFile}");
                    checkedRound = true;
                }
                string peerFile = Path.Combine(output, (host ? "client" : "host") + "-round" + completedRounds + ".txt");
                if (File.Exists(peerFile)) {
                    if (completedRounds == 0) { if (host && online.CurrentPhase != OnlineGameSession.Phase.Lobby) online.ReturnToLobby(); }
                    else {
                        if (host) foreach (var actor in FindObjectsByType<OnlineActor>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (actor.controller != gm.player) gm.WithdrawOnline(actor.controller);
                        float resultDeadline = Time.unscaledTime + 12;
                        while (online.CurrentPhase != OnlineGameSession.Phase.Results && Time.unscaledTime < resultDeadline) yield return null;
                        bool resultPassed = online.CurrentPhase == OnlineGameSession.Phase.Results && online.ResultLines.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Length == 4;
                        passed &= resultPassed;
                        File.WriteAllText(resultFile, (passed ? "PASS" : "FAIL") + ": two rounds, code join, 4 actors, movement, ice, fall, respawn, results=" + resultPassed);
                        yield return new WaitForSecondsRealtime(host ? 2 : 1);
                        online.LeaveRoom(); yield return new WaitForSecondsRealtime(3);
                        Application.Quit(passed ? 0 : 3); yield break;
                    }
                }
            }
            if (online.CurrentPhase == OnlineGameSession.Phase.Title && !online.Busy)
            { File.WriteAllText(resultFile, "FAIL: " + online.Notice); Application.Quit(4); yield break; }
            yield return null;
        }
        File.WriteAllText(resultFile, "FAIL: timeout phase=" + online.CurrentPhase + ", notice=" + online.Notice);
        online.LeaveRoom(); yield return new WaitForSecondsRealtime(3); Application.Quit(5);
    }
    static TMP_Text Label() => new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
}
