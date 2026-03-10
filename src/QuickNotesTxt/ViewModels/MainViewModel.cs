using System.Collections.ObjectModel;
using Avalonia.Threading;
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

    private readonly INotesRepository _notesRepository;
    private readonly ISettingsService _settingsService;
    private readonly IFileWatcherService _fileWatcherService;
    private readonly ObservableCollection<NoteSummary> _allNotes = [];
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
    private string _lastSavedText = "Last saved: --";

    [ObservableProperty]
    private bool _sidebarCollapsed;

    [ObservableProperty]
    private SortOption _selectedSortOption = SortOption.LastModified;

    [ObservableProperty]
    private bool _hasConflict;

    [ObservableProperty]
    private double _editorFontSize = DefaultEditorFontSize;

    [ObservableProperty]
    private string _selectedThemeName = AppTheme.Dark.Name;

    public IReadOnlyList<string> ThemeNames { get; } = AppTheme.BuiltInThemes.Select(t => t.Name).ToList();

    public MainViewModel(INotesRepository notesRepository, ISettingsService settingsService, IFileWatcherService fileWatcherService)
    {
        _notesRepository = notesRepository;
        _settingsService = settingsService;
        _fileWatcherService = fileWatcherService;
        _fileWatcherService.NotesChanged += OnNotesChanged;

        SortOptions = Enum.GetValues<SortOption>();
        _ = InitializeAsync();
    }

    public Func<Task<string?>>? PickFolderAsync { get; set; }

    public Func<string, Task<bool>>? ConfirmDeleteAsync { get; set; }

    public IReadOnlyList<SortOption> SortOptions { get; }

    public NoteDocument? CurrentNote { get; private set; }

    public bool HasUnsavedChanges { get; private set; }

    public bool HasSelectedFolder => !string.IsNullOrWhiteSpace(NotesFolder);

    public bool HasNotes => VisibleNotes.Count > 0;

    public bool ShowFolderPrompt => !HasSelectedFolder;

    public bool ShowTitleWatermark => HasSelectedFolder && string.IsNullOrWhiteSpace(EditorTitle);

    public bool ShowTagsWatermark => HasSelectedFolder && string.IsNullOrWhiteSpace(EditorTags);

    public bool ShowEditorWatermark => HasSelectedFolder && string.IsNullOrWhiteSpace(EditorBody);

    public bool HasFooterStatusOverride => !string.IsNullOrWhiteSpace(StatusMessage) && !string.Equals(StatusMessage, "Ready.", StringComparison.Ordinal);

    public string FooterStatusText => HasFooterStatusOverride ? StatusMessage : LastSavedText;


    partial void OnSearchTextChanged(string value) => RefreshVisibleNotes();

    partial void OnSelectedTagChanged(string? value)
    {
        OnPropertyChanged(nameof(HasActiveTagFilter));
        RefreshVisibleNotes();
    }

    public bool HasActiveTagFilter => !string.IsNullOrWhiteSpace(SelectedTag) && !string.Equals(SelectedTag, AllTagsFilter, StringComparison.Ordinal);

    partial void OnSelectedSortOptionChanged(SortOption value) => RefreshVisibleNotes();

    partial void OnSelectedThemeNameChanged(string value)
    {
        var theme = AppTheme.BuiltInThemes.FirstOrDefault(t => t.Name == value);
        if (theme is not null)
        {
            ThemeService.Apply(theme);
            if (!_isApplyingSelection)
            {
                _ = _settingsService.SetThemeNameAsync(value);
            }
        }
    }

    partial void OnNotesFolderChanged(string value)
    {
        OnPropertyChanged(nameof(HasSelectedFolder));
        OnPropertyChanged(nameof(ShowFolderPrompt));
        OnPropertyChanged(nameof(ShowTitleWatermark));
        OnPropertyChanged(nameof(ShowTagsWatermark));
        OnPropertyChanged(nameof(ShowEditorWatermark));
    }

    partial void OnVisibleNotesChanged(ObservableCollection<NoteSummary> value) => OnPropertyChanged(nameof(HasNotes));

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

        if (!string.IsNullOrWhiteSpace(settings.ThemeName))
        {
            var theme = AppTheme.BuiltInThemes.FirstOrDefault(t => t.Name == settings.ThemeName);
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
            await SetFolderAsync(settings.NotesFolder);
        }
    }

    private async Task SetFolderAsync(string folderPath)
    {
        Directory.CreateDirectory(folderPath);
        NotesFolder = folderPath;
        await _settingsService.SetNotesFolderAsync(folderPath);
        _fileWatcherService.Watch(folderPath);
        ClearEditor();
        LastSavedText = "Last saved: --";
        await RefreshFromDiskAsync();
        StatusMessage = "Ready.";
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

    private static double ClampEditorFontSize(double fontSize)
    {
        return Math.Clamp(fontSize, MinEditorFontSize, MaxEditorFontSize);
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
            LastSavedText = "Last saved: --";
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
            || status.StartsWith("Deleted ", StringComparison.Ordinal)
            || status.StartsWith("Renamed to ", StringComparison.Ordinal)
            || status.StartsWith("Editor font size: ", StringComparison.Ordinal);
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
        var preview = document.Body.ReplaceLineEndings(" ").Trim();
        if (preview.Length > 96)
        {
            preview = preview[..96] + "...";
        }

        return new NoteSummary
        {
            Id = document.FilePath,
            FilePath = document.FilePath,
            Title = document.Title,
            Tags = [.. document.Tags],
            CreatedAt = document.CreatedAt,
            UpdatedAt = document.UpdatedAt,
            Preview = preview,
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
}




