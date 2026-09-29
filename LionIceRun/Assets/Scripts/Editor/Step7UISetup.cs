using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Step 7: 게임 화면 UI. 구성은 Fall Guys UI(gameuidatabase.com/gameData.php?id=1499)를 따른다.
//  1) TextMeshPro 기본 리소스가 없으면 가져온다 (처음 한 번, 그 뒤 메뉴를 한 번 더 실행)
//  2) Windows 맑은 고딕 Bold로 TextMeshPro 글꼴(OS 글꼴을 실행 중에 읽는 방식)과 테두리·그림자 재질을 만든다
//  3) Assets/UI/Sprites 이미지를 선명하게 보이도록 설정 (압축 끔, 밉맵 켬, 늘리는 배경은 9-slice)
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
    static readonly Color Pink = new Color(0.96f, 0.18f, 0.55f);
    static readonly Color LightBlue = new Color(0.72f, 0.86f, 1f);
    static readonly Color Yellow = new Color(1f, 0.86f, 0.25f);

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
        Debug.Log("Step 7 UI 세팅 완료: Fall Guys식 구성으로 GameUI를 만들었습니다.");
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
        if (path.Contains("/Gen_PillWhite") || path.Contains("/Gen_BadgePink") || path.Contains("/Gen_BoxMagenta")) return new Vector4(48, 48, 48, 48);
        if (path.Contains("/Gen_ChipDark")) return new Vector4(38, 38, 38, 38);
        if (path.Contains("/Gen_KeyCap")) return new Vector4(26, 26, 26, 26);
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
        BuildBanner(root, hud);
        BuildResult(root, hud);
        return hud;
    }

    static void BuildTop(Transform root, GameHUD hud)
    {
        RectTransform top = Group(root, "HUD");

        // 왼쪽 위: 화면 끝에 붙은 분홍 목표 띠 (왼쪽 끝은 화면 밖으로)
        Image goal = Img(top, "Goal", Sprite("Gen_SkewPink"), new Vector2(0, 1), new Vector2(0, 1), new Vector2(-60, -34), new Vector2(620, 108));
        Label(goal.transform, "Text", "최대한 오래 버티세요!", 40, Color.white, outlined,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(104, 2), new Vector2(480, 80), TextAlignmentOptions.Left, true);

        // 위 가운데: 작은 제목 + 기울어진 분홍 배지 (Fall Guys의 LAP 1/2처럼)
        Label(top, "IceLabel", "남은 얼음", 30, Color.white, outlinedThin,
            new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -26), new Vector2(300, 44), TextAlignmentOptions.Center, true);
        Image badge = Img(top, "IceBadge", Sprite("Gen_BadgePink"), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -122), new Vector2(236, 104), true);
        badge.rectTransform.localRotation = Quaternion.Euler(0, 0, 3f);
        Img(badge.transform, "Icon", Sprite("Icon_IceCube"), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 6), new Vector2(80, 82));
        hud.iceCountText = Label(badge.transform, "Count", "36 / 36", 50, Color.white, outlined,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(18, 4), new Vector2(190, 80), TextAlignmentOptions.Center, true);
        hud.iceBadge = badge.rectTransform;

        // 오른쪽 위: 화면 끝에 붙은 남색 띠 (작은 제목 위, 큰 숫자 아래)
        Image timer = Img(top, "Timer", Sprite("Gen_SkewNavy"), new Vector2(1, 1), new Vector2(1, 1), new Vector2(60, -34), new Vector2(500, 138));
        Img(timer.transform, "Icon", Sprite("Icon_Stopwatch"), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(110, 2), new Vector2(70, 80));
        Label(timer.transform, "Label", "버틴 시간", 26, LightBlue, outlinedThin,
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(-104, -12), new Vector2(260, 38), TextAlignmentOptions.Right, true);
        hud.timerText = Label(timer.transform, "Time", "0:00.0", 64, Color.white, outlined,
            new Vector2(1, 0), new Vector2(1, 0), new Vector2(-100, 10), new Vector2(300, 84), TextAlignmentOptions.Right, true);

        Image best = Img(top, "Best", Sprite("Gen_SkewNavy"), new Vector2(1, 1), new Vector2(1, 1), new Vector2(60, -178), new Vector2(330, 64));
        hud.bestText = Label(best.transform, "Text", "최고  0:00.0", 28, Color.white, outlinedThin,
            new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-96, 0), new Vector2(240, 44), TextAlignmentOptions.Right, true);
    }

    static void BuildBottom(Transform root, GameHUD hud)
    {
        RectTransform bottom = Group(root, "Bottom");

        // 시작 전 왼쪽 아래: 초록 제목 띠 + 반투명 설명 (Fall Guys 스테이지 소개처럼)
        var intro = new GameObject("Intro", typeof(RectTransform));
        RectTransform introRect = Place(intro, bottom, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 150), new Vector2(1400, 130));
        hud.introGroup = introRect;
        Image title = Img(introRect, "Title", Sprite("Gen_SkewGreen"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(-60, 0), new Vector2(560, 124));
        Label(title.transform, "Text", "얼음 깨기", 60, Color.white, outlined,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(110, 4), new Vector2(400, 90), TextAlignmentOptions.Left, true);
        Image desc = Img(introRect, "Description", Sprite("Gen_ChipDark"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(470, 0), new Vector2(840, 110), true);
        Label(desc.transform, "Line1", "밟은 얼음은 세 번이면 깨져요. 끝까지 버티세요!", 26, Color.white, outlinedThin,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(40, 18), new Vector2(700, 40), TextAlignmentOptions.Left, false);
        Label(desc.transform, "Line2", "움직이면 시작!", 30, Yellow, outlinedThin,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(40, -22), new Vector2(700, 44), TextAlignmentOptions.Left, true);
        Img(desc.transform, "Icon", Sprite("Icon_IceCube"), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-56, 2), new Vector2(76, 78));

        // 오른쪽 아래: 키 칩 (오른쪽부터 채움)
        var controls = new GameObject("Controls", typeof(RectTransform));
        RectTransform controlsRect = Place(controls, bottom, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 40), new Vector2(900, 64));
        hud.controlsGroup = controls;
        float x = 0f;
        foreach (var (key, label) in new[] { ("Ctrl", "다이브"), ("Space", "점프"), ("WASD", "이동") })
        {
            RectTransform chip = Chip(controlsRect, key, label, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-x, 0));
            x += chip.sizeDelta.x + 14f;
        }
    }

    // 화면을 가로지르는 사건 알림 띠 ("시작!", "탈락!"). 양 끝에 알약 장식
    static void BuildBanner(Transform root, GameHUD hud)
    {
        var bannerObject = new GameObject("Banner", typeof(RectTransform));
        RectTransform banner = Place(bannerObject, root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(2600, 200));
        banner.localRotation = Quaternion.Euler(0, 0, 4f);

        (Vector2 pos, Vector2 size, Color color)[] pills =
        {
            (new Vector2(-760, 124), new Vector2(620, 56), Pink),
            (new Vector2(-900, -120), new Vector2(520, 46), DeepNavy),
            (new Vector2(760, -124), new Vector2(620, 56), Pink),
            (new Vector2(900, 120), new Vector2(520, 46), DeepNavy),
        };
        foreach (var (pos, size, color) in pills)
        {
            Image pill = Img(banner, "Pill", Sprite("Gen_PillWhite"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size, true);
            pill.color = color;
        }
        Image band = Img(banner, "Band", Sprite("Gen_Band"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2600, 200));
        band.preserveAspect = false;
        hud.bannerBand = band;
        hud.bannerText = Label(banner, "Text", "시작!", 150, Color.white, outlined,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 8), new Vector2(1200, 190), TextAlignmentOptions.Center, true);
        hud.banner = banner;
        bannerObject.SetActive(false);
    }

    static void BuildResult(Transform root, GameHUD hud)
    {
        RectTransform result = Group(root, "Result");
        hud.resultGroup = result.gameObject;

        Image dim = Img(result, "Dim", null, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Stretch(dim.rectTransform);
        dim.color = new Color(0.02f, 0.05f, 0.15f, 0.45f);
        dim.raycastTarget = true;  // 뒤쪽 클릭 막기

        Image card = Img(result, "Card", Sprite("Gen_PanelNavy"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(720, 470), true);
        hud.resultCard = card.rectTransform;
        Transform c = card.transform;

        // 카드 윗변에 걸친 자홍 제목 상자 (Fall Guys의 "57 REMAIN!"처럼)
        Image header = Img(c, "Header", Sprite("Gen_BoxMagenta"), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(660, 132), true);
        header.rectTransform.localRotation = Quaternion.Euler(0, 0, -2f);
        hud.resultTimeText = Label(header.transform, "Text", "0:00.0 버텼어요!", 60, Color.white, outlined,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 6), new Vector2(620, 100), TextAlignmentOptions.Center, true);

        // 새 기록이면 금색 띠
        Image ribbon = Img(c, "NewRecord", Sprite("Gen_SkewGold"), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -122), new Vector2(430, 86));
        Label(ribbon.transform, "Text", "새 기록!", 50, Color.white, outlined,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 4), new Vector2(380, 76), TextAlignmentOptions.Center, true);
        hud.newRecordRibbon = ribbon.gameObject;

        Label(c, "BestLabel", "최고 기록", 32, LightBlue, outlinedThin,
            new Vector2(0.5f, 0.5f), new Vector2(1, 0.5f), new Vector2(-20, 6), new Vector2(260, 50), TextAlignmentOptions.Right, true);
        hud.resultBestText = Label(c, "BestValue", "0:00.0", 52, Color.white, outlined,
            new Vector2(0.5f, 0.5f), new Vector2(0, 0.5f), new Vector2(10, 6), new Vector2(280, 70), TextAlignmentOptions.Left, true);

        // 다시 하기 버튼 + R 키 칩
        Image retry = Img(c, "RetryButton", Sprite("Btn_YellowRetry"), new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(340, 133));
        retry.raycastTarget = true;
        Label(retry.transform, "Text", "다시 하기", 44, Brown, plain,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(34, 4), new Vector2(230, 80), TextAlignmentOptions.Center, false);
        var button = retry.gameObject.AddComponent<Button>();
        button.targetGraphic = retry;
        UnityEventTools.AddPersistentListener(button.onClick, hud.OnRetryClicked);
        retry.gameObject.AddComponent<UIButtonPop>();
        Chip(c, "R", "다시 하기", new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 50));

        result.gameObject.SetActive(false);
        ribbon.gameObject.SetActive(false);
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

    // Fall Guys식 키 안내 칩: 반투명 알약 안에 [키 모양] + 설명. 글자 폭에 맞춰 너비를 정한다
    static RectTransform Chip(Transform parent, string key, string label, Vector2 anchor, Vector2 pivot, Vector2 position)
    {
        const float height = 60f, pad = 14f, gap = 10f;
        Image chip = Img(parent, $"Chip_{key}", Sprite("Gen_ChipDark"), anchor, pivot, position, new Vector2(100, height), true);

        TMP_Text keyText = Label(chip.transform, "KeyText", key, 24, Navy, plain,
            new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100, 40), TextAlignmentOptions.Center, false);
        // 글자 폭을 못 재면(글꼴이 아직 안 읽힘) 글자 수로 어림
        float keyMeasured = keyText.GetPreferredValues(key).x;
        if (keyMeasured < 1f) keyMeasured = key.Length * 15f;
        float keyWidth = Mathf.Max(44f, keyMeasured + 24f);
        Image cap = Img(chip.transform, "Key", Sprite("Gen_KeyCap"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(pad, 0), new Vector2(keyWidth, 44), true);
        keyText.transform.SetParent(cap.transform, false);
        Place(keyText.gameObject, cap.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 1), new Vector2(keyWidth, 40));

        TMP_Text labelText = Label(chip.transform, "Label", label, 26, Color.white, outlinedThin,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(pad + keyWidth + gap, 0), new Vector2(200, 40), TextAlignmentOptions.Left, false);
        float labelMeasured = labelText.GetPreferredValues(label).x;
        if (labelMeasured < 1f) labelMeasured = label.Length * 27f;
        float labelWidth = labelMeasured + 4f;
        labelText.rectTransform.sizeDelta = new Vector2(labelWidth, 40);

        chip.rectTransform.sizeDelta = new Vector2(pad + keyWidth + gap + labelWidth + pad + 6f, height);
        return chip.rectTransform;
    }

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

    // italic: Fall Guys 글자처럼 살짝 기울임
    static TMP_Text Label(Transform parent, string name, string content, float size, Color color, Material material,
        Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 box, TextAlignmentOptions alignment, bool italic)
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
        text.fontStyle = italic ? FontStyles.Italic : FontStyles.Normal;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        return text;
    }
}
