using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Step 7: 게임 화면 UI. 배치는 UI 시트 시안(인게임 화면, 실패 화면)을 따른다.
//  1) TextMeshPro 기본 리소스가 없으면 가져온다 (처음 한 번, 그 뒤 메뉴를 한 번 더 실행)
//  2) Windows 맑은 고딕 Bold로 TextMeshPro 글꼴(OS 글꼴을 실행 중에 읽는 방식)과 테두리·그림자 재질을 만든다
//  3) Assets/UI/Sprites 이미지를 선명하게 보이도록 설정 (압축 끔, 밉맵 켬, 패널은 9-slice)
//     이미지는 Tools/ui/slice_ui.ps1이 UI 시트에서 잘라내거나 그려서 만든다.
//  4) 씬에 GameUI 캔버스와 EventSystem을 새로 만든다 (다시 실행하면 새로 만듦, 직접 옮긴 위치는 초기화)
// 메뉴: LionIceRun > Step 7 UI 세팅
public static class Step7UISetup
{
    const string SpriteFolder = "Assets/UI/Sprites";
    const string FontFolder = "Assets/UI/Fonts";
    const string FontPath = FontFolder + "/MalgunGothicBold SDF.asset";

    static readonly Color Navy = new Color(0.106f, 0.165f, 0.361f);
    static readonly Color DeepNavy = new Color(0.055f, 0.10f, 0.24f);
    static readonly Color Brown = new Color(0.36f, 0.20f, 0.05f);
    static readonly Color Pink = new Color(0.93f, 0.30f, 0.56f);
    static readonly Color LightBlue = new Color(0.75f, 0.88f, 1f);

    static TMP_FontAsset font;
    static Material outlined;      // 흰 글자 + 두꺼운 남색 테두리 + 아래 그림자
    static Material outlinedThin;  // 작은 글자용 얇은 테두리
    static Material plain;         // 밝은 바탕 위의 남색 글자

    [MenuItem("LionIceRun/Step 7 UI 세팅")]
    static void Run()
    {
        GameManager manager = Object.FindAnyObjectByType<GameManager>();
        SurvivalTimer timer = Object.FindAnyObjectByType<SurvivalTimer>();
        IceGrid grid = Object.FindAnyObjectByType<IceGrid>();
        if (manager == null || timer == null || grid == null)
        {
            Debug.LogError("Step 7: GameManager, SurvivalTimer, IceGrid 중 없는 것이 있습니다. Step 2, 5, 6 메뉴를 먼저 실행하세요.");
            return;
        }

        if (!AssetDatabase.IsValidFolder("Assets/TextMesh Pro"))
        {
            TMP_PackageResourceImporter.ImportResources(true, false, false);
            Debug.LogWarning("Step 7: TextMeshPro 기본 리소스를 가져오는 중입니다. 가져오기가 끝나면 LionIceRun > Step 7 UI 세팅을 한 번 더 실행하세요.");
            return;
        }
        if (!ConfigureSprites())
        {
            Debug.LogError("Step 7: Assets/UI/Sprites에 이미지가 없습니다. Tools/ui/slice_ui.ps1을 먼저 실행하세요.");
            return;
        }
        if (!CreateFontAndMaterials()) return;

        GameObject old = GameObject.Find("GameUI");
        if (old != null) Object.DestroyImmediate(old);
        GameHUD hud = BuildCanvas(grid);
        EnsureEventSystem();
        timer.showDebugHud = false;
        EditorUtility.SetDirty(timer);

        EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();
        Selection.activeGameObject = hud.gameObject;
        Debug.Log("Step 7 UI 세팅 완료: 시안 배치대로 GameUI를 만들었습니다.");
    }

    // ---- 스프라이트 ----

