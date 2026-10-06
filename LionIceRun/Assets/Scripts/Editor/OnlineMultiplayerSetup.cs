using System;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 기존 화면을 유지하면서 로비 UI와 네트워크 템플릿을 씬에 저장한다. 재실행 시 사용자 배치를 보존한다.
public static class OnlineMultiplayerSetup
{
    const string ActorPath = "Assets/Prefabs/OnlinePlayer.prefab";
    static TMP_FontAsset font;
    static readonly Color Navy = new Color(0.04f, 0.08f, 0.18f, 0.94f);
    static readonly Color Blue = new Color(0.13f, 0.29f, 0.48f);
    static readonly Color Yellow = new Color(1f, 0.86f, 0.25f);

    [MenuItem("LionIceRun/친구 멀티 세팅")]
    static void Setup()
    {
        var manager = UnityEngine.Object.FindAnyObjectByType<GameManager>();
        var roulette = UnityEngine.Object.FindAnyObjectByType<MapRoulette>();
        if (manager == null || manager.player == null || roulette == null || roulette.selectionCanvas == null)
        { Debug.LogError("친구 멀티: GameManager·플레이어와 맵 선택 화면 세팅이 먼저 필요합니다."); return; }
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UI/Fonts/MalgunGothicBold SDF.asset");
        if (font == null) { Debug.LogError("Step 7 UI 세팅으로 글꼴을 먼저 생성하세요."); return; }

        var service = manager.GetComponent<OnlineGameSession>() ?? manager.gameObject.AddComponent<OnlineGameSession>();
        GameObject networkRoot = GameObject.Find("OnlineNetwork");
        if (networkRoot == null) networkRoot = new GameObject("OnlineNetwork");
        var transport = networkRoot.GetComponent<UnityTransport>() ?? networkRoot.AddComponent<UnityTransport>();
        var network = networkRoot.GetComponent<NetworkManager>() ?? networkRoot.AddComponent<NetworkManager>();
        if (network.NetworkConfig == null) network.NetworkConfig = new NetworkConfig();
        network.NetworkConfig.NetworkTransport = transport;
        network.NetworkConfig.EnableSceneManagement = false;
        network.NetworkConfig.ConnectionApproval = false;
        network.NetworkConfig.PlayerPrefab = null;
        network.NetworkConfig.TickRate = 30;
        network.NetworkConfig.ProtocolVersion = OnlineRules.Protocol;
        network.NetworkConfig.ForceSamePrefabs = false;
        service.network = network; service.manager = manager; service.roulette = roulette;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ActorPath);
        if (prefab == null)
        {
            GameObject template = UnityEngine.Object.Instantiate(manager.player.gameObject);
            template.name = "OnlinePlayer"; template.SetActive(false);
            var controller = template.GetComponent<PlayerController>();
            controller.grid = null; controller.spawnAtStartCell = false; controller.useExternalInput = true;
            var tracker = template.GetComponent<IceStepTracker>(); if (tracker != null) tracker.grid = null;
            if (template.GetComponent<PlayerPowerUps>() == null) template.AddComponent<PlayerPowerUps>();
            foreach (var component in template.GetComponents<Component>())
                if (component is BotBrain || component is OnlineActor) UnityEngine.Object.DestroyImmediate(component);
            prefab = PrefabUtility.SaveAsPrefabAsset(template, ActorPath);
            UnityEngine.Object.DestroyImmediate(template);
        }
        CombatPresentationSetup.ApplyPlayer(manager.player.gameObject, "플레이어");
        CombatPresentationSetup.UpdateOnlinePrefab();
        PresentationEffectsSetup.CreateCountdown();
        service.actorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ActorPath);

        GameObject ui = GameObject.Find("OnlineUI");
        if (ui == null) ui = new GameObject("OnlineUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = ui.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 110;
        var scaler = ui.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
        var frontend = ui.GetComponent<OnlineFrontend>() ?? ui.AddComponent<OnlineFrontend>();
        service.frontend = frontend; frontend.session = service;

        var modes = Panel(ui.transform, "Modes", new Vector2(1100, 850)); frontend.modesPanel = modes.gameObject;
        Text(modes, "Title", "함께 얼음을 깨요!", 52, new Vector2(0, 345), new Vector2(900, 80), Yellow);
        Text(modes, "NameLabel", "닉네임", 26, new Vector2(-380, 245), new Vector2(220, 55));
        frontend.nickname = Input(modes, "Nickname", "얼음 친구", new Vector2(-130, 245), new Vector2(340, 65), 16);
        frontend.skin = Dropdown(modes, "Skin", new[] { "사자", "북극곰", "호랑이" }, new Vector2(265, 245), new Vector2(270, 65));
        frontend.soloButton = Button(modes, "Solo", "혼자 하기", new Vector2(0, 125), new Vector2(500, 95));
        frontend.createButton = Button(modes, "Create", "로비 만들기", new Vector2(-145, 0), new Vector2(460, 95));
        frontend.capacity = Dropdown(modes, "Capacity", new[] { "최대 2명", "최대 3명", "최대 4명" }, new Vector2(310, 0), new Vector2(250, 65));
        frontend.capacity.value = 2;
        frontend.joinCode = Input(modes, "JoinCode", "친구의 방 코드", new Vector2(-170, -125), new Vector2(420, 65), 16);
        frontend.joinButton = Button(modes, "Join", "코드로 참가", new Vector2(290, -125), new Vector2(350, 95));
        frontend.notice = Text(modes, "Notice", "방 코드를 공유해 친구와 함께 플레이하세요.", 26, new Vector2(0, -255), new Vector2(940, 120));
        frontend.backButton = Button(modes, "Back", "타이틀로", new Vector2(0, -355), new Vector2(300, 70), false);

        var lobby = Panel(ui.transform, "Lobby", new Vector2(1440, 850)); frontend.lobbyPanel = lobby.gameObject;
        Text(lobby, "Title", "친구 로비", 54, new Vector2(0, 345), new Vector2(900, 80), Yellow);
        frontend.roomCode = Text(lobby, "RoomCode", "방 코드", 36, new Vector2(-80, 250), new Vector2(850, 65));
        frontend.copyButton = Button(lobby, "Copy", "코드 복사", new Vector2(455, 250), new Vector2(250, 75), false);
        frontend.slots = new TMP_Text[4]; frontend.slotSkins = new TMP_Text[4];
        for (int i = 0; i < 4; i++)
        {
            var slot = Panel(lobby, "Slot" + i, new Vector2(300, 260), new Vector2(-495 + i * 330, 50), Blue);
            frontend.slotSkins[i] = Text(slot, "Skin", "방 코드를 공유하세요", 25, new Vector2(0, 65), new Vector2(270, 55), Yellow);
            frontend.slots[i] = Text(slot, "Info", "친구를 기다려요", 29, new Vector2(0, -35), new Vector2(270, 120));
        }
        frontend.fillBots = Toggle(lobby, "FillBots", "빈자리 봇 채우기", new Vector2(-300, -150));
        frontend.aggressive = Dropdown(lobby, "Aggressive", new[] { "봇 모두 신중형", "공격형 최대 1명", "공격형 최대 2명" }, new Vector2(275, -150), new Vector2(360, 60));
        frontend.lobbyNotice = Text(lobby, "Notice", "모두 준비하기를 눌러 주세요.", 27, new Vector2(0, -245), new Vector2(1200, 75));
        frontend.readyButton = Button(lobby, "Ready", "준비하기", new Vector2(-195, -345), new Vector2(350, 100));
        frontend.readyText = frontend.readyButton.GetComponentInChildren<TMP_Text>();
        frontend.startButton = Button(lobby, "Start", "경기 시작", new Vector2(195, -345), new Vector2(350, 100));
        frontend.leaveButton = Button(lobby, "Leave", "나가기", new Vector2(545, -345), new Vector2(230, 75), false);

        var loading = Group(ui.transform, "Loading", new Vector2(1100, 180), new Vector2(0, 330)); frontend.loadingPanel = loading.gameObject;
        frontend.countdownText = Text(loading, "Text", "모두의 맵을 준비하고 있어요…", 52, Vector2.zero, new Vector2(1100, 160), Yellow);
        var result = Panel(ui.transform, "Results", new Vector2(900, 740)); frontend.resultsPanel = result.gameObject;
        Text(result, "Title", "경기 결과", 52, new Vector2(0, 280), new Vector2(800, 80), Yellow);
        frontend.resultText = Text(result, "Ranking", "순위", 38, new Vector2(0, 20), new Vector2(760, 340));
        frontend.returnButton = Button(result, "Return", "로비로 돌아가기", new Vector2(-180, -265), new Vector2(430, 95));
        frontend.resultLeaveButton = Button(result, "Leave", "나가기", new Vector2(250, -265), new Vector2(260, 75), false);
        Text(result, "Hint", "다음 경기는 방장이 로비로 돌아간 뒤 준비해요", 24, new Vector2(0, -340), new Vector2(800, 45));
        var leave = Group(ui.transform, "MatchLeave", new Vector2(260, 80), new Vector2(-785, -460)); frontend.leavePanel = leave.gameObject;
        frontend.matchLeaveButton = Button(leave, "Leave", "로비 나가기", Vector2.zero, new Vector2(260, 75), false);

        frontend.HidePanels();
        EditorUtility.SetDirty(service); EditorUtility.SetDirty(frontend); EditorUtility.SetDirty(network);
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene); EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets();
        Selection.activeGameObject = ui;
        Debug.Log("친구 멀티 세팅 완료. 타이틀 시작하기 → 로비 만들기/코드로 참가. Unity Dashboard에서 Authentication·Lobby·Relay 활성화 확인 후 별도 PC 빌드로 테스트하세요. 이 메뉴는 기존 OnlineUI 배치를 보존합니다.");
    }

    [MenuItem("LionIceRun/친구 멀티 세팅", true)]
    static bool CanSetup() => !EditorApplication.isPlayingOrWillChangePlaymode;

    static RectTransform Group(Transform parent, string name, Vector2 size, Vector2 position)
    {
        var old = parent.Find(name) as RectTransform;
        if (old != null) return old;
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform; rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
        rect.sizeDelta = size; rect.anchoredPosition = position;
        return rect;
    }
    static RectTransform Panel(Transform parent, string name, Vector2 size, Vector2 position = default, Color color = default)
    {
        var rect = Group(parent, name, size, position);
        if (rect.GetComponent<Image>() == null)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/Gen_PanelNavy.png");
            image.type = Image.Type.Sliced; image.color = color == default ? Navy : color; image.raycastTarget = true;
        }
        return rect;
    }
    static TMP_Text Text(Transform parent, string name, string text, float size, Vector2 position, Vector2 area, Color color = default)
    {
        var rect = Group(parent, name, area, position);
        var label = rect.GetComponent<TextMeshProUGUI>();
        if (label == null)
        {
            label = rect.gameObject.AddComponent<TextMeshProUGUI>(); label.font = font; label.fontSize = size;
            label.color = color == default ? Color.white : color; label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false; label.richText = false;
        }
        label.text = text; return label;
    }
    static Button Button(Transform parent, string name, string text, Vector2 position, Vector2 size, bool yellow = true)
    {
        var rect = Group(parent, name, size, position);
        var button = rect.GetComponent<Button>();
        if (button == null)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/Panel_" + (yellow ? "Yellow" : "Blue") + ".png");
            image.type = Image.Type.Sliced;
            button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            rect.gameObject.AddComponent<UIButtonPop>();
        }
        Text(rect, "Text", text, 30, Vector2.zero, size - new Vector2(30, 10), yellow ? new Color(0.24f, 0.14f, 0.05f) : Color.white);
        return button;
    }
    static TMP_InputField Input(Transform parent, string name, string placeholder, Vector2 position, Vector2 size, int limit)
    {
        var rect = Panel(parent, name, size, position, Color.white);
        var input = rect.GetComponent<TMP_InputField>();
        if (input != null) { UIPolishSetup.StyleInput(input); return input; }
        input = rect.gameObject.AddComponent<TMP_InputField>();
        var viewport = Group(rect, "Viewport", size - new Vector2(24, 8), Vector2.zero); viewport.gameObject.AddComponent<RectMask2D>();
        var label = Text(viewport, "Text", "", 28, Vector2.zero, size - new Vector2(28, 8), Navy);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        var hint = Text(viewport, "Placeholder", placeholder, 25, Vector2.zero, size - new Vector2(28, 8), new Color(0.4f, 0.5f, 0.6f));
        hint.alignment = TextAlignmentOptions.MidlineLeft;
        input.textViewport = viewport; input.textComponent = (TMP_Text)label; input.placeholder = hint;
        input.characterLimit = limit; input.lineType = TMP_InputField.LineType.SingleLine;
        UIPolishSetup.StyleInput(input);
        return input;
    }
    static TMP_Dropdown Dropdown(Transform parent, string name, string[] options, Vector2 position, Vector2 size)
    {
        var rect = Panel(parent, name, size, position, Blue);
        var dropdown = rect.GetComponent<TMP_Dropdown>(); if (dropdown != null) return dropdown;
        dropdown = rect.gameObject.AddComponent<TMP_Dropdown>(); dropdown.targetGraphic = rect.GetComponent<Image>();
        dropdown.captionText = Text(rect, "Caption", options[0], 26, Vector2.zero, size - new Vector2(20, 8));
        var template = Panel(rect, "Template", new Vector2(size.x, 180), new Vector2(0, -120), Blue);
        var scroll = template.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false;
        var viewport = Group(template, "Viewport", new Vector2(size.x, 180), Vector2.zero); viewport.gameObject.AddComponent<RectMask2D>();
        var content = Group(viewport, "Content", new Vector2(size.x, 60), new Vector2(0, 60));
        content.anchorMin = content.anchorMax = new Vector2(0.5f, 1); content.pivot = new Vector2(0.5f, 1); content.anchoredPosition = Vector2.zero;
        var item = Group(content, "Item", new Vector2(size.x, 60), new Vector2(0, -30));
        item.anchorMin = item.anchorMax = new Vector2(0.5f, 1); item.anchoredPosition = new Vector2(0, -30);
        var image = item.gameObject.AddComponent<Image>(); image.color = Blue;
        var toggle = item.gameObject.AddComponent<Toggle>(); toggle.targetGraphic = image;
        var check = Text(item, "Check", "●", 16, new Vector2(-size.x / 2 + 20, 0), new Vector2(30, 40), Yellow);
        toggle.graphic = check;
        dropdown.itemText = Text(item, "Label", options[0], 25, new Vector2(12, 0), size - new Vector2(50, 10));
        dropdown.template = template; scroll.viewport = viewport; scroll.content = content;
        dropdown.AddOptions(new System.Collections.Generic.List<string>(options)); template.gameObject.SetActive(false);
        return dropdown;
    }
    static Toggle Toggle(Transform parent, string name, string label, Vector2 position)
    {
        var rect = Group(parent, name, new Vector2(430, 60), position);
        var toggle = rect.GetComponent<Toggle>(); if (toggle != null) return toggle;
        var box = Panel(rect, "Box", new Vector2(45, 45), new Vector2(-185, 0), Blue);
        var check = Text(box, "Check", "✓", 32, Vector2.zero, new Vector2(45, 45), Yellow);
        toggle = rect.gameObject.AddComponent<Toggle>(); toggle.targetGraphic = box.GetComponent<Image>(); toggle.graphic = check;
        Text(rect, "Label", label, 26, new Vector2(30, 0), new Vector2(340, 60)); toggle.isOn = true;
        return toggle;
    }
}
