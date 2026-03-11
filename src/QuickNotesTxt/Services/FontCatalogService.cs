using System.Text.RegularExpressions;
using QuickNotesTxt.Models;

namespace QuickNotesTxt.Services;

public sealed class FontCatalogService : IFontCatalogService
{
    public const string DefaultFontKey = "IosevkaSlab";
    private const string AssetUriPrefix = "avares://QuickNotesTxt/Assets/Fonts";

    private static readonly Regex s_displayNameRegex = new("([a-z0-9])([A-Z])", RegexOptions.Compiled);
    private readonly string _fontsDirectory;

    public FontCatalogService()
        : this(Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts"))
    {
    }

    public FontCatalogService(string fontsDirectory)
    {
        _fontsDirectory = fontsDirectory;
    }

    public IReadOnlyList<BundledFontOption> LoadBundledFonts()
    {
        if (!Directory.Exists(_fontsDirectory))
        {
            return [CreateFontOption(DefaultFontKey)];
        }

        var fonts = Directory.EnumerateDirectories(_fontsDirectory)
            .Select(Path.GetFileName)
            .Where(static folderName => !string.IsNullOrWhiteSpace(folderName))
            .Select(static folderName => folderName!)
            .Where(ContainsSupportedFontFiles)
            .Select(CreateFontOption)
            .OrderBy(static option => option.DisplayName, StringComparer.Ordinal)
            .ToList();

        if (fonts.Count == 0)
        {
            fonts.Add(CreateFontOption(DefaultFontKey));
        }

        return fonts;
    }

    private bool ContainsSupportedFontFiles(string folderName)
    {
        var folderPath = Path.Combine(_fontsDirectory, folderName);
        return Directory.EnumerateFiles(folderPath)
            .Any(static file =>
                file.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)
                || file.EndsWith(".otf", StringComparison.OrdinalIgnoreCase));
    }

    private static BundledFontOption CreateFontOption(string key)
    {
        var displayName = s_displayNameRegex.Replace(key, "$1 $2");
        var resourceUri = $"{AssetUriPrefix}/{key}#{displayName}";
        return new BundledFontOption(key, displayName, resourceUri);
    }
}
