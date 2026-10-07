using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class MainRegressionBuild {
 public static void Run() {
  OnlineMultiplayerTests.Run(); OnlineSeriesTests.Run();
  EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
  var front=UnityEngine.Object.FindAnyObjectByType<OnlineFrontend>(FindObjectsInactive.Include);
  front.HidePanels(); front.session.roulette.ShowTitle();
  EditorSceneManager.SaveScene(front.gameObject.scene);
  PlayerSettings.companyName="CodexTests"; PlayerSettings.productName="MainOnlineRegression";
  PlayerSettings.runInBackground=true;
  var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=new[]{"Assets/Scenes/Main.unity"}, target=BuildTarget.StandaloneWindows64, locationPathName=File.ReadAllText("BuildOutput.txt").Trim(), options=BuildOptions.Development });
  File.WriteAllText("BuildResult.txt",report.summary.result + " errors=" + report.summary.totalErrors);
  if(report.summary.result!=BuildResult.Succeeded) throw new Exception("Regression player build failed");
 }
}
