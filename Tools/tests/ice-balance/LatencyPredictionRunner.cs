using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;

public class LatencyPredictionRunner : MonoBehaviour
{
    const string Output = "D:/UnityClassGame/Tools/tests/ice-balance/latency-prediction-results.txt";
    int checks, failures;
    void Check(bool pass, string message)
    {
        checks++; if (!pass) failures++;
        File.AppendAllText(Output, (pass ? "PASS " : "FAIL ") + message + "\n");
    }
    IEnumerator Start()
    {
        File.WriteAllText(Output, "");
        var history = new OnlineMotionHistory();
        history.Record(10, Vector3.right); history.Record(11, Vector3.right * 2);
        Check(history.Correction(10, Vector3.right * 0.8f, out var error) && Mathf.Abs(error.x + 0.2f) < 0.001f,
            "과거 처리 입력과 비교하여 아직 처리 안 된 이동을 보존");
        Check(history.Correction(10, Vector3.right * 0.8f, out error) && error.sqrMagnitude < 0.001f,
            "동일 응답을 반복 받아도 보정 누적 없음");
        Check(history.Correction(11, Vector3.right * 1.8f, out error) && error.sqrMagnitude < 0.001f,
            "미처리 이동 이력도 같은 보정 좌표계 적용");
        Check(!history.Correction(9, Vector3.zero, out error), "이미 지난 입력 응답을 임의 위치 보정으로 쓰지 않음");
        history.Clear(); Check(!history.Correction(11, Vector3.zero, out error), "부활/라운드 전환 시 이력 초기화");
        history.Record(uint.MaxValue, Vector3.one); history.Record(0, Vector3.one * 2);
        Check(history.Correction(0, Vector3.one * 2, out error) && !history.Correction(uint.MaxValue, Vector3.zero, out error),
            "입력 순번 순환 이후 오래된 이력 제거");
        history.Record(1, Vector3.zero); history.Record(257, Vector3.one);
        Check(!history.Correction(1, Vector3.zero, out error), "제한된 이력 버퍼에서 덮인 순번 거부");
        Check(OnlineRules.Protocol == 4, "입력/확인 순번 형식이 바뀐 프로토콜 분리");
        yield return null; yield return null;
        var gm = GameManager.Instance; gm.enabled = false;
        foreach (var brain in FindObjectsByType<BotBrain>(FindObjectsSortMode.None)) brain.enabled = false;
        foreach (var trial in FindObjectsByType<TrialBase>(FindObjectsSortMode.None)) trial.enabled = false;
        var session = OnlineGameSession.Instance; session.enabled = false;
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(OnlineGameSession).GetField("onlineMode", flags).SetValue(session, true);
        typeof(OnlineGameSession).GetField("<CurrentPhase>k__BackingField", flags).SetValue(session, OnlineGameSession.Phase.Playing);
        gm.ResetOnlineMatch(gm.player); gm.grid.mapFile = "default_6x6.json"; gm.grid.Build();
        gm.grid.networkReplica = true;
        yield return null;
        var player = gm.player;
        player.useExternalInput = true; player.localPrediction = true; player.networkReplica = false;
        player.InputEnabled = true;
        var body = player.GetComponent<Rigidbody>();
        var actor = player.gameObject.AddComponent<OnlineActor>(); actor.controller = player;
        actor.spawn = new OnlineSpawn { actorId = 1 };
        var start = gm.grid.CellTop(gm.grid.StartCell) + Vector3.up * 1.02f;
        player.ResetMotion(start); gm.BeginSelectedMatch();
        yield return new WaitForSeconds(0.12f);
        Check(gm.grid.GetBlock(gm.grid.StartCell).CurrentStage == IceBlock.Stage.Clean, "로컬 예측은 얼음을 파괴하지 않음");
        Vector3 before = body.position;
        player.SetExternalInput(Vector3.right, false, false, false, false, 1);
        yield return new WaitForSeconds(0.08f);
        Check(body.position.x > before.x + 0.025f, "호스트 응답 없이 80ms 안에 실제 물리 이동");
        player.ResetMotion(start);
        yield return new WaitForSeconds(0.08f);
        player.SetExternalInput(Vector3.zero, true, true, false, false, 2);
        yield return new WaitForSeconds(0.06f);
        Check(body.linearVelocity.y > 1 && player.SimulatedInputSequence == 2, "호스트 응답 없이 점프/실제 처리 순번 반영");
        actor.ApplyPose(start, Quaternion.identity, Vector3.zero, PlayerController.MoveState.Normal, true,
            -1, false, 0, true, false, 0, false, 0);
        player.ResetMotion(start); yield return new WaitForSeconds(0.08f);
        player.SetExternalInput(Vector3.right, false, false, true, false, 3);
        yield return new WaitForSeconds(0.04f);
        Check(player.State == PlayerController.MoveState.Diving, "호스트 응답 없이 다이브 반응");
        player.ApplyPredictionAuthority(Vector3.left * 8 + Vector3.up * 3, PlayerController.MoveState.KnockedDown, 1, 0, 0, false);
        Check(player.State == PlayerController.MoveState.KnockedDown && body.linearVelocity.x < -7,
            "호스트가 확정한 넘어짐과 충격 반영");
        player.ApplyPredictionAuthority(Vector3.zero, PlayerController.MoveState.Normal, 0, 0, 3, false);
        Check(player.BoostRemaining > 2.8f, "호스트 슈퍼 점프 효과 적용");
        actor.ApplyPose(start, Quaternion.identity, Vector3.zero, PlayerController.MoveState.Normal, true,
            -1, false, 0, false, true, 10, false, 3);
        Check(!player.gameObject.activeSelf && body.isKinematic, "호스트 사망 상태에서 예측 물리 정지");
        actor.ApplyPose(start, Quaternion.identity, Vector3.zero, PlayerController.MoveState.Normal, false,
            -1, false, 0, true, false, 0, false, 3);
        Check(player.gameObject.activeSelf && !body.isKinematic && Vector3.Distance(body.position, start) < 0.01f,
            "부활 위치로 초기화하고 예측 물리 재개");
        bool hadRoutes = gm.grid.SafeZones.Routes.Count > 0;
        gm.grid.Clear(); yield return null; yield return null;
        gm.grid.SafeZones.ApplyTime(20);
        Check(hadRoutes && gm.grid.SafeZones.Routes.Count == 0 && gm.grid.SafeZones.Cycle == 2,
            "얼음 삭제 뒤 안전지대의 이전 경로 참조 해제");
        gm.grid.Build(); yield return null;
        Check(gm.grid.SafeZones.Routes.Count > 0 && gm.grid.SafeZones.Routes[0][0] != null,
            "그리드 재생성 뒤 새 안전지대 경로 연결");
        File.AppendAllText(Output, $"RESULT checks={checks} failures={failures}\n");
        UnityEditor.EditorApplication.Exit(failures == 0 ? 0 : 1);
    }
}
