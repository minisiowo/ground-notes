using GroundNotes.Styles;
using Xunit;

namespace GroundNotes.Tests;

public sealed class ThemeBuilderTests
{
    [Fact]
    public void BuildTokens_UsesPaletteAndOverrides()
    {
        var theme = new AppTheme
        {
            Name = "Test",
            IsLight = false,
            Palette = CreatePalette(),
            Overrides = new ThemeTokenOverrides
            {
                MarkdownLinkLabel = "#ABCDEF",
                MarkdownLinkUrl = "#778899"
            }
        };

        var tokens = ThemeBuilder.BuildTokens(theme);

        Assert.Equal("#ABCDEF", tokens.MarkdownLinkLabel);
        Assert.Equal("#778899", tokens.MarkdownLinkUrl);
        Assert.Equal(theme.Palette.AppBackground, tokens.AppBackground);
        Assert.Equal(theme.Palette.PrimaryText, tokens.PrimaryText);
    }

    [Fact]
    public void BuildTokens_DerivesUrlColorDifferentFromLabelWithoutOverride()
    {
        var tokens = ThemeBuilder.BuildTokens(new AppTheme
        {
            Name = "Test",
            IsLight = false,
            Palette = CreatePalette()
        });

        Assert.NotEqual(tokens.MarkdownLinkLabel, tokens.MarkdownLinkUrl);
        Assert.NotEqual(tokens.SecondaryText, tokens.MarkdownLinkUrl);
    }

    [Fact]
    public void BuildTokens_MenuSurfaceDiffersFromPaneBackground()
    {
        var tokens = ThemeBuilder.BuildTokens(new AppTheme
        {
            Name = "Test",
            IsLight = false,
            Palette = CreatePalette()
        });

        Assert.NotEqual(tokens.PaneBackground, tokens.MenuSurface);
    }

    [Fact]
    public void BuildResources_ContainsMarkdownLinkResourcesAndAccentColors()
    {
        var resources = ThemeBuilder.BuildResources(new AppTheme
        {
            Name = "Test",
            IsLight = true,
            Palette = CreatePalette()
        });

        Assert.Contains(ThemeKeys.MarkdownLinkLabelBrush, resources.Brushes.Keys);
        Assert.Contains(ThemeKeys.MarkdownLinkUrlBrush, resources.Brushes.Keys);
        Assert.Contains(ThemeKeys.MenuSurfaceBrush, resources.Brushes.Keys);
        Assert.Contains(ThemeKeys.SystemAccentColor, resources.Colors.Keys);
        Assert.Contains(ThemeKeys.SystemAccentColorLight1, resources.Colors.Keys);
    }

    private static ThemePalette CreatePalette()
    {
        return new ThemePalette
        {
            AppBackground = "#202020",
            PaneBackground = "#252525",
            SurfaceBackground = "#202020",
            SurfaceHover = "#2A2A2A",
            SurfacePressed = "#161616",
            SurfaceRaised = "#303030",
            BorderBase = "#444444",
            PrimaryText = "#F0F0F0",
            SecondaryText = "#B0B0B0",
            MutedText = "#8B8B8B",
            PlaceholderText = "#6A6A6A",
            Accent = "#D97A5C",
            AccentSoft = "#C7A977",
            SelectionBackground = "#3A2B22",
            TextSelectionBrush = "#3A2B22",
            EditorTextSelectionBrush = "#66D97A5C",
            Success = "#8CB47A",
            Warning = "#D0A15B",
            Danger = "#932F1E"
        };
    }
}