    static bool ConfigureSprites()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { SpriteFolder });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            // 큰 원본을 작게 줄여 그려도 가장자리가 깨지지 않게
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.spriteBorder = SliceBorder(path);
            importer.SaveAndReimport();
        }
        return guids.Length > 0;
    }

    // 늘려 쓰는 배경의 9-slice 테두리 (왼, 아래, 오른, 위). 모서리 반지름보다 조금 넓게
    static Vector4 SliceBorder(string path)
    {
        if (path.Contains("/Panel_")) return new Vector4(70, 60, 70, 60);
        if (path.Contains("/Gen_BadgeWhite")) return new Vector4(72, 72, 72, 72);
        if (path.Contains("/Gen_PillNavy")) return new Vector4(60, 60, 60, 60);
        if (path.Contains("/Gen_PanelNavy") || path.Contains("/Gen_CardLight")) return new Vector4(52, 52, 52, 52);
        return Vector4.zero;
    }

    static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteFolder}/{name}.png");

    // ---- 글꼴 ----

    static bool CreateFontAndMaterials()
    {
        if (!AssetDatabase.IsValidFolder(FontFolder)) AssetDatabase.CreateFolder("Assets/UI", "Fonts");

        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null)
        {
            font = TMP_FontAsset.CreateFontAsset("Malgun Gothic", "Bold", 90);
            // 이름으로 못 찾으면 글꼴 파일 경로로 다시 시도
            if (font == null)
                font = TMP_FontAsset.CreateFontAsset("C:/Windows/Fonts/malgunbd.ttf", 0, 90, 9,
                    UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024);
            if (font == null)
            {
                Debug.LogError("Step 7: 맑은 고딕 Bold 글꼴을 찾지 못했습니다.");
                return false;
            }
            font.name = "MalgunGothicBold SDF";
            AssetDatabase.CreateAsset(font, FontPath);
            foreach (Texture2D atlas in font.atlasTextures)
            {
                atlas.name = "MalgunGothicBold Atlas";
                AssetDatabase.AddObjectToAsset(atlas, font);
            }
            font.material.name = "MalgunGothicBold Material";
            AssetDatabase.AddObjectToAsset(font.material, font);
            AssetDatabase.SaveAssets();
        }

        outlined = FontMaterial("Outlined", 0.30f, DeepNavy, true, 0.12f);
        outlinedThin = FontMaterial("OutlinedThin", 0.22f, DeepNavy, true, 0.05f);
        plain = FontMaterial("Plain", 0f, Navy, false, 0.05f);
        return true;
    }

    static Material FontMaterial(string suffix, float outlineWidth, Color outlineColor, bool underlay, float dilate)
    {
        string path = $"{FontFolder}/MalgunGothicBold {suffix}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(font.material);
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetFloat("_FaceDilate", dilate);
        material.SetFloat("_OutlineWidth", outlineWidth);
        material.SetColor("_OutlineColor", outlineColor);
        if (outlineWidth > 0f) material.EnableKeyword("OUTLINE_ON"); else material.DisableKeyword("OUTLINE_ON");
        if (underlay)
        {
            // 글자 아래로 살짝 내린 그림자 (폴가이즈 글자처럼 도톰하게)
            material.EnableKeyword("UNDERLAY_ON");
            material.SetColor("_UnderlayColor", new Color(DeepNavy.r, DeepNavy.g, DeepNavy.b, 0.85f));
            material.SetFloat("_UnderlayOffsetX", 0f);
            material.SetFloat("_UnderlayOffsetY", -0.9f);
            material.SetFloat("_UnderlayDilate", outlineWidth + 0.1f);
            material.SetFloat("_UnderlaySoftness", 0f);
        }
        else material.DisableKeyword("UNDERLAY_ON");
        EditorUtility.SetDirty(material);
        return material;
    }

    // ---- 캔버스 ----

    static GameHUD BuildCanvas(IceGrid grid)
    {
        var canvasObject = new GameObject("GameUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        GameHUD hud = canvasObject.AddComponent<GameHUD>();
        hud.grid = grid;
        Transform root = canvasObject.transform;

        BuildTop(root, hud);
        BuildBottom(root, hud);
        BuildResult(root, hud);
        return hud;
    }

    static void BuildTop(Transform root, GameHUD hud)
    {
        RectTransform top = Group(root, "HUD");

        // 왼쪽 위: 남은 얼음 (큐브 아이콘이 배지 왼쪽 밖으로 살짝 튀어나오게)
        Image ice = Img(top, "IceBadge", Sprite("Gen_PillNavy"), new Vector2(0, 1), new Vector2(0, 1), new Vector2(70, -44), new Vector2(270, 92), true);
        Img(ice.transform, "Icon", Sprite("Icon_IceCube"), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(8, 4), new Vector2(104, 106));
        hud.iceCountText = Label(ice.transform, "Count", "36 / 36", 46, Color.white, outlined,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(76, 2), new Vector2(180, 80), TextAlignmentOptions.Left);

        // 오른쪽 위: 흰 타이머 배지 + 아래 최고 기록
        Image timer = Img(top, "TimerBadge", Sprite("Gen_BadgeWhite"), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-44, -40), new Vector2(330, 104), true);
        Img(timer.transform, "Icon", Sprite("Icon_Stopwatch"), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(64, 4), new Vector2(72, 83));
        hud.timerText = Label(timer.transform, "Time", "0:00.0", 58, Navy, plain,
            new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-30, 0), new Vector2(210, 90), TextAlignmentOptions.Right);

        Image best = Img(top, "BestPill", Sprite("Gen_PillNavy"), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-44, -156), new Vector2(250, 58), true);
        hud.bestText = Label(best.transform, "Text", "최고  0:00.0", 28, Color.white, outlinedThin,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 1), new Vector2(230, 50), TextAlignmentOptions.Center);
    }

    static void BuildBottom(Transform root, GameHUD hud)
    {
        RectTransform bottom = Group(root, "Bottom");

        // 오른쪽 아래: 조작 안내 (시안처럼 반투명 남색 패널에 두 줄 표)
        Image controls = Img(bottom, "Controls", Sprite("Gen_PanelNavy"), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-44, 44), new Vector2(430, 176), true);
        hud.controlsPanel = controls.gameObject;
        string[,] rows = { { "이동", "WASD / 방향키" }, { "점프", "Space" }, { "다이브", "Ctrl  (게임패드 X)" } };
        for (int i = 0; i < rows.GetLength(0); i++)
        {
            float y = 52 - i * 46;
            Label(controls.transform, $"Key{i}", rows[i, 0], 28, LightBlue, outlinedThin,
                new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(30, y), new Vector2(110, 40), TextAlignmentOptions.Left);
            Label(controls.transform, $"Value{i}", rows[i, 1], 26, Color.white, outlinedThin,
                new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(130, y), new Vector2(290, 40), TextAlignmentOptions.Left);
        }

        // 아래 가운데: 시작 안내 (노란 버튼 모양, 통통 튐)
        Image ready = Img(bottom, "ReadyPrompt", Sprite("Panel_Yellow"), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 110), new Vector2(500, 112), true);
        Label(ready.transform, "Text", "움직이면 시작!", 50, Brown, plain,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 6), new Vector2(460, 90), TextAlignmentOptions.Center);
        hud.readyPrompt = ready.rectTransform;
    }

    static void BuildResult(Transform root, GameHUD hud)
    {
        RectTransform result = Group(root, "Result");
        hud.resultGroup = result.gameObject;

        Image dim = Img(result, "Dim", null, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, false);
        Stretch(dim.rectTransform);
        dim.color = new Color(0.02f, 0.05f, 0.15f, 0.5f);
        dim.raycastTarget = true;  // 뒤쪽 클릭 막기

        Image card = Img(result, "Card", Sprite("Gen_PanelNavy"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -30), new Vector2(760, 520), true);
        card.color = new Color(1f, 1f, 1f, 1f);
        hud.resultCard = card.rectTransform;
        Transform c = card.transform;

        // 카드 윗변에 걸친 배너 / 새 기록 배지
        hud.gameOverBanner = Img(c, "GameOverBanner", Sprite("Banner_GameOver"), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(560, 178)).gameObject;
        Image badge = Img(c, "NewRecordBadge", Sprite("Panel_Pink"), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(480, 118), true);
        Img(badge.transform, "Check", Sprite("Icon_Check"), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(70, 4), new Vector2(80, 82));
        Label(badge.transform, "Text", "새 기록!", 62, Color.white, outlined,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(34, 6), new Vector2(320, 100), TextAlignmentOptions.Center);
        hud.newRecordBadge = badge.gameObject;

        Label(c, "Subtitle", "바다로 떨어졌어요.", 32, Color.white, outlinedThin,
            new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -104), new Vector2(600, 50), TextAlignmentOptions.Center);

        // 기록 상자 (시안의 흰 정보 상자)
        Image stats = Img(c, "Stats", Sprite("Gen_CardLight"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 16), new Vector2(620, 180), true);
        Label(stats.transform, "TimeLabel", "버틴 시간", 34, Navy, plain,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(40, 38), new Vector2(240, 60), TextAlignmentOptions.Left);
        hud.resultTimeText = Label(stats.transform, "TimeValue", "0:00.0", 60, Navy, plain,
            new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-40, 38), new Vector2(300, 80), TextAlignmentOptions.Right);
        Label(stats.transform, "BestLabel", "최고 기록", 30, Navy, plain,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(40, -42), new Vector2(240, 56), TextAlignmentOptions.Left);
        hud.resultBestText = Label(stats.transform, "BestValue", "0:00.0", 44, Pink, plain,
            new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-40, -42), new Vector2(300, 64), TextAlignmentOptions.Right);

        // 다시 하기 버튼 (시트의 노란 버튼 + 실제 글자)
        Image retry = Img(c, "RetryButton", Sprite("Btn_YellowRetry"), new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 96), new Vector2(340, 133));
        retry.raycastTarget = true;
        Label(retry.transform, "Text", "다시 하기", 44, Brown, plain,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(34, 4), new Vector2(230, 80), TextAlignmentOptions.Center);
        var button = retry.gameObject.AddComponent<Button>();
        button.targetGraphic = retry;
        UnityEventTools.AddPersistentListener(button.onClick, hud.OnRetryClicked);
        retry.gameObject.AddComponent<UIButtonPop>();

        Label(c, "RetryHint", "R 키로도 다시 할 수 있어요", 24, LightBlue, outlinedThin,
            new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 16), new Vector2(600, 36), TextAlignmentOptions.Center);

        result.gameObject.SetActive(false);
        badge.gameObject.SetActive(false);
    }

    static void EnsureEventSystem()
    {
        EventSystem existing = Object.FindAnyObjectByType<EventSystem>();
        if (existing != null)
        {
            // 새 Input System 전용 프로젝트라 기존 입력 모듈은 쓸 수 없다
            var legacy = existing.GetComponent<StandaloneInputModule>();
            if (legacy != null) Object.DestroyImmediate(legacy);
            if (existing.GetComponent<InputSystemUIInputModule>() == null) existing.gameObject.AddComponent<InputSystemUIInputModule>();
            return;
        }
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    // ---- 만들기 도우미 ----

    static RectTransform Group(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        Stretch(rect);
        return rect;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // anchor: 부모의 어느 점에 붙일지, pivot: 내 어느 점을 그 자리에 둘지
    static RectTransform Place(GameObject go, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    static Image Img(Transform parent, string name, Sprite sprite, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, bool sliced = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        Place(go, parent, anchor, pivot, position, size);
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        image.preserveAspect = !sliced && sprite != null;
        image.raycastTarget = false;
        return image;
    }

    static TMP_Text Label(Transform parent, string name, string content, float size, Color color, Material material,
        Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 box, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        Place(go, parent, anchor, pivot, position, box);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSharedMaterial = material;
        text.text = content;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        return text;
    }
}
