namespace GroundNotes.ViewModels;

public enum EditorPaneTarget
{
    Primary,
    Secondary
}

public sealed class FocusEditorRequestEventArgs : EventArgs
{
    public bool MoveCaretToEndOfBody { get; init; }

    public EditorPaneTarget Target { get; init; } = EditorPaneTarget.Primary;

    public Guid? PaneId { get; init; }
}
