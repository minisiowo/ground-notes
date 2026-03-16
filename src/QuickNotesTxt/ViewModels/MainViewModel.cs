using System.Collections.ObjectModel;
using Avalonia.Threading;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuickNotesTxt.Models;
using QuickNotesTxt.Services;
using QuickNotesTxt.Styles;

namespace QuickNotesTxt.ViewModels;

public partial class MainViewModel : ViewModelBase, IDisposable
{
    private const double DefaultEditorFontSize = 12;
    private const double MinEditorFontSize = 10;
    private const double MaxEditorFontSize = 24;
    private const double DefaultUiFontSize = 12;
    private const double MinUiFontSize = 10;
    private const double MaxUiFontSize = 20;
    private const int NotePickerResultLimitValue = 10;

    private readonly INotesRepository _notesRepository;
    private readonly ISettingsService _settingsService;
    private readonly IFileWatcherService _fileWatcherService;
    private readonly IThemeLoaderService _themeLoaderService;
    private readonly IFontCatalogService _fontCatalogService;
    private readonly IAiPromptCatalogService _aiPromptCatalogService;
    private readonly IAiTextActionService _aiTextActionService;
    private readonly ObservableCollection<NoteSummary> _allNotes = [];
    private IReadOnlyList<AppTheme> _allThemes = AppTheme.BuiltInThemes;
    private IReadOnlyList<BundledFontFamilyOption> _allFonts =
    [
        new(
            FontCatalogService.DefaultFontKey,
            "Iosevka Slab",
            $"avares://QuickNotesTxt/Assets/Fonts/{FontCatalogService.DefaultFontKey}#Iosevka Slab",
            [new BundledFontVariantOption(FontCatalogService.DefaultVariantKey, FontCatalogService.DefaultVariantKey, FontWeight.Normal, FontStyle.Normal)])
    ];
    private CancellationTokenSource? _saveCts;
    private bool _isApplyingSelection;
    private DateTimeOffset _suppressWatcherUntil = DateTimeOffset.MinValue;

    [ObservableProperty]
    private ObservableCollection<NoteSummary> _visibleNotes = [];

    [ObservableProperty]
    private NoteSummary? _selectedNoteSummary;

    [ObservableProperty]
    private string _editorTitle = string.Empty;

    [ObservableProperty]
    private string _editorTags = string.Empty;

    [ObservableProperty]
    private string _editorBody = string.Empty;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string? _selectedTag = "All";

    [ObservableProperty]
    private ObservableCollection<string> _availableTags = ["All"];

    [ObservableProperty]
    private string _notesFolder = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Choose a folder to start.";

