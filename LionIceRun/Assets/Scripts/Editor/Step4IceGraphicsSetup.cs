using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Step 4 폴리싱: 얼음 그래픽.
//  1) 금 간 얼음 텍스처 4단계를 코드로 그려 PNG로 저장 (Assets/Textures/Ice/)
//     단계가 오를수록 앞 단계의 금은 그대로 두고 새 금을 더한다.
//  2) 얼음 조각·얼음 가루 재질 생성, 블럭 재질에 서리 텍스처 적용
//  3) 씬의 IceGrid에 연결
// 메뉴: LionIceRun > Step 4 얼음 그래픽 세팅
public static class Step4IceGraphicsSetup
{
    const string TextureFolder = "Assets/Textures/Ice";
    const string IceMaterialPath = "Assets/Materials/Ice.mat";
    const string ShardMaterialPath = "Assets/Materials/IceShard.mat";
    const string ParticleMaterialPath = "Assets/Materials/IceParticle.mat";
    const int Size = 256;
    const int Seed = 20260929;
    // 새 금이 자라는 단계 수 (마지막 = 완성된 단계 텍스처). 중간 텍스처는 GrowFrames - 1장
    const int GrowFrames = 4;
    // 금 무늬 벌 수. 1번은 처음 만든 무늬(Seed) 그대로, 나머지는 시드를 바꿔 만든다
    const int Variants = 4;

    [MenuItem("LionIceRun/Step 4 얼음 그래픽 세팅")]
    static void Run()
    {
        IceGrid grid = Object.FindAnyObjectByType<IceGrid>();
        if (grid == null)
        {
            Debug.LogError("Step 4 그래픽: IceGrid가 씬에 없습니다. 먼저 LionIceRun > Step 2 맵 세팅을 실행하세요.");
            return;
        }

        Texture2D[] textures = CreateCrackTextures(out Texture2D[] growFrames);

        Material ice = AssetDatabase.LoadAssetAtPath<Material>(IceMaterialPath);
        if (ice != null)
        {
            ice.SetTexture("_BaseMap", textures[0]);
            ice.SetFloat("_Smoothness", 0.8f);
            EditorUtility.SetDirty(ice);
        }

        Material shard = GetOrCreateMaterial(ShardMaterialPath, "Universal Render Pipeline/Lit");
        shard.SetColor("_BaseColor", new Color(0.55f, 0.75f, 0.95f));
        shard.SetFloat("_Smoothness", 0.9f);
        EditorUtility.SetDirty(shard);

        Material particle = GetOrCreateMaterial(ParticleMaterialPath, "Universal Render Pipeline/Particles/Unlit");
        particle.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(particle);

        grid.stageTextures = textures;
        grid.crackGrowFrames = growFrames;
        grid.crackGrowFramesPerStage = GrowFrames - 1;
        grid.crackVariantCount = Variants;
        grid.shardMaterial = shard;
        grid.particleMaterial = particle;
        EditorUtility.SetDirty(grid);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(grid.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();
        Selection.activeObject = textures[3];
        Debug.Log("Step 4 얼음 그래픽 세팅 완료: 금 간 텍스처 4장, 조각·가루 재질을 만들어 IceGrid에 연결했습니다.");
    }

    static Material GetOrCreateMaterial(string path, string shaderName)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(Shader.Find(shaderName));
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    // ---- 텍스처 그리기 ----

    struct Crack
    {
        public int stage;
        public List<Vector2> points;
        public float width;
    }

    // 무늬마다: 단계 텍스처 4장 + 단계 1~3마다 새 금이 자라는 중간 텍스처 (GrowFrames - 1)장씩
    // 반환(단계 텍스처) 순서: [무늬 * 4 + 단계]
    // growFrames 순서: [(무늬 * 3 + (단계-1)) * (GrowFrames-1) + (프레임-1)]
    static Texture2D[] CreateCrackTextures(out Texture2D[] growFrames)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Textures")) AssetDatabase.CreateFolder("Assets", "Textures");
        if (!AssetDatabase.IsValidFolder(TextureFolder)) AssetDatabase.CreateFolder("Assets/Textures", "Ice");
        DeleteLegacyTextures();

        string[] names = { "IceStage0_Clean", "IceStage1_Cracked", "IceStage2_HeavyCracked", "IceStage3_Shattering" };
        int framesPerStage = GrowFrames - 1;
        var textures = new Texture2D[Variants * 4];
        growFrames = new Texture2D[Variants * 3 * framesPerStage];

        for (int variant = 0; variant < Variants; variant++)
        {
            var rng = new System.Random(Seed + variant * 7919);
            List<Crack> cracks = BuildCracks(rng);
            float[] frost = BuildFrost(rng);
            string folder = $"{TextureFolder}/V{variant + 1}";

            for (int stage = 0; stage < 4; stage++)
                textures[variant * 4 + stage] = SaveTexture(Render(frost, cracks, stage, 1f), $"{folder}/{names[stage]}.png");

            for (int stage = 1; stage < 4; stage++)
            for (int frame = 1; frame < GrowFrames; frame++)
            {
                float grow = frame / (float)GrowFrames;
                growFrames[(variant * 3 + (stage - 1)) * framesPerStage + (frame - 1)] =
                    SaveTexture(Render(frost, cracks, stage, grow), $"{folder}/Grow/{names[stage]}_{frame}.png");
            }
        }
        return textures;
    }

