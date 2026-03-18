using System.Text.Json;
using QuickNotesTxt.Models;
using QuickNotesTxt.Styles;

namespace QuickNotesTxt.Services;

public sealed class StartupStateService : IStartupStateService
{
    private const double DefaultUiFontSize = 12;
    private const double MinUiFontSize = 10;
    private const double MaxUiFontSize = 20;

    private readonly ISettingsService _settingsService;
    private readonly IFontCatalogService _fontCatalogService;

    public StartupStateService(ISettingsService settingsService, IFontCatalogService fontCatalogService)
    {
        _settingsService = settingsService;
        _fontCatalogService = fontCatalogService;
    }

    public StartupStateSnapshot Load()
    {
        var settings = _settingsService.GetSettingsSync();
        var fonts = _fontCatalogService.LoadBundledFonts();
        var theme = ResolveTheme(settings.ThemeName) ?? AppTheme.Dark;
        var uiFontSize = Math.Clamp(settings.UiFontSize ?? DefaultUiFontSize, MinUiFontSize, MaxUiFontSize);

        var defaultFontFamily = ResolveFontFamilyByKey(fonts, FontCatalogService.DefaultFontKey) ?? fonts[0];
        var terminalFontFamily = ResolveFontFamilyByKey(fonts, settings.FontName) ?? defaultFontFamily;
        var sidebarFontFamily = ResolveFontFamilyByKey(fonts, settings.SidebarFontName) ?? defaultFontFamily;
        var codeDefaultFamily = ResolveFontFamilyByKey(fonts, FontCatalogService.DefaultCodeFontKey)
            ?? ResolveFontFamilyByKey(fonts, FontCatalogService.DefaultFontKey)
            ?? fonts[0];
        var codeFontFamily = ResolveFontFamilyByKey(fonts, settings.CodeFontName) ?? codeDefaultFamily;

        return new StartupStateSnapshot(
            settings,
            settings.WindowLayout,
            theme,
            fonts,
            terminalFontFamily,
            ResolveFontVariant(terminalFontFamily, settings.FontVariantName),
            sidebarFontFamily,
            ResolveFontVariant(sidebarFontFamily, settings.SidebarFontVariantName),
            codeFontFamily,
            ResolveFontVariant(codeFontFamily, settings.CodeFontVariantName),
            uiFontSize);
    }

    private static BundledFontFamilyOption? ResolveFontFamilyByKey(IReadOnlyList<BundledFontFamilyOption> fonts, string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        return fonts.FirstOrDefault(font => string.Equals(font.Key, key, StringComparison.Ordinal));
    }

    private static BundledFontVariantOption ResolveFontVariant(BundledFontFamilyOption family, string? variantKey)
    {
        if (!string.IsNullOrWhiteSpace(variantKey))
        {
            var exact = family.StandardVariants.FirstOrDefault(variant =>
                string.Equals(variant.Key, variantKey, StringComparison.Ordinal)
                || string.Equals(variant.DisplayName, variantKey, StringComparison.Ordinal));
            if (exact is not null)
            {
                return exact;
            }
        }

        return family.StandardVariants.FirstOrDefault(variant => string.Equals(variant.DisplayName, FontCatalogService.DefaultVariantKey, StringComparison.Ordinal))
            ?? family.StandardVariants.FirstOrDefault(variant => string.Equals(variant.DisplayName, "Medium", StringComparison.Ordinal))
            ?? family.StandardVariants.FirstOrDefault(variant => string.Equals(variant.DisplayName, "Light", StringComparison.Ordinal))
            ?? family.StandardVariants[0];
    }

    private static AppTheme? ResolveTheme(string? themeName)
    {
        if (string.IsNullOrWhiteSpace(themeName))
        {
            return null;
        }

        var builtInTheme = AppTheme.BuiltInThemes.FirstOrDefault(theme => string.Equals(theme.Name, themeName, StringComparison.Ordinal));
        if (builtInTheme is not null)
        {
            return builtInTheme;
        }

        var themesDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickNotesTxt", "themes");
        if (!Directory.Exists(themesDirectory))
        {
            return null;
        }

        foreach (var file in Directory.EnumerateFiles(themesDirectory, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var theme = JsonSerializer.Deserialize<AppTheme>(json);
                if (theme is not null && string.Equals(theme.Name, themeName, StringComparison.Ordinal))
                {
                    return theme;
                }
            }
            catch
            {
            }
        }

        return null;
    }
}
