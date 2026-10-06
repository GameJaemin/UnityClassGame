using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class OnlineSeriesTests
{
    [MenuItem("LionIceRun/온라인 5판 시리즈 규칙 검사")]
    public static void Run()
    {
        int checks = 0;
        void Check(bool value, string name) { if (!value) throw new Exception("시리즈 검사 실패: " + name); checks++; }
        var roster = Enumerable.Range(1, 4).Select(i => new OnlineSpawn { actorId = (ulong)i, nickname = "참가자" + i, bot = i > 2 }).ToArray();
        var series = new OnlineSeries(); series.Begin(roster);
        Check(series.CompleteRound(1, new ulong[] { 1, 2, 3, 4 }), "첫 라운드 집계");
        Check(series.Scores.Select(s => s.total).SequenceEqual(new[] { 4, 3, 2, 1 }), "순위별 4·3·2·1점");
        Check(!series.CompleteRound(1, new ulong[] { 1, 2, 3, 4 }) && series.Scores.Sum(s => s.total) == 10, "중복 결과로 점수 추가 방지");
        Check(!series.CompleteRound(3, new ulong[] { 1, 2, 3, 4 }), "건너뛴 라운드 거부");
        Check(!series.CompleteRound(2, new ulong[] { 1, 1, 3, 4 }), "중복 참가자 순위 거부");
        Check(!series.CompleteRound(2, new ulong[] { 1, 2, 3, 99 }), "다른 참가자 순위 거부");
        Check(!series.CompleteRound(2, new ulong[] { 1, 2 }), "일부 순위로 집계 방지");
        Check(series.CompletedRounds == 1 && series.Scores.Sum(s => s.total) == 10, "실패 집계는 점수/진행 유지");
        for (int round = 2; round <= 5; round++)
        {
            ulong winner = (ulong)((round - 1) % 4 + 1);
            var ranking = new[] { winner }.Concat(roster.Select(s => s.actorId).Where(id => id != winner)).ToArray();
            Check(series.CompleteRound(round, ranking), "연속 라운드 집계: " + round);
        }
        Check(series.Complete && series.Scores.Select(s => s.total).SequenceEqual(new[] { 17, 14, 11, 8 }), "5판 최종 합산");
        Check(series.Winners() == "참가자1", "최종 우승은 합산으로 결정");
        var differentLastWinner = new OnlineSeries(); differentLastWinner.Begin(roster);
        for (int number = 1; number <= 5; number++)
        {
            ulong winner = (ulong)(number == 5 ? 2 : number);
            differentLastWinner.CompleteRound(number, new[] { winner }.Concat(roster.Select(s => s.actorId).Where(id => id != winner)).ToArray());
        }
        Check(differentLastWinner.Winners() == "참가자1" && differentLastWinner.Scores[0].lastPlace == 2,
            "마지막 판 승자와 합산 우승자가 다를 때 합산 우선");
        Check(!series.CompleteRound(6, new ulong[] { 4, 3, 2, 1 }), "6판 자동 진행 방지");
        var client = new OnlineSeries();
        var packet = JsonUtility.FromJson<OnlinePacket>(JsonUtility.ToJson(new OnlinePacket { kind = "world", scores = series.Snapshot(), completedRounds = 5, seriesRound = 5 }));
        Check(client.ApplySnapshot(packet.completedRounds, packet.scores) && client.Complete && client.Winners() == series.Winners(), "점수 패킷 JSON 왕복/클라이언트 결과");
        Check(client.ApplySnapshot(5, packet.scores) && client.Scores.Sum(s => s.total) == 50, "반복 스냅샷은 누적하지 않음");
        Check(!client.ApplySnapshot(4, packet.scores) && client.Complete, "오래된 점수 스냅샷 거부");
        packet.scores[0].total = 0;
        Check(client.Scores[0].total == 17, "점수 스냅샷 참조 분리");
        series.Begin(roster);
        for (int round = 1; round <= 4; round++)
            series.CompleteRound(round, Enumerable.Range(0, 4).Select(i => (ulong)((i + round - 1) % 4 + 1)).ToArray());
        Check(series.Standings().All(s => s.total == 10) && series.Winners().Split('·').Length == 4, "동점은 공동 순위");
        series.Begin(roster.Take(2)); series.CompleteRound(1, new ulong[] { 2, 1 });
        Check(series.Scores.Select(s => s.total).SequenceEqual(new[] { 3, 4 }), "봇 없는 2인 경기 점수");
        series.Clear(); Check(series.CompletedRounds == 0 && series.Scores.Count == 0 && !series.Complete, "새 시리즈 초기화");
        Debug.Log("ONLINE_SERIES_CHECKS_PASSED: " + checks + "개");
    }
}
