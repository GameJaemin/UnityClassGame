using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class MovingSafetyEntry
{
    static MovingSafetyEntry()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("MovingSafetyTest", false)) return;
            SessionState.EraseBool("MovingSafetyTest");
            new GameObject("MovingSafetyTest").AddComponent<MovingSafetyRunner>();
        };
    }
    public static void Run()
    {
        OnlineMultiplayerTests.Run(); OnlineSeriesTests.Run();
        PlayerSettings.companyName = "CodexTests"; PlayerSettings.productName = "MovingSafety";
        EditorSettings.enterPlayModeOptionsEnabled = false;
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        SessionState.SetBool("LionIceRun.MapToolPlay", true);
        SessionState.SetBool("MovingSafetyTest", true);
        EditorApplication.EnterPlaymode();
    }
}
