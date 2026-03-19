using AvaloniaEdit;

namespace QuickNotesTxt.Views;

internal sealed class EditorTextSyncController
{
    private readonly TextEditor _editor;

    public EditorTextSyncController(TextEditor editor)
    {
        _editor = editor;
    }

    public bool IsUpdatingEditorFromViewModel { get; private set; }

    public bool IsUpdatingViewModelFromEditor { get; private set; }

    public string GetText()
    {
        return _editor.Document?.Text ?? string.Empty;
    }

    public bool SyncFromViewModel(string? text, bool appendSuffixWhenPossible, out bool appendedOnly)
    {
        appendedOnly = false;
        if (IsUpdatingViewModelFromEditor)
        {
            return false;
        }

        var document = _editor.Document;
        if (document is null)
        {
            return false;
        }

        text ??= string.Empty;
        var currentText = document.Text;
        if (string.Equals(currentText, text, StringComparison.Ordinal))
        {
            return false;
        }

        var caretOffset = Math.Min(_editor.CaretOffset, text.Length);

        IsUpdatingEditorFromViewModel = true;
        try
        {
            if (appendSuffixWhenPossible && text.StartsWith(currentText, StringComparison.Ordinal))
            {
                var suffix = text[currentText.Length..];
                if (suffix.Length > 0)
                {
                    document.Insert(document.TextLength, suffix);
                    appendedOnly = true;
                }
            }
            else
            {
                document.BeginUpdate();
                try
                {
                    document.Replace(0, document.TextLength, text);
                }
                finally
                {
                    document.EndUpdate();
                }

                _editor.CaretOffset = caretOffset;
                _editor.Select(caretOffset, 0);
            }
        }
        finally
        {
            IsUpdatingEditorFromViewModel = false;
        }

        return true;
    }

    public bool SyncToViewModel(Func<string> getViewModelText, Action<string> setViewModelText)
    {
        ArgumentNullException.ThrowIfNull(getViewModelText);
        ArgumentNullException.ThrowIfNull(setViewModelText);

        if (IsUpdatingEditorFromViewModel)
        {
            return false;
        }

        var text = GetText();
        if (string.Equals(getViewModelText(), text, StringComparison.Ordinal))
        {
            return false;
        }

        IsUpdatingViewModelFromEditor = true;
        try
        {
            setViewModelText(text);
        }
        finally
        {
            IsUpdatingViewModelFromEditor = false;
        }

        return true;
    }
}
