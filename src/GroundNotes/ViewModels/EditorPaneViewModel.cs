using CommunityToolkit.Mvvm.ComponentModel;
using GroundNotes.Models;

namespace GroundNotes.ViewModels;

public sealed partial class EditorPaneViewModel : ViewModelBase, IDisposable
{
    public EditorPaneViewModel()
    {
        Id = Guid.NewGuid();
    }

    public Guid Id { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowTitleWatermark))]
    [NotifyPropertyChangedFor(nameof(ShowTagsWatermark))]
    [NotifyPropertyChangedFor(nameof(ShowEditorWatermark))]
    private bool _hasSelectedFolder;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowStructuredMetadataEditors))]
    [NotifyPropertyChangedFor(nameof(ShowEditorWatermark))]
    private bool _showYamlFrontMatterInEditor;

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private NoteDocument? _currentNote;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowTitleWatermark))]
    private string _editorTitle = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowTagsWatermark))]
    private string _editorTags = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEditorWatermark))]
    private string _editorBody = string.Empty;

    [ObservableProperty]
    private bool _hasUnsavedChanges;

    [ObservableProperty]
    private bool _hasConflict;

    [ObservableProperty]
    private string _lastSavedText = "GroundNotes";

    public bool IsApplyingSelection { get; set; }

    public bool HasInvalidYamlFrontMatter { get; set; }

    public CancellationTokenSource? SaveCts { get; set; }

    public bool ShowTitleWatermark => HasSelectedFolder && string.IsNullOrWhiteSpace(EditorTitle);

    public bool ShowTagsWatermark => HasSelectedFolder && string.IsNullOrWhiteSpace(EditorTags);

    public bool ShowEditorWatermark => HasSelectedFolder && string.IsNullOrWhiteSpace(EditorBody) && !ShowYamlFrontMatterInEditor;

    public bool ShowStructuredMetadataEditors => !ShowYamlFrontMatterInEditor;

    public void Dispose()
    {
        SaveCts?.Cancel();
        SaveCts?.Dispose();
        SaveCts = null;
    }
}
