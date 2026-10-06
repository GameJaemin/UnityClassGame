using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 게임 흐름: 대기 → 진행 중 → 경기 끝(GameOver). 목표는 맵 위에 마지막까지 남는 1등.
//  - 대기: 플레이어가 아직 움직이지 않음 (시작 블럭 보호 중)
//  - 진행 중: 처음 이동·점프·다이브·밀치기 입력 순간 시작. 플레이어와 봇이 모두 같은 규칙으로 겨룬다
//    바다 수면 아래로 떨어지면 물보라와 함께 사라지고, respawnDelay(10초) 뒤 멀쩡한 칸 위 하늘에서 다시 떨어져 들어온다
//  - 경기 끝: 맵 위(다시 떨어져 들어오는 중 포함)에 한 명만 남으면 더 이상 다시 들어오지 않고 그 캐릭터가 1등.
//    나머지 순위는 늦게 떨어진 순서. 플레이어가 1등이면 우승 횟수를 저장한다 (PlayerPrefs)
// 봇은 BotManager가 만들면서 Register로 등록한다.
// UI(Step 7)는 StateChanged·Fell·Respawned 이벤트와 아래 값들을 보고 화면을 바꾼다.
public class GameManager : MonoBehaviour
{
    public enum GameState { Ready, Playing, GameOver }

    const string WinsKey = "LionIceRun.Wins";

    class Competitor
    {
        public PlayerController body;
        public bool outOfMap;
        public float fellTime = float.NegativeInfinity;
        public float respawnTime;
        public bool waitingForSpace;
    }

    public static GameManager Instance { get; private set; }

    public PlayerController player;
    public IceGrid grid;
    public SurvivalTimer timer;

    [Tooltip("바다에 빠질 때 튀는 물보라 알갱이 수")]
    [Min(0)] public int splashCount = 40;

    [Header("다시 들어오기")]
    [Tooltip("바다에 빠진 뒤 다시 들어올 때까지 기다리는 시간 (맵 위에 한 명만 남으면 더 이상 안 들어옴)")]
    [Min(0f)] public float respawnDelay = 10f;
    [Tooltip("다시 들어올 때 블럭 위 얼마나 높은 곳에서 떨어지는지")]
    [Min(1f)] public float respawnHeight = 7f;

    readonly List<Competitor> competitors = new List<Competitor>();

    public GameState State { get; private set; } = GameState.Ready;
    // 경기가 끝난 뒤: 1등 캐릭터와 플레이어 순위
    public PlayerController Winner { get; private set; }
    public int PlayerPlace { get; private set; }
    public bool PlayerWon => Winner != null && Winner == player;
    // 플레이어가 1등 한 횟수 (이 PC에 저장)
    public int Wins { get; private set; }

    public int CompetitorCount => competitors.Count;
    public int OnMapCount
    {
        get
        {
            int count = 0;
            foreach (Competitor c in competitors) if (!c.outOfMap) count++;
            return count;
        }
    }
    public bool PlayerOut => Find(player) is Competitor c && c.outOfMap;
    // 플레이어가 바다에 빠져 있으면 다시 들어올 때까지 남은 초, 아니면 0
    public float PlayerRespawnRemaining => Find(player) is Competitor c && c.outOfMap ? Mathf.Max(0f, c.respawnTime - Time.time) : 0f;

    public bool PlayerWaitingForSpace => Find(player) is Competitor c && c.waitingForSpace;

    // 비활성화된 봇도 참가자 목록에 남아 있으므로 사망 중 시간을 조회할 수 있다.
    public bool TryGetRespawnStatus(int index, out PlayerController body, out float remaining, out bool waitingForSpace)
    {
        body = null;
        remaining = 0f;
        waitingForSpace = false;
        if (State != GameState.Playing || index < 0 || index >= competitors.Count) return false;
        Competitor c = competitors[index];
        body = c.body;
        if (body == null || !c.outOfMap) return false;
        remaining = Mathf.Max(0f, c.respawnTime - Time.time);
        if (float.IsInfinity(remaining)) remaining = -1f;
        waitingForSpace = c.waitingForSpace;
        return true;
    }

    public event Action<GameState> StateChanged;
    public event Action<PlayerController> Fell;
    public event Action<PlayerController> Respawned;

