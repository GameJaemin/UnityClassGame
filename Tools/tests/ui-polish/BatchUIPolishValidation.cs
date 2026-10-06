using System;
using System.IO;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 별도 프로젝트에서 컴파일·저장·화면 렌더만 확인한다. 플레이어 빌드는 하지 않는다.
public static class BatchUIPolishValidation
{
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        OnlineMultiplayerTests.Run();
        var hud = GameObject.Find("GameUI");
        var positions = new Dictionary<RectTransform, Vector2>();
        if (hud != null) foreach (var rect in hud.GetComponentsInChildren<RectTransform>(true)) positions[rect] = rect.anchoredPosition;
        UIPolishSetup.Run();
        var front = UnityEngine.Object.FindAnyObjectByType<OnlineFrontend>();
        Check(!front.nickname.richText && !front.nickname.textComponent.richText, "입력창 IME 태그 설정 일치");
        Check(front.nickname.GetComponent<Image>().color == Color.white && front.nickname.textComponent.color == UIPolishSetup.Ink, "밝은 입력 배경과 짙은 글자");
        Check(front.nickname.textComponent.fontSharedMaterial != null, "입력 글자 재질");
        var picker = front.characterSelection;
        Check(picker != null && picker.models.Length == 3 && picker.previous != null && picker.next != null, "모델 3종과 화살표 연결");
        Check(picker.previewCamera != null && picker.image.texture == picker.previewCamera.targetTexture, "모델 카메라·이미지 연결");
        picker.Select(-1); Check(picker.Index == 2 && picker.models[2].activeSelf && !picker.models[0].activeSelf, "왼쪽 순환 선택");
        picker.Select(3); Check(picker.Index == 0 && picker.models[0].activeSelf && !picker.models[2].activeSelf, "오른쪽 순환 선택");
        picker.SetInteractable(false); Check(!picker.previous.interactable && !picker.next.interactable, "연결 중 캐릭터 변경 차단"); picker.SetInteractable(true);
        Check(!front.skin.gameObject.activeSelf, "이름 드롭다운 교체");
        Check(front.characterPortraits.Length == 3 && Array.TrueForAll(front.characterPortraits, x => x != null), "실제 모델 초상화 3종 저장");
        var nicknamePosition = front.nickname.GetComponent<RectTransform>().anchoredPosition;
        front.nickname.GetComponent<RectTransform>().anchoredPosition += Vector2.right * 3;
        UIPolishSetup.Run();
        Check(front.nickname.GetComponent<RectTransform>().anchoredPosition == nicknamePosition + Vector2.right * 3, "재적용 시 사용자 배치 보존");
        front.nickname.GetComponent<RectTransform>().anchoredPosition = nicknamePosition;
        foreach (var entry in positions) Check(entry.Key.anchoredPosition == entry.Value, "HUD 배치 보존: " + entry.Key.name);
        Check(front.modesPanel.transform.Find("Title").GetComponent<TMP_Text>().color == new Color(1f, 0.85f, 0.23f), "재적용 시 제목 색상 보존");
        EditorSceneManager.SaveOpenScenes();
        Render(front);
        Debug.Log("UI_POLISH_VALIDATION_PASSED: 입력창·3D 선택·초상화·재적용·HUD 보존, 플레이어 빌드 없음");
    }

    static void Render(OnlineFrontend front)
    {
        foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)) canvas.enabled = canvas.gameObject == front.gameObject;
        front.OpenModes(); front.nickname.text = "얼음 친구"; front.notice.text = "방 코드를 공유해 친구와 함께 플레이하세요.";
        front.characterSelection.previewCamera.Render();
        var canvasRoot = front.GetComponent<Canvas>();
        var camera = Camera.main;
        var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
        camera.targetTexture = rt;
        canvasRoot.renderMode = RenderMode.ScreenSpaceCamera; canvasRoot.worldCamera = camera; canvasRoot.planeDistance = 5f;
        Canvas.ForceUpdateCanvases();
        foreach (var text in front.GetComponentsInChildren<TMP_Text>(true)) text.ForceMeshUpdate();
        camera.Render();
        RenderTexture.active = rt;
        var image = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); image.Apply();
        File.WriteAllBytes("D:/UnityClassGame/docs/screenshots/ui-polish.png", image.EncodeToPNG());
        front.ShowLobby(); front.roomCode.text = "방 코드  AB12CD";
        front.lobbyNotice.text = "모두 준비됐어요! 경기를 시작하세요.";
        string[] names = { "얼음 친구 · 방장", "북극 여행자", "호랑이 친구", "친구를 기다려요" };
        for (int i = 0; i < 4; i++)
        {
            front.slots[i].text = names[i] + (i < 3 ? "\n준비 완료" : "");
            front.slotPortraits[i].enabled = i < 3;
            if (i < 3) front.slotPortraits[i].sprite = front.characterPortraits[i];
        }
        Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = rt;
        image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); image.Apply();
        File.WriteAllBytes("D:/UnityClassGame/docs/screenshots/ui-polish-lobby.png", image.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(image); RenderTexture.active = null; camera.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(rt);
    }
    static void Check(bool value, string message) { if (!value) throw new Exception("UI 검사 실패: " + message); }
}
