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
    private Task ShowKeyboardShortcutsHelpAsync()
    {
        return _workspaceDialogService.ShowKeyboardShortcutsHelpAsync(null);
    }

    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        var model = BuildSettingsDialogModel();
        IsSettingsPreviewActive = true;
        await _workspaceDialogService.ShowSettingsAsync(model, ApplySettingsLive);
        IsSettingsPreviewActive = false;
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
            ShowScrollBars,
            IsAiEnabled,
            OpenAiApiKey,
            SelectedAiModel,
            OpenAiProjectId,
            OpenAiOrganizationId,
            CurrentAiPromptsDirectory);
    }



    internal void ApplySettingsLive(SettingsDialogModel model)
    {
        ApplyThemeSelection(model.SelectedThemeName, persist: false);

        var sidebarFontFamily = GetFontFamilyByDisplayName(model.SelectedSidebarFontFamilyName)
            ?? FontResolutionHelper.FindByKey(_allFonts, FontCatalogService.DefaultFontKey)
            ?? _allFonts[0];
        var sidebarVariant = ResolveFontVariant(sidebarFontFamily, model.SelectedSidebarFontVariantName)
            ?? GetDefaultFontVariant(sidebarFontFamily);
        ApplySidebarFontSelection(sidebarFontFamily, sidebarVariant, persist: false);

        var fontFamily = GetFontFamilyByDisplayName(model.SelectedFontFamilyName)
            ?? FontResolutionHelper.FindByKey(_allFonts, FontCatalogService.DefaultFontKey)
            ?? _allFonts[0];
        var variant = ResolveFontVariant(fontFamily, model.SelectedFontVariantName)
            ?? GetDefaultFontVariant(fontFamily);
        ApplyFontSelection(fontFamily, variant, persist: false);

        var codeFontFamily = GetFontFamilyByDisplayName(model.SelectedCodeFontFamilyName)
            ?? FontResolutionHelper.FindByKey(_allFonts, FontCatalogService.DefaultCodeFontKey)
            ?? fontFamily;
        var codeVariant = ResolveFontVariant(codeFontFamily, model.SelectedCodeFontVariantName)
            ?? GetDefaultFontVariant(codeFontFamily);
        ApplyCodeFontSelection(codeFontFamily, codeVariant, persist: false);

        var persistedEditorFontSize = ClampEditorFontSize(model.EditorFontSize);
        if (!EditorFontSize.Equals(persistedEditorFontSize))
        {
            EditorFontSize = persistedEditorFontSize;
        }

        var persistedUiFontSize = ClampUiFontSize(model.UiFontSize);
        if (!UiFontSize.Equals(persistedUiFontSize))
        {
            UiFontSize = persistedUiFontSize;
        }

        var persistedEditorIndentSize = EditorDisplaySettings.NormalizeIndentSize(model.EditorIndentSize);
        if (EditorIndentSize != persistedEditorIndentSize)
        {
            EditorIndentSize = persistedEditorIndentSize;
        }

        var persistedEditorLineHeightFactor = EditorDisplaySettings.NormalizeLineHeightFactor(model.EditorLineHeightFactor);
        if (Math.Abs(EditorLineHeightFactor - persistedEditorLineHeightFactor) > 0.0001)
        {
            EditorLineHeightFactor = persistedEditorLineHeightFactor;
        }

        ShowScrollBars = model.ShowScrollBars;
        _appearanceService.ApplyScrollBars(model.ShowScrollBars);

        ApplyAiSettings(new AiSettings(
            model.ApiKey,
            model.DefaultModel,
            model.IsAiEnabled,
            model.ProjectId,
            model.OrganizationId));

        _ = PersistSettingsAsync(settings => settings with
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
            ShowScrollBars = model.ShowScrollBars,
            AiSettings = BuildAiSettings()
        });
    }
}
