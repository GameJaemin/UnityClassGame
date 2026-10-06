using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

// 서비스 코드와 경기 데이터는 구분한다. 모든 명령에는 방/라운드 상태 검사를 적용한다.
[Serializable] public class OnlineMember
{
    public ulong clientId;
    public string nickname;
    public int skin;
    public bool ready;
    public OnlineMapStamp[] maps;
    [NonSerialized] public uint lastInput;
    [NonSerialized] public double lastInputTime;
    [NonSerialized] public double lastCommandTime;
}
[Serializable] public class OnlineMapStamp { public string file; public string hash; }
[Serializable] public class OnlineSpawn
{
    public ulong actorId;
    public ulong ownerId;
    public string nickname;
    public int skin;
    public bool bot;
    public bool aggressive;
    public int x, y;
}
[Serializable] public class OnlineVisualState
{
    public int id, kind, animation;
    public float animationTime;
    public Vector3 position, scale;
    public Quaternion rotation;
    public bool visible;
}
[Serializable] public class OnlinePacket
{
    public string kind, message, mapFile, mapHash, stages;
    public int protocol, round, state, maxPlayers, aggressiveBots;
    public bool ready, fillBots;
    public ulong actorId;
    public double startAt, stopAt, stopFrom, stopTo, duration;
    public float elapsed;
    public float speed, spacing;
    public OnlineMember[] members;
    public OnlineSpawn[] spawns;
    public OnlineMapStamp[] maps;
    public string[] files;
    public ulong[] ranking;
    public int seriesRound, completedRounds;
    public double nextRoundAt;
    public OnlineScore[] scores;
    public OnlineVisualState[] visuals;
}

public static class OnlineRules
{
    public const int Protocol = 2;
    public const int MaxHumans = 4;
    public const int MaxPacketBytes = 32768;

    public static string Nickname(string value)
    {
        value = (value ?? "").Trim();
        var result = new StringBuilder();
        foreach (char c in value)
            if (!char.IsControl(c) && c != '<' && c != '>') result.Append(c);
        return result.Length == 0 ? "얼음 친구" : result.ToString(0, Math.Min(16, result.Length));
    }

    public static string Code(string value) => (value ?? "").Trim().Replace(" ", "").ToUpperInvariant();

    public static bool Newer(uint value, uint previous) => unchecked((int)(value - previous)) > 0;

    // 공백/JSON 속성 순서가 달라도 실제 맵 내용이 같으면 같은 해시를 사용한다.
    public static string MapHash(MapData map)
    {
        var text = new StringBuilder();
        text.Append(map.version).Append('|').Append(map.width).Append('|').Append(map.depth).Append('|');
        foreach (string row in map.rows) text.Append(row).Append('|');
        text.Append(map.playerStart.x).Append(',').Append(map.playerStart.y).Append('|');
        foreach (MapData.Point p in map.botStarts) text.Append(p.x).Append(',').Append(p.y).Append('|');
        using (var sha = SHA256.Create())
        {
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()));
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }
    }

    public static List<string> CommonMaps(IReadOnlyList<OnlineMember> members)
    {
        var result = new List<string>();
        if (members.Count == 0 || members[0].maps == null) return result;
        foreach (OnlineMapStamp map in members[0].maps)
        {
            bool common = map != null && !string.IsNullOrEmpty(map.file) && !string.IsNullOrEmpty(map.hash);
            for (int i = 1; common && i < members.Count; i++)
            {
                common = false;
                foreach (OnlineMapStamp other in members[i].maps ?? Array.Empty<OnlineMapStamp>())
                    if (other != null && other.file == map.file && other.hash == map.hash) { common = true; break; }
            }
            if (common && !result.Contains(map.file)) result.Add(map.file);
        }
        result.Sort(StringComparer.Ordinal);
        return result;
    }

    // 지정 칸부터 사용하되, 연결된 내부 칸에서 부족분을 멀리 떨어뜨려 보충한다.
    public static bool TrySpawns(MapData map, int count, out List<Vector2Int> result)
    {
        result = new List<Vector2Int>();
        var connected = new HashSet<Vector2Int>();
        var queue = new Queue<Vector2Int>();
        if (!map.IsIce(map.StartCell)) return false;
        queue.Enqueue(map.StartCell); connected.Add(map.StartCell);
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        while (queue.Count > 0)
        {
            Vector2Int cell = queue.Dequeue();
            foreach (var direction in directions)
                if (map.IsIce(cell + direction) && connected.Add(cell + direction)) queue.Enqueue(cell + direction);
        }
        var preferred = new List<Vector2Int> { map.StartCell };
        foreach (var point in map.botStarts) preferred.Add(point.Cell);
        foreach (var cell in preferred)
        {
            int neighbors = 0;
            foreach (var direction in directions) if (connected.Contains(cell + direction)) neighbors++;
            if (neighbors >= 2 && connected.Contains(cell) && !result.Contains(cell)) result.Add(cell);
            if (result.Count == count) return true;
        }
        while (result.Count < count)
        {
            Vector2Int best = default; float score = -1;
            for (int y = 0; y < map.depth; y++)
            for (int x = 0; x < map.width; x++)
            {
                var cell = new Vector2Int(x, y);
                if (!connected.Contains(cell) || result.Contains(cell)) continue;
                int neighbors = 0;
                foreach (var direction in directions) if (connected.Contains(cell + direction)) neighbors++;
                if (neighbors < 3) continue;
                float distance = 1000;
                foreach (var other in result) distance = Math.Min(distance, (cell - other).sqrMagnitude);
                if (distance < 4) continue;
                float candidate = distance + neighbors * 0.1f;
                if (candidate > score) { score = candidate; best = cell; }
            }
            if (score < 0) return false;
            result.Add(best);
        }
        return true;
    }
}
