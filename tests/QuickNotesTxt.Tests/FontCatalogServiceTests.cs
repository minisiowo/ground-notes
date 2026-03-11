using QuickNotesTxt.Services;
using Xunit;

namespace QuickNotesTxt.Tests;

public sealed class FontCatalogServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "QuickNotesTxt.Tests", Guid.NewGuid().ToString("N"));
    private readonly string _fontsDir;
    private readonly FontCatalogService _service;

    public FontCatalogServiceTests()
    {
        _fontsDir = Path.Combine(_tempRoot, "Assets", "Fonts");
        _service = new FontCatalogService(_fontsDir);
    }

    [Fact]
    public void LoadBundledFonts_ReturnsDefaultWhenDirectoryDoesNotExist()
    {
        var fonts = _service.LoadBundledFonts();

        var font = Assert.Single(fonts);
        Assert.Equal(FontCatalogService.DefaultFontKey, font.Key);
        Assert.Equal("Iosevka Slab", font.DisplayName);
    }

    [Fact]
    public void LoadBundledFonts_LoadsValidFontFoldersInDeterministicOrder()
    {
        CreateFontFolder("IosevkaSerif", "IosevkaSerif-Regular.ttf");
        CreateFontFolder("IosevkaSlab", "IosevkaSlab-Regular.ttf");

        var fonts = _service.LoadBundledFonts();

        Assert.Collection(fonts,
            font =>
            {
                Assert.Equal("Iosevka Serif", font.DisplayName);
                Assert.Equal("avares://QuickNotesTxt/Assets/Fonts/IosevkaSerif#Iosevka Serif", font.ResourceUri);
            },
            font =>
            {
                Assert.Equal("Iosevka Slab", font.DisplayName);
                Assert.Equal("avares://QuickNotesTxt/Assets/Fonts/IosevkaSlab#Iosevka Slab", font.ResourceUri);
            });
    }

    [Fact]
    public void LoadBundledFonts_IgnoresFoldersWithoutSupportedFontFiles()
    {
        Directory.CreateDirectory(Path.Combine(_fontsDir, "EmptyFont"));
        CreateFontFolder("IosevkaSlab", "IosevkaSlab-Regular.ttf");
        File.WriteAllText(Path.Combine(_fontsDir, "EmptyFont", "readme.txt"), "not a font");

        var fonts = _service.LoadBundledFonts();

        var font = Assert.Single(fonts);
        Assert.Equal("IosevkaSlab", font.Key);
    }

    private void CreateFontFolder(string folderName, string fileName)
    {
        var folderPath = Path.Combine(_fontsDir, folderName);
        Directory.CreateDirectory(folderPath);
        File.WriteAllText(Path.Combine(folderPath, fileName), "font");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
