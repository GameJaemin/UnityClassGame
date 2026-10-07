using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class ExperienceEntry
{
    static ExperienceEntry()
    {
        EditorApplication.playModeStateChanged+=state=>{
            if (state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("ExperienceTest",false)) return;
            SessionState.EraseBool("ExperienceTest"); new GameObject("ExperienceTest").AddComponent<ExperienceRunner>();
        };
    }
    public static void Run()
    {
        OnlineMultiplayerTests.Run(); OnlineSeriesTests.Run();
        PlayerSettings.companyName="CodexTests"; PlayerSettings.productName="ExperienceUI";
        EditorSettings.enterPlayModeOptionsEnabled=false;
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        IceExperienceSetup.Apply();
        var front=Object.FindAnyObjectByType<OnlineFrontend>(FindObjectsInactive.Include);
        if(front.GetComponent<IceExperienceUI>()?.front!=front || front.modesPanel.transform.Find("Title").GetComponent<TMPro.TMP_Text>().text!="플레이")
            throw new System.Exception("편집 화면 UI 적용/컴포넌트 저장 실패");
        Debug.Log("EXPERIENCE_EDITOR_SETUP_PASSED");
        MapRoulette.SkipNextSelection=false;
        SessionState.SetBool("ExperienceTest",true); EditorApplication.EnterPlaymode();
    }
}
