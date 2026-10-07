using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
public class GimmickMapRunner : MonoBehaviour
{
    string output;
    int failures, checks;
    void Check(bool condition, string message) {
        checks++; File.AppendAllText(output, (condition ? "PASS " : "FAIL ") + message + "\n");
        if (!condition) failures++;
    }
    IEnumerator Start() {
        output = Path.Combine(Application.dataPath, "../../gimmick-results.txt"); File.WriteAllText(output, "");
        yield return null; yield return null;
        var gm = GameManager.Instance; gm.enabled = false;
        foreach (var brain in FindObjectsByType<BotBrain>(FindObjectsSortMode.None)) brain.enabled = false;
        foreach (var trial in FindObjectsByType<TrialBase>(FindObjectsSortMode.None)) trial.enabled = false;
        foreach (var step in FindObjectsByType<IceStepTracker>(FindObjectsSortMode.None)) step.enabled = false;
        gm.BeginSelectedMatch();
        int active = 0, themed = 0;
        foreach (string file in MapData.ListFiles()) {
            var map = MapData.Load(file); if (map.archived) continue; active++;
            Check(OnlineRules.TrySpawns(map, 4, out var spawns), file + " 4인 출발");
            if (map.gimmicks.Length == 0) continue; themed++;
            string hash = OnlineRules.MapHash(map); map.gimmicks[0].speed += 1;
            Check(hash != OnlineRules.MapHash(map), file + " 기믹 해시");
            gm.timer.ApplyOnlineTime(0, false); gm.grid.mapFile = file; gm.grid.Build(); yield return null;
            Check(gm.grid.Gimmicks != null && gm.grid.Gimmicks.DeviceCount == map.gimmicks.Length, file + " 생성");
            Check(FindObjectsByType<MapGimmickRig>(FindObjectsSortMode.None).Length == 1, file + " 이전 기믹 제거");
            var blocks = gm.grid.IceBlocks.ToArray(); var initial = blocks.Select(b => b.transform.position).ToArray();
            gm.timer.ApplyOnlineTime(2.75f, false); yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            if (map.gimmicks.Any(g => g.kind == "move" || g.kind == "rotate"))
            {
                Check(blocks.Where((b,i) => Vector3.Distance(b.transform.position, initial[i]) > 0.2f).Any(), file + " 얼음 이동/회전");
                File.AppendAllText(output, $"MOTION {file} max={blocks.Select((b,i)=>Vector3.Distance(b.transform.position,initial[i])).Max()} timer={gm.timer.Elapsed} grid={gm.grid.mapFile} rotated={string.Join(",", FindObjectsByType<MapMovingSurface>(FindObjectsSortMode.None).Select(s=>s.transform.eulerAngles.y.ToString()))}\n");
            }
            Check(MapPreview.Get(file, map) != null, file + " 룰렛 미리보기");
            foreach (var surface in FindObjectsByType<MapMovingSurface>(FindObjectsSortMode.None)) {
                var riderPoint = surface.PreviousPosition + Vector3.right;
                var expect = surface.transform.position + surface.transform.rotation * Quaternion.Inverse(surface.PreviousRotation) * Vector3.right
                    + surface.ConveyorVelocity * Time.fixedDeltaTime;
                Check(Vector3.Distance(riderPoint + surface.CarryDelta(riderPoint), expect) < 0.001f, file + " 접촉점 운반");
            }
            bool overlap = false;
            for (int sample = 0; sample < 20 && !overlap; sample++) {
                gm.timer.ApplyOnlineTime(sample * 1.5f, false); yield return new WaitForFixedUpdate();
                foreach (var a in blocks) {
                    var surface = a.GetComponentInParent<MapMovingSurface>();
                    if (surface == null || surface.ConveyorVelocity != Vector3.zero) continue;
                    foreach (var b in blocks) {
                        if (a == b || b.GetComponentInParent<MapMovingSurface>() == surface) continue;
                        var ca = a.GetComponent<Collider>(); var cb = b.GetComponent<Collider>();
                        if (!ca.bounds.Intersects(cb.bounds)) continue;
                        if (Physics.ComputePenetration(ca, a.transform.position, a.transform.rotation, cb, b.transform.position, b.transform.rotation,
                            out _, out float distance) && distance > 0.03f) { overlap = true; break; }
                    }
                    if (overlap) break;
                }
            }
            Check(!overlap, file + " 이동 경로의 얼음 간섭 없음");
            // 호스트 얼음 붕괴와 클라이언트의 판정 차단을 각각 검사한다.
            gm.grid.networkReplica = true; gm.timer.ApplyOnlineTime(121, false); yield return new WaitForFixedUpdate();
            Check(blocks.All(b => b.CurrentStage == IceBlock.Stage.Clean), file + " 클라이언트 붕괴 차단");
            gm.grid.networkReplica = false; yield return new WaitForFixedUpdate();
            Check(blocks.All(b => b.IsSafe || b.CurrentStage >= IceBlock.Stage.Cracked), file + " 120초 붕괴/안전지대 제외");
        }
        Check(active == 12 && themed == 8, "기본 4개 + 기믹 8개 선택 목록");
        gm.timer.ApplyOnlineTime(0, false); gm.grid.mapFile = "harbor_ferries.json"; gm.grid.Build(); yield return null;
        gm.player.useExternalInput = true; gm.player.InputEnabled = false;
        gm.player.ResetMotion(gm.grid.CellTop(new Vector2Int(6, 5)) + Vector3.up * 0.2f);
        for (int i = 0; i < 30; i++) yield return new WaitForFixedUpdate();
        Check(gm.player.IsGrounded && gm.player.GroundCollider.GetComponentInParent<MapMovingSurface>() != null, "이동 발판 실제 착지");
        var riding = gm.player.GroundCollider != null ? gm.player.GroundCollider.GetComponentInParent<MapMovingSurface>() : null;
        Vector3 original = gm.player.transform.position;
        Vector3 relative = riding != null ? riding.transform.InverseTransformPoint(original) : Vector3.zero;
        for (int i = 1; i <= 100; i++) { gm.timer.ApplyOnlineTime(i * 0.02f, false); yield return new WaitForFixedUpdate(); }
        Check(Vector3.Distance(original, gm.player.transform.position) > 0.5f, "이동 발판 실제 승객 이동");
        File.AppendAllText(output, $"RIDER original={original} current={gm.player.transform.position} grounded={gm.player.IsGrounded} surface={riding?.transform.position}\n");
        Check(riding != null && Vector3.Distance(relative, riding.transform.InverseTransformPoint(gm.player.transform.position)) < 0.6f, "승객 미끄러짐 제한");
        // 예고 중에는 맞지 않고 튀어나올 때만 실제 캐릭터를 밀친다.
        gm.timer.ApplyOnlineTime(0, false); gm.grid.mapFile = "piston_pass.json"; gm.grid.Build(); yield return null;
        gm.timer.ApplyOnlineTime(0.3f, false);
        gm.player.ResetMotion(gm.grid.StaticCellTop(new Vector2Int(5, 6)) + Vector3.up * 0.8f);
        gm.player.useExternalInput = true; gm.player.InputEnabled = false;
        yield return new WaitForFixedUpdate();
        Check(gm.player.State == PlayerController.MoveState.Normal, "피스톤 예고 중 무타격");
        gm.timer.ApplyOnlineTime(1.35f, false);
        gm.player.ResetMotion(gm.grid.StaticCellTop(new Vector2Int(5, 7)) + Vector3.up * 0.8f);
        yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
        Check(gm.player.State == PlayerController.MoveState.KnockedDown, "피스톤 실제 타격");
        File.AppendAllText(output, $"RESULT checks={checks} failures={failures}\n");
        UnityEditor.EditorApplication.Exit(failures == 0 ? 0 : 1);
    }
}
