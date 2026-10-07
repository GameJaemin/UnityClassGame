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
//  4) 씬에 GameUI 캔버스와 EventSystem을 새로 만든다
//     다시 실행해도 에디터에서 직접 바꾼 위치·크기·회전·글자 크기·정렬은 같은 이름의 요소에 그대로 되돌려 놓는다
//     (처음 배치로 되돌리려면 "Step 7 UI 세팅 (배치 초기화)")
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
    static void Run() => Build(keepLayout: true);

    [MenuItem("LionIceRun/맵 선택 화면 세팅")]
    static void SetupMapSelection()
    {
        GameManager manager = Object.FindAnyObjectByType<GameManager>();
        GameHUD hud = Object.FindAnyObjectByType<GameHUD>(FindObjectsInactive.Include);
        if (manager == null || manager.grid == null || manager.player == null || hud == null)
        {
            Debug.LogError("맵 선택: GameManager·플레이어·GameUI가 필요합니다. 기존 세팅과 Step 7 UI 세팅을 먼저 실행하세요.");
            return;
        }
        if (!CreateFontAndMaterials()) return;
        CaptureMapScreenshots();
        GameObject old = GameObject.Find("MapSelectionUI");
        if (old != null) Object.DestroyImmediate(old);
        var go = new GameObject("MapSelectionUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        MapRoulette roulette = manager.GetComponent<MapRoulette>();
        if (roulette == null) roulette = manager.gameObject.AddComponent<MapRoulette>();
        roulette.manager = manager;
        roulette.selectionCanvas = canvas;
        roulette.gameCanvas = hud.GetComponent<Canvas>();
        roulette.scrollSpeed = 2300f;
        roulette.enabled = true;
        RepairTimerPanel(hud);
        Image background = Img(go.transform, "Background", null, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, Vector2.zero);
        Stretch(background.rectTransform);
        background.color = new Color(DeepNavy.r, DeepNavy.g, DeepNavy.b, 0.16f);
        background.raycastTarget = true;
        RectTransform titleScreen = Group(go.transform, "TitleScreen");
        roulette.titleGroup = titleScreen.gameObject;
        roulette.titleLogo = Img(titleScreen, "Logo", Sprite("Logo_Title"), Vector2.one * 0.5f, Vector2.one * 0.5f, new Vector2(0, 140), new Vector2(760, 480)).rectTransform;
        Label(titleScreen, "Tagline", "얼음을 깨고, 밀치고, 최후의 1인이 되세요!", 34, LightBlue, outlinedThin,
            Vector2.one * 0.5f, Vector2.one * 0.5f, new Vector2(0, -125), new Vector2(1200, 60), TextAlignmentOptions.Center, false);
        Image start = Img(titleScreen, "StartButton", Sprite("Btn_YellowPlay"), Vector2.one * 0.5f, Vector2.one * 0.5f, new Vector2(0, -260), new Vector2(440, 145));
        start.rectTransform.localRotation = Quaternion.Euler(0, 0, -2f);
        start.raycastTarget = true;
        roulette.startButton = start.gameObject.AddComponent<Button>();
        roulette.startButton.targetGraphic = start;
        start.gameObject.AddComponent<UIButtonPop>();
        Label(start.transform, "Text", "시작하기", 44, Brown, plain,
            Vector2.one * 0.5f, Vector2.one * 0.5f, new Vector2(35, 5), new Vector2(290, 80), TextAlignmentOptions.Center, true);
        RectTransform selectionScreen = Group(go.transform, "MapSelectionScreen");
        roulette.rouletteGroup = selectionScreen.gameObject;
        Image selectionBack = Img(selectionScreen, "Background", null, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, Vector2.zero);
        Stretch(selectionBack.rectTransform);
        selectionBack.color = DeepNavy;
        Label(selectionScreen, "Title", "맵을 골라요!", 64, Color.white, outlined,
            new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -130), new Vector2(1100, 90), TextAlignmentOptions.Center, true);
        roulette.status = Label(selectionScreen, "Status", "어떤 맵에서 겨룰까요?", 40, Yellow, outlinedThin,
            new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -240), new Vector2(1400, 140), TextAlignmentOptions.Center, false);
        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        RectTransform view = Place(viewport, selectionScreen, Vector2.one * 0.5f, Vector2.one * 0.5f, new Vector2(0, -15), new Vector2(1760, 360));
        roulette.cards = new RectTransform[7];
        roulette.previews = new Image[7];
        roulette.names = new TMP_Text[7];
        string[] files = MapData.ListFiles();
        for (int i = 0; i < 7; i++)
        {
            Image frame = Img(view, $"Card{i + 1}", null, Vector2.one * 0.5f, Vector2.one * 0.5f, new Vector2((i - 3) * 440, 0), new Vector2(420, 310));
            frame.color = i == 3 ? Yellow : Color.white;
            roulette.cards[i] = frame.rectTransform;
            string file = files.Length > 0 ? files[i % files.Length] : "";
            Sprite screenshot = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/MapPreviews/" + System.IO.Path.GetFileNameWithoutExtension(file) + ".png");
            roulette.previews[i] = Img(frame.transform, "Screenshot", screenshot, Vector2.one * 0.5f, Vector2.one * 0.5f, new Vector2(0, 25), new Vector2(396, 224));
            MapData map = file.Length > 0 ? MapData.Load(file) : null;
            roulette.names[i] = Label(frame.transform, "Name", map != null ? map.name : "저장된 맵 없음", 28, Navy, plain,
                new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 12), new Vector2(388, 48), TextAlignmentOptions.Center, false);
            roulette.names[i].overflowMode = TextOverflowModes.Ellipsis;
        }
        Label(selectionScreen, "Marker", "▼", 48, Yellow, outlinedThin,
            Vector2.one * 0.5f, Vector2.one * 0.5f, new Vector2(0, 190), new Vector2(100, 60), TextAlignmentOptions.Center, false);
        roulette.stopButton = null;
        roulette.buttonText = null;
        Label(selectionScreen, "Hint", UIConsistency.MapHint, 28, LightBlue, outlinedThin,
            new Vector2(0.5f, 0), Vector2.one * 0.5f, new Vector2(0, 100), new Vector2(1300, 50), TextAlignmentOptions.Center, false);
        EnsureEventSystem();
        selectionScreen.gameObject.SetActive(false);
        BuildTitleDecorations(roulette, manager);
        EditorUtility.SetDirty(roulette);
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();
        Selection.activeGameObject = go;
        Debug.Log("타이틀·맵 선택 화면 세팅 완료. 시작하기 → 빠른 맵 카드 이동 → 감속 선택 → 준비 3초 → 경기로 진행합니다.");
    }

    [MenuItem("LionIceRun/맵 선택 화면 세팅", true)]
    [MenuItem("LionIceRun/맵 미리보기 스크린샷 다시 찍기", true)]
    static bool CanSetupMapSelection() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // 기존 바다와 모델을 사용해 타이틀용 얼음 섬을 카메라 양옆에 배치한다.
    static void BuildTitleDecorations(MapRoulette roulette, GameManager manager)
    {
        GameObject old = GameObject.Find("TitleDecorations");
        if (old != null) Object.DestroyImmediate(old);
        Camera camera = Camera.main;
        if (camera == null) return;
        QuarterViewCamera view = camera.GetComponent<QuarterViewCamera>();
        if (view != null) view.Fit();
        var root = new GameObject("TitleDecorations");
        roulette.titleDecorations = root;
        PlayerSkin skin = manager.player.GetComponent<PlayerSkin>();
        GameObject penguin = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Hazards/Penguin.fbx");
        Vector2[] points = { new Vector2(0.19f, 0.43f), new Vector2(0.81f, 0.44f), new Vector2(0.12f, 0.18f), new Vector2(0.9f, 0.2f) };
        Plane water = new Plane(Vector3.up, new Vector3(0, manager.grid.SeaLevel + 0.35f, 0));
        for (int i = 0; i < points.Length; i++)
        {
            Ray ray = camera.ViewportPointToRay(new Vector3(points[i].x, points[i].y, 0));
            if (!water.Raycast(ray, out float distance)) continue;
            Vector3 center = ray.GetPoint(distance);
            var island = new GameObject($"TitleIsland{i + 1}");
            island.transform.SetParent(root.transform, false);
            island.transform.position = center;
            for (int x = -1; x <= 1; x++)
            for (int z = -1; z <= 1; z++)
            {
                if (Mathf.Abs(x) + Mathf.Abs(z) == 2) continue;
                GameObject ice = Object.Instantiate(manager.grid.blockPrefab, island.transform);
                ice.transform.localPosition = new Vector3(x * 1.6f, -0.3f, z * 1.6f);
                ice.transform.localScale = new Vector3(1.55f, 0.6f, 1.55f);
                foreach (Collider collider in ice.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
            }
            GameObject model = i < 3 && skin != null && skin.models != null && i < skin.models.Length ? skin.models[i] : penguin;
            if (model == null) continue;
            GameObject animal = Object.Instantiate(model, island.transform);
            animal.name = i < 3 ? new[] { "Lion", "PolarBear", "Tiger" }[i] : "Penguin";
            animal.transform.localPosition = Vector3.zero;
            animal.transform.localScale = Vector3.one * (i == 3 ? 2.8f : 2.1f);
            animal.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(camera.transform.position - center, Vector3.up));
            if (i < 3 && skin != null)
            {
                Animator animator = animal.GetComponent<Animator>();
                if (animator == null) animator = animal.AddComponent<Animator>();
                animator.runtimeAnimatorController = skin.animatorController;
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                animator.applyRootMotion = false;
            }
            foreach (Collider collider in animal.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
        }
    }

    // 시간 패널의 부모 위치는 유지하고 아이콘·제목·숫자 영역을 분리한다.
    static void RepairTimerPanel(GameHUD hud)
    {
        Transform timer = hud.transform.Find("HUD/Timer");
        if (timer == null) return;
        TMP_Text label = timer.Find("Label")?.GetComponent<TMP_Text>();
        TMP_Text time = timer.Find("Time")?.GetComponent<TMP_Text>();
        if (label != null) Place(label.gameObject, timer, Vector2.one, Vector2.one, new Vector2(-100, -10), new Vector2(260, 32));
        if (time != null)
        {
            Place(time.gameObject, timer, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-100, 12), new Vector2(240, 72));
            time.fontSize = 56;
        }
    }

    [MenuItem("LionIceRun/맵 미리보기 스크린샷 다시 찍기")]
    static void CaptureMapScreenshots()
    {
        IceGrid grid = Object.FindAnyObjectByType<IceGrid>();
        if (grid == null || grid.blockPrefab == null) { Debug.LogError("맵 촬영: IceGrid와 블럭 프리팹이 필요합니다."); return; }
        string folder = "Assets/Resources/MapPreviews";
        System.IO.Directory.CreateDirectory(folder);
        foreach (string file in MapData.ListFiles())
        {
            MapData map = MapData.Load(file);
            if (map == null || map.IceCount == 0) continue;
            var preview = new PreviewRenderUtility();
            Texture2D image = null;
            try
            {
                var root = new GameObject("MapScreenshot");
                preview.AddSingleGO(root);
                for (int x = 0; x < map.width; x++)
                for (int z = 0; z < map.depth; z++)
                {
                    if (!map.IsIce(new Vector2Int(x, z))) continue;
                    GameObject block = Object.Instantiate(grid.blockPrefab, root.transform);
                    block.transform.localPosition = new Vector3((x - (map.width - 1) * 0.5f) * grid.Spacing, -grid.blockHeight * 0.5f, (z - (map.depth - 1) * 0.5f) * grid.Spacing);
                    block.transform.localScale = new Vector3(grid.blockSize, grid.blockHeight, grid.blockSize);
                }
                preview.camera.backgroundColor = new Color(0.12f, 0.34f, 0.5f);
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.camera.orthographic = true;
                preview.camera.orthographicSize = new Vector2(map.width * grid.Spacing, map.depth * grid.Spacing).magnitude * 0.48f + 1f;
                Quaternion rotation = Quaternion.Euler(55f, 45f, 0f);
                preview.camera.transform.SetPositionAndRotation(-(rotation * Vector3.forward) * 60f, rotation);
                preview.camera.nearClipPlane = 0.1f;
                preview.camera.farClipPlane = 150f;
                preview.lights[0].intensity = 1.3f;
                preview.lights[0].transform.rotation = Quaternion.Euler(50, -30, 0);
                preview.lights[1].intensity = 0.6f;
                preview.ambientColor = new Color(0.6f, 0.7f, 0.8f);
                preview.BeginStaticPreview(new Rect(0, 0, 960, 540));
                preview.Render(true);
                image = preview.EndStaticPreview();
                string path = folder + "/" + System.IO.Path.GetFileNameWithoutExtension(file) + ".png";
                System.IO.File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                if (image != null) Object.DestroyImmediate(image);
                preview.Cleanup();
            }
        }
        AssetDatabase.Refresh();
        foreach (string file in MapData.ListFiles())
        {
            string path = folder + "/" + System.IO.Path.GetFileNameWithoutExtension(file) + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        Debug.Log("맵 미리보기 스크린샷을 Assets/Resources/MapPreviews에 저장했습니다.");
    }

    [MenuItem("LionIceRun/Step 7 조작 안내 겹침 수정")]
    static void RepairControlsInScene()
    {
        GameHUD hud = Object.FindAnyObjectByType<GameHUD>(FindObjectsInactive.Include);
        if (hud == null || hud.controlsGroup == null)
        {
            Debug.LogError("조작 안내: GameUI가 없습니다. Step 7 UI 세팅을 먼저 실행하세요.");
            return;
        }
        Undo.RegisterFullObjectHierarchyUndo(hud.controlsGroup, "조작 안내 겹침 수정");
        Canvas.ForceUpdateCanvases();
        bool changed = RepairControlChipOverlap(hud.controlsGroup);
        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
            EditorSceneManager.SaveScene(hud.gameObject.scene);
        }
        SceneView.RepaintAll();
        Selection.activeGameObject = hud.controlsGroup;
        Debug.Log(changed ? "조작 안내 겹침 수정 완료: 씬에 저장했습니다. Scene 뷰에서 확인하세요." : "조작 안내끼리 겹치는 부분이 없습니다.");
    }

    [MenuItem("LionIceRun/Step 7 조작 안내 겹침 수정", true)]
    static bool CanRepairControlsInScene() => !EditorApplication.isPlayingOrWillChangePlaymode;

    [MenuItem("LionIceRun/Step 7 UI 세팅 (배치 초기화)")]
    static void RunReset()
    {
        if (EditorUtility.DisplayDialog("UI 배치 초기화", "GameUI에서 직접 바꾼 위치·크기·글자 크기를 모두 처음 배치로 되돌립니다.", "초기화", "취소"))
            Build(keepLayout: false);
    }

    // 새 키 칩과 보존된 기존 키 칩의 위치가 겹칠 때만 오른쪽부터 다시 정렬한다.
    // 에디터에서 씬 배치를 저장해 Scene 뷰와 게임에서 같은 위치를 사용한다.
    static bool RepairControlChipOverlap(GameObject controlsGroup)
    {
        if (controlsGroup == null) return false;
        var chips = new System.Collections.Generic.List<RectTransform>();
        foreach (Transform child in controlsGroup.transform)
            if (child is RectTransform rect && child.name.StartsWith("Chip_")) chips.Add(rect);
        bool overlaps = false;
        for (int i = 0; i < chips.Count; i++)
        for (int j = i + 1; j < chips.Count; j++)
        {
            Bounds a = RectTransformUtility.CalculateRelativeRectTransformBounds(controlsGroup.transform, chips[i]);
            Bounds b = RectTransformUtility.CalculateRelativeRectTransformBounds(controlsGroup.transform, chips[j]);
            if (a.min.x < b.max.x && a.max.x > b.min.x && a.min.y < b.max.y && a.max.y > b.min.y) overlaps = true;
        }
        if (!overlaps) return false;
        float x = 0f;
        foreach (RectTransform chip in chips)
        {
            chip.anchorMin = chip.anchorMax = new Vector2(1f, 0.5f);
            chip.pivot = new Vector2(1f, 0.5f);
            chip.anchoredPosition = new Vector2(-x, 0f);
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(controlsGroup.transform, chip);
            // 회전·크기 변경이 있어도 실제 왼쪽/오른쪽 끝 사이에 간격을 둔다.
            float right = ((RectTransform)controlsGroup.transform).rect.xMax;
            chip.anchoredPosition += Vector2.left * (bounds.max.x - (right - x));
            x += bounds.size.x + 14f;
        }
        return true;
    }

    static void Build(bool keepLayout)
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
        LayoutSnapshot snapshot = old != null && keepLayout ? LayoutSnapshot.Capture(old.transform) : null;
        if (old != null) Object.DestroyImmediate(old);
        GameHUD hud = BuildCanvas(grid);
        int restored = snapshot != null ? snapshot.Apply(hud.transform) : 0;
        Canvas.ForceUpdateCanvases();
        RepairControlChipOverlap(hud.controlsGroup);
        RepairTimerPanel(hud);
        MapRoulette roulette = manager.GetComponent<MapRoulette>();
        if (roulette != null)
        {
            roulette.gameCanvas = hud.GetComponent<Canvas>();
            EditorUtility.SetDirty(roulette);
        }
        EnsureEventSystem();
        timer.showDebugHud = false;
        EditorUtility.SetDirty(timer);

        EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();
        Selection.activeGameObject = hud.gameObject;
        Debug.Log(keepLayout && snapshot != null
            ? $"Step 7 UI 세팅 완료: GameUI를 다시 만들고, 직접 바꾼 배치를 요소 {restored}개에 되돌려 놓았습니다."
            : "Step 7 UI 세팅 완료: 처음 배치로 GameUI를 만들었습니다.");
    }

    // 에디터에서 직접 바꾼 UI 배치를 요소 이름 경로로 기록했다가 새로 만든 UI에 되돌려 놓는다.
    // 같은 부모 아래 같은 이름이 여럿이면 "#순번"을 붙여 구분한다 (예: Banner/Pill#2)
    class LayoutSnapshot
    {
        struct RectState
        {
            public Vector2 anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta;
            public Quaternion rotation;
            public Vector3 scale;
        }

        struct TextState
        {
            public float fontSize;
            public FontStyles fontStyle;
            public TextAlignmentOptions alignment;
            public float characterSpacing;
            public bool autoSize;
            public float autoSizeMin, autoSizeMax;
        }

        readonly System.Collections.Generic.Dictionary<string, RectState> rects = new System.Collections.Generic.Dictionary<string, RectState>();
        readonly System.Collections.Generic.Dictionary<string, TextState> texts = new System.Collections.Generic.Dictionary<string, TextState>();

        public static LayoutSnapshot Capture(Transform root)
        {
            var snapshot = new LayoutSnapshot();
            snapshot.Walk(root, "", (path, t) =>
            {
                if (t is RectTransform r)
                    snapshot.rects[path] = new RectState
                    {
                        anchorMin = r.anchorMin, anchorMax = r.anchorMax, pivot = r.pivot,
                        anchoredPosition = r.anchoredPosition, sizeDelta = r.sizeDelta,
                        rotation = r.localRotation, scale = r.localScale,
                    };
                TMP_Text text = t.GetComponent<TMP_Text>();
                if (text != null)
                    snapshot.texts[path] = new TextState
                    {
                        fontSize = text.fontSize, fontStyle = text.fontStyle, alignment = text.alignment,
                        characterSpacing = text.characterSpacing, autoSize = text.enableAutoSizing,
                        autoSizeMin = text.fontSizeMin, autoSizeMax = text.fontSizeMax,
                    };
            });
            return snapshot;
        }

        public int Apply(Transform root)
        {
            int count = 0;
            Walk(root, "", (path, t) =>
            {
                bool changed = false;
                if (t is RectTransform r && rects.TryGetValue(path, out RectState s))
                {
                    r.anchorMin = s.anchorMin; r.anchorMax = s.anchorMax; r.pivot = s.pivot;
                    r.anchoredPosition = s.anchoredPosition; r.sizeDelta = s.sizeDelta;
                    r.localRotation = s.rotation; r.localScale = s.scale;
                    changed = true;
                }
                TMP_Text text = t.GetComponent<TMP_Text>();
                if (text != null && texts.TryGetValue(path, out TextState ts))
                {
                    text.fontSize = ts.fontSize; text.fontStyle = ts.fontStyle; text.alignment = ts.alignment;
                    text.characterSpacing = ts.characterSpacing; text.enableAutoSizing = ts.autoSize;
                    text.fontSizeMin = ts.autoSizeMin; text.fontSizeMax = ts.autoSizeMax;
                    changed = true;
                }
                if (changed) count++;
            });
            return count;
        }

        void Walk(Transform node, string path, System.Action<string, Transform> visit)
        {
            visit(path, node);
            var seen = new System.Collections.Generic.Dictionary<string, int>();
            foreach (Transform child in node)
            {
                seen.TryGetValue(child.name, out int index);
                seen[child.name] = index + 1;
                string childPath = (path.Length > 0 ? path + "/" : "") + child.name + (index > 0 ? $"#{index}" : "");
                Walk(child, childPath, visit);
            }
        }
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
        Label(goal.transform, "Text", "최후의 1인이 되세요!", 40, Color.white, outlined,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(104, 2), new Vector2(480, 80), TextAlignmentOptions.Left, true);

        // 대기 화면 위 가운데: 게임 타이틀 (게임이 시작되면 톡 커지며 사라짐)
        Image title = Img(top, "Title", Sprite("Logo_Title"), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(640, 406));
        title.gameObject.AddComponent<CanvasGroup>();
        hud.title = title.rectTransform;

        // 위 가운데: 작은 제목 + 기울어진 분홍 배지 (Fall Guys의 LAP 1/2처럼). 게임 시작 때 위에서 튀어나옴
        TMP_Text iceLabel = Label(top, "IceLabel", "남은 얼음", 30, Color.white, outlinedThin,
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
        Label(timer.transform, "Label", "경기 시간", 26, LightBlue, outlinedThin,
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(-104, -12), new Vector2(260, 38), TextAlignmentOptions.Right, true);
        hud.timerText = Label(timer.transform, "Time", "0:00.0", 64, Color.white, outlined,
            new Vector2(1, 0), new Vector2(1, 0), new Vector2(-100, 10), new Vector2(300, 84), TextAlignmentOptions.Right, true);

        Image best = Img(top, "Best", Sprite("Gen_SkewNavy"), new Vector2(1, 1), new Vector2(1, 1), new Vector2(60, -178), new Vector2(330, 64));
        hud.bestText = Label(best.transform, "Text", "우승  0회", 28, Color.white, outlinedThin,
            new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-96, 0), new Vector2(240, 44), TextAlignmentOptions.Right, true);

        // 왼쪽 위 목표 띠 아래: 맵 위에 남은 인원 / 전체 (화면 끝에 붙은 남색 띠)
        Image players = Img(top, "Players", Sprite("Gen_SkewNavy"), new Vector2(0, 1), new Vector2(0, 1), new Vector2(-60, -150), new Vector2(430, 72));
        Label(players.transform, "Label", "남은 인원", 26, LightBlue, outlinedThin,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(96, 0), new Vector2(150, 44), TextAlignmentOptions.Left, true);
        hud.playersText = Label(players.transform, "Count", "4 / 4", 40, Color.white, outlined,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(240, 2), new Vector2(160, 60), TextAlignmentOptions.Left, true);
        hud.playersBadge = players.rectTransform;

        // 대기 중에는 숨겼다가 게임 시작 때 튀어나오는 요소
        hud.popFromTop = new[] { iceLabel.rectTransform, badge.rectTransform, players.rectTransform };
        hud.popFromRight = new[] { timer.rectTransform, best.rectTransform };

        // 최고 기록 아래: 가진 파워업 (방패 / 슈퍼 점프 남은 시간)
        Image powerUp = Img(top, "PowerUp", Sprite("Gen_SkewGold"), new Vector2(1, 1), new Vector2(1, 1), new Vector2(60, -250), new Vector2(380, 64));
        hud.powerUpText = Label(powerUp.transform, "Text", "방패", 28, Color.white, outlinedThin,
            new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-96, 1), new Vector2(290, 44), TextAlignmentOptions.Right, true);
        hud.powerUpBadge = powerUp.gameObject;
        hud.playerPowerUps = Object.FindAnyObjectByType<PlayerPowerUps>();
        powerUp.gameObject.SetActive(false);

        // 위 가운데 배지 아래: 잠깐 뜨는 안내 (시련 예고, 파워업 획득)
        Image toast = Img(top, "Toast", Sprite("Gen_ChipDark"), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -222), new Vector2(560, 76), true);
        toast.gameObject.AddComponent<CanvasGroup>();
        hud.toastText = Label(toast.transform, "Text", "눈덩이가 떨어져요!", 40, Color.white, outlined,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 3), new Vector2(540, 64), TextAlignmentOptions.Center, true);
        hud.toast = toast.rectTransform;
        toast.gameObject.SetActive(false);

        // 새 부모 경로로 옮겨 예전 중앙 배치가 복원되지 않게 한다. 나머지 HUD 배치는 그대로 보존.
        var sidebar = new GameObject("RespawnSidebar", typeof(RectTransform));
        RectTransform sidebarRect = Place(sidebar, top, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-32, -344), new Vector2(340, 660));
        BuildRespawnTimer(sidebarRect, hud);
        BuildNpcRespawnList(sidebarRect, hud);
    }

    // 오른쪽 작은 카드: 중앙 플레이 화면을 가리지 않는 부활 안내
    static void BuildRespawnTimer(Transform parent, GameHUD hud)
    {
        var root = new GameObject("RespawnTimer", typeof(RectTransform), typeof(CanvasGroup));
        RectTransform rect = Place(root, parent, new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, new Vector2(340, 220));
        Img(rect, "Background", Sprite("Gen_PanelNavy"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(340, 220), true);

        Image title = Img(rect, "Title", Sprite("Gen_BoxMagenta"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 88), new Vector2(320, 48), true);
        title.rectTransform.localRotation = Quaternion.Euler(0, 0, -2f);
        Label(title.transform, "Text", "내 부활 대기", 28, Color.white, outlinedThin,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 2), new Vector2(300, 44), TextAlignmentOptions.Center, true);

        // 둥근 카운트다운: 흐린 바깥 고리 위에 노란 진행 막대, 그 위에 남색 원판과 숫자
        Image track = Img(rect, "Ring", Sprite("Gen_Circle"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-96, 0), new Vector2(108, 108));
        track.color = new Color(1f, 1f, 1f, 0.25f);
        Image fill = Img(rect, "RingFill", Sprite("Gen_Circle"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-96, 0), new Vector2(108, 108));
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Radial360;
        fill.fillOrigin = (int)Image.Origin360.Top;
        fill.fillClockwise = false;
        fill.fillAmount = 1f;
        fill.color = Yellow;
        hud.respawnFill = fill;
        Image disc = Img(rect, "Disc", Sprite("Gen_Circle"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-96, 0), new Vector2(88, 88));
        disc.color = DeepNavy;
        hud.respawnText = Label(disc.transform, "Count", "10", 46, Color.white, outlined,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 8), new Vector2(84, 60), TextAlignmentOptions.Center, true);
        Label(disc.transform, "Unit", "초", 18, LightBlue, outlinedThin,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -27), new Vector2(60, 24), TextAlignmentOptions.Center, true);

        hud.respawnCaption = Label(rect, "Caption", "하늘에서 다시 들어와요", 21, Color.white, outlinedThin,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(64, 0), new Vector2(190, 70), TextAlignmentOptions.Center, false);
        hud.respawnCaption.textWrappingMode = TextWrappingModes.Normal;
        Label(rect, "Warning", "한 명만 남으면 부활할 수 없어요", 20, Yellow, outlinedThin,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -83), new Vector2(320, 30), TextAlignmentOptions.Center, false);

        hud.respawnGroup = root;
        hud.respawnShowDelay = 0f;
        root.SetActive(false);
    }

    static void BuildNpcRespawnList(Transform parent, GameHUD hud)
    {
        Image panel = Img(parent, "NpcRespawns", Sprite("Gen_PanelNavy"), new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, new Vector2(340, 90), true);
        Label(panel.transform, "Title", "NPC 부활 대기", 26, LightBlue, outlinedThin,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -10), new Vector2(304, 36), TextAlignmentOptions.Left, false);
        hud.npcRespawnRowTemplate = Label(panel.transform, "RowTemplate", "NPC 1 · 10.0초", 23, Color.white, outlinedThin,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -50), new Vector2(304, 30), TextAlignmentOptions.Left, false);
        hud.npcRespawnGroup = panel.gameObject;
        hud.npcRespawnRowTemplate.gameObject.SetActive(false);
        panel.gameObject.SetActive(false);
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
        Label(desc.transform, "Line1", "서로 밀쳐 떨어뜨리고 마지막까지 남으면 1등!", 26, Color.white, outlinedThin,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(40, 18), new Vector2(700, 40), TextAlignmentOptions.Left, false);
        Label(desc.transform, "Line2", "움직이면 시작!", 30, Yellow, outlinedThin,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(40, -22), new Vector2(700, 44), TextAlignmentOptions.Left, true);
        Img(desc.transform, "Icon", Sprite("Icon_IceCube"), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-56, 2), new Vector2(76, 78));

        // 오른쪽 아래: 키 칩 (오른쪽부터 채움)
        var controls = new GameObject("Controls", typeof(RectTransform));
        RectTransform controlsRect = Place(controls, bottom, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 40), new Vector2(1200, 64));
        hud.controlsGroup = controls;
        float x = 0f;
        foreach (var (key, label) in new[] { ("좌클릭", "밀치기"),("Shift", "다이브"), ("Space", "점프"), ("WASD", "이동") })
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
        hud.resultTimeText = Label(header.transform, "Text", "1등!", 60, Color.white, outlined,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 6), new Vector2(620, 100), TextAlignmentOptions.Center, true);

        // 1등이면 금색 띠 (요소 이름은 배치 보존을 위해 예전 이름 NewRecord 그대로)
        Image ribbon = Img(c, "NewRecord", Sprite("Gen_SkewGold"), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -122), new Vector2(430, 86));
        Label(ribbon.transform, "Text", "우승!", 50, Color.white, outlined,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 4), new Vector2(380, 76), TextAlignmentOptions.Center, true);
        hud.newRecordRibbon = ribbon.gameObject;

        Label(c, "BestLabel", "우승 횟수", 32, LightBlue, outlinedThin,
            new Vector2(0.5f, 0.5f), new Vector2(1, 0.5f), new Vector2(-20, 6), new Vector2(260, 50), TextAlignmentOptions.Right, true);
        hud.resultBestText = Label(c, "BestValue", "0회", 52, Color.white, outlined,
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
        text.fontStyle = FontStyles.Normal;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        return text;
    }
}
