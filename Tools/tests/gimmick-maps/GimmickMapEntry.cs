using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
[InitializeOnLoad] public static class GimmickMapEntry
{
    static GimmickMapEntry() { EditorApplication.playModeStateChanged += s => {
        if (s == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("GimmickTest", false)) {
            SessionState.EraseBool("GimmickTest"); new GameObject("검증").AddComponent<GimmickMapRunner>();
        }
    }; }
    public static void Run() {
        OnlineMultiplayerTests.Run(); OnlineSeriesTests.Run();
        EditorSettings.enterPlayModeOptionsEnabled = false;
        PlayerSettings.companyName = "CodexTests"; PlayerSettings.productName = "GimmickMaps";
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        MapRoulette.SkipNextSelection = true;
        SessionState.SetBool("GimmickTest", true); EditorApplication.EnterPlaymode();
    }
}
