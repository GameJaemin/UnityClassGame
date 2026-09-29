using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Step 6: 게임 상태 관리. GameSystems 오브젝트에 GameManager를 붙이고 연결한다.
// 메뉴: LionIceRun > Step 6 게임 매니저 세팅
public static class Step6GameManagerSetup
{
    [MenuItem("LionIceRun/Step 6 게임 매니저 세팅")]
    static void Run()
    {
        PlayerController player = Object.FindAnyObjectByType<PlayerController>();
        IceGrid grid = Object.FindAnyObjectByType<IceGrid>();
        SurvivalTimer timer = Object.FindAnyObjectByType<SurvivalTimer>();
        if (player == null || grid == null || timer == null)
        {
            Debug.LogError("Step 6: Player, IceGrid, SurvivalTimer 중 없는 것이 있습니다. Step 2, 3, 5 메뉴를 먼저 실행하세요.");
            return;
        }

        GameManager manager = timer.GetComponent<GameManager>();
        if (manager == null) manager = timer.gameObject.AddComponent<GameManager>();
        manager.player = player;
        manager.grid = grid;
        manager.timer = timer;

        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();
        Selection.activeGameObject = manager.gameObject;
        Debug.Log("Step 6 게임 매니저 세팅 완료: GameSystems에 GameManager를 붙였습니다.");
    }
}
