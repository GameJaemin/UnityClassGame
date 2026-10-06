using System;
using UnityEngine;

// 생존 시간과 최고 기록.
//  - GameManager가 게임 시작 때 Begin, 게임 오버 때 Stop을 부른다
//  - Stop에서 최고 기록을 넘으면 저장 (PlayerPrefs)
//  - 시련 등 다른 시스템은 SurvivalTimer.Instance.Elapsed로 버틴 시간을 읽는다
public class SurvivalTimer : MonoBehaviour
{
    const string BestTimeKey = "LionIceRun.BestTime";

    public static SurvivalTimer Instance { get; private set; }

    [Tooltip("UI(Step 7)가 생기기 전까지 화면 왼쪽 위에 기록을 임시로 표시")]
    public bool showDebugHud = true;

    float startTime;

    public bool IsRunning { get; private set; }
    public bool IsFinished { get; private set; }
    public float Elapsed { get; private set; }
    public float BestTime { get; private set; }
    public bool IsNewRecord { get; private set; }

    public event Action Started;
    public event Action<float, bool> Finished;  // (버틴 시간, 새 기록 여부)

    void Awake()
    {
        Instance = this;
        BestTime = PlayerPrefs.GetFloat(BestTimeKey, 0f);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (IsRunning) Elapsed = Time.time - startTime;
    }

    public void Begin()
    {
        if (IsRunning || IsFinished) return;
        IsRunning = true;
        startTime = Time.time;
        Started?.Invoke();
    }

    // 기록을 멈추고 최고 기록이면 저장한다. 반환: 새 기록 여부
    public bool Stop()
    {
        if (IsFinished) return IsNewRecord;
        if (IsRunning) Elapsed = Time.time - startTime;
        IsRunning = false;
        IsFinished = true;

        IsNewRecord = !OnlineGameSession.InRoom && Elapsed > BestTime;
        if (IsNewRecord)
        {
            BestTime = Elapsed;
            PlayerPrefs.SetFloat(BestTimeKey, BestTime);
            PlayerPrefs.Save();
        }
        Finished?.Invoke(Elapsed, IsNewRecord);
        return IsNewRecord;
    }

    // 개발용: 최고 기록 지우기 (Inspector 컴포넌트 메뉴)
    public void ResetMatch()
    {
        IsRunning = IsFinished = IsNewRecord = false;
        Elapsed = 0f;
    }

    public void ApplyOnlineTime(float elapsed, bool running)
    {
        Elapsed = elapsed;
        startTime = Time.time - elapsed;
        IsRunning = running;
        IsFinished = !running && elapsed > 0f;
        IsNewRecord = false;
    }

    [ContextMenu("최고 기록 지우기")]
    void ResetBestTime()
    {
        PlayerPrefs.DeleteKey(BestTimeKey);
        BestTime = 0f;
    }

    public static string Format(float seconds) => $"{Mathf.FloorToInt(seconds / 60f)}:{seconds % 60f:00.0}";

    void OnGUI()
    {
        if (!showDebugHud) return;
        var style = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold };
        style.normal.textColor = Color.white;
        string state = IsFinished ? (IsNewRecord ? "  새 기록!" : "  게임 오버") : IsRunning ? "" : "  (움직이면 시작)";
        GUI.Label(new Rect(20, 16, 700, 40), $"버틴 시간 {Format(Elapsed)}{state}", style);
        style.fontSize = 20;
        GUI.Label(new Rect(20, 54, 700, 30), $"최고 기록 {Format(BestTime)}", style);
    }
}
