using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

// 복사 프로젝트 Assets/Scripts/Editor에 넣어 실행한다. 원본 씬은 수정하지 않는다.
public static class BatchReleaseBuild
{
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        OnlineMultiplayerTests.Run(); OnlineSeriesTests.Run();
        var front = UnityEngine.Object.FindAnyObjectByType<OnlineFrontend>(FindObjectsInactive.Include);
        if (front == null || front.characterSelection == null || front.seriesStatus == null
            || front.resultsTitle == null || front.resultsHint == null || front.nickname.richText
            || front.nickname.textComponent.richText)
            throw new Exception("전체 UI 업데이트가 씬에 저장되어 있지 않습니다.");
        // 편집용 미리보기는 닫아 배포 씬을 타이틀 상태로 저장한다.
        front.HidePanels(); front.session.roulette.ShowTitle();
        EditorSceneManager.MarkSceneDirty(front.gameObject.scene);
        EditorSceneManager.SaveScene(front.gameObject.scene);
        PlayerSettings.runInBackground = true;
        string output = File.ReadAllText("BuildOutput.txt").Trim();
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { "Assets/Scenes/Main.unity" }, target = BuildTarget.StandaloneWindows64,
            locationPathName = output, options = BuildOptions.None
        });
        File.WriteAllText("BuildResult.txt", $"{report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nBytes: {report.summary.totalSize}\nDuration: {report.summary.totalTime}\n");
        if (report.summary.result != BuildResult.Succeeded) throw new Exception("Windows 빌드 실패");
        Debug.Log("WINDOWS_RELEASE_BUILD_PASSED: " + output);
    }
}
