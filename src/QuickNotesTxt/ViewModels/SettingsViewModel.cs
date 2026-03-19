using CommunityToolkit.Mvvm.ComponentModel;
using QuickNotesTxt.Models;

namespace QuickNotesTxt.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly IReadOnlyList<BundledFontFamilyOption> _fontFamilies;
    private bool _isInitializing;

    public SettingsViewModel(SettingsDialogModel model)
    {
        ThemeNames = model.ThemeNames;
        _fontFamilies = model.FontFamilies;
        FontFamilies = model.FontFamilies.Select(font => font.DisplayName).ToList();
        EditorFontSizes = Enumerable.Range(10, 15).Select(static size => size.ToString()).ToList();
        UiFontSizes = Enumerable.Range(10, 11).Select(static size => size.ToString()).ToList();
        PromptsDirectory = string.IsNullOrWhiteSpace(model.PromptsDirectory)
            ? "Choose a notes folder first."
            : model.PromptsDirectory;

        _isInitializing = true;
        SelectedThemeName = model.SelectedThemeName;
        SelectedSidebarFontFamilyName = model.SelectedSidebarFontFamilyName;
        UpdateSidebarVariantNames(model.SelectedSidebarFontVariantName);
        SelectedFontFamilyName = model.SelectedFontFamilyName;
        UpdateFontVariantNames(model.SelectedFontVariantName);
        SelectedCodeFontFamilyName = model.SelectedCodeFontFamilyName;
        UpdateCodeFontVariantNames(model.SelectedCodeFontVariantName);
        SelectedEditorFontSize = Math.Round(model.EditorFontSize).ToString("0");
        SelectedUiFontSize = Math.Round(model.UiFontSize).ToString("0");
        IsAiEnabled = model.IsAiEnabled;
        ApiKey = model.ApiKey;
        DefaultModel = model.DefaultModel;
        ProjectId = model.ProjectId;
        OrganizationId = model.OrganizationId;
        _isInitializing = false;
    }

    public event EventHandler<SettingsDialogModel>? PreviewRequested;

    public IReadOnlyList<string> ThemeNames { get; }

    public IReadOnlyList<string> FontFamilies { get; }

    public IReadOnlyList<string> EditorFontSizes { get; }

    public IReadOnlyList<string> UiFontSizes { get; }

    public string PromptsDirectory { get; }

    [ObservableProperty]
    private string _selectedThemeName = string.Empty;

    [ObservableProperty]
    private string _selectedSidebarFontFamilyName = string.Empty;

    [ObservableProperty]
    private string _selectedSidebarFontVariantName = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<string> _sidebarFontVariantNames = [];

    [ObservableProperty]
    private string _selectedFontFamilyName = string.Empty;

    [ObservableProperty]
    private string _selectedFontVariantName = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<string> _fontVariantNames = [];

    [ObservableProperty]
    private string _selectedCodeFontFamilyName = string.Empty;

    [ObservableProperty]
    private string _selectedCodeFontVariantName = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<string> _codeFontVariantNames = [];

    [ObservableProperty]
    private string _selectedEditorFontSize = "12";

    [ObservableProperty]
    private string _selectedUiFontSize = "12";

    [ObservableProperty]
    private bool _isAiEnabled = true;

    [ObservableProperty]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    private string _defaultModel = "gpt-5.4-mini";

    [ObservableProperty]
    private string _projectId = string.Empty;

    [ObservableProperty]
    private string _organizationId = string.Empty;

    public SettingsDialogModel BuildModel()
    {
        return new SettingsDialogModel(
            ThemeNames,
            _fontFamilies,
            SelectedThemeName,
            SelectedSidebarFontFamilyName,
            SelectedSidebarFontVariantName,
            SelectedFontFamilyName,
            SelectedFontVariantName,
            SelectedCodeFontFamilyName,
            SelectedCodeFontVariantName,
            ParseSize(SelectedEditorFontSize, 12),
            ParseSize(SelectedUiFontSize, 12),
            IsAiEnabled,
            ApiKey.Trim(),
            string.IsNullOrWhiteSpace(DefaultModel) ? "gpt-5.4-mini" : DefaultModel.Trim(),
            ProjectId.Trim(),
            OrganizationId.Trim(),
            PromptsDirectory);
    }

    partial void OnSelectedThemeNameChanged(string value) => RaisePreviewRequested();

    partial void OnSelectedSidebarFontFamilyNameChanged(string value)
    {
        UpdateSidebarVariantNames(null);
        RaisePreviewRequested();
    }

    partial void OnSelectedSidebarFontVariantNameChanged(string value) => RaisePreviewRequested();

    partial void OnSelectedFontFamilyNameChanged(string value)
    {
        UpdateFontVariantNames(null);
        RaisePreviewRequested();
    }

    partial void OnSelectedFontVariantNameChanged(string value) => RaisePreviewRequested();

    partial void OnSelectedCodeFontFamilyNameChanged(string value)
    {
        UpdateCodeFontVariantNames(null);
        RaisePreviewRequested();
    }

    partial void OnSelectedCodeFontVariantNameChanged(string value) => RaisePreviewRequested();

    partial void OnSelectedEditorFontSizeChanged(string value) => RaisePreviewRequested();

    partial void OnSelectedUiFontSizeChanged(string value) => RaisePreviewRequested();

    partial void OnIsAiEnabledChanged(bool value) => RaisePreviewRequested();

    partial void OnApiKeyChanged(string value) => RaisePreviewRequested();

    partial void OnDefaultModelChanged(string value) => RaisePreviewRequested();

    partial void OnProjectIdChanged(string value) => RaisePreviewRequested();

    partial void OnOrganizationIdChanged(string value) => RaisePreviewRequested();

    private void UpdateSidebarVariantNames(string? preferredVariant)
    {
        var variants = GetVariants(SelectedSidebarFontFamilyName);
        SidebarFontVariantNames = variants;
        SelectedSidebarFontVariantName = ResolveVariantSelection(variants, preferredVariant ?? SelectedSidebarFontVariantName);
    }

    private void UpdateFontVariantNames(string? preferredVariant)
    {
        var variants = GetVariants(SelectedFontFamilyName);
        FontVariantNames = variants;
        SelectedFontVariantName = ResolveVariantSelection(variants, preferredVariant ?? SelectedFontVariantName);
    }

    private void UpdateCodeFontVariantNames(string? preferredVariant)
    {
        var variants = GetVariants(SelectedCodeFontFamilyName);
        CodeFontVariantNames = variants;
        SelectedCodeFontVariantName = ResolveVariantSelection(variants, preferredVariant ?? SelectedCodeFontVariantName);
    }

    private IReadOnlyList<string> GetVariants(string familyName)
    {
        return _fontFamilies
            .FirstOrDefault(font => string.Equals(font.DisplayName, familyName, StringComparison.Ordinal))?
            .StandardVariants
            .Select(variant => variant.DisplayName)
            .ToList()
            ?? [];
    }

    private static string ResolveVariantSelection(IReadOnlyList<string> variants, string? preferred)
    {
        if (preferred is not null && variants.Contains(preferred, StringComparer.Ordinal))
        {
            return preferred;
        }

        return variants.FirstOrDefault() ?? string.Empty;
    }

    private void RaisePreviewRequested()
    {
        if (_isInitializing)
        {
            return;
        }

        PreviewRequested?.Invoke(this, BuildModel());
    }

    private static double ParseSize(string text, double fallback)
    {
        return double.TryParse(text, out var value) ? value : fallback;
    }
}
