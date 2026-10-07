using System.Collections.Generic;
using UnityEngine;

// 새 맵은 사진이 없어도 룰렛에 지형과 기믹 위치가 보인다.
public static class MapPreview
{
    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
    public static Sprite Get(string file, MapData map)
    {
        var saved = Resources.Load<Sprite>("MapPreviews/" + System.IO.Path.GetFileNameWithoutExtension(file));
        if (saved != null && (map == null || (map.gimmicks.Length == 0 && !System.Array.Exists(map.rows, r => r.Contains("S"))))) return saved;
        if (cache.TryGetValue(file, out var sprite)) return sprite;
        if (map == null) return saved;
        var texture = new Texture2D(256, 160, TextureFormat.RGBA32, false);
        var pixels = new Color[256 * 160];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(0.08f, 0.23f, 0.36f);
        for (int z = map.depth - 1; z >= 0; z--)
        for (int x = 0; x < map.width; x++)
        {
            if (!map.IsIce(new Vector2Int(x, z))) continue;
            Color color = new Color(0.65f, 0.88f, 0.98f);
            if (map.Get(new Vector2Int(x, z)) == MapData.Safe) color = new Color(0.25f, 0.95f, 0.6f);
            foreach (var g in map.gimmicks)
                if (Mathf.Abs(x - g.x) < g.width / 2 && Mathf.Abs(z - g.z) < g.depth / 2)
                    color = g.kind == "conveyor" ? new Color(1, 0.8f, 0.2f) : new Color(0.2f, 0.9f, 0.75f);
            Diamond(pixels, 128 + (x - z) * 7, 22 + (x + z) * 4, color);
        }
        foreach (var g in map.gimmicks)
            if (g.kind == "piston" || g.kind == "sweeper")
                Diamond(pixels, 128 + Mathf.RoundToInt((g.x - g.z) * 7), 22 + Mathf.RoundToInt((g.x + g.z) * 4), new Color(1, 0.2f, 0.5f));
        texture.SetPixels(pixels); texture.Apply(); texture.filterMode = FilterMode.Point;
        sprite = Sprite.Create(texture, new Rect(0, 0, 256, 160), Vector2.one * 0.5f);
        cache[file] = sprite; return sprite;
    }
    static void Diamond(Color[] pixels, int cx, int cy, Color color)
    {
        for (int y = -3; y <= 3; y++)
        for (int x = -6; x <= 6; x++)
            if (Mathf.Abs(x) / 7f + Mathf.Abs(y) / 4f <= 1 && cx + x >= 0 && cx + x < 256 && cy + y >= 0 && cy + y < 160)
                pixels[(cy + y) * 256 + cx + x] = color;
    }
}
