using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 복사 프로젝트의 에디터 Play 모드로 실제 Relay 5판을 검사한다. BuildPipeline을 사용하지 않는다.
[InitializeOnLoad]
public static class SeriesEditorEntry
{
    static SeriesEditorEntry()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("SeriesSmoke.Pending", false)) return;
            SessionState.EraseBool("SeriesSmoke.Pending");
            new GameObject("SeriesSmoke").AddComponent<SeriesPlayRunner>();
        };
    }
    public static void Run()
    {
        OnlineMultiplayerTests.Run(); OnlineSeriesTests.Run();
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        var hud = GameObject.Find("GameUI");
        var rects = hud.GetComponentsInChildren<RectTransform>(true);
        var before = rects.Select(r => r.anchoredPosition).ToArray();
        OnlineSeriesSetup.Run();
        var front = UnityEngine.Object.FindAnyObjectByType<OnlineFrontend>();
        if (front.resultsTitle == null || front.resultsHint == null || front.seriesStatus == null
            || !front.resultText.enableAutoSizing || front.seriesStatus.raycastTarget) throw new Exception("시리즈 UI 저장 연결 실패");
        var original = front.seriesStatus.rectTransform.anchoredPosition;
        front.seriesStatus.rectTransform.anchoredPosition += Vector2.right * 7;
        OnlineSeriesSetup.Run();
        if (front.seriesStatus.rectTransform.anchoredPosition != original + Vector2.right * 7) throw new Exception("시리즈 표시 사용자 위치 변경");
        for (int i = 0; i < rects.Length; i++) if (rects[i].anchoredPosition != before[i]) throw new Exception("기존 HUD 배치 변경");
        Debug.Log("ONLINE_SERIES_UI_PASSED: 저장 연결·사용자 위치·기존 HUD 보존");
        PlayerSettings.companyName = "CodexTests"; PlayerSettings.productName = "LionIceRunSeriesValidation";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool("SeriesSmoke.Pending", true);
        EditorApplication.EnterPlaymode();
    }
}
