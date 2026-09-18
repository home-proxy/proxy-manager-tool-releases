using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkiaSharp;
using Svg.Skia;

namespace NetAgent.ProxyManager.App.Theme;

internal static class AppBranding
{
    public const string AppDisplayName = "ProxyManager";
    public const string AppExeName = "ProxyManager.App.exe";

    private const string AppShortcutName = AppDisplayName + ".lnk";
    private const string AssetsDirectoryName = "assets";
    private const string SmallLogoName = "small-logo";
    private const string LargeLogoName = "large-logo";
    private const string MerchantIdFileName = "merchant_id.txt";
    private const string GeneratedIconSearchPattern = "small-logo.generated-*.ico";
    private const string GeneratedLogoSearchPattern = "large-logo.generated-*.png";
    private const string LegacyGeneratedIconName = "small-logo.generated.ico";
    private const string LegacyGeneratedLogoName = "large-logo.generated.png";
    private const string ProductionSettingsFileName = "appsettings.Production.json";
    private const int ShellChangeNotifyAssocChanged = 0x08000000;

    private static readonly string[] SupportedExtensions =
    [
        ".ico",
        ".png",
        ".svg",
        ".jpg",
        ".jpeg",
        ".bmp",
        ".gif",
        ".tif",
        ".tiff",
        ".webp"
    ];

    private static readonly int[] IconSizes = [16, 24, 32, 48, 64, 128, 256];
    private static readonly ConditionalWeakTable<Form, AppliedIcon> AppliedFormIcons = [];

    public static string GetMainWindowTitle()
    {
        var version = GetDisplayVersion();
        return string.IsNullOrWhiteSpace(version)
            ? AppDisplayName
            : $"{AppDisplayName} v{version}";
    }

