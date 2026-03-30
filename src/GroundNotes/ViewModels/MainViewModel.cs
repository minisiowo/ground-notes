using System.Collections.ObjectModel;
using System.Globalization;
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
    private const double DefaultEditorFontSize = 12;
    private const double MinEditorFontSize = 10;
    private const double MaxEditorFontSize = 24;
    private const double DefaultUiFontSize = 12;
    private const double MinUiFontSize = 10;
    private const double MaxUiFontSize = 20;
    private const int DefaultEditorIndentSize = EditorDisplaySettings.DefaultIndentSize;
    private const double DefaultEditorLineHeightFactor = EditorDisplaySettings.DefaultLineHeightFactor;
    private const int NotePickerResultLimitValue = 10;

    private readonly INotesRepository _notesRepository;
    private readonly ISettingsService _settingsService;
    private readonly IFileWatcherService _fileWatcherService;
    private readonly IThemeLoaderService _themeLoaderService;
    private readonly IFontCatalogService _fontCatalogService;
    private readonly IAiPromptCatalogService _aiPromptCatalogService;
    private readonly IAiTextActionService _aiTextActionService;
    private readonly IAiTitleSuggestionService _aiTitleSuggestionService;
    private readonly INoteMutationService _noteMutationService;
    private readonly IWorkspaceDialogService _workspaceDialogService;
    private readonly IAppAppearanceService _appearanceService;
    private readonly IEditorLayoutState _editorLayoutState;
    private readonly IChatViewModelFactory _chatViewModelFactory;
    private readonly INoteSearchService _noteSearchService;
    private readonly ObservableCollection<NoteSummary> _allNotes = [];
    private HashSet<DateTime> _calendarNoteDates = [];
    private readonly Guid _mutationOriginId = Guid.NewGuid();
    private IReadOnlyList<AppTheme> _allThemes = AppTheme.BuiltInThemes;
    private IReadOnlyList<BundledFontFamilyOption> _allFonts =
    [
        new(
            FontCatalogService.DefaultFontKey,
            "Iosevka Slab",
            $"avares://GroundNotes/Assets/Fonts/{FontCatalogService.DefaultFontKey}#Iosevka Slab",
            [new BundledFontVariantOption(FontCatalogService.DefaultVariantKey, FontCatalogService.DefaultVariantKey, FontWeight.Normal, FontStyle.Normal)])
    ];
    private CancellationTokenSource? _saveCts;
    private Task? _initializationTask;
    private bool _isApplyingSelection;
    private bool _hasInvalidYamlFrontMatter;
    private DateTimeOffset _suppressWatcherUntil = DateTimeOffset.MinValue;
    private List<TagFilterItemViewModel> _allTagFilters = [];

    [ObservableProperty]
    private ObservableCollection<NoteListItemViewModel> _visibleNotes = [];

    [ObservableProperty]
    private NoteSummary? _selectedNoteSummary;

    [ObservableProperty]
    private NoteListItemViewModel? _selectedVisibleNote;

    [ObservableProperty]
    private string _editorTitle = string.Empty;

    [ObservableProperty]
    private string _editorTags = string.Empty;

    [ObservableProperty]
    private string _editorBody = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowStructuredMetadataEditors))]
    [NotifyPropertyChangedFor(nameof(ShowEditorWatermark))]
    private bool _showYamlFrontMatterInEditor;

    [ObservableProperty]
    private bool _showScrollBars = true;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _availableTags = [];

    [ObservableProperty]
    private ObservableCollection<TagFilterItemViewModel> _availableTagFilters = [];

    [ObservableProperty]
    private string _tagFilterSearchText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TagFilterModeText))]
    private bool _matchAllSelectedTags;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TagFilterPanelMaxHeight))]
    [NotifyPropertyChangedFor(nameof(TagFilterPanelOpacity))]
    [NotifyPropertyChangedFor(nameof(TagFilterTriggerText))]
    private bool _isTagFilterExpanded;

    [ObservableProperty]
    private string _notesFolder = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Choose a folder to start.";

    [ObservableProperty]
    private string _lastSavedText = "GroundNotes";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SidebarToggleIcon))]
    private bool _sidebarCollapsed;

    public string SidebarToggleIcon => SidebarCollapsed ? "›" : "‹";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CalendarPanelMaxHeight))]
    [NotifyPropertyChangedFor(nameof(CalendarPanelOpacity))]
    private bool _isCalendarExpanded;

    [ObservableProperty]
    private DateTime _displayedCalendarMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveDateFilter))]
    [NotifyPropertyChangedFor(nameof(CalendarTriggerText))]
    private DateTime? _selectedCalendarDate;

    [ObservableProperty]
    private IReadOnlyList<CalendarDayViewModel> _visibleCalendarDays = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CalendarPanelMaxHeight))]
    private int _calendarWeekRowCount = 6;

    [ObservableProperty]
    private SortOption _selectedSortOption = SortOption.LastModified;

    [ObservableProperty]
    private bool _hasConflict;

    [ObservableProperty]
    private double _editorFontSize = DefaultEditorFontSize;

    [ObservableProperty]
    private double _uiFontSize = DefaultUiFontSize;

    [ObservableProperty]
    private int _editorIndentSize = DefaultEditorIndentSize;

    [ObservableProperty]
    private double _editorLineHeightFactor = DefaultEditorLineHeightFactor;

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

    [ObservableProperty]
    private bool _isGeneratingTitleSuggestions;

    [ObservableProperty]
    private IReadOnlyList<string> _titleSuggestions = [];

    [ObservableProperty]
    private bool _isTitleSuggestionsOpen;

    [ObservableProperty]
    private string _titleSuggestionsContext = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _tagSuggestions = [];

    [ObservableProperty]
    private string? _selectedTagSuggestion;

    [ObservableProperty]
    private bool _isTagSuggestionsOpen;

    public MainViewModel(
        INotesRepository notesRepository,
        ISettingsService settingsService,
        IFileWatcherService fileWatcherService,
        IThemeLoaderService themeLoaderService,
        IFontCatalogService fontCatalogService,
        IAiPromptCatalogService aiPromptCatalogService,
        IAiTextActionService aiTextActionService,
        IAiTitleSuggestionService aiTitleSuggestionService,
        INoteMutationService noteMutationService,
        IWorkspaceDialogService workspaceDialogService,
        IAppAppearanceService appearanceService,
        IEditorLayoutState editorLayoutState,
        IChatViewModelFactory chatViewModelFactory,
        INoteSearchServiceFactory noteSearchServiceFactory)
    {
        _notesRepository = notesRepository;
        _settingsService = settingsService;
        _fileWatcherService = fileWatcherService;
        _themeLoaderService = themeLoaderService;
        _fontCatalogService = fontCatalogService;
        _aiPromptCatalogService = aiPromptCatalogService;
        _aiTextActionService = aiTextActionService;
        _aiTitleSuggestionService = aiTitleSuggestionService;
        _noteMutationService = noteMutationService;
        _workspaceDialogService = workspaceDialogService;
        _appearanceService = appearanceService;
        _editorLayoutState = editorLayoutState;
        _chatViewModelFactory = chatViewModelFactory;
        _noteSearchService = noteSearchServiceFactory.Create(() => _allNotes);
        _fileWatcherService.NoteChanged += OnNoteChanged;
        _noteMutationService.NoteMutated += OnNoteMutated;

        SortOptions = Enum.GetValues<SortOption>();
        RefreshCalendarDays();
    }

    public event EventHandler? FocusEditorRequested;

    public bool IsSettingsPreviewActive { get; private set; }

    public IReadOnlyList<SortOption> SortOptions { get; }

    public NoteDocument? CurrentNote { get; private set; }

    public bool HasUnsavedChanges { get; private set; }

    public bool HasSelectedFolder => !string.IsNullOrWhiteSpace(NotesFolder);

    public bool HasNotes => VisibleNotes.Count > 0;

    public bool HasAiPrompts => AiPrompts.Count > 0;

    public bool HasTitleSuggestions => TitleSuggestions.Count > 0;

    public bool CanEditTitleSuggestionsContext => !IsGeneratingTitleSuggestions;

    public bool HasActiveDateFilter => SelectedCalendarDate is not null;

    public double CalendarPanelMaxHeight => IsCalendarExpanded
        ? CalendarPanelChromeHeight + CalendarWeekHeaderHeight + (CalendarWeekRowCount * CalendarDayRowHeight)
        : 0;

    public double CalendarPanelOpacity => IsCalendarExpanded ? 1 : 0;

    public string CalendarHeaderText => DisplayedCalendarMonth.ToString("MMMM yyyy");

    public IReadOnlyList<string> CalendarWeekdayHeaders { get; } = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

    public string CalendarTriggerText => "Calendar";

    public string CalendarTriggerDateText => FormatCalendarTriggerDate(SelectedCalendarDate ?? DateTime.Today);

    public string CalendarTriggerChevron => IsCalendarExpanded ? "v" : ">";

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

    public bool ShowEditorWatermark => HasSelectedFolder && string.IsNullOrWhiteSpace(EditorBody) && !ShowYamlFrontMatterInEditor;

    public bool ShowStructuredMetadataEditors => !ShowYamlFrontMatterInEditor;

    public bool HasFooterStatusOverride => !string.IsNullOrWhiteSpace(StatusMessage) && !string.Equals(StatusMessage, "Ready.", StringComparison.Ordinal);

    public string FooterStatusText => HasFooterStatusOverride
        ? StatusMessage
        : LastSavedText;

    public string CurrentAiPromptsDirectory => !HasSelectedFolder
        ? string.Empty
        : _aiPromptCatalogService.GetNotesFolderPromptsDirectory(NotesFolder);


    partial void OnSearchTextChanged(string value) => RefreshVisibleNotes();

    partial void OnTagFilterSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(ShowSelectedTagFilters));
        OnPropertyChanged(nameof(ShowEmptySelectedTagHint));
        OnPropertyChanged(nameof(HasTagFilterSearchResults));
        RefreshAvailableTagFiltersView();
    }

    partial void OnMatchAllSelectedTagsChanged(bool value)
    {
        OnPropertyChanged(nameof(HasActiveTagFilter));
        OnPropertyChanged(nameof(TagFilterTriggerText));
        RefreshVisibleNotes();
    }

    public bool HasActiveTagFilter => SelectedTags.Count > 0;

    public IReadOnlyList<TagFilterItemViewModel> SelectedTagFilters => _allTagFilters
        .Where(tag => tag.IsSelected)
        .OrderByDescending(tag => tag.NoteCount)
        .ThenBy(tag => tag.Tag, StringComparer.OrdinalIgnoreCase)
        .ToList();

    public bool HasSelectedTagFilters => SelectedTagFilters.Count > 0;

    public bool ShowSelectedTagFilters => HasSelectedTagFilters && string.IsNullOrWhiteSpace(TagFilterSearchText);

    public bool ShowEmptySelectedTagHint => !HasSelectedTagFilters && string.IsNullOrWhiteSpace(TagFilterSearchText);

    public bool HasTagFilterSearchResults => !string.IsNullOrWhiteSpace(TagFilterSearchText) && AvailableTagFilters.Count > 0;

    public IReadOnlyList<string> SelectedTags => _allTagFilters
        .Where(tag => tag.IsSelected)
        .Select(tag => tag.Tag)
        .ToList();

    public string TagFilterModeText => MatchAllSelectedTags ? "All" : "Any";

    public double TagFilterPanelMaxHeight => IsTagFilterExpanded ? 196 : 0;

    public double TagFilterPanelOpacity => IsTagFilterExpanded ? 1 : 0;

    public string TagFilterTriggerText => "Tags";

    public string TagFilterTriggerBadgeText => SelectedTags.Count > 0 ? SelectedTags.Count.ToString() : string.Empty;

    public bool ShowTagFilterTriggerBadge => SelectedTags.Count > 0;

    public string TagFilterTriggerChevron => IsTagFilterExpanded ? "v" : ">";

    partial void OnSelectedSortOptionChanged(SortOption value) => RefreshVisibleNotes();

    partial void OnDisplayedCalendarMonthChanged(DateTime value)
    {
        OnPropertyChanged(nameof(CalendarHeaderText));
        RefreshCalendarDays();
    }

    partial void OnSelectedCalendarDateChanged(DateTime? value)
    {
        OnPropertyChanged(nameof(CalendarTriggerDateText));
        RefreshVisibleNotes();
        RefreshCalendarDays();
    }

    partial void OnIsCalendarExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(CalendarPanelMaxHeight));
        OnPropertyChanged(nameof(CalendarPanelOpacity));
        OnPropertyChanged(nameof(CalendarTriggerChevron));
    }

    private static string FormatCalendarTriggerDate(DateTime date)
    {
        return date.ToString("MMM d", CultureInfo.CurrentCulture);
    }

    partial void OnIsTagFilterExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(TagFilterPanelMaxHeight));
        OnPropertyChanged(nameof(TagFilterPanelOpacity));
        OnPropertyChanged(nameof(TagFilterTriggerText));
        OnPropertyChanged(nameof(TagFilterTriggerChevron));
    }

    partial void OnNotePickerQueryChanged(string value)
    {
        if (IsNotePickerOpen)
        {
            RefreshNotePickerResults();
        }
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

    partial void OnShowYamlFrontMatterInEditorChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowStructuredMetadataEditors));
        OnPropertyChanged(nameof(ShowEditorWatermark));
    }

    partial void OnVisibleNotesChanged(ObservableCollection<NoteListItemViewModel> value) => OnPropertyChanged(nameof(HasNotes));

    partial void OnNotePickerResultsChanged(ObservableCollection<NoteSummary> value) => OnPropertyChanged(nameof(HasNotePickerResults));

    partial void OnAiPromptsChanged(IReadOnlyList<AiPromptDefinition> value) => OnPropertyChanged(nameof(HasAiPrompts));

    partial void OnTitleSuggestionsChanged(IReadOnlyList<string> value) => OnPropertyChanged(nameof(HasTitleSuggestions));

    partial void OnIsGeneratingTitleSuggestionsChanged(bool value) => OnPropertyChanged(nameof(CanEditTitleSuggestionsContext));

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

    partial void OnSelectedVisibleNoteChanged(NoteListItemViewModel? value)
    {
        if (_isApplyingSelection)
        {
            return;
        }

        var nextSummary = value?.Summary;
        if (ReferenceEquals(SelectedNoteSummary, nextSummary))
        {
            return;
        }

        _isApplyingSelection = true;
        try
        {
            SelectedNoteSummary = nextSummary;
        }
        finally
        {
            _isApplyingSelection = false;
        }

        if (nextSummary is null || value?.IsRenaming != false)
        {
            return;
        }

        _ = LoadSelectedNoteAsync(nextSummary.FilePath);
    }

    partial void OnSelectedNoteSummaryChanged(NoteSummary? value)
    {
        DismissTitleSuggestions(clearContext: true);

        if (_isApplyingSelection)
        {
            return;
        }

        _isApplyingSelection = true;
        try
        {
            SelectedVisibleNote = value is null
                ? null
                : VisibleNotes.FirstOrDefault(note => string.Equals(note.FilePath, value.FilePath, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _isApplyingSelection = false;
        }

        if (value is null || SelectedVisibleNote?.IsRenaming == true)
        {
            return;
        }

        _ = LoadSelectedNoteAsync(value.FilePath);
    }

    partial void OnEditorTitleChanged(string value)
    {
        OnPropertyChanged(nameof(ShowTitleWatermark));
        ClearTransientStatusOnEdit();
        DismissTitleSuggestions(clearContext: false);

        if (_isApplyingSelection || !HasSelectedFolder || CurrentNote is null)
        {
            return;
        }

        if (!UpdateCurrentNoteFromEditor())
        {
            return;
        }

        ScheduleSave();
    }

    partial void OnEditorTagsChanged(string value)
    {
        OnPropertyChanged(nameof(ShowTagsWatermark));
    }

    partial void OnEditorBodyChanged(string value)
    {
        OnPropertyChanged(nameof(ShowEditorWatermark));
        ClearTransientStatusOnEdit();
        DismissTitleSuggestions(clearContext: false);

        if (_isApplyingSelection || !HasSelectedFolder)
        {
            return;
        }

        if (!EnsureDraftExists(value))
        {
            return;
        }

        if (!UpdateCurrentNoteFromEditor())
        {
            return;
        }

        ScheduleSave();
    }

    [RelayCommand]
    private async Task ChooseFolderAsync()
    {
        var chosenFolder = await _workspaceDialogService.PickFolderAsync();
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

        if (!await CanLeaveCurrentEditorStateAsync())
        {
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
        FocusEditorRequested?.Invoke(this, new FocusEditorRequestEventArgs { MoveCaretToEndOfBody = true });
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
    }

    [RelayCommand]
    private void ToggleSidebar()
    {
        SidebarCollapsed = !SidebarCollapsed;
    }

    [RelayCommand]
    private void ClearTagFilter()
    {
        TagFilterSearchText = string.Empty;
        SetSelectedTags([]);
    }

    internal void UpdateTagSuggestions(int caretIndex)
    {
        if (!HasSelectedFolder)
        {
            DismissTagSuggestions();
            return;
        }

        var suggestions = TagSuggestionHelper.GetSuggestions(EditorTags, caretIndex, AvailableTags);
        TagSuggestions = new ObservableCollection<string>(suggestions);
        SelectedTagSuggestion = TagSuggestions.FirstOrDefault();
        IsTagSuggestionsOpen = TagSuggestions.Count > 0;
    }

    internal void DismissTagSuggestions()
    {
        IsTagSuggestionsOpen = false;
        TagSuggestions = [];
        SelectedTagSuggestion = null;
    }

    internal bool SelectNextTagSuggestion(int direction)
    {
        if (TagSuggestions.Count == 0)
        {
            return false;
        }

        var currentIndex = SelectedTagSuggestion is null
            ? -1
            : TagSuggestions.IndexOf(SelectedTagSuggestion);

        var nextIndex = currentIndex + direction;
        if (nextIndex < 0)
        {
            nextIndex = TagSuggestions.Count - 1;
        }
        else if (nextIndex >= TagSuggestions.Count)
        {
            nextIndex = 0;
        }

        SelectedTagSuggestion = TagSuggestions[nextIndex];
        return true;
    }

    internal bool TryApplySelectedTagSuggestion(int caretIndex, out int nextCaretIndex)
    {
        if (string.IsNullOrWhiteSpace(SelectedTagSuggestion))
        {
            nextCaretIndex = caretIndex;
            return false;
        }

        var result = TagSuggestionHelper.ApplySuggestion(EditorTags, caretIndex, SelectedTagSuggestion);
        EditorTags = result.Text;
        nextCaretIndex = result.CaretIndex;
        DismissTagSuggestions();
        return true;
    }

    internal async Task CommitEditorTagsAsync()
    {
        DismissTagSuggestions();

        if (_isApplyingSelection || !HasSelectedFolder || ShowYamlFrontMatterInEditor)
        {
            return;
        }

        var committedTags = ParseTags(EditorTags);
        var normalizedText = string.Join(", ", committedTags);

        if (!string.Equals(EditorTags, normalizedText, StringComparison.Ordinal))
        {
            _isApplyingSelection = true;
            try
            {
                EditorTags = normalizedText;
            }
            finally
            {
                _isApplyingSelection = false;
            }
        }

        if (CurrentNote is null)
        {
            if (committedTags.Count > 0)
            {
                StatusMessage = "Tags are ready. Start the note body, then they can be saved.";
            }

            return;
        }

        if (CurrentNote.Tags.SequenceEqual(committedTags, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        ClearTransientStatusOnEdit();
        DismissTitleSuggestions(clearContext: false);
        CurrentNote.Tags = committedTags;
        HasUnsavedChanges = true;
        await SaveCurrentNoteAsync(CancellationToken.None);
    }

    [RelayCommand]
    private async Task ConfirmEditorTagsAsync()
    {
        await CommitEditorTagsAsync();
    }

    private void SetSelectedTags(IReadOnlyList<string> tags)
    {
        var selectedTags = tags.ToHashSet(StringComparer.OrdinalIgnoreCase);

        _isApplyingSelection = true;
        try
        {
            foreach (var tagFilter in AvailableTagFilters)
            {
                tagFilter.IsSelected = selectedTags.Contains(tagFilter.Tag);
            }
        }
        finally
        {
            _isApplyingSelection = false;
        }

        OnTagFilterSelectionChanged();
    }

    private void OnTagFilterSelectionChanged()
    {
        if (_isApplyingSelection)
        {
            return;
        }

        _allTagFilters = _allTagFilters
            .OrderByDescending(tag => tag.IsSelected)
            .ThenByDescending(tag => tag.NoteCount)
            .ThenBy(tag => tag.Tag, StringComparer.OrdinalIgnoreCase)
            .ToList();
        RefreshAvailableTagFiltersView();

        OnPropertyChanged(nameof(HasActiveTagFilter));
        OnPropertyChanged(nameof(SelectedTags));
        OnPropertyChanged(nameof(SelectedTagFilters));
        OnPropertyChanged(nameof(HasSelectedTagFilters));
        OnPropertyChanged(nameof(ShowSelectedTagFilters));
        OnPropertyChanged(nameof(ShowEmptySelectedTagHint));
        OnPropertyChanged(nameof(HasTagFilterSearchResults));
        OnPropertyChanged(nameof(TagFilterTriggerText));
        OnPropertyChanged(nameof(TagFilterTriggerBadgeText));
        OnPropertyChanged(nameof(ShowTagFilterTriggerBadge));
        RefreshVisibleNotes();
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
    private void StartRenameNote(NoteListItemViewModel? noteItem)
    {
        if (noteItem is null)
        {
            return;
        }

        CancelInlineRename();
        noteItem.RenameText = noteItem.DisplayName;
        noteItem.IsRenaming = true;

        _isApplyingSelection = true;
        try
        {
            SelectedVisibleNote = noteItem;
            SelectedNoteSummary = noteItem.Summary;
        }
        finally
        {
            _isApplyingSelection = false;
        }
    }

    [RelayCommand]
    private async Task DeleteNoteAsync(NoteListItemViewModel? noteItem)
    {
        if (noteItem is null)
        {
            return;
        }

        CancelInlineRename();

        var displayName = noteItem.DisplayName;
        var shouldDelete = await _workspaceDialogService.ConfirmDeleteAsync(displayName);
        if (!shouldDelete)
        {
            StatusMessage = "Delete canceled.";
            return;
        }

        CancelScheduledSave();
        SuppressWatcher();
        using (BeginMutationScope())
        {
            await _noteMutationService.DeleteIfExistsAsync(noteItem.FilePath);
        }
        StatusMessage = $"Deleted {displayName}";
    }

    [RelayCommand]
    private async Task DeleteCurrentNoteAsync()
    {
        NoteListItemViewModel? noteItem = null;

        if (CurrentNote is not null)
        {
            noteItem = VisibleNotes.FirstOrDefault(note => string.Equals(note.FilePath, CurrentNote.FilePath, StringComparison.OrdinalIgnoreCase))
                ?? new NoteListItemViewModel(BuildSummary(CurrentNote));
        }
        else if (SelectedVisibleNote is not null)
        {
            noteItem = SelectedVisibleNote;
        }

        if (noteItem is null)
        {
            return;
        }

        await DeleteNoteAsync(noteItem);
    }

    public Task InitializeAsync()
    {
        return _initializationTask ??= InitializeAsyncCore();
    }

    private async Task InitializeAsyncCore()
    {
        var settings = await _settingsService.GetSettingsAsync();
        EditorFontSize = ClampEditorFontSize(settings.EditorFontSize ?? DefaultEditorFontSize);
        UiFontSize = ClampUiFontSize(settings.UiFontSize ?? DefaultUiFontSize);
        EditorIndentSize = EditorDisplaySettings.NormalizeIndentSize(settings.EditorIndentSize);
        EditorLineHeightFactor = EditorDisplaySettings.NormalizeLineHeightFactor(settings.EditorLineHeightFactor);
        ShowYamlFrontMatterInEditor = settings.ShowYamlFrontMatterInEditor;
        ShowScrollBars = settings.ShowScrollBars;

        _allFonts = _fontCatalogService.LoadBundledFonts();
        FontFamilyNames = _allFonts.Select(f => f.DisplayName).ToList();

        _allThemes = await _themeLoaderService.LoadAllThemesAsync();
        ThemeNames = _allThemes.Select(t => t.Name).ToList();

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
            }
        }

        ApplyAiSettings(settings.AiSettings);
        var promptLoad = await LoadAiPromptsAsync();
        if (promptLoad.Warnings.Count > 0 && !HasSelectedFolder)
        {
            StatusMessage = BuildPromptLoadStatus(promptLoad);
        }

        var initialFont = FontResolutionHelper.FindByKey(_allFonts, settings.FontName)
            ?? FontResolutionHelper.FindByKey(_allFonts, FontCatalogService.DefaultFontKey)
            ?? _allFonts[0];
        var initialVariant = ResolveFontVariant(initialFont, settings.FontVariantName ?? string.Empty)
            ?? GetDefaultFontVariant(initialFont);
        ApplyFontSelection(initialFont, initialVariant, persist: false);

        var initialSidebarFont = FontResolutionHelper.FindByKey(_allFonts, settings.SidebarFontName)
            ?? FontResolutionHelper.FindByKey(_allFonts, FontCatalogService.DefaultFontKey)
            ?? initialFont;
        var initialSidebarVariant = ResolveFontVariant(initialSidebarFont, settings.SidebarFontVariantName ?? string.Empty)
            ?? GetDefaultFontVariant(initialSidebarFont);
        ApplySidebarFontSelection(initialSidebarFont, initialSidebarVariant, persist: false);

        var initialCodeFont = FontResolutionHelper.FindByKey(_allFonts, settings.CodeFontName)
            ?? FontResolutionHelper.FindByKey(_allFonts, FontCatalogService.DefaultCodeFontKey)
            ?? initialFont;
        var initialCodeVariant = ResolveFontVariant(initialCodeFont, settings.CodeFontVariantName ?? string.Empty)
            ?? GetDefaultFontVariant(initialCodeFont);
        ApplyCodeFontSelection(initialCodeFont, initialCodeVariant, persist: false);

        if (!string.IsNullOrWhiteSpace(settings.NotesFolder))
        {
            await SetFolderAsync(settings.NotesFolder, focusEditorWhenReady: true);
        }
    }

    private async Task SetFolderAsync(string folderPath, bool focusEditorWhenReady = false)
    {
        if (!await CanLeaveCurrentEditorStateAsync())
        {
            return;
        }

        Directory.CreateDirectory(folderPath);
        SelectedCalendarDate = null;
        DisplayedCalendarMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        NotesFolder = folderPath;
        await PersistSettingsAsync(settings => settings with { NotesFolder = folderPath });
        _fileWatcherService.Watch(folderPath);
        ClearEditor();
        LastSavedText = "GroundNotes";
        await RefreshFromDiskAsync();
        var promptLoad = await LoadAiPromptsAsync();
        StatusMessage = promptLoad.Warnings.Count > 0
            ? BuildPromptLoadStatus(promptLoad)
            : "Ready.";

        if (focusEditorWhenReady)
        {
            FocusEditorRequested?.Invoke(this, new FocusEditorRequestEventArgs { MoveCaretToEndOfBody = true });
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
        await PersistSettingsAsync(settings => settings with { EditorFontSize = clamped });
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
        await PersistSettingsAsync(settings => settings with { UiFontSize = clamped });
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
    public void Dispose()
    {
        _fileWatcherService.NoteChanged -= OnNoteChanged;
        _noteMutationService.NoteMutated -= OnNoteMutated;
        _fileWatcherService.Dispose();
        CancelScheduledSave();
    }

    private Task PersistSettingsAsync(Func<AppSettings, AppSettings> update)
    {
        return _settingsService.UpdateSettingsAsync(update);
    }
}
