using UnityEngine;
using UnityEngine.UI;

// 글꼴의 이모지 지원 여부에 의존하지 않는 여섯 가지 감정 아이콘.
[RequireComponent(typeof(CanvasRenderer))]
public class IceEmoteIcon : MaskableGraphic
{
    public int expression;
    static readonly Color Ink = new Color(.05f, .14f, .23f);
    public static readonly string[] Names = { "안녕!", "최고야", "고마워", "깜짝!", "아쉬워", "가자!" };
    public static readonly Color[] Colors = { new(.42f,.94f,.79f), new(1,.57f,.71f), new(1,.85f,.42f), new(.5f,.8f,1), new(.78f,.7f,1), new(1,.72f,.38f) };
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear(); int e = Mathf.Clamp(expression, 0, 5);
        Disc(vh, Vector2.zero, .47f, Colors[e]);
        if (e == 5)
        {
            Poly(vh, new[] { new Vector2(.04f,.32f), new Vector2(-.2f,-.04f), new Vector2(-.02f,-.04f), new Vector2(-.06f,-.33f), new Vector2(.22f,.05f), new Vector2(.03f,.05f) }, Ink); return;
        }
        for (int side = -1; side <= 1; side += 2)
        {
            Vector2 eye = new Vector2(side * .17f, .12f);
            if (e == 1) Heart(vh, eye, .10f, Ink);
            else if (e == 2) { Line(vh, eye + new Vector2(-.07f,-.02f), eye + new Vector2(0,.04f), .028f, Ink); Line(vh, eye + new Vector2(0,.04f), eye + new Vector2(.07f,-.02f), .028f, Ink); }
            else Disc(vh, eye, e == 3 ? .047f : .032f, Ink);
        }
        if (e == 3) { Disc(vh, new Vector2(0,-.15f), .105f, Ink); Disc(vh, new Vector2(0,-.15f), .06f, Colors[e]); }
        else if (e == 4)
        {
            Arc(vh, new Vector2(0,-.28f), .17f, 20, 160, .03f, Ink);
            Disc(vh, new Vector2(.25f,-.10f), .048f, new Color(.25f,.6f,1));
        }
        else Arc(vh, new Vector2(0,-.02f), .22f, 205, 335, .035f, Ink);
        if (e == 0 || e == 2) for (int side = -1; side <= 1; side += 2) Disc(vh, new Vector2(side*.3f,-.05f), .055f, new Color(1,.55f,.65f,.5f));
    }
    Vector2 P(Vector2 p) => rectTransform.rect.center + p * Mathf.Min(rectTransform.rect.width, rectTransform.rect.height);
    void Poly(VertexHelper vh, Vector2[] points, Color c)
    {
        int begin = vh.currentVertCount;
        foreach (var point in points) { var v = UIVertex.simpleVert; v.position = P(point); v.color = c; vh.AddVert(v); }
        for (int i = 1; i < points.Length - 1; i++) vh.AddTriangle(begin, begin+i, begin+i+1);
    }
    void Disc(VertexHelper vh, Vector2 center, float radius, Color c)
    {
        var points = new Vector2[33]; points[0] = center;
        for (int i=0;i<32;i++) { float a = i * Mathf.PI * 2 / 31; points[i+1] = center + new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius; }
        Poly(vh, points, c);
    }
    void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color c)
    {
        Vector2 n = new Vector2(-(b-a).y,(b-a).x).normalized * width*.5f;
        Poly(vh, new[] { a-n,a+n,b+n,b-n }, c);
    }
    void Arc(VertexHelper vh, Vector2 center, float r, float from, float to, float width, Color c)
    {
        Vector2 previous = center + new Vector2(Mathf.Cos(from*Mathf.Deg2Rad),Mathf.Sin(from*Mathf.Deg2Rad))*r;
        for (int i=1;i<=18;i++) { float a = Mathf.Lerp(from,to,i/18f)*Mathf.Deg2Rad; var p = center + new Vector2(Mathf.Cos(a),Mathf.Sin(a))*r; Line(vh,previous,p,width,c); previous=p; }
    }
    void Heart(VertexHelper vh, Vector2 p, float size, Color c)
    { Disc(vh,p+new Vector2(-size*.4f,0),size*.55f,c); Disc(vh,p+new Vector2(size*.4f,0),size*.55f,c); Poly(vh,new[]{p+new Vector2(-size,.02f),p+new Vector2(size,.02f),p+new Vector2(0,-size*1.25f)},c); }
}
