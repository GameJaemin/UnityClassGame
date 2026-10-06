using System;
using System.Collections.Generic;
using System.Linq;

[Serializable] public class OnlineScore
{
    public ulong actorId;
    public string nickname;
    public bool bot;
    public int total, lastPlace, lastPoints;
}

// 호스트만 점수를 더하고 클라이언트는 확정된 표를 복사한다. 같은 라운드는 두 번 집계하지 않는다.
public class OnlineSeries
{
    public const int RoundCount = 5;
    public const double ResultsSeconds = 8;
    readonly List<OnlineScore> scores = new List<OnlineScore>();
    public int CompletedRounds { get; private set; }
    public bool Complete => CompletedRounds == RoundCount;
    public IReadOnlyList<OnlineScore> Scores => scores;
    public void Clear() { scores.Clear(); CompletedRounds = 0; }
    public void Begin(IEnumerable<OnlineSpawn> roster)
    {
        Clear();
        foreach (var spawn in roster) scores.Add(new OnlineScore { actorId = spawn.actorId, nickname = spawn.nickname, bot = spawn.bot });
    }
    public bool CompleteRound(int number, ulong[] ranking)
    {
        if (Complete || number != CompletedRounds + 1 || ranking == null || ranking.Length != scores.Count
            || ranking.Length < 2 || ranking.Length > 4 || ranking.Distinct().Count() != ranking.Length
            || ranking.Any(id => scores.All(s => s.actorId != id))) return false;
        for (int i = 0; i < ranking.Length; i++)
        {
            var entry = scores.Find(s => s.actorId == ranking[i]);
            entry.lastPlace = i + 1; entry.lastPoints = 4 - i; entry.total += entry.lastPoints;
        }
        CompletedRounds = number; return true;
    }
    public OnlineScore[] Snapshot() => scores.Select(s => new OnlineScore {
        actorId = s.actorId, nickname = s.nickname, bot = s.bot, total = s.total, lastPlace = s.lastPlace, lastPoints = s.lastPoints
    }).ToArray();
    public bool ApplySnapshot(int completed, OnlineScore[] entries)
    {
        if (completed < CompletedRounds || completed > RoundCount || entries == null || entries.Length < 2 || entries.Length > 4
            || entries.Any(s => s == null || s.total < 0 || s.total > RoundCount * 4)
            || entries.Select(s => s.actorId).Distinct().Count() != entries.Length) return false;
        Clear(); CompletedRounds = completed;
        scores.AddRange(entries.Select(s => new OnlineScore { actorId = s.actorId, nickname = s.nickname,
            bot = s.bot, total = s.total, lastPlace = s.lastPlace, lastPoints = s.lastPoints }));
        return true;
    }
    public OnlineScore[] Standings() => scores.OrderByDescending(s => s.total).ThenBy(s => s.actorId).ToArray();
    public string Winners()
    {
        var table = Standings(); if (table.Length == 0) return "";
        return string.Join(" · ", table.Where(s => s.total == table[0].total).Select(s => s.nickname));
    }
}
