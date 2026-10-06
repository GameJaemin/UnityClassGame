using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

// 사용자 씬을 건드리지 않는 복사본에서 메뉴의 저장 결과와 Windows 빌드를 확인한다.
public static class BatchEffectsBuild
{
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        OnlineMultiplayerTests.Run(); PresentationEffectsSetup.Run();
        CombatPresentationSetup.Run();
        var manager = UnityEngine.Object.FindAnyObjectByType<GameManager>();
        var player = manager.player;
        Check(Mathf.Approximately(player.pushDistance, 2.4f), "씬 밀치기 거리 저장");
        Check(Mathf.Approximately(player.GetComponent<CharacterAnimator>().pushPoseTime, 0.55f), "씬 동작 시간 저장");
        var plate = player.GetComponent<PlayerNameplate>();
        Check(plate != null && plate.label != null && plate.label.font != null, "씬 이름표 글꼴/연결");
        plate.SetName("<친구>\n", true, false);
        Check(plate.label.text == "친구 · 나", "이름표 정규화와 내 캐릭터 표시");
        Vector3 original = plate.plate.localPosition;
        plate.plate.localPosition += Vector3.up * 0.25f;
        CombatPresentationSetup.Run();
        Check(plate.plate.localPosition == original + Vector3.up * 0.25f, "재실행 시 이름표 사용자 위치 보존");
        Check(player.GetComponentsInChildren<UnityEngine.Canvas>(true).Length == 1, "이름표 중복 생성 방지");
        plate.plate.localPosition = original;
        CombatPresentationSetup.Run();
        var prefab = PrefabUtility.LoadPrefabContents("Assets/Prefabs/OnlinePlayer.prefab");
        try
        {
            Check(Mathf.Approximately(prefab.GetComponent<PlayerController>().pushDistance, 2.4f), "멀티 템플릿 밀치기 저장");
            var remote = prefab.GetComponent<PlayerNameplate>();
            Check(remote != null && remote.label != null && remote.label.font != null, "멀티 템플릿 이름표 직렬화");
            remote.SetName("공격형 1", false, true);
            Check(remote.label.text == "공격형 1 · AI", "봇 표시");
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        Debug.Log("PRESENTATION_CHECKS_PASSED: 9개");
var countdown = UnityEngine.Object.FindAnyObjectByType<MatchCountdown>();
        Check(countdown != null && countdown.number != null && countdown.number.fontSize == 220f, "대형 카운트다운 연결");
        Check(countdown.number.rectTransform.anchoredPosition == Vector2.zero, "화면 중앙 카운트다운");
        Check(!countdown.number.raycastTarget, "카운트다운 입력 방해 없음");
        countdown.Show(3); countdown.Show(2); Check(countdown.number.text == "2", "카운트다운 숫자 갱신");
        countdown.Hide(); Check(!countdown.number.gameObject.activeSelf, "경기 시작 뒤 숨김");
        countdown.number.gameObject.SetActive(true);
        var fx = player.GetComponent<PushVfx>();
        Check(fx != null && fx.particles != null && !fx.particles.main.playOnAwake, "밀치기 파티클 연결과 자동 재생 방지");
        Check(fx.particles.GetComponent<ParticleSystemRenderer>().sharedMaterial != null, "밀치기 파티클 재질 저장");
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("EFFECTS_CHECKS_PASSED: 7개");
        PlayerSettings.runInBackground = true;
        string output = File.ReadAllText("BuildOutput.txt").Trim();
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { "Assets/Scenes/Main.unity" }, target = BuildTarget.StandaloneWindows64,
            locationPathName = output, options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded) throw new Exception("업데이트 빌드 실패");
        Debug.Log("WINDOWS_UPDATE_BUILD_PASSED: " + output);
    }

    static void Check(bool value, string name)
    { if (!value) throw new Exception("표현 검사 실패: " + name); }
}
