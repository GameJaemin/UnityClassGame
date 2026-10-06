using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 맵툴 창 (메뉴 LionIceRun > 맵툴). 얼음판 모양을 칠해서 StreamingAssets/Maps/*.json으로 저장한다.
//  - 파일: 새로 만들기, 불러오기, 저장, 다른 이름으로 저장, 휴지통으로 삭제. 저장 안 한 변경이 있으면 제목에 *
//  - 크기: 가로·세로 1~16 (바꿔도 칠한 칸은 가운데 기준으로 유지)
//  - 칠하기: 브러시를 고르고 왼쪽 클릭·드래그, 오른쪽 클릭·드래그는 빈칸. 채우기 도구는 이어진 같은 칸을 한 번에
//    브러시 단축키 1~6, 칠하기 P / 채우기 F
//  - 대칭 그리기: 좌우 / 상하 / 4방향 (칸 종류만, 시작 칸 표시는 대칭 안 함)
//  - 되돌리기: Ctrl+Z / Ctrl+Y (Unity Undo)
//  - 검사: 시작 칸, 얼음 칸 수, 점프로도 못 가는 떨어진 섬, 봇 시작 칸
//  - 씬에 미리보기: 저장한 뒤 씬의 IceGrid에 이 맵을 지정하고 블럭을 만들어 보여 줌 / 이 맵으로 플레이: 미리보기 + Play
// 화면 방향: 위가 +z, 오른쪽이 +x (게임 카메라는 45도 돌아가 있음)
public class MapEditorWindow : EditorWindow
{
    enum Brush { Ice, Cracked, Thick, Empty, PlayerStart, BotStart }
    enum Tool { Paint, Fill }
    enum Mirror { None, LeftRight, TopBottom, FourWay }

    static readonly (Brush brush, string label, char kind, Color color)[] Brushes =
    {
        (Brush.Ice, "얼음", MapData.Ice, new Color(0.74f, 0.90f, 0.98f)),
        (Brush.Cracked, "금 간 얼음", MapData.Cracked, new Color(0.50f, 0.72f, 0.90f)),
        (Brush.Thick, "두꺼운 얼음", MapData.Thick, new Color(0.96f, 0.99f, 1f)),
        (Brush.Empty, "빈칸 (바다)", MapData.Empty, new Color(0.13f, 0.30f, 0.46f)),
        (Brush.PlayerStart, "플레이어 시작", '\0', new Color(0.85f, 0.54f, 0.04f)),
        (Brush.BotStart, "봇 시작", '\0', new Color(0.88f, 0.33f, 0.56f)),
    };

    static readonly Color GridBack = new Color(0.08f, 0.20f, 0.32f);
    static readonly Color ThickEdge = new Color(0.55f, 0.80f, 0.91f);
    static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    [SerializeField] MapEditState state;
    [SerializeField] string currentFile = "";   // 비어 있으면 아직 저장 안 한 새 맵
    [SerializeField] string selectedLoadFile = ""; // 드롭다운 선택은 현재 열린 파일과 별도로 유지
    [SerializeField] string fileNameField = "new_map";
    [SerializeField] string savedJson = "";
    [SerializeField] Brush brush = Brush.Ice;
    [SerializeField] Tool tool = Tool.Paint;
    [SerializeField] Mirror mirror = Mirror.None;
    [SerializeField] int newWidth = 8;
    [SerializeField] int newDepth = 8;

    Vector2 sideScroll;
    int dragGroup = -1;
    readonly List<(MessageType type, string text)> problems = new List<(MessageType, string)>();

    MapData Map => state.map;
    bool Dirty => Map.ToJson() != savedJson;

    [MenuItem("LionIceRun/맵툴")]
    static void Open()
    {
        var window = GetWindow<MapEditorWindow>();
        window.minSize = new Vector2(640, 460);
        window.Show();
    }

    void OnEnable()
    {
        if (state == null)
        {
            state = CreateInstance<MapEditState>();
            state.hideFlags = HideFlags.HideAndDontSave;
        }
        if (state.map == null)
        {
            state.map = MapData.Filled("새 맵", newWidth, newDepth);
            savedJson = "";
        }
        Undo.undoRedoPerformed += OnUndoRedo;
        UpdateTitle();
    }

    void OnDisable() => Undo.undoRedoPerformed -= OnUndoRedo;

    void OnDestroy()
    {
        if (state != null) DestroyImmediate(state);
    }

    void OnUndoRedo()
    {
        UpdateTitle();
        Repaint();
    }

    void UpdateTitle()
    {
        string file = currentFile.Length > 0 ? currentFile : "새 맵";
        titleContent = new GUIContent($"맵툴 - {file}{(state != null && state.map != null && Dirty ? " *" : "")}");
    }

    // ---- 화면 ----

    void OnGUI()
    {
        HandleShortcuts();
        Validate();

        EditorGUILayout.BeginHorizontal();
        sideScroll = EditorGUILayout.BeginScrollView(sideScroll, GUILayout.Width(260));
        DrawFilePanel();
        DrawSizePanel();
        DrawBrushPanel();
        DrawChecksPanel();
        DrawScenePanel();
        EditorGUILayout.EndScrollView();

        Rect area = GUILayoutUtility.GetRect(10, 10, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        DrawGrid(area);
        EditorGUILayout.EndHorizontal();
        UpdateTitle();
    }

    void DrawFilePanel()
    {
        EditorGUILayout.LabelField("파일", EditorStyles.boldLabel);
        string shown = currentFile.Length > 0 ? currentFile : "(저장 안 함)";
        EditorGUILayout.LabelField("지금 파일", shown + (Dirty ? " *" : ""));

        string[] files = MapData.ListFiles();
        EditorGUILayout.BeginHorizontal();
        int index = System.Array.IndexOf(files, selectedLoadFile);
        if (index < 0)
        {
            index = Mathf.Max(0, System.Array.IndexOf(files, currentFile));
            selectedLoadFile = files.Length > 0 ? files[index] : "";
        }
        using (new EditorGUI.DisabledScope(files.Length == 0))
        {
            int picked = EditorGUILayout.Popup(index, files.Length > 0 ? files : new[] { "(맵 없음)" });
            if (files.Length > 0) selectedLoadFile = files[picked];
            if (GUILayout.Button("불러오기", GUILayout.Width(70)) && files.Length > 0
                && LoadFile(selectedLoadFile)) GUIUtility.ExitGUI();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUI.BeginChangeCheck();
        string mapName = EditorGUILayout.TextField("맵 이름", Map.name);
        if (EditorGUI.EndChangeCheck())
        {
            Record("맵 이름 바꾸기");
            Map.name = mapName;
        }
        fileNameField = EditorGUILayout.TextField(new GUIContent("파일 이름", "영어·숫자·_ 권장. .json은 자동"), fileNameField);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("새로 만들기")) NewMap();
        using (new EditorGUI.DisabledScope(currentFile.Length == 0))
            if (GUILayout.Button("저장")) SaveTo(currentFile);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("다른 이름으로 저장")) SaveAs();
        using (new EditorGUI.DisabledScope(currentFile.Length == 0))
            if (GUILayout.Button("삭제")) DeleteCurrent();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(8);
    }

    void DrawSizePanel()
    {
        EditorGUILayout.LabelField("크기", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("지금", $"{Map.width} x {Map.depth}  (얼음 {Map.IceCount}칸)");
        EditorGUILayout.BeginHorizontal();
        newWidth = Mathf.Clamp(EditorGUILayout.IntField(newWidth), 1, MapData.MaxSize);
        GUILayout.Label("x", GUILayout.Width(12));
        newDepth = Mathf.Clamp(EditorGUILayout.IntField(newDepth), 1, MapData.MaxSize);
        using (new EditorGUI.DisabledScope(newWidth == Map.width && newDepth == Map.depth))
            if (GUILayout.Button("크기 바꾸기", GUILayout.Width(80))) Resize(newWidth, newDepth);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("모두 얼음")) FillAll(MapData.Ice);
        if (GUILayout.Button("모두 비우기")) FillAll(MapData.Empty);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(8);
    }

    void DrawBrushPanel()
    {
        EditorGUILayout.LabelField("브러시 (1~6)", EditorStyles.boldLabel);
        for (int i = 0; i < Brushes.Length; i++)
        {
            var b = Brushes[i];
            Rect row = EditorGUILayout.GetControlRect(false, 22);
            bool on = brush == b.brush;
            if (on) EditorGUI.DrawRect(row, new Color(1f, 0.75f, 0.2f, 0.25f));
            EditorGUI.DrawRect(new Rect(row.x + 4, row.y + 4, 14, 14), b.color);
            if (GUI.Button(new Rect(row.x + 24, row.y, row.width - 24, row.height), $"{i + 1}  {b.label}", on ? EditorStyles.boldLabel : EditorStyles.label))
                brush = b.brush;
        }
        tool = (Tool)GUILayout.Toolbar((int)tool, new[] { "칠하기 (P)", "채우기 (F)" });
        mirror = (Mirror)EditorGUILayout.Popup("대칭 그리기", (int)mirror, new[] { "끔", "좌우", "상하", "4방향" });
        EditorGUILayout.HelpBox("왼쪽 클릭·드래그: 칠하기\n오른쪽 클릭·드래그: 빈칸으로\n봇 시작: 클릭할 때마다 놓기/빼기", MessageType.None);
        EditorGUILayout.Space(8);
    }

    void DrawChecksPanel()
    {
        EditorGUILayout.LabelField("검사", EditorStyles.boldLabel);
        if (problems.Count == 0) EditorGUILayout.HelpBox("문제 없음", MessageType.Info);
        foreach (var (type, text) in problems) EditorGUILayout.HelpBox(text, type);
        EditorGUILayout.Space(8);
    }

    void DrawScenePanel()
    {
        EditorGUILayout.LabelField("씬", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("씬에 미리보기")) PreviewInScene(false);
            if (GUILayout.Button("이 맵으로 플레이")) PreviewInScene(true);
        }
        EditorGUILayout.HelpBox("저장한 뒤 씬의 IceGrid Map File에 이 맵을 지정하고 블럭을 다시 만듭니다.", MessageType.None);
    }

    void DrawGrid(Rect area)
    {
        EditorGUI.DrawRect(area, new Color(0.16f, 0.16f, 0.16f));
        const float margin = 28f;
        float cell = Mathf.Floor(Mathf.Min((area.width - margin * 2) / Map.width, (area.height - margin * 2) / Map.depth));
        cell = Mathf.Clamp(cell, 12f, 56f);
        var gridSize = new Vector2(cell * Map.width, cell * Map.depth);
        var origin = new Vector2(area.x + (area.width - gridSize.x) / 2f, area.y + (area.height - gridSize.y) / 2f);
        EditorGUI.DrawRect(new Rect(origin.x - 4, origin.y - 4, gridSize.x + 8, gridSize.y + 8), GridBack);

        var letter = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(cell * 0.38f) };
        letter.normal.textColor = Color.white;
        Vector2Int start = Map.playerStart.Cell;

        for (int r = 0; r < Map.depth; r++)
        for (int x = 0; x < Map.width; x++)
        {
            var c = new Vector2Int(x, Map.depth - 1 - r);
            var rect = new Rect(origin.x + x * cell + 1, origin.y + r * cell + 1, cell - 2, cell - 2);
            char kind = Map.Get(c);
            Color color = ColorOf(kind);
            if (kind == MapData.Thick)
            {
                EditorGUI.DrawRect(rect, ThickEdge);
                EditorGUI.DrawRect(new Rect(rect.x + 3, rect.y + 3, rect.width - 6, rect.height - 6), color);
            }
            else EditorGUI.DrawRect(rect, kind == MapData.Empty ? new Color(1f, 1f, 1f, 0.04f) : color);
            if (kind == MapData.Cracked) GUI.Label(rect, "/", letter);

            if (c == start) DrawMark(rect, Brushes[(int)Brush.PlayerStart].color, "P", letter);
            else if (IsBotStart(c)) DrawMark(rect, Brushes[(int)Brush.BotStart].color, "B", letter);
        }

        // 방향 안내
        var axis = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
        GUI.Label(new Rect(origin.x, origin.y - 24, gridSize.x, 18), "↑ +z", axis);
        GUI.Label(new Rect(origin.x + gridSize.x + 4, origin.y + gridSize.y / 2 - 9, 40, 18), "+x →", EditorStyles.miniLabel);

        HandleMouse(new Rect(origin, gridSize), cell);
    }

    static void DrawMark(Rect rect, Color color, string text, GUIStyle style)
    {
        float inset = rect.width * 0.2f;
        var inner = new Rect(rect.x + inset, rect.y + inset, rect.width - inset * 2, rect.height - inset * 2);
        EditorGUI.DrawRect(inner, color);
        GUI.Label(inner, text, style);
    }

    static Color ColorOf(char kind)
    {
        foreach (var b in Brushes) if (b.kind == kind) return b.color;
        return Brushes[0].color;
    }

    // ---- 입력 ----

    void HandleShortcuts()
    {
        Event e = Event.current;
        if (e.type != EventType.KeyDown || EditorGUIUtility.editingTextField) return;
        if (e.keyCode >= KeyCode.Alpha1 && e.keyCode <= KeyCode.Alpha6) brush = (Brush)(e.keyCode - KeyCode.Alpha1);
        else if (e.keyCode == KeyCode.P) tool = Tool.Paint;
        else if (e.keyCode == KeyCode.F) tool = Tool.Fill;
        else return;
        e.Use();
        Repaint();
    }

    void HandleMouse(Rect gridRect, float cell)
    {
        Event e = Event.current;
        if (e.type == EventType.MouseUp && dragGroup >= 0)
        {
            Undo.CollapseUndoOperations(dragGroup);
            dragGroup = -1;
            return;
        }
        bool down = e.type == EventType.MouseDown;
        bool drag = e.type == EventType.MouseDrag;
        if ((!down && !drag) || (e.button != 0 && e.button != 1) || !gridRect.Contains(e.mousePosition)) return;

        int x = Mathf.FloorToInt((e.mousePosition.x - gridRect.x) / cell);
        int r = Mathf.FloorToInt((e.mousePosition.y - gridRect.y) / cell);
        var c = new Vector2Int(Mathf.Clamp(x, 0, Map.width - 1), Map.depth - 1 - Mathf.Clamp(r, 0, Map.depth - 1));
        bool erase = e.button == 1;

        if (down)
        {
            GUIUtility.keyboardControl = 0;
            Undo.IncrementCurrentGroup();
            dragGroup = Undo.GetCurrentGroup();
        }
        // 시작 칸 표시와 채우기는 누를 때 한 번만
        bool once = !erase && (tool == Tool.Fill || brush == Brush.PlayerStart || brush == Brush.BotStart);
        if (drag && once) return;

        Apply(c, erase);
        e.Use();
        Repaint();
    }

    void Apply(Vector2Int c, bool erase)
    {
        if (erase)
        {
            Record("빈칸으로 지우기");
            foreach (Vector2Int m in Mirrored(c)) SetKind(m, MapData.Empty);
            return;
        }
        switch (brush)
        {
            case Brush.PlayerStart:
                Record("플레이어 시작 칸");
                if (!Map.IsIce(c)) Map.Set(c, MapData.Ice);
                Map.playerStart = new MapData.Point(c);
                RemoveBotStart(c);
                return;
            case Brush.BotStart:
                Record("봇 시작 칸");
                if (IsBotStart(c)) RemoveBotStart(c);
                else if (c != Map.playerStart.Cell)
                {
                    if (!Map.IsIce(c)) Map.Set(c, MapData.Ice);
                    var list = new List<MapData.Point>(Map.botStarts) { new MapData.Point(c) };
                    Map.botStarts = list.ToArray();
                }
                return;
        }

        char kind = Brushes[(int)brush].kind;
        if (tool == Tool.Fill)
        {
            Record("채우기");
            foreach (Vector2Int m in Mirrored(c)) FloodFill(m, kind);
        }
        else
        {
            Record("칠하기");
            foreach (Vector2Int m in Mirrored(c)) SetKind(m, kind);
        }
    }

    IEnumerable<Vector2Int> Mirrored(Vector2Int c)
    {
        var set = new HashSet<Vector2Int> { c };
        int mx = Map.width - 1 - c.x, mz = Map.depth - 1 - c.y;
        if (mirror == Mirror.LeftRight || mirror == Mirror.FourWay) set.Add(new Vector2Int(mx, c.y));
        if (mirror == Mirror.TopBottom || mirror == Mirror.FourWay) set.Add(new Vector2Int(c.x, mz));
        if (mirror == Mirror.FourWay) set.Add(new Vector2Int(mx, mz));
        return set;
    }

    // 빈칸으로 바꾸면 그 칸의 시작 칸 표시도 지운다
    void SetKind(Vector2Int c, char kind)
    {
        Map.Set(c, kind);
        if (kind != MapData.Empty) return;
        if (Map.playerStart.Cell == c) Map.playerStart = new MapData.Point(-1, -1);
        RemoveBotStart(c);
    }

    void FloodFill(Vector2Int start, char kind)
    {
        char from = Map.Get(start);
        if (from == kind) return;
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(start);
        SetKind(start, kind);
        while (queue.Count > 0)
        {
            Vector2Int c = queue.Dequeue();
            foreach (Vector2Int d in Directions)
            {
                Vector2Int n = c + d;
                if (!Map.InBounds(n) || Map.Get(n) != from) continue;
                SetKind(n, kind);
                queue.Enqueue(n);
            }
        }
    }

    bool IsBotStart(Vector2Int c)
    {
        foreach (MapData.Point p in Map.botStarts) if (p.Cell == c) return true;
        return false;
    }

    void RemoveBotStart(Vector2Int c)
    {
        var list = new List<MapData.Point>(Map.botStarts);
        list.RemoveAll(p => p.Cell == c);
        Map.botStarts = list.ToArray();
    }

    void Record(string label) => Undo.RecordObject(state, $"맵툴: {label}");

    // ---- 편집 ----

    void FillAll(char kind)
    {
        Record(kind == MapData.Empty ? "모두 비우기" : "모두 얼음");
        for (int x = 0; x < Map.width; x++)
            for (int z = 0; z < Map.depth; z++)
                SetKind(new Vector2Int(x, z), kind);
    }

    // 가운데 기준으로 칠한 칸을 유지하며 크기를 바꾼다. 새로 생긴 칸은 얼음
    void Resize(int width, int depth)
    {
        Record("크기 바꾸기");
        MapData old = MapData.FromJson(Map.ToJson());
        Vector2Int offset = new Vector2Int((width - old.width) / 2, (depth - old.depth) / 2);
        MapData resized = MapData.Filled(old.name, width, depth);
        for (int x = 0; x < width; x++)
            for (int z = 0; z < depth; z++)
            {
                Vector2Int from = new Vector2Int(x, z) - offset;
                if (old.InBounds(from)) resized.Set(new Vector2Int(x, z), old.Get(from));
            }
        Vector2Int start = old.playerStart.Cell + offset;
        resized.playerStart = resized.InBounds(start) && resized.IsIce(start) ? new MapData.Point(start) : new MapData.Point(-1, -1);
        var bots = new List<MapData.Point>();
        foreach (MapData.Point p in old.botStarts)
        {
            Vector2Int moved = p.Cell + offset;
            if (resized.InBounds(moved) && resized.IsIce(moved)) bots.Add(new MapData.Point(moved));
        }
        resized.botStarts = bots.ToArray();
        state.map = resized;
    }

    // ---- 파일 ----

    bool ConfirmDiscard()
    {
        if (!Dirty) return true;
        int choice = EditorUtility.DisplayDialogComplex("맵툴", "저장하지 않은 변경이 있습니다.", "저장", "취소", "버리기");
        if (choice == 1) return false;
        if (choice == 0) return currentFile.Length > 0 ? SaveTo(currentFile) : SaveAs();
        return true;
    }

    void NewMap()
    {
        if (!ConfirmDiscard()) return;
        Record("새로 만들기");
        state.map = MapData.Filled("새 맵", newWidth, newDepth);
        currentFile = "";
        fileNameField = "new_map";
        savedJson = "";
    }

    bool LoadFile(string file)
    {
        // 같은 파일도 다시 읽어서 밖에서 저장한 변경을 반영한다.
        if (!ConfirmDiscard()) return false;
        MapData loaded = MapData.Load(file);
        if (loaded == null)
        {
            EditorUtility.DisplayDialog("맵툴", $"{file}을(를) 읽지 못했습니다. Console을 확인하세요.", "확인");
            return false;
        }
        Record("불러오기");
        state.map = loaded;
        currentFile = file;
        selectedLoadFile = file;
        fileNameField = Path.GetFileNameWithoutExtension(file);
        savedJson = Map.ToJson();
        newWidth = Map.width;
        newDepth = Map.depth;
        GUI.FocusControl(null);
        UpdateTitle();
        Repaint();
        return true;
    }

    bool SaveAs()
    {
        string name = CleanFileName(fileNameField);
        if (name.Length == 0)
        {
            EditorUtility.DisplayDialog("맵툴", "파일 이름을 적어 주세요 (영어·숫자·_ 권장).", "확인");
            return false;
        }
        string file = name + ".json";
        if (file != currentFile && File.Exists(MapData.FilePath(file))
            && !EditorUtility.DisplayDialog("맵툴", $"{file}이(가) 이미 있습니다. 덮어쓸까요?", "덮어쓰기", "취소"))
            return false;
        return SaveTo(file);
    }

    bool SaveTo(string file)
    {
        if (problems.Exists(p => p.type == MessageType.Error)
            && !EditorUtility.DisplayDialog("맵툴", "검사에서 오류가 있습니다. 이대로 저장할까요?\n\n" + ProblemText(MessageType.Error), "저장", "취소"))
            return false;
        Map.Save(file);
        currentFile = file;
        fileNameField = Path.GetFileNameWithoutExtension(file);
        savedJson = Map.ToJson();
        AssetDatabase.Refresh();
        Debug.Log($"맵툴: {MapData.FilePath(file)} 저장");
        return true;
    }

    void DeleteCurrent()
    {
        if (!EditorUtility.DisplayDialog("맵툴", $"{currentFile}을(를) 휴지통으로 옮길까요?", "삭제", "취소")) return;
        string assetPath = $"Assets/StreamingAssets/{MapData.FolderName}/{currentFile}";
        if (!AssetDatabase.MoveAssetToTrash(assetPath)) File.Delete(MapData.FilePath(currentFile));
        AssetDatabase.Refresh();
        currentFile = "";
        savedJson = "";
    }

    static string CleanFileName(string name)
    {
        name = (name ?? "").Trim();
        if (name.EndsWith(".json", System.StringComparison.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - 5);
        foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c.ToString(), "");
        return name.Replace(' ', '_');
    }

    // ---- 씬 ----

    void PreviewInScene(bool play)
    {
        if (currentFile.Length == 0 || Dirty)
        {
            if (!EditorUtility.DisplayDialog("맵툴", "씬에서 쓰려면 먼저 저장해야 합니다. 저장할까요?", "저장", "취소")) return;
            if (!(currentFile.Length > 0 ? SaveTo(currentFile) : SaveAs())) return;
        }

        IceGrid grid = FindAnyObjectByType<IceGrid>();
        if (grid == null)
        {
            EditorUtility.DisplayDialog("맵툴", "씬에 IceGrid가 없습니다. LionIceRun > Step 2 맵 세팅을 먼저 실행하세요.", "확인");
            return;
        }
        Undo.RecordObject(grid, "맵툴: 씬에 미리보기");
        grid.mapFile = currentFile;
        grid.Build();
        EditorUtility.SetDirty(grid);

        // 플레이어를 시작 칸 위로, 카메라를 새 크기에 맞춘다
        foreach (PlayerController player in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (player.useExternalInput) continue;
            Undo.RecordObject(player.transform, "맵툴: 플레이어 시작 칸");
            player.transform.position = grid.CellTop(grid.StartCell) + Vector3.up * player.spawnHeight;
        }
        QuarterViewCamera cam = FindAnyObjectByType<QuarterViewCamera>();
        if (cam != null)
        {
            Undo.RecordObject(cam.transform, "맵툴: 카메라 맞추기");
            cam.Fit();
        }
        EditorSceneManager.MarkSceneDirty(grid.gameObject.scene);
        Selection.activeGameObject = grid.gameObject;

        if (play)
        {
            MapRoulette.SkipNextSelection = true;
            SessionState.SetBool("LionIceRun.MapToolPlay", true);
            EditorApplication.isPlaying = true;
        }
    }

    // ---- 검사 ----

    int BotCount()
    {
        BotManager bots = FindAnyObjectByType<BotManager>();
        return bots != null ? bots.botCount : 3;
    }

    void Validate()
    {
        problems.Clear();
        int ice = Map.IceCount;
        int botCount = BotCount();
        Vector2Int start = Map.playerStart.Cell;

        if (string.IsNullOrWhiteSpace(Map.name)) problems.Add((MessageType.Warning, "맵 이름이 비어 있어요"));
        if (!Map.IsIce(start)) problems.Add((MessageType.Error, "플레이어 시작 칸이 없어요 (브러시 5로 지정)"));
        int needed = (botCount + 1) * 3;
        if (ice < needed) problems.Add((MessageType.Error, $"얼음이 {ice}칸뿐이에요 (봇 {botCount}마리 기준 최소 {needed}칸)"));

        if (Map.IsIce(start))
        {
            int reached = Reachable(start).Count;
            if (reached < ice) problems.Add((MessageType.Warning, $"점프로도 못 가는 떨어진 칸이 {ice - reached}개 있어요"));
        }

        var seen = new HashSet<Vector2Int>();
        foreach (MapData.Point p in Map.botStarts)
        {
            Vector2Int c = p.Cell;
            if (!Map.IsIce(c)) problems.Add((MessageType.Warning, $"봇 시작 칸 ({c.x}, {c.y})이 빈칸이에요"));
            if (!seen.Add(c)) problems.Add((MessageType.Warning, $"봇 시작 칸 ({c.x}, {c.y})이 겹쳐요"));
            if (Map.IsIce(start) && Mathf.Abs(c.x - start.x) <= 1 && Mathf.Abs(c.y - start.y) <= 1)
                problems.Add((MessageType.Warning, $"봇 시작 칸 ({c.x}, {c.y})이 플레이어 바로 옆이에요"));
        }
        if (Map.botStarts.Length < botCount)
            problems.Add((MessageType.Info, $"봇 시작 칸 {Map.botStarts.Length}개 (봇 {botCount}마리 중 나머지는 무작위 칸)"));
    }

    // 걷기 + 빈칸 1개 점프로 닿는 칸 (봇 길 찾기와 같은 규칙)
    HashSet<Vector2Int> Reachable(Vector2Int start)
    {
        var seen = new HashSet<Vector2Int> { start };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            Vector2Int c = queue.Dequeue();
            foreach (Vector2Int d in Directions)
            {
                Vector2Int near = c + d;
                Vector2Int target = Map.IsIce(near) ? near : Map.InBounds(near) ? c + d * 2 : new Vector2Int(-1, -1);
                if (!Map.IsIce(target) || !seen.Add(target)) continue;
                queue.Enqueue(target);
            }
        }
        return seen;
    }

    string ProblemText(MessageType type)
    {
        var lines = new List<string>();
        foreach (var (t, text) in problems) if (t == type) lines.Add("· " + text);
        return string.Join("\n", lines);
    }
}
