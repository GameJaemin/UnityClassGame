using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

// 원본 에디터와 별도로 복사 프로젝트에서 실행한다.
public static class BatchWebBuild
{
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        OnlineMultiplayerTests.Run(); OnlineSeriesTests.Run();
        WebBuildSupport.Prepare();
        // 같은 JSON을 읽는지 확인한다. 수정 후에는 새 객체를 돌려줘야 한다.
        foreach (string file in MapData.ListFiles())
        {
            var disk = MapData.Load(file);
            var packed = WebMapCatalog.Load(file);
            if (OnlineRules.MapHash(disk) != OnlineRules.MapHash(packed))
                throw new Exception("웹/Windows 맵 내용 불일치: " + file);
            if (ReferenceEquals(packed, WebMapCatalog.Load(file))) throw new Exception("맵 객체 재사용");
        }
        Debug.Log("WEB_MAP_PARITY_PASSED: " + MapData.ListFiles().Length);
        var front = UnityEngine.Object.FindAnyObjectByType<OnlineFrontend>(FindObjectsInactive.Include);
        front.HidePanels(); front.session.roulette.ShowTitle();
        EditorSceneManager.MarkSceneDirty(front.gameObject.scene);
        EditorSceneManager.SaveScene(front.gameObject.scene);
        string output = File.ReadAllText("BuildOutput.txt").Trim();
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { "Assets/Scenes/Main.unity" }, target = BuildTarget.WebGL,
            locationPathName = output, options = BuildOptions.None
        });
        File.WriteAllText("BuildResult.txt", $"{report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nBytes: {report.summary.totalSize}\nDuration: {report.summary.totalTime}\n");
        if (report.summary.result != BuildResult.Succeeded || report.summary.totalErrors > 0)
            throw new Exception("WebGL 빌드 실패 또는 빌드 중 오류 발견");
        Debug.Log("WEB_RELEASE_BUILD_PASSED: " + output);
    }
}
