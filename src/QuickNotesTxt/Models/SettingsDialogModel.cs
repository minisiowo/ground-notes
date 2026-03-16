namespace QuickNotesTxt.Models;

public sealed record SettingsDialogModel(
    IReadOnlyList<string> ThemeNames,
    IReadOnlyList<BundledFontFamilyOption> FontFamilies,
    string SelectedThemeName,
    string SelectedFontFamilyName,
    string SelectedFontVariantName,
    double EditorFontSize,
    double UiFontSize,
    bool IsAiEnabled,
    string ApiKey,
    string DefaultModel,
    string ProjectId,
    string OrganizationId,
    string PromptsDirectory);
