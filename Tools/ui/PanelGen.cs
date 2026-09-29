// 시트에 빈 형태로 없는 UI 배경(흰 배지, 반투명 남색 패널)을 둥근 사각형으로 그려 PNG로 저장한다.
// 9-slice로 늘려 쓰도록 모서리 반지름보다 넓게 테두리를 잡는다. slice_ui.ps1에서 Add-Type으로 쓴다.
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

public static class PanelGen
{
    // fill: 채움 색, border: 테두리 색, shade: 아래쪽 그늘 색(입체감, 없으면 투명), gloss: 위쪽 광택 줄 여부
    public static void RoundedPanel(string output, int width, int height, int radius, int borderWidth,
        Color fill, Color border, Color shade, bool gloss)
    {
        using (var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb))
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);

            float half = borderWidth / 2f;
            var outer = new RectangleF(half, half, width - borderWidth - 1, height - borderWidth - 1);
            using (GraphicsPath path = Rounded(outer, radius))
            {
                using (var brush = new SolidBrush(fill)) g.FillPath(brush, path);

                // 아래쪽 그늘: 바닥 30% 부분을 한 톤 어둡게 (폴가이즈 버튼처럼 도톰해 보이게)
                if (shade.A > 0)
                {
                    g.SetClip(path);
                    using (var brush = new SolidBrush(shade))
                        g.FillRectangle(brush, 0, height * 0.7f, width, height);
                    g.ResetClip();
                }
                // 위쪽 광택 줄
                if (gloss)
                {
                    var shine = new RectangleF(radius * 0.6f, borderWidth + 4, width - radius * 1.2f, height * 0.18f);
                    using (GraphicsPath shinePath = Rounded(shine, (int)(shine.Height / 2)))
                    using (var brush = new SolidBrush(Color.FromArgb(70, 255, 255, 255)))
                        g.FillPath(brush, shinePath);
                }
                if (borderWidth > 0)
                    using (var pen = new Pen(border, borderWidth)) g.DrawPath(pen, path);
            }
            bitmap.Save(output, ImageFormat.Png);
        }
    }

    // Fall Guys식 평행사변형 띠 (윗변이 오른쪽으로 skew만큼 밀림). 안쪽에 얇은 흰 선
    public static void Parallelogram(string output, int width, int height, int skew,
        Color fill, Color innerLine, int innerWidth)
    {
        using (var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb))
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);
            PointF[] outer = { new PointF(skew, 0), new PointF(width, 0), new PointF(width - skew, height), new PointF(0, height) };
            using (var brush = new SolidBrush(fill)) g.FillPolygon(brush, outer);
            if (innerWidth > 0)
            {
                float inset = innerWidth * 2.5f;
                float s = skew * (1f - 2f * inset / height);
                PointF[] inner = {
                    new PointF(skew + inset * 0.6f, inset), new PointF(width - inset * 1.4f, inset),
                    new PointF(width - skew - inset * 0.6f, height - inset), new PointF(inset * 1.4f, height - inset) };
                using (var pen = new Pen(innerLine, innerWidth)) g.DrawPolygon(pen, inner);
            }
            bitmap.Save(output, ImageFormat.Png);
        }
    }

    // 화면을 가로지르는 사건 알림 띠 (QUALIFIED! 같은). 옅은 무늬 + 위아래 진한 선. 흰 바탕이라 Unity에서 색을 입혀 쓴다
    public static void Band(string output, int width, int height, Color fill, Color pattern, Color edge)
    {
        using (var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb))
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using (var brush = new SolidBrush(fill)) g.FillRectangle(brush, 0, 0, width, height);
            // 비스듬한 알약 무늬를 엇갈려 깐다
            using (var brush = new SolidBrush(pattern))
            {
                int stepX = 90, stepY = 56;
                for (int row = 0; row * stepY < height + stepY; row++)
                    for (int x = (row % 2) * stepX / 2 - stepX; x < width + stepX; x += stepX)
                    {
                        var state = g.Save();
                        g.TranslateTransform(x, row * stepY - 10);
                        g.RotateTransform(-30);
                        using (GraphicsPath dash = Rounded(new RectangleF(0, 0, 38, 14), 7)) g.FillPath(brush, dash);
                        g.Restore(state);
                    }
            }
            using (var brush = new SolidBrush(edge))
            {
                g.FillRectangle(brush, 0, 0, width, 10);
                g.FillRectangle(brush, 0, height - 10, width, 10);
            }
            bitmap.Save(output, ImageFormat.Png);
        }
    }

    static GraphicsPath Rounded(RectangleF r, int radius)
    {
        float d = radius * 2f;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
