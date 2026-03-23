using System.Text.Json;
using GroundNotes.Services;
using GroundNotes.Styles;
using Xunit;

namespace GroundNotes.Tests;

public sealed class ThemeLoaderServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "GroundNotes.Tests", Guid.NewGuid().ToString("N"));
    private readonly string _themesDir;
    private readonly ThemeLoaderService _service;

    public ThemeLoaderServiceTests()
    {
        _themesDir = Path.Combine(_tempRoot, "themes");
        _service = new ThemeLoaderService(_themesDir);
    }

    [Fact]
    public async Task LoadAllThemesAsync_ReturnsBuiltInsWhenNoDirExists()
    {
        var themes = await _service.LoadAllThemesAsync();

        Assert.Equal(AppTheme.BuiltInThemes.Count, themes.Count);
        Assert.All(AppTheme.BuiltInThemes, builtIn =>
            Assert.Contains(themes, t => t.Name == builtIn.Name));
    }

    [Fact]
    public async Task LoadAllThemesAsync_LoadsValidCustomThemeV2()
    {
        WriteThemeFile("custom-v2.json", MakeValidThemeV2Json("My Custom Theme"));

        var themes = await _service.LoadAllThemesAsync();

        Assert.Equal(AppTheme.BuiltInThemes.Count + 1, themes.Count);
        Assert.Contains(themes, t => t.Name == "My Custom Theme");
    }

    [Fact]
    public async Task LoadAllThemesAsync_LoadsLegacyFlatTheme()
    {
        WriteThemeFile("custom-legacy.json", MakeValidLegacyThemeJson("Legacy Theme"));

        var themes = await _service.LoadAllThemesAsync();

        var loaded = Assert.Single(themes.Where(t => t.Name == "Legacy Theme"));
        var tokens = ThemeBuilder.BuildTokens(loaded);
        Assert.Equal("#67B7FF", tokens.MarkdownLinkLabel);
        Assert.Equal("#8899AA", tokens.MarkdownLinkUrl);
    }

    [Fact]
    public async Task LoadAllThemesAsync_SkipsMalformedJson()
    {
        Directory.CreateDirectory(_themesDir);
        await File.WriteAllTextAsync(Path.Combine(_themesDir, "bad.json"), "{ not valid json!!!");

        var themes = await _service.LoadAllThemesAsync();

        Assert.Equal(AppTheme.BuiltInThemes.Count, themes.Count);
    }

    [Fact]
    public async Task LoadAllThemesAsync_SkipsJsonMissingRequiredFields()
    {
        Directory.CreateDirectory(_themesDir);
        var partial = new { name = "Incomplete", isLight = false, palette = new { appBackground = "#000000" } };
        await File.WriteAllTextAsync(
            Path.Combine(_themesDir, "incomplete.json"),
            JsonSerializer.Serialize(partial));

        var themes = await _service.LoadAllThemesAsync();

        Assert.Equal(AppTheme.BuiltInThemes.Count, themes.Count);
    }

    [Fact]
    public async Task LoadAllThemesAsync_SkipsThemeMissingLegacyMarkdownToken()
    {
        Directory.CreateDirectory(_themesDir);
        var theme = JsonSerializer.Deserialize<Dictionary<string, object?>>(MakeValidLegacyThemeJson("Missing Token"))!;
        theme.Remove("markdownLinkLabel");

        await File.WriteAllTextAsync(
            Path.Combine(_themesDir, "missing-token.json"),
            JsonSerializer.Serialize(theme));

        var themes = await _service.LoadAllThemesAsync();

        Assert.Equal(AppTheme.BuiltInThemes.Count, themes.Count);
    }

    [Fact]
    public async Task LoadAllThemesAsync_SkipsBuiltInNameCollision()
    {
        WriteThemeFile("dark.json", MakeValidThemeV2Json("Dark"));

        var themes = await _service.LoadAllThemesAsync();

        Assert.Equal(AppTheme.BuiltInThemes.Count, themes.Count);
    }

    [Fact]
    public async Task LoadAllThemesAsync_SkipsBuiltInNameCollisionCaseInsensitive()
    {
        WriteThemeFile("dark.json", MakeValidThemeV2Json("dark"));

        var themes = await _service.LoadAllThemesAsync();

        Assert.Equal(AppTheme.BuiltInThemes.Count, themes.Count);
    }

    [Fact]
    public async Task LoadAllThemesAsync_SkipsInvalidHexColor()
    {
        WriteThemeFile("badcolor.json", MakeValidThemeV2Json("Bad Color", accent: "not-a-color"));

        var themes = await _service.LoadAllThemesAsync();

        Assert.Equal(AppTheme.BuiltInThemes.Count, themes.Count);
    }

    [Fact]
    public async Task ExportThemeAsync_RoundTripsV2()
    {
        var original = new AppTheme
        {
            Name = "Exported Theme",
            IsLight = true,
            Palette = CreatePalette(),
            Overrides = new ThemeTokenOverrides
            {
                MarkdownHeading1 = "#005FB8",
                MarkdownLinkLabel = "#005FB8",
                MarkdownLinkUrl = "#68727D",
                TitleBarCloseHover = "#C42B1C"
            }
        };

        await _service.ExportThemeAsync(original);
        var themes = await _service.LoadAllThemesAsync();

        var loaded = themes.FirstOrDefault(t => t.Name == "Exported Theme");
        Assert.NotNull(loaded);
        Assert.Equal(original.IsLight, loaded.IsLight);
        Assert.Equal(original.Palette.AppBackground, loaded.Palette.AppBackground);
        Assert.Equal(original.Palette.Accent, loaded.Palette.Accent);
        Assert.Equal(original.Overrides!.MarkdownLinkUrl, loaded.Overrides!.MarkdownLinkUrl);
    }

    private void WriteThemeFile(string fileName, string json)
    {
        Directory.CreateDirectory(_themesDir);
        File.WriteAllText(Path.Combine(_themesDir, fileName), json);
    }

    private static string MakeValidThemeV2Json(string name, string accent = "#4AA3FF")
    {
        var theme = new
        {
            name,
            isLight = false,
            palette = new
            {
                appBackground = "#1E1E1E",
                paneBackground = "#252526",
                surfaceBackground = "#1E1E1E",
                surfaceHover = "#2A2D2E",
                surfacePressed = "#1A1A1A",
                surfaceRaised = "#2D2D2D",
                borderBase = "#3C3C3C",
                primaryText = "#D4D4D4",
                secondaryText = "#9D9D9D",
                mutedText = "#6A6A6A",
                placeholderText = "#5A5A5A",
                accent,
                accentSoft = "#7FB0D9",
                selectionBackground = "#264F78",
                textSelectionBrush = "#264F78",
                editorTextSelectionBrush = "#66007ACC",
                success = "#6CB7A8",
                warning = "#D1B86F",
                danger = "#E81123"
            },
            overrides = new
            {
                selectionBorder = "#007ACC",
                focusBorder = "#007ACC",
                markdownHeading2 = "#6CB7A8",
                markdownHeading3 = "#8FB7A3",
                markdownLinkLabel = "#67B7FF",
                markdownLinkUrl = "#8899AA"
            }
        };

        return JsonSerializer.Serialize(theme, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string MakeValidLegacyThemeJson(string name, string appBackground = "#1E1E1E")
    {
        var theme = new
        {
            name,
            isLight = false,
            appBackground,
            paneBackground = "#252526",
            surfaceBackground = "#1E1E1E",
            surfaceHover = "#2A2D2E",
            surfacePressed = "#1A1A1A",
            surfaceRaised = "#2D2D2D",
            selectionBackground = "#264F78",
            selectionBorder = "#007ACC",
            textSelectionBrush = "#264F78",
            editorTextSelectionBrush = "#264F78",
            borderBase = "#3C3C3C",
            focusBorder = "#007ACC",
            primaryText = "#D4D4D4",
            secondaryText = "#9D9D9D",
            mutedText = "#6A6A6A",
            placeholderText = "#5A5A5A",
            editorText = "#D4D4D4",
            appText = "#CCCCCC",
            markdownHeading1 = "#4AA3FF",
            markdownHeading2 = "#6CB7A8",
            markdownHeading3 = "#8FB7A3",
            markdownLinkLabel = "#67B7FF",
            markdownLinkUrl = "#8899AA",
            markdownTaskDone = "#7DAA88",
            markdownTaskPending = "#D1B86F",
            markdownStrikethrough = "#7E7E7E",
            markdownRule = "#4C5662",
            markdownBlockquote = "#A5B6C7",
            markdownFenceMarker = "#5D7185",
            markdownFenceInfo = "#7FB0D9",
            markdownInlineCodeForeground = "#7CC0FF",
            markdownInlineCodeBackground = "#1A2530",
            markdownCodeBlockForeground = "#A9D3FF",
            markdownCodeBlockBackground = "#18222B",
            titleBarButtonHover = "#333333",
            titleBarCloseHover = "#E81123",
        };

        return JsonSerializer.Serialize(theme, new JsonSerializerOptions { WriteIndented = true });
    }

    private static ThemePalette CreatePalette()
    {
        return new ThemePalette
        {
            AppBackground = "#FFFFFF",
            PaneBackground = "#FAFAFA",
            SurfaceBackground = "#F5F5F5",
            SurfaceHover = "#EEEEEE",
            SurfacePressed = "#E0E0E0",
            SurfaceRaised = "#F0F0F0",
            BorderBase = "#C8C8C8",
            PrimaryText = "#1B1B1B",
            SecondaryText = "#5A5A5A",
            MutedText = "#8A8A8A",
            PlaceholderText = "#9E9E9E",
            Accent = "#005FB8",
            AccentSoft = "#4C647A",
            SelectionBackground = "#CCE5FF",
            TextSelectionBrush = "#ADD6FF",
            EditorTextSelectionBrush = "#665C86B8",
            Success = "#356859",
            Warning = "#7A5C00",
            Danger = "#C42B1C"
        };
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
