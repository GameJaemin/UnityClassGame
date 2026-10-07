using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
[InitializeOnLoad] public static class ResultUiEntry {
 static ResultUiEntry() { EditorApplication.playModeStateChanged += s=>{ if(s==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("ResultUiTest",false)) { SessionState.EraseBool("ResultUiTest"); new GameObject("ResultUiTest").AddComponent<ResultUiRunner>(); } }; }
 public static void Run() { OnlineMultiplayerTests.Run(); OnlineSeriesTests.Run(); Application.runInBackground=true; EditorSettings.enterPlayModeOptionsEnabled=false; PlayerSettings.companyName="CodexTests"; PlayerSettings.productName="ResultUiRegression"; EditorSceneManager.OpenScene("Assets/Scenes/Main.unity"); SessionState.SetBool("ResultUiTest",true); EditorApplication.EnterPlaymode(); }
}
