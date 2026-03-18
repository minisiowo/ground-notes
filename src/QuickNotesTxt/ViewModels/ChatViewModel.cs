using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuickNotesTxt.Models;
using QuickNotesTxt.Services;

namespace QuickNotesTxt.ViewModels;

public partial class ChatViewModel : ViewModelBase
{
    private const string UserHeader = "ME:";
    private const string AssistantHeader = "CHAT RESPONSE:";

    private readonly IAiChatService _aiChatService;
    private readonly INotesRepository _notesRepository;
    private readonly ISettingsService _settingsService;
    private NoteDocument _chatDocument;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
    private string _inputText = string.Empty;

    [ObservableProperty]
    private string _editorBody = string.Empty;

    [ObservableProperty]
    private ObservableCollection<NoteSummary> _attachedNotes = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = "Ask anything about your notes.";

    [ObservableProperty]
    private string _selectedModel = "gpt-5.4-mini";

    [ObservableProperty]
    private IReadOnlyList<string> _availableModels = ["gpt-5.4", "gpt-5.4-mini", "gpt-5.4-nano"];

    public Func<string, IReadOnlyList<NoteSummary>>? SearchAllNotesFunc { get; set; }

    public ChatViewModel(
        IAiChatService aiChatService,
        INotesRepository notesRepository,
        ISettingsService settingsService,
        NoteDocument chatDocument,
        IEnumerable<NoteSummary>? initialNotes = null)
    {
        _aiChatService = aiChatService;
        _notesRepository = notesRepository;
        _settingsService = settingsService;
        _chatDocument = chatDocument;
        _editorBody = chatDocument.Body;

        if (initialNotes is not null)
        {
            foreach (var note in initialNotes)
            {
                AttachedNotes.Add(note);
            }
        }
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
        if (string.IsNullOrWhiteSpace(userText)) return;

        // 1. Append user message to editor
        var newBody = EditorBody;
        if (!string.IsNullOrWhiteSpace(newBody) && !newBody.EndsWith("\n\n"))
        {
            newBody += newBody.EndsWith("\n") ? "\n" : "\n\n";
        }
        newBody += $"{UserHeader}\n---\n{userText}\n\n";
        EditorBody = newBody;
        InputText = string.Empty;
        
        IsBusy = true;
        StatusMessage = "AI is thinking...";

        try
        {
            // 2. Build history from editor content
            var history = await BuildHistoryFromEditorAsync();
            var settings = await _settingsService.GetAiSettingsAsync();
            
            // 3. Get AI response
            var response = await _aiChatService.GetResponseAsync(history, settings, SelectedModel);
            
            // 4. Append AI response to editor
            EditorBody += $"{AssistantHeader}\n---\n{response}\n\n";
            
            // 5. Save the document
            _chatDocument = _chatDocument with { Body = EditorBody };
            await _notesRepository.SaveNoteAsync(Path.GetDirectoryName(_chatDocument.FilePath) ?? string.Empty, _chatDocument);
            
            StatusMessage = "Ready.";
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

    private bool CanSendMessage() => !IsBusy && !string.IsNullOrWhiteSpace(InputText);

    private async Task<IEnumerable<AiChatMessage>> BuildHistoryFromEditorAsync()
    {
        var messages = new List<AiChatMessage>();

        // A. System message with attached notes context
        var systemContent = new System.Text.StringBuilder();
        systemContent.AppendLine("You are a helpful assistant specialized in managing and answering questions about the user's personal notes.");
        systemContent.AppendLine("Use the provided note contents below as context for your answers when relevant.");
        
        if (AttachedNotes.Count > 0)
        {
            systemContent.AppendLine("\n--- ATTACHED NOTES CONTEXT ---");
            foreach (var noteSummary in AttachedNotes)
            {
                var note = await _notesRepository.LoadNoteAsync(noteSummary.FilePath);
                if (note is not null)
                {
                    systemContent.AppendLine($"\n[File: {noteSummary.DisplayName}]");
                    systemContent.AppendLine(note.Body);
                    systemContent.AppendLine("---");
                }
            }
        }
        messages.Add(new AiChatMessage("system", systemContent.ToString()));

        // B. Parse history from EditorBody
        var lines = EditorBody.Split('\n');
        string currentRole = "";
        var currentContent = new System.Text.StringBuilder();
        var skipNextSeparatorLine = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (skipNextSeparatorLine && trimmed == "---")
            {
                skipNextSeparatorLine = false;
                continue;
            }

            if (IsUserHeader(trimmed))
            {
                AddCurrentMessage(messages, currentRole, currentContent);
                currentRole = "user";
                currentContent.Clear();
                skipNextSeparatorLine = true;
            }
            else if (IsAssistantHeader(trimmed))
            {
                AddCurrentMessage(messages, currentRole, currentContent);
                currentRole = "assistant";
                currentContent.Clear();
                skipNextSeparatorLine = true;
            }
            else
            {
                skipNextSeparatorLine = false;
                currentContent.AppendLine(line);
            }
        }
        AddCurrentMessage(messages, currentRole, currentContent);

        return messages;
    }

    private void AddCurrentMessage(List<AiChatMessage> messages, string role, System.Text.StringBuilder content)
    {
        if (string.IsNullOrEmpty(role)) return;
        var text = content.ToString().Trim();
        if (string.IsNullOrEmpty(text)) return;
        messages.Add(new AiChatMessage(role, text));
    }

    private static bool IsUserHeader(string line)
    {
        return line is "ME:" or "Me:" or "**Me:**";
    }

    private static bool IsAssistantHeader(string line)
    {
        return line is "CHAT RESPONSE:" or "Chat Response:" or "**Chat Response:**";
    }

    [RelayCommand]
    private void AddNoteToContext(NoteSummary note)
    {
        if (note is null) return;
        if (AttachedNotes.Any(n => n.FilePath == note.FilePath)) return;
        AttachedNotes.Add(note);
    }

    public IReadOnlyList<NoteSummary> SearchNotes(string query)
    {
        return SearchAllNotesFunc?.Invoke(query) ?? [];
    }
}
