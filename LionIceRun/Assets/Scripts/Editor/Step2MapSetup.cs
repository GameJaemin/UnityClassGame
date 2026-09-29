using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Step 2: 얼음 맵에 필요한 머티리얼, 프리팹, 씬 오브젝트를 한 번에 만든다.
// 메뉴: LionIceRun > Step 2 맵 세팅 (여러 번 실행해도 기존 에셋은 그대로 재사용)
public static class Step2MapSetup
{
    const string IceMaterialPath = "Assets/Materials/Ice.mat";
    const string SeaMaterialPath = "Assets/Materials/Sea.mat";
    const string BlockPrefabPath = "Assets/Prefabs/IceBlock.prefab";

    [MenuItem("LionIceRun/Step 2 맵 세팅")]
    static void Run()
    {
        Material ice = GetOrCreateMaterial(IceMaterialPath, new Color(0.72f, 0.9f, 0.98f), 0.85f);
        Material seaMaterial = GetOrCreateMaterial(SeaMaterialPath, new Color(0.13f, 0.42f, 0.65f), 0.95f);
        GameObject blockPrefab = GetOrCreateBlockPrefab(ice);

        IceGrid grid = Object.FindAnyObjectByType<IceGrid>();
        if (grid == null) grid = new GameObject("IceGrid").AddComponent<IceGrid>();
        grid.blockPrefab = blockPrefab;

        if (grid.sea == null)
        {
            GameObject sea = GameObject.CreatePrimitive(PrimitiveType.Plane);
            sea.name = "Sea";
            Object.DestroyImmediate(sea.GetComponent<Collider>());
            sea.GetComponent<Renderer>().sharedMaterial = seaMaterial;
            grid.sea = sea.transform;
        }
        grid.Build();

        SetUpCamera(grid);
        SetUpLighting();

        EditorSceneManager.MarkSceneDirty(grid.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();
        Selection.activeGameObject = grid.gameObject;
        Debug.Log("Step 2 맵 세팅 완료: IceGrid, Sea, 카메라, 조명을 설정하고 씬을 저장했습니다.");
    }

    static Material GetOrCreateMaterial(string path, Color color, float smoothness)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;

        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", smoothness);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    static GameObject GetOrCreateBlockPrefab(Material material)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BlockPrefabPath);
        if (prefab != null) return prefab;

        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        temp.name = "IceBlock";
        temp.GetComponent<Renderer>().sharedMaterial = material;
        prefab = PrefabUtility.SaveAsPrefabAsset(temp, BlockPrefabPath);
        Object.DestroyImmediate(temp);
        return prefab;
    }

    static void SetUpCamera(IceGrid grid)
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("Step 2: Main Camera를 찾지 못해 카메라 설정을 건너뜁니다.");
            return;
        }

        cam.fieldOfView = 35f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.62f, 0.85f, 0.97f);

        QuarterViewCamera quarterView = cam.GetComponent<QuarterViewCamera>();
        if (quarterView == null) quarterView = cam.gameObject.AddComponent<QuarterViewCamera>();
        quarterView.grid = grid;
        quarterView.Fit();
    }

    static void SetUpLighting()
    {
        foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.type != LightType.Directional) continue;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.intensity = 1.3f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
            break;
        }

        // 폴가이즈처럼 그림자 쪽도 밝은 파스텔 톤으로
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.75f, 0.88f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.85f, 0.9f, 0.95f);
        RenderSettings.ambientGroundColor = new Color(0.55f, 0.65f, 0.75f);
    }
}
