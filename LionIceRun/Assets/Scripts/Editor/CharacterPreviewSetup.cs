using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// 게임과 분리된 레이어에 실제 FBX를 놓고 UI용 투명 배경 카메라로 촬영한다.
public static class CharacterPreviewSetup
{
    const int PreviewLayer = 31;
    const string Folder = "Assets/UI/CharacterPreviews";
    static readonly string[] Names = { "Lion", "PolarBear", "Tiger" };

    public static void Configure(CharacterSelection picker, PlayerSkin source)
    {
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        string path = Folder + "/Character.renderTexture";
        var texture = AssetDatabase.LoadAssetAtPath<RenderTexture>(path);
        if (texture == null)
        {
            texture = new RenderTexture(576, 768, 24, RenderTextureFormat.ARGB32);
            texture.antiAliasing = 4; texture.filterMode = FilterMode.Bilinear;
            AssetDatabase.CreateAsset(texture, path);
        }
        picker.image.texture = texture;
        if (picker.previewRig != null && picker.models != null && picker.models.Length == 3)
        { picker.previewCamera.targetTexture = texture; return; }

        var rig = new GameObject("CharacterPreviewRig");
        rig.transform.position = new Vector3(10000f, 10000f, 10000f);
        picker.previewRig = rig;
        var cameraObject = new GameObject("PreviewCamera", typeof(Camera));
        cameraObject.transform.SetParent(rig.transform, false);
        cameraObject.transform.localPosition = new Vector3(2.8f, 1.8f, -5f);
        cameraObject.transform.LookAt(rig.transform.position + Vector3.up * 1.05f);
        var camera = cameraObject.GetComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = 1.5f;
        camera.nearClipPlane = 0.1f; camera.farClipPlane = 20f;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
        camera.cullingMask = 1 << PreviewLayer; camera.targetTexture = texture;
        camera.enabled = false; camera.allowHDR = false; camera.depth = -20;
        picker.previewCamera = camera;
        AddLight(rig.transform, "Key", new Vector3(45f, -30f, 0f), Color.white, 1.6f);
        AddLight(rig.transform, "Fill", new Vector3(20f, 150f, 0f), new Color(0.6f, 0.85f, 1f), 0.7f);
        picker.models = new GameObject[3];
        for (int i = 0; i < 3; i++)
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(source.models[i], rig.transform);
            model.name = Names[i]; model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            SetLayer(model);
            var animator = model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
            animator.runtimeAnimatorController = source.animatorController;
            animator.applyRootMotion = false; animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            picker.models[i] = model; model.SetActive(i == picker.Index);
        }
        var podium = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        podium.name = "Podium"; podium.transform.SetParent(rig.transform, false);
        podium.transform.localPosition = new Vector3(0f, -0.12f, 0f);
        podium.transform.localScale = new Vector3(1.8f, 0.12f, 1.8f);
        Object.DestroyImmediate(podium.GetComponent<Collider>()); SetLayer(podium);
        string materialPath = Folder + "/Podium.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = new Color(0.05f, 0.5f, 0.9f);
            material.SetFloat("_Smoothness", 0.35f); AssetDatabase.CreateAsset(material, materialPath);
        }
        podium.GetComponent<Renderer>().sharedMaterial = material;
        // 게임 카메라에서 미리보기 모델을 제외한다. 리그는 맵에서 멀리 떨어져 있다.
        foreach (var gameCamera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (gameCamera != camera) { Undo.RecordObject(gameCamera, "미리보기 레이어 분리"); gameCamera.cullingMask &= ~(1 << PreviewLayer); }
        rig.SetActive(false); EditorUtility.SetDirty(picker);
    }

    static void SetLayer(GameObject go)
    { foreach (var child in go.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = PreviewLayer; }
    static void AddLight(Transform parent, string name, Vector3 rotation, Color color, float intensity)
    {
        var go = new GameObject(name, typeof(Light)); go.transform.SetParent(parent, false);
        go.transform.localRotation = Quaternion.Euler(rotation);
        var light = go.GetComponent<Light>(); light.type = LightType.Directional;
        light.color = color; light.intensity = intensity; light.shadows = LightShadows.None; light.cullingMask = 1 << PreviewLayer;
    }

    public static Sprite[] CapturePortraits(CharacterSelection picker, string folder)
    {
        var result = new Sprite[3];
        bool wasActive = picker.previewRig.activeSelf; int selected = picker.Index;
        var previous = RenderTexture.active;
        try
        {
            for (int i = 0; i < 3; i++)
            {
                string path = folder + "/" + Names[i] + ".png";
                if (!File.Exists(path) && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                {
                    var image = RenderPortrait(picker, i);
                    File.WriteAllBytes(path, image.EncodeToPNG()); Object.DestroyImmediate(image);
                    AssetDatabase.ImportAsset(path);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                    importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
                    importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
                }
                result[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
        }
        finally
        { RenderTexture.active = previous; picker.Select(selected); picker.previewRig.SetActive(wasActive); }
        return result;
    }

    static Texture2D RenderPortrait(CharacterSelection picker, int index)
    {
        // 로비 이미지는 게임 씬과 별도의 미리보기 씬에서 촬영한다.
        var preview = new PreviewRenderUtility();
        var previous = RenderTexture.active;
        bool asynchronous = ShaderUtil.allowAsyncCompilation;
        try
        {
            ShaderUtil.allowAsyncCompilation = false;
            var root = Object.Instantiate(picker.previewRig);
            preview.AddSingleGO(root); root.transform.position = Vector3.zero;
            foreach (var camera in root.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(camera.gameObject);
            foreach (var light in root.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(light.gameObject);
            for (int i = 0; i < 3; i++) root.transform.Find(Names[i]).gameObject.SetActive(i == index);
            root.SetActive(true);
            var animator = root.transform.Find(Names[index]).GetComponent<Animator>();
            if (animator != null)
            {
                animator.Rebind(); animator.Update(0f);
                foreach (var clip in animator.runtimeAnimatorController.animationClips)
                    if (clip.name == "Idle" || clip.name.EndsWith("|Idle")) { clip.SampleAnimation(animator.gameObject, 0f); break; }
            }
            preview.camera.orthographic = true; preview.camera.orthographicSize = 1.5f;
            preview.camera.transform.position = new Vector3(2.8f, 1.8f, -5f);
            preview.camera.transform.LookAt(Vector3.up * 1.05f);
            preview.camera.nearClipPlane = 0.1f; preview.camera.farClipPlane = 20f;
            preview.camera.backgroundColor = Color.clear; preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.lights[0].intensity = 1.2f; preview.lights[0].transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            preview.lights[1].intensity = 0.5f; preview.lights[1].transform.rotation = Quaternion.Euler(20f, 150f, 0f);
            preview.ambientColor = new Color(0.4f, 0.45f, 0.5f);
            preview.BeginPreview(new Rect(0, 0, 576, 768), GUIStyle.none);
            preview.Render(true); preview.Render(true);
            var rt = (RenderTexture)preview.EndPreview(); RenderTexture.active = rt;
            var image = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); image.Apply();
            // 미리보기의 선형 렌더 타깃을 sRGB PNG로 저장한다. 알파는 그대로 보존한다.
            if (!rt.sRGB && QualitySettings.activeColorSpace == ColorSpace.Linear)
            {
                var colors = image.GetPixels();
                for (int i = 0; i < colors.Length; i++) colors[i] = colors[i].gamma;
                image.SetPixels(colors); image.Apply();
            }
            int visible = 0; foreach (var pixel in image.GetPixels32()) if (pixel.a > 10) visible++;
            if (visible < 1000) { Object.DestroyImmediate(image); throw new System.InvalidOperationException("캐릭터 초상화가 비어 있습니다: " + Names[index]); }
            return image;
        }
        finally { RenderTexture.active = previous; ShaderUtil.allowAsyncCompilation = asynchronous; preview.Cleanup(); }
    }
}
