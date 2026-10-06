using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// 시련·보상: 시련 4개(눈덩이, 찬바람, 고드름, 펭귄)를 GameSystems에 붙이고 플레이어에 파워업 상태를 붙인다.
//  - 재질: 예고 그림자·붉은 원(반투명), 눈덩이, 방패 거품(반투명)
//  - 펭귄: Blender에서 내보낸 Assets/Models/Hazards/Penguin.fbx를 가져오고 Ready/Slide/Exit 애니메이터를 만든다
// 파워업 아이템(얼음 복구·방패·슈퍼 점프·얼음 굳히기)은 기존 RepairItemSpawner가 낸다.
// 메뉴: LionIceRun > 시련·보상 세팅 (UI의 안내 문구·파워업 표시는 Step 7 UI 세팅을 다시 실행하면 생김)
public static class StepTrialSetup
{
    const string HazardFolder = "Assets/Models/Hazards";
    const string PenguinControllerPath = "Assets/Animations/Penguin.controller";

    [MenuItem("LionIceRun/바람·아이템 밸런스 적용")]
    static void ApplyBalance()
    {
        var wind = Object.FindAnyObjectByType<WindTrial>(FindObjectsInactive.Include);
        var spawner = Object.FindAnyObjectByType<RepairItemSpawner>(FindObjectsInactive.Include);
        if (wind == null || spawner == null)
        {
            Debug.LogError("바람·아이템: WindTrial 또는 RepairItemSpawner가 없습니다. 시련·보상 세팅과 Step 5 생존 시간 세팅을 먼저 실행하세요.");
            return;
        }

        Undo.RecordObjects(new Object[] { wind, spawner }, "바람·아이템 밸런스 적용");
        wind.strength = 9f;
        spawner.firstSpawnTime = 6f;
        spawner.interval = 7f;
        EditorUtility.SetDirty(wind);
        EditorUtility.SetDirty(spawner);
        EditorSceneManager.MarkSceneDirty(wind.gameObject.scene);
        EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();
        Selection.activeGameObject = wind.gameObject;
        Debug.Log("바람·아이템 밸런스 적용 완료: 바람 힘 9, 아이템 첫 등장 6초, 획득·소멸 후 다음 등장 7초.");
    }

    [MenuItem("LionIceRun/바람·아이템 밸런스 적용", true)]
    static bool CanApplyBalance() => !EditorApplication.isPlayingOrWillChangePlaymode;

