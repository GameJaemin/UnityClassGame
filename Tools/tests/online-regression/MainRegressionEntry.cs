using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
[InitializeOnLoad] public static class MainRegressionEntry {
 static MainRegressionEntry() { EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("MainRegression", false)) { SessionState.EraseBool("MainRegression"); new GameObject("MainRegression").AddComponent<MainRegressionRunner>(); } }; }
 public static void Run() { Application.runInBackground = true; EditorApplication.isPaused = false; OnlineMultiplayerTests.Run(); OnlineSeriesTests.Run(); EditorSettings.enterPlayModeOptionsEnabled = false; PlayerSettings.companyName="CodexTests"; PlayerSettings.productName="MainOnlineRegression"; EditorSceneManager.OpenScene("Assets/Scenes/Main.unity"); SessionState.SetBool("MainRegression",true); EditorApplication.EnterPlaymode(); }
}


