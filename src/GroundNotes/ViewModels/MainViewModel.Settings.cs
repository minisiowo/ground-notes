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
    private VimModeSettings? _vimModeSettings;

    public VimModeSettings VimModeSettings
    {
        get => _vimModeSettings ??= Models.VimModeSettings.Normalize(_settingsService.GetSettingsSync().VimModeSettings);
        private set
        {
            var normalized = Models.VimModeSettings.Normalize(value);
            if (normalized == _vimModeSettings)
            {
                return;
            }

            _vimModeSettings = normalized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(VimModeEnabled));
        }
    }

    public bool VimModeEnabled => VimModeSettings.IsEnabled;

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
        await _workspaceDialogService.ShowSettingsAsync(model, ApplySettingsLive, BuildSettingsPromptActions());
        IsSettingsPreviewActive = false;
    }

    private SettingsDialogModel BuildSettingsDialogModel()
    {
        return new SettingsDialogModel(
            ThemeNames,
            _allFonts,
            SelectedThemeName,
            SelectedUiFontFamilyName,
            SelectedUiFontVariantName,
            SelectedFontFamilyName,
            SelectedFontVariantName,
            SelectedCodeFontFamilyName,
            SelectedCodeFontVariantName,
            EditorFontSize,
            UiFontSize,
            FileListFontSize,
            ShowSidebarListBackground,
            ShowSidebarListBorder,
            EditorIndentSize,
            EditorLineHeightFactor,
            ShowScrollBars,
            IsAiEnabled,
            OpenAiApiKey,
            SelectedAiModel,
            SelectedAiReasoningEffort,
            OpenAiProjectId,
            OpenAiOrganizationId,
            CurrentAiPromptsDirectory,
            AiPrompts,
            _keyboardShortcutService.Settings,
            VimModeSettings);
    }



    internal void ApplySettingsLive(SettingsDialogModel model)
    {
        ApplyThemeSelection(model.SelectedThemeName, persist: false);

        var uiFontFamily = GetFontFamilyByDisplayName(model.SelectedUiFontFamilyName)
            ?? FontResolutionHelper.FindByKey(_allFonts, FontCatalogService.DefaultFontKey)
            ?? _allFonts[0];
        var uiFontVariant = ResolveFontVariant(uiFontFamily, model.SelectedUiFontVariantName)
            ?? GetDefaultFontVariant(uiFontFamily);
        ApplyUiFontSelection(uiFontFamily, uiFontVariant, persist: false);

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

        var persistedFileListFontSize = ClampFileListFontSize(model.FileListFontSize);
        if (!FileListFontSize.Equals(persistedFileListFontSize))
        {
            FileListFontSize = persistedFileListFontSize;
        }

        ShowSidebarListBackground = model.ShowSidebarListBackground;
        ShowSidebarListBorder = model.ShowSidebarListBorder;

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

        _keyboardShortcutService.ApplySettings(model.KeyboardShortcuts);
        VimModeSettings = Models.VimModeSettings.Normalize(model.VimModeSettings);

        ApplyAiSettings(new AiSettings(
            model.ApiKey,
            model.DefaultModel,
            model.IsAiEnabled,
            model.ProjectId,
            model.OrganizationId,
            model.DefaultReasoningEffort));

        _ = PersistSettingsAsync(settings => settings with
        {
            ThemeName = model.SelectedThemeName,
            UiFontName = uiFontFamily.Key,
            UiFontVariantName = uiFontVariant.Key,
            FontName = fontFamily.Key,
            FontVariantName = variant.Key,
            CodeFontName = codeFontFamily.Key,
            CodeFontVariantName = codeVariant.Key,
            EditorFontSize = persistedEditorFontSize,
            UiFontSize = persistedUiFontSize,
            FileListFontSize = persistedFileListFontSize,
            ShowSidebarListBackground = model.ShowSidebarListBackground,
            ShowSidebarListBorder = model.ShowSidebarListBorder,
            EditorIndentSize = persistedEditorIndentSize,
            EditorLineHeightFactor = persistedEditorLineHeightFactor,
            ShowScrollBars = model.ShowScrollBars,
            AiSettings = BuildAiSettings(),
            KeyboardShortcuts = _keyboardShortcutService.Settings,
            VimModeSettings = VimModeSettings
        });
    }
}
