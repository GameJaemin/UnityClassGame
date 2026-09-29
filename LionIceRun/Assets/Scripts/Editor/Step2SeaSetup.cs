using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Step 2 폴리싱: 입체적인 바다.
//  1) 바다를 약 200m로 키우고 잘게 나눈 파도용 메시(SeaSurface)로 바꾼다
//  2) Sea 재질을 StylizedSea 셰이더(파도, 잔물결, 깊이 색, 하늘 반사, 햇빛 반짝임, 물거품, 그림자, 먼 곳 흐려짐)로 바꾼다
//  3) 주변에 떠다니는 유빙(IceFloes)을 둔다
//  4) 12m 아래 수면까지 얼음판 그림자가 닿도록 그림자 거리를 늘린다
// 메뉴: LionIceRun > Step 2 바다 꾸미기
public static class Step2SeaSetup
{
    const string SeaMaterialPath = "Assets/Materials/Sea.mat";
    const string IceMaterialPath = "Assets/Materials/Ice.mat";

    [MenuItem("LionIceRun/Step 2 바다 꾸미기")]
    static void Run()
    {
        IceGrid grid = Object.FindAnyObjectByType<IceGrid>();
        if (grid == null || grid.sea == null)
        {
            Debug.LogError("Step 2 바다: IceGrid 또는 Sea가 씬에 없습니다. 먼저 LionIceRun > Step 2 맵 세팅을 실행하세요.");
            return;
        }
        Shader shader = Shader.Find("LionIceRun/StylizedSea");
        if (shader == null)
        {
            Debug.LogError("Step 2 바다: StylizedSea 셰이더를 찾지 못했습니다. Assets/Shaders/StylizedSea.shader의 컴파일 오류를 확인하세요.");
            return;
        }

        // 재질: 카메라 배경색과 먼 곳 색을 맞춰 지평선이 하늘에 녹아들게
        Material sea = AssetDatabase.LoadAssetAtPath<Material>(SeaMaterialPath);
        if (sea == null)
        {
            sea = new Material(shader);
            AssetDatabase.CreateAsset(sea, SeaMaterialPath);
        }
        if (sea.shader != shader)
        {
            // 예전 셰이더(URP Lit)에 저장된 같은 이름의 값이 남지 않게 셰이더 기본값으로 초기화
            var fresh = new Material(shader);
            EditorUtility.CopySerialized(fresh, sea);
            Object.DestroyImmediate(fresh);
            sea.name = "Sea";
        }
        if (Camera.main != null) sea.SetColor("_HorizonColor", Camera.main.backgroundColor);
        EditorUtility.SetDirty(sea);

        // 메시와 크기
        GameObject seaObject = grid.sea.gameObject;
        if (seaObject.GetComponent<SeaSurface>() == null) seaObject.AddComponent<SeaSurface>();
        var renderer = seaObject.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = sea;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = true;
        Collider collider = seaObject.GetComponent<Collider>();
        if (collider != null) Object.DestroyImmediate(collider);
        grid.seaMargin = 190f;
        grid.UpdateSea();
        EditorUtility.SetDirty(grid);

        // 유빙
        IceFloes floes = Object.FindAnyObjectByType<IceFloes>();
        if (floes == null) floes = new GameObject("IceFloes").AddComponent<IceFloes>();
        floes.grid = grid;
        floes.iceMaterial = AssetDatabase.LoadAssetAtPath<Material>(IceMaterialPath);

        // 그림자 거리: 카메라에서 수면까지 약 40~60m
        if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp && urp.shadowDistance < 90f)
        {
            urp.shadowDistance = 90f;
            EditorUtility.SetDirty(urp);
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(grid.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();
        Selection.activeGameObject = seaObject;
        Debug.Log("Step 2 바다 꾸미기 완료: 바다 크기, 파도 메시, 바다 셰이더, 유빙, 그림자 거리를 설정했습니다. 색과 파도는 Sea 재질에서 조정합니다.");
    }
}