    [MenuItem("LionIceRun/시련·보상 세팅")]
    static void Run()
    {
        PlayerController player = null;
        foreach (PlayerController candidate in Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            if (!candidate.useExternalInput) player = candidate;
        IceGrid grid = Object.FindAnyObjectByType<IceGrid>();
        GameManager manager = Object.FindAnyObjectByType<GameManager>();
        if (player == null || grid == null || manager == null)
        {
            Debug.LogError("시련·보상: Player, IceGrid, GameManager 중 없는 것이 있습니다. Step 2, 3, 6 메뉴를 먼저 실행하세요.");
            return;
        }

        Material shadow = GetOrCreateMaterial("Assets/Materials/TrialShadow.mat", "Universal Render Pipeline/Unlit", new Color(0.05f, 0.08f, 0.15f, 0.45f), true);
        Material danger = GetOrCreateMaterial("Assets/Materials/TrialDanger.mat", "Universal Render Pipeline/Unlit", new Color(1f, 0.2f, 0.25f, 0.55f), true);
        Material snow = GetOrCreateMaterial("Assets/Materials/Snow.mat", "Universal Render Pipeline/Lit", new Color(0.97f, 0.98f, 1f, 1f), false);
        Material shieldMaterial = GetOrCreateMaterial("Assets/Materials/Shield.mat", "Universal Render Pipeline/Lit", new Color(0.55f, 0.8f, 1f, 0.28f), true);
        shieldMaterial.SetFloat("_Smoothness", 0.95f);
        Material star = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/RepairItem.mat");
        Material ice = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Ice.mat");

        // 펭귄
        GameObject penguinModel = null;
        RuntimeAnimatorController penguinController = null;
        if (AssetDatabase.LoadAssetAtPath<GameObject>($"{HazardFolder}/Penguin.fbx") != null)
        {
            Step8CharacterSetup.ConfigureModel(HazardFolder, "Penguin", new[] { "Ready", "Slide" });
            penguinModel = AssetDatabase.LoadAssetAtPath<GameObject>($"{HazardFolder}/Penguin.fbx");
            penguinController = CreatePenguinController();
        }
        else Debug.LogWarning("시련·보상: Assets/Models/Hazards/Penguin.fbx가 없어 펭귄 시련은 건너뜁니다. Tools/blender/build_all.ps1을 실행하세요.");

        // 시련 (처음 붙일 때 각 시련의 Reset()이 등장 시간·간격 기본값을 넣는다)
        GameObject systems = manager.gameObject;
        var snowball = GetOrAdd<SnowballTrial>(systems);
        Link(snowball, grid, player);
        snowball.shadowMaterial = shadow;
        snowball.snowMaterial = snow;
        snowball.starMaterial = star;

        var wind = GetOrAdd<WindTrial>(systems);
        Link(wind, grid, player);

        var icicle = GetOrAdd<IcicleTrial>(systems);
        Link(icicle, grid, player);
        icicle.dangerMaterial = danger;
        icicle.iceMaterial = ice;
        icicle.starMaterial = star;

        var penguin = GetOrAdd<PenguinTrial>(systems);
        Link(penguin, grid, player);
        penguin.dangerMaterial = danger;
        penguin.penguinModel = penguinModel;
        penguin.penguinAnimator = penguinController;

        // 보상: 플레이어 파워업 상태
        var powerUps = GetOrAdd<PlayerPowerUps>(player.gameObject);
        powerUps.shieldMaterial = shieldMaterial;
        GameHUD hud = Object.FindAnyObjectByType<GameHUD>();
        if (hud != null) hud.playerPowerUps = powerUps;

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(systems.scene);
        EditorSceneManager.SaveOpenScenes();
        Selection.activeGameObject = systems;
        Debug.Log("시련·보상 세팅 완료: 눈덩이(15초~), 찬바람(30초~), 고드름(45초~), 펭귄(60초~)과 파워업 상태를 붙였습니다. " +
                  "안내 문구·파워업 표시가 안 보이면 LionIceRun > Step 7 UI 세팅을 다시 실행하세요.");
    }

    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        return component != null ? component : go.AddComponent<T>();
    }

    static void Link(TrialBase trial, IceGrid grid, PlayerController player)
    {
        trial.grid = grid;
        trial.player = player;
        EditorUtility.SetDirty(trial);
    }

    static Material GetOrCreateMaterial(string path, string shaderName, Color color, bool transparent)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find(shaderName));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", color);
        if (transparent)
        {
            // URP 반투명 (알파 블렌드, 깊이 쓰기 끔)
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.SetOverrideTag("RenderType", "Transparent");
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    // 상태만 있는 애니메이터 (전환은 PenguinTrial이 Animator.Play로 직접 바꾼다)
    static RuntimeAnimatorController CreatePenguinController()
    {
        var clips = AssetDatabase.LoadAllAssetsAtPath($"{HazardFolder}/Penguin.fbx")
            .OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__"))
            .ToDictionary(c => c.name);
        if (!clips.ContainsKey("Ready") || !clips.ContainsKey("Slide") || !clips.ContainsKey("Exit"))
        {
            Debug.LogError("시련·보상: Penguin.fbx에 Ready/Slide/Exit 클립이 없습니다.");
            return null;
        }
        if (!AssetDatabase.IsValidFolder("Assets/Animations")) AssetDatabase.CreateFolder("Assets", "Animations");
        AssetDatabase.DeleteAsset(PenguinControllerPath);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(PenguinControllerPath);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        foreach (string name in new[] { "Ready", "Slide", "Exit" })
        {
            AnimatorState state = machine.AddState(name);
            state.motion = clips[name];
            if (name == "Ready") machine.defaultState = state;
        }
        return controller;
    }
}
