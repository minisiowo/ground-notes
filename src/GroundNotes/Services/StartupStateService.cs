using GroundNotes.Models;
using GroundNotes.Styles;

namespace GroundNotes.Services;

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
        return FontResolutionHelper.FindByKey(fonts, key);
    }

    private static BundledFontVariantOption ResolveFontVariant(BundledFontFamilyOption family, string? variantKey)
    {
        return FontResolutionHelper.ResolveVariant(family, variantKey)
               ?? FontResolutionHelper.GetDefaultVariant(family);
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

        var themeLoader = new ThemeLoaderService();
        if (!Directory.Exists(themeLoader.ThemesDirectory))
        {
            return null;
        }

        foreach (var file in Directory.EnumerateFiles(themeLoader.ThemesDirectory, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var theme = ThemeLoaderService.DeserializeTheme(json);
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
