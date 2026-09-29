using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Step 3: 임시 캡슐 플레이어를 만든다.
// 메뉴: LionIceRun > Step 3 플레이어 세팅 (여러 번 실행해도 기존 플레이어를 재사용)
public static class Step3PlayerSetup
{
    const string PlayerMaterialPath = "Assets/Materials/PlayerTemp.mat";
    const string PhysicsMaterialPath = "Assets/Materials/PlayerNoFriction.asset";

    [MenuItem("LionIceRun/Step 3 플레이어 세팅")]
    static void Run()
    {
        IceGrid grid = Object.FindAnyObjectByType<IceGrid>();
        if (grid == null)
        {
            Debug.LogError("Step 3: IceGrid가 씬에 없습니다. 먼저 LionIceRun > Step 2 맵 세팅을 실행하세요.");
            return;
        }

        PlayerController player = Object.FindAnyObjectByType<PlayerController>();
        if (player == null)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Player";
            go.tag = "Player";
            go.AddComponent<Rigidbody>();
            player = go.AddComponent<PlayerController>();
        }

        player.grid = grid;
        player.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial();
        player.GetComponent<CapsuleCollider>().sharedMaterial = GetOrCreatePhysicsMaterial();

        Rigidbody body = player.GetComponent<Rigidbody>();
        body.mass = 1f;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        player.transform.position = grid.CellTop(grid.StartCell) + Vector3.up * player.spawnHeight;

        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();
        Selection.activeGameObject = player.gameObject;
        Debug.Log("Step 3 플레이어 세팅 완료: Player를 시작 블럭 위에 배치하고 씬을 저장했습니다.");
    }

    static Material GetOrCreateMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(PlayerMaterialPath);
        if (material != null) return material;

        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetColor("_BaseColor", new Color(1f, 0.72f, 0.3f));
        material.SetFloat("_Smoothness", 0.4f);
        AssetDatabase.CreateAsset(material, PlayerMaterialPath);
        return material;
    }

    // 블럭 모서리에 몸이 걸려 붙지 않도록 마찰 0
    static PhysicsMaterial GetOrCreatePhysicsMaterial()
    {
        PhysicsMaterial material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(PhysicsMaterialPath);
        if (material != null) return material;

        material = new PhysicsMaterial("PlayerNoFriction")
        {
            dynamicFriction = 0f,
            staticFriction = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounciness = 0f,
            bounceCombine = PhysicsMaterialCombine.Minimum
        };
        AssetDatabase.CreateAsset(material, PhysicsMaterialPath);
        return material;
    }
}
