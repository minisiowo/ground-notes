using Avalonia.Media.Imaging;

namespace GroundNotes.Services;

internal sealed class NoteAssetService
{
    private const string AssetsDirectoryName = "assets";

    public string BuildMarkdownImageReference(string assetFileName, int scalePercent = 100)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetFileName);

        var sanitizedScale = Math.Clamp(scalePercent, 1, 400);
        return $"![]({AssetsDirectoryName}/{assetFileName})|{sanitizedScale}";
    }

    public string? ResolveImagePath(string? baseDirectoryPath, string relativeOrAbsolutePath)
    {
        if (string.IsNullOrWhiteSpace(relativeOrAbsolutePath))
        {
            return null;
        }

        if (Path.IsPathRooted(relativeOrAbsolutePath))
        {
            return relativeOrAbsolutePath;
        }

        if (string.IsNullOrWhiteSpace(baseDirectoryPath))
        {
            return null;
        }

        return Path.GetFullPath(Path.Combine(baseDirectoryPath, relativeOrAbsolutePath));
    }

    public async Task<string> SaveBitmapAsync(string notesFolderPath, Bitmap bitmap, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(notesFolderPath);
        ArgumentNullException.ThrowIfNull(bitmap);

        cancellationToken.ThrowIfCancellationRequested();

        var assetsDirectoryPath = Path.Combine(notesFolderPath, AssetsDirectoryName);
        Directory.CreateDirectory(assetsDirectoryPath);

        var fileName = BuildAssetFileName(DateTimeOffset.Now);
        var filePath = GetUniqueAssetPath(assetsDirectoryPath, fileName);

        await using var stream = File.Create(filePath);
        bitmap.Save(stream, quality: null);
        await stream.FlushAsync(cancellationToken);

        return Path.GetFileName(filePath);
    }

    internal static string BuildAssetFileName(DateTimeOffset timestamp)
    {
        return $"image-{timestamp:yyyyMMdd-HHmmssfff}.png";
    }

    private static string GetUniqueAssetPath(string assetsDirectoryPath, string fileName)
    {
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var candidatePath = Path.Combine(assetsDirectoryPath, fileName);
        var suffix = 1;

        while (File.Exists(candidatePath))
        {
            candidatePath = Path.Combine(assetsDirectoryPath, $"{baseName}-{suffix}{extension}");
            suffix++;
        }

        return candidatePath;
    }
}
