using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

public class PartyStageRunner : MonoBehaviour
{
    string output;
    int checks, failures;
    void Check(bool value, string message)
    {
        checks++; if (!value) failures++;
        File.AppendAllText(output, (value ? "PASS " : "FAIL ") + message + "\n");
    }
    IEnumerator Start()
    {
        output = Path.Combine(Application.dataPath, "../../party-stage-results.txt"); File.WriteAllText(output, "");
        yield return null; yield return null;
        var gm = GameManager.Instance; gm.enabled = false; gm.timer.enabled = false;
        foreach (var bot in FindObjectsByType<BotBrain>()) bot.enabled = false;
        foreach (var trial in FindObjectsByType<TrialBase>()) trial.enabled = false;
        foreach (var tracker in FindObjectsByType<IceStepTracker>()) tracker.enabled = false;
        string[] files = { "seesaw_summit.json", "blink_boulevard.json", "bounce_carnival.json", "hammer_junction.json", "trick_garden.json", "sky_picnic.json" };
        foreach (var file in files)
        {
            gm.ResetOnlineMatch(gm.player); gm.grid.mapFile = file; gm.grid.networkReplica = false; gm.grid.Build();
            yield return null;
            gm.grid.SafeZones.enabled = false;
            gm.player.ResetMotion(gm.grid.StaticCellTop(gm.grid.StartCell) + Vector3.up * 1.05f);
            var map = gm.grid.Map;
            Check(map.gimmicks.All(g => MapGimmickData.IsSupported(g.kind)), file + " 기믹 종류");
            Check(gm.grid.Gimmicks.DeviceCount == map.gimmicks.Length, file + " 기믹 생성");
            Check(gm.grid.IceBlocks.Count == map.IceCount, file + " 얼음 생성 개수");
            Check(map.IsIce(map.playerStart.Cell) && map.botStarts.All(p => map.IsIce(p.Cell)), file + " 네 출발 칸");
            Check(OnlineRules.TrySpawns(map, 4, out var spawns) && spawns.Distinct().Count() == 4, file + " 온라인 시작 위치");
            Check(gm.grid.SafeZones.Routes.Count == 2 && gm.grid.SafeZones.Routes.All(r => r.Count > 1), file + " 이동 안전 경로 두 개");
            Check(MapPreview.Get(file, map) != null, file + " 룰렛 미리보기");
            gm.BeginSelectedMatch();
            gm.timer.ApplyOnlineTime(0, false); yield return new WaitForFixedUpdate();
            var surfaces = gm.grid.GetComponentsInChildren<MapMovingSurface>();
            var rotations = surfaces.Select(s => s.transform.rotation).ToArray();
            gm.timer.ApplyOnlineTime(1.5f, false); yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            if (map.gimmicks.Any(g => g.kind == "seesaw"))
                Check(surfaces.Where((s, i) => Quaternion.Angle(rotations[i], s.transform.rotation) > 5).Any(), file + " 시소 기울기 변화");
            foreach (var surface in surfaces)
            {
                Vector3 point = surface.PreviousPosition + Vector3.right;
                Vector3 expected = surface.transform.position + surface.transform.rotation * Quaternion.Inverse(surface.PreviousRotation) * Vector3.right;
                Check(Vector3.Distance(point + surface.CarryDelta(point), expected) < 0.001f, file + " 접촉점 운반");
            }
            if (map.gimmicks.Any(g => g.kind == "blink"))
            {
                var g = map.gimmicks.First(d => d.kind == "blink");
                var block = gm.grid.IceBlocks.First(b => Mathf.Abs(b.Cell.x - g.x) < g.width / 2 && Mathf.Abs(b.Cell.y - g.z) < g.depth / 2);
                gm.timer.ApplyOnlineTime(g.activeDuration + 0.2f - g.phase, false);
                yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
                Check(!block.SurfacePresent && !block.GetComponent<Collider>().enabled && gm.grid.GetBlock(block.Cell) == null, file + " 꺼짐·콜라이더·AI 경로 제외");
                block.ForceBreak();
                gm.timer.ApplyOnlineTime(g.period + 0.2f - g.phase, false);
                yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
                Check(block.SurfacePresent && block.IsBroken && !block.GetComponent<Collider>().enabled, file + " 다시 켜져도 파괴 상태 유지");
                block.Restore(); Check(block.GetComponent<Collider>().enabled, file + " 얼음 복구 연동");
                gm.grid.networkReplica = true;
                gm.timer.ApplyOnlineTime(g.activeDuration + 0.2f - g.phase, false);
                yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
                Check(!block.SurfacePresent, file + " 복제 PC 발판 주기 동일");
                gm.grid.networkReplica = false;
            }
            if (map.gimmicks.Any(g => g.kind == "launcher"))
            {
                gm.player.ResetMotion(gm.grid.StaticCellTop(gm.grid.StartCell) + Vector3.up * 1.05f);
                gm.player.Launch(8);
                Check(gm.player.Velocity.y >= 7.9f && gm.player.State != PlayerController.MoveState.KnockedDown, file + " 발사대 공중 조작·기절 없음");
                gm.player.networkReplica = true; var before = gm.player.GetComponent<Rigidbody>().linearVelocity;
                gm.player.Launch(9); Check(gm.player.GetComponent<Rigidbody>().linearVelocity == before, file + " 발사 호스트 판정");
                gm.player.networkReplica = false;
                var launch = map.gimmicks.First(g => g.kind == "launcher");
                gm.player.ResetMotion(gm.grid.StaticCellTop(new Vector2Int(Mathf.RoundToInt(launch.x), Mathf.RoundToInt(launch.z))) + Vector3.up * 1.05f);
                Physics.SyncTransforms();
                float maximum = 0;
                for (int tick = 0; tick < 10; tick++) { yield return new WaitForFixedUpdate(); maximum = Mathf.Max(maximum, gm.player.Velocity.y); }
                Check(maximum > 5, file + " 실제 발사대 접촉으로 점프");
            }
            if (map.gimmicks.Any(g => g.kind == "pendulum"))
            {
                var hammer = gm.grid.transform.Find("MapGimmicks/기믹_pendulum");
                var before = hammer.rotation;
                gm.timer.ApplyOnlineTime(2.1f, false); yield return new WaitForFixedUpdate();
                Check(Quaternion.Angle(before, hammer.rotation) > 3, file + " 해머 왕복 회전");
                gm.timer.ApplyOnlineTime(0, false); yield return new WaitForFixedUpdate();
                var head = hammer.GetComponentsInChildren<BoxCollider>().First(c => c.isTrigger);
                gm.player.ResetMotion(head.transform.position); Physics.SyncTransforms();
                for (int tick = 0; tick < 3; tick++) yield return new WaitForFixedUpdate();
                Check(gm.player.State == PlayerController.MoveState.KnockedDown, file + " 실제 해머 접촉 기절·밀침");
            }
        }
        File.AppendAllText(output, $"PARTY_STAGE_RESULT: {checks} checks, {failures} failures\n");
        Debug.Log($"PARTY_STAGE_RESULT: {checks} checks, {failures} failures");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(failures == 0 ? 0 : 1);
#endif
    }
}
