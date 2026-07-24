using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroundNotes.Models;
using GroundNotes.Services;

namespace GroundNotes.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly IReadOnlyList<BundledFontFamilyOption> _fontFamilies;
    private KeyboardShortcutSettings _appliedKeyboardShortcuts = KeyboardShortcutSettings.CreateDefault();
    private bool _isInitializing;

    public SettingsViewModel(SettingsDialogModel model)
    {
        _isInitializing = true;
        ThemeNames = model.ThemeNames;
        _fontFamilies = model.FontFamilies;
        FontFamilies = model.FontFamilies.Select(font => font.DisplayName).ToList();
        EditorFontSizes = Enumerable.Range(10, 15).Select(static size => size.ToString()).ToList();
        UiFontSizes = Enumerable.Range(10, 11).Select(static size => size.ToString()).ToList();
        FileListFontSizes = Enumerable.Range(8, 11).Select(static size => size.ToString()).ToList();
        IndentSizes = EditorDisplaySettings.SupportedIndentSizes
            .Select(static size => size.ToString(CultureInfo.InvariantCulture))
            .ToList();
        LineHeights = EditorDisplaySettings.SupportedLineHeightFactors
            .Select(EditorDisplaySettings.FormatLineHeight)
            .ToList();
        AvailableModels = BuildAvailableModels(model.DefaultModel);
        ReasoningEfforts = AiReasoningEffortCatalog.ReasoningEfforts;
        PromptsDirectory = string.IsNullOrWhiteSpace(model.PromptsDirectory)
            ? "Choose a notes folder first."
            : model.PromptsDirectory;
        var keyboardShortcuts = KeyboardShortcutSettings.Normalize(model.KeyboardShortcuts);
        AvailableShortcutKeys = BuildAvailableShortcutKeys(keyboardShortcuts);
        ShortcutActions = new ObservableCollection<KeyboardShortcutActionViewModel>(
            KeyboardShortcutCatalog.Definitions.Select(definition =>
            {
                var bindings = keyboardShortcuts.Bindings.TryGetValue(definition.Id, out var configured)
                    ? configured
                    : definition.DefaultBindings;
                var item = new KeyboardShortcutActionViewModel(
                    definition,
                    bindings,
                    AvailableShortcutKeys);
                item.Changed += OnShortcutActionChanged;
                return item;
            }));
        RefreshShortcutFilter();
        _appliedKeyboardShortcuts = BuildCurrentKeyboardShortcutSettings();
        RefreshShortcutValidation();
        _appliedKeyboardShortcuts = BuildAppliedKeyboardShortcutSettings();

        SelectedThemeName = model.SelectedThemeName;
        SelectedUiFontFamilyName = model.SelectedUiFontFamilyName;
        UpdateUiVariantNames(model.SelectedUiFontVariantName);
        SelectedFontFamilyName = model.SelectedFontFamilyName;
        UpdateFontVariantNames(model.SelectedFontVariantName);
        SelectedCodeFontFamilyName = model.SelectedCodeFontFamilyName;
        UpdateCodeFontVariantNames(model.SelectedCodeFontVariantName);
        SelectedEditorFontSize = Math.Round(model.EditorFontSize).ToString("0");
        SelectedUiFontSize = Math.Round(model.UiFontSize).ToString("0");
        SelectedFileListFontSize = Math.Round(model.FileListFontSize).ToString("0");
        ShowSidebarListBackground = model.ShowSidebarListBackground;
        ShowSidebarListBorder = model.ShowSidebarListBorder;
        SelectedIndentSize = EditorDisplaySettings.NormalizeIndentSize(model.EditorIndentSize).ToString(CultureInfo.InvariantCulture);
        SelectedLineHeight = EditorDisplaySettings.FormatLineHeight(model.EditorLineHeightFactor);
        ShowScrollBars = model.ShowScrollBars;
        IsAiEnabled = model.IsAiEnabled;
        ApiKey = model.ApiKey;
        DefaultModel = string.IsNullOrWhiteSpace(model.DefaultModel) ? AiModelCatalog.DefaultChatModel : model.DefaultModel;
        DefaultReasoningEffort = AiReasoningEffortCatalog.Normalize(model.DefaultReasoningEffort);
        ProjectId = model.ProjectId;
        OrganizationId = model.OrganizationId;
        SetAiPrompts(model.AiPrompts);
        _isInitializing = false;
    }

    public event EventHandler<SettingsDialogModel>? PreviewRequested;

    public IReadOnlyList<string> ThemeNames { get; }

    public IReadOnlyList<string> FontFamilies { get; }

    public IReadOnlyList<string> EditorFontSizes { get; }

    public IReadOnlyList<string> UiFontSizes { get; }

    public IReadOnlyList<string> FileListFontSizes { get; }

    public IReadOnlyList<string> IndentSizes { get; }

    public IReadOnlyList<string> LineHeights { get; }

    public IReadOnlyList<string> AvailableModels { get; }

    public IReadOnlyList<string> ReasoningEfforts { get; }

    public string PromptsDirectory { get; }

    public IReadOnlyList<string> AvailableShortcutKeys { get; }

    [ObservableProperty]
    private string _selectedThemeName = string.Empty;



    [ObservableProperty]
    private string _selectedUiFontFamilyName = string.Empty;

    [ObservableProperty]
    private string _selectedUiFontVariantName = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<string> _uiFontVariantNames = [];

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
    private string _selectedFileListFontSize = "11";

    [ObservableProperty]
    private bool _showSidebarListBackground = true;

    [ObservableProperty]
    private bool _showSidebarListBorder = true;

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
    private string _defaultReasoningEffort = AiReasoningEffortCatalog.DefaultReasoningEffort;

    [ObservableProperty]
    private string _projectId = string.Empty;

    [ObservableProperty]
    private string _organizationId = string.Empty;

    [ObservableProperty]
    private ObservableCollection<KeyboardShortcutActionViewModel> _shortcutActions = [];

    [ObservableProperty]
    private IReadOnlyList<KeyboardShortcutActionViewModel> _filteredShortcutActions = [];

    [ObservableProperty]
    private string _shortcutSearchText = string.Empty;

    public string ShortcutFilterSummary => string.IsNullOrWhiteSpace(ShortcutSearchText)
        ? $"{ShortcutActions.Count} shortcuts"
        : $"Showing {FilteredShortcutActions.Count} of {ShortcutActions.Count} shortcuts";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasShortcutConflict))]
    private string _shortcutConflictMessage = string.Empty;

    [ObservableProperty]
    private string _shortcutWarningMessage = string.Empty;

    public bool HasShortcutConflict => !string.IsNullOrWhiteSpace(ShortcutConflictMessage);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedPrompt))]
    [NotifyPropertyChangedFor(nameof(CanEditSelectedPrompt))]
    [NotifyPropertyChangedFor(nameof(CanDeleteSelectedPrompt))]
    private AiPromptListItemViewModel? _selectedPrompt;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPrompts))]
    private ObservableCollection<AiPromptListItemViewModel> _promptItems = [];

    public bool HasPrompts => PromptItems.Count > 0;

    public bool HasSelectedPrompt => SelectedPrompt is not null;

    public bool CanEditSelectedPrompt => SelectedPrompt?.CanEdit == true;

    public bool CanDeleteSelectedPrompt => SelectedPrompt?.CanDelete == true;

    public SettingsDialogModel BuildModel()
    {
        return new SettingsDialogModel(
            ThemeNames,
            _fontFamilies,
            SelectedThemeName,
            SelectedUiFontFamilyName,
            SelectedUiFontVariantName,
            SelectedFontFamilyName,
            SelectedFontVariantName,
            SelectedCodeFontFamilyName,
            SelectedCodeFontVariantName,
            ParseSize(SelectedEditorFontSize, 12),
            ParseSize(SelectedUiFontSize, 12),
            ParseSize(SelectedFileListFontSize, 11),
            ShowSidebarListBackground,
            ShowSidebarListBorder,
            ParseIndentSize(SelectedIndentSize),
            ParseLineHeight(SelectedLineHeight),
            ShowScrollBars,
            IsAiEnabled,
            ApiKey.Trim(),
            string.IsNullOrWhiteSpace(DefaultModel) ? AiModelCatalog.DefaultChatModel : DefaultModel.Trim(),
            AiReasoningEffortCatalog.Normalize(DefaultReasoningEffort),
            ProjectId.Trim(),
            OrganizationId.Trim(),
            PromptsDirectory,
            PromptItems.Select(item => item.Definition).ToList(),
            _appliedKeyboardShortcuts);
    }

    partial void OnSelectedThemeNameChanged(string value) => RaisePreviewRequested();



    partial void OnSelectedUiFontFamilyNameChanged(string value)
    {
        UpdateUiVariantNames(null);
        RaisePreviewRequested();
    }

    partial void OnSelectedUiFontVariantNameChanged(string value) => RaisePreviewRequested();

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

    partial void OnSelectedFileListFontSizeChanged(string value) => RaisePreviewRequested();

    partial void OnShowSidebarListBackgroundChanged(bool value) => RaisePreviewRequested();

    partial void OnShowSidebarListBorderChanged(bool value) => RaisePreviewRequested();

    partial void OnSelectedIndentSizeChanged(string value) => RaisePreviewRequested();

    partial void OnSelectedLineHeightChanged(string value) => RaisePreviewRequested();

    partial void OnShowScrollBarsChanged(bool value) => RaisePreviewRequested();

    partial void OnIsAiEnabledChanged(bool value) => RaisePreviewRequested();

    partial void OnApiKeyChanged(string value) => RaisePreviewRequested();

    partial void OnDefaultModelChanged(string value)
    {
        RefreshPromptLabels();
        RaisePreviewRequested();
    }

    partial void OnDefaultReasoningEffortChanged(string value)
    {
        DefaultReasoningEffort = AiReasoningEffortCatalog.Normalize(value);
        RefreshPromptLabels();
        RaisePreviewRequested();
    }

    partial void OnProjectIdChanged(string value) => RaisePreviewRequested();

    partial void OnOrganizationIdChanged(string value) => RaisePreviewRequested();

    partial void OnShortcutSearchTextChanged(string value)
    {
        RefreshShortcutFilter();
    }

    [RelayCommand]
    private void ResetAllShortcuts()
    {
        _isInitializing = true;
        foreach (var action in ShortcutActions)
        {
            action.ResetShortcutsCommand.Execute(null);
        }
        _isInitializing = false;
        HandleShortcutSettingsChanged();
    }

    public void SetAiPrompts(IReadOnlyList<AiPromptDefinition> prompts)
    {
        PromptItems = new ObservableCollection<AiPromptListItemViewModel>(
            prompts.Select(prompt => new AiPromptListItemViewModel(
                prompt,
                string.IsNullOrWhiteSpace(DefaultModel) ? AiModelCatalog.DefaultChatModel : DefaultModel,
                AiReasoningEffortCatalog.Normalize(DefaultReasoningEffort))));
        SelectedPrompt = null;
    }

    private void RefreshShortcutFilter()
    {
        var query = ShortcutSearchText.Trim();
        FilteredShortcutActions = string.IsNullOrEmpty(query)
            ? ShortcutActions.ToList()
            : ShortcutActions
                .Where(action => action.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                                 || action.Category.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();
        OnPropertyChanged(nameof(ShortcutFilterSummary));
    }

    private void OnShortcutActionChanged(object? sender, EventArgs e)
    {
        HandleShortcutSettingsChanged(sender as KeyboardShortcutBindingViewModel);
    }

    private void HandleShortcutSettingsChanged(KeyboardShortcutBindingViewModel? changedBinding = null)
    {
        RefreshShortcutValidation(changedBinding);
        _appliedKeyboardShortcuts = BuildAppliedKeyboardShortcutSettings();
        RaisePreviewRequested();
    }

    private KeyboardShortcutSettings BuildCurrentKeyboardShortcutSettings()
    {
        return new KeyboardShortcutSettings(
            ShortcutActions.ToDictionary(
                action => action.Id,
                action => action.BuildBindings().ToList(),
                StringComparer.Ordinal));
    }

    private KeyboardShortcutSettings BuildAppliedKeyboardShortcutSettings()
    {
        return new KeyboardShortcutSettings(
            ShortcutActions.ToDictionary(
                action => action.Id,
                action => action.Bindings
                    .Where(binding => binding.IsApplied && !binding.IsEmpty)
                    .Select(binding => binding.BuildBinding())
                    .ToList(),
                StringComparer.Ordinal));
    }

    private void RefreshShortcutValidation(KeyboardShortcutBindingViewModel? changedBinding = null)
    {
        var settings = BuildCurrentKeyboardShortcutSettings();
        var service = new KeyboardShortcutService();
        service.ApplySettings(settings);
        var entries = ShortcutActions
            .SelectMany(action => action.Bindings
                .Where(binding => !binding.IsEmpty)
                .Select(binding => new ShortcutValidationEntry(
                    action,
                    binding,
                    binding.BuildBinding(),
                    service.Format(binding.BuildBinding()))))
            .ToList();
        var invalidBindings = new HashSet<KeyboardShortcutBindingViewModel>();
        var previouslyInvalidBindings = entries
            .Where(entry => !entry.ViewModel.IsApplied)
            .Select(entry => entry.ViewModel)
            .ToHashSet();

        foreach (var entry in entries)
        {
            entry.ViewModel.SetValidation(null);
        }

        foreach (var entry in entries.Where(entry =>
                     !entry.Display.Contains('+', StringComparison.Ordinal)
                     && RunsInsideTextInput(entry.Action)
                     && !IsSafeUnmodifiedTextInputKey(entry.Binding.Key)))
        {
            RejectBinding(entry, $"{entry.Display} would interfere with typing.", invalidBindings);
        }

        var fixedShortcuts = BuildFixedShortcutEntries();
        foreach (var entry in entries.Where(entry => !invalidBindings.Contains(entry.ViewModel)))
        {
            var fixedConflict = fixedShortcuts.FirstOrDefault(fixedShortcut =>
                KeyboardShortcutCatalog.ScopesOverlap(entry.Action.Scope, fixedShortcut.Scope)
                && string.Equals(entry.Display, fixedShortcut.Display, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(fixedConflict.Display))
            {
                RejectBinding(entry, $"Reserved for '{fixedConflict.Name}'.", invalidBindings);
            }
        }

        for (var index = 0; index < entries.Count; index++)
        {
            for (var otherIndex = index + 1; otherIndex < entries.Count; otherIndex++)
            {
                var first = entries[index];
                var second = entries[otherIndex];
                if (invalidBindings.Contains(first.ViewModel)
                    || invalidBindings.Contains(second.ViewModel)
                    || !KeyboardShortcutCatalog.ScopesOverlap(first.Action.Scope, second.Action.Scope)
                    || !string.Equals(first.Display, second.Display, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var rejected = ReferenceEquals(first.ViewModel, changedBinding) ? first
                    : ReferenceEquals(second.ViewModel, changedBinding) ? second
                    : previouslyInvalidBindings.Contains(first.ViewModel) ? first
                    : second;
                var accepted = ReferenceEquals(rejected, first) ? second : first;
                var message = string.Equals(first.Action.Id, second.Action.Id, StringComparison.Ordinal)
                    ? $"Duplicate shortcut for '{accepted.Action.Name}'."
                    : $"Already used by '{accepted.Action.Name}'.";
                RejectBinding(rejected, message, invalidBindings);
            }
        }

        ShortcutConflictMessage = invalidBindings.Count == 0
            ? string.Empty
            : $"{invalidBindings.Count} shortcut{(invalidBindings.Count == 1 ? " is" : "s are")} not applied. See the affected row{(invalidBindings.Count == 1 ? string.Empty : "s")}.";

        var unmodified = entries.FirstOrDefault(entry =>
            !invalidBindings.Contains(entry.ViewModel)
            && !entry.Display.Contains('+', StringComparison.Ordinal)
            && IsPrintableKey(entry.Binding.Key)
            && !RunsInsideTextInput(entry.Action));
        ShortcutWarningMessage = unmodified is null
            ? string.Empty
            : $"Warning: {unmodified.Display} may interfere with typing and will be ignored while a text input is active.";
    }

    private static void RejectBinding(
        ShortcutValidationEntry entry,
        string message,
        ISet<KeyboardShortcutBindingViewModel> invalidBindings)
    {
        invalidBindings.Add(entry.ViewModel);
        entry.ViewModel.SetValidation(message);
    }

    private static bool RunsInsideTextInput(KeyboardShortcutActionViewModel action)
    {
        return action.Scope is KeyboardShortcutScope.Editor or KeyboardShortcutScope.TitleSuggestions or KeyboardShortcutScope.Chat
               || action.Id is KeyboardShortcutActionIds.DeleteNote
                   or KeyboardShortcutActionIds.ToggleYaml
                   or KeyboardShortcutActionIds.ToggleSidebar
                   or KeyboardShortcutActionIds.ToggleZenMode
                   or KeyboardShortcutActionIds.ShowShortcuts;
    }

    private static IReadOnlyList<(KeyboardShortcutScope Scope, string Display, string Name)> BuildFixedShortcutEntries()
    {
        return
        [
            (KeyboardShortcutScope.Editor, "Ctrl+Z", "Undo"),
            (KeyboardShortcutScope.Editor, "Ctrl+Y", "Redo"),
            (KeyboardShortcutScope.Editor, "Ctrl+Shift+Z", "Redo"),
            (KeyboardShortcutScope.Editor, "Meta+Z", "Undo"),
            (KeyboardShortcutScope.Editor, "Meta+Shift+Z", "Redo"),
            (KeyboardShortcutScope.Editor, "Ctrl+C", "Copy"),
            (KeyboardShortcutScope.Editor, "Ctrl+X", "Cut"),
            (KeyboardShortcutScope.Editor, "Ctrl+V", "Paste"),
            (KeyboardShortcutScope.Editor, "Tab", "Indent"),
            (KeyboardShortcutScope.Editor, "Shift+Tab", "Outdent"),
            (KeyboardShortcutScope.MainWindow, "Ctrl+OemPlus", "Increase editor font size"),
            (KeyboardShortcutScope.MainWindow, "Ctrl+Add", "Increase editor font size"),
            (KeyboardShortcutScope.MainWindow, "Ctrl+Shift+OemPlus", "Increase UI font size"),
            (KeyboardShortcutScope.MainWindow, "Ctrl+OemMinus", "Decrease editor font size"),
            (KeyboardShortcutScope.MainWindow, "Ctrl+Subtract", "Decrease editor font size"),
            (KeyboardShortcutScope.MainWindow, "Ctrl+Shift+OemMinus", "Decrease UI font size"),
            (KeyboardShortcutScope.Chat, "Enter", "Accept mention"),
            (KeyboardShortcutScope.Chat, "Escape", "Dismiss mention"),
            (KeyboardShortcutScope.Chat, "Up", "Move mention selection"),
            (KeyboardShortcutScope.Chat, "Down", "Move mention selection")
        ];
    }



    private static bool IsPrintableKey(string key)
    {
        return key.Length == 1
               || (key.Length == 2 && key[0] == 'D' && char.IsDigit(key[1]))
               || key.StartsWith("NumPad", StringComparison.Ordinal)
               || key.StartsWith("Oem", StringComparison.Ordinal)
               || key is "Space";
    }

    private static bool IsSafeUnmodifiedTextInputKey(string key)
    {
        return key.Length is 2 or 3
               && key[0] == 'F'
               && int.TryParse(key[1..], NumberStyles.None, CultureInfo.InvariantCulture, out var functionKey)
               && functionKey is >= 1 and <= 24;
    }

    private static IReadOnlyList<string> BuildAvailableShortcutKeys(KeyboardShortcutSettings settings)
    {
        var configuredKeys = settings.Bindings.Values.SelectMany(bindings => bindings).Select(binding => binding.Key);
        var defaultKeys = KeyboardShortcutCatalog.Definitions.SelectMany(definition => definition.DefaultBindings).Select(binding => binding.Key);
        return Enum.GetValues<Key>()
            .Where(key => key != Key.None)
            .Select(key => key.ToString())
            .Where(key => !key.Contains("Ctrl", StringComparison.OrdinalIgnoreCase)
                          && !key.Contains("Shift", StringComparison.OrdinalIgnoreCase)
                          && !key.Contains("Alt", StringComparison.OrdinalIgnoreCase)
                          && !key.Contains("Win", StringComparison.OrdinalIgnoreCase))
            .Concat(defaultKeys)
            .Concat(configuredKeys)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private sealed record ShortcutValidationEntry(
        KeyboardShortcutActionViewModel Action,
        KeyboardShortcutBindingViewModel ViewModel,
        KeyboardShortcutBinding Binding,
        string Display);

    private void RefreshPromptLabels()
    {
        SetAiPrompts(PromptItems.Select(item => item.Definition).ToList());
    }

    private static IReadOnlyList<string> BuildAvailableModels(string? currentModel)
    {
        var models = AiModelCatalog.ChatCompletionModels.ToList();
        if (!string.IsNullOrWhiteSpace(currentModel)
            && !models.Contains(currentModel.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            models.Add(currentModel.Trim());
        }

        return models;
    }



    private void UpdateUiVariantNames(string? preferredVariant)
    {
        var variants = GetVariants(SelectedUiFontFamilyName);
        UiFontVariantNames = variants;
        SelectedUiFontVariantName = ResolveVariantSelection(variants, preferredVariant ?? SelectedUiFontVariantName);
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
