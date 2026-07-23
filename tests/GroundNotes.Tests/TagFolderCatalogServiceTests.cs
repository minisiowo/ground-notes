using System.Text.Json;
using GroundNotes.Services;
using Xunit;

namespace GroundNotes.Tests;

public sealed class TagFolderCatalogServiceTests : IDisposable
{
    private readonly string _notesFolder = Path.Combine(
        Path.GetTempPath(),
        "GroundNotes.Tests",
        Guid.NewGuid().ToString("N"));
    private readonly TagFolderCatalogService _service = new();

    [Fact]
    public async Task LoadAsync_ReturnsEmptyCatalogWhenFileDoesNotExist()
    {
        var paths = await _service.LoadAsync(_notesFolder);

        Assert.Empty(paths);
    }

    [Fact]
    public async Task SaveAsync_PersistsNormalizedCaseInsensitiveUniquePaths()
    {
        await _service.SaveAsync(
            _notesFolder,
            [" Work / Alpha ", "work/alpha", "Personal///Ideas", " ", "///"]);

        var loaded = await _service.LoadAsync(_notesFolder);
        Assert.Equal(["Personal/Ideas", "Work/Alpha"], loaded);

        var catalogFile = Path.Combine(_notesFolder, ".groundnotes", "tag-folders.json");
        Assert.True(File.Exists(catalogFile));
        var persisted = JsonSerializer.Deserialize<List<string>>(await File.ReadAllTextAsync(catalogFile));
        Assert.Equal(["Personal/Ideas", "Work/Alpha"], persisted);
        Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(catalogFile)!, "*.tmp"));
    }

    [Fact]
    public async Task LoadAsync_NormalizesExistingCatalog()
    {
        var catalogDirectory = Path.Combine(_notesFolder, ".groundnotes");
        Directory.CreateDirectory(catalogDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(catalogDirectory, "tag-folders.json"),
            """[" zeta / child ", "ZETA/child", "Alpha"]""");

        var loaded = await _service.LoadAsync(_notesFolder);

        Assert.Equal(["Alpha", "ZETA/child"], loaded);
    }

    public void Dispose()
    {
        if (Directory.Exists(_notesFolder))
        {
            Directory.Delete(_notesFolder, recursive: true);
        }
    }
}
