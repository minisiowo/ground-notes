using System.Collections.ObjectModel;
using Avalonia.Threading;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroundNotes.Models;
using GroundNotes.Services;
using GroundNotes.Styles;

namespace GroundNotes.ViewModels;

public partial class MainViewModel : ViewModelBase, IDisposable
{
    partial void OnSelectedThemeNameChanged(string value)
    {
        var theme = _allThemes.FirstOrDefault(t => t.Name == value);
        if (theme is not null)
        {
            _appearanceService.ApplyTheme(theme);

            if (!_isApplyingSelection && !IsSettingsPreviewActive)
            {
                _ = PersistSettingsAsync(settings => settings with { ThemeName = value });
            }
        }
    }

    partial void OnSelectedSidebarFontFamilyNameChanged(string value)
    {
        if (_isApplyingSelection)
        {
            return;
        }

        var fontFamily = GetFontFamilyByDisplayName(value);
        if (fontFamily is null)
        {
            return;
        }

        var variant = ResolveFontVariant(fontFamily, SelectedSidebarFontVariantName)
            ?? GetDefaultFontVariant(fontFamily);
        ApplySidebarFontSelection(fontFamily, variant, persist: true, updateFamilyName: false);
    }

    partial void OnSelectedSidebarFontVariantNameChanged(string value)
    {
        if (_isApplyingSelection)
        {
            return;
        }

        var fontFamily = GetFontFamilyByDisplayName(SelectedSidebarFontFamilyName);
        var variant = fontFamily is null ? null : ResolveFontVariant(fontFamily, value);
        if (fontFamily is null || variant is null)
        {
            return;
        }

        ApplySidebarFontSelection(fontFamily, variant, persist: true, updateFamilyName: false);
    }

    partial void OnSelectedFontFamilyNameChanged(string value)
    {
        if (_isApplyingSelection)
        {
            return;
        }

        var fontFamily = GetFontFamilyByDisplayName(value);
        if (fontFamily is null)
        {
            return;
        }

        var variant = ResolveFontVariant(fontFamily, SelectedFontVariantName)
            ?? GetDefaultFontVariant(fontFamily);

        ApplyFontSelection(fontFamily, variant, persist: true, updateFamilyName: false);
    }

    partial void OnSelectedFontVariantNameChanged(string value)
    {
        if (_isApplyingSelection)
        {
            return;
        }

        var fontFamily = GetFontFamilyByDisplayName(SelectedFontFamilyName);
        var variant = fontFamily is null ? null : ResolveFontVariant(fontFamily, value);
        if (fontFamily is null || variant is null)
        {
            return;
        }

        ApplyFontSelection(fontFamily, variant, persist: true, updateFamilyName: false);
    }

    partial void OnSelectedCodeFontFamilyNameChanged(string value)
    {
        if (_isApplyingSelection)
        {
            return;
        }

        var fontFamily = GetFontFamilyByDisplayName(value);
        if (fontFamily is null)
        {
            return;
        }

        var variant = ResolveFontVariant(fontFamily, SelectedCodeFontVariantName)
            ?? GetDefaultFontVariant(fontFamily);
        ApplyCodeFontSelection(fontFamily, variant, persist: true, updateFamilyName: false);
    }

    partial void OnSelectedCodeFontVariantNameChanged(string value)
    {
        if (_isApplyingSelection)
        {
            return;
        }

        var fontFamily = GetFontFamilyByDisplayName(SelectedCodeFontFamilyName);
        var variant = fontFamily is null ? null : ResolveFontVariant(fontFamily, value);
        if (fontFamily is null || variant is null)
        {
            return;
        }

        ApplyCodeFontSelection(fontFamily, variant, persist: true, updateFamilyName: false);
    }

    private BundledFontFamilyOption? GetFontFamilyByDisplayName(string displayName)
    {
        return _allFonts.FirstOrDefault(font => string.Equals(font.DisplayName, displayName, StringComparison.Ordinal));
    }

    private static BundledFontVariantOption? ResolveFontVariant(BundledFontFamilyOption fontFamily, string variantDisplayName)
    {
        return fontFamily.StandardVariants.FirstOrDefault(variant => string.Equals(variant.DisplayName, variantDisplayName, StringComparison.Ordinal));
    }

    private static BundledFontVariantOption GetDefaultFontVariant(BundledFontFamilyOption fontFamily)
    {
        return ResolveFontVariant(fontFamily, FontCatalogService.DefaultVariantKey)
            ?? ResolveFontVariant(fontFamily, "Medium")
            ?? ResolveFontVariant(fontFamily, "Light")
            ?? fontFamily.StandardVariants[0];
    }

