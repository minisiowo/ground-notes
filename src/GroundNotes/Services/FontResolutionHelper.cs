using GroundNotes.Models;

namespace GroundNotes.Services;

public static class FontResolutionHelper
{
    public static BundledFontFamilyOption? FindByDisplayName(
        IReadOnlyList<BundledFontFamilyOption> fonts, string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return null;
        }

        return fonts.FirstOrDefault(font =>
            string.Equals(font.DisplayName, displayName, StringComparison.Ordinal));
    }

    public static BundledFontFamilyOption? FindByKey(
        IReadOnlyList<BundledFontFamilyOption> fonts, string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        return fonts.FirstOrDefault(font =>
            string.Equals(font.Key, key, StringComparison.Ordinal));
    }

    public static BundledFontVariantOption? ResolveVariant(
        BundledFontFamilyOption fontFamily, string? variantName)
    {
        if (string.IsNullOrWhiteSpace(variantName))
        {
            return null;
        }

        return fontFamily.StandardVariants.FirstOrDefault(v =>
                   string.Equals(v.Key, variantName, StringComparison.Ordinal))
               ?? fontFamily.StandardVariants.FirstOrDefault(v =>
                   string.Equals(v.DisplayName, variantName, StringComparison.Ordinal));
    }

    public static BundledFontVariantOption GetDefaultVariant(BundledFontFamilyOption fontFamily)
    {
        return ResolveVariant(fontFamily, FontCatalogService.DefaultVariantKey)
               ?? ResolveVariant(fontFamily, "Medium")
               ?? ResolveVariant(fontFamily, "Light")
               ?? fontFamily.StandardVariants[0];
    }
}
