using System.IO;
using System.Linq;
using System.Collections;
using UnityEngine;
public class IceBalanceRunner : MonoBehaviour
{
    string output;
    int checks, failures;
    void Check(bool condition, string message) {
        checks++; File.AppendAllText(output, (condition ? "PASS " : "FAIL ") + message + "\n");
        if (!condition) failures++;
    }
    IEnumerator Start() {
        output = Path.Combine(Application.dataPath, "../../ice-balance-results.txt"); File.WriteAllText(output, "");
        yield return null; yield return null;
        var gm = GameManager.Instance; gm.enabled = false;
        foreach (var brain in FindObjectsByType<BotBrain>(FindObjectsSortMode.None)) brain.enabled = false;
        foreach (var trial in FindObjectsByType<TrialBase>(FindObjectsSortMode.None)) trial.enabled = false;
        foreach (var step in FindObjectsByType<IceStepTracker>(FindObjectsSortMode.None)) step.enabled = false;
        gm.ResetOnlineMatch(gm.player); gm.grid.mapFile = "default_6x6.json"; gm.grid.Build(); yield return null;
        var player = gm.player; var tracker = player.GetComponent<IceStepTracker>(); tracker.enabled = true;
        player.useExternalInput = true; player.InputEnabled = false;
        player.ResetMotion(gm.grid.CellTop(gm.grid.StartCell) + Vector3.up * 1.05f);
        var first = gm.grid.GetBlock(gm.grid.StartCell);
        yield return new WaitForSeconds(1);
        Check(first.CurrentStage == IceBlock.Stage.Clean && !player.HasMoved, "카운트다운/준비 중 보호");
        Check(gm.grid.standTimeToCrack == 4 && gm.grid.decayInterval == 12 && gm.grid.breakDelay == 2,
            "기존 Main 씬에도 느려진 시간 적용");
        gm.BeginSelectedMatch(); player.InputEnabled = false;
        float began = Time.time;
        yield return new WaitForSeconds(0.3f);
        Check(!player.HasMoved && first.CurrentStage == IceBlock.Stage.Cracked, "이동 입력 없이 시작 얼음 금감");
        yield return new WaitForSeconds(5.7f);
        Check(!first.IsBroken && first.CurrentStage < IceBlock.Stage.Shaking, "6초에는 일반 얼음 유지");
        while (!first.IsBroken && Time.time - began < 13) yield return null;
        Check(first.IsBroken && Time.time - began >= 9.5f, "서 있기 파괴 약 10초");
        File.AppendAllText(output, "IDLE breakSeconds=" + (Time.time - began) + "\n");
        tracker.enabled = false;
        foreach (string file in MapData.ListFiles()) {
            var map = MapData.Load(file); if (map.archived) continue;
            Check(map.rows.Sum(r => r.Count(c => c == 'S')) >= 2 && map.Get(map.StartCell) != MapData.Safe, file + " 안전지대와 일반 시작 칸");
            gm.timer.ApplyOnlineTime(0, false); gm.grid.mapFile = file; gm.grid.Build(); yield return null;
            var safe = gm.grid.IceBlocks.Where(b => b.IsSafe).ToArray();
            foreach (var block in safe) { block.Step(); block.ForceAdvance(); block.ForceBreak(); }
            Check(safe.Length >= 2 && safe.All(b => !b.IsBroken && b.CurrentStage == IceBlock.Stage.Clean && b.gameObject.activeSelf), file + " 밟기/함정 파괴 면역");
            gm.grid.networkReplica = true;
            foreach (var block in safe) block.ApplyNetworkStage(IceBlock.Stage.Broken);
            Check(safe.All(b => b.CurrentStage == IceBlock.Stage.Clean && b.gameObject.activeSelf), file + " 원격 안전지대 보호");
            gm.grid.networkReplica = false;
            if (gm.grid.Gimmicks == null) continue;
            gm.timer.ApplyOnlineTime(61, false); yield return new WaitForFixedUpdate();
            Check(gm.grid.IceBlocks.All(b => b.CurrentStage == IceBlock.Stage.Clean), file + " 60초 전체 붕괴 제거");
            gm.timer.ApplyOnlineTime(121, false); yield return new WaitForFixedUpdate();
            Check(safe.All(b => b.CurrentStage == IceBlock.Stage.Clean) && gm.grid.IceBlocks.Where(b => !b.IsSafe).All(b => b.CurrentStage == IceBlock.Stage.Cracked), file + " 120초 붕괴와 안전지대 유지");
        }
        gm.timer.ApplyOnlineTime(0, false); gm.grid.mapFile = "windmill_well.json"; gm.grid.Build(); yield return null;
        player.ResetMotion(gm.grid.StaticCellTop(new Vector2Int(8,7)) + Vector3.up);
        player.ReceivePush(Vector3.right, player);
        yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
        Check(player.State == PlayerController.MoveState.KnockedDown && !player.IsShoved && player.HorizontalSpeed > 8, "회전 막대 강한 타격과 기존 밀침 해제");
        Vector3 start = player.transform.position;
        yield return new WaitForSeconds(0.45f);
        Check(Vector3.Distance(start, player.transform.position) > 2, "넘어지면서 실제로 2m 이상 밀림");
        player.ResetMotion(gm.grid.StaticCellTop(new Vector2Int(8,7)) + Vector3.up);
        yield return new WaitForFixedUpdate();
        Check(player.State == PlayerController.MoveState.Normal, "회전 막대 연속 타격 보호");
        gm.timer.ApplyOnlineTime(0, false); gm.grid.Build(); yield return null;
        player.ResetMotion(gm.grid.StaticCellTop(new Vector2Int(8,7)) + Vector3.up);
        player.GetComponent<PlayerPowerUps>().GiveShield();
        yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
        Check(player.State == PlayerController.MoveState.Normal && !player.GetComponent<PlayerPowerUps>().HasShield, "강한 함정도 방패 1회 방어");
        gm.timer.ApplyOnlineTime(0, false); gm.grid.mapFile = "candy_carousel.json"; gm.grid.Build(); yield return null;
        gm.timer.ApplyOnlineTime(2, false); yield return new WaitForFixedUpdate();
        var rotating = FindObjectsByType<MapMovingSurface>(FindObjectsSortMode.None).First(s => s.RotationSpeed > 0);
        var probe = player.gameObject.AddComponent<BalanceCollisionProbe>(); probe.output = output;
        File.AppendAllText(output, $"SIDE root={rotating.transform.position} radius={player.GetComponent<CapsuleCollider>().radius} scale={player.transform.lossyScale} speed={rotating.RotationSpeed}\n");
        player.ResetMotion(rotating.transform.TransformPoint(new Vector3(1.9f, 0.2f, 0)));
        for (int i=0;i<5;i++) yield return new WaitForFixedUpdate();
        File.AppendAllText(output, $"SIDE player={player.transform.position} state={player.State} grounded={player.IsGrounded}\n");
        Check(player.State == PlayerController.MoveState.KnockedDown, "회전 발판 옆면 실제 충돌 타격");
        Check(gm.GetComponent<WindTrial>().strength == 22, "기존 씬의 바람 강화 적용");
        File.AppendAllText(output, $"RESULT checks={checks} failures={failures}\n");
        UnityEditor.EditorApplication.Exit(failures == 0 ? 0 : 1);
    }
}
public class BalanceCollisionProbe : MonoBehaviour
{
    public string output;
    void OnCollisionStay(Collision collision) {
        File.AppendAllText(output,"CONTACT "+collision.gameObject.name+" "+string.Join(",",collision.contacts.Select(c=>c.normal.ToString()+" @"+c.point))+"\n");
    }
}
