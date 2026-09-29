// UI 시트 이미지에서 투명 배경으로 나뉜 요소를 찾아 잘라낸다.
// PowerShell에서 Add-Type으로 불러 쓴다 (Tools/ui/slice_ui.ps1).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class SliceSheet
{
    // 알파가 threshold 이상인 픽셀을 이어진 덩어리로 묶어 경계 상자를 돌려준다.
    // 속도를 위해 step 픽셀 간격으로 성긴 격자에서 찾고, 작은 덩어리(minArea 칸 미만)는 버린다.
    public static List<Rectangle> FindRegions(string path, int threshold, int step, int minArea)
    {
        using (var bitmap = new Bitmap(path))
        {
            int w = bitmap.Width, h = bitmap.Height;
            byte[] alpha = ReadAlpha(bitmap);
            int gw = w / step, gh = h / step;
            var solid = new bool[gw * gh];
            for (int gy = 0; gy < gh; gy++)
                for (int gx = 0; gx < gw; gx++)
                {
                    // 격자 칸 안에 불투명 픽셀이 하나라도 있으면 채움
                    bool any = false;
                    for (int y = gy * step; y < (gy + 1) * step && !any; y++)
                        for (int x = gx * step; x < (gx + 1) * step; x++)
                            if (alpha[y * w + x] >= threshold) { any = true; break; }
                    solid[gy * gw + gx] = any;
                }

            var visited = new bool[gw * gh];
            var regions = new List<Rectangle>();
            var stack = new Stack<int>();
            for (int start = 0; start < solid.Length; start++)
            {
                if (!solid[start] || visited[start]) continue;
                int minX = int.MaxValue, minY = int.MaxValue, maxX = 0, maxY = 0, area = 0;
                stack.Push(start);
                visited[start] = true;
                while (stack.Count > 0)
                {
                    int i = stack.Pop();
                    int x = i % gw, y = i / gw;
                    area++;
                    minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= gw || ny >= gh) continue;
                            int n = ny * gw + nx;
                            if (solid[n] && !visited[n]) { visited[n] = true; stack.Push(n); }
                        }
                }
                if (area >= minArea)
                    regions.Add(new Rectangle(minX * step, minY * step, (maxX - minX + 1) * step, (maxY - minY + 1) * step));
            }
            regions.Sort((a, b) => a.Y / 60 != b.Y / 60 ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
            return regions;
        }
    }

    // 사각형 영역을 여백(pad)을 두고 잘라 PNG로 저장
    public static void Crop(string source, Rectangle rect, int pad, string output)
    {
        using (var bitmap = new Bitmap(source))
        {
            var r = Rectangle.Intersect(new Rectangle(rect.X - pad, rect.Y - pad, rect.Width + pad * 2, rect.Height + pad * 2),
                new Rectangle(0, 0, bitmap.Width, bitmap.Height));
            using (var cropped = bitmap.Clone(r, PixelFormat.Format32bppArgb))
                cropped.Save(output, ImageFormat.Png);
        }
    }

    static byte[] ReadAlpha(Bitmap bitmap)
    {
        var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        BitmapData data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var bytes = new byte[data.Stride * data.Height];
        Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
        bitmap.UnlockBits(data);
        var alpha = new byte[bitmap.Width * bitmap.Height];
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
                alpha[y * bitmap.Width + x] = bytes[y * data.Stride + x * 4 + 3];
        return alpha;
    }
}
