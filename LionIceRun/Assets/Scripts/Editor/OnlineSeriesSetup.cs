using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class OnlineSeriesSetup
{
    [MenuItem("LionIceRun/온라인 5판 시리즈 UI 업데이트")]
    public static void Run()
    {
        var front = Object.FindAnyObjectByType<OnlineFrontend>(FindObjectsInactive.Include);
        if (front == null) { Debug.LogError("친구 멀티 세팅을 먼저 실행하세요."); return; }
        Apply(front); front.HidePanels();
        front.gameObject.SetActive(true); front.GetComponent<Canvas>().enabled = true;
        front.resultsPanel.SetActive(true);
        front.resultText.text = "라운드 종료 후 누적 순위와 점수가 표시됩니다.";
        front.returnButton.gameObject.SetActive(false);
        Selection.activeGameObject = front.resultsPanel; SceneView.RepaintAll();
        EditorSceneManager.MarkSceneDirty(front.gameObject.scene); EditorSceneManager.SaveScene(front.gameObject.scene); AssetDatabase.SaveAssets();
        Debug.Log("5판 시리즈 UI 저장 완료. Scene 뷰에 결과 화면을 표시합니다. 입력창·캐릭터 선택 등 디자인까지 적용하려면 UI 전체 업데이트 메뉴를 실행하세요. HUD 기존 배치는 유지합니다.");
    }
    [MenuItem("LionIceRun/온라인 5판 시리즈 UI 업데이트", true)]
    static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

    public static void Apply(OnlineFrontend front)
    {
        Undo.RecordObject(front, "5판 결과 표시 연결");
        front.resultsTitle = front.resultsPanel.transform.Find("Title").GetComponent<TMP_Text>();
        front.resultsHint = front.resultsPanel.transform.Find("Hint").GetComponent<TMP_Text>();
        front.resultsTitle.text = "1 / 5 라운드 결과";
        front.resultsHint.text = "8초 뒤 2 / 5 라운드 자동 시작";
        front.resultText.enableAutoSizing = true; front.resultText.fontSizeMin = 22f; front.resultText.fontSizeMax = 38f;
        front.resultsHint.enableAutoSizing = true; front.resultsHint.fontSizeMin = 16f; front.resultsHint.fontSizeMax = 24f;
        var existing = front.transform.Find("SeriesProgress");
        if (existing == null)
        {
            var go = new GameObject("SeriesProgress", typeof(RectTransform), typeof(TextMeshProUGUI));
            var rect = (RectTransform)go.transform; rect.SetParent(front.transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -28f); rect.sizeDelta = new Vector2(580f, 56f);
            var label = go.GetComponent<TextMeshProUGUI>();
            label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UI/Fonts/MalgunGothicBold SDF.asset");
            label.fontSharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/UI/Fonts/MalgunGothicBold OutlinedThin.mat");
            label.fontSize = 28f; label.color = Color.white; label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false; label.richText = false; existing = go.transform;
        }
        front.seriesStatus = existing.GetComponent<TMP_Text>(); front.seriesStatus.text = "1 / 5 라운드 · 총 0점";
        front.seriesStatus.gameObject.SetActive(false);
        EditorUtility.SetDirty(front); EditorUtility.SetDirty(front.resultText); EditorUtility.SetDirty(front.resultsHint);
        if (front.session.network != null) { front.session.network.NetworkConfig.ProtocolVersion = OnlineRules.Protocol; EditorUtility.SetDirty(front.session.network); }
    }
}
