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

// 격리된 에디터에서 실행한다. 원본 씬이나 서비스 설정을 수정하지 않는다.
public class SocialRelayRunner : MonoBehaviour
{
    IEnumerator Start()
    {
        string[] args = Environment.GetCommandLineArgs();
        bool host = args.Contains("-smokeHost");
        string output = args.SkipWhile(a => a != "-smokeOutput").Skip(1).First();
        string codeFile = Path.Combine(output, "room-code.txt");
        string resultFile = Path.Combine(output, host ? "host-result.txt" : "client-result.txt");
        InputSystem.actions.Enable();
        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var gm = GameManager.Instance;
        var online = OnlineGameSession.Instance;
        var network = online.network;
        var r = online.roulette;
        GamePreferences.SetMapSelectionSpeed(host ? 0 : 2);
        if(args.Contains("-customTiming")) GamePreferences.SetMapSelectionSeconds(host?3.5f:10f);
        int checkedRoll=0;
        foreach (var camera in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None)) camera.enabled = false;
        int chatReceived = 0, emoteReceived = 0;
        bool chatSent = false, emoteSent = false, socialChecked = false, identityChanged = false;
        bool limited = false;
        online.ChatReceived += (author, text) => { if (text == "한글 채팅 검증") chatReceived++; };
        online.EmoteReceived += (player, emote) => emoteReceived++;
        yield return null;

