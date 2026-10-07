using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class MovingSafetyRunner : MonoBehaviour
{
    int checks, failures;
    string output;
    void Check(bool condition, string message)
    {
        checks++; if (!condition) failures++;
        File.AppendAllText(output, (condition ? "PASS " : "FAIL ") + message + "\n");
    }
    IEnumerator Start()
    {
        output = Path.Combine(Application.dataPath, "../../moving-safety-results.txt");
        File.WriteAllText(output, "");
        yield return null; yield return null;
        var gm = GameManager.Instance; gm.enabled = false; gm.timer.enabled = false;
        foreach (var bot in FindObjectsByType<BotBrain>()) bot.enabled = false;
        foreach (var trial in FindObjectsByType<TrialBase>()) trial.enabled = false;
        foreach (var tracker in FindObjectsByType<IceStepTracker>()) tracker.enabled = false;
        Check(MovingSafeZones.HoldSeconds == 10 && MovingSafeZones.FadeSeconds == 2, "10초 유지·2초 보간");
        Check(MovingSafeZones.Appearance(0) == 1 && MovingSafeZones.Appearance(8) == 1
            && Mathf.Abs(MovingSafeZones.Appearance(9) - 0.5f) < 0.001f
            && MovingSafeZones.Appearance(10) == 0 && Mathf.Abs(MovingSafeZones.Appearance(11) - 0.5f) < 0.001f
            && MovingSafeZones.Appearance(12) == 1, "이동 전·후 안전 색 보간");
        int maps = 0;
        foreach (var file in MapData.ListFiles())
        {
            var map = MapData.Load(file); if (map.archived) continue;
            gm.grid.networkReplica = false; gm.grid.mapFile = file; gm.grid.Build();
            yield return null;
            var zones = gm.grid.SafeZones; zones.enabled = false;
            if (gm.grid.Gimmicks != null) gm.grid.Gimmicks.enabled = false;
            if (zones.Routes.Count == 0)
            {
                Check(string.Join("", map.rows).IndexOf('S') < 0, file + " 안전 시작 칸 없는 사용자 맵 유지");
                continue;
            }
            maps++;
            Check(zones.Routes.Count == 2, file + " 안전 경로 두 개");
            var used = new HashSet<IceBlock>();
            foreach (var route in zones.Routes)
            {
                bool adjacent = route.Count > 1;
                for (int i = 0; i < route.Count; i++)
                {
                    var a = route[i].Cell; var b = route[(i + 1) % route.Count].Cell;
                    adjacent &= Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) == 1;
                }
                Check(adjacent, file + " 빈칸을 건너뛰지 않는 인접 순환 경로");
                var local = new HashSet<IceBlock>(route);
                Check(!used.Overlaps(local), file + " 안전 경로 겹침 없음");
                used.UnionWith(local);
                if (route.Count < 2) continue;
                var old = route[0]; var next = route[1];
                next.ForceBreak();
                Check(next.IsBroken, file + " 다음 위치 파괴 준비");
                zones.ApplyTime(9.999f);
                old.ForceBreak(); Check(old.IsSafe && !old.IsBroken, file + " 10초 직전까지 보호");
                zones.ApplyTime(10);
                Check(!old.IsSafe && next.IsSafe && !next.IsBroken && next.GetComponent<Collider>().enabled,
                    file + " 10초 이동·파괴 칸 복구·콜라이더 활성");
                old.ForceAdvance(); Check(old.CurrentStage == IceBlock.Stage.Cracked, file + " 떠난 칸 다시 금감");
                next.ForceBreak(); Check(!next.IsBroken, file + " 새 위치 보호");
                // 다음 경로 검사 전 원점으로 돌린다.
                zones.ApplyTime(0);
            }
            int count = zones.Routes[0].Count;
            zones.ApplyTime(count * 10f);
            Check(zones.Routes[0][0].IsSafe, file + " 경로 반복");
            // 복제 PC는 예측 시각으로 보호 판정을 앞당기지 않는다.
            zones.ApplyTime(0); gm.grid.networkReplica = true;
            zones.ApplyTime(10, false);
            Check(zones.Routes[0][0].IsSafe && !zones.Routes[0][1].IsSafe, file + " 클라이언트 보호 예측 금지");
            zones.ApplyTime(10);
            var remote = zones.Routes[0][1]; remote.ApplyNetworkStage(IceBlock.Stage.Broken);
            Check(remote.IsSafe && !remote.IsBroken, file + " 스냅샷 보호 적용 후 파괴 상태 방어");
            var leaving = zones.Routes[0][0]; leaving.ApplyNetworkStage(IceBlock.Stage.Broken);
            Check(leaving.IsBroken, file + " 이전 안전 칸 원격 파괴 적용");
            gm.grid.networkReplica = false;
        }
        Check(maps == 12, "활성 12개 맵 검사");
        File.AppendAllText(output, $"MOVING_SAFETY_RESULT: {checks} checks, {failures} failures\n");
        Debug.Log($"MOVING_SAFETY_RESULT: {checks} checks, {failures} failures");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(failures == 0 ? 0 : 1);
#endif
    }
}
