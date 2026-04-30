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

    public bool IsManagedAssetPath(string? notesFolderPath, string? assetPath)
    {
        if (string.IsNullOrWhiteSpace(notesFolderPath) || string.IsNullOrWhiteSpace(assetPath))
        {
            return false;
        }

        var assetsDirectoryPath = Path.GetFullPath(Path.Combine(notesFolderPath, AssetsDirectoryName));
        var fullAssetPath = Path.GetFullPath(assetPath);
        return string.Equals(Path.GetDirectoryName(fullAssetPath), assetsDirectoryPath, StringComparison.OrdinalIgnoreCase);
    }

    public string BuildAssetMarkdownPath(string assetFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetFileName);
        return $"{AssetsDirectoryName}/{assetFileName.Replace('\\', '/')}";
    }

    public bool TryBuildRenameAssetPath(
        string notesFolderPath,
        string currentAssetPath,
        string requestedFileName,
        out string newAssetPath,
        out string newMarkdownPath,
        out string errorMessage)
    {
        newAssetPath = string.Empty;
        newMarkdownPath = string.Empty;
        errorMessage = string.Empty;

        if (!IsManagedAssetPath(notesFolderPath, currentAssetPath))
        {
            errorMessage = "Only images from this note folder's assets directory can be renamed.";
            return false;
        }

        var sanitizedFileName = NormalizeAssetRenameFileName(requestedFileName, Path.GetExtension(currentAssetPath));
        if (sanitizedFileName is null)
        {
            errorMessage = "Use a file name without folders or invalid characters.";
            return false;
        }

        var assetsDirectoryPath = Path.GetFullPath(Path.Combine(notesFolderPath, AssetsDirectoryName));
        var candidatePath = Path.GetFullPath(Path.Combine(assetsDirectoryPath, sanitizedFileName));
        if (!IsManagedAssetPath(notesFolderPath, candidatePath))
        {
            errorMessage = "Use a file name inside the assets directory.";
            return false;
        }

        if (string.Equals(Path.GetFullPath(currentAssetPath), candidatePath, StringComparison.OrdinalIgnoreCase))
        {
            errorMessage = "The image already has that name.";
            return false;
        }

        if (File.Exists(candidatePath))
        {
            errorMessage = "An image with that name already exists.";
            return false;
        }

        newAssetPath = candidatePath;
        newMarkdownPath = BuildAssetMarkdownPath(sanitizedFileName);
        return true;
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

    internal static string? NormalizeAssetRenameFileName(string requestedFileName, string fallbackExtension)
    {
        var trimmed = requestedFileName.Trim();
        if (string.IsNullOrWhiteSpace(trimmed)
            || trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || trimmed.Contains(Path.DirectorySeparatorChar)
            || trimmed.Contains(Path.AltDirectorySeparatorChar))
        {
            return null;
        }

        var fileName = Path.GetFileName(trimmed);
        if (!string.Equals(fileName, trimmed, StringComparison.Ordinal) || string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(Path.GetExtension(fileName)))
        {
            fileName += fallbackExtension;
        }

        return fileName;
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
