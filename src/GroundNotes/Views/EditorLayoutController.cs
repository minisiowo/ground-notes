using AvaloniaEdit;
using AvaloniaEdit.Document;
using GroundNotes.Models;

namespace GroundNotes.Views;

internal sealed class EditorLayoutController
{
    private readonly TextEditor _editor;
    private EditorLayoutSettings _currentSettings;
    private bool _hasAppliedInitialLayout;

    public EditorLayoutController(TextEditor editor)
    {
        _editor = editor;
        _currentSettings = new EditorLayoutSettings(
            editor.Options.IndentationSize,
            editor.Options.LineHeightFactor);
    }

    public void ApplyInitialLayout(EditorLayoutSettings settings)
    {
        var normalized = EditorLayoutSettings.Normalize(settings);
        ApplyOptions(normalized);
        ForceFullRebuild();
        _hasAppliedInitialLayout = true;
        _currentSettings = normalized;
    }

    public void ApplyRuntimeLayout(EditorLayoutSettings settings)
    {
        var normalized = EditorLayoutSettings.Normalize(settings);
        if (_hasAppliedInitialLayout && normalized.Equals(_currentSettings))
        {
            return;
        }

        ApplyOptions(normalized);
        RefreshLayout();
        _currentSettings = normalized;
    }

    internal void RefreshLayout()
    {
        var textView = _editor.TextArea.TextView;
        textView.InvalidateMeasure();
        textView.InvalidateArrange();
        textView.InvalidateVisual();
        textView.Redraw();
        textView.EnsureVisualLines();
        _editor.InvalidateMeasure();
        _editor.InvalidateArrange();
        _editor.InvalidateVisual();
    }

    private void ApplyOptions(EditorLayoutSettings settings)
    {
        _editor.Options.IndentationSize = settings.IndentationSize;
        _editor.Options.LineHeightFactor = settings.LineHeightFactor;
    }

    private void ForceFullRebuild()
    {
        var document = _editor.Document;
        if (document is null)
        {
            RefreshLayout();
            return;
        }

        var text = document.Text;
        var selectionStart = _editor.SelectionStart;
        var selectionLength = _editor.SelectionLength;
        var caretOffset = _editor.CaretOffset;

        _editor.Document = new TextDocument(text);
        RefreshLayout();

        var textLength = _editor.Document.TextLength;
        var clampedSelectionStart = Math.Clamp(selectionStart, 0, textLength);
        var clampedSelectionLength = Math.Clamp(selectionLength, 0, textLength - clampedSelectionStart);
        _editor.Select(clampedSelectionStart, clampedSelectionLength);
        _editor.CaretOffset = Math.Clamp(caretOffset, 0, textLength);
    }
}
