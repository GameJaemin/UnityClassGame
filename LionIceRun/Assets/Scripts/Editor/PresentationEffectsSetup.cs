using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 카운트다운과 입자 설정은 씬/프리팹에 저장하고 기존 사용자 배치를 보존한다.
public static class PresentationEffectsSetup
{
    [MenuItem("LionIceRun/카운트다운·밀치기 효과 업데이트")]
    public static void Run()
    {
        CombatPresentationSetup.Run();
        CreateCountdown();
        EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets();
        Debug.Log("중앙 대형 카운트다운과 밀치기 입자 효과 저장 완료. 새 빌드에 반영하세요.");
    }
    [MenuItem("LionIceRun/카운트다운·밀치기 효과 업데이트", true)]
    static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

    public static void CreateCountdown()
    {
        GameObject root = GameObject.Find("MatchCountdownUI");
        if (root != null) return;
        root = new GameObject("MatchCountdownUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(MatchCountdown));
        Undo.RegisterCreatedObjectUndo(root, "중앙 카운트다운 생성");
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 115;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = 0.5f;
        var go = new GameObject("Number", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(root.transform, false);
        var rect = (RectTransform)go.transform; rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
        rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(900f, 400f);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UI/Fonts/MalgunGothicBold SDF.asset");
        text.fontSharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/UI/Fonts/MalgunGothicBold Outlined.mat");
        text.fontSize = 220f; text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(1f, 0.86f, 0.25f); text.raycastTarget = false; text.text = "3";
        root.GetComponent<MatchCountdown>().number = text;
        // Scene view에서는 미리 볼 수 있도록 켜고, Awake에서 경기 전에는 숨긴다.
        EditorSceneManager.MarkSceneDirty(root.scene);
    }

    public static void ApplyVfx(GameObject player)
    {
        var effect = player.GetComponent<PushVfx>() ?? player.AddComponent<PushVfx>();
        var old = player.transform.Find("PushParticles");
        if (old != null) { effect.particles = old.GetComponent<ParticleSystem>(); return; }
        var go = new GameObject("PushParticles", typeof(ParticleSystem)); go.transform.SetParent(player.transform, false);
        var system = go.GetComponent<ParticleSystem>(); system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main; main.loop = false; main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.38f); main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f); main.maxParticles = 40;
        main.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(0.55f, 0.9f, 1f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = system.emission; emission.enabled = false;
        var shape = system.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 22f; shape.radius = 0.3f;
        var size = system.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
        var color = system.colorOverLifetime; color.enabled = true;
        var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.55f, 0.9f, 1f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }); color.color = gradient;
        var renderer = system.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/IceParticle.mat");
        renderer.renderMode = ParticleSystemRenderMode.Stretch; renderer.lengthScale = 1.8f; renderer.velocityScale = 0.1f;
        effect.particles = system; EditorUtility.SetDirty(effect);
    }
}
