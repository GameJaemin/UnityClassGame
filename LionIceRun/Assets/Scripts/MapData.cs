using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// 얼음판 맵 한 장 (맵툴이 만들고 IceGrid가 읽는 JSON과 같은 모양).
//  - 파일 위치: Assets/StreamingAssets/Maps/<파일 이름>.json (빌드한 게임 폴더에도 그대로 들어감, PC 기준)
//  - rows: 한 행에 한 글자열. rows[0]이 맵툴 화면의 맨 위 행 = z가 가장 큰 행, 글자 순서는 x 0부터
//  - 칸 글자: '.' 빈칸(바다), 'I' 얼음, 'C' 금 간 얼음, 'T' 두꺼운 얼음 (C·T는 3단계 전까지 얼음과 같음)
//  - playerStart / botStarts: 칸 번호 (x, y = z 칸 번호)
// 읽고 쓰기는 Unity 기본 JsonUtility.
[Serializable]
public class MapData
{
    public const int CurrentVersion = 1;
    public const int MaxSize = 16;
    public const string FolderName = "Maps";

    public const char Empty = '.';
    public const char Ice = 'I';
    public const char Cracked = 'C';
    public const char Thick = 'T';

    // JsonUtility로 {"x":3,"y":3} 모양을 확실히 쓰려고 Vector2Int 대신 둠
    [Serializable]
    public struct Point
    {
        public int x;
        public int y;

        public Point(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public Point(Vector2Int cell) : this(cell.x, cell.y) { }
        public Vector2Int Cell => new Vector2Int(x, y);
    }

    public int version = CurrentVersion;
    public string name = "";
    public int width;
    public int depth;
    public string[] rows = new string[0];
    public Point playerStart = new Point(-1, -1);
    public Point[] botStarts = new Point[0];

    public static string FolderPath => Path.Combine(Application.streamingAssetsPath, FolderName);

    public static string FilePath(string fileName)
    {
        if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) fileName += ".json";
        return Path.Combine(FolderPath, fileName);
    }

    public bool InBounds(Vector2Int cell) => cell.x >= 0 && cell.y >= 0 && cell.x < width && cell.y < depth;

    public char Get(Vector2Int cell)
    {
        if (!InBounds(cell)) return Empty;
        string row = rows[depth - 1 - cell.y];
        return cell.x < row.Length ? row[cell.x] : Empty;
    }

    public void Set(Vector2Int cell, char kind)
    {
        if (!InBounds(cell)) return;
        int r = depth - 1 - cell.y;
        char[] chars = rows[r].ToCharArray();
        chars[cell.x] = kind;
        rows[r] = new string(chars);
    }

    public bool IsIce(Vector2Int cell) => Get(cell) != Empty;

    public int IceCount
    {
        get
        {
            int count = 0;
            for (int x = 0; x < width; x++)
                for (int z = 0; z < depth; z++)
                    if (IsIce(new Vector2Int(x, z))) count++;
            return count;
        }
    }

    // 플레이어 시작 칸. 지정이 없거나 빈칸이면 가운데에서 가장 가까운 얼음 칸
    public Vector2Int StartCell
    {
        get
        {
            Vector2Int start = playerStart.Cell;
            if (IsIce(start)) return start;
            Vector2Int center = new Vector2Int(width / 2, depth / 2);
            Vector2Int best = center;
            float bestDistance = float.PositiveInfinity;
            for (int x = 0; x < width; x++)
                for (int z = 0; z < depth; z++)
                {
                    var cell = new Vector2Int(x, z);
                    float distance = (cell - center).sqrMagnitude;
                    if (IsIce(cell) && distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = cell;
                    }
                }
            return best;
        }
    }

    // 모든 칸이 얼음인 직사각형 맵 (맵 파일이 없을 때와 같은 판)
    public static MapData Filled(string name, int width, int depth)
    {
        width = Mathf.Clamp(width, 1, MaxSize);
        depth = Mathf.Clamp(depth, 1, MaxSize);
        var map = new MapData { name = name, width = width, depth = depth, rows = new string[depth] };
        for (int r = 0; r < depth; r++) map.rows[r] = new string(Ice, width);
        map.playerStart = new Point(width / 2, depth / 2);
        return map;
    }

    // 잘못 저장된 값 바로잡기: 크기 제한, 행 수·길이 맞추기, 모르는 글자는 얼음으로
    public void Normalize()
    {
        width = Mathf.Clamp(width, 1, MaxSize);
        depth = Mathf.Clamp(depth, 1, MaxSize);
        if (rows == null) rows = new string[0];
        var fixedRows = new string[depth];
        for (int r = 0; r < depth; r++)
        {
            string row = r < rows.Length && rows[r] != null ? rows[r] : "";
            var chars = new char[width];
            for (int x = 0; x < width; x++)
            {
                char c = x < row.Length ? char.ToUpperInvariant(row[x]) : Empty;
                chars[x] = c == Empty || c == Ice || c == Cracked || c == Thick ? c : Ice;
            }
            fixedRows[r] = new string(chars);
        }
        rows = fixedRows;
        if (botStarts == null) botStarts = new Point[0];
    }

    public string ToJson() => JsonUtility.ToJson(this, true);

    public static MapData FromJson(string json)
    {
        MapData map = JsonUtility.FromJson<MapData>(json);
        if (map == null) return null;
        if (map.version > CurrentVersion)
            Debug.LogWarning($"MapData: 더 새 형식(version {map.version})의 맵입니다. 아는 부분만 읽습니다.");
        map.Normalize();
        return map;
    }

    // 실패하면 이유를 Console에 남기고 null
    public static MapData Load(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return null;
        string path = FilePath(fileName);
        if (!File.Exists(path))
        {
            Debug.LogError($"MapData: 맵 파일이 없습니다: {path}");
            return null;
        }
        try
        {
            MapData map = FromJson(File.ReadAllText(path));
            if (map == null) Debug.LogError($"MapData: 맵 파일을 읽지 못했습니다: {path}");
            return map;
        }
        catch (Exception e)
        {
            Debug.LogError($"MapData: 맵 파일 형식이 잘못되었습니다: {path}\n{e.Message}");
            return null;
        }
    }

    public void Save(string fileName)
    {
        Normalize();
        Directory.CreateDirectory(FolderPath);
        File.WriteAllText(FilePath(fileName), ToJson());
    }

    // StreamingAssets/Maps 안의 맵 파일 이름들 (확장자 포함, 이름순)
    public static string[] ListFiles()
    {
        if (!Directory.Exists(FolderPath)) return new string[0];
        var names = new List<string>();
        foreach (string path in Directory.GetFiles(FolderPath, "*.json")) names.Add(Path.GetFileName(path));
        names.Sort(StringComparer.OrdinalIgnoreCase);
        return names.ToArray();
    }
}

// IceGrid의 맵 파일 칸을 Inspector에서 맵 목록 드롭다운으로 보여 준다 (Editor/MapFileDrawer.cs)
public class MapFileAttribute : PropertyAttribute { }
