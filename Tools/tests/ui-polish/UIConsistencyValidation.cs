using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 복사 프로젝트에서 원본 씬의 텍스트·숨은 템플릿·사용자 배치와 버튼 없는 룰렛을 검사한다.
public static class UIConsistencyValidation
{
    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UI/Fonts/MalgunGothicBold SDF.asset");
        var positions = new Dictionary<RectTransform, Vector2>();
        var texts = new List<TMP_Text>();
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var rect in root.GetComponentsInChildren<RectTransform>(true)) positions[rect] = rect.anchoredPosition;
            texts.AddRange(root.GetComponentsInChildren<TMP_Text>(true));
        }
        Check(texts.Count > 50 && font != null, "전체 UI 및 숨은 템플릿 로드");
        texts[0].fontStyle |= FontStyles.Italic;
        UIConsistencySetup.Run();
        var roulette = UnityEngine.Object.FindAnyObjectByType<MapRoulette>(FindObjectsInactive.Include);
        Check(roulette.stopButton == null && roulette.buttonText == null, "구형 멈추기 버튼·참조 제거");
        var hint = roulette.rouletteGroup.transform.Find("Hint").GetComponent<TMP_Text>();
        Check(hint.text == UIConsistency.MapHint, "자동 선택 안내 일치");
        foreach (var text in texts)
            if (text != null) Check(text.font == font && (text.fontStyle & FontStyles.Italic) == 0, "글꼴·기울임: " + text.name);
        foreach (var entry in positions)
            if (entry.Key != null) Check(entry.Key.anchoredPosition == entry.Value, "배치 유지: " + entry.Key.name);
        UIConsistencySetup.Run();
        Check(roulette.stopButton == null, "재적용 시 버튼 재생성 없음");
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        roulette = UnityEngine.Object.FindAnyObjectByType<MapRoulette>(FindObjectsInactive.Include);
        Check(roulette.stopButton == null, "씬 저장·재열기 유지");
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        typeof(MapRoulette).GetMethod("Awake", flags).Invoke(roulette, null);
        Check(roulette.enabled, "버튼 없는 씬에서도 룰렛 초기화 성공");
        var files = (List<string>)typeof(MapRoulette).GetField("files", flags).GetValue(roulette);
        var names = (List<string>)typeof(MapRoulette).GetField("mapNames", flags).GetValue(roulette);
        var shots = (List<Sprite>)typeof(MapRoulette).GetField("screenshots", flags).GetValue(roulette);
        files.Add("default_6x6.json"); names.Add("검사 맵"); shots.Add(null);
        roulette.BeginChoosing();
        typeof(MapRoulette).GetField("soloStopAt", flags).SetValue(roulette, -1f);
        typeof(MapRoulette).GetMethod("Update", flags).Invoke(roulette, null);
        Check((bool)typeof(MapRoulette).GetField("stopping", flags).GetValue(roulette), "시리즈 없는 솔로도 자동 감속·정지");
        roulette.ShowTitle();
        Check(Time.timeScale == 1f && roulette.startButton.interactable, "타이틀 복귀·입력 및 일시정지 복구");
        OnlineMultiplayerTests.Run(); OnlineSeriesTests.Run();
        Debug.Log("UI_CONSISTENCY_PASSED: " + texts.Count + " 텍스트, 기존 배치·버튼 삭제·자동 선택·타이틀·기존 59개 규칙. EXE 빌드 없음");
    }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
