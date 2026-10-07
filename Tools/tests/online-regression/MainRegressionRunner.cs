#if UNITY_EDITOR || DEVELOPMENT_BUILD
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

// 복사 프로젝트의 실제 Main 씬에서만 실행한다. 원본 씬/PlayerPrefs/빌드를 수정하지 않는다.
public class MainRegressionRunner : MonoBehaviour
{
    static bool playerTestStarted;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void InitializePlayerTest()
    {
        if (!playerTestStarted && !Application.isEditor && Environment.GetCommandLineArgs().Contains("-mainOnlineRegression"))
        {
            playerTestStarted = true;
            new GameObject("MainRegression").AddComponent<MainRegressionRunner>();
        }
    }
    static void Exit(int code)
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(code);
#else
        Application.Quit(code);
#endif
    }
    IEnumerator Start()
    {
        string[] args = Environment.GetCommandLineArgs();
        bool host = args.Contains("-smokeHost");
        string output = args.SkipWhile(a => a != "-smokeOutput").Skip(1).First();
        string codeFile = Path.Combine(output, "room-code.txt");
        string resultFile = Path.Combine(output, host ? "host-result.txt" : "client-result.txt");
        yield return null;
        var online = OnlineGameSession.Instance;
        var network = online.network;
        var gm = online.manager;
        var r = online.roulette;
        var front = online.frontend;
        string traceFile = Path.Combine(output, host ? "host-trace.txt" : "client-trace.txt");
        OnlineGameSession.Phase previousPhase = (OnlineGameSession.Phase)(-1);
        float limit = Time.unscaledTime + 360;
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
            if (previousPhase != online.CurrentPhase)
            {
                previousPhase = online.CurrentPhase;
                File.AppendAllText(traceFile, $"{previousPhase} round={online.SeriesRound} members={online.Members.Count}\n");
            }
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
            if (host && online.CurrentPhase == OnlineGameSession.Phase.Loading)
                foreach (var member in online.Members.Where(m => m.clientId != network.LocalClientId))
                    member.lastCommandTime = online.ServerNow; // 준비 명령 직후에도 loaded 응답은 받아야 한다.
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
                    if (!passed) { Exit(6); yield break; }
                    if (host) front.returnButton.onClick.Invoke();
                    while ((online.CurrentPhase != OnlineGameSession.Phase.Lobby || online.Busy) && Time.unscaledTime < limit) yield return null;
                    if (online.CurrentPhase != OnlineGameSession.Phase.Lobby || online.Members.Count != 2)
                    { Finish(resultFile, "FAIL: final lobby did not retain both peers", 9); yield break; }
                    File.AppendAllText(resultFile, "PASS: final lobby retained both peers\n");
                    File.WriteAllText(Path.Combine(output, host ? "host-lobby.txt" : "client-lobby.txt"), "PASS");
                    string peerLobby = Path.Combine(output, host ? "client-lobby.txt" : "host-lobby.txt");
                    while (!File.Exists(peerLobby) && Time.unscaledTime < limit) yield return null;
                    // 다시 시작한 시리즈에서 클라이언트가 나가면 호스트는 취소 후 로비에 남는다.
                    online.ToggleReady();
                    if (host)
                    {
                        while (!online.CanStart && Time.unscaledTime < limit) yield return null;
                        front.startButton.onClick.Invoke();
                    }
                    while (online.CurrentPhase != OnlineGameSession.Phase.Playing && Time.unscaledTime < limit) yield return null;
                    if (!host)
                    {
                        while (!File.Exists(Path.Combine(output, "host-busy.txt")) && Time.unscaledTime < limit) yield return null;
                        front.matchLeaveButton.onClick.Invoke();
                    }
                    else
                    {
                        var busyProperty = typeof(OnlineGameSession).GetProperty("Busy");
                        busyProperty.SetValue(online, true);
                        File.WriteAllText(Path.Combine(output, "host-busy.txt"), "test disconnect during busy operation");
                        while (online.Members.Count != 1 && Time.unscaledTime < limit) yield return null;
                        busyProperty.SetValue(online, false);
                        while ((online.CurrentPhase != OnlineGameSession.Phase.Lobby || online.Busy || online.Members.Count != 1) && Time.unscaledTime < limit) yield return null;
                        if (online.CurrentPhase != OnlineGameSession.Phase.Lobby || online.Members.Count != 1)
                        { Finish(resultFile, "FAIL: client departure did not cancel series", 10); yield break; }
                        front.leaveButton.onClick.Invoke();
                    }
                    while ((online.CurrentPhase != OnlineGameSession.Phase.Title || online.Busy) && Time.unscaledTime < limit) yield return null;
                    front.backButton.onClick.Invoke();
                    if (!r.selectionCanvas.gameObject.activeSelf || !r.titleGroup.activeSelf || r.rouletteGroup.activeSelf || !r.startButton.interactable
                        || OnlineGameSession.InRoom || network.IsListening || network.ShutdownInProgress)
                    { Finish(resultFile, "FAIL: title left roulette/network state active", 11); yield break; }
                    r.startButton.onClick.Invoke();
                    if (!front.modesPanel.activeSelf) { Finish(resultFile, "FAIL: title start button unresponsive", 12); yield break; }
                    File.AppendAllText(resultFile, "PASS: departure cancellation, title restoration, title start button\n");
                    // 같은 NetworkManager로 방을 다시 만들고 닫을 수 있어야 한다.
                    online.CreateRoom(host ? "재접속 방장" : "재접속 친구", 0, 2);
                    while (online.Busy && Time.unscaledTime < limit) yield return null;
                    if (online.CurrentPhase != OnlineGameSession.Phase.Lobby) { Finish(resultFile, "FAIL: room recreation", 13); yield break; }
                    online.LeaveRoom();
                    while (online.Busy && Time.unscaledTime < limit) yield return null;
                    if (online.CurrentPhase != OnlineGameSession.Phase.Title) { Finish(resultFile, "FAIL: recreated room departure", 14); yield break; }
                    File.AppendAllText(resultFile, "PASS: room recreation and departure\n");
                    DontDestroyOnLoad(gameObject);
                    front.soloButton.onClick.Invoke();
                    while (OnlineGameSession.Instance == online && Time.unscaledTime < limit) yield return null;
                    yield return null;
                    var solo = OnlineGameSession.Instance;
                    if (solo == null || OnlineGameSession.InRoom || !solo.roulette.rouletteGroup.activeSelf
                        || !solo.manager.GetComponent<BotManager>().enabled || Time.timeScale != 0f)
                    { Finish(resultFile, "FAIL: online to solo selection", 15); yield break; }
                    solo.roulette.stopButton.onClick.Invoke();
                    while ((GameManager.Instance == null || GameManager.Instance.State != GameManager.GameState.Playing) && Time.unscaledTime < limit) yield return null;
                    if (GameManager.Instance == null || GameManager.Instance.State != GameManager.GameState.Playing || Time.timeScale != 1f)
                    { Finish(resultFile, "FAIL: solo countdown/start after online", 16); yield break; }
                    File.AppendAllText(resultFile, "PASS: online to solo selection/countdown/play\n");
                    Exit(0); yield break;
                }
            }
            if (online.CurrentPhase == OnlineGameSession.Phase.Title && !online.Busy)
            { Finish(resultFile, "FAIL: " + online.Notice, 7); yield break; }
            yield return null;
        }
        File.WriteAllText(resultFile, "FAIL: timeout phase=" + online.CurrentPhase + ", notice=" + online.Notice);
        online.LeaveRoom(); yield return new WaitForSecondsRealtime(3); Exit(8);
    }
    static void Finish(string path, string result, int code) { File.WriteAllText(path, result); Exit(code); }
    static TMP_Text Label() => new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
}


#endif

