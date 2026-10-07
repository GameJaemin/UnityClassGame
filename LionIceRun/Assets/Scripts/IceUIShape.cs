using UnityEngine;
using UnityEngine.UI;

// 이미지 파일 없이 그리는 둥근 패널. 테두리와 위아래 색은 하나의 UI 메시로 처리한다.
[RequireComponent(typeof(CanvasRenderer))]
public class IceUIShape : MaskableGraphic
{
    public float radius = 24, border = 1.5f;
    public Color bottom = new Color(0.04f, 0.11f, 0.2f);
    public Color edge = new Color(0.45f, 0.75f, 0.85f, 0.25f);
    const int Steps = 8;
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear(); var rect = GetPixelAdjustedRect();
        if (rect.width <= 0 || rect.height <= 0) return;
        float r = Mathf.Clamp(radius, 0, Mathf.Min(rect.width, rect.height) * .5f);
        int n = 4 * (Steps + 1);
        var vertex = UIVertex.simpleVert;
        vertex.position = rect.center; vertex.color = Color.Lerp(bottom, color, .5f); vh.AddVert(vertex);
        for (int ring = 0; ring < 2; ring++)
            for (int corner = 0; corner < 4; corner++)
                for (int s = 0; s <= Steps; s++)
                {
                    float inset = ring == 0 ? Mathf.Min(border, r) : 0;
                    float inner = Mathf.Max(0, r - inset);
                    Vector2 center = new Vector2(corner == 0 || corner == 3 ? rect.xMax - r : rect.xMin + r,
                        corner < 2 ? rect.yMax - r : rect.yMin + r);
                    float angle = (corner * 90f + s * 90f / Steps) * Mathf.Deg2Rad;
                    Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * inner;
                    vertex.position = point;
                    vertex.color = ring == 1 ? edge : Color.Lerp(bottom, color, Mathf.InverseLerp(rect.yMin, rect.yMax, point.y));
                    vh.AddVert(vertex);
                }
        for (int i = 0; i < n; i++)
        {
            int next = (i + 1) % n;
            vh.AddTriangle(0, next + 1, i + 1);
            vh.AddTriangle(i + 1, n + next + 1, n + i + 1);
            vh.AddTriangle(i + 1, next + 1, n + next + 1);
        }
    }
    public void Repaint() => SetVerticesDirty();
}
