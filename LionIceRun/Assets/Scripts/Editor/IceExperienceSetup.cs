using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 선택 화면의 새 디자인을 편집기에서도 미리 볼 수 있다. 플레이 시 기능 UI는 자동 적용한다.
public static class IceExperienceSetup
{
    const string DesignVersion="20261007-ice-game-ui";
    [InitializeOnLoadMethod]
    static void RegisterAutoApply()
    {
        EditorApplication.delayCall+=AutoApply;
        EditorSceneManager.sceneOpened+=(_,__)=>EditorApplication.delayCall+=AutoApply;
        EditorApplication.playModeStateChanged+=state=> { if(state==PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall+=AutoApply; };
    }
    static void AutoApply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) return;
        if(EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall+=AutoApply; return; }
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/Main.unity" || SessionState.GetString("LionIceRun.ExperienceDesign","")==DesignVersion) return;
        var front=Object.FindAnyObjectByType<OnlineFrontend>(FindObjectsInactive.Include);
        if(front==null) return;
        bool wasDirty=scene.isDirty;
        ApplyDesign(front);
        SessionState.SetString("LionIceRun.ExperienceDesign",DesignVersion);
        // 편집 중이던 다른 변경은 자동 저장하지 않는다.
        if(!wasDirty) EditorSceneManager.SaveScene(scene);
        Debug.Log("새 UI를 편집 화면에 적용했습니다. Play 후 우측 설정에서 맵 선택 시간(1~15초)을 입력할 수 있습니다.");
    }
    static void ApplyDesign(OnlineFrontend front)
    {
        Undo.RegisterFullObjectHierarchyUndo(front.gameObject,"새 UI 디자인");
        var experience=front.GetComponent<IceExperienceUI>() ?? Undo.AddComponent<IceExperienceUI>(front.gameObject);
        experience.front=front; EditorUtility.SetDirty(experience);
        IceExperienceUI.Restyle(front); front.OpenModes();
        EditorSceneManager.MarkSceneDirty(front.gameObject.scene);
    }
    [MenuItem("LionIceRun/새 UI 디자인 적용·미리보기")]
    public static void Apply()
    {
        var front=Object.FindAnyObjectByType<OnlineFrontend>(FindObjectsInactive.Include);
        if (front==null) return;
        ApplyDesign(front);
        EditorSceneManager.SaveScene(front.gameObject.scene);
        Debug.Log("새 UI 디자인 저장 완료. 설정·감정표현·채팅은 플레이 시작 시 자동 연결됩니다. 빌드는 생성하지 않았습니다.");
    }
    [MenuItem("LionIceRun/새 UI 디자인 적용·미리보기",true)]
    static bool Allowed()=>!EditorApplication.isPlayingOrWillChangePlaymode;
}
