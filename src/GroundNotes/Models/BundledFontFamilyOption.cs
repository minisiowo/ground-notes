namespace GroundNotes.Models;

public sealed record BundledFontFamilyOption(string Key, string DisplayName, string ResourceUri, IReadOnlyList<BundledFontVariantOption> StandardVariants);
