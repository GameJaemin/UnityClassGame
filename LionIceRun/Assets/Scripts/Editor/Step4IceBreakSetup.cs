using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Step 4: 얼음 깨짐. Player에 IceStepTracker를 붙인다.
// 블럭의 IceBlock은 게임 시작 시 IceGrid가 자동으로 붙이므로 프리팹은 그대로 둔다.
// 메뉴: LionIceRun > Step 4 얼음 깨짐 세팅
public static class Step4IceBreakSetup
{
    [MenuItem("LionIceRun/Step 4 얼음 깨짐 세팅")]
    static void Run()
    {
        PlayerController player = Object.FindAnyObjectByType<PlayerController>();
        IceGrid grid = Object.FindAnyObjectByType<IceGrid>();
        if (player == null || grid == null)
        {
            Debug.LogError("Step 4: Player 또는 IceGrid가 씬에 없습니다. Step 2, Step 3 메뉴를 먼저 실행하세요.");
            return;
        }

        IceStepTracker tracker = player.GetComponent<IceStepTracker>();
        if (tracker == null) tracker = player.gameObject.AddComponent<IceStepTracker>();
        tracker.grid = grid;

        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();
        Selection.activeGameObject = grid.gameObject;
        Debug.Log("Step 4 얼음 깨짐 세팅 완료: Player에 IceStepTracker를 붙였습니다. 깨짐 수치는 IceGrid Inspector의 '얼음 깨짐'에서 조정합니다.");
    }
}
