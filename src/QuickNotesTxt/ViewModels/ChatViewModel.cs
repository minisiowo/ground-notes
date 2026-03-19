using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuickNotesTxt.Models;
using QuickNotesTxt.Services;

namespace QuickNotesTxt.ViewModels;

public partial class ChatViewModel : ViewModelBase
{
    private const string UserHeader = "ME:";
    private const string AssistantHeader = "CHAT RESPONSE:";
    private const string LinkedNoteTag = "AI";

    private readonly IAiChatService _aiChatService;
    private readonly INotesRepository _notesRepository;
    private readonly ISettingsService _settingsService;
    private readonly INoteMutationService _noteMutationService;
    private readonly INoteSearchService _noteSearchService;
    private readonly string _notesFolder;
    private readonly NoteSummary? _originNote;
    private readonly DateTimeOffset _sessionStartedAt;
    private AiChatSession _session = new();
    private NoteDocument? _chatDocument;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
    private string _inputText = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveConversationCommand))]
    [NotifyCanExecuteChangedFor(nameof(AppendToFileCommand))]
    private string _editorBody = string.Empty;

    [ObservableProperty]
    private ObservableCollection<NoteSummary> _attachedNotes = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveConversationCommand))]
    [NotifyCanExecuteChangedFor(nameof(AppendToFileCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = "Ask anything about your notes.";

    [ObservableProperty]
    private string _selectedModel = AiSettings.Default.DefaultModel;

    [ObservableProperty]
    private IReadOnlyList<string> _availableModels = ["gpt-5.4", "gpt-5.4-mini", "gpt-5.4-nano"];

    [ObservableProperty]
    private ObservableCollection<NoteSummary> _mentionSuggestions = [];

    [ObservableProperty]
    private bool _isMentionPopupOpen;

    [ObservableProperty]
    private int _selectedMentionIndex = -1;

    public bool IsPersisted => _chatDocument is not null;

    public ChatViewModel(
        IAiChatService aiChatService,
        INotesRepository notesRepository,
        ISettingsService settingsService,
        INoteMutationService noteMutationService,
        INoteSearchService noteSearchService,
        string notesFolder,
        string defaultModel,
        IReadOnlyList<string> availableModels,
        NoteSummary? originNote = null,
        IEnumerable<NoteSummary>? initialNotes = null)
    {
        _aiChatService = aiChatService;
        _notesRepository = notesRepository;
        _settingsService = settingsService;
        _noteMutationService = noteMutationService;
        _noteSearchService = noteSearchService;
        _notesFolder = notesFolder;
        _originNote = originNote;
        _sessionStartedAt = DateTimeOffset.Now;
        _selectedModel = string.IsNullOrWhiteSpace(defaultModel) ? AiSettings.Default.DefaultModel : defaultModel;
        _availableModels = availableModels.Count == 0 ? ["gpt-5.4", "gpt-5.4-mini", "gpt-5.4-nano"] : availableModels;

        if (initialNotes is not null)
        {
            foreach (var note in initialNotes)
            {
                AttachedNotes.Add(note);
            }
        }
    }

    partial void OnEditorBodyChanged(string value)
    {
        OnPropertyChanged(nameof(IsPersisted));
    }

    public void UpdateMentionSuggestions(string text, int caretIndex)
    {
        text ??= string.Empty;

        if (TryResolveMentionQuery(text, caretIndex, out var query))
        {
            var results = _noteSearchService.Search(query);
            UpdateMentionPopupState(results);
            return;
        }

        DismissMentionPopup();
    }

    public void DismissMentionPopup()
    {
        MentionSuggestions = new ObservableCollection<NoteSummary>();
        SelectedMentionIndex = -1;
        IsMentionPopupOpen = false;
    }

    public void MoveMentionSelection(int delta)
    {
        if (!IsMentionPopupOpen || MentionSuggestions.Count == 0)
        {
            return;
        }

        var nextIndex = SelectedMentionIndex;
        if (nextIndex < 0 || nextIndex >= MentionSuggestions.Count)
        {
            nextIndex = 0;
        }

        nextIndex = (nextIndex + delta + MentionSuggestions.Count) % MentionSuggestions.Count;
        SelectedMentionIndex = nextIndex;
    }

    public bool TryAcceptMention(string text, int caretIndex, out string updatedText, out int updatedCaretIndex)
    {
        text ??= string.Empty;
        updatedText = text;
        updatedCaretIndex = caretIndex;

        if (!IsMentionPopupOpen || MentionSuggestions.Count == 0)
        {
            return false;
        }

        var selectedIndex = SelectedMentionIndex;
        if (selectedIndex < 0 || selectedIndex >= MentionSuggestions.Count)
        {
            DismissMentionPopup();
            return false;
        }

        var mentionNote = MentionSuggestions[selectedIndex];
        var lastAt = text.LastIndexOf('@', Math.Max(0, caretIndex - 1));
        if (lastAt < 0)
        {
            DismissMentionPopup();
            return false;
        }

        updatedText = text.Remove(lastAt, caretIndex - lastAt);
        updatedCaretIndex = lastAt;
        AddNoteToContext(mentionNote);
        DismissMentionPopup();
        return true;
    }

    [RelayCommand]
    private void RemoveAttachedNote(NoteSummary note)
    {
        AttachedNotes.Remove(note);
    }

    [RelayCommand(CanExecute = nameof(CanSendMessage))]
    private async Task SendMessageAsync()
    {
        var userText = InputText.Trim();
        if (string.IsNullOrWhiteSpace(userText))
        {
            return;
        }

        _session = _session.WithAppended("user", userText);
        RenderEditorBodyFromSession();
        InputText = string.Empty;

        IsBusy = true;
        StatusMessage = "AI is thinking...";

        try
        {
            var history = await BuildHistoryFromSessionAsync();
            var settings = await _settingsService.GetAiSettingsAsync();
            var response = await _aiChatService.GetResponseAsync(history, settings, SelectedModel);

            _session = _session.WithAppended("assistant", response);
            RenderEditorBodyFromSession();

            if (_chatDocument is not null)
            {
                await SaveExistingDocumentAsync();
                StatusMessage = "Ready. Conversation saved.";
            }
            else
            {
                StatusMessage = "Ready. Conversation is in editor; use Save As New to create a note.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSaveConversation))]
    private async Task SaveConversationAsync()
    {
        IsBusy = true;
        try
        {
            if (_chatDocument is null)
            {
                _chatDocument = CreateInitialChatDocument();
            }

            await SaveExistingDocumentAsync();
            StatusMessage = "Conversation saved.";
            OnPropertyChanged(nameof(IsPersisted));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanAppendToFile))]
    private async Task AppendToFileAsync()
    {
        if (_originNote is null)
        {
            StatusMessage = "Append is unavailable without an original note.";
            return;
        }

        IsBusy = true;
        try
        {
            var targetNote = await _notesRepository.LoadNoteAsync(_originNote.FilePath);
            if (targetNote is null)
            {
                throw new InvalidOperationException("The original note could not be loaded.");
            }

            var updatedNote = targetNote with { Body = AppendEditorBodyToNote(targetNote.Body, EditorBody) };
            var folderPath = Path.GetDirectoryName(targetNote.FilePath) ?? _notesFolder;
            await _noteMutationService.SaveAsync(folderPath, updatedNote);
            StatusMessage = $"Appended to {_originNote.DisplayName}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanSaveConversation()
    {
        if (IsBusy)
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(EditorBody) || _chatDocument is not null;
    }

    private bool CanAppendToFile()
    {
        return !IsBusy
            && _originNote is not null
            && !string.IsNullOrWhiteSpace(EditorBody);
    }

    private bool CanSendMessage()
    {
        return !IsBusy && !string.IsNullOrWhiteSpace(InputText);
    }

    private void UpdateMentionPopupState(IReadOnlyList<NoteSummary> suggestions)
    {
        if (suggestions.Count == 0)
        {
            DismissMentionPopup();
            return;
        }

        MentionSuggestions = new ObservableCollection<NoteSummary>(suggestions);
        SelectedMentionIndex = 0;
        IsMentionPopupOpen = true;
    }

    private NoteDocument CreateInitialChatDocument()
    {
        var draft = _notesRepository.CreateDraftNote(_notesFolder, _sessionStartedAt);
        var title = $"AI Chat ({_sessionStartedAt:yyyy-MM-dd HH:mm})";
        var tags = new List<string> { LinkedNoteTag };

        foreach (var summary in AttachedNotes)
        {
            foreach (var tag in summary.Tags)
            {
                if (!tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
                {
                    tags.Add(tag);
                }
            }
        }

        return draft with
        {
            Title = title,
            OriginalTitle = title,
            Tags = tags,
            Body = BuildStandaloneConversationBody()
        };
    }

    private void RenderEditorBodyFromSession()
    {
        var builder = new StringBuilder();
        for (var i = 0; i < _session.Messages.Count; i++)
        {
            var message = _session.Messages[i];
            if (i > 0)
            {
                builder.AppendLine();
            }

            builder.AppendLine(message.Role == "assistant" ? AssistantHeader : UserHeader);
            builder.AppendLine("---");
            builder.AppendLine(message.Content);
        }

        EditorBody = builder.ToString().TrimEnd();
    }

    private string BuildStandaloneConversationBody()
    {
        var body = EditorBody;
        var references = BuildLinkedNoteReferenceBlock();
        if (string.IsNullOrWhiteSpace(references))
        {
            return body;
        }

        if (body.StartsWith(references, StringComparison.Ordinal))
        {
            return body;
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return references;
        }

        return references + "\n\n" + body;
    }

    private string BuildLinkedNoteReferenceBlock()
    {
        var lines = AttachedNotes
            .Select(BuildLinkedNoteReferenceLine)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (lines.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.AppendLine("> Related notes:");
        foreach (var line in lines)
        {
            builder.Append("> - ");
            builder.AppendLine(line);
        }

        return builder.ToString().TrimEnd();
    }

    private static string AppendEditorBodyToNote(string existingBody, string editorBody)
    {
        if (string.IsNullOrWhiteSpace(existingBody))
        {
            return editorBody;
        }

        return existingBody.TrimEnd() + "\n\n" + editorBody;
    }

    private bool TryResolveMentionQuery(string text, int caretIndex, out string query)
    {
        text ??= string.Empty;
        query = string.Empty;

        if (caretIndex <= 0)
        {
            return false;
        }

        var lastAt = text.LastIndexOf('@', caretIndex - 1);
        if (lastAt < 0)
        {
            return false;
        }

        if (lastAt > 0 && !char.IsWhiteSpace(text[lastAt - 1]))
        {
            return false;
        }

        query = text.Substring(lastAt + 1, caretIndex - (lastAt + 1));
        return true;
    }

    private static string BuildLinkedNoteReferenceLine(NoteSummary summary)
    {
        var displayName = summary.DisplayName;
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return string.Empty;
        }

        var pathWithoutExtension = Path.ChangeExtension(summary.FilePath, null) ?? displayName;
        var normalizedPath = pathWithoutExtension.Replace('\\', '/');
        return string.Format(CultureInfo.InvariantCulture, "[[{0}]] ({1})", displayName, normalizedPath);
    }

    private async Task SaveExistingDocumentAsync()
    {
        if (_chatDocument is null)
        {
            return;
        }

        _chatDocument = _chatDocument with { Body = BuildStandaloneConversationBody() };
        _chatDocument = await _noteMutationService.SaveAsync(_notesFolder, _chatDocument);
        EditorBody = _chatDocument.Body;
    }

    private async Task<IReadOnlyList<AiChatMessage>> BuildHistoryFromSessionAsync()
    {
        var messages = new List<AiChatMessage>();

        var systemContent = new StringBuilder();
        systemContent.AppendLine("You are a helpful assistant specialized in managing and answering questions about the user's personal notes.");
        systemContent.AppendLine("Use the provided note contents below as context for your answers when relevant.");

        if (AttachedNotes.Count > 0)
        {
            systemContent.AppendLine("\n--- ATTACHED NOTES CONTEXT ---");
            foreach (var noteSummary in AttachedNotes)
            {
                var note = await _notesRepository.LoadNoteAsync(noteSummary.FilePath);
                if (note is null)
                {
                    continue;
                }

                systemContent.AppendLine($"\n[File: {noteSummary.DisplayName}]");
                systemContent.AppendLine(note.Body);
                systemContent.AppendLine("---");
            }
        }

        messages.Add(new AiChatMessage("system", systemContent.ToString()));
        messages.AddRange(_session.Messages);
        return messages;
    }

    [RelayCommand]
    private void AddNoteToContext(NoteSummary note)
    {
        if (note is null)
        {
            return;
        }

        if (AttachedNotes.Any(n => n.FilePath == note.FilePath))
        {
            return;
        }

        AttachedNotes.Add(note);
    }
}
