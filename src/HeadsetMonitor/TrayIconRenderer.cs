using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using HeadsetMonitor.Core;

namespace HeadsetMonitor;

internal static class TrayIconRenderer
{
    private static readonly Lazy<Bitmap?> HeadphoneArtwork = new(LoadHeadphoneArtwork);

    public static Icon Render(BatterySnapshot? snapshot, AppSettings settings)
    {
        const int size = 32;
        using var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        var (text, colour) = GetPresentation(snapshot, settings);
        DrawTintedHeadphones(graphics, colour, size);
        using var font = CreateLargestFontThatFits(graphics, text, size - 3, size - 3);
        using var foreground = new SolidBrush(Color.White);
        using var outline = new SolidBrush(Color.FromArgb(210, Color.Black));
        using var format = new StringFormat(StringFormat.GenericTypographic)
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        foreach (var offset in new[] { new PointF(-1, 0), new PointF(1, 0), new PointF(0, -1), new PointF(0, 1) })
            graphics.DrawString(text, font, outline, new RectangleF(offset.X, offset.Y, size, size), format);
        graphics.DrawString(text, font, foreground, new RectangleF(0, 0, size, size), format);

        var handle = bitmap.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    private static Font CreateLargestFontThatFits(Graphics graphics, string text, int maxWidth, int maxHeight)
    {
        for (var fontSize = 24; fontSize >= 8; fontSize--)
        {
            var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
            var measured = graphics.MeasureString(text, font, int.MaxValue, StringFormat.GenericTypographic);
            if (measured.Width <= maxWidth && measured.Height <= maxHeight)
                return font;
            font.Dispose();
        }

        return new Font("Segoe UI", 8, FontStyle.Bold, GraphicsUnit.Pixel);
    }

    private static void DrawTintedHeadphones(Graphics graphics, Color colour, int size)
    {
        var artwork = HeadphoneArtwork.Value;
        if (artwork is null)
        {
            using var fallback = new SolidBrush(colour);
            graphics.FillEllipse(fallback, 1, 1, size - 2, size - 2);
            return;
        }

        var matrix = new ColorMatrix
        {
            Matrix00 = 0,
            Matrix11 = 0,
            Matrix22 = 0,
            Matrix33 = 1,
            Matrix40 = colour.R / 255f,
            Matrix41 = colour.G / 255f,
            Matrix42 = colour.B / 255f,
            Matrix44 = 1
        };
        using var attributes = new ImageAttributes();
        attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
        graphics.DrawImage(artwork, new Rectangle(1, 1, size - 2, size - 2),
            0, 0, artwork.Width, artwork.Height, GraphicsUnit.Pixel, attributes);
    }

    private static Bitmap? LoadHeadphoneArtwork()
    {
        using var stream = typeof(TrayIconRenderer).Assembly
            .GetManifestResourceStream("HeadsetMonitor.Icons.headphone.png");
        return stream is null ? null : new Bitmap(stream);
    }

    private static (string Text, Color Colour) GetPresentation(BatterySnapshot? snapshot, AppSettings settings)
    {
        if (snapshot?.State == BatteryState.Error)
            return ("!", Color.DarkOrange);
        if (snapshot?.HasCurrentLevel == true && snapshot.LevelPercent is int level)
        {
            var colour = settings.GetBand(level) switch
            {
                BatteryBand.Critical => Color.Firebrick,
                BatteryBand.Warning => Color.Goldenrod,
                _ => Color.ForestGreen
            };
            return (level.ToString(), colour);
        }
        return ("?", Color.DimGray);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
