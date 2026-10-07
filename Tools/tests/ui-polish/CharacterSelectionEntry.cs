using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
[InitializeOnLoad]
public static class CharacterSelectionEntry
{
    static CharacterSelectionEntry()
    {
        if (SessionState.GetBool("CharacterSelectionReload", false))
            EditorApplication.delayCall += () =>
            {
                SessionState.EraseBool("CharacterSelectionReload");
                Object.FindAnyObjectByType<CharacterSelectionRunner>().ResumeAfterReload();
            };
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("CharacterSelectionTest", false)) return;
            SessionState.EraseBool("CharacterSelectionTest");
            new GameObject("CharacterSelectionTest").AddComponent<CharacterSelectionRunner>();
        };
    }
    public static void Run()
    {
        PlayerSettings.companyName = "CodexTests"; PlayerSettings.productName = "CharacterSelection";
        EditorSettings.enterPlayModeOptionsEnabled = false;
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        SessionState.SetBool("CharacterSelectionTest", true);
        EditorApplication.EnterPlaymode();
    }
}
