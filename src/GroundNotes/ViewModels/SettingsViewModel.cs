using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using GroundNotes.Models;

namespace GroundNotes.ViewModels;

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
        IndentSizes = EditorDisplaySettings.SupportedIndentSizes
            .Select(static size => size.ToString(CultureInfo.InvariantCulture))
            .ToList();
        LineHeights = EditorDisplaySettings.SupportedLineHeightFactors
            .Select(EditorDisplaySettings.FormatLineHeight)
            .ToList();
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
        SelectedIndentSize = EditorDisplaySettings.NormalizeIndentSize(model.EditorIndentSize).ToString(CultureInfo.InvariantCulture);
        SelectedLineHeight = EditorDisplaySettings.FormatLineHeight(model.EditorLineHeightFactor);
        ShowScrollBars = model.ShowScrollBars;
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

    public IReadOnlyList<string> IndentSizes { get; }

    public IReadOnlyList<string> LineHeights { get; }

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
    private string _selectedIndentSize = EditorDisplaySettings.DefaultIndentSize.ToString(CultureInfo.InvariantCulture);

    [ObservableProperty]
    private string _selectedLineHeight = "1.15";

    [ObservableProperty]
    private bool _showScrollBars = true;

    [ObservableProperty]
    private bool _isAiEnabled = true;

    [ObservableProperty]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    private string _defaultModel = AiModelCatalog.DefaultChatModel;

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
            ParseIndentSize(SelectedIndentSize),
            ParseLineHeight(SelectedLineHeight),
            ShowScrollBars,
            IsAiEnabled,
            ApiKey.Trim(),
            string.IsNullOrWhiteSpace(DefaultModel) ? AiModelCatalog.DefaultChatModel : DefaultModel.Trim(),
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

    partial void OnSelectedIndentSizeChanged(string value) => RaisePreviewRequested();

    partial void OnSelectedLineHeightChanged(string value) => RaisePreviewRequested();

    partial void OnShowScrollBarsChanged(bool value) => RaisePreviewRequested();

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
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    }

    private static int ParseIndentSize(string text)
    {
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? EditorDisplaySettings.NormalizeIndentSize(value)
            : EditorDisplaySettings.DefaultIndentSize;
    }

    private static double ParseLineHeight(string text)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? EditorDisplaySettings.NormalizeLineHeightFactor(value)
            : EditorDisplaySettings.DefaultLineHeightFactor;
    }
}
