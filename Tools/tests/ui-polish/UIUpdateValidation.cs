using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 복사 프로젝트 전용. 씬 적용·미리보기·반복 실행·타이틀 진입을 확인한다. 빌드는 하지 않는다.
public static class UIUpdateValidation
{
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        var front = UnityEngine.Object.FindAnyObjectByType<OnlineFrontend>(FindObjectsInactive.Include);
        var positions = new Dictionary<RectTransform, Vector2>();
        foreach (var rect in GameObject.Find("GameUI").GetComponentsInChildren<RectTransform>(true)) positions[rect] = rect.anchoredPosition;
        front.gameObject.SetActive(false);
        UIPolishSetup.Run();
        Check(front.gameObject.activeSelf && front.modesPanel.activeInHierarchy, "비활성 UI 검색과 모드 미리보기");
        Check(front.characterSelection != null && !front.nickname.richText && !front.nickname.textComponent.richText, "모델 선택과 입력창 동시 적용");
        Check(front.seriesStatus != null && front.resultsHint != null && front.resultsTitle != null, "5판 UI 동시 연결");
        var position = front.nickname.GetComponent<RectTransform>().anchoredPosition + Vector2.right * 3;
        front.nickname.GetComponent<RectTransform>().anchoredPosition = position;
        UIPolishSetup.Run();
        Check(front.nickname.GetComponent<RectTransform>().anchoredPosition == position, "반복 적용 사용자 배치 보존");
        foreach (var entry in positions) Check(entry.Key.anchoredPosition == entry.Value, "게임 HUD 위치 보존");
        OnlineSeriesSetup.Run();
        Check(front.resultsPanel.activeInHierarchy && !front.returnButton.gameObject.activeSelf, "중간 결과 미리보기");
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        front = UnityEngine.Object.FindAnyObjectByType<OnlineFrontend>(FindObjectsInactive.Include);
        Check(front.resultsPanel.activeInHierarchy && front.characterSelection != null, "씬 재열기 저장 유지");
        typeof(OnlineFrontend).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(front, null);
        Check(!front.resultsPanel.activeSelf && !front.modesPanel.activeSelf && !front.seriesStatus.gameObject.activeSelf, "게임 시작 시 미리보기 닫기");
        OnlineSeriesTests.Run();
        Debug.Log("UI_UPDATE_VALIDATION_PASSED: 전체 적용·비활성 검색·씬 저장·미리보기·사용자 배치 보존·시작 초기화, 빌드 없음");
    }
    static void Check(bool value, string name) { if (!value) throw new Exception(name); }
}
