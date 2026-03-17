using System.Text.Json;
using Xunit;
using QuickNotesTxt.Services;
using QuickNotesTxt.Styles;

namespace QuickNotesTxt.Tests;

public sealed class ThemeLoaderServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "QuickNotesTxt.Tests", Guid.NewGuid().ToString("N"));
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
    public async Task LoadAllThemesAsync_LoadsValidCustomTheme()
    {
        WriteThemeFile("custom.json", MakeValidThemeJson("My Custom Theme"));

        var themes = await _service.LoadAllThemesAsync();

        Assert.Equal(AppTheme.BuiltInThemes.Count + 1, themes.Count);
        Assert.Contains(themes, t => t.Name == "My Custom Theme");
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
        var partial = new { name = "Incomplete", isLight = false, appBackground = "#000000" };
        await File.WriteAllTextAsync(
            Path.Combine(_themesDir, "incomplete.json"),
            JsonSerializer.Serialize(partial));

        var themes = await _service.LoadAllThemesAsync();

        Assert.Equal(AppTheme.BuiltInThemes.Count, themes.Count);
    }

    [Fact]
    public async Task LoadAllThemesAsync_SkipsThemeMissingEditorModeToken()
    {
        Directory.CreateDirectory(_themesDir);
        var theme = JsonSerializer.Deserialize<Dictionary<string, object?>>(MakeValidThemeJson("Missing Token"))!;
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
        WriteThemeFile("dark.json", MakeValidThemeJson("Dark"));

        var themes = await _service.LoadAllThemesAsync();

        Assert.Equal(AppTheme.BuiltInThemes.Count, themes.Count);
    }

    [Fact]
    public async Task LoadAllThemesAsync_SkipsBuiltInNameCollisionCaseInsensitive()
    {
        WriteThemeFile("dark.json", MakeValidThemeJson("dark"));

        var themes = await _service.LoadAllThemesAsync();

        Assert.Equal(AppTheme.BuiltInThemes.Count, themes.Count);
    }

    [Fact]
    public async Task LoadAllThemesAsync_SkipsInvalidHexColor()
    {
        WriteThemeFile("badcolor.json", MakeValidThemeJson("Bad Color", appBackground: "not-a-color"));

        var themes = await _service.LoadAllThemesAsync();

        Assert.Equal(AppTheme.BuiltInThemes.Count, themes.Count);
    }

    [Fact]
    public async Task ExportThemeAsync_RoundTrips()
    {
        var original = new AppTheme
        {
            Name = "Exported Theme",
            IsLight = true,
            AppBackground = "#FFFFFF",
            PaneBackground = "#FAFAFA",
            SurfaceBackground = "#F5F5F5",
            SurfaceHover = "#EEEEEE",
            SurfacePressed = "#E0E0E0",
            SurfaceRaised = "#F0F0F0",
            SelectionBackground = "#CCE5FF",
            SelectionBorder = "#0078D4",
            TextSelectionBrush = "#ADD6FF",
            EditorTextSelectionBrush = "#ADD6FF",
            BorderBase = "#C8C8C8",
            FocusBorder = "#005FB8",
            PrimaryText = "#1B1B1B",
            SecondaryText = "#5A5A5A",
            MutedText = "#8A8A8A",
            PlaceholderText = "#9E9E9E",
            EditorText = "#1B1B1B",
            AppText = "#1B1B1B",
            MarkdownHeading1 = "#005FB8",
            MarkdownHeading2 = "#7A5C00",
            MarkdownHeading3 = "#356859",
            MarkdownLinkLabel = "#005FB8",
            MarkdownLinkUrl = "#5A5A5A",
            MarkdownTaskDone = "#356859",
            MarkdownTaskPending = "#7A5C00",
            MarkdownStrikethrough = "#757575",
            MarkdownRule = "#B8C2CC",
            MarkdownBlockquote = "#4C647A",
            MarkdownFenceMarker = "#7F8B96",
            MarkdownFenceInfo = "#356859",
            MarkdownInlineCodeForeground = "#004B91",
            MarkdownInlineCodeBackground = "#E7EEF5",
            MarkdownCodeBlockForeground = "#1E3A5F",
            MarkdownCodeBlockBackground = "#E1E8EF",
            TitleBarButtonHover = "#E5E5E5",
            TitleBarCloseHover = "#C42B1C",
        };

        await _service.ExportThemeAsync(original);
        var themes = await _service.LoadAllThemesAsync();

        var loaded = themes.FirstOrDefault(t => t.Name == "Exported Theme");
        Assert.NotNull(loaded);
        Assert.Equal(original.IsLight, loaded.IsLight);
        Assert.Equal(original.AppBackground, loaded.AppBackground);
        Assert.Equal(original.PrimaryText, loaded.PrimaryText);
        Assert.Equal(original.MarkdownHeading1, loaded.MarkdownHeading1);
        Assert.Equal(original.MarkdownHeading2, loaded.MarkdownHeading2);
        Assert.Equal(original.MarkdownLinkLabel, loaded.MarkdownLinkLabel);
        Assert.Equal(original.MarkdownTaskDone, loaded.MarkdownTaskDone);
        Assert.Equal(original.MarkdownRule, loaded.MarkdownRule);
        Assert.Equal(original.MarkdownInlineCodeForeground, loaded.MarkdownInlineCodeForeground);
        Assert.Equal(original.MarkdownCodeBlockBackground, loaded.MarkdownCodeBlockBackground);
        Assert.Equal(original.TitleBarCloseHover, loaded.TitleBarCloseHover);
    }

    private void WriteThemeFile(string fileName, string json)
    {
        Directory.CreateDirectory(_themesDir);
        File.WriteAllText(Path.Combine(_themesDir, fileName), json);
    }

    private static string MakeValidThemeJson(string name, string appBackground = "#1E1E1E")
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

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
