namespace QuickNotesTxt.ViewModels;

public sealed class FocusEditorRequestEventArgs : EventArgs
{
    public bool MoveCaretToEndOfBody { get; init; }
}