    private void ApplyFontSelection(
        BundledFontFamilyOption fontFamily,
        BundledFontVariantOption variant,
        bool persist,
        bool updateFamilyName = true,
        bool updateVariantName = true)
    {
        _isApplyingSelection = true;
        try
        {
            FontVariantNames = fontFamily.StandardVariants.Select(v => v.DisplayName).ToList();

            if (updateFamilyName)
            {
                SelectedFontFamilyName = fontFamily.DisplayName;
            }

            if (updateVariantName)
            {
                SelectedFontVariantName = variant.DisplayName;
            }
        }
        finally
        {
            _isApplyingSelection = false;
        }

        _appearanceService.ApplyTerminalFont(fontFamily, variant);

        if (persist)
        {
            _ = PersistFontSelectionAsync(fontFamily.Key, variant.Key);
        }
    }

    private async Task PersistFontSelectionAsync(string fontFamilyKey, string fontVariantKey)
    {
        await PersistSettingsAsync(settings => settings with
        {
            FontName = fontFamilyKey,
            FontVariantName = fontVariantKey
        });
    }

    private async Task PersistSidebarFontSelectionAsync(string fontFamilyKey, string fontVariantKey)
    {
        await PersistSettingsAsync(settings => settings with
        {
            SidebarFontName = fontFamilyKey,
            SidebarFontVariantName = fontVariantKey
        });
    }

    private void ApplySidebarFontSelection(
        BundledFontFamilyOption fontFamily,
        BundledFontVariantOption variant,
        bool persist,
        bool updateFamilyName = true,
        bool updateVariantName = true)
    {
        _isApplyingSelection = true;
        try
        {
            SidebarFontVariantNames = fontFamily.StandardVariants.Select(v => v.DisplayName).ToList();

            if (updateFamilyName)
            {
                SelectedSidebarFontFamilyName = fontFamily.DisplayName;
            }

            if (updateVariantName)
            {
                SelectedSidebarFontVariantName = variant.DisplayName;
            }
        }
        finally
        {
            _isApplyingSelection = false;
        }

        _appearanceService.ApplySidebarFont(fontFamily, variant);

        if (persist)
        {
            _ = PersistSidebarFontSelectionAsync(fontFamily.Key, variant.Key);
        }
    }

    private async Task PersistCodeFontSelectionAsync(string fontFamilyKey, string fontVariantKey)
    {
        await PersistSettingsAsync(settings => settings with
        {
            CodeFontName = fontFamilyKey,
            CodeFontVariantName = fontVariantKey
        });
    }

    private void ApplyCodeFontSelection(
        BundledFontFamilyOption fontFamily,
        BundledFontVariantOption variant,
        bool persist,
        bool updateFamilyName = true,
        bool updateVariantName = true)
    {
        _isApplyingSelection = true;
        try
        {
            CodeFontVariantNames = fontFamily.StandardVariants.Select(v => v.DisplayName).ToList();

            if (updateFamilyName)
            {
                SelectedCodeFontFamilyName = fontFamily.DisplayName;
            }

            if (updateVariantName)
            {
                SelectedCodeFontVariantName = variant.DisplayName;
            }
        }
        finally
        {
            _isApplyingSelection = false;
        }

        _appearanceService.ApplyCodeFont(fontFamily, variant);

        if (persist)
        {
            _ = PersistCodeFontSelectionAsync(fontFamily.Key, variant.Key);
        }
    }

    partial void OnUiFontSizeChanged(double value)
    {
        _appearanceService.ApplyUiFontSize(value);
    }

    partial void OnEditorIndentSizeChanged(int value)
    {
        _editorLayoutState.Set(new EditorLayoutSettings(
            EditorDisplaySettings.NormalizeIndentSize(value),
            EditorLineHeightFactor));
    }

    partial void OnEditorLineHeightFactorChanged(double value)
    {
        _editorLayoutState.Set(new EditorLayoutSettings(
            EditorIndentSize,
            EditorDisplaySettings.NormalizeLineHeightFactor(value)));
    }

    private void ApplyThemeSelection(string themeName, bool persist)
    {
        var theme = _allThemes.FirstOrDefault(t => string.Equals(t.Name, themeName, StringComparison.Ordinal));
        if (theme is null)
        {
            return;
        }

        _isApplyingSelection = true;
        try
        {
            SelectedThemeName = theme.Name;
        }
        finally
        {
            _isApplyingSelection = false;
        }

        if (persist)
        {
            _ = PersistSettingsAsync(settings => settings with { ThemeName = theme.Name });
        }
    }
}