    // 무늬가 한 벌뿐이던 때 Assets/Textures/Ice 바로 아래에 만든 텍스처를 지운다
    static void DeleteLegacyTextures()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TextureFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string relative = path.Substring(TextureFolder.Length + 1);
            if (!relative.StartsWith("V")) AssetDatabase.DeleteAsset(path);
        }
        if (AssetDatabase.IsValidFolder($"{TextureFolder}/Grow")) AssetDatabase.DeleteAsset($"{TextureFolder}/Grow");
    }

    // stage 이전 단계의 금은 다 그리고, stage에 새로 생기는 금은 grow(0~1) 비율만큼만 그린다
    static float[] Render(float[] frost, List<Crack> cracks, int stage, float grow)
    {
        float[] value = (float[])frost.Clone();
        foreach (Crack crack in cracks)
        {
            if (crack.stage < stage) DrawCrack(value, crack, 1f);
            else if (crack.stage == stage) DrawCrack(value, crack, grow);
        }
        return value;
    }

    static Texture2D SaveTexture(float[] value, string path)
    {
        string folder = Path.GetDirectoryName(path).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\', '/'), Path.GetFileName(folder));

        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        var pixels = new Color[Size * Size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(value[i], value[i], value[i], 1f);
        texture.SetPixels(pixels);
        texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.anisoLevel = 4;
        importer.mipmapEnabled = true;
        importer.sRGBTexture = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // 바탕: 살짝 얼룩진 서리 + 옅은 사선 결 + 밝은 테두리(모서리가 빛나는 느낌) + 가장 바깥 어두운 선(블럭 구분)
    static float[] BuildFrost(System.Random rng)
    {
        var value = new float[Size * Size];
        float ox = (float)rng.NextDouble() * 100f, oy = (float)rng.NextDouble() * 100f;
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            float noise = Mathf.PerlinNoise(ox + x * 0.03f, oy + y * 0.03f) * 0.06f
                        + Mathf.PerlinNoise(ox + x * 0.12f, oy + y * 0.12f) * 0.03f;
            float edge = Mathf.Min(Mathf.Min(x, Size - 1 - x), Mathf.Min(y, Size - 1 - y));
            float v = 0.84f + noise;
            if (edge < 14f) v = Mathf.Lerp(1f, v, edge / 14f);
            if (edge < 2f) v = 0.72f;
            value[y * Size + x] = v;
        }

        // 옅은 결
        for (int i = 0; i < 10; i++)
        {
            var start = new Vector2(rng.Next(20, Size - 20), rng.Next(20, Size - 20));
            var end = start + new Vector2(1f, -0.6f).normalized * rng.Next(25, 60);
            DrawLine(value, start, end, 1f, v => Mathf.Max(v, 0.95f));
        }
        return value;
    }

    // 금: 가운데 충격점에서 퍼지는 금(1단계) → 더 길고 갈라지는 금(2단계) → 거미줄처럼 잇는 금(3단계)
    static List<Crack> BuildCracks(System.Random rng)
    {
        var cracks = new List<Crack>();
        var center = new Vector2(Size * 0.5f + rng.Next(-20, 20), Size * 0.5f + rng.Next(-20, 20));

        void AddRadial(int stage, int count, int minLength, int maxLength, float width, float branchChance)
        {
            float offset = (float)rng.NextDouble() * Mathf.PI * 2f;
            for (int i = 0; i < count; i++)
            {
                float angle = offset + i * Mathf.PI * 2f / count + (float)(rng.NextDouble() - 0.5) * 0.6f;
                List<Vector2> points = Walk(rng, center, angle, rng.Next(minLength, maxLength));
                cracks.Add(new Crack { stage = stage, points = points, width = width });
                if (rng.NextDouble() < branchChance && points.Count > 3)
                {
                    Vector2 from = points[rng.Next(1, points.Count - 1)];
                    float branchAngle = angle + (rng.NextDouble() < 0.5 ? -1f : 1f) * (0.5f + (float)rng.NextDouble() * 0.5f);
                    cracks.Add(new Crack { stage = stage, points = Walk(rng, from, branchAngle, rng.Next(20, 45)), width = width * 0.7f });
                }
            }
        }

        AddRadial(1, 3, 60, 95, 2.2f, 0.3f);
        AddRadial(2, 5, 90, 150, 1.8f, 0.8f);
        AddRadial(3, 7, 110, 170, 1.4f, 1f);

        // 3단계: 충격점 주위를 두 겹으로 잇는 거미줄 금
        foreach (float radius in new[] { 40f, 80f })
        {
            var ring = new List<Vector2>();
            int segments = 11;
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                float r = radius + (float)(rng.NextDouble() - 0.5) * 18f;
                ring.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r);
            }
            cracks.Add(new Crack { stage = 3, points = ring, width = 1.2f });
        }
        return cracks;
    }

    static List<Vector2> Walk(System.Random rng, Vector2 start, float angle, int length)
    {
        var points = new List<Vector2> { start };
        Vector2 position = start;
        for (int travelled = 0; travelled < length;)
        {
            int step = rng.Next(6, 12);
            angle += (float)(rng.NextDouble() - 0.5) * 0.7f;
            position += new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * step;
            points.Add(position);
            travelled += step;
        }
        return points;
    }

    // fraction: 금 전체 길이 중 앞에서부터 그릴 비율 (충격점에서 바깥으로 자라는 모습)
    static void DrawCrack(float[] value, Crack crack, float fraction)
    {
        if (fraction <= 0f) return;
        float total = 0f;
        for (int i = 1; i < crack.points.Count; i++) total += Vector2.Distance(crack.points[i - 1], crack.points[i]);
        float remaining = total * Mathf.Clamp01(fraction);

        for (int i = 1; i < crack.points.Count && remaining > 0f; i++)
        {
            Vector2 a = crack.points[i - 1], b = crack.points[i];
            float segment = Vector2.Distance(a, b);
            if (segment > remaining) b = Vector2.Lerp(a, b, remaining / segment);
            remaining -= segment;
            // 한쪽에 밝은 선을 그어 금이 파인 듯한 깊이감을 준다
            DrawLine(value, a + new Vector2(1.5f, 1.5f), b + new Vector2(1.5f, 1.5f), crack.width * 0.8f, v => Mathf.Max(v, 1f));
            DrawLine(value, a, b, crack.width, v => Mathf.Min(v, 0.3f));
        }
    }

    static void DrawLine(float[] value, Vector2 a, Vector2 b, float width, System.Func<float, float> apply)
    {
        float length = Vector2.Distance(a, b);
        int steps = Mathf.Max(1, Mathf.CeilToInt(length * 2f));
        int radius = Mathf.CeilToInt(width * 0.5f);
        for (int s = 0; s <= steps; s++)
        {
            Vector2 p = Vector2.Lerp(a, b, s / (float)steps);
            for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (dx * dx + dy * dy > width * width * 0.25f + 0.5f) continue;
                int x = Mathf.RoundToInt(p.x) + dx, y = Mathf.RoundToInt(p.y) + dy;
                if (x < 0 || y < 0 || x >= Size || y >= Size) continue;
                int index = y * Size + x;
                value[index] = apply(value[index]);
            }
        }
    }
}
