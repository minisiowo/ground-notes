using AvaloniaEdit;
using QuickNotesTxt.Editors;

namespace QuickNotesTxt.Views;

internal sealed class EditorHostController : IDisposable
{
    private readonly EditorThemeController _themeController;
    private readonly EditorTextSyncController _textSyncController;

    public EditorHostController(TextEditor editor, MarkdownColorizingTransformer colorizer)
    {
        _themeController = new EditorThemeController(editor, colorizer);
        _textSyncController = new EditorTextSyncController(editor);
    }

    public bool IsUpdatingEditorFromViewModel => _textSyncController.IsUpdatingEditorFromViewModel;

    public bool IsUpdatingViewModelFromEditor => _textSyncController.IsUpdatingViewModelFromEditor;

    public string GetText() => _textSyncController.GetText();

    public void ApplySelectionTheme() => _themeController.ApplySelectionTheme();

    public void RefreshThemeResources() => _themeController.RefreshThemeResources();

    public bool SyncFromViewModel(string? text, bool appendSuffixWhenPossible, out bool appendedOnly)
        => _textSyncController.SyncFromViewModel(text, appendSuffixWhenPossible, out appendedOnly);

    public bool SyncToViewModel(Func<string> getViewModelText, Action<string> setViewModelText)
        => _textSyncController.SyncToViewModel(getViewModelText, setViewModelText);

    public void Dispose()
    {
        _themeController.Dispose();
    }
}
