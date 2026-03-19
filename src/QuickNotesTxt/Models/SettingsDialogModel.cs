namespace QuickNotesTxt.Models;

public sealed record SettingsDialogModel(
    IReadOnlyList<string> ThemeNames,
    IReadOnlyList<BundledFontFamilyOption> FontFamilies,
    string SelectedThemeName,
    string SelectedSidebarFontFamilyName,
    string SelectedSidebarFontVariantName,
    string SelectedFontFamilyName,
    string SelectedFontVariantName,
    string SelectedCodeFontFamilyName,
    string SelectedCodeFontVariantName,
    double EditorFontSize,
    double UiFontSize,
    int EditorIndentSize,
    double EditorLineHeightFactor,
    bool IsAiEnabled,
    string ApiKey,
    string DefaultModel,
    string ProjectId,
    string OrganizationId,
    string PromptsDirectory);
