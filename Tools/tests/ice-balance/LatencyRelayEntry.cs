using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class LatencyRelayEntry
{
    static LatencyRelayEntry()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("LatencyRelayTest", false)) return;
            SessionState.EraseBool("LatencyRelayTest");
            new GameObject("지연 Relay 검사").AddComponent<LatencyRelayRunner>();
        };
    }
    public static void Run()
    {
        OnlineMultiplayerTests.Run(); OnlineSeriesTests.Run();
        PlayerSettings.companyName = "CodexTests"; PlayerSettings.productName = "LatencyRelay";
        EditorSettings.enterPlayModeOptionsEnabled = false;
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        MapRoulette.SkipNextSelection = false;
        SessionState.SetBool("LatencyRelayTest", true);
        EditorApplication.EnterPlaymode();
    }
}
