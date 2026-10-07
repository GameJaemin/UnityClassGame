using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
[InitializeOnLoad] public static class SoloSeriesEntry {
 static SoloSeriesEntry() { EditorApplication.playModeStateChanged += s=>{ if(s==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("SoloTest",false)) { SessionState.EraseBool("SoloTest"); new GameObject("SoloTest").AddComponent<SoloSeriesRunner>(); } }; }
 public static void Run() { OnlineMultiplayerTests.Run(); OnlineSeriesTests.Run(); Application.runInBackground=true; EditorSettings.enterPlayModeOptionsEnabled=false; PlayerSettings.companyName="CodexTests"; PlayerSettings.productName="SoloSeriesRegression"; EditorSceneManager.OpenScene("Assets/Scenes/Main.unity"); SessionState.SetBool("SoloTest",true); EditorApplication.EnterPlaymode(); }
}
