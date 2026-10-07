using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

public class ExperienceRunner : MonoBehaviour
{
    const string Folder="D:/UnityClassGame/Tools/tests/experience-ui/";
    int checks,failures;
    void Check(bool pass,string name)
    { checks++; if(!pass) failures++; File.AppendAllText(Folder+"results.txt",(pass?"PASS ":"FAIL ")+name+"\n"); }
    IEnumerator Start()
    {
        File.WriteAllText(Folder+"results.txt","");
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.updateMode=InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
        yield return null; yield return null;
        var ui=IceExperienceUI.Instance; var front=ui.front;
        var gm=front.session.manager; var player=gm.player;
        PlayerPrefs.DeleteKey("LionIceRun.MapSelectionSpeed"); PlayerPrefs.DeleteKey("LionIceRun.MapSelectionSeconds"); GamePreferences.Apply();
        Check(front.session.roulette.titleGroup.transform.Find("SettingsButton")!=null && ui.transform.root.Find("IceExperienceOverlay/SettingsButton")==null,"설정 버튼은 타이틀에 배치");
        GamePreferences.ResetBindings();
        Check(GamePreferences.Path(5)=="<Keyboard>/leftShift","다이브 기본 Shift");
        Check(GamePreferences.SetBinding(5,"<Keyboard>/e"),"다이브 키 변경");
        Check(PlayerPrefs.GetString("LionIceRun.Key.Dive")=="<Keyboard>/e","키 저장");
        Check(!GamePreferences.SetBinding(5,"<Keyboard>/w"),"중복 키 거부");
        Check(!GamePreferences.SetBinding(5,"<Keyboard>/escape"),"설정 닫기 키 보호");
        Check(GamePreferences.SetBinding(0,"<Keyboard>/upArrow"),"이동 키 변경");
        var move=InputSystem.actions.FindAction("Player/Move"); bool overridden=false;
        foreach(var binding in move.bindings) if(binding.name=="up" && binding.effectivePath=="<Keyboard>/upArrow") overridden=true;
        Check(overridden,"실제 이동 액션 반영");
        PlayerPrefs.DeleteKey("LionIceRun.Nickname"); GamePreferences.SetAnonymous(false);
        Check(GamePreferences.Nickname=="","저장한 이름이 없으면 기본 빈 닉네임");
        front.nickname.text=" \t\n<> ";
        typeof(OnlineFrontend).GetMethod("SaveName",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(front,null);
        Check(front.nickname.text=="" && PlayerPrefs.GetString("LionIceRun.Nickname")=="","빈 닉네임 저장 후 입력창도 빈칸 유지");
        for(int i=0;i<3;i++) Check(GamePreferences.PublicName(i)==GamePreferences.Animal(i),"빈 이름은 선택 동물로 참가 "+i);
        Check(player.GetComponent<PlayerNameplate>().label.text.Contains(GamePreferences.Animal((int)player.GetComponent<PlayerSkin>().skin)),"빈 이름 솔로 이름표는 동물 이름");
        PlayerPrefs.SetString("LionIceRun.Nickname","한글 닉네임"); GamePreferences.SetAnonymous(false);
        Check(GamePreferences.PublicName(1)=="한글 닉네임","한글 이름 유지");
        GamePreferences.SetAnonymous(true); Check(GamePreferences.PublicName(1)=="북극곰","익명 동물 이름");
        Check(!GamePreferences.PublicName(1).Contains("닉네임"),"익명에 원래 이름 미포함");
        GamePreferences.SetAnonymous(false);
        Check(player.GetComponent<PlayerNameplate>().label.text.Contains("한글 닉네임"),"솔로 플레이어 이름표");
        Check(OnlineGameSession.CleanChat("  안녕\n<친구>  ")=="안녕친구","한글 채팅 정리");
        Check(OnlineGameSession.CleanChat(new string('가',200)).Length==120,"채팅 길이 제한");
        for(int i=0;i<6;i++)
        {
            float angle=(90-i*60)*Mathf.Deg2Rad;
            Check(IceExperienceUI.SelectEmote(new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*150)==i,"감정 원형 선택 "+i);
        }
        Check(IceExperienceUI.SelectEmote(Vector2.one)==-1,"감정표현 가운데 취소");
        front.OpenModes(); front.nickname.text="한글 닉네임"; front.notice.text="친구와 함께 다섯 라운드를 달려요.";
        yield return null;
        Check(front.nickname.GetComponent<WebTextInput>()!=null && front.nickname.characterValidation==TMP_InputField.CharacterValidation.None,"한글 입력창 브리지 연결");
        Check(front.soloButton.targetGraphic is IceUIShape && front.nickname.targetGraphic is IceUIShape,"이미지 없는 버튼/입력 표면");
        Capture("modes.png");
        ui.OpenSettings(true); yield return null;
        Check(GamePreferences.InputBlocked,"설정 중 경기 입력 차단"); Capture("settings.png");
        ui.OpenSettings(false); Check(!GamePreferences.InputBlocked,"설정 닫은 뒤 입력 복구");
        front.HidePanels(); front.session.roulette.enabled=false;
        foreach(var brain in Object.FindObjectsByType<BotBrain>(FindObjectsSortMode.None)) brain.enabled=false;
        foreach(var trial in Object.FindObjectsByType<TrialBase>(FindObjectsSortMode.None)) trial.enabled=false;
        gm.enabled=false; gm.BeginSelectedMatch(); player.useExternalInput=false;
        player.ResetMotion(gm.grid.CellTop(gm.grid.StartCell)+Vector3.up*1.02f);
        player.InputEnabled=true; var body=player.GetComponent<Rigidbody>(); body.isKinematic=false;
        EventSystem.current.SetSelectedGameObject(null);
        InputSystem.actions.Enable(); var keyboard=InputSystem.AddDevice<Keyboard>(); var mouse=InputSystem.AddDevice<Mouse>();
        GamePreferences.ResetBindings(); yield return new WaitForSeconds(.08f);
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftShift)); yield return new WaitForSeconds(.06f);
        Check(player.State==PlayerController.MoveState.Diving,"Shift 실제 다이브");
        InputSystem.QueueStateEvent(keyboard,new KeyboardState()); player.ResetMotion(gm.grid.CellTop(gm.grid.StartCell)+Vector3.up*1.02f);
        yield return new WaitForSeconds(.08f);
        ui.OpenSettings(true); InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftShift)); yield return new WaitForSeconds(.06f);
        Check(player.State==PlayerController.MoveState.Normal,"설정 중 Shift 다이브 차단");
        typeof(GameManager).GetProperty("State").SetValue(gm,GameManager.GameState.GameOver);
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.R)); yield return null; yield return null;
        Check(gm.State==GameManager.GameState.GameOver,"결과 화면 설정 중 R 재시작 차단");
        typeof(GameManager).GetProperty("State").SetValue(gm,GameManager.GameState.Playing);
        InputSystem.QueueStateEvent(keyboard,new KeyboardState()); ui.OpenSettings(false);
        yield return null;
        Vector2 wheelCenter=new Vector2(Screen.width*.5f,Screen.height*.5f);
        InputSystem.QueueStateEvent(mouse,new MouseState{position=wheelCenter});
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.T)); yield return null; yield return null;
        Check(ui.wheel.activeSelf && GamePreferences.ModalInput,"T 누름으로 휠 열기");
        InputSystem.QueueStateEvent(mouse,new MouseState{position=wheelCenter+Vector2.up*150}); yield return null; yield return null;
        Capture("emote-wheel.png");
        InputSystem.QueueStateEvent(keyboard,new KeyboardState()); yield return null; yield return null;
        Check(!ui.wheel.activeSelf && player.GetComponent<PlayerEmoteBubble>()!=null,"T 놓아서 감정표현/입력 복구");
        Check(!GamePreferences.ModalInput,"휠 닫기 입력 복구");
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        var history=(GameObject)typeof(IceExperienceUI).GetField("chatHistory",flags).GetValue(ui);
        var compose=ui.chatBox;
        var lines=(TMP_Text)typeof(IceExperienceUI).GetField("chatLines",flags).GetValue(ui);
        var receive=typeof(IceExperienceUI).GetMethod("ReceiveChat",flags);
        for(int i=0;i<8;i++) receive.Invoke(ui,new object[]{"북극곰",$"메시지{i}: "+new string('가',110)});
        history.SetActive(true); compose.SetActive(true); Canvas.ForceUpdateCanvases();
        EventSystem.current.SetSelectedGameObject(null);
        Check(GamePreferences.InputBlocked,"채팅창 포커스가 없어도 경기 입력 차단");
        Check(!lines.text.Contains("메시지0:") && !lines.text.Contains("메시지1:") && lines.text.Contains("메시지7:"),"채팅 최근 여섯 메시지 유지");
        var scroll=history.GetComponent<ScrollRect>();
        Check(scroll.content.rect.height>scroll.viewport.rect.height && scroll.viewport.GetComponent<RectMask2D>()!=null,"긴 채팅 스크롤과 영역 마스크");
        typeof(IceExperienceUI).GetMethod("LateUpdate",flags).Invoke(ui,null);
        Check(scroll.verticalNormalizedPosition<.01f,"새 메시지 하단 자동 스크롤");
        var dock=(RectTransform)history.transform.parent;
        bool fits=true;
        foreach(var child in new[]{(RectTransform)history.transform,(RectTransform)compose.transform})
        {
            var corners=new Vector3[4]; child.GetWorldCorners(corners);
            foreach(var corner in corners)
            {
                var local=dock.InverseTransformPoint(corner); var bounds=dock.rect;
                if(local.x<bounds.xMin-.1f || local.x>bounds.xMax+.1f || local.y<bounds.yMin-.1f || local.y>bounds.yMax+.1f) fits=false;
            }
        }
        Check(fits,"채팅 목록과 입력창이 화면 안쪽 도크에 배치");
        Capture("chat.png");
        typeof(IceExperienceUI).GetMethod("CloseChat",flags).Invoke(ui,null); history.SetActive(false);
        Check(!GamePreferences.InputBlocked,"채팅 닫은 뒤 입력 복구");
        PlayerPrefs.DeleteKey("LionIceRun.MapSelectionSpeed"); GamePreferences.Apply();
        PlayerPrefs.DeleteKey("LionIceRun.MapSelectionSeconds"); GamePreferences.Apply();
        Check(GamePreferences.MapSelectionSeconds==5,"맵 선택 기본 5초");
        var timeField=ui.settings.transform.Find("Card/MapSeconds").GetComponent<TMP_InputField>();
        timeField.onEndEdit.Invoke("3.5");
        Check(GamePreferences.MapSelectionSeconds==3.5f && PlayerPrefs.GetFloat("LionIceRun.MapSelectionSeconds")==3.5f,"맵 선택 시간 직접 입력과 저장");
        timeField.onEndEdit.Invoke("잘못된 값"); Check(GamePreferences.MapSelectionSeconds==3.5f,"잘못된 시간 입력 복원");
        timeField.onEndEdit.Invoke("0"); Check(GamePreferences.MapSelectionSeconds==1,"맵 선택 최소 1초");
        timeField.onEndEdit.Invoke("30"); Check(GamePreferences.MapSelectionSeconds==15,"맵 선택 최대 15초");
        GamePreferences.SetMapSelectionSpeed(0);
        var roulette=front.session.roulette; roulette.ShowTitle(); roulette.enabled=false;
        roulette.BeginChoosing();
        var rouletteFlags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        float stopAt=(float)typeof(MapRoulette).GetField("soloStopAt",rouletteFlags).GetValue(roulette);
        Check(Mathf.Abs(stopAt-Time.unscaledTime-.6f)<.01f && Mathf.Abs(roulette.stopDuration-1.1f)<.01f,"솔로 빠른 회전과 감속 시간 적용");
        GamePreferences.SetMapSelectionSpeed(2);
        roulette.StopChoosing();
        float brake=(float)typeof(MapRoulette).GetField("brakingDuration",rouletteFlags).GetValue(roulette);
        float hold=(float)typeof(MapRoulette).GetField("soloConfirmSeconds",rouletteFlags).GetValue(roulette);
        Check(Mathf.Abs(hold-.3f)<.01f && Mathf.Abs(.6f+brake+hold-2)<.01f,"선택 중 설정 변경은 다음 판부터 적용·선택 총 2초");
        roulette.ShowTitle(); GamePreferences.SetMapSelectionSpeed(0);
        PlayerPrefs.DeleteKey("LionIceRun.MapSelectionSpeed"); PlayerPrefs.DeleteKey("LionIceRun.MapSelectionSeconds"); GamePreferences.Apply();
        front.HidePanels(); roulette.ShowTitle(); yield return null; Capture("title.png");
        var ramp=GameObject.CreatePrimitive(PrimitiveType.Cube); ramp.name="경사 물리 검사";
        ramp.transform.position=new Vector3(100,20,100); ramp.transform.localScale=new Vector3(24,1,24);
        var surface=ramp.AddComponent<MapMovingSurface>();
        player.useExternalInput=true; player.InputEnabled=true;
        Vector3 down=new Vector3(-1,0,0); float[] travelled=new float[5];
        for(int i=0;i<5;i++)
        {
            ramp.transform.rotation=Quaternion.Euler(0,0,i==0?0:i==4?-18:18);
            surface.PreviousPosition=ramp.transform.position; surface.PreviousRotation=ramp.transform.rotation;
            player.localPrediction=i==4;
            player.SetExternalInput(Vector3.zero,false,false,false);
            player.ResetMotion(ramp.transform.position+Vector3.up*1.7f);
            Physics.SyncTransforms(); yield return new WaitForSeconds(.35f);
            Vector3 start=body.position;
            Vector3 input=i==0||i==2?-down:i==3?down:Vector3.zero;
            player.SetExternalInput(input,false,false,false);
            yield return new WaitForSeconds(.65f);
            travelled[i]=Vector3.Dot(body.position-start,i==0||i==2?-down:down);
            Check(player.IsGrounded,"경사 접지 유지 "+i);
        }
        Check(travelled[1]>.4f,"기울어진 발판에서 가만히 있어도 아래로 미끄러짐");
        Check(travelled[2]<travelled[0]*.65f,"오르막 이동은 평지보다 느림");
        Check(travelled[3]>Mathf.Max(.5f,travelled[2]+.7f),"내리막 이동이 오르막보다 빠름");
        Check(travelled[4]<-.4f,"반대 경사·클라이언트 예측도 반대 방향 미끄러짐");
        File.WriteAllText(Folder+"slope-motion.txt",string.Join(",",travelled));
        Destroy(ramp); player.localPrediction=false;
        File.AppendAllText(Folder+"results.txt",$"RESULT checks={checks} failures={failures}\n");
        UnityEditor.EditorApplication.Exit(failures==0?0:1);
    }
    void Capture(string filename)
    {
        if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        var camera=Camera.main;
        var rt=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32);
        var uiCameraObject=new GameObject("UI Capture Camera",typeof(Camera));
        var uiCamera=uiCameraObject.GetComponent<Camera>(); uiCamera.CopyFrom(camera);
        uiCamera.transform.SetPositionAndRotation(camera.transform.position,camera.transform.rotation);
        uiCamera.cullingMask=1<<5; uiCamera.clearFlags=CameraClearFlags.Nothing; uiCamera.targetTexture=rt;
        uiCamera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
        int oldMask=camera.cullingMask; camera.cullingMask &= ~(1<<5);
        var canvases=Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        var changed=new System.Collections.Generic.List<Canvas>();
        var world=new System.Collections.Generic.List<Canvas>();
        foreach(var canvas in canvases)
        {
            if(canvas.renderMode==RenderMode.WorldSpace && canvas.enabled) { world.Add(canvas); canvas.enabled=false; }
            if(canvas.renderMode!=RenderMode.ScreenSpaceOverlay) continue;
            changed.Add(canvas);
            foreach(var child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer=5;
            canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=uiCamera; canvas.planeDistance=5;
        }
        IceExperienceUI.Instance.front.characterSelection.previewCamera.Render();
        camera.targetTexture=rt; Canvas.ForceUpdateCanvases(); camera.Render(); uiCamera.Render(); RenderTexture.active=rt;
        foreach(var shape in Object.FindObjectsByType<IceUIShape>(FindObjectsSortMode.None))
        {
            var mesh=shape.canvasRenderer.GetMesh();
            Debug.Log($"SURFACE {filename} {shape.name} rect={shape.rectTransform.rect} vertices={mesh?.vertexCount} culled={shape.canvasRenderer.cull} alpha={shape.color.a} bounds={mesh?.bounds}");
        }
        var texture=new Texture2D(1920,1080,TextureFormat.RGBA32,false);
        texture.ReadPixels(new Rect(0,0,1920,1080),0,0); texture.Apply();
        File.WriteAllBytes(Folder+filename,texture.EncodeToPNG());
        RenderTexture.active=null; camera.targetTexture=null; Destroy(texture); Destroy(rt);
        foreach(var canvas in changed) canvas.renderMode=RenderMode.ScreenSpaceOverlay;
        foreach(var canvas in world) canvas.enabled=true;
        camera.cullingMask=oldMask; Destroy(uiCameraObject);
    }
}
