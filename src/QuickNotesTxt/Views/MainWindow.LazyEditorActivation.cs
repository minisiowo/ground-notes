using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Editing;
using QuickNotesTxt.ViewModels;

namespace QuickNotesTxt.Views;

public partial class MainWindow
{
    /// <summary>
    /// When we insert from <see cref="OnWindowTunnelKeyDownForLazyEditor"/>, a matching single-character
    /// <see cref="TextInputEventArgs"/> can still follow; skip the duplicate insert.
    /// </summary>
    private bool _lazyKeyDownInserted;

    private void OnWindowTunnelKeyDownForLazyEditor(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        if (!ShouldStealTypingForMainEditor(vm, e.Source as Visual))
        {
            return;
        }

        var mods = e.KeyModifiers;
        if (mods.HasFlag(KeyModifiers.Control) || mods.HasFlag(KeyModifiers.Alt) || mods.HasFlag(KeyModifiers.Meta))
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            _lazyKeyDownInserted = true;
            ActivateEditorAtEndAndInsertText("\n");
            ScheduleLazyKeyDownFlagReset();
            return;
        }

        if (e.Key == Key.Tab)
        {
            e.Handled = true;
            _lazyKeyDownInserted = true;
            ActivateEditorAtEndAndInsertText("\t");
            ScheduleLazyKeyDownFlagReset();
            return;
        }

        if (TryGetTypingTextFromKey(e.Key, mods, out var text) && text.Length > 0)
        {
            e.Handled = true;
            _lazyKeyDownInserted = true;
            ActivateEditorAtEndAndInsertText(text);
            ScheduleLazyKeyDownFlagReset();
        }
    }

    private void OnWindowTunnelTextInputForLazyEditor(object? sender, TextInputEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        if (!ShouldStealTypingForMainEditor(vm, e.Source as Visual))
        {
            return;
        }

        if (string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        if (_lazyKeyDownInserted && e.Text.Length == 1)
        {
            e.Handled = true;
            _lazyKeyDownInserted = false;
            return;
        }

        e.Handled = true;
        ActivateEditorAtEndAndInsertText(e.Text);
    }

    private void ScheduleLazyKeyDownFlagReset()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(
            () => _lazyKeyDownInserted = false,
            DispatcherPriority.Input);
    }

    private bool ShouldStealTypingForMainEditor(MainViewModel vm, Visual? eventSource)
    {
        if (!vm.HasSelectedFolder)
        {
            return false;
        }

        if (vm.IsNotePickerOpen)
        {
            return false;
        }

        if (EditorTextEditor.IsKeyboardFocusWithin)
        {
            return false;
        }

        if (FocusManager?.GetFocusedElement() is Button)
        {
            return false;
        }

        if (eventSource is null)
        {
            return true;
        }

        return !IsTypingInExcludedTextSink(eventSource);
    }

    private bool IsTypingInExcludedTextSink(Visual source)
    {
        if (source.FindAncestorOfType<TextEditor>() is not null)
        {
            return true;
        }

        if (source.FindAncestorOfType<TextArea>() is not null)
        {
            return true;
        }

        if (source.FindAncestorOfType<ComboBox>() is not null)
        {
            return true;
        }

        if (source.FindAncestorOfType<TextBox>() is TextBox tb)
        {
            if (ReferenceEquals(tb, SidebarSearchTextBox))
            {
                return true;
            }

            if (ReferenceEquals(tb, EditorTitleTextBox))
            {
                return true;
            }

            if (ReferenceEquals(tb, EditorTagsTextBox))
            {
                return true;
            }

            if (ReferenceEquals(tb, NotePickerSearchTextBox))
            {
                return true;
            }

            if (NotesListBox is not null
                && tb.FindAncestorOfType<ListBox>() is ListBox lb
                && ReferenceEquals(lb, NotesListBox))
            {
                return true;
            }
        }

        return false;
    }

    private void ActivateEditorAtEndAndInsertText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        text = text.Replace("\r\n", "\n").Replace('\r', '\n');

        var doc = EditorTextEditor.Document;
        if (doc is null)
        {
            return;
        }

        var end = doc.TextLength;
        doc.Insert(end, text);
        var newEnd = end + text.Length;
        EditorTextEditor.CaretOffset = newEnd;
        EditorTextEditor.Select(newEnd, 0);

        // Focus/activate in a later dispatcher pass so the tunneling key event finishes first (otherwise
        // the sidebar ListBox can keep keyboard focus and the caret stays hidden). AvaloniaEdit draws
        // the caret on TextArea; focus that explicitly and scroll the caret into view.
        Dispatcher.UIThread.Post(() =>
        {
            Dispatcher.UIThread.Post(ApplyWindowAndEditorFocus, DispatcherPriority.Input);
        }, DispatcherPriority.Render);
    }

    private void ApplyWindowAndEditorFocus()
    {
        Activate();

        var textArea = EditorTextEditor.TextArea;
        textArea.Focus(NavigationMethod.Unspecified, KeyModifiers.None);

        var caretOffset = Math.Clamp(EditorTextEditor.CaretOffset, 0, EditorTextEditor.Document?.TextLength ?? 0);
        EditorTextEditor.CaretOffset = caretOffset;
        EditorTextEditor.Select(caretOffset, 0);
        textArea.Caret.BringCaretToView();

        _slashCommandPopup.ScheduleRefresh();
    }

    /// <summary>
    /// Maps physical keys to typed text for a US QWERTY layout (Shift variants for punctuation row).
    /// Non-US layouts may differ; IME and multi-codepoint input use <see cref="TextInputEventArgs"/> instead.
    /// </summary>
    private static bool TryGetTypingTextFromKey(Key key, KeyModifiers modifiers, out string text)
    {
        text = string.Empty;
        var shift = modifiers.HasFlag(KeyModifiers.Shift);

        if (key is >= Key.A and <= Key.Z)
        {
            var c = (char)('a' + (key - Key.A));
            if (shift)
            {
                c = char.ToUpperInvariant(c);
            }

            text = c.ToString();
            return true;
        }

        if (key is >= Key.D0 and <= Key.D9)
        {
            const string shiftDigits = ")!@#$%^&*(";
            text = shift
                ? shiftDigits[(int)(key - Key.D0)].ToString()
                : ((char)('0' + (key - Key.D0))).ToString();
            return true;
        }

        if (key == Key.Space)
        {
            text = " ";
            return true;
        }

        switch (key)
        {
            case Key.OemMinus:
                text = shift ? "_" : "-";
                return true;
            case Key.OemPlus:
                text = shift ? "+" : "=";
                return true;
            case Key.OemOpenBrackets:
                text = shift ? "{" : "[";
                return true;
            case Key.OemCloseBrackets:
                text = shift ? "}" : "]";
                return true;
            case Key.OemPipe:
                text = shift ? "|" : "\\";
                return true;
            case Key.OemSemicolon:
                text = shift ? ":" : ";";
                return true;
            case Key.OemQuotes:
                text = shift ? "\"" : "'";
                return true;
            case Key.OemComma:
                text = shift ? "<" : ",";
                return true;
            case Key.OemPeriod:
                text = shift ? ">" : ".";
                return true;
            case Key.OemQuestion:
                text = shift ? "?" : "/";
                return true;
            case Key.OemTilde:
                text = shift ? "~" : "`";
                return true;
            default:
                return false;
        }
    }
}
