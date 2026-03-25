using System.Reflection;
using System.Runtime.CompilerServices;
using AvaloniaEdit.Document;
using GroundNotes.Editors;
using Xunit;

namespace GroundNotes.Tests;

public sealed class MarkdownColorizingTransformerTests
{
    [Fact]
    public void InvalidateResourceCache_ClearsCachedResources()
    {
        using var colorizer = new MarkdownColorizingTransformer();
        SeedResourceCache(colorizer);

        colorizer.InvalidateResourceCache();

        Assert.Null(GetResourceCache(colorizer));
    }

    [Fact]
    public void QueryIsFencedCodeLine_ReturnsTrueForFenceContentAndBlankInnerLines()
    {
        using var colorizer = new MarkdownColorizingTransformer();
        var document = new TextDocument("before\n```csharp\ncode\n\nmore\n```\nafter");

        Assert.False(colorizer.QueryIsFencedCodeLine(document, 1));
        Assert.True(colorizer.QueryIsFencedCodeLine(document, 2));
        Assert.True(colorizer.QueryIsFencedCodeLine(document, 3));
        Assert.True(colorizer.QueryIsFencedCodeLine(document, 4));
        Assert.True(colorizer.QueryIsFencedCodeLine(document, 5));
        Assert.True(colorizer.QueryIsFencedCodeLine(document, 6));
        Assert.False(colorizer.QueryIsFencedCodeLine(document, 7));
    }

    private static void SeedResourceCache(MarkdownColorizingTransformer colorizer)
    {
        var field = GetResourceCacheField();
        var cacheType = typeof(MarkdownColorizingTransformer).GetNestedType("ResourceCache", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ResourceCache type not found.");
        var cache = RuntimeHelpers.GetUninitializedObject(cacheType);
        field.SetValue(colorizer, cache);
    }

    private static object? GetResourceCache(MarkdownColorizingTransformer colorizer)
    {
        return GetResourceCacheField().GetValue(colorizer);
    }

    private static FieldInfo GetResourceCacheField()
    {
        return typeof(MarkdownColorizingTransformer).GetField("_resourceCache", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Resource cache field not found.");
    }
}
