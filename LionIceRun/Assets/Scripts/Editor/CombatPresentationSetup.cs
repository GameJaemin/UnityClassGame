using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 저장된 밀치기 수치와 이름표를 메뉴로 갱신한다. 기존 이름표의 사용자 배치는 유지한다.
public static class CombatPresentationSetup
{
    const string ActorPath = "Assets/Prefabs/OnlinePlayer.prefab";

    [MenuItem("LionIceRun/밀치기·이름표 업데이트")]
    public static void Run()
    {
        var manager = Object.FindAnyObjectByType<GameManager>();
        if (manager == null || manager.player == null)
        { Debug.LogError("Main 씬의 플레이어를 찾지 못했습니다."); return; }
        ApplyPlayer(manager.player.gameObject, "플레이어");
        UpdateOnlinePrefab();
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("밀치기 2.4m·0.38초, 동작 0.55초, 캐릭터 이름표 저장 완료. 변경 반영에는 새 빌드가 필요합니다.");
    }

    [MenuItem("LionIceRun/밀치기·이름표 업데이트", true)]
    static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

    public static void UpdateOnlinePrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ActorPath) == null) return;
        var root = PrefabUtility.LoadPrefabContents(ActorPath);
        try { ApplyPlayer(root, "친구 이름"); PrefabUtility.SaveAsPrefabAsset(root, ActorPath); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    public static void ApplyPlayer(GameObject root, string previewName)
    {
        PresentationEffectsSetup.ApplyVfx(root);
        var player = root.GetComponent<PlayerController>();
        Undo.RecordObject(player, "밀치기 강화");
        player.pushDistance = 2.4f; player.pushDuration = 0.38f;
        player.pushLunge = 2.2f; player.pushActiveTime = 0.2f;
        var animation = root.GetComponent<CharacterAnimator>();
        if (animation != null)
        {
            Undo.RecordObject(animation, "밀치기 동작 강화");
            animation.pushPoseTime = 0.55f; animation.pushLean = 16f; animation.pushModelReach = 0.16f;
            EditorUtility.SetDirty(animation);
        }
        EditorUtility.SetDirty(player);
        var nameplate = root.GetComponent<PlayerNameplate>();
        if (nameplate == null) nameplate = Undo.AddComponent<PlayerNameplate>(root);
        var plate = root.transform.Find("Nameplate") as RectTransform;
        if (plate == null)
        {
            var go = new GameObject("Nameplate", typeof(RectTransform), typeof(Canvas));
            Undo.RegisterCreatedObjectUndo(go, "이름표 생성");
            plate = (RectTransform)go.transform; plate.SetParent(root.transform, false);
            plate.localPosition = new Vector3(0f, 2.2f, 0f); plate.localScale = Vector3.one * 0.007f;
            plate.sizeDelta = new Vector2(420f, 64f);
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true; canvas.sortingOrder = 20;
            var text = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
            text.transform.SetParent(plate, false);
            var rect = (RectTransform)text.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var label = text.GetComponent<TextMeshProUGUI>();
            label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UI/Fonts/MalgunGothicBold SDF.asset");
            label.fontSharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/UI/Fonts/MalgunGothicBold OutlinedThin.mat");
            label.fontSize = 34f; label.enableAutoSizing = true; label.fontSizeMin = 20f; label.fontSizeMax = 34f;
            label.alignment = TextAlignmentOptions.Center; label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis; label.raycastTarget = false; label.richText = false;
        }
        Undo.RecordObject(nameplate, "이름표 연결");
        nameplate.plate = plate; nameplate.label = plate.GetComponentInChildren<TMP_Text>(true);
        nameplate.SetName(previewName, false, false);
        EditorUtility.SetDirty(nameplate);
        if (nameplate.label != null) EditorUtility.SetDirty(nameplate.label);
    }
}
