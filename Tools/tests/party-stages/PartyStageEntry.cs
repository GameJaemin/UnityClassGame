using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
[InitializeOnLoad]
public static class PartyStageEntry
{
    static PartyStageEntry()
    {
        EditorApplication.playModeStateChanged += s =>
        {
            if (s == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("PartyStagesTest", false))
            { SessionState.EraseBool("PartyStagesTest"); new GameObject("PartyStagesTest").AddComponent<PartyStageRunner>(); }
        };
    }
    public static void Run()
    {
        OnlineMultiplayerTests.Run(); OnlineSeriesTests.Run();
        PlayerSettings.companyName = "CodexTests"; PlayerSettings.productName = "PartyStages";
        EditorSettings.enterPlayModeOptionsEnabled = false;
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        SessionState.SetBool("LionIceRun.MapToolPlay", true);
        SessionState.SetBool("PartyStagesTest", true); EditorApplication.EnterPlaymode();
    }
}
