using System.Reflection;
using System.Runtime.CompilerServices;
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
