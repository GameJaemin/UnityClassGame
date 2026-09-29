using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// 게임 흐름: 대기 → 진행 중 → 게임 오버.
//  - 대기: 플레이어가 아직 움직이지 않음 (시작 블럭 보호 중)
//  - 진행 중: 처음 이동·점프·다이브 입력 순간 시작, 생존 타이머가 돈다
//  - 게임 오버: 바다 수면 아래로 떨어지면. 기록 저장, 조작 막기, 물보라 후 제자리에 멈춤
// UI(Step 7)는 StateChanged 이벤트와 SurvivalTimer 값을 보고 화면을 바꾼다.
public class GameManager : MonoBehaviour
{
    public enum GameState { Ready, Playing, GameOver }

    public static GameManager Instance { get; private set; }

    public PlayerController player;
    public IceGrid grid;
    public SurvivalTimer timer;

    [Tooltip("바다에 빠질 때 튀는 물보라 알갱이 수")]
    [Min(0)] public int splashCount = 40;

    public GameState State { get; private set; } = GameState.Ready;

    public event Action<GameState> StateChanged;

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
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
                if (grid != null && player.transform.position.y < grid.SeaLevel) GameOver();
                break;
        }
    }

    void GameOver()
    {
        if (timer != null) timer.Stop();
        player.InputEnabled = false;

        // 바다에 풍덩: 물보라를 튀기고 더 떨어지지 않게 멈춘다
        Vector3 splashPoint = new Vector3(player.transform.position.x, grid.SeaLevel, player.transform.position.z);
        grid.PlayIceBurst(splashPoint, splashCount, 1.4f);
        var body = player.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.isKinematic = true;
        }

        SetState(GameState.GameOver);
    }

    // 현재 씬을 다시 불러와 처음부터 (Step 7의 재시작 버튼·R 키에서 사용)
    public void Restart() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

    void SetState(GameState state)
    {
        State = state;
        StateChanged?.Invoke(state);
    }
}