    private static string? GetDisplayVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(AppBranding).Assembly;
        var version = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?.Trim();
        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        var metadataIndex = version.IndexOf('+', StringComparison.Ordinal);
        return metadataIndex >= 0
            ? version[..metadataIndex].Trim()
            : version;
    }

    public static void PrepareBranding(bool updateShortcuts)
    {
        var assetsDirectory = GetAssetsDirectory();

        TryRun(() => PrepareSmallLogo(assetsDirectory));
        TryRun(() => PrepareLargeLogo(assetsDirectory));
        TryRun(() => ApplyMerchantId(assetsDirectory));

        if (updateShortcuts)
        {
            UpdateShortcutIcons(GetApplicationIconPath());
            NotifyShellIconCacheChanged();
        }

        TryDeleteBrandingPayload(assetsDirectory);
    }

    public static Image? LoadLogoImage()
    {
        var logoPath = GetLogoImagePath();
        return logoPath is null ? null : TryLoadImage(logoPath, 512);
    }

    public static Image? LoadSmallLogoImage()
    {
        var logoPath = GetSmallLogoImagePath();
        return logoPath is null ? null : TryLoadImage(logoPath, 128, squareCanvas: true);
    }

    public static Icon? LoadApplicationIcon()
    {
        var iconPath = GetApplicationIconPath();
        if (iconPath is null)
        {
            return null;
        }

        try
        {
            return new Icon(iconPath);
        }
        catch
        {
            return null;
        }
    }

    public static void ApplyApplicationIcon(Form form)
    {
        if (AppliedFormIcons.TryGetValue(form, out _))
        {
            return;
        }

        var icon = LoadApplicationIcon();
        if (icon is null)
        {
            return;
        }

        var appliedIcon = new AppliedIcon(icon);
        AppliedFormIcons.Add(form, appliedIcon);
        form.Icon = icon;
        form.Disposed += (_, _) => appliedIcon.Dispose();
    }

    public static void ApplyApplicationIconToOpenForms()
    {
        foreach (Form form in Application.OpenForms)
        {
            ApplyApplicationIcon(form);
        }
    }

    private static void PrepareSmallLogo(string assetsDirectory)
    {
        var sourcePath = FindBrandingFile(assetsDirectory, SmallLogoName);
        if (sourcePath is null)
        {
            return;
        }

        var generatedPath = Path.Combine(AppContext.BaseDirectory, GetGeneratedIconName(sourcePath));
        if (string.Equals(Path.GetExtension(sourcePath), ".ico", StringComparison.OrdinalIgnoreCase))
        {
            CopyIfNewer(sourcePath, generatedPath);
        }
        else
        {
            EnsureGeneratedIcon(sourcePath, generatedPath);
        }

        DeleteOldGeneratedFiles(GeneratedIconSearchPattern, generatedPath);
        TryDeleteFile(Path.Combine(AppContext.BaseDirectory, LegacyGeneratedIconName));
    }

    private static void PrepareLargeLogo(string assetsDirectory)
    {
        var sourcePath = FindBrandingFile(assetsDirectory, LargeLogoName);
        if (sourcePath is null)
        {
            return;
        }

        var generatedPath = Path.Combine(AppContext.BaseDirectory, GetGeneratedLogoName(sourcePath));
        if (string.Equals(Path.GetExtension(sourcePath), ".png", StringComparison.OrdinalIgnoreCase))
        {
            CopyIfNewer(sourcePath, generatedPath);
        }
        else
        {
            using var image = TryLoadImage(sourcePath, 512);
            image?.Save(generatedPath, System.Drawing.Imaging.ImageFormat.Png);
        }

        DeleteOldGeneratedFiles(GeneratedLogoSearchPattern, generatedPath);
        TryDeleteFile(Path.Combine(AppContext.BaseDirectory, LegacyGeneratedLogoName));
    }

    private static void ApplyMerchantId(string assetsDirectory)
    {
        var merchantFilePath = Path.Combine(assetsDirectory, MerchantIdFileName);
        if (!File.Exists(merchantFilePath))
        {
            return;
        }

        var merchantId = File.ReadAllText(merchantFilePath).Trim();
        if (!Guid.TryParse(merchantId, out var parsedMerchantId))
        {
            return;
        }

        var settingsPath = Path.Combine(AppContext.BaseDirectory, ProductionSettingsFileName);

        // Prefer an existing on-disk file; otherwise seed from the copy embedded in the
        // exe so branding still works for the standalone single-file build (the đại lý zip
        // ships only the exe + assets/, never appsettings.Production.json on disk).
        var json = File.Exists(settingsPath)
            ? File.ReadAllText(settingsPath)
            : EmbeddedAssets.ReadAllText(ProductionSettingsFileName) ?? "{}";

        var root = JsonNode.Parse(json)?.AsObject() ?? new JsonObject();
        var backendApi = root["BackendApi"] as JsonObject;
        if (backendApi is null)
        {
            backendApi = new JsonObject();
            root["BackendApi"] = backendApi;
        }

        backendApi["MerchantId"] = parsedMerchantId.ToString();
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(settingsPath, root.ToJsonString(options) + Environment.NewLine);
    }

    private static string? GetApplicationIconPath()
    {
        var generatedPath = FindLatestGeneratedFile(GeneratedIconSearchPattern)
            ?? FindExistingFile(Path.Combine(AppContext.BaseDirectory, LegacyGeneratedIconName));
        if (generatedPath is not null)
        {
            return generatedPath;
        }

        var payloadIcon = FindBrandingFile(GetAssetsDirectory(), SmallLogoName);
        return string.Equals(Path.GetExtension(payloadIcon), ".ico", StringComparison.OrdinalIgnoreCase)
            ? payloadIcon
            : null;
    }

    private static string? GetLogoImagePath()
    {
        var generatedPath = FindLatestGeneratedFile(GeneratedLogoSearchPattern)
            ?? FindExistingFile(Path.Combine(AppContext.BaseDirectory, LegacyGeneratedLogoName));
        if (generatedPath is not null)
        {
            return generatedPath;
        }

        return FindBrandingFile(GetAssetsDirectory(), LargeLogoName);
    }

    private static string? GetSmallLogoImagePath() =>
        FindLatestGeneratedFile(GeneratedIconSearchPattern)
        ?? FindExistingFile(Path.Combine(AppContext.BaseDirectory, LegacyGeneratedIconName))
        ?? FindBrandingFile(GetAssetsDirectory(), SmallLogoName);

    private static bool EnsureGeneratedIcon(string sourcePath, string generatedPath)
    {
        try
        {
            var images = new List<IconImage>();
            foreach (var size in IconSizes)
            {
                using var bitmap = TryLoadImage(sourcePath, size, squareCanvas: true);
                if (bitmap is null)
                {
                    continue;
                }

                using var stream = new MemoryStream();
                bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                images.Add(new IconImage(size, stream.ToArray()));
            }

            if (images.Count == 0)
            {
                return false;
            }

            WriteIconFile(generatedPath, images);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void WriteIconFile(string path, IReadOnlyList<IconImage> images)
    {
        var tempPath = path + ".tmp";
        using (var stream = File.Create(tempPath))
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write((ushort)0);
            writer.Write((ushort)1);
            writer.Write((ushort)images.Count);

            var imageOffset = 6 + (16 * images.Count);
            foreach (var image in images)
            {
                writer.Write((byte)(image.Size >= 256 ? 0 : image.Size));
                writer.Write((byte)(image.Size >= 256 ? 0 : image.Size));
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((ushort)1);
                writer.Write((ushort)32);
                writer.Write(image.Bytes.Length);
                writer.Write(imageOffset);
                imageOffset += image.Bytes.Length;
            }

            foreach (var image in images)
            {
                writer.Write(image.Bytes);
            }
        }

        File.Move(tempPath, path, overwrite: true);
    }

    private static Bitmap? TryLoadImage(string path, int maxSize, bool squareCanvas = false)
    {
        try
        {
            var extension = Path.GetExtension(path);
            if (string.Equals(extension, ".ico", StringComparison.OrdinalIgnoreCase))
            {
                using var icon = new Icon(path, maxSize, maxSize);
                using var bitmap = icon.ToBitmap();
                return squareCanvas ? ResizeToSquareCanvas(bitmap, maxSize) : new Bitmap(bitmap);
            }

            if (string.Equals(extension, ".svg", StringComparison.OrdinalIgnoreCase))
            {
                return RenderSvg(path, maxSize, squareCanvas);
            }

            return RenderRaster(path, maxSize, squareCanvas);
        }
        catch
        {
            return null;
        }
    }

    private static Bitmap? RenderSvg(string path, int maxSize, bool squareCanvas)
    {
        using var svg = new SKSvg();
        if (svg.Load(path) is null || svg.Picture is null)
        {
            return null;
        }

        var sourceBounds = svg.Picture.CullRect;
        var sourceWidth = sourceBounds.Width > 0 ? sourceBounds.Width : maxSize;
        var sourceHeight = sourceBounds.Height > 0 ? sourceBounds.Height : maxSize;
        var canvasWidth = squareCanvas ? maxSize : Math.Max(1, (int)Math.Round(maxSize * sourceWidth / Math.Max(sourceWidth, sourceHeight)));
        var canvasHeight = squareCanvas ? maxSize : Math.Max(1, (int)Math.Round(maxSize * sourceHeight / Math.Max(sourceWidth, sourceHeight)));
        var scale = Math.Min(canvasWidth / sourceWidth, canvasHeight / sourceHeight);
        var drawWidth = sourceWidth * scale;
        var drawHeight = sourceHeight * scale;

        using var bitmap = new SKBitmap(canvasWidth, canvasHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        canvas.Translate((canvasWidth - drawWidth) / 2f, (canvasHeight - drawHeight) / 2f);
        canvas.Scale(scale);
        canvas.Translate(-sourceBounds.Left, -sourceBounds.Top);
        canvas.DrawPicture(svg.Picture);
        canvas.Flush();

        return ToSystemBitmap(bitmap);
    }

    private static Bitmap? RenderRaster(string path, int maxSize, bool squareCanvas)
    {
        using var source = SKBitmap.Decode(path);
        if (source is null || source.Width <= 0 || source.Height <= 0)
        {
            return null;
        }

        var canvasWidth = squareCanvas ? maxSize : Math.Max(1, (int)Math.Round(maxSize * source.Width / (double)Math.Max(source.Width, source.Height)));
        var canvasHeight = squareCanvas ? maxSize : Math.Max(1, (int)Math.Round(maxSize * source.Height / (double)Math.Max(source.Width, source.Height)));
        var scale = Math.Min(canvasWidth / (float)source.Width, canvasHeight / (float)source.Height);
        var drawWidth = source.Width * scale;
        var drawHeight = source.Height * scale;
        var destination = new SKRect(
            (canvasWidth - drawWidth) / 2f,
            (canvasHeight - drawHeight) / 2f,
            (canvasWidth + drawWidth) / 2f,
            (canvasHeight + drawHeight) / 2f);

        using var bitmap = new SKBitmap(canvasWidth, canvasHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        canvas.DrawBitmap(source, destination);
        canvas.Flush();

        return ToSystemBitmap(bitmap);
    }

    private static Bitmap ResizeToSquareCanvas(Bitmap source, int size)
    {
        var target = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(target);
        graphics.Clear(Color.Transparent);
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

        var scale = Math.Min(size / (float)source.Width, size / (float)source.Height);
        var width = source.Width * scale;
        var height = source.Height * scale;
        graphics.DrawImage(source, (size - width) / 2f, (size - height) / 2f, width, height);
        return target;
    }

    private static Bitmap ToSystemBitmap(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = new MemoryStream(data.ToArray());
        using var decoded = Image.FromStream(stream);
        return new Bitmap(decoded);
    }

    private static string? FindBrandingFile(string directory, string baseName)
    {
        try
        {
            if (!Directory.Exists(directory))
            {
                return null;
            }

            return Directory.EnumerateFiles(directory, baseName + ".*", SearchOption.TopDirectoryOnly)
                .Where(path => string.Equals(Path.GetFileNameWithoutExtension(path), baseName, StringComparison.OrdinalIgnoreCase))
                .Where(path => SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                .OrderBy(path => Array.IndexOf(SupportedExtensions, Path.GetExtension(path).ToLowerInvariant()))
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static string GetAssetsDirectory() =>
        Path.Combine(AppContext.BaseDirectory, AssetsDirectoryName);

    private static string GetGeneratedIconName(string sourcePath) =>
        $"small-logo.generated-{ComputeFileHashPrefix(sourcePath)}.ico";

    private static string GetGeneratedLogoName(string sourcePath) =>
        $"large-logo.generated-{ComputeFileHashPrefix(sourcePath)}.png";

    private static string ComputeFileHashPrefix(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash, 0, 8).ToLowerInvariant();
    }

    private static string? FindLatestGeneratedFile(string pattern)
    {
        try
        {
            return Directory.EnumerateFiles(AppContext.BaseDirectory, pattern, SearchOption.TopDirectoryOnly)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static string? FindExistingFile(string path) =>
        File.Exists(path) ? path : null;

    private static void CopyIfNewer(string sourcePath, string destinationPath)
    {
        File.Copy(sourcePath, destinationPath, overwrite: true);
    }

    private static void TryDeleteBrandingPayload(string directory)
    {
        try
        {
            if (!Directory.Exists(directory))
            {
                return;
            }

            foreach (var file in Directory.EnumerateFiles(directory, "*.*", SearchOption.TopDirectoryOnly))
            {
                TryDeleteFile(file);
            }

            if (!Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory);
            }
        }
        catch
        {
        }
    }

    private static void DeleteOldGeneratedFiles(string pattern, string currentPath)
    {
        try
        {
            foreach (var path in Directory.EnumerateFiles(AppContext.BaseDirectory, pattern, SearchOption.TopDirectoryOnly))
            {
                if (!string.Equals(Path.GetFullPath(path), Path.GetFullPath(currentPath), StringComparison.OrdinalIgnoreCase))
                {
                    TryDeleteFile(path);
                }
            }
        }
        catch
        {
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private static void TryRun(Action action)
    {
        try
        {
            action();
        }
        catch
        {
        }
    }

    private static void UpdateShortcutIcons(string? iconPath)
    {
        var exePath = Path.Combine(AppContext.BaseDirectory, AppExeName);
        var iconLocation = File.Exists(iconPath) ? iconPath + ",0" : exePath + ",0";

        foreach (var shortcutPath in GetShortcutPaths())
        {
            TryUpdateShortcutIcon(shortcutPath, iconLocation);
        }
    }

    private static IEnumerable<string> GetShortcutPaths()
    {
        var folders = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar")
        };

        foreach (var folder in folders.Where(folder => !string.IsNullOrWhiteSpace(folder)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            yield return Path.Combine(folder, AppShortcutName);
        }
    }

    private static void TryUpdateShortcutIcon(string shortcutPath, string iconLocation)
    {
        if (!File.Exists(shortcutPath))
        {
            return;
        }

        object? shell = null;
        object? shortcut = null;

        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null)
            {
                return;
            }

            shell = Activator.CreateInstance(shellType);
            if (shell is null)
            {
                return;
            }

            shortcut = shellType.InvokeMember(
                "CreateShortcut",
                BindingFlags.InvokeMethod,
                null,
                shell,
                [shortcutPath]);

            shortcut?.GetType().InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, [iconLocation]);
            shortcut?.GetType().InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
        }
        catch
        {
        }
        finally
        {
            ReleaseComObject(shortcut);
            ReleaseComObject(shell);
        }
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.FinalReleaseComObject(value);
        }
    }

    private static void NotifyShellIconCacheChanged()
    {
        try
        {
            SHChangeNotify(ShellChangeNotifyAssocChanged, 0, IntPtr.Zero, IntPtr.Zero);
        }
        catch
        {
        }
    }

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    private sealed class AppliedIcon(Icon icon) : IDisposable
    {
        public void Dispose() => icon.Dispose();
    }

    private sealed record IconImage(int Size, byte[] Bytes);
}
