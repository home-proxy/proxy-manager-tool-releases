using SkiaSharp;
using Svg.Skia;

namespace NetAgent.ProxyManager.App.Theme;

internal static class SidebarIconRenderer
{
    private const int IconSize = 20;
    private static readonly Dictionary<string, Bitmap?> Cache = [];
    private static readonly object CacheLock = new();

    public static Bitmap? Load(string fileName, Color color)
    {
        return Load(fileName, color, IconSize);
    }

    public static Bitmap? Load(string fileName, Color color, int size)
    {
        var key = $"{fileName}|{color.ToArgb():x8}|{size}";
        lock (CacheLock)
        {
            if (Cache.TryGetValue(key, out var cached))
            {
                return cached;
            }
        }

        var bitmap = TryRender(fileName, color, size);
        lock (CacheLock)
        {
            Cache[key] = bitmap;
        }

        return bitmap;
    }

    public static Bitmap? LoadOriginal(string fileName)
    {
        return LoadOriginal(fileName, IconSize);
    }

    public static Bitmap? LoadOriginal(string fileName, int size)
    {
        var key = $"{fileName}|original|{size}";
        lock (CacheLock)
        {
            if (Cache.TryGetValue(key, out var cached))
            {
                return cached;
            }
        }

        var bitmap = TryRender(fileName, null, size);
        lock (CacheLock)
        {
            Cache[key] = bitmap;
        }

        return bitmap;
    }

    private static Bitmap? TryRender(string fileName, Color? color, int size)
    {
        try
        {
            using var svg = new SKSvg();
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "icons", fileName);
            if (File.Exists(path))
            {
                if (svg.Load(path) is null)
                {
                    return null;
                }
            }
            else
            {
                using var resource = EmbeddedAssets.Open($"Assets/icons/{fileName}");
                if (resource is null || svg.Load(resource) is null)
                {
                    return null;
                }
            }

            if (svg.Picture is null)
            {
                return null;
            }

            var bounds = svg.Picture.CullRect;
            var targetSize = Math.Max(1, size);
            var sourceWidth = bounds.Width > 0 ? bounds.Width : targetSize;
            var sourceHeight = bounds.Height > 0 ? bounds.Height : targetSize;
            var scale = Math.Min(targetSize / sourceWidth, targetSize / sourceHeight);
            var drawWidth = sourceWidth * scale;
            var drawHeight = sourceHeight * scale;

            using var skBitmap = new SKBitmap(targetSize, targetSize, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var canvas = new SKCanvas(skBitmap);
            canvas.Clear(SKColors.Transparent);
            canvas.Translate((targetSize - drawWidth) / 2f, (targetSize - drawHeight) / 2f);
            canvas.Scale(scale);
            canvas.Translate(-bounds.Left, -bounds.Top);
            canvas.DrawPicture(svg.Picture);
            canvas.Flush();

            if (color is { } targetColor)
            {
                Recolor(skBitmap, new SKColor(targetColor.R, targetColor.G, targetColor.B));
            }

            return ToSystemBitmap(skBitmap);
        }
        catch
        {
            return null;
        }
    }

    private static void Recolor(SKBitmap bitmap, SKColor color)
    {
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.Alpha == 0)
                {
                    continue;
                }

                bitmap.SetPixel(x, y, color.WithAlpha(pixel.Alpha));
            }
        }
    }

    private static Bitmap ToSystemBitmap(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = new MemoryStream(data.ToArray());
        using var decoded = Image.FromStream(stream);
        return new Bitmap(decoded);
    }
}
