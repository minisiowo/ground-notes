using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Markup.Xaml;
using QuickNotesTxt.Models;
using QuickNotesTxt.Services;
using QuickNotesTxt.Styles;
using QuickNotesTxt.ViewModels;
using QuickNotesTxt.Views;

namespace QuickNotesTxt;

public partial class App : Application
{
    private const double DefaultUiFontSize = 12;
    private const double MinUiFontSize = 10;
    private const double MaxUiFontSize = 20;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        ApplyStartupAppearance();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settingsService = new FolderSettingsService();
            var savedLayout = settingsService.GetWindowLayoutSync();
            var repository = new NotesRepository();
            var fileWatcher = new FileWatcherService();
            var themeLoader = new ThemeLoaderService();
            var fontCatalog = new FontCatalogService();
            var aiPromptCatalog = new AiPromptCatalogService();
            var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(60)
            };
            var aiTextActionService = new OpenAiTextActionService(httpClient);
            var aiChatService = new OpenAiChatService(httpClient);
            var mainViewModel = new MainViewModel(repository, settingsService, fileWatcher, themeLoader, fontCatalog, aiPromptCatalog, aiTextActionService, aiChatService);

            var mainWindow = new MainWindow
            {
                DataContext = mainViewModel,
                Opacity = 0
            };
            mainWindow.SetSettingsService(settingsService);

            // Apply saved layout synchronously before the window is shown,
            // so it appears at the correct position and size immediately.
            if (savedLayout is not null)
            {
                mainWindow.Position = new PixelPoint((int)savedLayout.X, (int)savedLayout.Y);
                mainWindow.Width = savedLayout.Width;
                mainWindow.Height = savedLayout.Height;
            }

            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ApplyStartupAppearance()
    {
        var settingsService = new FolderSettingsService();
        var settings = settingsService.GetSettingsSync();

        var fontCatalog = new FontCatalogService();
        var fonts = fontCatalog.LoadBundledFonts();

        ApplyStartupTheme(settings.ThemeName);
        ApplyStartupUiFontSize(settings.UiFontSize);
        ApplyStartupTerminalFont(settings, fonts);
        ApplyStartupSidebarFont(settings, fonts);
        ApplyStartupCodeFont(settings, fonts);
    }

    private static void ApplyStartupTheme(string? themeName)
    {
        var fallbackTheme = AppTheme.Dark;

        try
        {
            var theme = ResolveTheme(themeName) ?? fallbackTheme;
            ThemeService.Apply(theme);
        }
        catch
        {
            ThemeService.Apply(fallbackTheme);
        }
    }

    private static void ApplyStartupUiFontSize(double? value)
    {
        var clamped = Math.Clamp(value ?? DefaultUiFontSize, MinUiFontSize, MaxUiFontSize);
        ThemeService.ApplyUiFontSize(clamped);
    }

    private static void ApplyStartupTerminalFont(AppSettings settings, IReadOnlyList<BundledFontFamilyOption> fonts)
    {
        var defaultFamily = ResolveFontFamilyByKey(fonts, FontCatalogService.DefaultFontKey) ?? fonts[0];
        var family = ResolveFontFamilyByKey(fonts, settings.FontName) ?? defaultFamily;
        var variant = ResolveFontVariant(family, settings.FontVariantName);
        ThemeService.ApplyTerminalFont(new FontFamily(family.ResourceUri), variant.FontWeight, variant.FontStyle);
    }

    private static void ApplyStartupSidebarFont(AppSettings settings, IReadOnlyList<BundledFontFamilyOption> fonts)
    {
        var defaultFamily = ResolveFontFamilyByKey(fonts, FontCatalogService.DefaultFontKey) ?? fonts[0];
        var family = ResolveFontFamilyByKey(fonts, settings.SidebarFontName) ?? defaultFamily;
        var variant = ResolveFontVariant(family, settings.SidebarFontVariantName);
        ThemeService.ApplySidebarFont(new FontFamily(family.ResourceUri), variant.FontWeight, variant.FontStyle);
    }

    private static void ApplyStartupCodeFont(AppSettings settings, IReadOnlyList<BundledFontFamilyOption> fonts)
    {
        var defaultFamily = ResolveFontFamilyByKey(fonts, FontCatalogService.DefaultCodeFontKey)
            ?? ResolveFontFamilyByKey(fonts, FontCatalogService.DefaultFontKey)
            ?? fonts[0];
        var family = ResolveFontFamilyByKey(fonts, settings.CodeFontName) ?? defaultFamily;
        var variant = ResolveFontVariant(family, settings.CodeFontVariantName);
        ThemeService.ApplyCodeFont(new FontFamily(family.ResourceUri), variant.FontWeight, variant.FontStyle);
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

        var builtInTheme = AppTheme.BuiltInThemes
            .FirstOrDefault(theme => string.Equals(theme.Name, themeName, StringComparison.Ordinal));
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
                if (theme is null)
                {
                    continue;
                }

                if (string.Equals(theme.Name, themeName, StringComparison.Ordinal))
                {
                    return theme;
                }
            }
            catch
            {
                // Ignore malformed theme files during startup fallback resolution.
            }
        }

        return null;
    }

}
