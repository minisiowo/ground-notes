using AvaloniaEdit;
using GroundNotes.Views;
using Xunit;

namespace GroundNotes.Tests;

public sealed class EditorThemeControllerTests
{
    [Fact]
    public void ConfigureEditorOptions_DisablesBuiltInHyperlinkRendering()
    {
        var options = new TextEditorOptions();

        EditorThemeController.ConfigureEditorOptions(options);

        Assert.False(options.ConvertTabsToSpaces);
        Assert.False(options.EnableRectangularSelection);
        Assert.False(options.EnableHyperlinks);
        Assert.False(options.EnableEmailHyperlinks);
        Assert.False(options.RequireControlModifierForHyperlinkClick);
    }
}
