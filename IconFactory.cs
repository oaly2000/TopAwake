using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace TopAwake;

internal static class IconFactory
{
    public static Icon Create(bool awakeActive)
    {
        const int size = 32;
        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;

            using var path = RoundedRect(new Rectangle(1, 1, size - 2, size - 2), 9);
            using var bg = new SolidBrush(Color.FromArgb(0, 120, 215));
            g.FillPath(bg, path);

            using var font = CreateFont();
            using var fg = new SolidBrush(Color.White);
            using var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            g.DrawString("顶", font, fg, new RectangleF(0, 0, size, size), format);

            if (awakeActive)
            {
                using var dot = new SolidBrush(Color.OrangeRed);
                using var border = new Pen(Color.White, 1.5f);
                g.FillEllipse(dot, size - 13, 2, 10, 10);
                g.DrawEllipse(border, size - 13, 2, 10, 10);
            }
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    private static Font CreateFont()
    {
        foreach (var name in new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "SimSun" })
        {
            var font = new Font(name, 16f, FontStyle.Bold, GraphicsUnit.Pixel);
            if (string.Equals(font.Name, name, StringComparison.OrdinalIgnoreCase)) return font;
            font.Dispose();
        }
        return new Font(FontFamily.GenericSansSerif, 16f, FontStyle.Bold, GraphicsUnit.Pixel);
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        var d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
