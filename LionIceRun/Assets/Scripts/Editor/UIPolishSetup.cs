using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 참고: Fall Guys (2024 & Create Mode). 화면 자산은 기존 UI와 자체 캐릭터만 사용한다.
public static class UIPolishSetup
{
    const string FontPath = "Assets/UI/Fonts/MalgunGothicBold SDF.asset";
    const string PlainPath = "Assets/UI/Fonts/MalgunGothicBold Plain.mat";
    const string PreviewFolder = "Assets/UI/CharacterPreviews";
    public static readonly Color Ink = new Color(0.045f, 0.12f, 0.25f);
    static readonly Color Ocean = new Color(0.06f, 0.32f, 0.65f);
    static readonly Color Cyan = new Color(0.08f, 0.64f, 0.82f);
    static readonly Color Gold = new Color(1f, 0.85f, 0.23f);
    static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/" + name + ".png");

    [MenuItem("LionIceRun/UI 전체 업데이트")]
    [MenuItem("LionIceRun/UI 폴리싱")]
    public static void Run()
    {
        var front = Object.FindAnyObjectByType<OnlineFrontend>(FindObjectsInactive.Include);
        if (front == null) { Debug.LogError("친구 멀티 세팅을 먼저 실행하세요."); return; }
        foreach (string rootName in new[] { "OnlineUI", "MapSelectionUI", "GameUI" })
        {
            var root = System.Array.Find(front.gameObject.scene.GetRootGameObjects(), go => go.name == rootName);
            if (root == null) continue;
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.GetComponentInParent<TMP_InputField>() != null) continue;
                Undo.RecordObject(text, "UI 글자 대비 개선");
                text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
                bool light = LightBackground(text.transform);
                text.fontSharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(light || rootName == "OnlineUI" ? PlainPath : "Assets/UI/Fonts/MalgunGothicBold OutlinedThin.mat");
                text.color = light ? Ink : Color.white;
                text.richText = false; EditorUtility.SetDirty(text);
            }
            foreach (var button in root.GetComponentsInChildren<Button>(true)) StyleButton(button);
            foreach (var field in root.GetComponentsInChildren<TMP_InputField>(true)) StyleInput(field);
        }
        StylePanel(front.modesPanel, Ocean); StylePanel(front.lobbyPanel, Ocean); StylePanel(front.resultsPanel, Ocean);
        BuildSelection(front);
        StyleDropdown(front.capacity); StyleDropdown(front.aggressive);
        var botCheck = front.fillBots.graphic as TMP_Text;
        if (botCheck != null) { botCheck.text = "●"; botCheck.color = Gold; botCheck.fontSize = 24f; }
        BuildPortraits(front);
        OnlineSeriesSetup.Apply(front);
        UIConsistency.Apply(front.gameObject.scene, AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath));
        var roulette = Object.FindAnyObjectByType<MapRoulette>(FindObjectsInactive.Include);
        if (roulette != null && roulette.stopButton != null)
        {
            Undo.DestroyObjectImmediate(roulette.stopButton.gameObject);
            roulette.stopButton = null; roulette.buttonText = null;
            EditorUtility.SetDirty(roulette);
        }
        front.gameObject.SetActive(true);
        front.GetComponent<Canvas>().enabled = true;
        front.OpenModes();
        Selection.activeGameObject = front.modesPanel;
        SceneView.RepaintAll();
        EditorUtility.SetDirty(front); EditorSceneManager.MarkSceneDirty(front.gameObject.scene);
        EditorSceneManager.SaveScene(front.gameObject.scene); AssetDatabase.SaveAssets();
        Debug.Log("전체 UI 업데이트 저장 완료: 입력창·모델/화살표 선택·로비 초상화·5판 결과/점수 표시. Scene 뷰에는 모드 선택 화면을 표시하며 Play는 타이틀부터 시작합니다. 기존 HUD 배치는 유지합니다. 빌드는 수행하지 않았습니다.");
    }
    [MenuItem("LionIceRun/UI 폴리싱", true)]
    [MenuItem("LionIceRun/UI 전체 업데이트", true)]
    static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

    public static void StyleInput(TMP_InputField input)
    {
        Undo.RecordObject(input, "입력창 가독성 개선");
        // TMP 내부 IME 밑줄 태그 생성 여부와 글자 렌더러의 설정을 같은 값으로 맞춘다.
        input.richText = false; input.isRichTextEditingAllowed = false;
        input.customCaretColor = true; input.caretColor = Ink; input.caretWidth = 3;
        input.selectionColor = new Color(0.08f, 0.55f, 0.95f, 0.3f);
        var background = input.GetComponent<Image>();
        if (background != null)
        {
            background.sprite = Sprite("Gen_CardLight"); background.type = Image.Type.Sliced;
            background.color = Color.white; input.targetGraphic = background;
        }
        if (input.textComponent != null)
        {
            input.textComponent.richText = false; input.textComponent.color = Ink;
            input.textComponent.fontSharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(PlainPath);
            input.textComponent.alignment = TextAlignmentOptions.MidlineLeft;
            Stretch(input.textComponent.rectTransform, 0f, 0f);
            EditorUtility.SetDirty(input.textComponent);
        }
        if (input.placeholder is TMP_Text hint)
        { hint.color = new Color(0.3f, 0.4f, 0.53f); hint.fontSharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(PlainPath); Stretch(hint.rectTransform, 0f, 0f); EditorUtility.SetDirty(hint); }
        if (input.textViewport != null) Stretch(input.textViewport, 22f, 8f);
        var colors = input.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(0.87f, 0.96f, 1f);
        colors.selectedColor = colors.highlightedColor; colors.disabledColor = new Color(0.75f, 0.82f, 0.88f); input.colors = colors;
        EditorUtility.SetDirty(input); if (background != null) EditorUtility.SetDirty(background);
    }

    static void StyleButton(Button button)
    {
        Undo.RecordObject(button, "버튼 반응 개선");
        var colors = button.colors; colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.96f, 0.7f); colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(0.76f, 0.9f, 1f); colors.disabledColor = new Color(0.55f, 0.65f, 0.77f);
        colors.fadeDuration = 0.12f; button.colors = colors;
        if (button.GetComponent<UIButtonPop>() == null) button.gameObject.AddComponent<UIButtonPop>();
        var shadow = button.GetComponent<Shadow>() ?? button.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.025f, 0.06f, 0.16f, 0.5f); shadow.effectDistance = new Vector2(0f, -6f);
        EditorUtility.SetDirty(button);
    }
    static void StyleDropdown(TMP_Dropdown dropdown)
    {
        var arrow = Label(dropdown.transform, "PolishArrow", "▼", Vector2.zero, new Vector2(24f, 36f), 18f, Color.white);
        var rect = arrow.rectTransform; rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f); rect.anchoredPosition = new Vector2(-12f, 0f);
        var colors = dropdown.colors; colors.normalColor = Color.white;
        colors.highlightedColor = colors.selectedColor = new Color(0.8f, 0.95f, 1f);
        colors.disabledColor = new Color(0.55f, 0.65f, 0.77f); dropdown.colors = colors;
        EditorUtility.SetDirty(dropdown);
    }
    static bool LightBackground(Transform parent)
    {
        while (parent != null)
        {
            var image = parent.GetComponent<Image>();
            if (image != null && image.sprite != null)
            {
                string name = image.sprite.name;
                float tint = image.color.r * 0.2126f + image.color.g * 0.7152f + image.color.b * 0.0722f;
                return tint > 0.65f && (name.Contains("White") || name.Contains("Light") || name.Contains("Yellow") || name.Contains("KeyCap"));
            }
            parent = parent.parent;
        }
        return false;
    }
    static void StylePanel(GameObject panel, Color color)
    {
        if (panel == null) return;
        var image = panel.GetComponent<Image>(); if (image == null) return;
        image.sprite = Sprite("Gen_CardLight"); image.type = Image.Type.Sliced; image.color = color;
        var outline = panel.GetComponent<Outline>() ?? panel.AddComponent<Outline>();
        outline.effectColor = Color.white; outline.effectDistance = new Vector2(3f, -3f);
    }
    static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var existing = parent.Find(name) as RectTransform; if (existing != null) return existing;
        var go = new GameObject(name, typeof(RectTransform)); var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
        rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
    }
    static TMP_Text Label(Transform parent, string name, string value, Vector2 position, Vector2 size, float fontSize, Color color)
    {
        var rect = Rect(parent, name, position, size);
        var text = rect.GetComponent<TextMeshProUGUI>() ?? rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath); text.fontSharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(PlainPath);
        text.text = value; text.color = color; text.fontSize = fontSize; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false; text.richText = false;
        return text;
    }
    static Button Arrow(Transform parent, string name, string value, Vector2 position)
    {
        var rect = Rect(parent, name, position, new Vector2(78f, 78f));
        var image = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>(); image.sprite = Sprite("Panel_Pink"); image.type = Image.Type.Sliced;
        var button = rect.GetComponent<Button>() ?? rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        Label(rect, "Text", value, Vector2.zero, new Vector2(68f, 68f), 48f, Color.white); StyleButton(button); return button;
    }
    static void Move(Component component, Vector2 position, Vector2 size)
    { var rect = component.transform as RectTransform; Undo.RecordObject(rect, "캐릭터 선택 화면 배치"); rect.anchoredPosition = position; rect.sizeDelta = size; }
    static void Stretch(RectTransform rect, float x, float y)
    { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(x, y); rect.offsetMax = new Vector2(-x, -y); }

    static void BuildSelection(OnlineFrontend front)
    {
        bool first = front.modesPanel.transform.Find("AvatarPicker") == null;
        var panel = Rect(front.modesPanel.transform, "AvatarPicker", new Vector2(-490f, -5f), new Vector2(540f, 680f));
        var background = panel.GetComponent<Image>() ?? panel.gameObject.AddComponent<Image>(); background.sprite = Sprite("Gen_CardLight"); background.type = Image.Type.Sliced; background.color = Cyan;
        var picker = panel.GetComponent<CharacterSelection>() ?? panel.gameObject.AddComponent<CharacterSelection>();
        picker.previous = Arrow(panel, "Previous", "‹", new Vector2(-215f, -65f)); picker.next = Arrow(panel, "Next", "›", new Vector2(215f, -65f));
        picker.caption = Label(panel, "Caption", "사자", new Vector2(0f, -260f), new Vector2(360f, 50f), 32f, Color.white);
        picker.count = Label(panel, "Count", "1 / 3", new Vector2(0f, -310f), new Vector2(240f, 40f), 23f, Color.white);
        Label(panel, "Title", "나의 캐릭터", new Vector2(0f, 285f), new Vector2(400f, 55f), 32f, Color.white);
        var preview = Rect(panel, "Model", new Vector2(0f, 10f), new Vector2(371.25f, 495f));
        picker.image = preview.GetComponent<RawImage>() ?? preview.gameObject.AddComponent<RawImage>(); picker.image.raycastTarget = false;
        CharacterPreviewSetup.Configure(picker, front.session.manager.player.GetComponent<PlayerSkin>());
        front.characterSelection = picker; front.skin.gameObject.SetActive(false);
        front.modesPanel.transform.Find("Title").GetComponent<TMP_Text>().color = Gold;
        front.modesPanel.transform.Find("Title").GetComponent<TMP_Text>().fontSharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/UI/Fonts/MalgunGothicBold OutlinedThin.mat");
        if (first)
        {
            ((RectTransform)front.modesPanel.transform).sizeDelta = new Vector2(1720f, 930f);
            var title = front.modesPanel.transform.Find("Title").GetComponent<TMP_Text>(); Move(title, new Vector2(0f, 385f), new Vector2(1500f, 85f)); title.color = Gold;
            Move(front.modesPanel.transform.Find("NameLabel"), new Vector2(180f, 270f), new Vector2(220f, 45f));
            Move(front.nickname, new Vector2(410f, 210f), new Vector2(650f, 76f));
            Move(front.soloButton, new Vector2(410f, 75f), new Vector2(650f, 100f));
            Move(front.createButton, new Vector2(295f, -70f), new Vector2(420f, 95f));
            Move(front.capacity, new Vector2(630f, -70f), new Vector2(210f, 65f));
            Move(front.joinCode, new Vector2(275f, -215f), new Vector2(380f, 75f));
            Move(front.joinButton, new Vector2(615f, -215f), new Vector2(250f, 95f));
            Move(front.notice, new Vector2(410f, -320f), new Vector2(660f, 80f)); front.notice.fontSize = 24f;
            Move(front.backButton, new Vector2(0f, -405f), new Vector2(300f, 70f));
            StyleInput(front.nickname); StyleInput(front.joinCode);
        }
        front.soloButton.GetComponent<Image>().sprite = Sprite("Panel_Pink");
        front.soloButton.GetComponentInChildren<TMP_Text>().color = Color.white;
        front.createButton.GetComponent<Image>().sprite = Sprite("Panel_Yellow"); front.createButton.GetComponentInChildren<TMP_Text>().color = Ink;
        front.joinButton.GetComponent<Image>().sprite = Sprite("Panel_Blue"); front.joinButton.GetComponentInChildren<TMP_Text>().color = Color.white;
        EditorUtility.SetDirty(picker);
    }

    static void BuildPortraits(OnlineFrontend front)
    {
        front.characterPortraits = CharacterPreviewSetup.CapturePortraits(front.characterSelection, PreviewFolder);
        front.slotPortraits = new Image[4];
        for (int i = 0; i < 4; i++)
        {
            var slot = front.slotSkins[i].transform.parent;
            bool first = slot.Find("Portrait") == null;
            var rect = Rect(slot, "Portrait", new Vector2(0f, 32f), new Vector2(155f, 165f));
            var image = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
            image.preserveAspect = true; image.raycastTarget = false; image.sprite = front.characterPortraits[0]; front.slotPortraits[i] = image;
            front.slotSkins[i].gameObject.SetActive(false);
            front.slots[i].enableAutoSizing = true; front.slots[i].fontSizeMin = 18f; front.slots[i].fontSizeMax = 29f;
            if (first) Move(front.slots[i], new Vector2(0f, -87f), new Vector2(280f, 78f));
        }
    }
}
