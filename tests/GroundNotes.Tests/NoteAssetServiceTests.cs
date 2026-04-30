using GroundNotes.Services;
using Xunit;

namespace GroundNotes.Tests;

public sealed class NoteAssetServiceTests
{
    [Fact]
    public void BuildMarkdownImageReference_UsesAssetsPathAndScale()
    {
        var service = new NoteAssetService();

        var reference = service.BuildMarkdownImageReference("photo.png", 50);

        Assert.Equal("![](assets/photo.png)|50", reference);
    }

    [Fact]
    public void ResolveImagePath_ResolvesRelativePathAgainstBaseDirectory()
    {
        var service = new NoteAssetService();
        var baseDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        var resolved = service.ResolveImagePath(baseDirectory, "assets/photo.png");

        Assert.Equal(Path.Combine(baseDirectory, "assets", "photo.png"), resolved);
    }

    [Fact]
    public void BuildAssetFileName_UsesDeterministicPngFormat()
    {
        var timestamp = new DateTimeOffset(2026, 4, 1, 12, 34, 56, 789, TimeSpan.Zero);

        var fileName = NoteAssetService.BuildAssetFileName(timestamp);

        Assert.Equal("image-20260401-123456789.png", fileName);
    }

    [Fact]
    public void IsManagedAssetPath_ReturnsTrueOnlyForDirectAssets()
    {
        var service = new NoteAssetService();
        var baseDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        Assert.True(service.IsManagedAssetPath(baseDirectory, Path.Combine(baseDirectory, "assets", "photo.png")));
        Assert.False(service.IsManagedAssetPath(baseDirectory, Path.Combine(baseDirectory, "photo.png")));
        Assert.False(service.IsManagedAssetPath(baseDirectory, Path.Combine(baseDirectory, "other", "photo.png")));
    }

    [Fact]
    public void TryBuildRenameAssetPath_AddsCurrentExtensionAndMarkdownPath()
    {
        var service = new NoteAssetService();
        var baseDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var currentPath = Path.Combine(baseDirectory, "assets", "photo.png");

        var result = service.TryBuildRenameAssetPath(baseDirectory, currentPath, "renamed", out var newPath, out var markdownPath, out var error);

        Assert.True(result, error);
        Assert.Equal(Path.Combine(baseDirectory, "assets", "renamed.png"), newPath);
        Assert.Equal("assets/renamed.png", markdownPath);
    }

    [Fact]
    public void NormalizeAssetRenameFileName_RejectsUnsafeNames()
    {
        Assert.Null(NoteAssetService.NormalizeAssetRenameFileName("../photo.png", ".png"));
        Assert.Null(NoteAssetService.NormalizeAssetRenameFileName("folder/photo.png", ".png"));
        Assert.Null(NoteAssetService.NormalizeAssetRenameFileName(" ", ".png"));
        Assert.Equal("photo.png", NoteAssetService.NormalizeAssetRenameFileName("photo", ".png"));
    }
}
