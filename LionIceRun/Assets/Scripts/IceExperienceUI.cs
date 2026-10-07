using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 저장된 씬을 다시 만들지 않고 현재 UI에 새 표면/기능을 적용한다. HUD의 사용자 위치는 유지한다.
[DefaultExecutionOrder(700)]
public class IceExperienceUI : MonoBehaviour
{
    public static IceExperienceUI Instance { get; private set; }
    public OnlineFrontend front;
    public GameObject settings, wheel, chatBox;
    RectTransform overlay;
    GameObject backdrop;
    GameObject controlsBackdrop;
    Button titleSettingsButton;
    TMP_Text bindingHint, anonymity, chatToggle, chatLines, wheelHint, controlHint;
    TMP_InputField mapSeconds;
    TMP_InputField chatField;
    GameObject chatHistory;
    ScrollRect chatScroll;
    bool scrollToLatest;
    Button[] bindingButtons;
    IceEmoteIcon[] wheelIcons;
    InputAction emoteAction, chatAction;
    readonly Queue<string> messages=new();
    int capturing=-1, selectedEmote=-1;
    int captureFrame;
    float nextSoloEmote;
    Vector2 dragOrigin;
    bool wheelOpen, wasInRoom;
    public bool SettingsOpen => settings != null && settings.activeSelf;
    public bool ChatOpen => chatBox != null && chatBox.activeSelf;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    { SceneManager.sceneLoaded-=OnScene; SceneManager.sceneLoaded+=OnScene; }
    static void OnScene(Scene scene, LoadSceneMode mode)
    {
        var f=Object.FindAnyObjectByType<OnlineFrontend>(FindObjectsInactive.Include);
        if (f != null && f.GetComponent<IceExperienceUI>() == null) f.gameObject.AddComponent<IceExperienceUI>().front=f;
    }
    void Start()
    {
        Instance=this;
        if (front == null) front=GetComponent<OnlineFrontend>();
        if (front == null || front.nickname == null) { enabled=false; return; }
        IceUI.Font=front.nickname.textComponent.font;
        Restyle(front);
        if (GameHUD.Instance != null && GameHUD.Instance.controlsGroup != null) GameHUD.Instance.controlsGroup.SetActive(false);
        BuildOverlay(); BuildSettings(); BuildWheel(); BuildChat();
        emoteAction=GamePreferences.CreateButton("SocialEmote",7); chatAction=GamePreferences.CreateButton("SocialChat",8);
        emoteAction.Enable(); chatAction.Enable();
        GamePreferences.Changed+=RefreshPreferences;
        front.session.ChatReceived+=ReceiveChat; front.session.EmoteReceived+=PlayerEmoteBubble.Display;
        GamePreferences.Apply();
    }
    public static void Restyle(OnlineFrontend f)
    {
        if (f == null) return;
        IceUI.Font=f.nickname.textComponent.font;
        foreach (var text in f.GetComponentsInChildren<TMP_Text>(true))
            IceUI.StyleText(text,Mathf.Clamp(text.fontSize,20,44),IceUI.White,text.alignment);
        foreach (var button in f.GetComponentsInChildren<Button>(true)) IceUI.StyleButton(button,IceUI.Card);
        foreach (var field in f.GetComponentsInChildren<TMP_InputField>(true)) IceUI.StyleField(field);
        foreach (var dropdown in f.GetComponentsInChildren<TMP_Dropdown>(true))
        {
            dropdown.targetGraphic=IceUI.Surface(dropdown.gameObject,IceUI.Card,18);
            IceUI.StyleText(dropdown.captionText,22,IceUI.White);
            if (dropdown.template != null)
            {
                IceUI.Surface(dropdown.template.gameObject,IceUI.Panel,18);
                foreach (var text in dropdown.template.GetComponentsInChildren<TMP_Text>(true)) IceUI.StyleText(text,22,IceUI.White);
                foreach (var toggle in dropdown.template.GetComponentsInChildren<Toggle>(true)) toggle.targetGraphic=IceUI.Surface(toggle.gameObject,IceUI.Card,8);
            }
        }
        foreach (var panel in new[]{f.modesPanel,f.lobbyPanel,f.loadingPanel,f.resultsPanel}) if (panel != null) IceUI.Surface(panel,IceUI.Panel,6);
        var modesSurface=IceUI.Surface(f.modesPanel,new Color(1,1,1,0),0); modesSurface.border=0; modesSurface.edge=Color.clear; modesSurface.Repaint();
        IceUI.Place(f.modesPanel.transform,new Vector2(0,0),new Vector2(1640,920));
        var modes=f.modesPanel.transform;
        var title=modes.Find("Title")?.GetComponent<TMP_Text>();
        IceUI.Place(title,new Vector2(355,305),new Vector2(720,90));
        if (title != null) { title.text="플레이"; IceUI.StyleText(title,48,IceUI.Ink,TextAlignmentOptions.MidlineLeft); }
        foreach(var name in new[]{"Edition","Subtitle"}) { var old=modes.Find(name); if(old!=null) old.gameObject.SetActive(false); }
        var nameLabel=modes.Find("NameLabel")?.GetComponent<TMP_Text>();
        IceUI.Place(nameLabel,new Vector2(355,178),new Vector2(720,30));
        IceUI.StyleText(nameLabel,20,IceUI.Muted,TextAlignmentOptions.MidlineLeft);
        IceUI.Place(f.nickname,new Vector2(355,119),new Vector2(720,76));
        IceUI.Place(f.soloButton,new Vector2(355,8),new Vector2(720,88)); IceUI.StyleButton(f.soloButton,IceUI.Mint,true);
        f.soloButton.GetComponentInChildren<TMP_Text>().text="혼자 하기";
        IceUI.Label(modes,"TogetherLabel","멀티플레이",new Vector2(355,-70),new Vector2(720,30),20,IceUI.Muted);
        IceUI.Place(f.createButton,new Vector2(220,-137),new Vector2(450,82));
        IceUI.StyleButton(f.createButton,new Color(1f,.57f,.42f));
        IceUI.Place(f.capacity,new Vector2(600,-137),new Vector2(230,82));
        IceUI.Place(f.joinCode,new Vector2(220,-246),new Vector2(450,76));
        IceUI.Place(f.joinButton,new Vector2(600,-246),new Vector2(230,76));
        IceUI.StyleButton(f.joinButton,new Color(.45f,.78f,1f));
        IceUI.Place(f.notice,new Vector2(355,-340),new Vector2(720,62)); IceUI.StyleText(f.notice,21,IceUI.Muted,TextAlignmentOptions.MidlineLeft);
        IceUI.Place(f.backButton,new Vector2(-490,-407),new Vector2(200,52));
        var picker=f.characterSelection;
        if (picker != null)
        {
            IceUI.Place(picker,new Vector2(-490,15),new Vector2(540,740));
            var portraitSurface=IceUI.Surface(picker.gameObject,new Color(1,1,1,0),0); portraitSurface.border=0; portraitSurface.edge=Color.clear; portraitSurface.Repaint();
            IceUI.Place(picker.image,new Vector2(0,30),new Vector2(370,493));
            var pickerTitle=picker.transform.Find("Title")?.GetComponent<TMP_Text>();
            if (pickerTitle != null) { pickerTitle.text="캐릭터"; IceUI.Place(pickerTitle,new Vector2(0,301),new Vector2(420,45)); IceUI.StyleText(pickerTitle,22,IceUI.Muted); }
            IceUI.Place(picker.caption,new Vector2(0,-250),new Vector2(300,60)); IceUI.StyleText(picker.caption,38,IceUI.White);
            IceUI.Place(picker.count,new Vector2(0,-313),new Vector2(200,35)); IceUI.StyleText(picker.count,20,IceUI.Muted);
            IceUI.Place(picker.previous,new Vector2(-209,-250),new Vector2(62,62));
            IceUI.Place(picker.next,new Vector2(209,-250),new Vector2(62,62));
            IceUI.StyleButton(picker.previous,IceUI.Card); IceUI.StyleButton(picker.next,IceUI.Card);
        }
        foreach (var slot in f.slots) if (slot != null) IceUI.Surface(slot.transform.parent.gameObject,IceUI.Card,24);
        IceUI.StyleButton(f.readyButton,IceUI.Mint,true); IceUI.StyleButton(f.startButton,new Color(1,.82f,.42f),true);
        if (f.resultsTitle != null) IceUI.StyleText(f.resultsTitle,44,IceUI.White);
        if (f.resultText != null) IceUI.StyleText(f.resultText,32,IceUI.White);
        var roulette=f.session.roulette;
        foreach (var button in roulette.GetComponentsInChildren<Button>(true)) IceUI.StyleButton(button,IceUI.Mint,true);
        if (roulette.startButton != null) IceUI.StyleButton(roulette.startButton,IceUI.Mint,true);
    }
    void BuildOverlay()
    {
        var root=new GameObject("IceExperienceOverlay",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        overlay=(RectTransform)root.transform; root.layer=5;
        var canvas=root.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.overrideSorting=true; canvas.sortingOrder=230;
        var scaler=root.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1920,1080); scaler.matchWidthOrHeight=.5f;
        var titleParent=front.session.roulette.titleGroup.transform;
        titleSettingsButton=IceUI.ButtonAt(titleParent,"SettingsButton","설정",new Vector2(0,-405),new Vector2(200,60),IceUI.Panel);
        titleSettingsButton.onClick.AddListener(()=>OpenSettings(!SettingsOpen));
        var controls=IceUI.PanelAt(overlay,"ControlsBackdrop",Vector2.zero,new Vector2(1312,44),new Color(.98f,.98f,.94f,.92f),6);
        controls.anchorMin=controls.anchorMax=controls.pivot=new Vector2(1,0); controls.anchoredPosition=new Vector2(-16,19);
        controls.GetComponent<IceUIShape>().raycastTarget=false; controlsBackdrop=controls.gameObject;
        controlHint=IceUI.Label(overlay,"Controls","",Vector2.zero,new Vector2(1280,34),18,IceUI.White,TextAlignmentOptions.MidlineRight);
        var hint=controlHint.rectTransform; hint.anchorMin=hint.anchorMax=hint.pivot=new Vector2(1,0); hint.anchoredPosition=new Vector2(-28,24);
        // 원본 화면/버튼 이름은 유지한다. 배경은 모드 선택/로비/결과에서만 보인다.
        var dim=IceUI.PanelAt(transform,"ExperienceBackdrop",Vector2.zero,new Vector2(1920,1080),new Color(.85f,.94f,1f,.88f),0);
        IceUI.Stretch(dim); dim.SetAsFirstSibling(); backdrop=dim.gameObject;
    }
    void BuildSettings()
    {
        settings=IceUI.PanelAt(overlay,"SettingsDim",Vector2.zero,new Vector2(1920,1080),new Color(.85f,.94f,1f,.9f),0).gameObject;
        IceUI.Stretch((RectTransform)settings.transform);
        var panel=IceUI.PanelAt(settings.transform,"Card",Vector2.zero,new Vector2(1120,900),IceUI.Panel,32);
        IceUI.Label(panel,"Title","설정",new Vector2(0,335),new Vector2(960,65),42);
        bindingButtons=new Button[9];
        for (int i=0;i<9;i++)
        {
            int index=i; int col=i<5?0:1; int row=i<5?i:i-5;
            float x=col==0?-270:270, y=227-row*76;
            IceUI.Label(panel,"Action"+i,GamePreferences.Labels[i],new Vector2(x-75,y),new Vector2(250,48),24);
            var button=IceUI.ButtonAt(panel,"Binding"+i,"",new Vector2(x+125,y),new Vector2(190,52));
            button.onClick.AddListener(()=>BeginBinding(index)); bindingButtons[i]=button;
        }
        IceUI.Label(panel,"MapSpeedLabel","맵 선택 (초)",new Vector2(195,-77),new Vector2(250,48),24);
        mapSeconds=IceUI.FieldAt(panel,"MapSeconds","1~15초",new Vector2(395,-77),new Vector2(190,52),5);
        mapSeconds.onEndEdit.AddListener(SaveMapSeconds);
        bindingHint=IceUI.Label(panel,"BindingHint","버튼을 누른 뒤 원하는 키를 입력하세요. Esc로 취소합니다.",new Vector2(0,-179),new Vector2(960,48),20,IceUI.Muted);
        var anonymousButton=IceUI.ButtonAt(panel,"Anonymous","",new Vector2(-240,-245),new Vector2(460,62));
        anonymity=anonymousButton.GetComponentInChildren<TMP_Text>(); anonymousButton.onClick.AddListener(()=>GamePreferences.SetAnonymous(!GamePreferences.Anonymous));
        var chatButton=IceUI.ButtonAt(panel,"ChatVisible","",new Vector2(240,-245),new Vector2(460,62));
        chatToggle=chatButton.GetComponentInChildren<TMP_Text>(); chatButton.onClick.AddListener(()=>GamePreferences.SetChatVisible(!GamePreferences.ChatVisible));
        IceUI.Label(panel,"PrivacyHint","익명 사용 시 동물 이름만 표시됩니다.\n온라인 맵 선택 속도는 방장 설정을 사용합니다.",new Vector2(0,-305),new Vector2(960,50),17,IceUI.Muted);
        var reset=IceUI.ButtonAt(panel,"Reset","기본 키로 되돌리기",new Vector2(-245,-370),new Vector2(410,64));
        reset.onClick.AddListener(()=>{ capturing=-1; GamePreferences.ResetBindings(); bindingHint.text="기본 조작으로 돌아왔어요. 다이브는 Shift입니다."; });
        var close=IceUI.ButtonAt(panel,"Close","완료",new Vector2(245,-370),new Vector2(410,64),IceUI.Mint,true);
        close.onClick.AddListener(()=>OpenSettings(false)); settings.SetActive(false);
    }
    void BuildWheel()
    {
        wheel=IceUI.PanelAt(overlay,"EmoteWheel",Vector2.zero,new Vector2(540,540),IceUI.Panel,6).gameObject;
        wheelIcons=new IceEmoteIcon[6];
        for (int i=0;i<6;i++)
        {
            float angle=(90-i*60)*Mathf.Deg2Rad;
            var rect=IceUI.Rect(wheel.transform,"Emote"+i,new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*183,new Vector2(88,88));
            var icon=rect.gameObject.AddComponent<IceEmoteIcon>(); icon.expression=i; icon.raycastTarget=false; wheelIcons[i]=icon;
            IceUI.Label(rect,"Name",IceEmoteIcon.Names[i],new Vector2(0,-64),new Vector2(134,32),20,IceUI.White,TextAlignmentOptions.Center);
        }
        wheelHint=IceUI.Label(wheel.transform,"Hint","마우스를 끌어\n감정을 선택하세요",Vector2.zero,new Vector2(210,100),23,IceUI.White,TextAlignmentOptions.Center);
        var release=IceUI.PanelAt(wheel.transform,"ReleaseCard",new Vector2(0,-306),new Vector2(560,42),IceUI.Panel,14);
        IceUI.Label(release,"Release","키를 놓으면 표현 · 가운데는 취소",Vector2.zero,new Vector2(540,35),19,IceUI.White,TextAlignmentOptions.Center);
        wheel.SetActive(false);
    }
    void BuildChat()
    {
        var root=IceUI.Rect(overlay,"ChatDock",Vector2.zero,new Vector2(470,260));
        root.anchorMin=root.anchorMax=root.pivot=new Vector2(0,0); root.anchoredPosition=new Vector2(28,26);
        var history=IceUI.PanelAt(root,"History",new Vector2(0,40),new Vector2(470,180),new Color(.98f,.98f,.94f,.93f),6);
        chatHistory=history.gameObject;
        var viewport=IceUI.Rect(history,"Viewport",Vector2.zero,new Vector2(430,150)); viewport.gameObject.AddComponent<RectMask2D>();
        chatLines=IceUI.Label(viewport,"Lines","",Vector2.zero,new Vector2(430,150),19,IceUI.White,TextAlignmentOptions.TopLeft);
        chatLines.rectTransform.anchorMin=new Vector2(0,1); chatLines.rectTransform.anchorMax=Vector2.one; chatLines.rectTransform.pivot=new Vector2(.5f,1);
        chatLines.rectTransform.sizeDelta=new Vector2(0,150);
        chatLines.textWrappingMode=TextWrappingModes.Normal;
        chatScroll=history.gameObject.AddComponent<ScrollRect>(); chatScroll.viewport=viewport; chatScroll.content=chatLines.rectTransform;
        chatScroll.horizontal=false; chatScroll.vertical=true; chatScroll.movementType=ScrollRect.MovementType.Clamped;
        chatBox=IceUI.Rect(root,"Compose",new Vector2(0,-92),new Vector2(470,76)).gameObject;
        chatField=IceUI.FieldAt(chatBox.transform,"ChatInput","메시지 입력 · Enter 전송",new Vector2(-42,0),new Vector2(380,64),120);
        var send=IceUI.ButtonAt(chatBox.transform,"Send","전송",new Vector2(192,0),new Vector2(88,64),IceUI.Mint,true);
        send.onClick.AddListener(SubmitChat); chatField.onSubmit.AddListener(_=>SubmitChat()); chatBox.SetActive(false);
    }
    public void OpenSettings(bool open)
    {
        CloseWheel(false); CloseChat(); capturing=-1; settings.SetActive(open);
        GamePreferences.ModalInput=open;
        if (open) settings.transform.SetAsLastSibling();
        EventSystem.current?.SetSelectedGameObject(null);
    }
    public void BeginBinding(int index)
    {
        if (!SettingsOpen) return;
        capturing=index; captureFrame=Time.frameCount;
        bindingHint.text=GamePreferences.Labels[index]+"에 사용할 키를 누르세요. Esc는 취소입니다.";
    }
    void RefreshPreferences()
    {
        GamePreferences.RefreshButton(emoteAction,7); GamePreferences.RefreshButton(chatAction,8);
        for (int i=0;i<bindingButtons.Length;i++) bindingButtons[i].GetComponentInChildren<TMP_Text>().text=GamePreferences.KeyLabel(i);
        anonymity.text="익명 표시  "+(GamePreferences.Anonymous?"켜짐":"꺼짐");
        chatToggle.text="채팅 표시  "+(GamePreferences.ChatVisible?"켜짐":"꺼짐");
        mapSeconds.SetTextWithoutNotify(GamePreferences.MapSelectionSeconds.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture));
        controlHint.text=$"{GamePreferences.KeyLabel(0)}/{GamePreferences.KeyLabel(1)}/{GamePreferences.KeyLabel(2)}/{GamePreferences.KeyLabel(3)} 이동   {GamePreferences.KeyLabel(4)} 점프   {GamePreferences.KeyLabel(5)} 다이브   {GamePreferences.KeyLabel(6)} 밀치기   {GamePreferences.KeyLabel(7)} 감정표현";
        if (!OnlineGameSession.InRoom && front.session.manager.player != null)
        {
            var player=front.session.manager.player;
            player.GetComponent<PlayerNameplate>()?.SetName(GamePreferences.PublicName((int)player.GetComponent<PlayerSkin>().skin),true,false);
            foreach (var score in SoloGameSeries.Scores) if (score.actorId==1) score.nickname=GamePreferences.PublicName((int)player.GetComponent<PlayerSkin>().skin);
        }
        if (!GamePreferences.ChatVisible) CloseChat();
    }
    void SaveMapSeconds(string text)
    {
        if(float.TryParse(text.Replace(',','.'),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out float seconds)
            && !float.IsNaN(seconds) && !float.IsInfinity(seconds))
        {
            GamePreferences.SetMapSelectionSeconds(seconds);
            bindingHint.text="맵 선택 시간을 저장했어요. 다음 맵 선택부터 적용됩니다. (1~15초)";
        }
        else { RefreshPreferences(); bindingHint.text="맵 선택 시간은 1~15초 숫자로 입력하세요."; }
    }
    void Update()
    {
        if (front == null || overlay == null) return;
        backdrop.SetActive(front.modesPanel.activeSelf || front.lobbyPanel.activeSelf || front.resultsPanel.activeSelf);
        bool inRoom=OnlineGameSession.InRoom;
        if (GameHUD.Instance != null && GameHUD.Instance.controlsGroup != null && GameHUD.Instance.controlsGroup.activeSelf) GameHUD.Instance.controlsGroup.SetActive(false);
        bool showControls=front.session.manager.State==GameManager.GameState.Playing && !front.session.roulette.selectionCanvas.gameObject.activeSelf && !SettingsOpen && !wheelOpen && !ChatOpen;
        controlHint.gameObject.SetActive(showControls); controlsBackdrop.SetActive(showControls);
        if (!inRoom && wasInRoom) { messages.Clear(); chatLines.text=""; CloseChat(); }
        wasInRoom=inRoom;
        chatHistory.SetActive(inRoom && GamePreferences.ChatVisible && messages.Count>0 && !SettingsOpen);
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (capturing>=0) { capturing=-1; bindingHint.text="키 변경을 취소했어요."; }
            else if (SettingsOpen) OpenSettings(false);
            else if (wheelOpen) CloseWheel(false);
            else CloseChat();
            return;
        }
        if (capturing>=0 && Time.frameCount>captureFrame)
        {
            string path=null;
            if (Keyboard.current != null) foreach (var key in Keyboard.current.allKeys) if (key.wasPressedThisFrame) { path="<Keyboard>/"+key.name; break; }
            if (Mouse.current != null)
            { if (Mouse.current.leftButton.wasPressedThisFrame) path="<Mouse>/leftButton"; else if (Mouse.current.rightButton.wasPressedThisFrame) path="<Mouse>/rightButton"; }
            if (path != null)
            {
                if (GamePreferences.SetBinding(capturing,path)) { capturing=-1; bindingHint.text="저장했어요. 다음 조작부터 바로 적용됩니다."; }
                else bindingHint.text="이미 사용하는 키입니다. 다른 키를 누르세요.";
            }
            return;
        }
        if (SettingsOpen) return;
        if (chatAction.WasPressedThisFrame() && inRoom && GamePreferences.ChatVisible && !GamePreferences.InputBlocked)
        { chatBox.SetActive(true); chatField.Select(); chatField.ActivateInputField(); return; }
        if (emoteAction.WasPressedThisFrame() && CanEmote() && !GamePreferences.InputBlocked)
        {
            wheelOpen=true; wheel.SetActive(true); selectedEmote=-1;
            dragOrigin=Mouse.current != null?Mouse.current.position.ReadValue():new Vector2(Screen.width*.5f,Screen.height*.5f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay,dragOrigin,null,out var point);
            ((RectTransform)wheel.transform).anchoredPosition=new Vector2(Mathf.Clamp(point.x,overlay.rect.xMin+310,overlay.rect.xMax-310),Mathf.Clamp(point.y,overlay.rect.yMin+340,overlay.rect.yMax-340));
            GamePreferences.ModalInput=true;
        }
        if (wheelOpen)
        {
            Vector2 delta=Mouse.current != null?Mouse.current.position.ReadValue()-dragOrigin:Vector2.zero;
            selectedEmote=SelectEmote(delta,Mathf.Min(Screen.width,Screen.height)*.045f);
            for (int i=0;i<6;i++) wheelIcons[i].transform.localScale=Vector3.one*(selectedEmote==i?1.2f:1);
            wheelHint.text=selectedEmote>=0?IceEmoteIcon.Names[selectedEmote]:"마우스를 끌어\n감정을 선택하세요";
            if (!CanEmote()) CloseWheel(false);
            else if (emoteAction.WasReleasedThisFrame()) CloseWheel(true);
        }
    }
    public static int SelectEmote(Vector2 delta,float deadzone=40)
    { if (delta.magnitude<deadzone) return -1; return Mathf.RoundToInt(Mathf.Repeat(90-Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg,360)/60)%6; }
    bool CanEmote() => front.session.manager.player != null && front.session.manager.player.gameObject.activeSelf
        && (OnlineGameSession.InRoom?OnlineGameSession.MatchPlaying:front.session.manager.State==GameManager.GameState.Playing);
    void CloseWheel(bool submit)
    {
        if (!wheelOpen) return;
        wheelOpen=false; wheel.SetActive(false); GamePreferences.ModalInput=SettingsOpen;
        if (!submit || selectedEmote<0 || !CanEmote()) return;
        if (OnlineGameSession.InRoom)
        { if (front.session.SendEmote(selectedEmote)) PlayerEmoteBubble.Display(front.session.manager.player,selectedEmote); }
        else if (Time.unscaledTime>=nextSoloEmote)
        { nextSoloEmote=Time.unscaledTime+1.2f; PlayerEmoteBubble.Display(front.session.manager.player,selectedEmote); }
    }
    void ReceiveChat(string author,string text)
    {
        messages.Enqueue(author+"  ·  "+text); while (messages.Count>6) messages.Dequeue();
        chatLines.text=string.Join("\n",messages); // 새 메시지가 올 때만 갱신한다.
        float height=Mathf.Max(150,chatLines.GetPreferredValues(chatLines.text,430,float.PositiveInfinity).y+12);
        chatLines.rectTransform.sizeDelta=new Vector2(0,height); scrollToLatest=true;
    }
    void SubmitChat()
    {
        if (front.session.SendChat(chatField.text)) { chatField.text=""; CloseChat(); }
    }
    void LateUpdate()
    { if (scrollToLatest && chatScroll != null) { chatScroll.verticalNormalizedPosition=0; scrollToLatest=false; } }
    void CloseChat()
    {
        if (chatBox == null) return;
        chatField.DeactivateInputField(); chatBox.SetActive(false);
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject==chatField.gameObject) EventSystem.current.SetSelectedGameObject(null);
    }
    void OnDestroy()
    {
        if (Instance==this) { Instance=null; GamePreferences.ModalInput=false; }
        GamePreferences.Changed-=RefreshPreferences;
        if (front != null && front.session != null) { front.session.ChatReceived-=ReceiveChat; front.session.EmoteReceived-=PlayerEmoteBubble.Display; }
        emoteAction?.Dispose(); chatAction?.Dispose();
        if (overlay != null) Destroy(overlay.gameObject);
    }
}
