using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
public static class BatchOnlineBuild
{
    public static void Run()
    {
        var actions = ScriptableObject.CreateInstance<InputActionAsset>();
        var map = actions.AddActionMap("Player");
        map.AddAction("Move", InputActionType.Value).AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s").With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
        map.AddAction("Jump", InputActionType.Button, "<Keyboard>/space");
        System.IO.File.WriteAllText("Assets/Smoke.inputactions", actions.ToJson());
        AssetDatabase.ImportAsset("Assets/Smoke.inputactions", ImportAssetOptions.ForceUpdate);
        InputSystem.actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Smoke.inputactions");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("OnlineSmoke", typeof(BatchOnlineRunner));
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/OnlineSmoke.unity");
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/OnlineSmoke.unity" }, target = BuildTarget.StandaloneWindows64,
            locationPathName = "SmokePlayer/OnlineSmoke.exe", options = BuildOptions.Development
        });
        if (report.summary.result != BuildResult.Succeeded) throw new Exception("Smoke build failed");
        Debug.Log("ONLINE_SMOKE_BUILD_PASSED");
    }
}
