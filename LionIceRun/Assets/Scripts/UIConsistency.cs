using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// 기존 씬의 배치를 유지하면서 숨은 템플릿까지 글꼴과 자동 선택 안내를 정리한다.
public static class UIConsistency
{
    public const string MapHint = "맵이 자동으로 선택돼요. 선택이 끝나면 경기가 시작됩니다!";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TMP_FontAsset font = null;
#if UNITY_WEBGL && !UNITY_EDITOR
        font = Resources.Load<TMP_FontAsset>("WebRuntime/KoreanBold");
#else
        foreach (var candidate in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            if (candidate.name == "MalgunGothicBold SDF") { font = candidate; break; }
#endif
        Apply(scene, font);
    }

    public static void Apply(Scene scene, TMP_FontAsset font)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;
        bool hasHud = false;
        foreach (var root in scene.GetRootGameObjects())
            hasHud |= root.GetComponentInChildren<GameHUD>(true) != null;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (hasHud)
                foreach (var timer in root.GetComponentsInChildren<SurvivalTimer>(true))
                    timer.showDebugHud = false;
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                ApplyWebFont(text);
#else
                if (font != null && text.font != font)
                {
                    // 다른 글꼴의 atlas 재질을 계속 사용하면 글자가 깨진다.
                    text.font = font;
                    text.fontSharedMaterial = font.material;
                }
#endif
                text.fontStyle &= ~FontStyles.Italic;
            }
            foreach (var roulette in root.GetComponentsInChildren<MapRoulette>(true))
            {
                if (roulette.stopButton != null) roulette.stopButton.gameObject.SetActive(false);
                if (roulette.rouletteGroup == null) continue;
                var hint = roulette.rouletteGroup.transform.Find("Hint")?.GetComponent<TMP_Text>();
                if (hint != null) hint.text = MapHint;
            }
            foreach (var front in root.GetComponentsInChildren<OnlineFrontend>(true))
            {
                // 모델 화살표 선택과 중복되는 구형 이름 드롭다운.
                if (front.skin != null)
                    front.skin.gameObject.SetActive(front.characterSelection == null || !front.characterSelection.IsConfigured);
            }
        }
    }

    static readonly System.Collections.Generic.Dictionary<Material, Material> webMaterials = new();
    public static void ApplyWebFont(TMP_Text text)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (text == null) return;
        var font = Resources.Load<TMP_FontAsset>("WebRuntime/KoreanBold");
        if (font == null || text.font == font) return;
        Material original = text.fontSharedMaterial;
        Material material = font.material;
        if (original != null)
        {
            if (!webMaterials.TryGetValue(original, out material))
            {
                material = new Material(original) { name = original.name + " Web" };
                material.mainTexture = font.material.mainTexture;
                foreach (string property in new[] { "_GradientScale", "_TextureWidth", "_TextureHeight", "_WeightNormal", "_WeightBold" })
                    if (material.HasProperty(property)) material.SetFloat(property, font.material.GetFloat(property));
                webMaterials.Add(original, material);
            }
        }
        text.font = font;
        text.fontSharedMaterial = material;
#endif
    }
}
