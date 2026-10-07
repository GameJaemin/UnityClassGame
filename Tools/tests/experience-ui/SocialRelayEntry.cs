using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class SocialRelayEntry
{
    static SocialRelayEntry()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("SocialRelayTest", false)) return;
            SessionState.EraseBool("SocialRelayTest");
            new GameObject("지연 Relay 검사").AddComponent<SocialRelayRunner>();
        };
    }
    public static void Run()
    {
        OnlineMultiplayerTests.Run(); OnlineSeriesTests.Run();
        PlayerSettings.companyName = "CodexTests";
        PlayerSettings.productName = System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"-smokeHost")>=0 ? "SocialRelayHost" : "SocialRelayClient";
        EditorSettings.enterPlayModeOptionsEnabled = false;
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        MapRoulette.SkipNextSelection = false;
        SessionState.SetBool("SocialRelayTest", true);
        EditorApplication.EnterPlaymode();
    }
}
