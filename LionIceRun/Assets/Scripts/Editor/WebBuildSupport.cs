using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.SceneManagement;

// Web를 선택해 빌드할 때만 적용한다. Windows 설정/원본 씬의 배치는 바꾸지 않는다.
public class WebBuildSupport : IPreprocessBuildWithReport, IPostprocessBuildWithReport, IProcessSceneWithReport
{
    public int callbackOrder => -1000;
    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform == BuildTarget.WebGL) Prepare();
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.WebGL) return;
        string folder = Path.Combine(report.summary.outputPath, "Licenses");
        Directory.CreateDirectory(folder);
        File.Copy("Assets/UI/Fonts/Web/OFL.txt", Path.Combine(folder, "NotoSansCJK-OFL.txt"), true);
        File.Copy("Assets/UI/Fonts/Web/SOURCE.txt", Path.Combine(folder, "NotoSansCJK-SOURCE.txt"), true);
    }

    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (report == null || report.summary.platform != BuildTarget.WebGL) return;
        foreach (var root in scene.GetRootGameObjects())
        foreach (var front in root.GetComponentsInChildren<OnlineFrontend>(true))
        {
            // 웹 16:9 화면에서 기존 얼음/시간 HUD와 겹치지 않게 배포 씬만 조정한다.
            if (front.seriesStatus != null)
            {
                var rect = front.seriesStatus.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(0, -210);
                rect.sizeDelta = new Vector2(650, 55);
                front.seriesStatus.enableAutoSizing = false;
                front.seriesStatus.fontSize = 28;
                front.seriesStatus.color = new Color(0.035f, 0.07f, 0.16f);
            }
        }
    }

    [MenuItem("LionIceRun/웹 빌드 리소스 준비")]
    public static void Prepare()
    {
        const string folder = "Assets/Resources/WebRuntime";
        Directory.CreateDirectory(folder);
        var files = Directory.GetFiles(MapData.FolderPath, "*.json")
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).ToArray();
        var bundle = new WebMapCatalog.Bundle {
            files = files.Select(Path.GetFileName).ToArray(), json = files.Select(File.ReadAllText).ToArray()
        };
        foreach (var json in bundle.json)
            if (MapData.FromJson(json) == null) throw new BuildFailedException("웹 맵 JSON 검사 실패");
        File.WriteAllText(folder + "/Maps.json", JsonUtility.ToJson(bundle));
        AssetDatabase.Refresh();
        const string fontPath = folder + "/KoreanBold.asset";
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath) == null)
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Fonts/Web/NotoSansCJKkr-Bold.otf");
            if (source == null) throw new BuildFailedException("웹 한글 원본 글꼴이 없습니다.");
            var font = TMP_FontAsset.CreateFontAsset(source, 64, 9, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
            if (font == null) throw new BuildFailedException("웹 한글 글꼴 생성 실패");
            font.name = "Web Korean Bold SDF";
            AssetDatabase.CreateAsset(font, fontPath);
            AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var texture in font.atlasTextures) AssetDatabase.AddObjectToAsset(texture, font);
            EditorUtility.SetDirty(font);
        }
        AssetDatabase.SaveAssets();
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.template = "PROJECT:LionIceWeb";
        PlayerSettings.runInBackground = true;
        Debug.Log("WEB_RESOURCES_READY: " + files.Length + " maps, embedded Korean font, gzip fallback");
    }
}
