using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Web 전용 렌더/연출 예산. 원본 품질 에셋과 Windows 설정은 변경하지 않는다.
public static class WebRuntimePerformance
{
    public static bool Enabled => Application.platform == RuntimePlatform.WebGLPlayer;
    public const int MaxShards = 96;
    static int activeShards;
    public static int ActiveShards => activeShards;
    public static bool TryBeginShard()
    {
        if (!Enabled) return true;
        if (activeShards >= MaxShards) return false;
        activeShards++;
        return true;
    }
    public static void EndShard() { if (Enabled) activeShards = Mathf.Max(0, activeShards - 1); }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Configure()
    {
        activeShards = 0;
        if (!Enabled) return;
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
        var source = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (source == null) return;
        var runtime = Object.Instantiate(source);
        runtime.name = "Web Runtime Pipeline";
        runtime.renderScale = 0.85f;
        runtime.msaaSampleCount = 2;
        runtime.shadowDistance = 24;
        runtime.shadowCascadeCount = 1;
        runtime.supportsHDR = false;
        QualitySettings.renderPipeline = runtime;
    }
}
