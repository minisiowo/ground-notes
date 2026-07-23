using System.Text.Json;

namespace GroundNotes.Services;

public sealed class TagFolderCatalogService : ITagFolderCatalogService
{
    private const string AppDataDirectoryName = ".groundnotes";
    private const string CatalogFileName = "tag-folders.json";

    private readonly SemaphoreSlim _catalogLock = new(1, 1);

    public async Task<IReadOnlyList<string>> LoadAsync(
        string notesFolder,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(notesFolder);

        await _catalogLock.WaitAsync(cancellationToken);
        try
        {
            var filePath = GetCatalogFilePath(notesFolder);
            if (!File.Exists(filePath))
            {
                return [];
            }

            await using var stream = File.OpenRead(filePath);
            var paths = await JsonSerializer.DeserializeAsync<List<string>>(
                stream,
                cancellationToken: cancellationToken);
            return NormalizePaths(paths ?? []);
        }
        finally
        {
            _catalogLock.Release();
        }
    }

    public async Task SaveAsync(
        string notesFolder,
        IEnumerable<string> folderPaths,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(notesFolder);
        ArgumentNullException.ThrowIfNull(folderPaths);

        var normalizedPaths = NormalizePaths(folderPaths);
        await _catalogLock.WaitAsync(cancellationToken);
        try
        {
            var filePath = GetCatalogFilePath(notesFolder);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

            var temporaryFilePath = $"{filePath}.{Guid.NewGuid():N}.tmp";
            try
            {
                await using (var stream = File.Create(temporaryFilePath))
                {
                    await JsonSerializer.SerializeAsync(
                        stream,
                        normalizedPaths,
                        cancellationToken: cancellationToken);
                }

                File.Move(temporaryFilePath, filePath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryFilePath))
                {
                    File.Delete(temporaryFilePath);
                }
            }
        }
        finally
        {
            _catalogLock.Release();
        }
    }

    private static string GetCatalogFilePath(string notesFolder)
    {
        return Path.Combine(notesFolder, AppDataDirectoryName, CatalogFileName);
    }

    private static IReadOnlyList<string> NormalizePaths(IEnumerable<string> paths)
    {
        return paths
            .Select(NormalizePath)
            .Where(static path => path is not null)
            .Select(static path => path!)
            .GroupBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.OrderBy(static path => path, StringComparer.Ordinal).First())
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static path => path, StringComparer.Ordinal)
            .ToList();
    }

    private static string? NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var segments = path.Split(
            '/',
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return segments.Length == 0 ? null : string.Join('/', segments);
    }
}