        float limit = Time.unscaledTime + 300;
        // 접속 뒤 RegisterHandlers가 한 번 실행된 다음 아래에서 지연 수신기로 교체한다.
        bool delayInstalled = false, responseChecked = false;
        float responseAt = 0;
        Vector3 responseStart = Vector3.zero;
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
                if (!delayInstalled && online.Members.Count == 2)
                {
                    delayInstalled = true;
                    string channel = host ? "LionIceRun.Input.v1" : "LionIceRun.Pose.v1";
                    network.CustomMessagingManager.UnregisterNamedMessageHandler(channel);
                    int received = 0;
                    network.CustomMessagingManager.RegisterNamedMessageHandler(channel, (sender, reader) => {
                        received++;
                        if (received % 10 != 0) StartCoroutine(DeliverAfterDelay(online, sender, reader.ToArray(), host));
                    });
                }
                if (finished > 0) { Finish(resultFile, "FAIL: returned to lobby between rounds", 3); yield break; }
                if (host && online.RoomCode.Length > 0 && !File.Exists(codeFile))
                { File.WriteAllText(codeFile + ".tmp", online.RoomCode); File.Move(codeFile + ".tmp", codeFile); }
                if (online.Members.Count == 2)
                {
                    if (!chatSent)
                    { chatSent = online.SendChat("한글 채팅 검증"); limited = !online.SendChat("중복 메시지"); }
                    var local = online.Members.FirstOrDefault(m => m.clientId == network.LocalClientId);
                    if (local != null && !local.ready && readyCalls == 0) { online.ToggleReady(); readyCalls++; }
                }
                if (host && online.CanStart && !started) { started = true; online.StartRound(); }
            }
            if (online.CurrentPhase == OnlineGameSession.Phase.Rolling && r.stopButton != null && r.stopButton.gameObject.activeSelf)
            { Finish(resultFile, "FAIL: online stop button visible", 4); yield break; }
            if (online.CurrentPhase == OnlineGameSession.Phase.Rolling && checkedRoll!=online.SeriesRound)
            {
                checkedRoll=online.SeriesRound;
                var packet=(OnlinePacket)typeof(OnlineGameSession).GetField("roll",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(online);
                double seconds=packet.stopAt-packet.startAt+packet.duration+packet.selectionHold;
                double expected=args.Contains("-customTiming")?3.5:2;
                bool fast=Math.Abs(packet.stopAt-packet.startAt-expected*.3)<.01 && Math.Abs(packet.selectionHold-expected*.15)<.01 && Math.Abs(seconds-expected)<.01;
                File.AppendAllText(Path.Combine(output,host?"host-roll.txt":"client-roll.txt"),(fast?"PASS":"FAIL")+$": round={checkedRoll}, selectionSeconds={seconds:F3}, localSpeed={GamePreferences.MapSelectionSpeed}\n");
                if(!fast) { Finish(resultFile,"FAIL: host map timing not shared",9); yield break; }
            }
            if (online.CurrentPhase == OnlineGameSession.Phase.Playing)
            {
                if(args.Contains("-quickTiming"))
                {
                    bool separate=checkedRoll==1 && (args.Contains("-customTiming")?GamePreferences.MapSelectionSeconds==(host?3.5f:10f):GamePreferences.MapSelectionSpeed==(host?0:2));
                    File.WriteAllText(resultFile,(separate?"PASS":"FAIL")+$": host timing shared with localSeconds={GamePreferences.MapSelectionSeconds}");
                    yield return new WaitForSecondsRealtime(2); online.LeaveRoom(); yield return new WaitForSecondsRealtime(3);
                    UnityEditor.EditorApplication.Exit(separate?0:10); yield break;
                }
                if (lastPlaying != online.SeriesRound)
                {
                    lastPlaying = online.SeriesRound; playingAt = Time.unscaledTime; forced = false;
                    if (host) foreach (var brain in FindObjectsByType<BotBrain>(FindObjectsSortMode.None)) brain.enabled = false;
                }
                if (!host && lastPlaying == 1 && !responseChecked)
                {
                    if (responseAt == 0 && Time.unscaledTime - playingAt > 0.8f)
                    {
                        responseAt = Time.unscaledTime;
                        responseStart = gm.player.transform.position;
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D, Key.Space));
                    }
                    else if (responseAt > 0 && Time.unscaledTime - responseAt >= 0.10f)
                    {
                        responseChecked = true;
                        float moved = Vector3.Distance(responseStart, gm.player.transform.position);
                        bool responsive = gm.player.localPrediction && !gm.player.networkReplica && moved > 0.03f;
                        File.WriteAllText(Path.Combine(output, "response.txt"),
                            (responsive ? "PASS" : "FAIL") + ": local response at 100ms, delta=" + moved + ", one-way input/pose delay=180ms, drop=10%"
                            + $"\n prediction={gm.player.localPrediction}, replica={gm.player.networkReplica}, enabled={gm.player.InputEnabled}, active={gm.player.gameObject.activeSelf}, kinematic={gm.player.GetComponent<Rigidbody>().isKinematic}, state={gm.player.State}, move={InputSystem.actions.FindAction("Player/Move").ReadValue<Vector2>()}, jump={InputSystem.actions.FindAction("Player/Jump").IsPressed()}, keyboard={keyboard.dKey.isPressed}");
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                        if (!responsive) { Finish(resultFile, "FAIL: predicted input did not respond before Relay feedback", 10); yield break; }
                    }
                }
                if (lastPlaying == 1 && !emoteSent && Time.unscaledTime - playingAt > 1.2f)
                { emoteSent = online.SendEmote(host ? 1 : 2); }
                if (!host && lastPlaying == 1 && !identityChanged && Time.unscaledTime - playingAt > 1.6f)
                { identityChanged = true; GamePreferences.SetAnonymous(true); }
                if (lastPlaying == 1 && !socialChecked && Time.unscaledTime - playingAt > 3f)
                {
                    socialChecked = true;
                    bool alias = online.Members.Any(m => m.skin == 1 && m.nickname == "북극곰");
                    bool pass = chatReceived >= 2 && emoteReceived >= 2 && limited && alias;
                    File.WriteAllText(Path.Combine(output, host ? "host-social.txt" : "client-social.txt"),
                        (pass ? "PASS" : "FAIL") + $": chat={chatReceived}, emote={emoteReceived}, rateLimited={limited}, anonymous={alias}");
                    if (!pass) { online.LeaveRoom(); yield return new WaitForSecondsRealtime(3); Finish(resultFile, "FAIL: social delivery", 12); yield break; }
                }
                if (host && !forced && Time.unscaledTime - playingAt > 4)
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
    IEnumerator DeliverAfterDelay(OnlineGameSession online, ulong sender, byte[] bytes, bool input)
    {
        yield return new WaitForSecondsRealtime(0.18f);
        if (online == null || online.CurrentPhase == OnlineGameSession.Phase.Title) yield break;
        using (var reader = new Unity.Netcode.FastBufferReader(bytes, Unity.Collections.Allocator.Temp))
            typeof(OnlineGameSession).GetMethod(input ? "OnInput" : "OnPose",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(online, new object[] { sender, reader });
    }
    static void Finish(string path, string result, int code) { File.WriteAllText(path, result); UnityEditor.EditorApplication.Exit(code); }
    static TMP_Text Label() => new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
}
