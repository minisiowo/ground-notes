using Avalonia;
using Avalonia.Controls.Primitives;
using AvaloniaEdit;
using GroundNotes.Editors;
using GroundNotes.Editors.Vim;
using GroundNotes.Models;

namespace GroundNotes.Views;

internal sealed class EditorHostController : IDisposable
{
    private readonly TextEditor _editor;
    private readonly EditorThemeController _themeController;
    private readonly EditorTextSyncController _textSyncController;
    private readonly EditorLayoutController _layoutController;
    private readonly VimEditorController _vimController;
    private readonly EditorMarkdownListController _listController;

    public EditorHostController(
        TextEditor editor,
        MarkdownColorizingTransformer colorizer,
        Func<string, Task>? copyCodeBlockAsync = null,
        VimWorkspaceState? vimWorkspaceState = null)
    {
        _editor = editor;
        _themeController = new EditorThemeController(editor, colorizer, copyCodeBlockAsync);
        _textSyncController = new EditorTextSyncController(editor);
        _layoutController = new EditorLayoutController(editor);
        _vimController = new VimEditorController(editor, vimWorkspaceState ?? new VimWorkspaceState());
        _listController = new EditorMarkdownListController(editor, colorizer);
    }

    public event EventHandler<VimStatusChangedEventArgs>? VimStatusChanged
    {
        add => _vimController.StatusChanged += value;
        remove => _vimController.StatusChanged -= value;
    }

    public bool IsUpdatingEditorFromViewModel => _textSyncController.IsUpdatingEditorFromViewModel;

    public bool IsUpdatingViewModelFromEditor => _textSyncController.IsUpdatingViewModelFromEditor;

    public VimMode VimMode => _vimController.Mode;

    public string GetText() => _textSyncController.GetText();

    public void ApplySelectionTheme() => _themeController.ApplySelectionTheme();

    public void RefreshVisualResources() => _themeController.RefreshVisualResources();

    public void RefreshThemeResources() => RefreshVisualResources();

    public void RefreshTypographyResources() => _themeController.RefreshTypographyResources();

    public void ForceRefreshTypographyResources() => _themeController.ForceRefreshTypographyResources();

    public MarkdownImagePreviewHitTestResult? TryHitTestImagePreview(Point point) => _themeController.TryHitTestImagePreview(point);

    public MarkdownCodeBlockCopyHitTestResult? TryHitTestCodeBlockCopyButton(Point point) => _themeController.TryHitTestCodeBlockCopyButton(point);

    public void SetBaseDirectoryPath(string? baseDirectoryPath) => _themeController.SetBaseDirectoryPath(baseDirectoryPath);

    public void RefreshImagePreviews(string? resolvedImagePath = null) => _themeController.RefreshImagePreviews(resolvedImagePath);

    public IDisposable BeginContinuousResize() => _themeController.BeginContinuousResize();

    public void ApplyInitialLayout(EditorLayoutSettings settings) => _layoutController.ApplyInitialLayout(settings);

    public void ApplyRuntimeLayout(EditorLayoutSettings settings) => _layoutController.ApplyRuntimeLayout(settings);

    public void SetVimModeSettings(VimModeSettings settings) => _vimController.SetSettings(settings);

    public void SetPreVimKeyHandler(Func<Avalonia.Input.KeyEventArgs, bool>? handler) => _vimController.SetPreVimKeyHandler(handler);

    public void SetVimLeaderCommandHandler(Func<string, Task>? handler) => _vimController.SetLeaderCommandHandler(handler);

    public void ResetVimState() => _vimController.ResetState();

    public void SetDocumentDisplayMode(EditorDocumentDisplayMode mode)
    {
        var markdownFormattingEnabled = mode == EditorDocumentDisplayMode.Markdown;
        _themeController.SetMarkdownFormattingEnabled(markdownFormattingEnabled);
        _listController.SetMarkdownFormattingEnabled(markdownFormattingEnabled);
    }

    internal void RefreshLayoutAfterDocumentReplace()
    {
        _themeController.RefreshAfterDocumentReplace();
        _layoutController.RefreshLayout();
    }

    internal void ResetViewportToDocumentStart()
    {
        if (_editor.Document is null)
        {
            return;
        }

        _editor.CaretOffset = 0;
        _editor.Select(0, 0);

        if (_editor.TextArea.TextView is IScrollable scrollable)
        {
            scrollable.Offset = new Vector(0, 0);
        }
    }

    public bool SyncFromViewModel(string? text, bool appendSuffixWhenPossible, out bool appendedOnly)
        => _textSyncController.SyncFromViewModel(text, appendSuffixWhenPossible, out appendedOnly);

    public bool SyncToViewModel(Func<string> getViewModelText, Action<string> setViewModelText)
        => _textSyncController.SyncToViewModel(getViewModelText, setViewModelText);

    public void Dispose()
    {
        _listController.Dispose();
        _vimController.Dispose();
        _themeController.Dispose();
    }
}