    [ObservableProperty]
    private string _lastSavedText = "QuickNotes";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SidebarToggleIcon))]
    private bool _sidebarCollapsed;

    public string SidebarToggleIcon => SidebarCollapsed ? "›" : "‹";

    [ObservableProperty]
    private SortOption _selectedSortOption = SortOption.LastModified;

    [ObservableProperty]
    private bool _hasConflict;

    [ObservableProperty]
    private double _editorFontSize = DefaultEditorFontSize;

    [ObservableProperty]
    private double _uiFontSize = DefaultUiFontSize;

    [ObservableProperty]
    private string _selectedThemeName = AppTheme.Dark.Name;

    [ObservableProperty]
    private IReadOnlyList<string> _themeNames = AppTheme.BuiltInThemes.Select(t => t.Name).ToList();

    [ObservableProperty]
    private string _selectedSidebarFontFamilyName = "Iosevka Slab";

    [ObservableProperty]
    private string _selectedSidebarFontVariantName = FontCatalogService.DefaultVariantKey;

    [ObservableProperty]
    private IReadOnlyList<string> _sidebarFontVariantNames = [FontCatalogService.DefaultVariantKey];

    [ObservableProperty]
    private string _selectedFontFamilyName = "Iosevka Slab";

    [ObservableProperty]
    private IReadOnlyList<string> _fontFamilyNames = ["Iosevka Slab"];

    [ObservableProperty]
    private string _selectedFontVariantName = FontCatalogService.DefaultVariantKey;

    [ObservableProperty]
    private IReadOnlyList<string> _fontVariantNames = [FontCatalogService.DefaultVariantKey];

    [ObservableProperty]
    private string _selectedCodeFontFamilyName = "JetBrains Mono";

    [ObservableProperty]
    private string _selectedCodeFontVariantName = FontCatalogService.DefaultVariantKey;

    [ObservableProperty]
    private IReadOnlyList<string> _codeFontVariantNames = [FontCatalogService.DefaultVariantKey];

    [ObservableProperty]
    private bool _isNotePickerOpen;

    [ObservableProperty]
    private string _notePickerQuery = string.Empty;

    [ObservableProperty]
    private ObservableCollection<NoteSummary> _notePickerResults = [];

    [ObservableProperty]
    private NoteSummary? _selectedNotePickerSummary;

    [ObservableProperty]
    private int _notePickerTotalMatchCount;

    [ObservableProperty]
    private IReadOnlyList<AiPromptDefinition> _aiPrompts = [];

    [ObservableProperty]
    private string _openAiApiKey = string.Empty;

    [ObservableProperty]
    private string _selectedAiModel = AiSettings.Default.DefaultModel;

    [ObservableProperty]
    private string _openAiProjectId = string.Empty;

    [ObservableProperty]
    private string _openAiOrganizationId = string.Empty;

    [ObservableProperty]
    private bool _isAiEnabled = AiSettings.Default.IsEnabled;

    [ObservableProperty]
    private bool _isAiBusy;

    public MainViewModel(
        INotesRepository notesRepository,
        ISettingsService settingsService,
        IFileWatcherService fileWatcherService,
        IThemeLoaderService themeLoaderService,
        IFontCatalogService fontCatalogService,
        IAiPromptCatalogService aiPromptCatalogService,
        IAiTextActionService aiTextActionService)
    {
        _notesRepository = notesRepository;
        _settingsService = settingsService;
        _fileWatcherService = fileWatcherService;
        _themeLoaderService = themeLoaderService;
        _fontCatalogService = fontCatalogService;
        _aiPromptCatalogService = aiPromptCatalogService;
        _aiTextActionService = aiTextActionService;
        _fileWatcherService.NotesChanged += OnNotesChanged;

        SortOptions = Enum.GetValues<SortOption>();
        _ = InitializeAsync();
    }

    public event EventHandler? FocusEditorRequested;

    public bool IsSettingsPreviewActive { get; private set; }

    public Func<Task<string?>>? PickFolderAsync { get; set; }

    public Func<string, Task<bool>>? ConfirmDeleteAsync { get; set; }

    public Func<SettingsDialogModel, Task<SettingsDialogModel?>>? ShowSettingsAsync { get; set; }

    public IReadOnlyList<SortOption> SortOptions { get; }

    public NoteDocument? CurrentNote { get; private set; }

    public bool HasUnsavedChanges { get; private set; }

    public bool HasSelectedFolder => !string.IsNullOrWhiteSpace(NotesFolder);

    public bool HasNotes => VisibleNotes.Count > 0;

    public bool HasAiPrompts => AiPrompts.Count > 0;

    public bool HasNotePickerResults => NotePickerResults.Count > 0;

    public int NotePickerResultLimit => NotePickerResultLimitValue;

    public bool IsNotePickerTruncated => NotePickerTotalMatchCount > NotePickerResultLimit;

    public string NotePickerStatusText => NotePickerTotalMatchCount switch
    {
        0 => "No matching notes.",
        _ when IsNotePickerTruncated => $"Showing {NotePickerResults.Count} of {NotePickerTotalMatchCount} matches",
        1 => "1 match",
        _ => $"{NotePickerTotalMatchCount} matches"
    };

    public string NotePickerFooterHint => IsNotePickerTruncated
        ? "Refine search to narrow more notes."
        : "Type to filter. Enter opens. Up and Down move. Esc closes.";

    public bool ShowFolderPrompt => !HasSelectedFolder;

    public bool ShowTitleWatermark => HasSelectedFolder && string.IsNullOrWhiteSpace(EditorTitle);

    public bool ShowTagsWatermark => HasSelectedFolder && string.IsNullOrWhiteSpace(EditorTags);

    public bool ShowEditorWatermark => HasSelectedFolder && string.IsNullOrWhiteSpace(EditorBody);

    public bool HasFooterStatusOverride => !string.IsNullOrWhiteSpace(StatusMessage) && !string.Equals(StatusMessage, "Ready.", StringComparison.Ordinal);

    public string FooterStatusText => HasFooterStatusOverride
        ? StatusMessage
        : LastSavedText;

    public string CurrentAiPromptsDirectory => !HasSelectedFolder
        ? string.Empty
        : _aiPromptCatalogService.GetNotesFolderPromptsDirectory(NotesFolder);


    partial void OnSearchTextChanged(string value) => RefreshVisibleNotes();

    partial void OnSelectedTagChanged(string? value)
    {
        OnPropertyChanged(nameof(HasActiveTagFilter));
        RefreshVisibleNotes();
    }

    public bool HasActiveTagFilter => !string.IsNullOrWhiteSpace(SelectedTag) && !string.Equals(SelectedTag, AllTagsFilter, StringComparison.Ordinal);

    partial void OnSelectedSortOptionChanged(SortOption value) => RefreshVisibleNotes();

    partial void OnNotePickerQueryChanged(string value)
    {
        if (IsNotePickerOpen)
        {
            RefreshNotePickerResults();
        }
    }

    partial void OnSelectedThemeNameChanged(string value)
    {
        var theme = _allThemes.FirstOrDefault(t => t.Name == value);
        if (theme is not null)
        {
            ThemeService.Apply(theme);
            if (!_isApplyingSelection && !IsSettingsPreviewActive)
            {
                _ = _settingsService.SetThemeNameAsync(value);
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

        ThemeService.ApplyTerminalFont(new FontFamily(fontFamily.ResourceUri), variant.FontWeight, variant.FontStyle);

        if (persist)
        {
            _ = PersistFontSelectionAsync(fontFamily.Key, variant.Key);
        }
    }

    private async Task PersistFontSelectionAsync(string fontFamilyKey, string fontVariantKey)
    {
        await _settingsService.SetFontNameAsync(fontFamilyKey);
        await _settingsService.SetFontVariantNameAsync(fontVariantKey);
    }

    private async Task PersistSidebarFontSelectionAsync(string fontFamilyKey, string fontVariantKey)
    {
        await _settingsService.SetSidebarFontNameAsync(fontFamilyKey);
        await _settingsService.SetSidebarFontVariantNameAsync(fontVariantKey);
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

        ThemeService.ApplySidebarFont(new FontFamily(fontFamily.ResourceUri), variant.FontWeight, variant.FontStyle);

        if (persist)
        {
            _ = PersistSidebarFontSelectionAsync(fontFamily.Key, variant.Key);
        }
    }

    private async Task PersistCodeFontSelectionAsync(string fontFamilyKey, string fontVariantKey)
    {
        await _settingsService.SetCodeFontNameAsync(fontFamilyKey);
        await _settingsService.SetCodeFontVariantNameAsync(fontVariantKey);
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

        ThemeService.ApplyCodeFont(new FontFamily(fontFamily.ResourceUri), variant.FontWeight, variant.FontStyle);

        if (persist)
        {
            _ = PersistCodeFontSelectionAsync(fontFamily.Key, variant.Key);
        }
    }

    partial void OnUiFontSizeChanged(double value)
    {
        ThemeService.ApplyUiFontSize(value);
    }

    partial void OnNotesFolderChanged(string value)
    {
        OnPropertyChanged(nameof(HasSelectedFolder));
        OnPropertyChanged(nameof(CurrentAiPromptsDirectory));
        OnPropertyChanged(nameof(ShowFolderPrompt));
        OnPropertyChanged(nameof(ShowTitleWatermark));
        OnPropertyChanged(nameof(ShowTagsWatermark));
        OnPropertyChanged(nameof(ShowEditorWatermark));
        OnPropertyChanged(nameof(FooterStatusText));
    }

    partial void OnVisibleNotesChanged(ObservableCollection<NoteSummary> value) => OnPropertyChanged(nameof(HasNotes));

    partial void OnNotePickerResultsChanged(ObservableCollection<NoteSummary> value) => OnPropertyChanged(nameof(HasNotePickerResults));

    partial void OnAiPromptsChanged(IReadOnlyList<AiPromptDefinition> value) => OnPropertyChanged(nameof(HasAiPrompts));

    partial void OnNotePickerTotalMatchCountChanged(int value)
    {
        OnPropertyChanged(nameof(IsNotePickerTruncated));
        OnPropertyChanged(nameof(NotePickerStatusText));
        OnPropertyChanged(nameof(NotePickerFooterHint));
    }

    partial void OnStatusMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasFooterStatusOverride));
        OnPropertyChanged(nameof(FooterStatusText));
    }

    partial void OnLastSavedTextChanged(string value) => OnPropertyChanged(nameof(FooterStatusText));

    partial void OnSelectedNoteSummaryChanged(NoteSummary? value)
    {
        if (_isApplyingSelection || value is null || value.IsRenaming)
        {
            return;
        }

        _ = LoadSelectedNoteAsync(value.FilePath);
    }

    partial void OnEditorTitleChanged(string value)
    {
        OnPropertyChanged(nameof(ShowTitleWatermark));
        ClearTransientStatusOnEdit();

        if (_isApplyingSelection || !HasSelectedFolder || CurrentNote is null)
        {
            return;
        }

        UpdateCurrentNoteFromEditor();
        ScheduleSave();
    }

    partial void OnEditorTagsChanged(string value)
    {
        OnPropertyChanged(nameof(ShowTagsWatermark));
        ClearTransientStatusOnEdit();

        if (_isApplyingSelection || !HasSelectedFolder || CurrentNote is null)
        {
            return;
        }

        UpdateCurrentNoteFromEditor();
        ScheduleSave();
    }

    partial void OnEditorBodyChanged(string value)
    {
        OnPropertyChanged(nameof(ShowEditorWatermark));
        ClearTransientStatusOnEdit();

        if (_isApplyingSelection || !HasSelectedFolder)
        {
            return;
        }

        if (!EnsureDraftExists(value))
        {
            return;
        }

        UpdateCurrentNoteFromEditor();
        ScheduleSave();
    }

    [RelayCommand]
    private async Task ChooseFolderAsync()
    {
        if (PickFolderAsync is null)
        {
            StatusMessage = "Folder picker is unavailable.";
            return;
        }

        var chosenFolder = await PickFolderAsync();
        if (string.IsNullOrWhiteSpace(chosenFolder))
        {
            return;
        }

        await SetFolderAsync(chosenFolder);
    }

    [RelayCommand]
    private async Task NewNoteAsync()
    {
        if (!HasSelectedFolder)
        {
            StatusMessage = "Choose a folder first.";
            return;
        }

        CancelInlineRename();
        await FlushPendingSaveAsync();

        _isApplyingSelection = true;
        try
        {
            CurrentNote = null;
            SelectedNoteSummary = null;
            EditorTitle = string.Empty;
            EditorTags = string.Empty;
            EditorBody = string.Empty;
            HasUnsavedChanges = false;
            HasConflict = false;
        }
        finally
        {
            _isApplyingSelection = false;
        }

        StatusMessage = "New note ready.";
        FocusEditorRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void OpenNotePicker()
    {
        if (!HasSelectedFolder)
        {
            StatusMessage = "Choose a folder first.";
            return;
        }

        CancelInlineRename();

        if (IsNotePickerOpen)
        {
            RefreshNotePickerResults();
            return;
        }

        IsNotePickerOpen = true;
        NotePickerQuery = string.Empty;
        RefreshNotePickerResults();
    }

    [RelayCommand]
    private void CloseNotePicker()
    {
        if (!IsNotePickerOpen)
        {
            return;
        }

        IsNotePickerOpen = false;
        NotePickerQuery = string.Empty;
        NotePickerResults = [];
        SelectedNotePickerSummary = null;
        NotePickerTotalMatchCount = 0;
    }

    [RelayCommand]
    private void MoveNotePickerSelection(int delta)
    {
        if (!IsNotePickerOpen || NotePickerResults.Count == 0)
        {
            return;
        }

        var currentIndex = SelectedNotePickerSummary is null
            ? -1
            : NotePickerResults.IndexOf(SelectedNotePickerSummary);

        if (currentIndex < 0)
        {
            SelectedNotePickerSummary = NotePickerResults[0];
            return;
        }

        var nextIndex = currentIndex + delta;
        if (nextIndex < 0)
        {
            nextIndex = NotePickerResults.Count - 1;
        }
        else if (nextIndex >= NotePickerResults.Count)
        {
            nextIndex = 0;
        }

        SelectedNotePickerSummary = NotePickerResults[nextIndex];
    }

    [RelayCommand]
    private void AcceptNotePickerSelection()
    {
        if (!IsNotePickerOpen)
        {
            return;
        }

        var note = SelectedNotePickerSummary ?? NotePickerResults.FirstOrDefault();
        if (note is null)
        {
            return;
        }

        CloseNotePicker();
        SelectedNoteSummary = note;
        FocusEditorRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void ToggleSidebar()
    {
        SidebarCollapsed = !SidebarCollapsed;
    }

    [RelayCommand]
    private void ClearTagFilter()
    {
        SelectedTag = AllTagsFilter;
    }

    [RelayCommand]
    private async Task ReloadAsync()
    {
        await RefreshFromDiskAsync();
    }

    [RelayCommand]
    private async Task IncreaseEditorFontSizeAsync()
    {
        await SetEditorFontSizeAsync(EditorFontSize + 1);
    }

    [RelayCommand]
    private async Task DecreaseEditorFontSizeAsync()
    {
        await SetEditorFontSizeAsync(EditorFontSize - 1);
    }

    [RelayCommand]
    private async Task IncreaseUiFontSizeAsync()
    {
        await SetUiFontSizeAsync(UiFontSize + 1);
    }

    [RelayCommand]
    private async Task DecreaseUiFontSizeAsync()
    {
        await SetUiFontSizeAsync(UiFontSize - 1);
    }

    [RelayCommand]
    private void StartRenameNote(NoteSummary? noteSummary)
    {
        if (noteSummary is null)
        {
            return;
        }

        CancelInlineRename();
        noteSummary.RenameText = noteSummary.DisplayName;
        noteSummary.IsRenaming = true;

        _isApplyingSelection = true;
        try
        {
            SelectedNoteSummary = noteSummary;
        }
        finally
        {
            _isApplyingSelection = false;
        }
    }

    [RelayCommand]
    private async Task DeleteNoteAsync(NoteSummary? noteSummary)
    {
        if (noteSummary is null)
        {
            return;
        }

        CancelInlineRename();

        var displayName = noteSummary.DisplayName;
        var shouldDelete = ConfirmDeleteAsync is null || await ConfirmDeleteAsync(displayName);
        if (!shouldDelete)
        {
            StatusMessage = "Delete canceled.";
            return;
        }

        CancelScheduledSave();
        SuppressWatcher();
        await _notesRepository.DeleteNoteIfExistsAsync(noteSummary.FilePath);
        RemoveSummary(noteSummary.FilePath);

        var deletedOpenNote = CurrentNote is not null && string.Equals(CurrentNote.FilePath, noteSummary.FilePath, StringComparison.OrdinalIgnoreCase);
        if (deletedOpenNote)
        {
            ClearEditor();
        }
        else if (SelectedNoteSummary is not null && string.Equals(SelectedNoteSummary.FilePath, noteSummary.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            _isApplyingSelection = true;
            try
            {
                SelectedNoteSummary = null;
            }
            finally
            {
                _isApplyingSelection = false;
            }
        }

        RefreshAvailableTags();
        RefreshVisibleNotes();
        StatusMessage = $"Deleted {displayName}";
    }

    [RelayCommand]
    private async Task DeleteCurrentNoteAsync()
    {
        NoteSummary? noteSummary = null;

        if (CurrentNote is not null)
        {
            noteSummary = _allNotes.FirstOrDefault(note => string.Equals(note.FilePath, CurrentNote.FilePath, StringComparison.OrdinalIgnoreCase))
                ?? BuildSummary(CurrentNote);
        }
        else if (SelectedNoteSummary is not null)
        {
            noteSummary = SelectedNoteSummary;
        }

        if (noteSummary is null)
        {
            return;
        }

        await DeleteNoteAsync(noteSummary);
    }

    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        if (ShowSettingsAsync is null)
        {
            StatusMessage = "Settings are unavailable.";
            return;
        }

        var original = BuildSettingsDialogModel();
        var updated = await ShowSettingsAsync(original);
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

    [RelayCommand]
    private async Task ReloadAiPromptsAsync()
    {
        await LoadAiPromptsAsync();
        StatusMessage = HasAiPrompts
            ? $"Loaded {AiPrompts.Count} AI prompts."
            : "No AI prompts were found.";
    }

    public async Task<string?> RunAiPromptAsync(AiPromptDefinition prompt, string selectedText, CancellationToken cancellationToken = default)
    {
        if (!IsAiEnabled)
        {
            StatusMessage = "AI is disabled in settings.";
            return null;
        }

        if (string.IsNullOrWhiteSpace(selectedText))
        {
            StatusMessage = "Select text first.";
            return null;
        }

        if (IsAiBusy)
        {
            StatusMessage = "AI is already processing a prompt.";
            return null;
        }

        IsAiBusy = true;
        StatusMessage = $"Running {prompt.Name}...";

        try
        {
            var result = await _aiTextActionService.RunPromptAsync(prompt, selectedText, BuildAiSettings(), cancellationToken);
            return result;
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "AI request canceled.";
            return null;
        }
        catch (InvalidOperationException ex)
        {
            StatusMessage = ex.Message;
            return null;
        }
        catch (HttpRequestException)
        {
            StatusMessage = "AI request failed.";
            return null;
        }
        finally
        {
            IsAiBusy = false;
        }
    }

    public async Task CommitRenameAsync(NoteSummary? noteSummary)
    {
        if (noteSummary is null || !noteSummary.IsRenaming || !HasSelectedFolder)
        {
            return;
        }

        var newName = noteSummary.RenameText.Trim();
        if (string.IsNullOrWhiteSpace(newName) || string.Equals(newName, noteSummary.DisplayName, StringComparison.Ordinal))
        {
            CancelInlineRename();
            return;
        }

        CancelScheduledSave();

        NoteDocument? document;
        if (CurrentNote is not null && string.Equals(CurrentNote.FilePath, noteSummary.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            UpdateCurrentNoteFromEditor();
            document = CurrentNote;
        }
        else
        {
            document = await _notesRepository.LoadNoteAsync(noteSummary.FilePath);
        }

        if (document is null)
        {
            CancelInlineRename();
            await RefreshFromDiskAsync();
            return;
        }

        SuppressWatcher();
        var renamed = await _notesRepository.RenameNoteAsync(NotesFolder, document, newName, CancellationToken.None);
        CancelInlineRename();

        if (CurrentNote is not null && string.Equals(CurrentNote.FilePath, noteSummary.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            CurrentNote = renamed;
            ApplyDocumentToEditor(renamed);
        }

        ReplaceSummary(noteSummary.FilePath, BuildSummary(renamed));
        RefreshAvailableTags();
        RefreshVisibleNotes();
        SelectSummaryByPath(renamed.FilePath);
        StatusMessage = $"Renamed to {Path.GetFileNameWithoutExtension(renamed.FilePath)}";
    }

    public void CancelRename(NoteSummary? noteSummary)
    {
        if (noteSummary is null)
        {
            return;
        }

        noteSummary.IsRenaming = false;
        noteSummary.RenameText = noteSummary.DisplayName;
    }

    private async Task InitializeAsync()
    {
        var settings = await _settingsService.GetSettingsAsync();
        EditorFontSize = ClampEditorFontSize(settings.EditorFontSize ?? DefaultEditorFontSize);
        UiFontSize = ClampUiFontSize(settings.UiFontSize ?? DefaultUiFontSize);

        _allFonts = _fontCatalogService.LoadBundledFonts();
        FontFamilyNames = _allFonts.Select(f => f.DisplayName).ToList();

        _allThemes = await _themeLoaderService.LoadAllThemesAsync();
        ThemeNames = _allThemes.Select(t => t.Name).ToList();
        ApplyAiSettings(settings.AiSettings);
        await LoadAiPromptsAsync();

        var initialFont = _allFonts.FirstOrDefault(f => string.Equals(f.Key, settings.FontName, StringComparison.Ordinal))
            ?? _allFonts.FirstOrDefault(f => string.Equals(f.Key, FontCatalogService.DefaultFontKey, StringComparison.Ordinal))
            ?? _allFonts[0];

        var initialVariant = ResolveFontVariant(initialFont, settings.FontVariantName ?? string.Empty)
            ?? GetDefaultFontVariant(initialFont);

        ApplyFontSelection(initialFont, initialVariant, persist: false);

        var initialSidebarFont = _allFonts.FirstOrDefault(f => string.Equals(f.Key, settings.SidebarFontName, StringComparison.Ordinal))
            ?? _allFonts.FirstOrDefault(f => string.Equals(f.Key, FontCatalogService.DefaultFontKey, StringComparison.Ordinal))
            ?? initialFont;
        var initialSidebarVariant = ResolveFontVariant(initialSidebarFont, settings.SidebarFontVariantName ?? string.Empty)
            ?? GetDefaultFontVariant(initialSidebarFont);
        ApplySidebarFontSelection(initialSidebarFont, initialSidebarVariant, persist: false);

        var initialCodeFont = _allFonts.FirstOrDefault(f => string.Equals(f.Key, settings.CodeFontName, StringComparison.Ordinal))
            ?? _allFonts.FirstOrDefault(f => string.Equals(f.Key, FontCatalogService.DefaultCodeFontKey, StringComparison.Ordinal))
            ?? initialFont;
        var initialCodeVariant = ResolveFontVariant(initialCodeFont, settings.CodeFontVariantName ?? string.Empty)
            ?? GetDefaultFontVariant(initialCodeFont);
        ApplyCodeFontSelection(initialCodeFont, initialCodeVariant, persist: false);

        if (!string.IsNullOrWhiteSpace(settings.ThemeName))
        {
            var theme = _allThemes.FirstOrDefault(t => t.Name == settings.ThemeName);
            if (theme is not null)
            {
                _isApplyingSelection = true;
                try
                {
                    SelectedThemeName = theme.Name;
                }
                finally
                {
                    _isApplyingSelection = false;
                }
                ThemeService.Apply(theme);
            }
        }

        if (!string.IsNullOrWhiteSpace(settings.NotesFolder))
        {
            await SetFolderAsync(settings.NotesFolder, focusEditorWhenReady: true);
        }
    }

    private async Task SetFolderAsync(string folderPath, bool focusEditorWhenReady = false)
    {
        Directory.CreateDirectory(folderPath);
        NotesFolder = folderPath;
        await _settingsService.SetNotesFolderAsync(folderPath);
        _fileWatcherService.Watch(folderPath);
        ClearEditor();
        LastSavedText = "QuickNotes";
        await RefreshFromDiskAsync();
        await LoadAiPromptsAsync();
        StatusMessage = "Ready.";

        if (focusEditorWhenReady)
        {
            FocusEditorRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task SetEditorFontSizeAsync(double fontSize)
    {
        var clamped = ClampEditorFontSize(fontSize);
        if (Math.Abs(EditorFontSize - clamped) < 0.01)
        {
            return;
        }

        EditorFontSize = clamped;
        await _settingsService.SetEditorFontSizeAsync(clamped);
        StatusMessage = $"Editor font size: {clamped:0}";
    }

    private async Task SetUiFontSizeAsync(double fontSize)
    {
        var clamped = ClampUiFontSize(fontSize);
        if (Math.Abs(UiFontSize - clamped) < 0.01)
        {
            return;
        }

        UiFontSize = clamped;
        await _settingsService.SetUiFontSizeAsync(clamped);
        StatusMessage = $"UI font size: {clamped:0}";
    }

    private static double ClampEditorFontSize(double fontSize)
    {
        return Math.Clamp(fontSize, MinEditorFontSize, MaxEditorFontSize);
    }

    private static double ClampUiFontSize(double fontSize)
    {
        return Math.Clamp(fontSize, MinUiFontSize, MaxUiFontSize);
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

        await SetEditorFontSizeAsync(model.EditorFontSize);
        await SetUiFontSizeAsync(model.UiFontSize);

        ApplyAiSettings(new AiSettings(
            model.ApiKey,
            model.DefaultModel,
            model.IsAiEnabled,
            model.ProjectId,
            model.OrganizationId));
        await _settingsService.SetAiSettingsAsync(BuildAiSettings());
    }

    public void ApplySettingsPreview(SettingsDialogModel model)
    {
        IsSettingsPreviewActive = true;
        ApplyThemeSelection(model.SelectedThemeName, persist: false);

        var sidebarFontFamily = GetFontFamilyByDisplayName(model.SelectedSidebarFontFamilyName)
            ?? _allFonts.FirstOrDefault(font => string.Equals(font.Key, FontCatalogService.DefaultFontKey, StringComparison.Ordinal))
            ?? _allFonts[0];
        var sidebarVariant = ResolveFontVariant(sidebarFontFamily, model.SelectedSidebarFontVariantName)
            ?? GetDefaultFontVariant(sidebarFontFamily);
        ApplySidebarFontSelection(sidebarFontFamily, sidebarVariant, persist: false);

        var fontFamily = GetFontFamilyByDisplayName(model.SelectedFontFamilyName)
            ?? _allFonts.FirstOrDefault(font => string.Equals(font.Key, FontCatalogService.DefaultFontKey, StringComparison.Ordinal))
            ?? _allFonts[0];
        var variant = ResolveFontVariant(fontFamily, model.SelectedFontVariantName)
            ?? GetDefaultFontVariant(fontFamily);
        ApplyFontSelection(fontFamily, variant, persist: false);

        var codeFontFamily = GetFontFamilyByDisplayName(model.SelectedCodeFontFamilyName)
            ?? _allFonts.FirstOrDefault(font => string.Equals(font.Key, FontCatalogService.DefaultCodeFontKey, StringComparison.Ordinal))
            ?? fontFamily;
        var codeVariant = ResolveFontVariant(codeFontFamily, model.SelectedCodeFontVariantName)
            ?? GetDefaultFontVariant(codeFontFamily);
        ApplyCodeFontSelection(codeFontFamily, codeVariant, persist: false);

        EditorFontSize = ClampEditorFontSize(model.EditorFontSize);
        UiFontSize = ClampUiFontSize(model.UiFontSize);
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

        ThemeService.Apply(theme);

        if (persist)
        {
            _ = _settingsService.SetThemeNameAsync(theme.Name);
        }
    }

    private async Task RefreshFromDiskAsync()
    {
        if (!HasSelectedFolder)
        {
            return;
        }

        var summaries = await _notesRepository.LoadSummariesAsync(NotesFolder);
        _allNotes.Clear();
        foreach (var summary in summaries)
        {
            summary.RenameText = summary.DisplayName;
            _allNotes.Add(summary);
        }

        RefreshAvailableTags();
        RefreshVisibleNotes();

        if (CurrentNote is null)
        {
            return;
        }

        var matchingSummary = _allNotes.FirstOrDefault(note => string.Equals(note.FilePath, CurrentNote.FilePath, StringComparison.OrdinalIgnoreCase));
        if (matchingSummary is null)
        {
            ClearEditor();
            StatusMessage = "The current note was removed.";
            return;
        }

        if (HasUnsavedChanges)
        {
            HasConflict = true;
            StatusMessage = "This note changed on disk while you had local edits. Reselect it to reload.";
            return;
        }

        var reloaded = await _notesRepository.LoadNoteAsync(matchingSummary.FilePath);
        if (reloaded is not null)
        {
            ApplyDocumentToEditor(reloaded);
            SelectSummaryByPath(reloaded.FilePath);
        }
    }

    private async Task LoadSelectedNoteAsync(string filePath)
    {
        var note = await _notesRepository.LoadNoteAsync(filePath);
        if (note is null)
        {
            return;
        }

        CancelInlineRename();
        ApplyDocumentToEditor(note);
        HasConflict = false;
        StatusMessage = "Ready.";
    }

    private void ApplyDocumentToEditor(NoteDocument note)
    {
        _isApplyingSelection = true;
        try
        {
            CurrentNote = note;
            EditorTitle = note.Title;
            EditorTags = string.Join(", ", note.Tags);
            EditorBody = note.Body;
            HasUnsavedChanges = false;
            LastSavedText = FormatLastSavedText(note.UpdatedAt);
        }
        finally
        {
            _isApplyingSelection = false;
        }
    }

    private void ClearEditor()
    {
        CancelScheduledSave();
        CancelInlineRename();

        _isApplyingSelection = true;
        try
        {
            CurrentNote = null;
            SelectedNoteSummary = null;
            EditorTitle = string.Empty;
            EditorTags = string.Empty;
            EditorBody = string.Empty;
            HasUnsavedChanges = false;
            HasConflict = false;
            LastSavedText = "QuickNotes";
        }
        finally
        {
            _isApplyingSelection = false;
        }
    }

    private bool EnsureDraftExists(string incomingBody)
    {
        if (CurrentNote is not null)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(incomingBody))
        {
            return false;
        }

        var draft = _notesRepository.CreateDraftNote(NotesFolder, DateTimeOffset.Now);
        CurrentNote = draft;

        _isApplyingSelection = true;
        try
        {
            if (!string.IsNullOrWhiteSpace(EditorTitle))
            {
                draft.Title = EditorTitle.Trim();
            }
            else
            {
                EditorTitle = draft.Title;
            }

            if (!string.IsNullOrWhiteSpace(EditorTags))
            {
                draft.Tags = ParseTags(EditorTags);
            }
            else
            {
                EditorTags = string.Empty;
            }
        }
        finally
        {
            _isApplyingSelection = false;
        }

        HasUnsavedChanges = true;
        return true;
    }

    private void UpdateCurrentNoteFromEditor()
    {
        if (CurrentNote is null)
        {
            return;
        }

        CurrentNote.Title = string.IsNullOrWhiteSpace(EditorTitle) ? CurrentNote.OriginalTitle : EditorTitle.Trim();
        CurrentNote.Body = EditorBody;
        CurrentNote.Tags = ParseTags(EditorTags);
        HasUnsavedChanges = true;
        HasConflict = false;
    }

    private void ScheduleSave()
    {
        CancelScheduledSave();
        _saveCts = new CancellationTokenSource();
        var token = _saveCts.Token;
        _ = SaveAfterDebounceAsync(token);
    }

    private async Task SaveAfterDebounceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(450, cancellationToken);
            await SaveCurrentNoteAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void ClearTransientStatusOnEdit()
    {
        if (IsTransientFooterStatus(StatusMessage))
        {
            StatusMessage = "Ready.";
        }
    }

    private static bool IsTransientFooterStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return false;
        }

        return status == "New note ready."
            || status == "Delete canceled."
            || status == "AI settings saved."
            || status == "AI request canceled."
            || status == "AI request failed."
            || status == "AI is disabled in settings."
            || status == "AI is already processing a prompt."
            || status == "Select text first."
            || status.StartsWith("Deleted ", StringComparison.Ordinal)
            || status.StartsWith("Renamed to ", StringComparison.Ordinal)
            || status.StartsWith("Editor font size: ", StringComparison.Ordinal)
            || status.StartsWith("UI font size: ", StringComparison.Ordinal)
            || status.StartsWith("Loaded ", StringComparison.Ordinal)
            || status.StartsWith("Running ", StringComparison.Ordinal)
            || status.EndsWith(" applied.", StringComparison.Ordinal);
    }
    private async Task SaveCurrentNoteAsync(CancellationToken cancellationToken)
    {
        if (CurrentNote is null || !HasSelectedFolder)
        {
            return;
        }

        if (ShouldDeleteEmptyAutoCreatedNote(CurrentNote))
        {
            SuppressWatcher();
            await _notesRepository.DeleteNoteIfExistsAsync(CurrentNote.FilePath, cancellationToken);
            RemoveSummary(CurrentNote.FilePath);
            RefreshAvailableTags();
            RefreshVisibleNotes();
            ClearEditor();
            StatusMessage = "Empty draft discarded.";
            return;
        }

        var saved = await PersistNoteAsync(CurrentNote, cancellationToken);
        CurrentNote = saved;
        HasUnsavedChanges = false;
        LastSavedText = FormatLastSavedText(saved.UpdatedAt);
    }

    private async Task<NoteDocument> PersistNoteAsync(NoteDocument document, CancellationToken cancellationToken)
    {
        var previousPath = document.FilePath;
        SuppressWatcher();
        var saved = await _notesRepository.SaveNoteAsync(NotesFolder, document, cancellationToken);
        ReplaceSummary(previousPath, BuildSummary(saved));
        RefreshAvailableTags();
        RefreshVisibleNotes();
        SelectSummaryByPath(saved.FilePath);
        return saved;
    }

    private async Task FlushPendingSaveAsync()
    {
        if (!HasUnsavedChanges || CurrentNote is null)
        {
            return;
        }

        CancelScheduledSave();
        await SaveCurrentNoteAsync(CancellationToken.None);
    }

    private void CancelScheduledSave()
    {
        _saveCts?.Cancel();
        _saveCts?.Dispose();
        _saveCts = null;
    }

    private void CancelInlineRename()
    {
        foreach (var note in _allNotes.Where(note => note.IsRenaming))
        {
            note.IsRenaming = false;
            note.RenameText = note.DisplayName;
        }
    }

    private static bool ShouldDeleteEmptyAutoCreatedNote(NoteDocument note)
    {
        return note.IsAutoCreated && string.IsNullOrWhiteSpace(note.Body) && note.Tags.Count == 0 && string.Equals(note.Title, note.OriginalTitle, StringComparison.Ordinal);
    }

    private void RefreshVisibleNotes()
    {
        var effectiveTag = string.Equals(SelectedTag, AllTagsFilter, StringComparison.Ordinal) ? null : SelectedTag;
        VisibleNotes = new ObservableCollection<NoteSummary>(_notesRepository.QueryNotes(_allNotes, SearchText, effectiveTag, SelectedSortOption));

        if (IsNotePickerOpen)
        {
            RefreshNotePickerResults();
        }
    }

    private void RefreshNotePickerResults()
    {
        if (!IsNotePickerOpen)
        {
            return;
        }

        var allResults = _notesRepository.QueryNotesForPicker(_allNotes, NotePickerQuery, maxResults: 0);
        NotePickerTotalMatchCount = allResults.Count;

        var results = NotePickerTotalMatchCount <= NotePickerResultLimit
            ? allResults
            : allResults.Take(NotePickerResultLimit).ToList();

        NotePickerResults = new ObservableCollection<NoteSummary>(results);

        if (NotePickerResults.Count == 0)
        {
            SelectedNotePickerSummary = null;
            return;
        }

        if (SelectedNotePickerSummary is not null)
        {
            var matching = NotePickerResults.FirstOrDefault(note => string.Equals(note.FilePath, SelectedNotePickerSummary.FilePath, StringComparison.OrdinalIgnoreCase));
            if (matching is not null)
            {
                SelectedNotePickerSummary = matching;
                return;
            }
        }

        var currentSelection = CurrentNote is null
            ? null
            : NotePickerResults.FirstOrDefault(note => string.Equals(note.FilePath, CurrentNote.FilePath, StringComparison.OrdinalIgnoreCase));

        SelectedNotePickerSummary = currentSelection ?? NotePickerResults[0];
    }

    private const string AllTagsFilter = "All";

    private void RefreshAvailableTags()
    {
        var tags = _allNotes
            .SelectMany(note => note.Tags)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToList();

        tags.Insert(0, AllTagsFilter);
        AvailableTags = new ObservableCollection<string>(tags);
    }

    private void ReplaceSummary(string previousPath, NoteSummary summary)
    {
        RemoveSummary(previousPath);
        _allNotes.Add(summary);
    }

    private void RemoveSummary(string filePath)
    {
        var existing = _allNotes.FirstOrDefault(note => string.Equals(note.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            _allNotes.Remove(existing);
        }
    }

    private void SelectSummaryByPath(string filePath)
    {
        var matching = _allNotes.FirstOrDefault(note => string.Equals(note.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        if (matching is null)
        {
            return;
        }

        _isApplyingSelection = true;
        try
        {
            SelectedNoteSummary = matching;
        }
        finally
        {
            _isApplyingSelection = false;
        }
    }

    private static NoteSummary BuildSummary(NoteDocument document)
    {
        return new NoteSummary
        {
            Id = document.FilePath,
            FilePath = document.FilePath,
            Title = document.Title,
            Tags = [.. document.Tags],
            CreatedAt = document.CreatedAt,
            UpdatedAt = document.UpdatedAt,
            Preview = NotePreviewFormatter.Build(document.Body),
            SearchText = string.Join(' ', new[] { document.Title, document.Body, string.Join(' ', document.Tags) }),
            RenameText = Path.GetFileNameWithoutExtension(document.FilePath)
        };
    }

    private void SuppressWatcher()
    {
        _suppressWatcherUntil = DateTimeOffset.UtcNow.AddMilliseconds(900);
    }

    private static List<string> ParseTags(string input)
    {
        return input
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string FormatLastSavedText(DateTimeOffset updatedAt)
    {
        return $"Last saved: {updatedAt.LocalDateTime:yyyy-MM-dd HH:mm}";
    }

    private void OnNotesChanged(object? sender, EventArgs e)
    {
        if (DateTimeOffset.UtcNow < _suppressWatcherUntil)
        {
            return;
        }

        _ = Dispatcher.UIThread.InvokeAsync(async () => await RefreshFromDiskAsync());
    }

    public void Dispose()
    {
        _fileWatcherService.NotesChanged -= OnNotesChanged;
        _fileWatcherService.Dispose();
        CancelScheduledSave();
    }

    private void ApplyAiSettings(AiSettings settings)
    {
        OpenAiApiKey = settings.ApiKey;
        SelectedAiModel = string.IsNullOrWhiteSpace(settings.DefaultModel)
            ? AiSettings.Default.DefaultModel
            : settings.DefaultModel.Trim();
        OpenAiProjectId = settings.ProjectId;
        OpenAiOrganizationId = settings.OrganizationId;
        IsAiEnabled = settings.IsEnabled;
    }

    private AiSettings BuildAiSettings()
    {
        var model = string.IsNullOrWhiteSpace(SelectedAiModel)
            ? AiSettings.Default.DefaultModel
            : SelectedAiModel.Trim();

        return new AiSettings(OpenAiApiKey.Trim(), model, IsAiEnabled, OpenAiProjectId.Trim(), OpenAiOrganizationId.Trim());
    }

    private async Task LoadAiPromptsAsync()
    {
        AiPrompts = await _aiPromptCatalogService.LoadPromptsAsync(HasSelectedFolder ? NotesFolder : null);
    }
}
