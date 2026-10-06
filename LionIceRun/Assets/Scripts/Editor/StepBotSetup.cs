using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// AI 봇: GameSystems에 BotManager를 붙인다. 봇은 게임 시작 때 코드로 만들어진다 (플레이어와 같은 부품 + BotBrain).
// 메뉴: LionIceRun > AI 봇 세팅
public static class StepBotSetup
{
    [MenuItem("LionIceRun/AI 봇 세팅")]
    static void Run()
    {
        PlayerController player = null;
        foreach (PlayerController candidate in Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            if (!candidate.useExternalInput) player = candidate;
        IceGrid grid = Object.FindAnyObjectByType<IceGrid>();
        GameManager manager = Object.FindAnyObjectByType<GameManager>();
        if (player == null || grid == null || manager == null)
        {
            Debug.LogError("AI 봇: Player, IceGrid, GameManager 중 없는 것이 있습니다. Step 2, 3, 6 메뉴를 먼저 실행하세요.");
            return;
        }
        if (player.GetComponent<PlayerSkin>() == null)
            Debug.LogWarning("AI 봇: Player에 PlayerSkin이 없어 봇이 캡슐 없이 보이지 않을 수 있습니다. Step 8 캐릭터 가져오기를 먼저 실행하세요.");

        BotManager bots = manager.GetComponent<BotManager>();
        if (bots == null) bots = manager.gameObject.AddComponent<BotManager>();
        bots.grid = grid;
        bots.player = player;

        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();
        Selection.activeGameObject = manager.gameObject;
        Debug.Log($"AI 봇 세팅 완료: 봇 {bots.botCount}마리 (신중형 {bots.CautiousCount}, 공격형 {bots.AggressiveCount}). Bot Count는 전체 수, Aggressive Bot Count는 공격형 수입니다. 지정 시작 위치 순서로 신중형부터 배치합니다.");
    }
}
