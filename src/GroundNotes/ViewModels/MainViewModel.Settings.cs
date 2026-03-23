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
    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        var original = BuildSettingsDialogModel();
        var updated = await _workspaceDialogService.ShowSettingsAsync(original, ApplySettingsPreview);
        if (updated is null)
        {
            ApplySettingsPreview(original);
            IsSettingsPreviewActive = false;
            return;
        }

        IsSettingsPreviewActive = false;
        await ApplySettingsAsync(updated);
        StatusMessage = "Settings saved.";
    }

    private SettingsDialogModel BuildSettingsDialogModel()
    {
        return new SettingsDialogModel(
            ThemeNames,
            _allFonts,
            SelectedThemeName,
            SelectedSidebarFontFamilyName,
            SelectedSidebarFontVariantName,
            SelectedFontFamilyName,
            SelectedFontVariantName,
            SelectedCodeFontFamilyName,
            SelectedCodeFontVariantName,
            EditorFontSize,
            UiFontSize,
            EditorIndentSize,
            EditorLineHeightFactor,
            IsAiEnabled,
            OpenAiApiKey,
            SelectedAiModel,
            OpenAiProjectId,
            OpenAiOrganizationId,
            CurrentAiPromptsDirectory);
    }

    private async Task ApplySettingsAsync(SettingsDialogModel model)
    {
        ApplyThemeSelection(model.SelectedThemeName, persist: true);

        var sidebarFontFamily = GetFontFamilyByDisplayName(model.SelectedSidebarFontFamilyName)
            ?? _allFonts.FirstOrDefault(font => string.Equals(font.Key, FontCatalogService.DefaultFontKey, StringComparison.Ordinal))
            ?? _allFonts[0];
        var sidebarVariant = ResolveFontVariant(sidebarFontFamily, model.SelectedSidebarFontVariantName)
            ?? GetDefaultFontVariant(sidebarFontFamily);
        ApplySidebarFontSelection(sidebarFontFamily, sidebarVariant, persist: false);
        await PersistSidebarFontSelectionAsync(sidebarFontFamily.Key, sidebarVariant.Key);

        var fontFamily = GetFontFamilyByDisplayName(model.SelectedFontFamilyName)
            ?? _allFonts.FirstOrDefault(font => string.Equals(font.Key, FontCatalogService.DefaultFontKey, StringComparison.Ordinal))
            ?? _allFonts[0];
        var variant = ResolveFontVariant(fontFamily, model.SelectedFontVariantName)
            ?? GetDefaultFontVariant(fontFamily);
        ApplyFontSelection(fontFamily, variant, persist: false);
        await PersistFontSelectionAsync(fontFamily.Key, variant.Key);

        var codeFontFamily = GetFontFamilyByDisplayName(model.SelectedCodeFontFamilyName)
            ?? _allFonts.FirstOrDefault(font => string.Equals(font.Key, FontCatalogService.DefaultCodeFontKey, StringComparison.Ordinal))
            ?? fontFamily;
        var codeVariant = ResolveFontVariant(codeFontFamily, model.SelectedCodeFontVariantName)
            ?? GetDefaultFontVariant(codeFontFamily);
        ApplyCodeFontSelection(codeFontFamily, codeVariant, persist: false);
        await PersistCodeFontSelectionAsync(codeFontFamily.Key, codeVariant.Key);

        ApplyAiSettings(new AiSettings(
            model.ApiKey,
            model.DefaultModel,
            model.IsAiEnabled,
            model.ProjectId,
            model.OrganizationId));

        var persistedEditorFontSize = ClampEditorFontSize(model.EditorFontSize);
        var persistedUiFontSize = ClampUiFontSize(model.UiFontSize);
        var persistedEditorIndentSize = EditorDisplaySettings.NormalizeIndentSize(model.EditorIndentSize);
        var persistedEditorLineHeightFactor = EditorDisplaySettings.NormalizeLineHeightFactor(model.EditorLineHeightFactor);
        if (!EditorFontSize.Equals(persistedEditorFontSize))
        {
            EditorFontSize = persistedEditorFontSize;
        }

        if (!UiFontSize.Equals(persistedUiFontSize))
        {
            UiFontSize = persistedUiFontSize;
        }

        if (EditorIndentSize != persistedEditorIndentSize)
        {
            EditorIndentSize = persistedEditorIndentSize;
        }

        if (Math.Abs(EditorLineHeightFactor - persistedEditorLineHeightFactor) > 0.0001)
        {
            EditorLineHeightFactor = persistedEditorLineHeightFactor;
        }

        await PersistSettingsAsync(settings => settings with
        {
            ThemeName = model.SelectedThemeName,
            SidebarFontName = sidebarFontFamily.Key,
            SidebarFontVariantName = sidebarVariant.Key,
            FontName = fontFamily.Key,
            FontVariantName = variant.Key,
            CodeFontName = codeFontFamily.Key,
            CodeFontVariantName = codeVariant.Key,
            EditorFontSize = persistedEditorFontSize,
            UiFontSize = persistedUiFontSize,
            EditorIndentSize = persistedEditorIndentSize,
            EditorLineHeightFactor = persistedEditorLineHeightFactor,
            AiSettings = BuildAiSettings()
        });
    }

    public void ApplySettingsPreview(SettingsDialogModel model)
    {
        IsSettingsPreviewActive = true;
        if (!string.Equals(SelectedThemeName, model.SelectedThemeName, StringComparison.Ordinal))
        {
            ApplyThemeSelection(model.SelectedThemeName, persist: false);
        }

        var sidebarFontFamily = GetFontFamilyByDisplayName(model.SelectedSidebarFontFamilyName)
            ?? _allFonts.FirstOrDefault(font => string.Equals(font.Key, FontCatalogService.DefaultFontKey, StringComparison.Ordinal))
            ?? _allFonts[0];
        var sidebarVariant = ResolveFontVariant(sidebarFontFamily, model.SelectedSidebarFontVariantName)
            ?? GetDefaultFontVariant(sidebarFontFamily);
        if (!string.Equals(SelectedSidebarFontFamilyName, sidebarFontFamily.DisplayName, StringComparison.Ordinal)
            || !string.Equals(SelectedSidebarFontVariantName, sidebarVariant.DisplayName, StringComparison.Ordinal))
        {
            ApplySidebarFontSelection(sidebarFontFamily, sidebarVariant, persist: false);
        }

        var fontFamily = GetFontFamilyByDisplayName(model.SelectedFontFamilyName)
            ?? _allFonts.FirstOrDefault(font => string.Equals(font.Key, FontCatalogService.DefaultFontKey, StringComparison.Ordinal))
            ?? _allFonts[0];
        var variant = ResolveFontVariant(fontFamily, model.SelectedFontVariantName)
            ?? GetDefaultFontVariant(fontFamily);
        if (!string.Equals(SelectedFontFamilyName, fontFamily.DisplayName, StringComparison.Ordinal)
            || !string.Equals(SelectedFontVariantName, variant.DisplayName, StringComparison.Ordinal))
        {
            ApplyFontSelection(fontFamily, variant, persist: false);
        }

        var codeFontFamily = GetFontFamilyByDisplayName(model.SelectedCodeFontFamilyName)
            ?? _allFonts.FirstOrDefault(font => string.Equals(font.Key, FontCatalogService.DefaultCodeFontKey, StringComparison.Ordinal))
            ?? fontFamily;
        var codeVariant = ResolveFontVariant(codeFontFamily, model.SelectedCodeFontVariantName)
            ?? GetDefaultFontVariant(codeFontFamily);
        if (!string.Equals(SelectedCodeFontFamilyName, codeFontFamily.DisplayName, StringComparison.Ordinal)
            || !string.Equals(SelectedCodeFontVariantName, codeVariant.DisplayName, StringComparison.Ordinal))
        {
            ApplyCodeFontSelection(codeFontFamily, codeVariant, persist: false);
        }

        var editorFontSize = ClampEditorFontSize(model.EditorFontSize);
        if (!EditorFontSize.Equals(editorFontSize))
        {
            EditorFontSize = editorFontSize;
        }

        var uiFontSize = ClampUiFontSize(model.UiFontSize);
        if (!UiFontSize.Equals(uiFontSize))
        {
            UiFontSize = uiFontSize;
        }

        var editorIndentSize = EditorDisplaySettings.NormalizeIndentSize(model.EditorIndentSize);
        if (EditorIndentSize != editorIndentSize)
        {
            EditorIndentSize = editorIndentSize;
        }

        var editorLineHeightFactor = EditorDisplaySettings.NormalizeLineHeightFactor(model.EditorLineHeightFactor);
        if (Math.Abs(EditorLineHeightFactor - editorLineHeightFactor) > 0.0001)
        {
            EditorLineHeightFactor = editorLineHeightFactor;
        }
    }
}
