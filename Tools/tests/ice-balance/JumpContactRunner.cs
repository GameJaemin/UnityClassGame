using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

public class JumpContactRunner : MonoBehaviour
{
    const string Output = "D:/UnityClassGame/Tools/tests/ice-balance/jump-contact-results.txt";
    int checks, failures;
    void Check(bool pass, string message)
    {
        checks++;
        if (!pass) failures++;
        File.AppendAllText(Output, (pass ? "PASS " : "FAIL ") + message + "\n");
    }
    IEnumerator Start()
    {
        File.WriteAllText(Output, "");
        yield return null;
        yield return null;
        var gm = GameManager.Instance;
        gm.enabled = false;
        foreach (var brain in FindObjectsByType<BotBrain>(FindObjectsSortMode.None)) brain.enabled = false;
        foreach (var step in FindObjectsByType<IceStepTracker>(FindObjectsSortMode.None)) step.enabled = false;
        foreach (var trial in FindObjectsByType<TrialBase>(FindObjectsSortMode.None)) trial.enabled = false;
        gm.ResetOnlineMatch(gm.player);
        gm.grid.mapFile = "default_6x6.json";
        gm.grid.Build();
        yield return null;
        var player = gm.player;
        var tracker = player.GetComponent<IceStepTracker>();
        tracker.enabled = true;
        player.useExternalInput = true;
        player.InputEnabled = true;
        var body = player.GetComponent<Rigidbody>();
        var blocks = gm.grid.IceBlocks.Where(b => !b.IsSafe).Take(8).ToArray();
        player.ResetMotion(gm.grid.CellTop(blocks[0].Cell) + Vector3.up * 1.02f);
        yield return new WaitForSeconds(0.15f);
        Check(blocks[0].CurrentStage == IceBlock.Stage.Clean, "준비 중 접지는 얼음을 깨지 않음");
        gm.BeginSelectedMatch();

        // 매 물리 프레임 점프를 예약: 접지 프레임에서 즉시 재점프하도록 한다.
        for (int i = 0; i < 3; i++)
        {
            var block = blocks[i];
            player.ResetMotion(gm.grid.CellTop(block.Cell) + Vector3.up * 1.02f);
            float end = Time.time + 0.5f;
            while (Time.time < end)
            {
                player.SetExternalInput(Vector3.zero, false, true, false);
                yield return new WaitForFixedUpdate();
                if (body.linearVelocity.y > 1) break;
            }
            Check(block.CurrentStage == IceBlock.Stage.Cracked && body.linearVelocity.y > 1,
                "즉시 점프한 새 칸의 밟힘 1회 " + i + " stage=" + block.CurrentStage);
        }
        var repeated = blocks[3];
        player.ResetMotion(gm.grid.CellTop(repeated.Cell) + Vector3.up * 1.02f);
        int launches = 0;
        bool wasRising = false;
        float deadline = Time.time + 6;
        while (launches < 3 && Time.time < deadline)
        {
            player.SetExternalInput(Vector3.zero, false, true, false);
            yield return new WaitForFixedUpdate();
            bool rising = body.linearVelocity.y > 1;
            if (rising && !wasRising) launches++;
            wasRising = rising;
        }
        Check(launches == 3, "실제 물리에서 연속 점프 3회 실행");
        Check(repeated.CurrentStage >= IceBlock.Stage.Shaking, "같은 칸의 예약 재점프 착지마다 금 진행");

        player.ResetMotion(gm.grid.CellTop(blocks[4].Cell) + Vector3.up * 4);
        yield return new WaitForSeconds(0.1f);
        Check(blocks[4].CurrentStage == IceBlock.Stage.Clean, "공중 통과만으로 밟힘 판정하지 않음");
        player.ResetMotion(gm.grid.CellTop(blocks[5].Cell) + Vector3.up * 1.02f);
        player.SetExternalInput(Vector3.zero, false, false, false);
        yield return new WaitForSeconds(0.2f);
        Check(blocks[5].CurrentStage == IceBlock.Stage.Cracked, "접지 유지 여러 물리 프레임에도 중복 금 방지");
        blocks[6].SetSafeProtection(true);
        player.ResetMotion(gm.grid.CellTop(blocks[6].Cell) + Vector3.up * 1.02f);
        player.SetExternalInput(Vector3.zero, false, true, false);
        yield return new WaitForSeconds(0.15f);
        Check(blocks[6].CurrentStage == IceBlock.Stage.Clean, "안전지대 점프 보호 유지");
        gm.grid.networkReplica = true;
        player.ResetMotion(gm.grid.CellTop(blocks[7].Cell) + Vector3.up * 1.02f);
        player.SetExternalInput(Vector3.zero, false, true, false);
        yield return new WaitForSeconds(0.15f);
        Check(blocks[7].CurrentStage == IceBlock.Stage.Clean, "클라이언트 얼음 상태를 임의로 변경하지 않음");
        gm.grid.networkReplica = false;
        File.AppendAllText(Output, $"RESULT checks={checks} failures={failures}\n");
        UnityEditor.EditorApplication.Exit(failures == 0 ? 0 : 1);
    }
}
