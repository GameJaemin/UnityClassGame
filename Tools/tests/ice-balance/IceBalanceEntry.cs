using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
[InitializeOnLoad] public static class IceBalanceEntry
{
    static IceBalanceEntry() { EditorApplication.playModeStateChanged += s => {
        if (s == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("IceBalanceTest", false)) {
            SessionState.EraseBool("IceBalanceTest"); new GameObject("얼음 밸런스 검사").AddComponent<IceBalanceRunner>();
        }
    }; }
    public static void Run() {
        OnlineMultiplayerTests.Run(); OnlineSeriesTests.Run();
        EditorSettings.enterPlayModeOptionsEnabled = false;
        PlayerSettings.companyName = "CodexTests"; PlayerSettings.productName = "IceBalance";
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        MapRoulette.SkipNextSelection = true;
        SessionState.SetBool("IceBalanceTest", true); EditorApplication.EnterPlaymode();
    }
}
