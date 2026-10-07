using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class LatencyPredictionEntry
{
    static LatencyPredictionEntry()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("LatencyPredictionTest", false)) return;
            SessionState.EraseBool("LatencyPredictionTest");
            new GameObject("클라이언트 입력 예측 검사").AddComponent<LatencyPredictionRunner>();
        };
    }
    public static void Run()
    {
        OnlineMultiplayerTests.Run(); OnlineSeriesTests.Run();
        PlayerSettings.companyName = "CodexTests"; PlayerSettings.productName = "LatencyPrediction";
        EditorSettings.enterPlayModeOptionsEnabled = false;
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        MapRoulette.SkipNextSelection = true;
        SessionState.SetBool("LatencyPredictionTest", true);
        EditorApplication.EnterPlaymode();
    }
}
