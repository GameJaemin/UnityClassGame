using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class UIConsistencySetup
{
    [MenuItem("LionIceRun/UI 정리·폰트 통일")]
    public static void Run()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UI/Fonts/MalgunGothicBold SDF.asset");
        if (font == null) { Debug.LogError("Step 7 UI 세팅으로 글꼴을 먼저 생성하세요."); return; }
        foreach (var root in scene.GetRootGameObjects())
            foreach (var component in root.GetComponentsInChildren<Component>(true))
                if (component is TMP_Text || component is MapRoulette || component is SurvivalTimer || component is Transform)
                    Undo.RecordObject(component, "UI 정리·폰트 통일");
        UIConsistency.Apply(scene, font);
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var roulette in root.GetComponentsInChildren<MapRoulette>(true))
            {
                if (roulette.stopButton != null) Undo.DestroyObjectImmediate(roulette.stopButton.gameObject);
                roulette.stopButton = null; roulette.buttonText = null;
                EditorUtility.SetDirty(roulette);
            }
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true)) EditorUtility.SetDirty(text);
            foreach (var timer in root.GetComponentsInChildren<SurvivalTimer>(true)) EditorUtility.SetDirty(timer);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("UI 정리 완료: 맑은 고딕 Bold 통일·기울임 제거·멈추기 버튼 삭제·자동 선택 안내. 기존 배치 유지.");
    }

    [MenuItem("LionIceRun/UI 정리·폰트 통일", true)]
    static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;
}
