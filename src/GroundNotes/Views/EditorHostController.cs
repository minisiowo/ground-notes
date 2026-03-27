using AvaloniaEdit;
using GroundNotes.Editors;
using GroundNotes.Models;

namespace GroundNotes.Views;

internal sealed class EditorHostController : IDisposable
{
    private readonly EditorThemeController _themeController;
    private readonly EditorTextSyncController _textSyncController;
    private readonly EditorLayoutController _layoutController;
    private readonly EditorMarkdownListController _listController;

    public EditorHostController(TextEditor editor, MarkdownColorizingTransformer colorizer)
    {
        _themeController = new EditorThemeController(editor, colorizer);
        _textSyncController = new EditorTextSyncController(editor);
        _layoutController = new EditorLayoutController(editor);
        _listController = new EditorMarkdownListController(editor, colorizer);
    }

    public bool IsUpdatingEditorFromViewModel => _textSyncController.IsUpdatingEditorFromViewModel;

    public bool IsUpdatingViewModelFromEditor => _textSyncController.IsUpdatingViewModelFromEditor;

    public string GetText() => _textSyncController.GetText();

    public void ApplySelectionTheme() => _themeController.ApplySelectionTheme();

    public void RefreshVisualResources() => _themeController.RefreshVisualResources();

    public void RefreshThemeResources() => RefreshVisualResources();

    public void RefreshTypographyResources() => _themeController.RefreshTypographyResources();

    public void ForceRefreshTypographyResources() => _themeController.ForceRefreshTypographyResources();

    public void ApplyInitialLayout(EditorLayoutSettings settings) => _layoutController.ApplyInitialLayout(settings);

    public void ApplyRuntimeLayout(EditorLayoutSettings settings) => _layoutController.ApplyRuntimeLayout(settings);

    internal void RefreshLayoutAfterDocumentReplace() => _layoutController.RefreshLayout();

    public bool SyncFromViewModel(string? text, bool appendSuffixWhenPossible, out bool appendedOnly)
        => _textSyncController.SyncFromViewModel(text, appendSuffixWhenPossible, out appendedOnly);

    public bool SyncToViewModel(Func<string> getViewModelText, Action<string> setViewModelText)
        => _textSyncController.SyncToViewModel(getViewModelText, setViewModelText);

    public void Dispose()
    {
        _listController.Dispose();
        _themeController.Dispose();
    }
}