    void Awake()
    {
        Instance = this;
        Wins = PlayerPrefs.GetInt(WinsKey, 0);
        if (player != null) Register(player);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // 경기에 참가하는 캐릭터 등록 (플레이어는 자동, 봇은 BotManager가 부름)
    public void Register(PlayerController body)
    {
        if (body == null || Find(body) != null) return;
        competitors.Add(new Competitor { body = body });
    }

    void Update()
    {
        if (OnlineGameSession.InRoom && (!OnlineGameSession.Authority || !OnlineGameSession.MatchPlaying)) return;
        if (player == null) return;

        switch (State)
        {
            case GameState.Ready:
                if (player.HasMoved)
                {
                    if (timer != null) timer.Begin();
                    SetState(GameState.Playing);
                }
                break;

            case GameState.Playing:
                UpdateMatch();
                break;
        }
    }

    // 맵 선택 화면의 준비 카운트다운이 끝나면 입력과 경기 시간을 함께 시작한다.
    public void BeginSelectedMatch()
    {
        if (State != GameState.Ready || player == null) return;
        player.InputEnabled = true;
        if (timer != null) timer.Begin();
        SetState(GameState.Playing);
    }

    public void ResetOnlineMatch(PlayerController localPlayer)
    {
        competitors.Clear();
        player = localPlayer;
        Winner = null;
        PlayerPlace = 0;
        Wins = PlayerPrefs.GetInt("LionIceRun.OnlineWins", 0);
        State = GameState.Ready;
        if (timer != null) timer.ResetMatch();
    }

    public bool StatusOf(PlayerController body, out bool fallen, out float remaining, out bool waiting)
    {
        Competitor c = Find(body);
        fallen = c != null && c.outOfMap;
        remaining = fallen ? Mathf.Max(0f, c.respawnTime - Time.time) : 0f;
        if (float.IsInfinity(remaining)) remaining = -1f;
        waiting = c != null && c.waitingForSpace;
        return c != null;
    }

    public void ApplyOnlineStatus(PlayerController body, bool fallen, float remaining, bool waiting)
    {
        Competitor c = Find(body);
        if (c == null) return;
        bool changed = c.outOfMap != fallen;
        c.outOfMap = fallen;
        c.waitingForSpace = waiting;
        c.respawnTime = remaining < 0f ? float.PositiveInfinity : Time.time + remaining;
        if (changed)
        {
            if (fallen) Fell?.Invoke(body);
            else Respawned?.Invoke(body);
        }
    }

    public void WithdrawOnline(PlayerController body)
    {
        Competitor c = Find(body);
        if (c == null) return;
        c.outOfMap = true;
        c.fellTime = Time.time;
        c.respawnTime = float.PositiveInfinity;
        c.body.gameObject.SetActive(false);
        Fell?.Invoke(body);
    }

    public List<PlayerController> RankedBodies()
    {
        var order = new List<Competitor>(competitors);
        order.Sort((a, b) =>
        {
            if (a.outOfMap != b.outOfMap) return a.outOfMap ? 1 : -1;
            int time = b.fellTime.CompareTo(a.fellTime);
            return time != 0 ? time : competitors.IndexOf(a).CompareTo(competitors.IndexOf(b));
        });
        return order.ConvertAll(c => c.body);
    }

    public void ApplyOnlineResult(PlayerController winner, int place)
    {
        if (State == GameState.GameOver) return;
        Winner = winner;
        PlayerPlace = place;
        if (player != null) player.InputEnabled = false;
        SetState(GameState.GameOver);
    }

    void UpdateMatch()
    {
        if (grid == null) return;

        foreach (Competitor c in competitors)
            if (!c.outOfMap && c.body != null && c.body.transform.position.y < grid.SeaLevel) Fall(c);

        // 맵 위에 한 명만 남으면 끝 (봇 없이 혼자면 떨어지는 순간 끝)
        int onMap = OnMapCount;
        if (onMap == 0 || (onMap == 1 && competitors.Count >= 2))
        {
            Finish();
            return;
        }

        foreach (Competitor c in competitors)
            if (c.outOfMap && Time.time >= c.respawnTime) Respawn(c);
    }

    void Fall(Competitor c)
    {
        Vector3 p = c.body.transform.position;
        grid.PlayIceBurst(new Vector3(p.x, grid.SeaLevel, p.z), splashCount, 1.4f);
        c.outOfMap = true;
        c.fellTime = Time.time;
        c.respawnTime = Time.time + respawnDelay;
        c.waitingForSpace = false;
        c.body.gameObject.SetActive(false);
        Fell?.Invoke(c.body);
    }

    void Respawn(Competitor c)
    {
        // 설 칸이 없으면 1초 뒤 다시 확인
        if (!PickRespawnCell(out Vector2Int cell))
        {
            c.respawnTime = Time.time + 1f;
            c.waitingForSpace = true;
            return;
        }
        c.outOfMap = false;
        c.waitingForSpace = false;
        c.body.gameObject.SetActive(true);
        c.body.ResetMotion(grid.CellTop(cell) + Vector3.up * respawnHeight);
        Respawned?.Invoke(c.body);
    }

    // 멀쩡하고 흔들리지 않는 칸. 되도록 맵 위 다른 캐릭터의 칸·바로 옆 칸은 피한다 (머리 위로 떨어지지 않게)
    bool PickRespawnCell(out Vector2Int cell)
    {
        var occupied = new List<Vector2Int>();
        foreach (Competitor c in competitors)
            if (!c.outOfMap && c.body != null) occupied.Add(grid.WorldToCell(c.body.transform.position));

        var free = new List<Vector2Int>();
        var any = new List<Vector2Int>();
        foreach (IceBlock block in grid.IceBlocks)
        {
            if (block.IsBroken || block.CurrentStage == IceBlock.Stage.Shaking) continue;
            any.Add(block.Cell);
            bool near = false;
            foreach (Vector2Int o in occupied)
                if (Mathf.Abs(o.x - block.Cell.x) <= 1 && Mathf.Abs(o.y - block.Cell.y) <= 1) near = true;
            if (!near) free.Add(block.Cell);
        }
        List<Vector2Int> pool = free.Count > 0 ? free : any;
        cell = pool.Count > 0 ? pool[UnityEngine.Random.Range(0, pool.Count)] : default;
        return pool.Count > 0;
    }

    void Finish()
    {
        // 순위: 맵 위에 남은 캐릭터가 1등, 나머지는 늦게 떨어진 순서
        var order = new List<Competitor>(competitors);
        order.Sort((a, b) =>
        {
            if (a.outOfMap != b.outOfMap) return a.outOfMap ? 1 : -1;
            int time = b.fellTime.CompareTo(a.fellTime);
            return time != 0 ? time : competitors.IndexOf(a).CompareTo(competitors.IndexOf(b));
        });
        // 혼자 하다가 떨어진 경우는 1등이 없다
        Winner = competitors.Count >= 2 || !order[0].outOfMap ? order[0].body : null;
        PlayerPlace = order.FindIndex(c => c.body == player) + 1;

        if (PlayerWon && !OnlineGameSession.InRoom)
        {
            Wins++;
            PlayerPrefs.SetInt(OnlineGameSession.InRoom ? "LionIceRun.OnlineWins" : WinsKey, Wins);
            PlayerPrefs.Save();
        }
        if (timer != null) timer.Stop();
        player.InputEnabled = false;

        SetState(GameState.GameOver);
    }

    Competitor Find(PlayerController body)
    {
        if (body == null) return null;
        foreach (Competitor c in competitors) if (c.body == body) return c;
        return null;
    }

    public void RecordOnlineSeriesWin()
    {
        Wins++; PlayerPrefs.SetInt("LionIceRun.OnlineWins", Wins); PlayerPrefs.Save();
    }

    // 개발용: 우승 횟수 지우기 (Inspector 컴포넌트 메뉴)
    [ContextMenu("우승 횟수 지우기")]
    void ResetWins()
    {
        PlayerPrefs.DeleteKey(WinsKey);
        Wins = 0;
    }

    // 현재 씬을 다시 불러와 처음부터 (Step 7의 재시작 버튼·R 키에서 사용)
    public void Restart()
    {
        if (OnlineGameSession.InRoom) { OnlineGameSession.Instance.ReturnToLobby(); return; }
        if (OnlineGameSession.Instance != null && OnlineGameSession.Instance.isActiveAndEnabled)
        { OnlineGameSession.Instance.ReloadSoloScene(); return; }
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void SetState(GameState state)
    {
        State = state;
        StateChanged?.Invoke(state);
    }
}
