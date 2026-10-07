using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

// 맵 선택 때 씬을 다시 불러와도 같은 참가자의 누적 점수를 유지한다.
public static class SoloGameSeries
{
    static readonly OnlineSeries series = new OnlineSeries();
    static readonly Dictionary<PlayerController, ulong> actorIds = new Dictionary<PlayerController, ulong>();
    static readonly Dictionary<int, int> botSkins = new Dictionary<int, int>();
    static double nextRoundAt;
    static bool advancing;
    public static bool Active { get; private set; }
    public static int Round { get; private set; }
    public static bool Complete => Active && series.Complete;
    public static IReadOnlyList<OnlineScore> Scores => series.Scores;
    public static int LocalTotal => series.Scores.FirstOrDefault(s => s.actorId == 1)?.total ?? 0;
    public static string Title => Complete ? "5판 최종 결과" : $"{Round} / 5 라운드 결과";
    public static string Hint => Complete ? "합산 점수 우승: " + series.Winners()
        : OnlineSeries.ContinueHint(nextRoundAt - Time.realtimeSinceStartupAsDouble, Round + 1);
    public static string ResultLines
    {
        get
        {
            var lines = new StringBuilder();
            int place = 0, previous = -1, index = 0;
            foreach (var score in series.Standings())
            {
                index++; if (score.total != previous) place = index;
                previous = score.total;
                lines.Append(place).Append("등  ").Append(score.nickname).Append("  ").Append(score.total).Append("점");
                if (!Complete) lines.Append(" (+").Append(score.lastPoints).Append(")");
                lines.AppendLine();
            }
            return lines.ToString();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset()
    {
        Active = advancing = false; Round = 0; nextRoundAt = 0;
        series.Clear(); actorIds.Clear(); botSkins.Clear();
    }

    public static void Begin(GameManager manager)
    {
        Reset(); Active = true; Round = 1;
        var bodies = manager.Bodies();
        for (int i = 1; i < bodies.Count; i++)
            if (bodies[i].TryGetComponent<PlayerSkin>(out var skin)) botSkins[i - 1] = (int)skin.skin;
    }

    public static int BotSkin(int index, int randomSkin)
    {
        if (!Active) return randomSkin;
        if (!botSkins.TryGetValue(index, out int skin)) botSkins[index] = skin = randomSkin;
        return skin;
    }

    public static void MatchStarted(GameManager manager)
    {
        if (!Active) return;
        advancing = false;
        actorIds.Clear();
        var bodies = manager.Bodies();
        for (int i = 0; i < bodies.Count; i++) actorIds.Add(bodies[i], (ulong)i + 1);
        if (Round == 1)
            series.Begin(bodies.Select((body, i) => new OnlineSpawn { actorId = (ulong)i + 1, bot = i > 0,
                nickname = i == 0 ? GamePreferences.PublicName((int)manager.player.GetComponent<PlayerSkin>().skin) : $"NPC {i}" }));
    }

    public static void MatchFinished(GameManager manager)
    {
        if (!Active) return;
        var ranking = manager.RankedBodies().Select(body => actorIds[body]).ToArray();
        if (!series.CompleteRound(Round, ranking))
        { Debug.LogError("솔로 시리즈 참가자/점수를 확인하지 못했습니다."); Reset(); return; }
        nextRoundAt = Time.realtimeSinceStartupAsDouble + OnlineSeries.ResultsSeconds;
        if (series.Complete && LocalTotal == series.Standings()[0].total && manager.Winner != null)
            manager.RecordSoloSeriesWin();
    }

    public static void Update(GameManager manager)
    {
        if (!Active || Complete || advancing || manager.State != GameManager.GameState.GameOver
            || Time.realtimeSinceStartupAsDouble < nextRoundAt) return;
        advancing = true; Round++;
        MapRoulette.AutoStartSolo = true;
        ReloadScene();
    }

    public static void ReturnToTitle()
    {
        Reset(); MapRoulette.AutoStartSolo = false;
        Time.timeScale = 1f;
        ReloadScene();
    }

    static void ReloadScene()
    {
        if (OnlineGameSession.Instance != null && OnlineGameSession.Instance.isActiveAndEnabled)
            OnlineGameSession.Instance.ReloadSoloScene();
        else SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
