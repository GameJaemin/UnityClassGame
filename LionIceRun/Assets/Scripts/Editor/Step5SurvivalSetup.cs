using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Step 5: 생존 시간·최고 기록 + 얼음 복구 아이템.
//  - 씬에 GameSystems 오브젝트를 만들고 SurvivalTimer, RepairItemSpawner를 붙인다
//  - 얼음 복구 아이템 프리팹(빛나는 얼음 결정)과 재질을 만든다
// 메뉴: LionIceRun > Step 5 생존 시간 세팅
public static class Step5SurvivalSetup
{
    const string ItemMaterialPath = "Assets/Materials/RepairItem.mat";
    const string ItemPrefabPath = "Assets/Prefabs/IceRepairItem.prefab";

    [MenuItem("LionIceRun/Step 5 생존 시간 세팅")]
    static void Run()
    {
        PlayerController player = Object.FindAnyObjectByType<PlayerController>();
        IceGrid grid = Object.FindAnyObjectByType<IceGrid>();
        if (player == null || grid == null)
        {
            Debug.LogError("Step 5: Player 또는 IceGrid가 씬에 없습니다. Step 2, Step 3 메뉴를 먼저 실행하세요.");
            return;
        }

        RepairItem itemPrefab = GetOrCreateItemPrefab();

        SurvivalTimer timer = Object.FindAnyObjectByType<SurvivalTimer>();
        GameObject systems = timer != null ? timer.gameObject : GameObject.Find("GameSystems");
        if (systems == null) systems = new GameObject("GameSystems");
        if (timer == null) timer = systems.AddComponent<SurvivalTimer>();

        RepairItemSpawner spawner = systems.GetComponent<RepairItemSpawner>();
        if (spawner == null) spawner = systems.AddComponent<RepairItemSpawner>();
        spawner.grid = grid;
        spawner.player = player;
        spawner.itemPrefab = itemPrefab;

        EditorSceneManager.MarkSceneDirty(systems.scene);
        EditorSceneManager.SaveOpenScenes();
        Selection.activeGameObject = systems;
        Debug.Log("Step 5 생존 시간 세팅 완료: GameSystems(SurvivalTimer, RepairItemSpawner)와 얼음 복구 아이템 프리팹을 만들었습니다.");
    }

    static RepairItem GetOrCreateItemPrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<RepairItem>(ItemPrefabPath);
        if (existing != null) return existing;

        Material material = AssetDatabase.LoadAssetAtPath<Material>(ItemMaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", new Color(0.6f, 0.95f, 1f));
            material.SetFloat("_Smoothness", 0.9f);
            // 멀리서도 눈에 띄게 은은하게 빛남
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(0.25f, 0.75f, 1f) * 1.5f);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            AssetDatabase.CreateAsset(material, ItemMaterialPath);
        }

        // 루트: 먹는 판정(트리거) / 자식: 돌며 둥실거리는 얼음 결정 두 겹
        var root = new GameObject("IceRepairItem");
        var trigger = root.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 0.6f;

        var visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        AddCrystal(visual.transform, material, new Vector3(0.28f, 0.5f, 0.28f), new Vector3(0f, 45f, 0f));
        AddCrystal(visual.transform, material, new Vector3(0.18f, 0.32f, 0.18f), new Vector3(0f, 0f, 45f));

        RepairItem item = root.AddComponent<RepairItem>();
        item.visual = visual.transform;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ItemPrefabPath);
        Object.DestroyImmediate(root);
        return prefab.GetComponent<RepairItem>();
    }

    static void AddCrystal(Transform parent, Material material, Vector3 scale, Vector3 euler)
    {
        GameObject crystal = GameObject.CreatePrimitive(PrimitiveType.Cube);
        crystal.name = "Crystal";
        Object.DestroyImmediate(crystal.GetComponent<Collider>());
        crystal.transform.SetParent(parent, false);
        crystal.transform.localScale = scale;
        // 45도 기울여 마름모꼴 결정처럼
        crystal.transform.localRotation = Quaternion.Euler(45f, 0f, 45f) * Quaternion.Euler(euler);
        crystal.GetComponent<Renderer>().sharedMaterial = material;
    }
}
