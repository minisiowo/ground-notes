using GroundNotes.Services;
using Xunit;

namespace GroundNotes.Tests;

public sealed class TagSuggestionHelperTests
{
    [Fact]
    public void GetSuggestions_UsesCurrentCommaSeparatedFragment()
    {
        var suggestions = TagSuggestionHelper.GetSuggestions("ops, de", 7, ["ops", "deploy", "debug"]);

        Assert.Equal(new[] { "debug", "deploy" }, suggestions.ToArray());
    }

    [Fact]
    public void GetSuggestions_ExcludesAlreadySelectedTagsFromOtherSegments()
    {
        var suggestions = TagSuggestionHelper.GetSuggestions("ops, de", 7, ["ops", "deploy"]);

        Assert.Equal(new[] { "deploy" }, suggestions.ToArray());
    }

    [Fact]
    public void ApplySuggestion_ReplacesCurrentSegment_AndNormalizesSpacing()
    {
        var result = TagSuggestionHelper.ApplySuggestion("ops,de", 6, "deploy");

        Assert.Equal("ops, deploy", result.Text);
        Assert.Equal("ops, deploy".Length, result.CaretIndex);
    }

    [Fact]
    public void ApplySuggestion_ReplacesMiddleSegment()
    {
        var result = TagSuggestionHelper.ApplySuggestion("ops, de, later", 7, "deploy");

        Assert.Equal("ops, deploy, later", result.Text);
        Assert.Equal("ops, deploy".Length, result.CaretIndex);
    }
}
