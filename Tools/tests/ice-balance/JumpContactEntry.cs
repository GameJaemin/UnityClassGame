using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class JumpContactEntry
{
    static JumpContactEntry()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("JumpContactTest", false)) return;
            SessionState.EraseBool("JumpContactTest");
            new GameObject("연속 점프 밟힘 검사").AddComponent<JumpContactRunner>();
        };
    }
    public static void Run()
    {
        OnlineMultiplayerTests.Run();
        OnlineSeriesTests.Run();
        PlayerSettings.companyName = "CodexTests";
        PlayerSettings.productName = "JumpContact";
        EditorSettings.enterPlayModeOptionsEnabled = false;
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        MapRoulette.SkipNextSelection = true;
        SessionState.SetBool("JumpContactTest", true);
        EditorApplication.EnterPlaymode();
    }
}
